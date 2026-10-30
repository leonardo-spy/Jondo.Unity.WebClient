#!/usr/bin/env python3
"""
Unit tests for resolve_service_registrations.py.
"""

import json
import os
import unittest

from resolve_service_registrations import (
    load_recovered_bkqa_code,
    load_script_symbols,
    parse_service_catalog,
)


class ResolveServiceRegistrationsTests(unittest.TestCase):
    def test_recovered_bkqa_loads_valid_evidence(self):
        recovery_path, evidence_id, code = load_recovered_bkqa_code()
        self.assertTrue(os.path.exists(recovery_path))
        self.assertEqual(evidence_id, "ev_2cab1b810a461412627f36c71eb4fad30457d1670244c940d8bf361a92de01df")
        self.assertGreater(len(code), 50000)
        self.assertIn("FUN_181e36360", code)

    def test_script_symbols_cover_registration_helpers(self):
        script_path, metadata, metadata_method, method = load_script_symbols()
        self.assertTrue(os.path.exists(script_path))
        self.assertGreater(len(metadata), 10000)
        self.assertGreater(len(metadata_method), 20000)
        self.assertGreater(len(method), 50000)

        # Confirm registration helper methods are present in metadata_method
        has_bkpt = any("Compositor.bkpt" in name for name in metadata_method.values())
        has_bkpw = any("Compositor.bkpw" in name for name in metadata_method.values())
        self.assertTrue(has_bkpt, "bkpt helper missing from symbols")
        self.assertTrue(has_bkpw, "bkpw helper missing from symbols")

    def test_catalog_reconstructs_239_registrations(self):
        out_path, result = parse_service_catalog()
        self.assertTrue(os.path.exists(out_path))
        self.assertEqual(result["schemaVersion"], 1)
        self.assertEqual(result["totalRegistrations"], 239)
        self.assertEqual(result["counts"]["total"], 239)
        self.assertEqual(result["counts"]["instantiated"], 236)
        self.assertEqual(result["counts"]["field"], 3)
        self.assertEqual(result["counts"]["expression"], 0)

        services = result["services"]
        self.assertEqual(len(services), 239)

        # Check the 3 fields
        field_entries = [s for s in services if s["sourceType"] == "field"]
        self.assertEqual(len(field_entries), 3)
        field_names = {f["fieldName"] for f in field_entries}
        self.assertEqual(field_names, {"dxob", "dxoc", "dxod"})

        # Check MapDisplayService is registered
        map_service = next((s for s in services if s.get("concreteClass") == "Core.Services.Rendering.MapDisplayService.MapDisplayService"), None)
        self.assertIsNotNone(map_service)
        self.assertIn("ezm", map_service["genericTypes"])

        # Check DisplayableService is registered
        disp_service = next((s for s in services if s.get("concreteClass") == "Core.Rendering.Displayables.DisplayableService"), None)
        self.assertIsNotNone(disp_service)
        self.assertIn("exm", disp_service["genericTypes"])

        # Check sequential order
        orders = [s["order"] for s in services]
        self.assertEqual(orders, list(range(239)))


if __name__ == "__main__":
    unittest.main()

