import contextlib
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import transcribe_methods


class RecoveredTranscriptionTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.export = self.root / "export.c"
        self.export.write_bytes(b"original export")
        self.binary = self.root / "binary.dll"
        self.binary.write_bytes(b"original binary")
        self.body = "void recovered()\n{\n return;\n}\n"
        binary_hash = hashlib.sha256(self.binary.read_bytes()).hexdigest()
        self.response = self.root / "response.json"
        self.response.write_text(json.dumps({"structuredContent": {
            "operation": "procedure_pseudo_code", "evidence_id": "fixture", "normalized_result": self.body,
            "parameters": {"procedure": "0x1000"}, "subject": {"digest": {"sha256": binary_hash}},
        }}), encoding="utf-8")
        self.coverage = self.root / "coverage.json"
        self.coverage.write_text(json.dumps({
            "source": {"sha256": hashlib.sha256(self.export.read_bytes()).hexdigest()},
            "metadata": {"binary": {"path": str(self.binary), "sha256": binary_hash}},
            "recovery": {"recoveredBodies": [{"address": "0x1000", "responsePath": str(self.response),
                         "evidenceId": "fixture", "codeSha256": hashlib.sha256(self.body.encode("utf-8")).hexdigest()}]},
        }), encoding="utf-8")

    def test_recovered_body_is_selected_by_address_and_retains_provenance(self):
        with patch.object(transcribe_methods, "DECOMPILED_C", str(self.export)):
            methods = transcribe_methods.load_recovered_methods(str(self.coverage))
        self.assertEqual(self.body, methods[0x1000]["body"])
        self.assertEqual("fixture", methods[0x1000]["source"]["evidenceId"])
        self.assertFalse(methods[0x1000]["source"]["semanticTranslationVerified"])

    def test_changed_export_is_rejected(self):
        self.export.write_bytes(b"another export")
        with patch.object(transcribe_methods, "DECOMPILED_C", str(self.export)):
            with self.assertRaisesRegex(ValueError, "current decompiled export"):
                transcribe_methods.load_recovered_methods(str(self.coverage))

    def test_virtual_address_is_not_confused_with_rva(self):
        dump = self.root / "dump.cs"
        dump.write_text("// Namespace: Core.Engine.Compositor\npublic class GameCompositor\n{\n"
                        " // RVA: 0x1E36360 Offset: 0x1E35560 VA: 0x181E36360\n"
                        " protected void bkqa() { }\n}\n", encoding="utf-8")
        with patch.object(transcribe_methods, "DUMP_CS", str(dump)):
            methods, _ = transcribe_methods.find_type_methods("GameCompositor")
        self.assertEqual(0x181E36360, methods[0]["va"])

    def test_changed_pseudocode_is_rejected(self):
        response = json.loads(self.response.read_text(encoding="utf-8"))
        response["structuredContent"]["normalized_result"] = "void altered() {}"
        self.response.write_text(json.dumps(response), encoding="utf-8")
        with patch.object(transcribe_methods, "DECOMPILED_C", str(self.export)):
            with self.assertRaisesRegex(ValueError, "audited pseudocode"):
                transcribe_methods.load_recovered_methods(str(self.coverage))

    def test_transcription_uses_recovered_body_before_legacy_lookup(self):
        method = {"name": "bkqa", "signature": "protected void bkqa()", "va": 0x1000, "line": 1}
        with patch.object(transcribe_methods, "DECOMPILED_C", str(self.export)), \
                patch.object(transcribe_methods, "RECOVERY_COVERAGE", str(self.coverage)), \
                patch.object(transcribe_methods, "TRANSCRIBED_DIR", str(self.root / "output")), \
                patch.object(transcribe_methods, "load_decompiled_index", return_value={}), \
                patch.object(transcribe_methods, "load_obfuscated_strings", return_value={}), \
                patch.object(transcribe_methods, "find_type_methods", return_value=([method], "Example.Type")), \
                patch.object(transcribe_methods, "find_c_function", side_effect=AssertionError("Legacy lookup must not run")), \
                contextlib.redirect_stdout(io.StringIO()):
            results = transcribe_methods.transcribe_type("Example.Type", "bkqa")
        self.assertEqual("OK", results[0]["status"])
        self.assertEqual("rea-pseudocode", results[0]["source"]["kind"])


if __name__ == "__main__":
    unittest.main()