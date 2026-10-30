#!/usr/bin/env python3
"""
Resolve all 239 service registrations from the recovered GameCompositor.bkqa body.

Traces each bkpt<T> and bkpw<T,U> call to:
  - Registered interface key(s)
  - Concrete class instantiated (via il2cpp_object_new / TypeInfo)
  - Constructor invoked (.ctor) and argument dependencies
  - Or field bindings on GameCompositor (dxob, dxoc, dxod)

Provenance:
  - Body: GameCompositor.bkqa (VA: 0x181e36360)
  - Evidence: ev_2cab1b810a461412627f36c71eb4fad30457d1670244c940d8bf361a92de01df
  - Symbols: Decompiled/script.json
"""

import json
import os
import re
import sys
import hashlib

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(SCRIPT_DIR))
OUTPUT_DIR = os.environ.get("GENERATED_OUTPUT_DIR", os.path.join(REPO_ROOT, "Generated", "output"))


def compute_sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(65536):
            h.update(chunk)
    return h.hexdigest()


def load_recovered_bkqa_code():
    recovery_path = os.path.join(
        REPO_ROOT,
        "Generated",
        "output",
        "decompilation-recovery-232df1ab-0191-4602-8a04-21db2cd3f0f1",
        "000-0x181e36360.json",
    )
    if not os.path.exists(recovery_path):
        raise FileNotFoundError(f"Recovery file not found: {recovery_path}")

    with open(recovery_path, "r", encoding="utf-8") as f:
        rec = json.load(f)

    evidence_id = rec.get("structuredContent", {}).get("evidence_id")
    code = rec.get("structuredContent", {}).get("normalized_result")
    if not code:
        raise ValueError("Normalized result is empty in recovery file")

    return recovery_path, evidence_id, code


def load_script_symbols():
    script_path = os.path.join(REPO_ROOT, "Decompiled", "script.json")
    if not os.path.exists(script_path):
        raise FileNotFoundError(f"script.json not found: {script_path}")

    with open(script_path, "r", encoding="utf-8") as f:
        script = json.load(f)

    base = 0x180000000
    metadata = {}
    metadata_method = {}
    method = {}

    for item in script.get("ScriptMetadata", []):
        addr_key = "dat_" + hex(base + item["Address"])[2:].lower()
        metadata[addr_key] = item["Name"]

    for item in script.get("ScriptMetadataMethod", []):
        addr_key = "dat_" + hex(base + item["Address"])[2:].lower()
        metadata_method[addr_key] = item["Name"]

    for item in script.get("ScriptMethod", []):
        addr_key = "fun_" + hex(base + item["Address"])[2:].lower()
        method[addr_key] = item["Name"]

    return script_path, metadata, metadata_method, method


def parse_service_catalog():
    recovery_path, evidence_id, code = load_recovered_bkqa_code()
    script_path, metadata, metadata_method, method = load_script_symbols()

    lines = code.splitlines()
    var_source = {}
    catalog = []

    # Regex patterns
    re_new = re.compile(r"(\w+)\s*=\s*(?:thunk_)?FUN_1805a5520\((DAT_[0-9a-fA-F]+)\);")
    re_ctor = re.compile(r"(\w+)\(([^)]+)\);")
    re_reg = re.compile(r"(FUN_182f89620|FUN_182f899d0)\(param_1,\s*([^,]+),\s*(DAT_[0-9a-fA-F]+)\);")

    # Field map for GameCompositor fields
    field_map = {
        "0x50": {"field": "dxob", "fieldType": "ezp", "registryType": "ezp"},
        "0x60": {"field": "dxod", "fieldType": "fyh", "registryType": "ezf"},
        "0x58": {"field": "dxoc", "fieldType": "ecr", "registryType": "fcx"},
    }

    order_index = 0
    for i, line in enumerate(lines):
        line_clean = line.strip()

        # 1. Check for registration call first (so it does not get intercepted by ctor matcher)
        m_reg = re_reg.match(line_clean)
        if m_reg:
            helper_fn, instance_expr, reg_dat = m_reg.groups()
            reg_symbol = metadata_method.get(reg_dat.lower(), "UNKNOWN")
            helper_family = "bkpt" if helper_fn == "FUN_182f89620" else "bkpw"

            generic_types = []
            m_gen = re.search(r"<(.*)>", reg_symbol)
            if m_gen:
                generic_types = [t.strip() for t in m_gen.group(1).split(",")]

            entry = {
                "order": order_index,
                "lineIndex": i,
                "helper": helper_family,
                "registrationSymbol": reg_symbol,
                "genericTypes": generic_types,
                "instanceExpr": instance_expr,
            }

            if instance_expr in var_source:
                det = var_source[instance_expr]
                entry["sourceType"] = "instantiated"
                entry["concreteClass"] = det["class_name"]
                entry["classDat"] = det["class_dat"]
                entry["constructorMethod"] = det["ctor_name"]
                entry["constructorArgs"] = det["ctor_args"]
            elif "param_1 +" in instance_expr:
                entry["sourceType"] = "field"
                m_offset = re.search(r"param_1 \+ (0x[0-9a-fA-F]+|\d+)", instance_expr)
                offset = m_offset.group(1) if m_offset else None
                entry["fieldOffset"] = offset
                f_info = field_map.get(offset, {})
                entry["fieldName"] = f_info.get("field")
                entry["concreteClass"] = f_info.get("fieldType")
            else:
                entry["sourceType"] = "expression"
                entry["concreteClass"] = "UNKNOWN"

            catalog.append(entry)
            order_index += 1
            continue

        # 2. New object instantiation: var = thunk_FUN_1805a5520(DAT_...);
        m_new = re_new.match(line_clean)
        if m_new:
            var_name, class_dat = m_new.groups()
            raw_class_name = metadata.get(class_dat.lower(), "UNKNOWN")
            clean_class_name = raw_class_name
            if clean_class_name.endswith("_TypeInfo"):
                clean_class_name = clean_class_name[:-9]

            var_source[var_name] = {
                "class_dat": class_dat,
                "class_name": clean_class_name,
                "ctor": None,
                "ctor_name": None,
                "ctor_args": [],
            }
            continue

        # 3. Constructor invocation
        m_ctor = re_ctor.match(line_clean)
        if m_ctor:
            fn_name, args_str = m_ctor.groups()
            args = [a.strip() for a in args_str.split(",")]
            if args and args[0] in var_source:
                var_source[args[0]]["ctor"] = fn_name
                var_source[args[0]]["ctor_name"] = method.get(fn_name.lower(), "UNKNOWN")
                var_source[args[0]]["ctor_args"] = args[1:]
            continue

    result = {
        "schemaVersion": 1,
        "evidenceId": evidence_id,
        "recoverySource": recovery_path,
        "recoverySha256": compute_sha256(recovery_path),
        "scriptSymbolsSha256": compute_sha256(script_path),
        "totalRegistrations": len(catalog),
        "counts": {
            "total": len(catalog),
            "instantiated": sum(1 for c in catalog if c["sourceType"] == "instantiated"),
            "field": sum(1 for c in catalog if c["sourceType"] == "field"),
            "expression": sum(1 for c in catalog if c["sourceType"] == "expression"),
        },
        "services": catalog,
    }

    out_path = os.path.join(OUTPUT_DIR, "ReconstructedServiceCatalog.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2)

    print(f"Generated ReconstructedServiceCatalog.json with {len(catalog)} registrations.")
    print(f"  Instantiated: {result['counts']['instantiated']}")
    print(f"  Fields: {result['counts']['field']}")
    print(f"  Expressions: {result['counts']['expression']}")
    return out_path, result


if __name__ == "__main__":
    parse_service_catalog()

