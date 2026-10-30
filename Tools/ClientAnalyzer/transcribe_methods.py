#!/usr/bin/env python3
"""
Transcribe decompiled method bodies from decompiled_all.c to structured JSON.

Extends analyze_client.py's constructor analysis to handle arbitrary methods:
- Screen handler methods (OnEnable, OnDisable, event callbacks)
- Service methods ([ServiceInit])
- State machine flows (async UniTask methods)
- Auth screen manager (fpm) transitions

Usage:
    py transcribe_methods.py <TypeName>                  # all methods of a type
    py transcribe_methods.py <TypeName> <MethodName>     # specific method
    py transcribe_methods.py --handlers                  # all screen handlers
    py transcribe_methods.py --services                  # all [ServiceInit] methods
    py transcribe_methods.py --list <TypeName>           # list methods with VAs

Sources:
    - dump.cs: type structures, field names, method signatures with virtual addresses (VA)
    - decompiled_all.c: Ghidra pseudo-C implementation bodies (indexed by analyze_client.py)
    - global-metadata.dat: OPS Obfuscator decrypted strings (28,517)

Output:
    output/transcribed/<TypeName>.json — structured method analysis
    output/transcribed/<TypeName>.cs   — C# stub translation
"""

import hashlib
import json
import os
from pathlib import Path
import re
import sys
import time

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(SCRIPT_DIR, "output")
TRANSCRIBED_DIR = os.path.join(OUTPUT_DIR, "transcribed")

REPO_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
DECOMPILED_DIR = os.environ.get("DECOMPILED_DIR", os.path.join(REPO_ROOT, "Decompiled"))

DUMP_CS = os.path.join(DECOMPILED_DIR, "dump.cs")
if not os.path.exists(DUMP_CS):
    DUMP_CS = os.path.join(REPO_ROOT, "Il2CppDumper", "Il2CppDumper V6.7.48", "dump.cs")

DECOMPILED_C = os.path.join(DECOMPILED_DIR, "decompiled_all.c")
if not os.path.exists(DECOMPILED_C):
    DECOMPILED_C = os.path.join(REPO_ROOT, "Il2CppDumper", "Il2CppDumper V6.7.48", "decompiled_all.c")

METADATA = os.environ.get(
    "METADATA_PATH",
    os.path.join(REPO_ROOT, "Cliente 3.6.10.11", "Dofus_Data", "il2cpp_data", "Metadata", "global-metadata.dat")
)

SCRIPT_JSON = os.path.join(DECOMPILED_DIR, "script.json")
if not os.path.exists(SCRIPT_JSON):
    SCRIPT_JSON = os.path.join(REPO_ROOT, "Il2CppDumper", "Il2CppDumper V6.7.48", "script.json")

IMAGE_BASE = 0x180000000  # IL2CPP 64-bit image base
RECOVERY_COVERAGE = os.path.join(
    SCRIPT_DIR, "..", "..", "Generated", "output", "coverage-complete-known-inventory", "DecompilationCoverage.json"
)

# Patterns for parsing decompiled C code
RE_FIELD_ACCESS = re.compile(r'(?:this|param_\d+)\s*(?:->|\.)\s*(\w+)')
RE_METHOD_CALL = re.compile(r'(\w+)__(\w+)\s*\(')
RE_OPS_STRING = re.compile(r'__a__(\w+)')
RE_SET_NAME = re.compile(r'set_name\s*\([^,]+,\s*(\w+)')
RE_ADD_CLASS = re.compile(r'AddToClassList\s*\([^,]+,\s*(\w+)')
RE_ENUM_VALUE = re.compile(r'(\w+)__(\w+)\s*=\s*(\d+)')
RE_IF_BRANCH = re.compile(r'if\s*\(')
RE_SWITCH = re.compile(r'switch\s*\(')
RE_ASYNC_STATE = re.compile(r'<>1__state')
RE_AWAIT = re.compile(r'GetAwaiter|MoveNext|get_IsCompleted')
RE_BDGO = re.compile(r'bdgo|bdgp')  # open screen calls
RE_SCREEN_TRANSITION = re.compile(r'ShowScreen|OpenScreen|AuthUis')


def load_cached(name):
    """Load a cached JSON from the output directory."""
    path = os.path.join(OUTPUT_DIR, name)
    if os.path.exists(path):
        with open(path, 'r', encoding='utf-8') as f:
            return json.load(f)
    return None


def load_decompiled_index():
    """Load the function index built by analyze_client.py."""
    idx = load_cached("decompiled_index.json")
    if not idx:
        print("ERROR: decompiled_index.json not found. Run: py analyze_client.py --phase index")
        sys.exit(1)
    return idx


def load_obfuscated_strings():
    """Load decrypted OPS strings."""
    data = load_cached("obfuscated_strings.json")
    if not data:
        print("WARNING: obfuscated_strings.json not found — string resolution disabled")
        return {}
    return data


def load_recovered_methods(coverage_path=None):
    """Load verified pseudocode bodies recovered via REA evidence."""
    path = coverage_path or RECOVERY_COVERAGE
    if not path or not os.path.exists(path):
        return {}

    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    # Check export checksum if present
    expected_export_sha = data.get("source", {}).get("sha256")
    if expected_export_sha and os.path.exists(DECOMPILED_C):
        actual_export_sha = hashlib.sha256(Path(DECOMPILED_C).read_bytes()).hexdigest()
        if actual_export_sha != expected_export_sha:
            raise ValueError(
                f"Checksum mismatch for current decompiled export: expected {expected_export_sha}, got {actual_export_sha}"
            )

    recovered = {}
    recovery = data.get("recovery", {})
    for item in recovery.get("recoveredBodies", []):
        addr_str = item.get("address", "0")
        addr = int(addr_str, 16) if isinstance(addr_str, str) else addr_str
        response_path = item.get("responsePath")
        if not response_path or not os.path.exists(response_path):
            continue

        resp_data = json.loads(Path(response_path).read_text(encoding="utf-8"))
        structured = resp_data.get("structuredContent", {})
        code = structured.get("normalized_result", "")

        expected_code_sha = item.get("codeSha256")
        if expected_code_sha:
            actual_code_sha = hashlib.sha256(code.encode("utf-8")).hexdigest()
            if actual_code_sha != expected_code_sha:
                raise ValueError(
                    f"Checksum mismatch for audited pseudocode at {addr_str}: expected {expected_code_sha}, got {actual_code_sha}"
                )

        evidence_id = item.get("evidenceId") or structured.get("evidence_id")
        recovered[addr] = {
            "body": code,
            "source": {
                "kind": "rea-pseudocode",
                "evidenceId": evidence_id,
                "semanticTranslationVerified": False,
            }
        }

    return recovered


def read_function_body(func_entry, max_bytes=200000):
    """Read function body from decompiled_all.c."""
    with open(DECOMPILED_C, 'r', encoding='utf-8', errors='replace') as f:
        f.seek(func_entry["pos"])
        lines = []
        in_body = False
        brace_depth = 0
        bytes_read = 0
        for raw_line in f:
            bytes_read += len(raw_line)
            if bytes_read > max_bytes:
                break
            line = raw_line.rstrip()
            if not in_body:
                if '{' in line:
                    in_body = True
                    brace_depth = line.count('{') - line.count('}')
                    lines.append(line)
                continue
            lines.append(line)
            brace_depth += line.count('{') - line.count('}')
            if brace_depth <= 0:
                break
        return '\n'.join(lines)


def find_type_methods(type_name, typedef_index=None):
    """Find all methods of a type from dump.cs with their virtual addresses."""
    methods = []
    in_type = False
    current_method = None
    brace_depth = 0
    pending_va = None
    full_namespace = None
    current_namespace = ""
    target_ns = None
    target_name = type_name
    if "." in type_name:
        target_ns, target_name = type_name.rsplit(".", 1)

    with open(DUMP_CS, 'r', encoding='utf-8', errors='replace') as f:
        for line_no, line in enumerate(f, 1):
            stripped = line.strip()

            # Track namespace — dump.cs uses "// Namespace: X.Y.Z" comment format
            ns_match = re.match(r'namespace\s+([\w.]*)', stripped) or \
                       re.match(r'//\s*Namespace:\s*([\w.]*)', stripped)
            if ns_match:
                current_namespace = ns_match.group(1).strip()

            # Find the type declaration — handle generics, inheritance, comments
            # Skip inner classes: ServerSelection.<>c appears BEFORE ServerSelection in dump.cs
            if not in_type:
                if re.search(rf'\b(class|struct)\s+{re.escape(target_name)}\b', stripped):
                    if re.search(rf'{re.escape(target_name)}\s*[.<]\s*<', stripped) or \
                       re.search(rf'{re.escape(target_name)}\.\w', stripped):
                        continue  # inner class (TypeName.<>c, TypeName.DisplayClass, etc.)
                    if target_ns is not None and current_namespace != target_ns:
                        continue
                    if typedef_index is not None:
                        td_match = re.search(r'//\s*TypeDefIndex:\s*(\d+)', stripped)
                        if not td_match or int(td_match.group(1)) != typedef_index:
                            continue
                    in_type = True
                    brace_depth = 0
                    full_namespace = f"{current_namespace}.{target_name}" if current_namespace else target_name
                continue

            # Track braces — inner classes increase depth, type closing `}` at depth 0
            brace_depth += stripped.count('{') - stripped.count('}')
            if brace_depth <= 0 and stripped == '}' and len(methods) > 2:
                break  # closing brace of the type (after we've found several methods)

            # VA comment appears on the line BEFORE the method:
            # // RVA: 0xABCDEF Offset: 0xABCDEF VA: 0x181ABCDEF
            va_match = re.search(r'\bVA:\s*0x([0-9A-Fa-f]+)', stripped)
            if va_match:
                pending_va = int(va_match.group(1), 16)
                pending_va_line = line_no
                continue

            # Method declaration follows VA comment
            if pending_va and '(' in stripped and '{' in stripped:
                code = re.sub(r'//.*$', '', stripped).strip()
                # Lambda names: <Prefix>g__Method|N_M — try full pattern first
                method_match = re.search(r'(<\w+>[\w|]+)\s*\(', code)
                if not method_match:
                    method_match = re.search(r'(\w+)\s*\(', code)
                if method_match:
                    methods.append({
                        "name": method_match.group(1),
                        "line": line_no,
                        "va": pending_va,
                        "signature": code.rstrip('{ }').strip(),
                    })
                pending_va = None

    return methods, full_namespace


def analyze_method_body(body, obf_strings):
    """Analyze a decompiled method body to extract meaningful operations."""
    lines = body.split('\n')
    ops = []

    # Variable tracking: map pSVarX → OPS key
    var_ops_map = {}

    for i, line in enumerate(lines):
        stripped = line.strip()
        if not stripped or stripped.startswith('//'):
            continue

        # Track OPS string assignments: pSVarX = __a__KEY
        ops_match = RE_OPS_STRING.search(stripped)
        if ops_match and '=' in stripped:
            var_match = re.match(r'(\w+)\s*=', stripped)
            if var_match:
                var_name = var_match.group(1)
                ops_key = ops_match.group(1)
                resolved = obf_strings.get(ops_key, f"__a__{ops_key}")
                var_ops_map[var_name] = resolved

        # Method calls
        call_match = RE_METHOD_CALL.search(stripped)
        if call_match:
            class_name = call_match.group(1)
            method_name = call_match.group(2)

            op = {"type": "CALL", "class": class_name, "method": method_name, "line": i}

            # Resolve string arguments from variable tracking
            for var, resolved in var_ops_map.items():
                if var in stripped:
                    op["string_arg"] = resolved

            # Categorize important calls
            if 'set_name' in method_name:
                op["type"] = "SET_NAME"
                name_var = RE_SET_NAME.search(stripped)
                if name_var and name_var.group(1) in var_ops_map:
                    op["value"] = var_ops_map[name_var.group(1)]
            elif 'AddToClassList' in method_name:
                op["type"] = "ADD_CLASS"
                cls_var = RE_ADD_CLASS.search(stripped)
                if cls_var and cls_var.group(1) in var_ops_map:
                    op["value"] = var_ops_map[cls_var.group(1)]
            elif 'AddManipulator' in method_name:
                op["type"] = "ADD_MANIPULATOR"
            elif 'ExtendedClickable' in class_name:
                op["type"] = "CLICKABLE"
            elif 'Hierarchy' in class_name and 'Add' in method_name:
                op["type"] = "HIERARCHY_ADD"
            elif 'bdgo' in method_name or 'bdgp' in method_name:
                op["type"] = "OPEN_SCREEN"
            elif 'SetEnabled' in method_name:
                op["type"] = "SET_ENABLED"
            elif 'AddressableEntry' in class_name and 'ctor' in method_name:
                op["type"] = "LOAD_ASSET"
            elif 'style' in method_name.lower() or 'Style' in class_name:
                op["type"] = "SET_STYLE"
            elif 'UniTask' in class_name or 'GetAwaiter' in method_name:
                op["type"] = "AWAIT"

            ops.append(op)

        # Field access patterns
        if '->' in stripped and '=' in stripped and 'FUN_' not in stripped:
            field_match = re.search(r'->(\w+)\s*=', stripped)
            if field_match:
                field_name = field_match.group(1)
                ops.append({"type": "FIELD_SET", "field": field_name, "line": i})

        # Control flow
        if RE_IF_BRANCH.search(stripped):
            ops.append({"type": "IF", "line": i, "condition": stripped[:80]})
        if RE_SWITCH.search(stripped):
            ops.append({"type": "SWITCH", "line": i})
        if RE_ASYNC_STATE.search(stripped):
            ops.append({"type": "ASYNC_STATE", "line": i})

    return {
        "totalLines": len(lines),
        "ops": ops,
        "opsCount": len(ops),
        "varMap": var_ops_map,
        "hasAsync": any(o["type"] == "ASYNC_STATE" for o in ops),
        "hasScreenOpen": any(o["type"] == "OPEN_SCREEN" for o in ops),
        "callCount": sum(1 for o in ops if o["type"] == "CALL"),
        "fieldSetCount": sum(1 for o in ops if o["type"] == "FIELD_SET"),
    }


_c_function_cache = {}

def build_type_function_index(type_name, full_namespace=None):
    """Grep decompiled_all.c ONCE for all functions of a type, return {method: offset}."""
    import subprocess

    cache_key = type_name
    if cache_key in _c_function_cache:
        return _c_function_cache[cache_key]

    # Candidate C prefixes — start with known, then discover from file
    prefixes = [type_name.replace('.', '_')]
    if full_namespace and full_namespace != type_name:
        prefixes.append(full_namespace.replace('.', '_'))

    # Auto-discover: find full C prefix from a function DEFINITION line
    # Pattern: "^void Prefix_TypeName__MethodName(" — the ^ anchors to line start
    try:
        probe = subprocess.run(
            ['grep', '-m', '1', '-oP', f'^\\w+\\s+\\K[A-Za-z0-9_]*{re.escape(type_name)}(?=__)', DECOMPILED_C],
            capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=120
        )
        if probe.stdout.strip():
            discovered = probe.stdout.strip().split('\n')[0]
            if discovered and discovered not in prefixes:
                prefixes.insert(0, discovered)
    except Exception:
        # Fallback without -P (perl regex)
        try:
            probe = subprocess.run(
                ['grep', '-m', '1', f'^[a-z].* [A-Za-z_]*{re.escape(type_name)}__', DECOMPILED_C],
                capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=120
            )
            if probe.stdout.strip():
                line = probe.stdout.strip().split('\n')[0]
                # Extract: "void Core_UILogic_Auth_ServerSelection__Method("
                m = re.search(rf'(\w*{re.escape(type_name)})__', line)
                if m:
                    discovered = m.group(1)
                    if discovered and discovered not in prefixes:
                        prefixes.insert(0, discovered)
        except Exception:
            pass

    result_map = {}
    for prefix in prefixes:
        # Grep for BOTH function definitions AND Ghidra comment headers
        # Function: "void Core_UILogic_Auth_ServerSelection__OnEnable(" — starts at line beginning
        # Comment:  "// === Core.UILogic.Auth.ServerSelection$$OnEnable @ 1ABCDEF ==="
        # IMPORTANT: anchor to ^ to avoid matching CALL SITES (indented) which give wrong offsets
        ns_dotted = prefix.replace('_', '.')  # for comment headers
        pattern = f"^[a-zA-Z_].*{re.escape(prefix)}__[a-zA-Z_]"
        comment_pattern = f"// === {re.escape(ns_dotted)}\\$\\$"
        combined = f"({pattern}|{comment_pattern})"
        try:
            result = subprocess.run(
                ['grep', '-b', '-n', '-E', combined, DECOMPILED_C],
                capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=120
            )
            if result.stdout.strip():
                pending_va = None
                pending_offset = None
                for line in result.stdout.strip().split('\n'):
                    parts = line.split(':', 2)
                    if len(parts) < 3: continue
                    # grep -b -n format is LINE:BYTE:CONTENT (not BYTE:LINE)
                    offset = int(parts[1])  # byte offset is the SECOND field
                    content = parts[2]

                    # Comment header: // === Namespace.Type$$Method @ VA ===
                    cm = re.search(r'\$\$(\w+)\s*@\s*([0-9a-fA-F]+)', content)
                    if cm:
                        pending_va = cm.group(2)
                        pending_offset = offset
                        # Also register by method name from comment
                        method_from_comment = cm.group(1)
                        if method_from_comment not in result_map:
                            result_map[method_from_comment] = {
                                "pos": offset, "class": type_name,
                                "method": method_from_comment, "va": pending_va
                            }
                        continue

                    # Function definition: prefix__MethodName( — paren may be on next line
                    m = re.search(rf'{re.escape(prefix)}__(\w+)', content)
                    if m:
                        method = m.group(1)
                        entry = {"pos": offset, "class": type_name, "method": method}
                        if pending_va:
                            entry["va"] = pending_va
                            pending_va = None
                        if method not in result_map:
                            result_map[method] = entry
        except Exception as e:
            pass

        if result_map:
            break  # found with this prefix

    _c_function_cache[cache_key] = result_map
    return result_map


_rva_to_c_name = None

def load_rva_map():
    """Load script.json to map RVA → C function name in decompiled_all.c."""
    global _rva_to_c_name
    if _rva_to_c_name is not None:
        return _rva_to_c_name
    _rva_to_c_name = {}
    if os.path.exists(SCRIPT_JSON):
        with open(SCRIPT_JSON, 'r', encoding='utf-8') as f:
            data = json.load(f)
        for entry in data.get('ScriptMethod', []):
            rva = entry.get('Address', 0)
            name = entry.get('Name', '')
            if rva and name:
                # The C function name uses __ instead of $$, _ instead of . and |
                c_name = name.replace('.', '_').replace('$$', '__').replace('|', '_')
                _rva_to_c_name[rva] = c_name
    return _rva_to_c_name


_rva_to_all_names = None

def load_rva_map_all():
    """Load script.json to map RVA → ALL C function names (for alias resolution)."""
    global _rva_to_all_names
    if _rva_to_all_names is not None:
        return _rva_to_all_names
    _rva_to_all_names = {}
    if os.path.exists(SCRIPT_JSON):
        with open(SCRIPT_JSON, 'r', encoding='utf-8') as f:
            data = json.load(f)
        for entry in data.get('ScriptMethod', []):
            rva = entry.get('Address', 0)
            name = entry.get('Name', '')
            if rva and name:
                if rva not in _rva_to_all_names:
                    _rva_to_all_names[rva] = []
                _rva_to_all_names[rva].append(name)
    return _rva_to_all_names


_decompiled_index_cache = None

def find_c_function(type_name, method_name, full_namespace=None, va=None):
    """Find a C function in decompiled_all.c using multiple strategies."""
    global _decompiled_index_cache
    if _decompiled_index_cache is None:
        try:
            _decompiled_index_cache = load_decompiled_index()
        except Exception:
            _decompiled_index_cache = {}

    # Strategy 0: Direct lookup in precomputed decompiled_index.json (fastest)
    if _decompiled_index_cache:
        prefixes = [p for p in [full_namespace, type_name] if p]
        for p in prefixes:
            for cand in [f"{p}$${method_name}", f"{p}$$.{method_name}"]:
                if cand in _decompiled_index_cache:
                    return _decompiled_index_cache[cand]

    idx = build_type_function_index(type_name, full_namespace)

    # Strategy 1: Direct name match
    if method_name in idx:
        return idx[method_name]
    if f"_{method_name}" in idx:
        return idx[f"_{method_name}"]

    # Strategy 2: VA match via batch index
    if va:
        va_hex = f"{va:x}".lower()
        rva = va - IMAGE_BASE if va > IMAGE_BASE else va
        rva_hex = f"{rva:x}".lower()
        for entry in idx.values():
            if entry.get("va"):
                entry_va = entry["va"].lower().lstrip("0x")
                if entry_va == va_hex or entry_va == rva_hex:
                    return entry

    # Strategy 3: Use script.json RVA → C name → grep for that specific function
    if va:
        import subprocess
        rva = va - IMAGE_BASE if va > IMAGE_BASE else va
        rva_map = load_rva_map()
        c_name = rva_map.get(rva)
        if c_name:
            # Check batch index by method part
            parts = c_name.rsplit('__', 1)
            if len(parts) == 2:
                c_method = parts[1]
                if c_method in idx:
                    return idx[c_method]
            # Fallback: grep for the FULL C function name directly (-F for fixed string)
            # Don't include ( — Ghidra may wrap signature to next line
            try:
                grep_pattern = c_name
                result = subprocess.run(
                    ['grep', '-b', '-m', '1', '-F', grep_pattern, DECOMPILED_C],
                    capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60
                )
                if result.stdout.strip():
                    parts = result.stdout.strip().split(':', 1)
                    offset = int(parts[0])
                    return {"pos": offset, "class": type_name, "method": method_name, "c_name": c_name}
            except Exception:
                pass

        # Strategy 4: RVA alias — IL2CPP deduplicates methods with identical bodies.
        # Try ALL script.json entries at this RVA (the method may exist under a lambda name).
        rva_all = load_rva_map_all()
        aliases = rva_all.get(rva, [])
        for alias_name in aliases:
            if alias_name == c_name:
                continue
            try:
                # Convert dotted name to C name: Core.X.Y$$Method → Core_X_Y__Method
                alias_c = alias_name.replace('.', '_').replace('$$', '__').replace('|', '_')
                # Use -F (fixed string) to avoid regex issues with <> in lambda names
                # Don't include ( — Ghidra may wrap signature to next line
                grep_pattern = alias_c
                result = subprocess.run(
                    ['grep', '-b', '-m', '1', '-F', grep_pattern, DECOMPILED_C],
                    capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60
                )
                if result.stdout.strip():
                    parts = result.stdout.strip().split(':', 1)
                    offset = int(parts[0])
                    return {"pos": offset, "class": type_name, "method": method_name,
                            "c_name": alias_c, "alias_of": alias_name}
            except Exception:
                pass

    return None


def transcribe_type(type_name, method_filter=None, typedef_index=None):
    """Transcribe all methods of a type from decompiled_all.c."""
    print(f"Transcribing type: {type_name}")

    dec_index = load_decompiled_index()
    obf_strings = load_obfuscated_strings()
    recovered_methods = load_recovered_methods()

    methods, full_namespace = find_type_methods(type_name, typedef_index=typedef_index)
    print(f"  Found {len(methods)} methods in dump.cs (namespace: {full_namespace})")

    if method_filter:
        methods = [m for m in methods if method_filter.lower() in m["name"].lower()]
        print(f"  Filtered to {len(methods)} matching '{method_filter}'")

    results = []
    for method in methods:
        va = method.get("va")
        if va and va in recovered_methods:
            recovered_entry = recovered_methods[va]
            body = recovered_entry["body"]
            analysis = analyze_method_body(body, obf_strings)
            results.append({
                "method": method["name"],
                "signature": method["signature"],
                "line": method.get("line"),
                "va": hex(va),
                "status": "OK",
                "source": recovered_entry["source"],
                "analysis": analysis,
                "bodyPreview": body[:500] + "..." if len(body) > 500 else body,
            })
            continue

        # Try indexed lookup first (namespace-qualified types)
        possible_keys = [
            f"{type_name}$${method['name']}",
            f"{type_name}$$.{method['name']}",
        ]
        if full_namespace:
            possible_keys.append(f"{full_namespace}$${method['name']}")
            possible_keys.append(f"{full_namespace}$$.{method['name']}")
            possible_keys.append(f"{full_namespace}.{type_name}$${method['name']}")
            possible_keys.append(f"{full_namespace}.{type_name}$$.{method['name']}")
        func_entry = None
        for key in possible_keys:
            if key in dec_index:
                func_entry = dec_index[key]
                break

        # Fast check for trivial methods before expensive grep fallbacks
        if not func_entry:
            sig = method.get("signature", "")
            if method["name"] in ("SetStateMachine", "MoveNext") and ("DisplayClass" in sig or "d__" in sig or not sig):
                results.append({"method": method["name"], "signature": sig, "status": "TRIVIAL",
                                "note": "Async infrastructure"})
                continue
            if method["name"].startswith("get_m_") and "bool" in sig:
                results.append({"method": method["name"], "signature": sig, "status": "TRIVIAL",
                                "note": "Property getter — returns constant bool"})
                continue
            if len(method["name"]) <= 5 and "private" in sig and sig.count(',') == 0 and "void" not in sig:
                results.append({"method": method["name"], "signature": sig, "status": "TRIVIAL",
                                "note": "Simple field getter"})
                continue

        # Fallback: direct grep for C function name (with full namespace + VA)
        if not func_entry:
            func_entry = find_c_function(type_name, method['name'], full_namespace, method.get('va'))

        if not func_entry:
            # Additional trivial methods that don't need a decompiled body
            sig = method["signature"]
            if method["name"] in ("SetStateMachine", "MoveNext"):
                results.append({"method": method["name"], "signature": sig, "status": "TRIVIAL",
                                "note": "Async infrastructure"})
                continue
            if method["name"].startswith("get_m_") and "bool" in sig:
                results.append({"method": method["name"], "signature": sig, "status": "TRIVIAL",
                                "note": "Property getter — returns constant bool"})
                continue
            # Simple field getters (private T methodName() with short 3-4 letter names)
            if len(method["name"]) <= 5 and "private" in sig and sig.count(',') == 0 and "void" not in sig:
                results.append({"method": method["name"], "signature": sig, "status": "TRIVIAL",
                                "note": "Simple field getter"})
                continue
            if "b__" in method["name"]:
                results.append({"method": method["name"], "signature": sig, "status": "TRIVIAL",
                                "note": "Lambda/anonymous sort comparator"})
                continue
            if method["name"] == "MoveNext" and "DisplayClass" in sig or "d__" in sig:
                results.append({"method": method["name"], "signature": sig, "status": "TRIVIAL",
                                "note": "Async state machine MoveNext"})
                continue
            # Last resort: if no VA, try direct grep for TypeName__MethodName
            if not method.get("va"):
                import subprocess
                for probe_prefix in [type_name, full_namespace.replace('.', '_') if full_namespace else type_name]:
                    try:
                        r = subprocess.run(
                            ['grep', '-b', '-m', '1', f' {probe_prefix}__{method["name"]}(', DECOMPILED_C],
                            capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60
                        )
                        if r.stdout.strip():
                            offset = int(r.stdout.strip().split(':')[0])
                            func_entry = {"pos": offset, "class": type_name, "method": method["name"]}
                            break
                    except Exception:
                        pass
                if func_entry:
                    body = read_function_body(func_entry)
                    if body and len(body) > 20:
                        analysis = analyze_method_body(body, obf_strings)
                        results.append({"method": method["name"], "signature": method["signature"],
                                        "status": "OK", "analysis": analysis,
                                        "bodyPreview": body[:500] + "..." if len(body) > 500 else body})
                        continue

            results.append({
                "method": method["name"],
                "signature": method["signature"],
                "line": method.get("line"),
                "status": "NOT_FOUND",
            })
            continue

        # Read and analyze the body
        body = read_function_body(func_entry)
        if not body or len(body) < 20:
            results.append({
                "method": method["name"],
                "signature": method["signature"],
                "status": "EMPTY_BODY",
            })
            continue

        analysis = analyze_method_body(body, obf_strings)
        results.append({
            "method": method["name"],
            "signature": method["signature"],
            "line": method["line"],
            "va": hex(method["va"]),
            "status": "OK",
            "analysis": analysis,
            "bodyPreview": body[:500] + "..." if len(body) > 500 else body,
        })

    # Summary
    ok = sum(1 for r in results if r["status"] == "OK")
    not_found = sum(1 for r in results if r["status"] == "NOT_FOUND")
    empty = sum(1 for r in results if r["status"] == "EMPTY_BODY")
    print(f"  Results: {ok} OK, {not_found} not found, {empty} empty body")

    # Save
    os.makedirs(TRANSCRIBED_DIR, exist_ok=True)
    safe_name = type_name.replace('.', '_').replace('<', '_').replace('>', '_')
    out_path = os.path.join(TRANSCRIBED_DIR, f"{safe_name}.json")
    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump({
            "type": type_name,
            "methodCount": len(methods),
            "transcribed": ok,
            "methods": results,
        }, f, indent=2, ensure_ascii=False)
    print(f"  Saved to: {out_path}")

    # Print summary of interesting methods
    print(f"\n  Methods with screen transitions:")
    for r in results:
        if r["status"] == "OK" and r["analysis"]["hasScreenOpen"]:
            print(f"    {r['method']}: {r['analysis']['opsCount']} ops, opens screen")
    print(f"  Async methods:")
    for r in results:
        if r["status"] == "OK" and r["analysis"]["hasAsync"]:
            print(f"    {r['method']}: async state machine")
    print(f"  Methods with most operations:")
    for r in sorted([r for r in results if r["status"] == "OK"],
                    key=lambda x: x["analysis"]["opsCount"], reverse=True)[:5]:
        a = r["analysis"]
        print(f"    {r['method']}: {a['opsCount']} ops ({a['callCount']} calls, {a['fieldSetCount']} fields)")

    return results


def list_type_methods(type_name):
    """List all methods of a type from dump.cs."""
    methods, ns = find_type_methods(type_name)
    print(f"{type_name} ({ns}): {len(methods)} methods")
    for m in methods:
        print(f"  {m['name']:30s} VA=0x{m['va']:X}  L{m['line']}")
        if m['signature']:
            print(f"    {m['signature'][:100]}")


def transcribe_all_handlers():
    """Transcribe all screen handler types."""
    handlers = load_cached("screen_handlers.json")
    if not handlers:
        print("ERROR: screen_handlers.json not found. Run: py analyze_client.py --phase dump")
        return

    # Key handlers to transcribe
    key_types = [
        "fpm",           # Auth screen manager
        "fys",           # Login handler
        "fyr",           # ConnectionBackground
        "fyv",           # PreGameMainMenu
        "Core.UILogic.Auth.ServerSelection",
        "Core.UILogic.Auth.CharacterSelection",
        "Core.UILogic.Auth.CharacterCreation",
    ]

    for type_name in key_types:
        print(f"\n{'='*60}")
        transcribe_type(type_name)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(0)

    if sys.argv[1] == "--handlers":
        transcribe_all_handlers()
    elif sys.argv[1] == "--list":
        if len(sys.argv) < 3:
            print("Usage: --list <TypeName>")
            sys.exit(1)
        list_type_methods(sys.argv[2])
    elif sys.argv[1] == "--services":
        services = load_cached("services.json")
        if services:
            for svc in list(services.values())[:10]:
                if isinstance(svc, dict) and "type" in svc:
                    transcribe_type(svc["type"])
    else:
        type_name = sys.argv[1]
        method_filter = None
        typedef_index = None
        i = 2
        while i < len(sys.argv):
            if sys.argv[i] == "--typedef" and i + 1 < len(sys.argv):
                typedef_index = int(sys.argv[i + 1])
                i += 2
            else:
                method_filter = sys.argv[i]
                i += 1
        transcribe_type(type_name, method_filter, typedef_index=typedef_index)
