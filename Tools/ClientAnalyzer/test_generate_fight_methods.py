import json
import os
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).parent))
import generate_fight_methods


class GenerateFightMethodsTests(unittest.TestCase):
    def test_fight_code_contains_fight_turn_struct(self):
        code = generate_fight_methods.generate_fight_code()
        self.assertIn("public struct FightTurn : IEquatable<FightTurn>", code)
        self.assertIn("public readonly int round;", code)
        self.assertIn("public readonly int order;", code)
        self.assertIn("public readonly string additionalId;", code)
        self.assertIn("public static readonly FightTurn invalid", code)

    def test_fight_code_contains_spell_zone_calculator(self):
        code = generate_fight_methods.generate_fight_code()
        self.assertIn("public enum SpellZoneShape", code)
        self.assertIn("SingleCell = 0,", code)
        self.assertIn("Circle = 1,", code)
        self.assertIn("Cross = 2,", code)
        self.assertIn("Line = 3,", code)
        self.assertIn("public static class SpellZoneCalculator", code)
        self.assertIn("public static List<int> GetAffectedCells(int originCell, SpellZoneShape shape, int radius, int minRadius = 0)", code)

    def test_fight_code_contains_combatant_and_services(self):
        code = generate_fight_methods.generate_fight_code()
        self.assertIn("public class FightCombatant", code)
        self.assertIn("public class FightState", code)
        self.assertIn("public class FightEntitiesService", code)
        self.assertIn("public class FightTurnService", code)

    def test_fight_code_preserves_turn_lifecycle(self):
        code = generate_fight_methods.generate_fight_code()
        self.assertIn("public void StartFight()", code)
        self.assertIn("public void StartTurn(long entityId, int round, int order)", code)
        self.assertIn("public void PassTurn()", code)
        self.assertIn("public void EndFight()", code)
        self.assertIn("public void UpdateCountdown(float deltaTime)", code)

    def test_target_file_is_written_and_non_empty(self):
        target = generate_fight_methods.TARGET_CS
        self.assertTrue(target.exists())
        self.assertGreater(target.stat().st_size, 3000)


if __name__ == "__main__":
    unittest.main()

