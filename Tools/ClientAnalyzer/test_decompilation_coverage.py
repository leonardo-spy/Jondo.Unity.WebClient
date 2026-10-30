import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from analyze_client import audit_decompiled_export, read_recovered_pseudocode


class DecompilationCoverageTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.export = self.root / "export.c"
        self.export.write_text("// === Type$$Method @ 1000 ===\nvoid method()\n{\n return;\n}\n"
                               "// === Type$$Method @ 1010 === FAILED TO DECOMPILE\n"
                               "// === Other$$Missing @ 1020 ===\nvoid missing();\n", encoding="utf-8")

    def test_failed_and_header_only_are_not_counted_as_bodies(self):
        report = audit_decompiled_export(self.export)
        self.assertEqual(3, report["export"]["headers"])
        self.assertEqual(1, report["export"]["braceDelimitedBodies"])
        self.assertEqual(1, report["export"]["explicitFailures"])
        self.assertEqual(1, report["export"]["headerOnly"])
        self.assertEqual(1, report["export"]["repeatedSymbolNames"])
        self.assertEqual("partial", report["coverageStatus"])
        self.assertEqual(hashlib.sha256(self.export.read_bytes()).hexdigest(), report["source"]["sha256"])

    def test_metadata_aliases_no_body_and_missing_are_distinguished(self):
        metadata = {"records": [{"operation": "inspect_il2cpp_metadata", "evidence_id": "fixture", "normalized_result": {
            "artifact": {"sha256": "metadata"}, "binary": {"artifact": {"sha256": "binary"}, "image_base": "0x1000"},
            "images": [{"name": "Core.dll", "type_start": 0, "type_count": 1}],
            "types": [{"namespace": "Example", "name": "Type"}],
            "methods": [{"name": name, "declaring_type": 0} for name in ("First", "Alias", "Failed", "Missing", "Abstract")],
            "method_addresses": [{"state": "code", "address": address} for address in ("0x1000", "0x1000", "0x1010", "0x9999")] +
                                [{"state": "no-body", "address": None}],
        }}]}
        path = self.root / "metadata.json"
        path.write_text(json.dumps(metadata), encoding="utf-8")
        report = audit_decompiled_export(self.export, path)
        counts = report["metadata"]["assemblies"]["Core.dll"]
        self.assertEqual(2, counts["bodyObserved"])
        self.assertEqual(1, counts["failed"])
        self.assertEqual(1, counts["missing"])
        self.assertEqual(1, counts["noBody"])
        self.assertEqual(3, report["metadata"]["uniqueCodeAddresses"])
        self.assertFalse(report["metadata"]["genericInstancesRequested"])
        self.assertNotIn("genericInstances", report["metadata"])
        metadata["records"][0]["normalized_result"]["generic_method_instances"] = [
            {"state": "code", "address": "0x1000", "method": 0, "name": "Present<int>"},
            {"state": "code", "address": "0x8888", "method": 0, "name": "Missing<int>"},
            {"state": "unknown", "address": None, "method": 0, "name": None},
        ]
        path.write_text(json.dumps(metadata), encoding="utf-8")
        report = audit_decompiled_export(self.export, path)
        self.assertTrue(report["metadata"]["genericInstancesRequested"])
        generic = report["metadata"]["genericInstances"]
        self.assertEqual(1, generic["counts"]["bodyObserved"])
        self.assertEqual(1, generic["counts"]["missing"])
        self.assertEqual(1, generic["counts"]["unknown"])
        self.assertEqual(2, generic["uniqueCodeAddresses"])
        symbols = self.root / "script.json"
        symbols.write_text(json.dumps({"ScriptMethod": [
            {"Name": "Present<int>", "Address": 0}, {"Name": "Failed<int>", "Address": 0x10},
            {"Name": "Absent<int>", "Address": 0x77},
        ]}), encoding="utf-8")
        report = audit_decompiled_export(self.export, path, symbols)
        self.assertEqual({"methods": 3, "bodyObserved": 1, "failed": 1, "missing": 1}, report["symbolMap"]["counts"])
        self.assertEqual(3, report["symbolMap"]["uniqueAddresses"])

    def test_malformed_header_is_reported(self):
        self.export.write_text("// === malformed\n", encoding="utf-8")
        report = audit_decompiled_export(self.export)
        self.assertEqual(1, report["export"]["malformedHeaders"])
        self.assertEqual("partial", report["coverageStatus"])

    def recovery_fixture(self):
        code = "void recovered()\n{\n return;\n}\n"
        record = {"operation": "procedure_pseudo_code", "evidence_id": "fixture", "normalized_result": code,
                  "subject": {"digest": {"sha256": "binary"}}, "parameters": {"procedure": "0x1010"}}
        response = self.root / "response.json"
        response.write_text(json.dumps({"structuredContent": record}), encoding="utf-8")
        audit = self.root / "audit.json"
        export_hash = hashlib.sha256(self.export.read_bytes()).hexdigest()
        audit.write_text(json.dumps({"source": {"sha256": export_hash}}), encoding="utf-8")
        bundle = self.root / "evidence-001.json"
        bundle.write_text(json.dumps({"records": [record]}), encoding="utf-8")
        run = {"status": "failed", "binary": {"sha256": "binary"}, "audit": {"path": str(audit)}, "results": [
            {"status": "tool-completed", "address": "0x1010", "symbol": "Type$$Method", "evidenceId": "fixture", "response": "response.json"},
            {"status": "failed", "address": "0x1020", "response": "failure.json"},
        ]}
        path = self.root / "run.json"
        path.write_text(json.dumps(run), encoding="utf-8")
        return path, export_hash, record

    def test_partial_run_preserves_only_valid_recovered_bodies(self):
        path, export_hash, _ = self.recovery_fixture()
        recovered = read_recovered_pseudocode(path, export_hash, "binary")
        self.assertEqual(1, len(recovered))
        self.assertEqual("0x1010", recovered[0]["address"])

    def test_recovered_response_must_match_retained_code(self):
        path, export_hash, record = self.recovery_fixture()
        record["normalized_result"] = "void altered() {}"
        (self.root / "response.json").write_text(json.dumps({"structuredContent": record}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "retained Evidence differ"):
            read_recovered_pseudocode(path, export_hash, "binary")

    def test_recovery_rejects_mixed_binaries(self):
        path, export_hash, _ = self.recovery_fixture()
        with self.assertRaisesRegex(ValueError, "differs from the audited binary"):
            read_recovered_pseudocode(path, export_hash, "other")


if __name__ == "__main__":
    unittest.main()