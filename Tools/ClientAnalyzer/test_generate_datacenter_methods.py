import json
import os
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).parent))
import generate_datacenter_methods


class GenerateDataCenterMethodsTests(unittest.TestCase):
    def test_datacenter_code_contains_containers(self):
        code = generate_datacenter_methods.generate_datacenter_code()
        self.assertIn("public interface IDataContainer", code)
        self.assertIn("public class DataRoot<T> : IDataContainer", code)
        self.assertIn("public T Get(int id)", code)
        self.assertIn("public bool TryGet(int id, out T item)", code)

    def test_datacenter_code_contains_data_models(self):
        code = generate_datacenter_methods.generate_datacenter_code()
        self.assertIn("public class ItemData", code)
        self.assertIn("public class SpellData", code)
        self.assertIn("public class MonsterData", code)
        self.assertIn("public class BreedData", code)

    def test_datacenter_code_contains_i18n_service(self):
        code = generate_datacenter_methods.generate_datacenter_code()
        self.assertIn("public static class I18nService", code)
        self.assertIn("public static string GetText(string key, string fallback = null)", code)
        self.assertIn("public static void SetLanguage(string lang)", code)

    def test_datacenter_code_contains_datacenter_module(self):
        code = generate_datacenter_methods.generate_datacenter_code()
        self.assertIn("public static class DataCenterModule", code)
        self.assertIn("public static readonly DataRoot<ItemData> itemsDataRoot", code)
        self.assertIn("public static readonly DataRoot<SpellData> spellsDataRoot", code)
        self.assertIn("public static readonly DataRoot<MonsterData> monstersDataRoot", code)
        self.assertIn("public static readonly DataRoot<BreedData> breedsDataRoot", code)
        self.assertIn("public static void Initialize()", code)

    def test_target_file_is_written_and_non_empty(self):
        target = generate_datacenter_methods.TARGET_CS
        self.assertTrue(target.exists())
        self.assertGreater(target.stat().st_size, 3000)


if __name__ == "__main__":
    unittest.main()

