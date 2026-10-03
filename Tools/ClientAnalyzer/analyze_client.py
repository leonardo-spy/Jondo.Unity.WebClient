"""
Client Analyzer — Extracts ALL system data from the Dofus 3 IL2CPP client.

Sources:
  - dump.cs        (1.98M lines) — type definitions, fields, consts, method VAs
  - decompiled_all.c (964MB)     — pseudo-C function bodies from Ghidra
  - stringliteral.json           — all string literals with addresses
  - script.json                  — method addresses for cross-referencing

Outputs:
  - client_types.json       — complete type hierarchy (all 40k types)
  - visual_elements.json    — VisualElement subtypes with inheritance, fields, consts
  - constructors.json       — extracted constructor behavior (children, USS classes, manipulators)
  - asset_paths.json        — all asset path constants from AssetsPaths and similar
  - screen_registry.json    — screen handlers, bindings, UXML paths
  - service_graph.json      — service types, dependencies, [ServiceInit] methods
  - ClientData.json         — synthesized runtime data for WebGLProbe

Usage:
  py analyze_client.py                          — run all phases
  py analyze_client.py --phase dump             — parse dump.cs only
  py analyze_client.py --phase index            — build decompiled_all.c index
  py analyze_client.py --phase constructors     — extract constructors (needs index)
  py analyze_client.py --phase synthesize       — build ClientData.json from cached results
"""

import argparse
import json
import os
import re
import sys
import time
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
REPO_ROOT = SCRIPT_DIR.parent.parent
IL2CPP_DIR = REPO_ROOT / "Il2CppDumper" / "Il2CppDumper V6.7.48"
OUTPUT_DIR = SCRIPT_DIR / "output"

DUMP_CS = IL2CPP_DIR / "dump.cs"
DECOMPILED_C = IL2CPP_DIR / "decompiled_all.c"
SCRIPT_JSON = IL2CPP_DIR / "script.json"
STRINGLIT_JSON = IL2CPP_DIR / "stringliteral.json"
GLOBAL_METADATA = REPO_ROOT / "Cliente 3.6.10.11" / "Dofus_Data" / "il2cpp_data" / "Metadata" / "global-metadata.dat"

# OPS Obfuscator encrypted string table (main GUID)
OBF_METADATA_OFFSET = 0x13D51B0
OBF_METADATA_SIZE = 0xC40D3
OBF_GUID = "0E33E920"

# ════════════════════════════════════════════════════════════════════════
# Phase 0: Decrypt OPS Obfuscator strings from global-metadata.dat
# XOR formula: decoded[i] = encrypted[i] ^ (i & 0xFF) ^ 0xAA
# ════════════════════════════════════════════════════════════════════════

RE_OBF_DECODE = re.compile(r'__a__a_\s+\((\w+),(\w+),(\w+),')


def decrypt_obfuscated_strings(dec_index=None):
    """Decrypt all OPS Obfuscator strings from the client binary."""
    cache_file = OUTPUT_DIR / "obfuscated_strings.json"
    if cache_file.exists():
        age_hours = (time.time() - cache_file.stat().st_mtime) / 3600
        if age_hours < 24:
            print(f"[Phase 0] Loading cached strings ({age_hours:.1f}h old)...")
            with open(cache_file, 'r', encoding='utf-8') as f:
                return json.load(f)

    if not GLOBAL_METADATA.exists():
        print(f"[Phase 0] SKIP: global-metadata.dat not found at {GLOBAL_METADATA}")
        return {}
    if not DECOMPILED_C.exists() or dec_index is None:
        print(f"[Phase 0] SKIP: decompiled_all.c or index not available")
        return {}

    print(f"[Phase 0] Decrypting OPS Obfuscator strings...")
    t0 = time.time()

    # Read and XOR-decrypt the byte array
    with open(GLOBAL_METADATA, 'rb') as f:
        f.seek(OBF_METADATA_OFFSET)
        raw = f.read(OBF_METADATA_SIZE)
    decoded = bytearray(OBF_METADATA_SIZE)
    for i in range(OBF_METADATA_SIZE):
        decoded[i] = raw[i] ^ (i & 0xFF) ^ 0xAA

    # Find all wrapper functions in the decompiled index
    wrapper_keys = [k for k in dec_index if OBF_GUID in k and '$$' in k]
    infra = {'.cctor', 'a_', 'a', 'b'}
    wrapper_keys = [k for k in wrapper_keys if k.split('$$')[1] not in infra]

    # Read each wrapper body and extract (index, offset, length) tuples
    strings_map = {}
    with open(DECOMPILED_C, 'r', encoding='utf-8', errors='replace') as f:
        for k in wrapper_keys:
            name = k.split('$$')[1]
            entry = dec_index[k]
            f.seek(entry['pos'])
            body = f.read(3000)
            m = RE_OBF_DECODE.search(body)
            if m:
                off = int(m.group(2), 0)
                ln = int(m.group(3), 0)
                if off + ln <= OBF_METADATA_SIZE:
                    try:
                        strings_map[name] = decoded[off:off+ln].decode('utf-8')
                    except:
                        pass

    elapsed = time.time() - t0
    print(f"  Decrypted {len(strings_map)}/{len(wrapper_keys)} strings in {elapsed:.1f}s")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    with open(cache_file, 'w', encoding='utf-8') as f:
        json.dump(strings_map, f, indent=2, ensure_ascii=False)
    return strings_map


# ════════════════════════════════════════════════════════════════════════
# Phase 1: Parse dump.cs
# ════════════════════════════════════════════════════════════════════════

RE_NAMESPACE = re.compile(r'^// Namespace: (.*)$')
RE_TYPE_DEF = re.compile(
    r'^(?:\s*)(?:public |private |protected |internal )?'
    r'(?:static |sealed |abstract )*'
    r'(class|struct|enum|interface)\s+'
    r'(\S+?)(?:\s*<[^>]+>)?'
    r'(?:\s*:\s*(.+?))?'
    r'\s*(?://\s*TypeDefIndex:\s*(\d+))?$'
)
RE_FIELD = re.compile(
    r'^\s+(?:public|private|protected|internal)\s+'
    r'(?:static\s+|readonly\s+|volatile\s+)*'
    r'(.+?)\s+(\w+);\s*//\s*(0x[0-9A-Fa-f]+)'
)
RE_CONST_STR = re.compile(
    r'^\s+(?:public|private|protected|internal)\s+'
    r'(?:static\s+)?const\s+string\s+(\w+)\s*=\s*"(.*)";'
)
RE_CONST_NUM = re.compile(
    r'^\s+(?:public|private|protected|internal)\s+'
    r'(?:static\s+)?const\s+(\w+)\s+(\w+)\s*=\s*(.+);'
)
RE_RVA = re.compile(
    r'^\s+// RVA:\s*(0x[0-9A-Fa-f]+)\s+Offset:\s*(0x[0-9A-Fa-f]+)\s+VA:\s*(0x[0-9A-Fa-f]+)'
)
RE_METHOD = re.compile(
    r'^\s+(?:public|private|protected|internal)\s+'
    r'(?:static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)*'
    r'(.+?)\s+([.\w]+)\(([^)]*)\)\s*\{?\s*\}?'
)
RE_ATTRIBUTE = re.compile(r'^\s*\[(\w+)(?:\(.*?\))?\]')
RE_ENUM_VALUE = re.compile(r'^\s+public\s+const\s+\w+\s+(\w+)\s*=\s*(\d+);')
RE_SECTION = re.compile(r'^\s+// (Fields|Properties|Methods)')


def parse_dump_cs():
    """Parse dump.cs into a complete type index."""
    print(f"[Phase 1] Parsing {DUMP_CS} ({DUMP_CS.stat().st_size / 1e6:.1f} MB)...")
    t0 = time.time()

    types = {}
    current_ns = ""
    current_type = None
    current_section = None
    pending_rva = None
    pending_attrs = []
    brace_depth = 0
    line_num = 0

    with open(DUMP_CS, 'r', encoding='utf-8-sig') as f:
        for line in f:
            line_num += 1
            line = line.rstrip('\n')

            # Namespace
            m = RE_NAMESPACE.match(line)
            if m:
                current_ns = m.group(1).strip()
                pending_attrs = []
                continue

            # Attribute (before type or method)
            m = RE_ATTRIBUTE.match(line)
            if m and brace_depth == 0:
                pending_attrs.append(m.group(1))
                continue

            # Type definition
            m = RE_TYPE_DEF.match(line)
            if m and brace_depth == 0:
                kind = m.group(1)
                name = m.group(2)
                bases_raw = m.group(3) or ""
                tdi = int(m.group(4)) if m.group(4) else -1

                bases = [b.strip() for b in bases_raw.split(',') if b.strip()] if bases_raw else []
                fullname = f"{current_ns}.{name}" if current_ns else name

                is_abstract = 'abstract ' in line
                is_sealed = 'sealed ' in line
                is_static = 'static ' in line

                current_type = {
                    "fullName": fullname,
                    "name": name,
                    "namespace": current_ns,
                    "kind": kind,
                    "bases": bases,
                    "typeDefIndex": tdi,
                    "isAbstract": is_abstract,
                    "isSealed": is_sealed,
                    "isStatic": is_static,
                    "attributes": pending_attrs[:],
                    "fields": [],
                    "consts": {},
                    "methods": [],
                    "enumValues": {},
                    "line": line_num,
                }
                types[fullname] = current_type
                pending_attrs = []
                current_section = None
                continue

            if line.strip() == '{' and current_type and brace_depth == 0:
                brace_depth = 1
                continue
            if line.strip() == '}' and brace_depth == 1:
                brace_depth = 0
                current_type = None
                current_section = None
                continue

            if not current_type or brace_depth < 1:
                pending_attrs = []
                continue

            # RVA line (precedes method) — check BEFORE brace counting
            m = RE_RVA.match(line)
            if m:
                pending_rva = {
                    "rva": m.group(1),
                    "offset": m.group(2),
                    "va": m.group(3),
                }
                continue

            # Method signature (has { } on same line) — check BEFORE brace counting
            m = RE_METHOD.match(line)
            if m and pending_rva:
                method = {
                    "returnType": m.group(1).strip(),
                    "name": m.group(2),
                    "params": m.group(3).strip(),
                    "attributes": pending_attrs[:],
                    **pending_rva,
                }
                current_type["methods"].append(method)
                pending_rva = None
                pending_attrs = []
                continue

            # Nested type opening brace (inner class)
            if '{' in line and '}' not in line:
                brace_depth += line.count('{') - line.count('}')
                continue
            if '}' in line:
                brace_depth += line.count('{') - line.count('}')
                if brace_depth < 1:
                    brace_depth = 0
                    current_type = None
                continue

            # Section header
            m = RE_SECTION.match(line)
            if m:
                current_section = m.group(1)
                continue

            # Enum value
            if current_type["kind"] == "enum":
                m = RE_ENUM_VALUE.match(line)
                if m:
                    current_type["enumValues"][m.group(1)] = int(m.group(2))
                    continue

            # Field with offset
            m = RE_FIELD.match(line)
            if m:
                current_type["fields"].append({
                    "type": m.group(1).strip(),
                    "name": m.group(2),
                    "offset": m.group(3),
                })
                continue

            # Const string
            m = RE_CONST_STR.match(line)
            if m:
                current_type["consts"][m.group(1)] = m.group(2)
                continue

            # Const numeric
            m = RE_CONST_NUM.match(line)
            if m:
                current_type["consts"][m.group(2)] = m.group(3)
                continue

            # Attribute on method
            m = RE_ATTRIBUTE.match(line)
            if m and brace_depth >= 1:
                pending_attrs.append(m.group(1))
                continue

            pending_rva = None

    elapsed = time.time() - t0
    print(f"  Parsed {len(types)} types in {elapsed:.1f}s")
    return types


def extract_ve_hierarchy(all_types):
    """Extract all VisualElement subtypes and their inheritance chains."""
    print("[Phase 1b] Building VisualElement hierarchy...")

    ve_full = "UnityEngine.UIElements.VisualElement"

    # Build parent lookup: fullName -> first base class fullName
    name_to_type = {}
    short_to_full = {}
    for fn, t in all_types.items():
        name_to_type[fn] = t
        short = t["name"]
        if short not in short_to_full:
            short_to_full[short] = fn
        # Also index without generic params
        bare = short.split('<')[0] if '<' in short else short
        if bare not in short_to_full:
            short_to_full[bare] = fn

    def resolve_base(base_str):
        base_str = base_str.strip().split('<')[0]
        if base_str in name_to_type:
            return base_str
        if base_str in short_to_full:
            return short_to_full[base_str]
        return None

    def inherits_from_ve(fullname, visited=None):
        if visited is None:
            visited = set()
        if fullname in visited:
            return False
        visited.add(fullname)
        if fullname == ve_full:
            return True
        t = name_to_type.get(fullname)
        if not t:
            return False
        for b in t["bases"]:
            resolved = resolve_base(b)
            if resolved and inherits_from_ve(resolved, visited):
                return True
        return False

    ve_types = {}
    for fn, t in all_types.items():
        if t["kind"] != "class":
            continue
        if inherits_from_ve(fn):
            chain = []
            cur = fn
            while cur and cur != ve_full:
                chain.append(cur)
                tt = name_to_type.get(cur)
                if not tt or not tt["bases"]:
                    break
                cur = resolve_base(tt["bases"][0])
            if cur == ve_full:
                chain.append(ve_full)

            # Determine factory base type from chain
            factory_base = "VisualElement"
            for ancestor in chain:
                an = name_to_type.get(ancestor, {}).get("name", "")
                if an == "Label":
                    factory_base = "Label"
                    break
                if an == "Button":
                    factory_base = "Button"
                    break
                if an == "TextField":
                    factory_base = "TextField"
                    break
                if an == "Toggle":
                    factory_base = "Toggle"
                    break

            # Get constructor VAs
            ctor_vas = []
            for m in t.get("methods", []):
                if m["name"] == ".ctor":
                    ctor_vas.append(m["va"])

            # Extract USS class from const fields
            uss_classes = []
            for cname, cval in t.get("consts", {}).items():
                if "USS" in cname or "ClassName" in cname:
                    if isinstance(cval, str):
                        uss_classes.append(cval)

            # Check for ExtendedClickable field
            has_clickable = any(
                "ExtendedClickable" in f["type"] or "Clickable" in f["type"]
                for f in t.get("fields", [])
            )

            # Check for add_Clicked event (from dump.cs methods)
            # Types with Clicked event need Button base for text + click behavior
            has_clicked = any(
                m["name"] == "add_Clicked" for m in t.get("methods", [])
            )

            # Child fields (VisualElement-typed instance fields — exclude enums/non-VE)
            VE_FIELD_TYPES = {
                "VisualElement", "DofusLabel", "DofusIcon", "DofusVisualElement",
                "DofusBindableElement", "Label", "Button", "TextField",
                "DofusButtonCustom", "PlayButton", "Dropdown", "DropdownButton",
                "TextInput", "TextInputToValidate", "WindowFigma", "Divider",
                "DofusToggleButtonCustom", "CheckboxCustom", "DofusTab",
                "HeaderWidget", "HeaderWindow", "SectionHeader", "Switch",
                "RadioButton", "CircularGauge", "Tag", "Pills", "CurrencyField",
                "ScrollContainer", "SearchField", "Combobox", "Slot", "Tile",
            }
            child_fields = []
            for f in t.get("fields", []):
                ft = f["type"]
                if ft in VE_FIELD_TYPES or ft.startswith("Core.UILogic."):
                    child_fields.append({"name": f["name"], "type": ft})

            has_uxml_sd = any(
                t2.get("name", "").startswith("UxmlSerializedData")
                for t2 in all_types.values()
                if t2.get("namespace", "") == t["namespace"]
                and t2.get("name", "").startswith(t["name"] + "/UxmlSerializedData")
            )

            ve_types[fn] = {
                "name": t["name"],
                "namespace": t["namespace"],
                "fullName": fn,
                "bases": t["bases"],
                "chain": chain,
                "factoryBase": factory_base,
                "isAbstract": t["isAbstract"],
                "attributes": t["attributes"],
                "ussClasses": uss_classes,
                "hasClickable": has_clickable,
                "hasClicked": has_clicked,
                "childFields": child_fields,
                "ctorVAs": ctor_vas,
                "consts": t.get("consts", {}),
                "line": t["line"],
            }

    # Check for UxmlSerializedData as nested types
    for fn, vet in list(ve_types.items()):
        sd_name = f"{fn}/UxmlSerializedData"
        alt_sd = f"{fn}+UxmlSerializedData"
        for tname in all_types:
            if "UxmlSerializedData" in tname and vet["name"] in tname:
                vet["hasUxmlSerializedData"] = True
                break
        else:
            vet["hasUxmlSerializedData"] = False

    print(f"  Found {len(ve_types)} VisualElement types")
    return ve_types


def extract_asset_paths(all_types):
    """Extract all constant asset paths from AssetsPaths and similar classes."""
    print("[Phase 1c] Extracting asset paths...")
    paths = {}
    for fn, t in all_types.items():
        if not t.get("isStatic") and "Paths" not in t.get("name", ""):
            continue
        for cname, cval in t.get("consts", {}).items():
            if isinstance(cval, str) and ("Assets/" in cval or "assets/" in cval):
                paths[cname] = {"value": cval, "source": fn}
    print(f"  Found {len(paths)} asset paths")
    return paths


def extract_services(all_types):
    """Extract service types, [ServiceInit] methods, [DependsOnService] fields."""
    print("[Phase 1d] Extracting service graph...")
    services = {}
    for fn, t in all_types.items():
        service_init = []
        depends_on = []
        for m in t.get("methods", []):
            if "ServiceInit" in m.get("attributes", []):
                service_init.append({"name": m["name"], "va": m.get("va")})
        for f in t.get("fields", []):
            # DependsOnService is on the field line in dump.cs as an attribute
            pass  # Will be extracted from decompiled
        if service_init:
            services[fn] = {
                "name": t["name"],
                "fullName": fn,
                "serviceInitMethods": service_init,
                "fields": [{"name": f["name"], "type": f["type"]} for f in t.get("fields", [])],
            }
    print(f"  Found {len(services)} service types with [ServiceInit]")
    return services


def extract_uss_bem(uss_project_dir="C:/DofusWeb/Assets"):
    """Extract BEM-style USS class patterns from reconstructed .uss files."""
    print("[Phase 1e-pre] Extracting BEM classes from USS files...")
    bem_map = {}
    uss_count = 0
    if not os.path.isdir(uss_project_dir):
        print(f"  USS directory not found: {uss_project_dir}")
        return bem_map
    for root, dirs, files in os.walk(uss_project_dir):
        for fn in files:
            if not fn.endswith('.uss') or fn.endswith('.meta'):
                continue
            try:
                with open(os.path.join(root, fn), 'r', encoding='utf-8', errors='replace') as f:
                    content = f.read()
                uss_count += 1
                for m in re.finditer(r'\.([\w-]+)__([\w-]+)', content):
                    rc = m.group(1)
                    cp = m.group(2)
                    if rc not in bem_map:
                        bem_map[rc] = set()
                    bem_map[rc].add(cp)
            except:
                pass
    # Convert sets to sorted lists for JSON
    for k in bem_map:
        bem_map[k] = sorted(bem_map[k])
    print(f"  Scanned {uss_count} USS files, {len(bem_map)} BEM roots")
    return bem_map


def build_child_tree(ve_info, ctor_analysis, bem_map, obf_strings=None):
    """Build a child element tree from dump.cs fields + decompiled ops + USS BEM + decoded strings."""
    child_fields = ve_info.get("childFields", [])
    ops = ctor_analysis.get("ops", []) if ctor_analysis else []
    string_lits = ctor_analysis.get("stringLiterals", []) if ctor_analysis else []
    spr_keys = set(ctor_analysis.get("spriteOpsKeys", [])) if ctor_analysis else set()
    nam_keys = set(ctor_analysis.get("namOpsKeys", [])) if ctor_analysis else set()
    cls_keys = set(ctor_analysis.get("clsOpsKeys", [])) if ctor_analysis else set()

    # Resolve string literals to actual names via decrypted OPS strings
    resolved_strings = []
    if obf_strings and string_lits:
        for sl in string_lits:
            resolved_strings.append(obf_strings.get(sl, sl))

    if not child_fields and not resolved_strings:
        return []

    def field_type_code(ft):
        if "DofusLabel" in ft or ft == "Label":
            return "LBL"
        if "DofusIcon" in ft:
            return "ICO"
        if ft == "Button":
            return "BTN"
        return "VE"

    # USS root class for BEM lookup
    uss_roots = ve_info.get("ussClasses", [])

    def lookup_bem(elem_name):
        classes = []
        for uss_root in uss_roots:
            bem_children = bem_map.get(uss_root, [])
            kebab = re.sub(r'([A-Z])', lambda m: '-' + m.group(1).lower(), elem_name).lstrip('-')
            for bc in bem_children:
                if bc == elem_name or bc == kebab or bc == elem_name.lower():
                    classes.append(f"{uss_root}__{bc}")
            alt_root = f"dofus-{uss_root}"
            for bc in bem_map.get(alt_root, []):
                if bc == elem_name or bc == kebab or bc == elem_name.lower():
                    classes.append(f"{alt_root}__{bc}")
        return classes

    # Build children from the resolved string sequence + field types.
    # The ops+strings tell us the actual construction order:
    # - CLS string → AddToClassList (USS class, skip as child)
    # - VE+NAM string (before FIELD) → wrapper container name
    # - NAM string + FIELD → child element with that name
    has_add = "ADD" in ops
    first_field_idx = next((i for i, op in enumerate(ops) if op.startswith("F:")), len(ops))
    ve_before_field = any(ops[i] == "VE" for i in range(first_field_idx))

    # Match resolved strings to children using variable tracing.
    # Each OPS key (__a__XXX) is traced to its consumer in the decompiled code:
    #   set_name → nam_keys (child element names)
    #   AddToClassList → cls_keys (USS classes, skip as children)
    #   AddressableEntry___ctor → spr_keys (sprite asset names)
    # This avoids relying on ops sequence order which Ghidra can reorder.
    wrapper_name = None
    child_names_from_strings = []
    sprite_for_child = {}
    str_idx = 0
    has_traced_keys = len(nam_keys) > 0 or len(cls_keys) > 0

    if has_traced_keys:
        # Use variable tracing: classify each string by its consumer
        for si, lit_key in enumerate(string_lits):
            if si >= len(resolved_strings):
                break
            name = resolved_strings[si]
            if not name:
                continue
            if lit_key in cls_keys:
                continue  # USS class, skip
            elif lit_key in spr_keys:
                if child_names_from_strings:
                    sprite_for_child[len(child_names_from_strings) - 1] = name
            elif lit_key in nam_keys:
                if wrapper_name is None and ve_before_field and has_add:
                    wrapper_name = name
                else:
                    child_names_from_strings.append(name)
            # Strings not in any traced set are skipped (untracked consumers)
    else:
        # Fallback: use ops sequence order (less accurate for complex constructors)
        past_first_field = False
        for oi, op in enumerate(ops):
            if str_idx >= len(resolved_strings):
                break
            if op.startswith("F:"):
                past_first_field = True
            if op == "CLS":
                str_idx += 1
            elif op == "NAM":
                name = resolved_strings[str_idx] if str_idx < len(resolved_strings) else None
                str_idx += 1
                if name:
                    if wrapper_name is None and ve_before_field and has_add and not past_first_field:
                        wrapper_name = name
                        continue
                    child_names_from_strings.append(name)
            elif op == "SPR":
                if str_idx < len(string_lits) and string_lits[str_idx] in spr_keys:
                    spr_name = resolved_strings[str_idx] if str_idx < len(resolved_strings) else None
                    str_idx += 1
                    if spr_name and child_names_from_strings:
                        sprite_for_child[len(child_names_from_strings) - 1] = spr_name

    # Build child nodes from dump.cs field types + matched string names
    children = []
    for i, cf in enumerate(child_fields):
        fname = cf["name"]
        field_elem_name = fname[2:] if fname.startswith("m_") else fname
        ftype = cf["type"]
        tc = field_type_code(ftype)

        # Use resolved string name if available, else field name convention
        elem_name = field_elem_name
        if i < len(child_names_from_strings):
            elem_name = child_names_from_strings[i]

        bem = lookup_bem(elem_name)
        child = {"n": elem_name, "t": tc}
        if bem:
            child["cls"] = bem
        if i in sprite_for_child:
            child["spr"] = sprite_for_child[i]
        children.append(child)

    # If we detected a wrapper, nest children inside it
    if wrapper_name and has_add and ve_before_field:
        bem = lookup_bem(wrapper_name)
        wrapper = {"n": wrapper_name, "t": "VE", "ch": children}
        if bem:
            wrapper["cls"] = bem
        return [wrapper]

    return children


def extract_screen_handlers(all_types):
    """Extract screen handler types (fzy<T> subclasses) with their bindings."""
    print("[Phase 1e] Extracting screen handlers...")
    handlers = {}
    for fn, t in all_types.items():
        for b in t.get("bases", []):
            if "fzy" in b or "fzy<" in b or "gax" in b or "gax<" in b:
                binding_match = re.search(r'<(.+?)>', b)
                binding = binding_match.group(1) if binding_match else None
                handlers[fn] = {
                    "name": t["name"],
                    "fullName": fn,
                    "baseHandler": b,
                    "binding": binding,
                    "namespace": t["namespace"],
                    "methods": [m["name"] for m in t.get("methods", [])],
                }
                break
    print(f"  Found {len(handlers)} screen handlers")
    return handlers


def extract_enums(all_types):
    """Extract key enums used in the game systems."""
    print("[Phase 1f] Extracting key enums...")
    key_patterns = ["AuthUi", "ecs", "FigmaIcon", "StylePropertyId"]
    enums = {}
    for fn, t in all_types.items():
        if t["kind"] != "enum":
            continue
        if t.get("enumValues"):
            enums[fn] = {
                "name": t["name"],
                "namespace": t["namespace"],
                "values": t["enumValues"],
            }
    print(f"  Found {len(enums)} enums with values")
    return enums


def extract_bindings(all_types):
    """Extract UI Binding structs (fields = element names)."""
    print("[Phase 1g] Extracting UI bindings...")
    bindings = {}
    for fn, t in all_types.items():
        if t["kind"] != "struct":
            continue
        if not t.get("fields"):
            continue
        # Bindings have VisualElement/DofusLabel/Button etc. fields with underscore-prefixed names
        ve_fields = [
            f for f in t["fields"]
            if any(vt in f["type"] for vt in [
                "VisualElement", "DofusLabel", "DofusVisualElement", "DofusBindableElement",
                "DofusButtonCustom", "PlayButton", "Dropdown", "TextInput", "WindowFigma",
                "Label", "Button", "DofusIcon", "DofusTab", "CheckboxCustom",
                "DropdownButton", "Divider", "HeaderWidget",
            ])
        ]
        if len(ve_fields) >= 3:
            bindings[fn] = {
                "name": t["name"],
                "namespace": t["namespace"],
                "elements": [{"name": f["name"], "type": f["type"]} for f in ve_fields],
            }
    print(f"  Found {len(bindings)} UI binding structs")
    return bindings


# ════════════════════════════════════════════════════════════════════════
# Phase 2: Index decompiled_all.c
# ════════════════════════════════════════════════════════════════════════

RE_FUNC_HEADER = re.compile(r'^// === (.+?)\$\$(.+?) @ ([0-9a-fA-F]+) ===$')


def build_decompiled_index():
    """Scan decompiled_all.c and record byte offsets of all function headers."""
    index_file = OUTPUT_DIR / "decompiled_index.json"

    if index_file.exists():
        age_hours = (time.time() - index_file.stat().st_mtime) / 3600
        if age_hours < 24:
            print(f"[Phase 2] Loading cached index ({age_hours:.1f}h old)...")
            with open(index_file, 'r') as f:
                return json.load(f)

    print(f"[Phase 2] Indexing {DECOMPILED_C} ({DECOMPILED_C.stat().st_size / 1e9:.2f} GB)...")
    print("  This takes 2-3 minutes the first time. Index is cached for 24h.")
    t0 = time.time()

    index = {}
    count = 0
    with open(DECOMPILED_C, 'r', encoding='utf-8', errors='replace') as f:
        while True:
            pos = f.tell()
            line = f.readline()
            if not line:
                break
            m = RE_FUNC_HEADER.match(line.rstrip())
            if m:
                class_name = m.group(1)
                method_name = m.group(2)
                va = m.group(3).lower()
                key = f"{class_name}$${method_name}"
                index[key] = {"pos": pos, "va": va, "class": class_name, "method": method_name}
                count += 1
                if count % 50000 == 0:
                    print(f"  {count} functions indexed ({f.tell() / 1e9:.1f} GB)...")

    elapsed = time.time() - t0
    print(f"  Indexed {count} functions in {elapsed:.0f}s")

    with open(index_file, 'w') as f:
        json.dump(index, f)
    print(f"  Saved index to {index_file}")
    return index


def read_function_body(func_entry, max_lines=50000):
    """Read a function body from decompiled_all.c given its index entry."""
    with open(DECOMPILED_C, 'r', encoding='utf-8', errors='replace') as f:
        f.seek(func_entry["pos"])
        lines = []
        in_body = False
        brace_depth = 0
        for _ in range(max_lines):
            line = f.readline()
            if not line:
                break
            lines.append(line.rstrip())
            if '{' in line:
                in_body = True
                brace_depth += line.count('{') - line.count('}')
            elif '}' in line and in_body:
                brace_depth += line.count('{') - line.count('}')
                if brace_depth <= 0:
                    break
        return '\n'.join(lines)


# ════════════════════════════════════════════════════════════════════════
# Phase 3: Extract constructor behavior from decompiled_all.c
# ════════════════════════════════════════════════════════════════════════

RE_FIELD_ASSIGN = re.compile(r'\(__this->fields\)\.(\w+)\s*=\s*(\w+)')
RE_HIERARCHY_ADD = re.compile(r'Hierarchy__Add')
RE_ADD_CLASS = re.compile(r'AddToClassList')
RE_ADD_MANIPULATOR = re.compile(r'AddManipulator')
RE_REMOVE_MANIPULATOR = re.compile(r'RemoveManipulator')
RE_SET_NAME = re.compile(r'set_name')
RE_SET_PICKING = re.compile(r'set_pickingMode')
RE_SET_FOCUSABLE = re.compile(r'set_focusable')
RE_STYLE_SET = re.compile(r'get_style')
RE_EXT_CLICKABLE = re.compile(r'ExtendedClickable')
RE_CURSOR_HANDLER = re.compile(r'DofusCursor__AddCursorHandler')
RE_USS_LOAD = re.compile(r'AddStyleSheetPath|AddStyleSheet[^>]')
RE_PRIVATE_IMPL = re.compile(r'PrivateImplementationDetails.*?__a__(\w+)')
RE_CTOR_CALL = re.compile(r'(\w+)___ctor\b')
RE_REGISTER_CALLBACK = re.compile(r'RegisterCallback')


def analyze_constructor(body, ve_info):
    """Analyze a decompiled constructor body to extract behavior."""
    result = {
        "fieldAssignOrder": [],
        "hierarchyAdds": 0,
        "addToClassListCalls": 0,
        "addManipulatorCalls": 0,
        "removeManipulatorCalls": 0,
        "setNameCalls": 0,
        "setPickingModeCalls": 0,
        "setFocusableCalls": 0,
        "styleSetCalls": 0,
        "registerCallbackCalls": 0,
        "hasExtendedClickable": False,
        "hasCursorHandler": False,
        "ussLoadCalls": 0,
        "stringLiterals": [],
        "ctorCalls": [],
        "bodyLength": len(body.split('\n')),
    }

    for line in body.split('\n'):
        m = RE_FIELD_ASSIGN.search(line)
        if m:
            fname = m.group(1)
            if fname not in result["fieldAssignOrder"]:
                result["fieldAssignOrder"].append(fname)

        if RE_HIERARCHY_ADD.search(line):
            result["hierarchyAdds"] += 1
        if RE_ADD_CLASS.search(line):
            result["addToClassListCalls"] += 1
        if RE_ADD_MANIPULATOR.search(line):
            result["addManipulatorCalls"] += 1
        if RE_REMOVE_MANIPULATOR.search(line):
            result["removeManipulatorCalls"] += 1
        if RE_SET_NAME.search(line):
            result["setNameCalls"] += 1
        if RE_SET_PICKING.search(line):
            result["setPickingModeCalls"] += 1
        if RE_SET_FOCUSABLE.search(line):
            result["setFocusableCalls"] += 1
        if RE_STYLE_SET.search(line):
            result["styleSetCalls"] += 1
        if RE_REGISTER_CALLBACK.search(line):
            result["registerCallbackCalls"] += 1
        if RE_EXT_CLICKABLE.search(line):
            result["hasExtendedClickable"] = True
        if RE_CURSOR_HANDLER.search(line):
            result["hasCursorHandler"] = True
        if RE_USS_LOAD.search(line):
            result["ussLoadCalls"] += 1

        m = RE_PRIVATE_IMPL.search(line)
        if m:
            result["stringLiterals"].append(m.group(1))

        m = RE_CTOR_CALL.search(line)
        if m:
            ctor_name = m.group(1)
            if ctor_name not in result["ctorCalls"] and "FUN_" not in ctor_name:
                result["ctorCalls"].append(ctor_name)

        # Sprite/addressable loads
        if 'UIUtils__pao' in line or ('AddressableEntry' in line and '___ctor' in line):
            result.setdefault("spriteLoads", 0)
            result["spriteLoads"] = result.get("spriteLoads", 0) + 1

    # Variable tracing: map pSVarX = __a__KEY assignments, then check
    # which vars are passed to AddressableEntry___ctor (sprite keys)
    # vs set_name (element name keys). This is how Ghidra structures the code:
    # assignments at the top, consumers further down.
    var_to_ops_key = {}  # "pSVar8" → "ysx"
    spr_ops_keys = set()
    nam_ops_keys = set()
    cls_ops_keys = set()
    body_lines = body.split('\n')
    for li, line in enumerate(body_lines):
        s = line.strip()
        m = re.search(r'(pSVar\d+)\s*=.*__a__([a-z]+)', s)
        if m:
            var_to_ops_key[m.group(1)] = m.group(2)
        # Trace which __a__ keys are consumed by which operation
        window = ' '.join(body_lines[li:li + 10])
        if 'AddressableEntry___ctor' in s:
            for var, key in var_to_ops_key.items():
                if var in window:
                    spr_ops_keys.add(key)
        if 'set_name' in s:
            for var, key in var_to_ops_key.items():
                if var in window:
                    nam_ops_keys.add(key)
        if 'AddToClassList' in s:
            for var, key in var_to_ops_key.items():
                if var in window:
                    cls_ops_keys.add(key)
    result["spriteOpsKeys"] = list(spr_ops_keys)
    result["namOpsKeys"] = list(nam_ops_keys)
    result["clsOpsKeys"] = list(cls_ops_keys)

    # Build operation sequence for constructor behavior reconstruction
    ops = []
    for li, line in enumerate(body_lines):
        s = line.strip()
        m = RE_FIELD_ASSIGN.search(s)
        if m:
            ops.append(f"F:{m.group(1)}")
        if 'AddToClassList' in s:
            ops.append("CLS")
        if 'set_name' in s:
            ops.append("NAM")
        if 'Hierarchy__Add' in s:
            ops.append("ADD")
        if 'VisualElement___ctor' in s and '__this' not in s.split('___ctor')[0][-30:]:
            ops.append("VE")
        if 'DofusLabel___ctor' in s:
            ops.append("LBL")
        if 'DofusIcon___ctor' in s:
            ops.append("ICO")
        if 'AltAllowedClickable___ctor' in s or 'ExtendedClickable___ctor' in s:
            ops.append("CLK")
        if 'AddManipulator' in s:
            ops.append("MAN")
        if 'DofusCursor__AddCursorHandler' in s:
            ops.append("CUR")
        if 'UIUtils__pao' in s:
            ops.append("SPR")
        if 'get_style' in s:
            ops.append("STY")
    result["ops"] = ops

    return result


def extract_constructors(ve_types, dec_index):
    """Extract constructor behavior for all VisualElement types."""
    print(f"[Phase 3] Extracting constructors for {len(ve_types)} VE types...")
    t0 = time.time()

    constructors = {}
    found = 0
    skipped = 0

    for fn, vet in ve_types.items():
        if vet["isAbstract"]:
            skipped += 1
            continue

        # Find constructor in decompiled index
        class_key = fn  # e.g. "Core.UILogic.Components.Figma.PlayButton"
        ctor_key = f"{class_key}$$.ctor"

        if ctor_key not in dec_index:
            # Try with just the class name
            skipped += 1
            continue

        body = read_function_body(dec_index[ctor_key])
        if not body or len(body) < 50:
            skipped += 1
            continue

        analysis = analyze_constructor(body, vet)

        # Merge with dump.cs const data
        constructors[fn] = {
            "name": vet["name"],
            "fullName": fn,
            "factoryBase": vet["factoryBase"],
            "ussClasses": vet["ussClasses"],
            "hasClickable": vet["hasClickable"] or analysis["hasExtendedClickable"],
            "childFields": vet["childFields"],
            "analysis": analysis,
        }
        found += 1

    elapsed = time.time() - t0
    print(f"  Extracted {found} constructors, {skipped} skipped in {elapsed:.1f}s")
    return constructors


# ════════════════════════════════════════════════════════════════════════
# Phase 4: Synthesize ClientData.json
# ════════════════════════════════════════════════════════════════════════

def synthesize(ve_types, constructors, asset_paths, screen_handlers, services, bindings, enums, bem_map=None, obf_strings=None):
    """Build the final ClientData.json for runtime consumption."""
    print("[Phase 4] Synthesizing ClientData.json...")

    # Factory registry: type → base type + USS classes + clickable + children
    factories = {}
    for fn, vet in ve_types.items():
        if vet["isAbstract"]:
            continue

        ctor = constructors.get(fn, {})
        analysis = ctor.get("analysis", {})

        # Determine factory base: use inheritance chain (authoritative) not heuristics
        factory_base = vet["factoryBase"]

        # Upgrade to Button ONLY for types with add_Clicked event (from dump.cs methods).
        # These types emit click events and need Button for text + clicked handler.
        # Types with only ExtendedClickable (like Dropdown) use VisualElement + manipulator.
        # Determined by scanning dump.cs for "add_Clicked(Action" in the class body.
        if factory_base == "VisualElement" and vet.get("hasClicked"):
            factory_base = "Button"

        uss = list(vet["ussClasses"])

        child_tree = build_child_tree(vet, analysis, bem_map or {}, obf_strings)

        factories[fn] = {
            "name": vet["name"],
            "namespace": vet["namespace"],
            "factoryBase": factory_base,
            "ussClasses": uss,
            "hasClickable": vet["hasClickable"] or analysis.get("hasExtendedClickable", False),
            "hasCursor": analysis.get("hasCursorHandler", False),
            "tree": child_tree,
            "consts": vet.get("consts", {}),
            "fieldAssignOrder": analysis.get("fieldAssignOrder", []),
            "ops": analysis.get("ops", []),
        }

    # Screen registry
    screens = {}
    for fn, h in screen_handlers.items():
        binding_data = None
        if h.get("binding"):
            for bfn, bdata in bindings.items():
                if bdata["name"] == h["binding"]:
                    binding_data = bdata
                    break

        screens[h["name"]] = {
            "handler": fn,
            "binding": h.get("binding"),
            "bindingElements": binding_data["elements"] if binding_data else [],
            "methods": h.get("methods", []),
        }

    client_data = {
        "_meta": {
            "generator": "analyze_client.py",
            "dumpCs": str(DUMP_CS),
            "decompiledC": str(DECOMPILED_C),
            "totalTypes": len(ve_types),
            "totalConstructors": len(constructors),
        },
        "factories": factories,
        "assetPaths": {k: v["value"] for k, v in asset_paths.items()},
        "screens": screens,
        "services": services,
        "enums": {fn: e["values"] for fn, e in enums.items() if len(e["values"]) <= 50},
    }

    return client_data


# ════════════════════════════════════════════════════════════════════════
# Main
# ════════════════════════════════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(description="Dofus 3 Client Analyzer")
    parser.add_argument("--phase", choices=["dump", "index", "constructors", "synthesize", "all"],
                        default="all", help="Which phase to run")
    parser.add_argument("--output", default=str(OUTPUT_DIR), help="Output directory")
    args = parser.parse_args()

    OUTPUT_DIR_PATH = Path(args.output)
    OUTPUT_DIR_PATH.mkdir(parents=True, exist_ok=True)

    if not DUMP_CS.exists():
        print(f"ERROR: dump.cs not found at {DUMP_CS}")
        sys.exit(1)

    # Phase 1: Parse dump.cs
    all_types_file = OUTPUT_DIR_PATH / "all_types.json"
    if args.phase in ("dump", "all"):
        all_types = parse_dump_cs()
        ve_types = extract_ve_hierarchy(all_types)
        asset_paths = extract_asset_paths(all_types)
        bem_map = extract_uss_bem()
        services = extract_services(all_types)
        screen_handlers = extract_screen_handlers(all_types)
        enums = extract_enums(all_types)
        bindings = extract_bindings(all_types)

        # Save intermediates
        with open(OUTPUT_DIR_PATH / "bem_map.json", 'w') as f:
            json.dump(bem_map, f, indent=2)
        with open(OUTPUT_DIR_PATH / "visual_elements.json", 'w') as f:
            json.dump(ve_types, f, indent=2, default=str)
        with open(OUTPUT_DIR_PATH / "asset_paths.json", 'w') as f:
            json.dump(asset_paths, f, indent=2)
        with open(OUTPUT_DIR_PATH / "services.json", 'w') as f:
            json.dump(services, f, indent=2)
        with open(OUTPUT_DIR_PATH / "screen_handlers.json", 'w') as f:
            json.dump(screen_handlers, f, indent=2)
        with open(OUTPUT_DIR_PATH / "enums.json", 'w') as f:
            json.dump(enums, f, indent=2)
        with open(OUTPUT_DIR_PATH / "bindings.json", 'w') as f:
            json.dump(bindings, f, indent=2)

        # Save summary stats
        stats = {
            "totalTypes": len(all_types),
            "veTypes": len(ve_types),
            "assetPaths": len(asset_paths),
            "services": len(services),
            "screenHandlers": len(screen_handlers),
            "enums": len(enums),
            "bindings": len(bindings),
        }
        print(f"\n[Phase 1 Summary]")
        for k, v in stats.items():
            print(f"  {k}: {v}")
        with open(OUTPUT_DIR_PATH / "stats.json", 'w') as f:
            json.dump(stats, f, indent=2)
    else:
        # Load cached
        with open(OUTPUT_DIR_PATH / "visual_elements.json", 'r') as f:
            ve_types = json.load(f)
        with open(OUTPUT_DIR_PATH / "asset_paths.json", 'r') as f:
            asset_paths = json.load(f)
        with open(OUTPUT_DIR_PATH / "services.json", 'r') as f:
            services = json.load(f)
        with open(OUTPUT_DIR_PATH / "screen_handlers.json", 'r') as f:
            screen_handlers = json.load(f)
        with open(OUTPUT_DIR_PATH / "enums.json", 'r') as f:
            enums = json.load(f)
        with open(OUTPUT_DIR_PATH / "bindings.json", 'r') as f:
            bindings = json.load(f)

    # Phase 2: Index decompiled_all.c
    dec_index = None
    if args.phase in ("index", "constructors", "all"):
        if DECOMPILED_C.exists():
            dec_index = build_decompiled_index()
        else:
            print(f"[Phase 2] SKIP: decompiled_all.c not found at {DECOMPILED_C}")

    # Phase 3: Extract constructors
    constructors = {}
    if args.phase in ("constructors", "all") and dec_index:
        constructors = extract_constructors(ve_types, dec_index)
        with open(OUTPUT_DIR_PATH / "constructors.json", 'w') as f:
            json.dump(constructors, f, indent=2)
    elif (OUTPUT_DIR_PATH / "constructors.json").exists():
        with open(OUTPUT_DIR_PATH / "constructors.json", 'r') as f:
            constructors = json.load(f)

    # Phase 3.5: Decrypt OPS Obfuscator strings
    if args.phase in ("all",) and dec_index:
        obf_strings = decrypt_obfuscated_strings(dec_index)
    elif (OUTPUT_DIR_PATH / "obfuscated_strings.json").exists():
        with open(OUTPUT_DIR_PATH / "obfuscated_strings.json", 'r', encoding='utf-8') as f:
            obf_strings = json.load(f)

    # Phase 4: Synthesize
    if args.phase in ("synthesize", "all"):
        bem_map = {}
        if (OUTPUT_DIR_PATH / "bem_map.json").exists():
            with open(OUTPUT_DIR_PATH / "bem_map.json", 'r') as f:
                bem_map = json.load(f)
        obf_strings = {}
        if (OUTPUT_DIR_PATH / "obfuscated_strings.json").exists():
            with open(OUTPUT_DIR_PATH / "obfuscated_strings.json", 'r', encoding='utf-8') as f:
                obf_strings = json.load(f)
        elif dec_index:
            obf_strings = decrypt_obfuscated_strings(dec_index)
        client_data = synthesize(ve_types, constructors, asset_paths, screen_handlers, services, bindings, enums, bem_map, obf_strings)

        out_path = OUTPUT_DIR_PATH / "ClientData.json"
        with open(out_path, 'w') as f:
            json.dump(client_data, f, indent=2)
        print(f"\n[DONE] ClientData.json: {out_path}")
        print(f"  factories: {len(client_data['factories'])}")
        print(f"  assetPaths: {len(client_data['assetPaths'])}")
        print(f"  screens: {len(client_data['screens'])}")
        print(f"  services: {len(client_data['services'])}")

        # Also save a compact version for Unity Resources
        compact = {
            "factories": {},
            "assetPaths": client_data["assetPaths"],
        }
        for fn, fdata in client_data["factories"].items():
            entry = {
                "n": fdata["name"],
                "b": fdata["factoryBase"],
                "u": fdata["ussClasses"],
                "c": fdata["hasClickable"],
            }
            if fdata.get("hasCursor"):
                entry["cur"] = True
            if fdata.get("tree"):
                entry["tr"] = fdata["tree"]
            compact["factories"][fn] = entry
        compact_path = OUTPUT_DIR_PATH / "ClientData_compact.json"
        with open(compact_path, 'w') as f:
            json.dump(compact, f, separators=(',', ':'))
        print(f"  Compact: {compact_path} ({compact_path.stat().st_size / 1024:.1f} KB)")


if __name__ == "__main__":
    main()
