import json
from pathlib import Path
import tempfile
import unittest

import generate_movement_methods


class GenerateMovementMethodsTests(unittest.TestCase):
    def test_movement_code_contains_official_pathfinder_constants(self):
        code = generate_movement_methods.generate_movement_code()
        self.assertIn("public const int doiv = 10;", code)
        self.assertIn("public const int doiw = 15;", code)
        self.assertIn("public const int doix = 10;", code)
        self.assertIn("public const int doiy = 99999999;", code)
        self.assertIn("public const int doiz = 1000;", code)

    def test_movement_code_contains_transpiled_classes(self):
        code = generate_movement_methods.generate_movement_code()
        self.assertIn("public enum rc", code)
        self.assertIn("public interface ezl", code)
        self.assertIn("public static class oh", code)
        self.assertIn("public class MapPoint : IEquatable<MapPoint>", code)
        self.assertIn("public class fyy", code)
        self.assertIn("public class fzc", code)
        self.assertIn("public static class elf", code)

    def test_movement_code_preserves_method_signatures(self):
        code = generate_movement_methods.generate_movement_code()
        self.assertIn("public static fzc lmu(ezl map, MapPoint start, MapPoint target", code)
        self.assertIn("public static int own(int x, int y)", code)
        self.assertIn("public static Vector2Int owm(int cellId)", code)
        self.assertIn("public static int owi(int a, int b)", code)
        self.assertIn("public void bjfv(int a)", code)
        self.assertIn("public void bjfx(int a)", code)
        self.assertIn("public void bjfz(int a)", code)

    def test_target_file_is_written_and_non_empty(self):
        target = generate_movement_methods.TARGET_CS
        self.assertTrue(target.exists())
        self.assertGreater(target.stat().st_size, 5000)

    def test_movement_code_contains_set_cell_data(self):
        code = generate_movement_methods.generate_movement_code()
        self.assertIn("public void SetCellData(int cellId, bool walkable, bool los, int floor, int speed)", code)
        self.assertIn("public bool HasLineOfSight(int cellId)", code)
        self.assertIn("public int GetCellFloor(int cellId)", code)
        self.assertIn("public int GetCellSpeed(int cellId)", code)


if __name__ == "__main__":
    unittest.main()
