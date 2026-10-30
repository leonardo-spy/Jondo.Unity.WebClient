import contextlib
import io
import json
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch

import translate_boot


class BootTranslationTests(unittest.TestCase):
    def generate_boot(self, services=None, registration=None):
        fixtures = {"services.json": services or {}, "asset_paths.json": {}, "screen_handlers.json": {},
                    "RegistrationObservations.json": registration}
        with tempfile.TemporaryDirectory() as output_dir:
            with patch.object(translate_boot, "OUTPUT_DIR", output_dir), \
                    patch.object(translate_boot, "load_json", side_effect=fixtures.get), \
                    contextlib.redirect_stdout(io.StringIO()):
                translate_boot.main()
            return json.loads((Path(output_dir) / "BootLogic.json").read_text(encoding="utf-8"))

    def test_dependencies_load_before_preinit_and_init(self):
        boot = self.generate_boot()
        phases = [phase["name"] for phase in boot["phases"]]
        self.assertLess(phases.index("RegisterServices"), phases.index("LoadDataDependencies"))
        self.assertLess(phases.index("LoadDataDependencies"), phases.index("PreInitServices"))
        self.assertLess(phases.index("PreInitServices"), phases.index("InitializeServices"))

    def test_service_inventory_is_preserved(self):
        services = {"example": {"fullName": "Example.Service", "serviceInitMethods": [
            {"name": "Initialize", "va": "0x1234"}, "AnotherInit"
        ]}}
        boot = self.generate_boot(services)
        initialization = next(phase for phase in boot["phases"] if phase["name"] == "InitializeServices")
        self.assertEqual(["Initialize", "AnotherInit"], [method["method"] for method in initialization["serviceInits"]])
        self.assertEqual("0x1234", initialization["serviceInits"][0]["va"])

    def test_central_registry_uses_extracted_fields_and_interface_keys(self):
        binding = {"field": "fieldFromDump", "fieldType": "ConcreteFromDump", "registryType": "InterfaceFromNative"}
        boot = self.generate_boot(registration={"existingFieldRegistrations": [binding], "source": {"evidenceId": "fixture"}})
        self.assertEqual([binding], boot["centralRegistrations"])
        self.assertEqual("fixture", boot["registrationSource"]["evidenceId"])

    def test_duplicate_extracted_registry_keys_are_rejected(self):
        binding = {"field": "fieldFromDump", "fieldType": "ConcreteFromDump", "registryType": "InterfaceFromNative"}
        with self.assertRaisesRegex(ValueError, "Duplicate extracted registry key"):
            self.generate_boot(registration={"existingFieldRegistrations": [binding, binding]})

    def test_deploy_publishes_the_generated_file(self):
        with tempfile.TemporaryDirectory() as output_dir, tempfile.TemporaryDirectory() as resource_dir:
            with patch.object(translate_boot, "OUTPUT_DIR", output_dir), \
                    patch.object(translate_boot, "RESOURCE_DIR", resource_dir), \
                    patch.object(translate_boot, "load_json", return_value={}), \
                    contextlib.redirect_stdout(io.StringIO()):
                translate_boot.main(deploy=True)
            self.assertEqual((Path(output_dir) / "BootLogic.json").read_bytes(),
                             (Path(resource_dir) / "BootLogic.json").read_bytes())

    def test_missing_service_inventory_is_rejected(self):
        with patch.object(translate_boot, "load_json", return_value=None), \
                contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaises(FileNotFoundError):
                translate_boot.main()

    def test_probe_loads_bundles_before_initializing_services(self):
        probe = (Path(translate_boot.REPO_ROOT) / "UnityProject" / "Assets" / "WebGLProbe.cs").read_text(encoding="utf-8")
        flow = probe.split("IEnumerator OriginalBootFlow()", 1)[1].split("bool RegisterServices()", 1)[0]
        self.assertIn("if (!RegisterServices()) yield break;", flow)
        self.assertLess(flow.index("RegisterServices()"), flow.index("StartCoroutine(LoadBundles())"))
        self.assertLess(flow.index("StartCoroutine(LoadBundles())"), flow.index("CallServiceInitMethods();"))
        self.assertLess(flow.index("CallServiceInitMethods();"), flow.index('GetField("isRunning"'))

    @unittest.skipUnless((Path(translate_boot.REPO_ROOT) / "Decompiled" / "decompiled_all.c").is_file(),
                         "Original decompiled export not available")
    def test_reviewed_original_compose_suspension_states(self):
        root = Path(translate_boot.REPO_ROOT)
        symbol = "Core.Engine.Compositor.Compositor.<Compose>d__24$$MoveNext"
        index = json.loads((root / "Generated" / "output" / "decompiled_index.json").read_text(encoding="utf-8"))
        entry = index[symbol]
        address_view = json.loads((root / "Generated" / "output" / "boot_addresses_view_rea.json").read_text(encoding="utf-8"))
        self.assertIn("0x" + entry["va"].lower(),
                      [item["address"] for item in address_view["normalized_result"]["items"]])
        lines = []
        depth = 0
        started = False
        with (root / "Decompiled" / "decompiled_all.c").open("rb") as source:
            source.seek(entry["pos"])
            for _ in range(2000):
                raw = source.readline()
                self.assertTrue(raw, "Incomplete original function body")
                line = raw.decode("utf-8")
                lines.append(line)
                opened = line.count("{")
                started = started or opened > 0
                depth += opened - line.count("}")
                if started and depth == 0:
                    break
            else:
                self.fail("Original function exceeds the reviewed extraction boundary")
        body = "".join(lines)
        self.assertIn(symbol, lines[0])
        markers = ("_7_bkqe.methodPtr", "Start<Compositor_<PreInitServices>d__16>__",
                   "Start<Compositor_<InitializeServices>d__17>__")
        states = []
        for marker in markers:
            position = body.rfind(marker)
            self.assertGreaterEqual(position, 0, f"Original call anchor changed: {marker}")
            suspended = re.search(r"__1__state\s*=\s*(\d+);", body[position:])
            self.assertIsNotNone(suspended, f"No suspension state after {marker}")
            states.append(int(suspended.group(1)))
        self.assertEqual([0, 1, 2], states)
        self.assertRegex(body, r"if \(iVar6 == 0\)")
        self.assertIn("goto LAB_181e46ea0;", body)


if __name__ == "__main__":
    unittest.main()