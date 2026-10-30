import json
import os
from pathlib import Path
import unittest

import extract_character_atlas


class CharacterAtlasExtractionTests(unittest.TestCase):
    def test_breeds_catalog_has_19_classes(self):
        catalog = extract_character_atlas.load_breeds_catalog()
        self.assertEqual(19, len(catalog))
        self.assertIn(1, catalog) # Feca
        self.assertIn(8, catalog) # Iop
        self.assertIn(9, catalog) # Cra

    def test_breed_looks_format(self):
        catalog = extract_character_atlas.load_breeds_catalog()
        feca = catalog[1]
        self.assertEqual("{1|10||53}", feca["maleLook"])
        self.assertEqual("{1|11||52}", feca["femaleLook"])
        self.assertEqual(1, feca["bonesId"])
        self.assertEqual(10, feca["maleSkinId"])
        self.assertEqual(11, feca["femaleSkinId"])

    def test_extract_skin_texture_and_metadata(self):
        skin_id = 10
        metadata = extract_character_atlas.extract_skin_info(skin_id)
        self.assertIsNotNone(metadata)
        self.assertEqual(10, metadata["skinId"])
        self.assertGreater(len(metadata["parts"]), 0)
        self.assertEqual((1024, 2048), tuple(metadata["textureSize"]))


if __name__ == "__main__":
    unittest.main()

