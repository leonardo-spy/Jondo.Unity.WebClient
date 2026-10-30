#!/usr/bin/env python3
"""
Universal runtime data extractor — extracts EVERYTHING the WebGL client needs.

Instead of running 10 separate Python scripts, this single tool extracts all
runtime data from the original client and outputs it as HTTP-servable files.
The C# WebGLProbe becomes a thin loader — download JSON/PNG, upload to GPU.

Usage:
    py extract_all_runtime_data.py                    # extract everything
    py extract_all_runtime_data.py --map 191104002    # extract one map fully
    py extract_all_runtime_data.py --inventory        # show what's extracted vs missing
    py extract_all_runtime_data.py --baseline         # hash inputs and verify saved REA identity
    py extract_all_runtime_data.py --ledger-input     # prepare an open REA obligation ledger request
    py extract_all_runtime_data.py --renderer-contract # extract renderer injection signature from REA metadata
    py extract_all_runtime_data.py --registration-observations <run.json> # inspect captured native registration

Data extracted:
    Map/Json/{mapId}.json          — MapMetadata (cells, elements, neighbors, atlas rects)
    Map/Sprites/1x/{gfxId}.png    — Individual sprite textures
    Map/Composed/{mapId}_*.png    — Pre-composed atlas textures per layer
    Map/Composed/{mapId}_render.json — Pre-computed vertex/UV/color data
    CellGrid.json                  — Cell coordinate constants from oh/hdk/hdl classes
    RenderConfig.json              — Camera, shader, layer, render queue constants
    CharacterLooks.json            — Character breed/look data for rendering
    EntityConfig.json              — Displayable/SortUnit config for entity rendering
"""

import argparse
import hashlib
import json
import os
import re
import sys
import time

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.join(SCRIPT_DIR, "..", "..")
CLIENT_DIR = os.environ.get("DOFUS_CLIENT_DIR", os.path.join(REPO_ROOT, "Cliente 3.6.10.11"))
CONTENT_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content")
DUMP_CS = os.path.join(os.environ.get("DECOMPILED_DIR", os.path.join(REPO_ROOT, "Decompiled")), "dump.cs")
OUTPUT_DIR = os.path.join(CONTENT_DIR)  # serve directly via /assets/


def read_dump_constants(dump_path, fields):
    requested = set(fields)
    owners = {field.rsplit(".", 1)[0] for field in requested}
    constants = {}
    source_hash = hashlib.sha256()
    owner = None
    class_pattern = re.compile(r"^(?:public|internal|private|protected)\s+(?:static\s+)?class\s+(\w+)\b")
    field_pattern = re.compile(r"^\s*(?:public|private|internal|protected)\s+const\s+(int|uint|float)\s+(\w+)\s*=\s*([^;]+);")
    with open(dump_path, "rb") as source:
        for line_number, raw_line in enumerate(source, 1):
            source_hash.update(raw_line)
            line = raw_line.decode("utf-8-sig")
            declaration = class_pattern.match(line)
            if declaration:
                owner = declaration.group(1)
            elif line.startswith("}"):
                owner = None
            if owner not in owners:
                continue
            field = field_pattern.match(line)
            if not field:
                continue
            value_type, name, literal = field.groups()
            symbol = f"{owner}.{name}"
            if symbol not in requested:
                continue
            if symbol in constants:
                raise ValueError(f"Ambiguous constant {symbol} in {dump_path}:{line_number}")
            literal = literal.strip()
            if value_type == "float":
                literal = literal.rstrip("fF").replace(",", ".")
                if not re.fullmatch(r"[+-]?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?", literal):
                    raise ValueError(f"Unsupported float {symbol}: {literal}")
                value = float(literal)
            else:
                literal = literal.rstrip("uU")
                if not re.fullmatch(r"[+-]?(?:\d+|0[xX][0-9a-fA-F]+)", literal):
                    raise ValueError(f"Unsupported integer {symbol}: {literal}")
                value = int(literal, 16 if "0x" in literal.lower() else 10)
            constants[symbol] = {"value": value, "type": value_type, "line": line_number}
    missing = requested - constants.keys()
    if missing:
        raise ValueError(f"Missing constants in {dump_path}: {', '.join(sorted(missing))}")
    return constants, source_hash.hexdigest()


def extract_cell_grid_constants(dump_path=None, provenance=None):
    """Extract cell grid values without changing the existing runtime schema."""
    bindings = {
        "cellCount": "oh.ddji",
        "mapHalfWidth": "oh.ddjj",
        "mapHalfHeight": "oh.ddjk",
        "cellHalfWidth": "oh.ddjc",
        "cellHalfHeight": "oh.ddjd",
        "tileFullWidth": "hdk.ebdg",
        "tileHalfWidth": "hdk.ebdh",
        "tileHalfHeight": "hdk.ebdi",
        "tileQuarter": "hdk.ebdj",
        "cellDiagonal": "hdk.ebdn",
        "mapMinX": "hdl.ebds",
        "mapMinY": "hdl.ebdt",
        "mapMaxX": "hdl.ebdu",
        "mapMaxY": "hdl.ebdv",
        "mapFullWidth": "hdl.ebdw",
        "mapFullHeight": "hdl.ebdx",
        "worldIdMax": "oh.ddjg",
        "mapCoordsMax": "oh.ddjh",
    }
    dump_path = dump_path or DUMP_CS
    constants, source_hash = read_dump_constants(dump_path, bindings.values())
    if provenance is not None:
        provenance.update({
            "source": {"path": os.path.abspath(dump_path), "sha256": source_hash},
            "fields": {key: {"symbol": symbol, **constants[symbol]} for key, symbol in bindings.items()},
        })
    return {key: constants[symbol]["value"] for key, symbol in bindings.items()}


def describe_artifact(path):
    digest = hashlib.sha256()
    size = 0
    with open(path, "rb") as artifact:
        first = artifact.read(1024 * 1024)
        if first.startswith(b"version https://git-lfs.github.com/spec/v1"):
            raise ValueError(f"Git LFS pointer is not an extracted artifact: {path}")
        digest.update(first)
        size += len(first)
        for chunk in iter(lambda: artifact.read(1024 * 1024), b""):
            digest.update(chunk)
            size += len(chunk)
    if not size:
        raise ValueError(f"Empty artifact: {path}")
    return {"path": os.path.abspath(path), "bytes": size, "sha256": digest.hexdigest()}


def build_runtime_baseline(client_dir=None, dump_path=None, evidence_path=None):
    client_dir = client_dir or CLIENT_DIR
    dump_path = dump_path or DUMP_CS
    decompiled_dir = os.path.dirname(os.path.abspath(dump_path))
    evidence_path = evidence_path or os.path.join(REPO_ROOT, "Generated", "gameassembly_rea.json")
    paths = {
        "gameAssembly": os.path.join(client_dir, "GameAssembly.dll"),
        "globalMetadata": os.path.join(client_dir, "Dofus_Data", "il2cpp_data", "Metadata", "global-metadata.dat"),
        "dumpCs": dump_path,
        "scriptJson": os.path.join(decompiled_dir, "script.json"),
        "decompiledC": os.path.join(decompiled_dir, "decompiled_all.c"),
        "savedReaOverview": evidence_path,
    }
    artifacts = {name: describe_artifact(path) for name, path in paths.items()}
    with open(evidence_path, encoding="utf-8-sig") as source:
        evidence = json.load(source)
    expected_hash = evidence["subject"]["digest"]["sha256"].lower()
    if expected_hash != artifacts["gameAssembly"]["sha256"]:
        raise ValueError("GameAssembly digest does not match the saved REA overview")
    if evidence.get("operation") != "binary_overview" or not evidence.get("evidence_id"):
        raise ValueError("Expected an identified REA binary_overview Evidence record")
    provenance = {}
    grid = extract_cell_grid_constants(dump_path, provenance)
    if provenance["source"]["sha256"] != artifacts["dumpCs"]["sha256"]:
        raise ValueError("dump.cs changed while building the baseline")
    return {
        "schemaVersion": 1,
        "artifacts": artifacts,
        "reaEvidence": {
            "gameAssemblyOverview": {
                "evidenceId": evidence["evidence_id"],
                "operation": evidence["operation"],
                "binaryDigestMatches": True,
                "limitations": evidence.get("limitations", []),
            },
        },
        "cellGrid": {"values": grid, "provenance": provenance},
        "limitations": [
            "File hashes identify current inputs; dump.cs and exports are not proven to originate from this binary.",
            "Grid values are observations from dump.cs, not runtime equivalence claims.",
            "The saved binary overview does not prove a live Ghidra session or function behavior.",
            "This baseline does not launch or verify the client runtime.",
        ],
    }


def build_reconstruction_ledger_input(baseline, managed_bundle, native_bundle, native_function_bundle=None):
    bundle = json.loads(json.dumps(managed_bundle))
    records = {record["evidence_id"]: record for record in bundle["records"]}
    native_sources = [native_bundle] + ([native_function_bundle] if native_function_bundle is not None else [])
    for source in native_sources:
        for record in source["records"]:
            records[record["evidence_id"]] = record
    bundle["records"] = sorted(records.values(), key=lambda record: record["evidence_id"])
    for collection in ("artifacts", "providers", "environments", "scenarios", "captures", "unknowns"):
        entries = bundle[collection] + [item for source in native_sources for item in source[collection]]
        canonical = {json.dumps(item, sort_keys=True, separators=(",", ":"), ensure_ascii=False): item for item in entries}
        bundle[collection] = [canonical[key] for key in sorted(canonical)]
    bundle["artifacts"].sort(key=lambda artifact: json.dumps(
        {key: artifact[key] for key in ("digest", "format", "architecture")},
        separators=(",", ":"), ensure_ascii=False))
    binary_hash = baseline["artifacts"]["gameAssembly"]["sha256"]
    identities = [record for record in native_bundle["records"]
                  if record.get("operation") == "inspect_native_exports"
                  and record.get("subject", {}).get("digest", {}).get("sha256") == binary_hash]
    if len(identities) != 1:
        raise ValueError("Ledger requires one native export Evidence matching the baseline binary")
    identity = identities[0]
    native_functions = []
    if native_function_bundle is not None:
        native_functions = [record for record in native_function_bundle["records"]
                            if record.get("operation") == "analyze_function"]
        if len(native_functions) != 1 or native_functions[0].get("subject", {}).get("digest", {}).get("sha256") != binary_hash:
            raise ValueError("Ledger native function Evidence does not match the baseline binary")
    definitions = [
        ("baseline.identity", "Bind current inputs to the analyzed client", "packaging", "static", "unit", [],
         "Artifact digests; dump/export origin still requires independent confirmation", "build_runtime_baseline"),
        ("boot.lifecycle", "Reconstruct ordered original service lifecycle", "runtime", "runtime", "integration", ["baseline.identity"],
         "Compositor.Awake, bkqj, bkqa, bkpy, bkpz, bkqe, bkqk and async continuations", "OriginalBootFlow"),
        ("grid.constants", "Extract grid constants with source provenance", "runtime", "static", "unit", ["baseline.identity"],
         "oh/hdk/hdl constants; exports are artifact context, not field-level proof", "extract_cell_grid_constants"),
        ("map.rendering", "Reconstruct original map geometry and rendering", "runtime", "runtime", "renderer", ["boot.lifecycle", "grid.constants"],
         "MapRenderer, MapDisplayService, hde mesh geometry, materials and sorting", "BuildMapFromComposed"),
        ("entity.displayables", "Reconstruct entity lifecycle and character animation", "runtime", "runtime", "renderer", ["boot.lifecycle", "map.rendering"],
         "DisplayableService, kub handler, looks and Animator2D", None),
        ("movement.transitions", "Reconstruct movement and map transitions", "protocol", "runtime", "protocol", ["entity.displayables", "grid.constants"],
         "Pathfinding, movement messages, map load cancellation and teardown", None),
        ("game.screens", "Reconstruct in-game screen lifecycle and bindings", "application", "runtime", "integration", ["boot.lifecycle", "entity.displayables"],
         "HUD, inventory, chat and remaining artifact-discovered screens", None),
        ("game.fight", "Reconstruct complete combat lifecycle", "application", "runtime", "integration", ["movement.transitions", "game.screens"],
         "Placement, turns, targeting, actions, results and return to roleplay", None),
    ]
    reviewed = []
    bindings = []
    extractor_path = os.path.abspath(__file__)
    probe_path = os.path.join(REPO_ROOT, "UnityProject", "Assets", "WebGLProbe.cs")
    for identifier, title, layer, authority, proof, dependencies, location, owner_symbol in definitions:
        reviewed.append({
            "obligation_id": identifier, "obligation_version": 1, "title": title,
            "application_layer": layer, "family": identifier.split(".")[0],
            "target": {"artifact_sha256": binary_hash, "application_node_id": None,
                       "semantic_node_id": None, "location": location},
            "required": True, "required_case_kinds": ["positive", "negative"],
            "required_original_authority": authority,
            "required_fixture_authority": proof, "required_verifier_authority": proof,
            "requires_parser_type": identifier == "map.rendering",
            "dependency_obligation_ids": dependencies, "residual_unknown_ids": [],
            "unavailable_authority": [],
            "required_next_evidence": [f"Collect claim-specific original evidence and verifier proof for {identifier}; native exports supply identity context only."],
            "disposition": "active", "review_evidence_ids": [identity["evidence_id"]] +
                ([native_functions[0]["evidence_id"]] if identifier == "boot.lifecycle" and native_functions else []),
        })
        if owner_symbol is not None:
            owner_path = extractor_path if identifier in ("baseline.identity", "grid.constants") else probe_path
            bindings.append({
                "obligation_id": identifier,
                "owner": {"module_path": os.path.relpath(owner_path, REPO_ROOT).replace(os.sep, "/"),
                          "symbol": owner_symbol, "owner_sha256": describe_artifact(owner_path)["sha256"]},
                "parser_type": None, "original_cases": [], "fixtures": [], "verifier": None,
            })
    return {"evidence_bundle": bundle, "reviewed_obligations": reviewed,
            "manifest": {"bindings": bindings, "contradictions": []}}


def read_compositor_instance_fields(dump_path):
    fields = {}
    owner = False
    declarations = 0
    declaration = re.compile(r"^public class GameCompositor\b")
    field_pattern = re.compile(r"^\s*(?:public|private|protected|internal)\s+(\w[\w.<>\[\], ]*?)\s+(\w+);\s*//\s*0x([0-9a-fA-F]+)")
    digest = hashlib.sha256()
    with open(dump_path, "rb") as source:
        for line_number, raw in enumerate(source, 1):
            digest.update(raw)
            line = raw.decode("utf-8-sig")
            if declaration.match(line):
                declarations += 1
                owner = True
            elif line.startswith("}"):
                owner = False
            if not owner:
                continue
            match = field_pattern.match(line)
            if match and not match.group(1).startswith("static "):
                type_name, field_name, offset = match.groups()
                offset = int(offset, 16)
                if offset in fields:
                    raise ValueError(f"Ambiguous compositor field offset {hex(offset)}")
                fields[offset] = {"field": field_name, "fieldType": type_name, "offset": offset, "line": line_number}
    if declarations != 1:
        raise ValueError("Expected exactly one GameCompositor declaration")
    return fields, {"path": os.path.abspath(dump_path), "sha256": digest.hexdigest()}


def extract_registration_observations(run_path, script_path=None, address_view_path=None, dump_path=None):
    with open(run_path, encoding="utf-8-sig") as source:
        run = json.load(source)
    if run.get("status") != "captured" or run.get("exitCode") != 0:
        raise ValueError("Registration recovery has not captured successful Evidence")
    bundle_artifact = describe_artifact(run["evidenceBundle"])
    if bundle_artifact["sha256"] != run["evidenceBundleSha256"]:
        raise ValueError("Captured registration bundle digest changed")
    with open(run["evidenceBundle"], encoding="utf-8-sig") as source:
        bundle = json.load(source)
    records = [record for record in bundle["records"] if record.get("operation") == "analyze_function"]
    if len(records) != 1 or records[0]["evidence_id"] != run["evidenceId"]:
        raise ValueError("Recovery journal does not identify one native function Evidence")
    record = records[0]
    result = record["normalized_result"]
    if record["subject"]["digest"]["sha256"] != run["binarySha256"]:
        raise ValueError("Native function does not match the journal binary")
    if result["procedure"]["address"].lower() != run["address"].lower():
        raise ValueError("Native function does not match the journal procedure")
    if not result.get("facet_availability", {}).get("assembly", {}).get("available"):
        raise ValueError("Registration assembly is unavailable")
    script_path = script_path or os.path.join(os.path.dirname(DUMP_CS), "script.json")
    address_view_path = address_view_path or os.path.join(REPO_ROOT, "Generated", "output", "boot_addresses_view_rea.json")
    with open(address_view_path, encoding="utf-8-sig") as source:
        address_view = json.load(source)
    binary_source = address_view["normalized_result"]["binary_source"]
    if binary_source["artifact"]["sha256"] != run["binarySha256"]:
        raise ValueError("Metadata address view and native function belong to different binaries")
    if run["address"].lower() not in {
        item["address"].lower() for item in address_view["normalized_result"]["items"] if item.get("address")
    }:
        raise ValueError("Native procedure is not selected by the metadata address view")
    image_base = int(binary_source["image_base"], 16)
    with open(script_path, encoding="utf-8-sig") as source:
        script = json.load(source)
    slots = {}
    for collection, kind in (("ScriptMetadataMethod", "method"), ("ScriptMetadata", "type")):
        for item in script.get(collection, []):
            slot = image_base + item["Address"]
            symbol = {"kind": kind, "name": item["Name"]}
            if kind == "method":
                method_rva = item.get("MethodAddress", 0)
                symbol["methodAddress"] = hex(image_base + method_rva) if method_rva else None
            if symbol not in slots.setdefault(slot, []):
                slots[slot].append(symbol)
    method_names = {}
    for item in script.get("ScriptMethod", []):
        address = image_base + item["Address"]
        if item["Name"] not in method_names.setdefault(address, []):
            method_names[address].append(item["Name"])
    reads = []
    calls = []
    call_targets = {}
    metadata_reference_count = 0
    unmatched_data_reads = 0
    for reference_index, reference in enumerate(result["outgoing_references"]):
        target = reference.get("target_address", "")
        if not re.fullmatch(r"0x[0-9a-fA-F]+", target):
            continue
        address = int(target, 16)
        kind = reference["kind"]
        if kind.get("data"):
            symbols = slots.get(address)
            if symbols:
                metadata_reference_count += 1
                if kind.get("read"):
                    reads.append({"referenceIndex": reference_index, "instruction": reference["source_address"],
                                  "slot": target, "symbols": symbols})
            elif kind.get("read"):
                unmatched_data_reads += 1
        if kind.get("call"):
            call_targets.setdefault(target, {"symbols": method_names.get(address, [])})
            calls.append({"referenceIndex": reference_index, "instruction": reference["source_address"],
                          "target": target})
    reads.sort(key=lambda item: int(item["instruction"], 16))
    calls.sort(key=lambda item: int(item["instruction"], 16))
    compositor_reads = []
    families = {}
    pattern = re.compile(r"^Method\$Core\.Engine\.Compositor\.Compositor\.(\w+)<(.+)>\(\)$")
    for observation in reads:
        for symbol in observation["symbols"]:
            match = pattern.fullmatch(symbol["name"]) if symbol["kind"] == "method" else None
            if match:
                family, arguments = match.groups()
                compositor_reads.append({"instruction": observation["instruction"], "slot": observation["slot"],
                                         "referenceIndex": observation["referenceIndex"], "family": family,
                                         "genericArgumentsText": arguments, "symbol": symbol["name"]})
                families[family] = families.get(family, 0) + 1
    instructions = {}
    instruction_order = []
    for line in result["assembly"]:
        match = re.fullmatch(r"(0x[0-9a-fA-F]+):\s+(.+)", line)
        if match:
            address, text = match.groups()
            instructions[address] = (len(instruction_order), text)
            instruction_order.append((address, text))
    call_references = {(item["instruction"], item["target"]) for item in calls}
    helper_matches = []
    if record["subject"].get("architecture") == "x86_64":
        for observation in compositor_reads:
            position, text = instructions.get(observation["instruction"], (-1, ""))
            if text != f"MOV R8, qword ptr [{observation['slot']}]" or position + 3 >= len(instruction_order):
                continue
            argument_moves = instruction_order[position + 1:position + 3]
            instance = next((match for _, instruction in argument_moves
                             if (match := re.fullmatch(r"MOV RDX, (.+)", instruction))), None)
            compositor = next((match for _, instruction in argument_moves
                               if (match := re.fullmatch(r"MOV RCX, (.+)", instruction))), None)
            if instance is None or compositor is None:
                continue
            terminal_index = position + 3
            completion = "call"
            epilogue = ["MOV RBX, qword ptr [RSP + 0x30]", "MOV RSI, qword ptr [RSP + 0x38]",
                        "ADD RSP, 0x20", "POP RDI"]
            if [text for _, text in instruction_order[terminal_index:terminal_index + len(epilogue)]] == epilogue:
                terminal_index += len(epilogue)
                completion = "tail-jump"
            if terminal_index >= len(instruction_order):
                continue
            call_address, call_text = instruction_order[terminal_index]
            opcode = "JMP" if completion == "tail-jump" else "CALL"
            target = re.fullmatch(rf"{opcode} (0x[0-9a-fA-F]+)", call_text)
            if instance is None or compositor is None or target is None:
                continue
            target_address = target.group(1)
            prefix = f"Core.Engine.Compositor.Compositor$${observation['family']}<"
            aliases = call_targets.get(target_address, {}).get("symbols", [])
            if (call_address, target_address) not in call_references or not any(name.startswith(prefix) for name in aliases):
                continue
            helper_matches.append({**observation, "callInstruction": call_address, "target": target_address,
                                   "instanceOperand": instance.group(1), "compositorOperand": compositor.group(1),
                                   "completion": completion})
    fields, field_source = read_compositor_instance_fields(dump_path or DUMP_CS)
    field_bindings = []
    unresolved_fields = []
    for observation in helper_matches:
        field = re.fullmatch(r"qword ptr \[(\w+) \+ (0x[0-9a-fA-F]+)\]", observation["instanceOperand"])
        if field is None:
            continue
        offset = int(field.group(2), 16)
        if field.group(1) != observation["compositorOperand"] or offset not in fields:
            unresolved_fields.append(observation)
            continue
        if observation["family"] != "bkpt" or not re.fullmatch(r"[\w.]+", observation["genericArgumentsText"]):
            unresolved_fields.append(observation)
            continue
        field_bindings.append({**fields[offset], "registryType": observation["genericArgumentsText"],
                               "instruction": observation["instruction"], "callInstruction": observation["callInstruction"]})
    return {
        "schemaVersion": 1,
        "source": {"evidenceId": record["evidence_id"], "bundle": bundle_artifact,
                   "binarySha256": run["binarySha256"], "imageBase": hex(image_base),
                   "scriptJson": describe_artifact(script_path),
                   "compositorFields": field_source,
                   "addressViewEvidenceId": address_view["evidence_id"]},
        "procedure": result["procedure"], "facetAvailability": result["facet_availability"],
        "counts": {"assembly": len(result["assembly"]), "basicBlocks": len(result["basic_blocks"]),
                   "callees": len(result["callees"]), "metadataReferences": metadata_reference_count,
                   "metadataReads": len(reads), "unmappedDataReads": unmatched_data_reads,
                   "compositorGenericReads": len(compositor_reads), "helperCallMatches": len(helper_matches),
                   "unmatchedGenericReads": len(compositor_reads) - len(helper_matches),
                   "existingFieldRegistrations": len(field_bindings), "unresolvedFieldOperands": len(unresolved_fields)},
        "metadataReads": reads, "calls": calls, "callTargets": call_targets, "compositorGenericReads": compositor_reads,
        "helperCallMatches": helper_matches,
        "existingFieldRegistrations": field_bindings, "unresolvedFieldOperands": unresolved_fields,
        "genericFamilyCounts": families, "runtimeReady": False,
        "ordering": "ascending-instruction-address-not-execution-order",
        "limitations": record.get("limitations", []) + result.get("limitations", []) + [
            "This local cross-reference projection is not a new REA Evidence record.",
            "Script metadata slots are correlated by exact address; common build origin is not independently proved.",
            "Generic arguments remain literal text, not an inferred interface-to-implementation registration.",
            "Metadata preparation references are not registrations; data reads do not prove calls or runtime execution.",
            "Helper matches require one exact x64 instruction pattern and a matching call reference; register operands are not resolved service instances.",
            "Aliases are retained; indirect flow and service initialization order are not reconstructed by this projection.",
        ],
    }


def extract_renderer_boot_contract(metadata_path=None):
    metadata_path = metadata_path or os.path.join(REPO_ROOT, "Generated", "il2cpp_metadata_rea.json")
    with open(metadata_path, encoding="utf-8-sig") as source:
        bundle = json.load(source)
    records = [record for record in bundle["records"] if record.get("operation") == "inspect_il2cpp_metadata"]
    if len(records) != 1:
        raise ValueError("Renderer contract requires exactly one IL2CPP metadata Evidence record")
    record = records[0]
    report = record["normalized_result"]
    owners = [index for index, owner in enumerate(report["types"])
              if owner["name"] == "MapRenderer" and owner["namespace"] == "Core.Rendering"]
    if len(owners) != 1:
        raise ValueError("Expected exactly one Core.Rendering.MapRenderer owner")
    methods = [index for index, method in enumerate(report["methods"])
               if method["name"] == "qcu" and method["declaring_type"] == owners[0]]
    if len(methods) != 1:
        raise ValueError("Expected exactly one MapRenderer.qcu initializer")
    method_index = methods[0]
    address = report["method_addresses"][method_index]
    if address["state"] != "code" or not address["address"]:
        raise ValueError("MapRenderer.qcu has no resolved native code address")
    if len(report["parameters"]) != len(report["parameter_types"]):
        raise ValueError("Parameter/type collections are not aligned")
    parameters = []
    for metadata_index, parameter in enumerate(report["parameters"]):
        if parameter["method"] != method_index:
            continue
        parameter_type = report["parameter_types"][metadata_index]
        if not parameter_type["name"]:
            raise ValueError(f"Unresolved MapRenderer.qcu parameter type at {metadata_index}")
        parameters.append({
            "position": len(parameters), "metadataIndex": metadata_index,
            "name": parameter["name"], "typeName": parameter_type["name"], "typeIndex": parameter_type["type"],
        })
    if not parameters:
        raise ValueError("MapRenderer.qcu parameter inventory is empty")
    return {
        "schemaVersion": 1,
        "source": {"evidenceId": record["evidence_id"], "metadata": report["artifact"],
                   "binary": report["binary"]["artifact"]},
        "owner": {"namespace": "Core.Rendering", "name": "MapRenderer", "metadataIndex": owners[0]},
        "method": {"name": "qcu", "metadataIndex": method_index, **address},
        "parameters": parameters,
        "runtimeReady": False,
        "limitations": [
            "This is a local projection of retained REA metadata, not a new REA Evidence record.",
            "A resolved signature does not establish service instances, registration order or initialized behavior.",
        ],
    }


def extract_render_config():
    """Extract rendering constants from dump.cs (MapRenderer, hdp, qi, etc.)."""
    config = {
        "camera": {
            "mainOrthoSize": 500,       # MapRenderer.MainCameraOrthographicSize
            "waveOrthoSize": 688,       # MapRenderer.WaveCameraOrthographicSize
            "mapCenterX": 21.5,         # midpoint of [-580.5, 623.5]
            "mapCenterY": 0.0,          # midpoint of [-440.75, 440.75]
        },
        "renderQueue": {
            "base": 3000,               # hdp.ebeg
            "next": 3001,               # hdp.ebeh
        },
        "layers": {
            "default": "Default",       # qi.ddox
            "background": "Background", # qi.ddoy
        },
        "maxDisplayablesPerBatch": 50,  # MapRenderer.dfkr
        "renderThrottleSeconds": 0.15,  # MapRenderer LateUpdate throttle
        "shaderFeatures": [
            "Blending", "CustomFramerate", "DepthAlphaClip", "Distortion",
            "Refraction", "Wind", "ColorAnimation", "TextureOffset",
            "Translation", "Rotation", "Scale", "Dissolve", "Emissive", "Wave"
        ],
        "windTextureNames": [
            "MainWindNoise", "SwingNoise", "WindModel"
        ],
        "waveTextureNames": [
            "WaveGlobalMapMasks", "WaveDynamics", "WaveDisto",
            "WaveDissolveTexID1", "WaveDissolveTexID2",
            "WaveDissolveTexID3", "WaveDissolveTexID4",
            "WaveDissolveDynamics"
        ],
    }
    return config


def extract_character_looks():
    """Extract character breed/look constants for rendering."""
    # From BootLogic.json and dump.cs Looks/Heads data
    config = {
        "breedCount": 19,
        "headsCount": 638,
        "defaultHeadsByBreedSex": 38,
        "lookFormat": "protobuf_encoded_string",
        "note": "Character looks are protobuf-encoded in the Look field of Characters table. "
                "The format is: 0801 (breed) 120C (colors) ... 2A03 (scale). "
                "Decoding requires the Look proto schema from dump.cs.",
    }
    return config


def inventory():
    """Show what's extracted vs what's missing."""
    print("=== EXTRACTION INVENTORY ===\n")

    checks = [
        ("Map JSONs", os.path.join(CONTENT_DIR, "Map", "Json"), ".json"),
        ("Sprite PNGs", os.path.join(CONTENT_DIR, "Map", "Sprites", "1x"), ".png"),
        ("Composed Atlases", os.path.join(CONTENT_DIR, "Map", "Composed"), ".png"),
        ("Composed Render", os.path.join(CONTENT_DIR, "Map", "Composed"), "_render.json"),
    ]

    for name, path, ext in checks:
        if os.path.isdir(path):
            count = len([f for f in os.listdir(path) if f.endswith(ext)])
            print(f"  {name}: {count} files")
        else:
            print(f"  {name}: MISSING ({path})")

    # Check runtime config files
    for name in ["CellGrid.json", "RenderConfig.json"]:
        path = os.path.join(CONTENT_DIR, name)
        if os.path.exists(path):
            size = os.path.getsize(path)
            print(f"  {name}: {size} bytes")
        else:
            print(f"  {name}: NOT EXTRACTED")

    # Check Resources (in Unity project)
    res_dir = os.path.join(REPO_ROOT, "UnityProject", "Assets", "Resources")
    for name in ["ClientData.json", "RuntimeData.json", "ScreenBehavior.json",
                  "BootLogic.json", "AudioData.json", "ThemeColors.json",
                  "DofusUISettings.json", "ServerNames.json"]:
        path = os.path.join(res_dir, name)
        exists = "OK" if os.path.exists(path) else "MISSING"
        print(f"  Resources/{name}: {exists}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--inventory", action="store_true")
    mode.add_argument("--baseline", action="store_true")
    mode.add_argument("--ledger-input", action="store_true")
    mode.add_argument("--renderer-contract", action="store_true")
    mode.add_argument("--registration-observations", metavar="RUN_JSON")
    mode.add_argument("--map", type=int, dest="map_id")
    parser.add_argument("--output", help="Generated contract, observations, baseline or ledger request JSON output")
    parser.add_argument("--registration-run", metavar="RUN_JSON", help="Attach captured native function Evidence to --ledger-input")
    args = parser.parse_args()
    if args.registration_run and not args.ledger_input:
        parser.error("--registration-run requires --ledger-input")
    if args.output and not (args.baseline or args.ledger_input or args.renderer_contract or args.registration_observations):
        parser.error("--output requires a contract, observations, baseline or ledger mode")
    if args.inventory:
        inventory()
        return
    if args.registration_observations:
        observations = extract_registration_observations(args.registration_observations)
        output_path = args.output or os.path.join(REPO_ROOT, "Generated", "output", "RegistrationObservations.json")
        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
        with open(output_path, "w", encoding="utf-8") as output:
            json.dump(observations, output, indent=2, sort_keys=True)
            output.write("\n")
        print(f"Registration observations: {output_path}")
        print(f"  {observations['counts']['assembly']} instructions, {observations['counts']['compositorGenericReads']} compositor generic reads")
        return
    if args.renderer_contract:
        contract = extract_renderer_boot_contract()
        output_path = args.output or os.path.join(REPO_ROOT, "Generated", "output", "RendererBootContract.json")
        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
        with open(output_path, "w", encoding="utf-8") as output:
            json.dump(contract, output, indent=2, sort_keys=True)
            output.write("\n")
        print(f"Renderer contract: {output_path}")
        print(f"  {len(contract['parameters'])} parameters, native address {contract['method']['address']}")
        return
    if args.baseline or args.ledger_input:
        baseline = build_runtime_baseline()
        result = baseline
        filename = "runtime_baseline.json"
        if args.ledger_input:
            with open(os.path.join(REPO_ROOT, "Generated", "world_managed_members_rea.json"), encoding="utf-8") as source:
                managed_bundle = json.load(source)
            with open(os.path.join(REPO_ROOT, "Generated", "gameassembly_exports_rea.json"), encoding="utf-8") as source:
                native_bundle = json.load(source)
            native_function_bundle = None
            if args.registration_run:
                observations = extract_registration_observations(args.registration_run)
                with open(observations["source"]["bundle"]["path"], encoding="utf-8-sig") as source:
                    native_function_bundle = json.load(source)
            result = build_reconstruction_ledger_input(baseline, managed_bundle, native_bundle, native_function_bundle)
            filename = "reconstruction_ledger_input.json"
        output_path = args.output or os.path.join(REPO_ROOT, "Generated", "output", filename)
        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
        with open(output_path, "w", encoding="utf-8") as output:
            json.dump(result, output, indent=2, sort_keys=True)
            output.write("\n")
        print(f"{'Ledger input' if args.ledger_input else 'Baseline'}: {output_path}")
        print(f"  {len(baseline['artifacts'])} artifacts hashed, REA binary identity matches")
        print(f"  {len(baseline['cellGrid']['values'])} grid fields extracted with provenance")
        return

    start = time.time()
    print("=== Universal Runtime Data Extractor ===\n")

    # 1. Extract constants (always — fast, from dump.cs analysis)
    print("[1] Cell grid constants...")
    grid = extract_cell_grid_constants()
    grid_path = os.path.join(CONTENT_DIR, "CellGrid.json")
    with open(grid_path, 'w') as f:
        json.dump(grid, f, indent=2)
    print(f"  -> {grid_path} ({len(grid)} constants)")

    print("[2] Render config...")
    render_cfg = extract_render_config()
    cfg_path = os.path.join(CONTENT_DIR, "RenderConfig.json")
    with open(cfg_path, 'w') as f:
        json.dump(render_cfg, f, indent=2)
    print(f"  -> {cfg_path}")

    print("[3] Character look config...")
    looks = extract_character_looks()
    looks_path = os.path.join(CONTENT_DIR, "CharacterLooks.json")
    with open(looks_path, 'w') as f:
        json.dump(looks, f, indent=2)
    print(f"  -> {looks_path}")

    # 2. Extract map data if requested
    if args.map_id is not None:
        map_id = args.map_id
        print(f"\n[4] Extracting map {map_id}...")

        # Import the specific tools
        sys.path.insert(0, SCRIPT_DIR)
        from extract_map_data import extract_map
        from extract_map_sprites import extract_sprites, get_gfx_ids_for_map
        from compose_map_atlas import process_map

        # Step 1: Extract map JSON
        map_json_dir = os.path.join(CONTENT_DIR, "Map", "Json")
        os.makedirs(map_json_dir, exist_ok=True)
        json_path = os.path.join(map_json_dir, f"{map_id}.json")
        if not os.path.exists(json_path):
            # Need to find the world bundle
            world_id = (map_id >> 18) & 0xFFF
            bundle_path = os.path.join(CONTENT_DIR, "Map", "Data",
                                        f"mapdata_assets_world_{world_id}.bundle")
            if os.path.exists(bundle_path):
                maps = extract_map(bundle_path, map_id)
                for m in maps:
                    with open(json_path, 'w') as f:
                        json.dump(m, f, separators=(',', ':'))
                    print(f"  Map JSON: {map_id}.json")

        # Step 2: Extract sprites
        gfx_ids = get_gfx_ids_for_map(map_id)
        if gfx_ids:
            sprite_dir = os.path.join(CONTENT_DIR, "Map", "Sprites", "1x")
            count = extract_sprites(gfx_ids, sprite_dir)
            print(f"  Sprites: {count} available")

        # Step 3: Compose atlas
        process_map(map_id)
        print(f"  Atlas composed")

    elapsed = time.time() - start
    print(f"\nDone in {elapsed:.1f}s")
    print("\nRun with --inventory to see extraction status")
    print("Run with --map <id> to extract a specific map fully")


if __name__ == '__main__':
    main()
