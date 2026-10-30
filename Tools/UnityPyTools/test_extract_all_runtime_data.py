import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest

from extract_all_runtime_data import DUMP_CS, build_reconstruction_ledger_input, build_runtime_baseline, describe_artifact, extract_cell_grid_constants, extract_registration_observations, extract_renderer_boot_contract, read_dump_constants
from compose_map_atlas import build_render_data


class DumpConstantTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.dump_path = Path(self.directory.name) / "dump.cs"

    def write_dump(self, text):
        self.dump_path.write_text(text, encoding="utf-8")
        return self.dump_path

    def test_numeric_literals_and_source_identity(self):
        text = "public static class sample\n{\n private const float half = -21,5;\n public const uint count = 0x230u;\n public const float rate = 1.5e-1f;\n}\n"
        path = self.write_dump(text)
        constants, digest = read_dump_constants(path, ["sample.half", "sample.count", "sample.rate"])
        self.assertEqual(-21.5, constants["sample.half"]["value"])
        self.assertEqual(560, constants["sample.count"]["value"])
        self.assertEqual(0.15, constants["sample.rate"]["value"])
        self.assertEqual(3, constants["sample.half"]["line"])
        self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), digest)

    def test_missing_field_does_not_fall_back(self):
        path = self.write_dump("public static class sample\n{\n public const int other = 560;\n}\n")
        with self.assertRaisesRegex(ValueError, "Missing constants.*sample.count"):
            read_dump_constants(path, ["sample.count"])

    def test_duplicate_symbol_is_rejected(self):
        path = self.write_dump("public static class sample\n{\n public const int count = 560;\n public const int count = 561;\n}\n")
        with self.assertRaisesRegex(ValueError, "Ambiguous constant sample.count"):
            read_dump_constants(path, ["sample.count"])

    def test_expression_is_not_guessed(self):
        path = self.write_dump("public static class sample\n{\n public const int count = 14 * 40;\n}\n")
        with self.assertRaisesRegex(ValueError, "Unsupported integer sample.count"):
            read_dump_constants(path, ["sample.count"])

    def test_same_field_name_in_other_class_is_ignored(self):
        path = self.write_dump("public static class other\n{\n public const int count = 1;\n}\npublic static class sample\n{\n public const int count = 560;\n}\n")
        constants, _ = read_dump_constants(path, ["sample.count"])
        self.assertEqual(560, constants["sample.count"]["value"])

    def test_artifact_digest(self):
        path = self.write_dump("source bytes\n")
        artifact = describe_artifact(path)
        self.assertEqual(path.stat().st_size, artifact["bytes"])
        self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), artifact["sha256"])

    def test_map_flat_buffers_match_legacy_geometry(self):
        element = {"g": 7, "c": 0x80402010, "t": [1, 0, 0, 1, 10, 20]}
        source = {
            "cells": [{"n": 0}], "backgroundElements": [element, element],
            "backgroundAtlas": {"width": 100, "height": 80,
                                "rects": {"7": {"x": 0, "y": 0, "w": 0.5, "h": 0.5}}},
        }
        render = build_render_data(source, 2 ** 40)
        layer = render["background"]
        self.assertEqual(2, render["schemaVersion"])
        self.assertEqual(2 ** 40, render["mapId"])
        self.assertEqual("quad-approximation", render["geometryMode"])
        self.assertEqual([coordinate for quad in layer["quads"] for vertex in quad["v"] for coordinate in vertex],
                         layer["positions"])
        self.assertEqual([0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7], layer["indices"])
        self.assertEqual([64, 32, 16, 128] * 8, layer["colors"])
        self.assertEqual(len(layer["positions"]), len(layer["uvs"]))

    def test_empty_map_has_no_fabricated_layers(self):
        render = build_render_data({"cells": []}, 1)
        self.assertEqual(0, render["cells"])
        self.assertNotIn("background", render)

    def renderer_bundle(self):
        return {"records": [{
            "operation": "inspect_il2cpp_metadata", "evidence_id": "fixture-evidence",
            "normalized_result": {
                "artifact": {"sha256": "fixture-metadata"},
                "binary": {"artifact": {"sha256": "fixture-binary"}},
                "types": [{"name": "MapRenderer", "namespace": "Other"},
                          {"name": "MapRenderer", "namespace": "Core.Rendering"}],
                "methods": [{"name": "qcu", "declaring_type": 0}, {"name": "qcu", "declaring_type": 1}],
                "method_addresses": [{"state": "code", "address": "0x10", "rva": "0x10"},
                                     {"state": "code", "address": "0x20", "rva": "0x20"}],
                "parameters": [{"name": "ignored", "method": 0}, {"name": "first", "method": 1},
                               {"name": "second", "method": 1}],
                "parameter_types": [{"name": "Other", "type": 0}, {"name": "ezo", "type": 1},
                                    {"name": "ejo", "type": 2}],
            },
        }]}

    def test_renderer_contract_preserves_parameter_order_and_owner(self):
        path = self.write_dump(json.dumps(self.renderer_bundle()))
        contract = extract_renderer_boot_contract(path)
        self.assertEqual(1, contract["owner"]["metadataIndex"])
        self.assertEqual(["ezo", "ejo"], [parameter["typeName"] for parameter in contract["parameters"]])
        self.assertEqual([0, 1], [parameter["position"] for parameter in contract["parameters"]])
        self.assertEqual("0x20", contract["method"]["address"])
        self.assertFalse(contract["runtimeReady"])

    def test_renderer_contract_rejects_unresolved_parameter_type(self):
        bundle = self.renderer_bundle()
        bundle["records"][0]["normalized_result"]["parameter_types"][1]["name"] = None
        path = self.write_dump(json.dumps(bundle))
        with self.assertRaisesRegex(ValueError, "Unresolved MapRenderer.qcu parameter type"):
            extract_renderer_boot_contract(path)

    def test_renderer_contract_rejects_ambiguous_initializer(self):
        bundle = self.renderer_bundle()
        report = bundle["records"][0]["normalized_result"]
        report["methods"].append(dict(report["methods"][1]))
        path = self.write_dump(json.dumps(bundle))
        with self.assertRaisesRegex(ValueError, "exactly one MapRenderer.qcu"):
            extract_renderer_boot_contract(path)

    def test_lfs_pointer_is_not_a_valid_artifact(self):
        path = self.write_dump("version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 100\n")
        with self.assertRaisesRegex(ValueError, "Git LFS pointer"):
            describe_artifact(path)

    def registration_fixture(self):
        root = Path(self.directory.name)
        binary_hash = "fixture-binary"
        record = {
            "operation": "analyze_function", "evidence_id": "fixture-native",
            "subject": {"digest": {"sha256": binary_hash}, "architecture": "x86_64"}, "limitations": [],
            "normalized_result": {
                "procedure": {"address": "0x1100"}, "assembly": ["fixture"], "basic_blocks": [], "callees": [],
                "facet_availability": {"assembly": {"available": True}, "pseudocode": {"available": False}},
                "outgoing_references": [
                    {"source_address": "0x1101", "target_address": "0x1200", "kind": {"data": True, "read": False}},
                    {"source_address": "0x1102", "target_address": "0x1200", "kind": {"data": True, "read": True}},
                    {"source_address": "0x1103", "target_address": "0x1300", "kind": {"call": True}},
                ],
            },
        }
        bundle_path = root / "function-evidence.json"
        bundle_path.write_text(json.dumps({"records": [record]}), encoding="utf-8")
        run = {"status": "captured", "exitCode": 0, "evidenceBundle": str(bundle_path),
               "evidenceBundleSha256": describe_artifact(bundle_path)["sha256"],
               "evidenceId": "fixture-native", "binarySha256": binary_hash, "address": "0x1100"}
        script = {
            "ScriptMetadataMethod": [{"Address": 0x200, "Name": "Method$Core.Engine.Compositor.Compositor.bkpt<Service>()", "MethodAddress": 0},
                                     {"Address": 0x200, "Name": "Method$Alias<Other>()", "MethodAddress": 0}],
            "ScriptMetadata": [],
            "ScriptMethod": [{"Address": 0x300, "Name": "FirstAlias"}, {"Address": 0x300, "Name": "SecondAlias"}],
        }
        view = {"evidence_id": "fixture-view", "normalized_result": {
            "binary_source": {"artifact": {"sha256": binary_hash}, "image_base": "0x1000"},
            "items": [{"address": "0x1100"}],
        }}
        paths = []
        for name, value in (("run.json", run), ("script.json", script), ("view.json", view)):
            path = root / name
            path.write_text(json.dumps(value), encoding="utf-8")
            paths.append(path)
        dump_path = root / "compositor.cs"
        dump_path.write_text("public class GameCompositor : Compositor\n{\n private Service bus; // 0x50\n}\n", encoding="utf-8")
        paths.append(dump_path)
        return paths

    def test_registration_reads_exclude_metadata_preparation_and_preserve_aliases(self):
        paths = self.registration_fixture()
        observations = extract_registration_observations(*paths)
        self.assertEqual(2, observations["counts"]["metadataReferences"])
        self.assertEqual(1, observations["counts"]["metadataReads"])
        self.assertEqual(2, len(observations["metadataReads"][0]["symbols"]))
        target = observations["calls"][0]["target"]
        self.assertEqual(["FirstAlias", "SecondAlias"], observations["callTargets"][target]["symbols"])
        self.assertEqual({"bkpt": 1}, observations["genericFamilyCounts"])
        self.assertEqual("Service", observations["compositorGenericReads"][0]["genericArgumentsText"])
        self.assertFalse(observations["runtimeReady"])

    def test_registration_rejects_modified_bundle(self):
        paths = self.registration_fixture()
        bundle_path = Path(self.directory.name) / "function-evidence.json"
        bundle_path.write_text("{}", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "bundle digest changed"):
            extract_registration_observations(*paths)

    def test_registration_helper_match_requires_exact_instructions_and_call_reference(self):
        paths = self.registration_fixture()
        bundle_path = Path(self.directory.name) / "function-evidence.json"
        bundle = json.loads(bundle_path.read_text(encoding="utf-8"))
        result = bundle["records"][0]["normalized_result"]
        result["assembly"] = ["0x1102: MOV R8, qword ptr [0x1200]", "0x1103: MOV RDX, RBX",
                              "0x1104: MOV RCX, RSI", "0x1105: CALL 0x1300"]
        result["outgoing_references"][-1]["source_address"] = "0x1105"
        script = json.loads(paths[1].read_text(encoding="utf-8"))
        script["ScriptMethod"].append({"Address": 0x300, "Name": "Core.Engine.Compositor.Compositor$$bkpt<object>"})
        paths[1].write_text(json.dumps(script), encoding="utf-8")
        def capture():
            bundle_path.write_text(json.dumps(bundle), encoding="utf-8")
            run = json.loads(paths[0].read_text(encoding="utf-8"))
            run["evidenceBundleSha256"] = describe_artifact(bundle_path)["sha256"]
            paths[0].write_text(json.dumps(run), encoding="utf-8")
            return extract_registration_observations(*paths)
        observations = capture()
        self.assertEqual(1, observations["counts"]["helperCallMatches"])
        self.assertEqual("RBX", observations["helperCallMatches"][0]["instanceOperand"])
        result["outgoing_references"][-1]["kind"]["call"] = False
        self.assertEqual(0, capture()["counts"]["helperCallMatches"])
        result["outgoing_references"][-1]["kind"]["call"] = True
        result["assembly"][1] = "0x1103: JMP 0x1300"
        self.assertEqual(0, capture()["counts"]["helperCallMatches"])
        result["assembly"][1:3] = ["0x1103: MOV RCX, RSI", "0x1104: MOV RDX, qword ptr [RSI + 0x50]"]
        observations = capture()
        self.assertEqual(1, observations["counts"]["helperCallMatches"])
        self.assertEqual("qword ptr [RSI + 0x50]", observations["helperCallMatches"][0]["instanceOperand"])
        self.assertEqual("bus", observations["existingFieldRegistrations"][0]["field"])
        self.assertEqual("Service", observations["existingFieldRegistrations"][0]["registryType"])
        result["assembly"][2] = "0x1104: MOV RDX, qword ptr [RSI + 0x90]"
        observations = capture()
        self.assertEqual([], observations["existingFieldRegistrations"])
        self.assertEqual(1, observations["counts"]["unresolvedFieldOperands"])
        result["assembly"][2] = "0x1104: MOV RDX, qword ptr [RSI + 0x50]"
        result["assembly"][3:] = ["0x1105: MOV RBX, qword ptr [RSP + 0x30]",
                      "0x1106: MOV RSI, qword ptr [RSP + 0x38]",
                      "0x1107: ADD RSP, 0x20", "0x1108: POP RDI", "0x1109: JMP 0x1300"]
        result["outgoing_references"][-1]["source_address"] = "0x1109"
        observations = capture()
        self.assertEqual(1, observations["counts"]["helperCallMatches"])
        self.assertEqual("tail-jump", observations["helperCallMatches"][0]["completion"])
        result["assembly"][5] = "0x1107: XOR RDX, RDX"
        self.assertEqual(0, capture()["counts"]["helperCallMatches"])

    def test_registration_rejects_failed_run(self):
        paths = self.registration_fixture()
        run = json.loads(paths[0].read_text(encoding="utf-8"))
        run["status"] = "failed"
        paths[0].write_text(json.dumps(run), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "has not captured"):
            extract_registration_observations(*paths)

    def test_registration_rejects_mixed_binary_identities(self):
        paths = self.registration_fixture()
        view = json.loads(paths[2].read_text(encoding="utf-8"))
        view["normalized_result"]["binary_source"]["artifact"]["sha256"] = "another-binary"
        paths[2].write_text(json.dumps(view), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "different binaries"):
            extract_registration_observations(*paths)

    def test_empty_artifact_is_rejected(self):
        path = self.write_dump("")
        with self.assertRaisesRegex(ValueError, "Empty artifact"):
            describe_artifact(path)

    def test_baseline_rejects_mismatched_binary(self):
        root = Path(self.directory.name)
        metadata = root / "Dofus_Data" / "il2cpp_data" / "Metadata"
        metadata.mkdir(parents=True)
        (metadata / "global-metadata.dat").write_bytes(b"metadata")
        (root / "GameAssembly.dll").write_bytes(b"binary")
        self.write_dump("dump")
        (root / "script.json").write_text("{}", encoding="utf-8")
        (root / "decompiled_all.c").write_text("export", encoding="utf-8")
        evidence_path = root / "overview.json"
        evidence_path.write_text(json.dumps({"subject": {"digest": {"sha256": "0" * 64}}}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "digest does not match"):
            build_runtime_baseline(root, self.dump_path, evidence_path)

    def test_ledger_keeps_missing_proofs_open(self):
        digest = "0" * 64
        identity = {
            "evidence_id": "ev_" + "1" * 64,
            "operation": "inspect_native_exports",
            "subject": {"digest": {"sha256": digest}, "format": "pe", "architecture": None},
            "provider": {"id": "fixture", "name": "fixture", "version": "1"},
        }
        bundle = {"artifacts": [], "providers": [], "records": [],
                  "environments": [], "scenarios": [], "captures": [], "unknowns": []}
        bundle["artifacts"] = [{"architecture": "x86", "digest": {"sha256": "f" * 64}, "format": "pe"}]
        baseline = {"artifacts": {"gameAssembly": {"sha256": digest}}}
        native_bundle = {**bundle, "records": [identity], "artifacts": [identity["subject"]]}
        request = build_reconstruction_ledger_input(baseline, bundle, native_bundle)
        self.assertEqual([], bundle["records"])
        self.assertEqual(digest, request["evidence_bundle"]["artifacts"][0]["digest"]["sha256"])
        self.assertEqual(8, len(request["reviewed_obligations"]))
        for binding in request["manifest"]["bindings"]:
            self.assertEqual([], binding["original_cases"])
            self.assertEqual([], binding["fixtures"])
            self.assertIsNone(binding["verifier"])
        self.assertTrue(all(item["required_next_evidence"] for item in request["reviewed_obligations"]))
        function_id = "ev_" + "2" * 64
        function_bundle = {**bundle, "records": [{"evidence_id": function_id, "operation": "analyze_function",
                                                "subject": {"digest": {"sha256": digest}}}]}
        request = build_reconstruction_ledger_input(baseline, bundle, native_bundle, function_bundle)
        boot = next(item for item in request["reviewed_obligations"] if item["obligation_id"] == "boot.lifecycle")
        self.assertIn(function_id, boot["review_evidence_ids"])
        self.assertTrue(all(function_id not in item["review_evidence_ids"] for item in request["reviewed_obligations"]
                            if item["obligation_id"] != "boot.lifecycle"))
        function_bundle["records"][0]["subject"]["digest"]["sha256"] = "f" * 64
        with self.assertRaisesRegex(ValueError, "does not match the baseline"):
            build_reconstruction_ledger_input(baseline, bundle, native_bundle, function_bundle)

    @unittest.skipUnless(os.path.isfile(DUMP_CS), "Original dump not available")
    def test_original_grid_and_provenance(self):
        provenance = {}
        grid = extract_cell_grid_constants(provenance=provenance)
        self.assertEqual(18, len(grid))
        self.assertEqual(560, grid["cellCount"])
        self.assertEqual(grid["mapFullWidth"], grid["mapMaxX"] - grid["mapMinX"])
        self.assertEqual(grid["mapFullHeight"], grid["mapMaxY"] - grid["mapMinY"])
        self.assertEqual("oh.ddji", provenance["fields"]["cellCount"]["symbol"])
        self.assertGreater(provenance["fields"]["cellCount"]["line"], 0)
        self.assertEqual(64, len(provenance["source"]["sha256"]))


if __name__ == "__main__":
    unittest.main()