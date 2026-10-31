#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
test_scaffold_controller.py — Testes Unitários do Scaffolder de Controllers
"""

import unittest
from scaffold_controller import (
    find_class_in_dump,
    to_pascal_case,
    generate_controller_csharp
)

SAMPLE_DUMP_CLASS = """
// Namespace: Core.UILogic.Fight
public class EndTurnWidgetUI : gag<EndTurnWidgetBinding> // TypeDefIndex: 4318
{
	// Fields
	private uint m_turnCount; // 0x238
	private uint m_turnDuration; // 0x23C
	private bool m_canSkipTurn; // 0x26C

	// Methods
	private void OnReadyOrSkipClicked() { }
	private void OnPointCellClicked(ClickEvent evt) { }
	private void OnLeaveClicked(ClickEvent evt) { }
	public void SetTurn(uint count) { }
	public void UpdateTimer(float remaining) { }
}
"""

SAMPLE_BINDING_FIELDS = [
    ("MagneticDragWindow", "VisualElement", "EndTurnWidget"),
    ("VisualElement", "VisualElement", "btn_EndTurn"),
    ("DofusLabel", "Label", "lbl_seconds"),
    ("Label", "Label", "lbl_turnCount"),
    ("DofusToggleButtonCustom", "VisualElement", "tg_permanentPing"),
    ("DofusButtonCustom", "VisualElement", "btn_leave"),
]


class TestScaffoldController(unittest.TestCase):

    def test_to_pascal_case(self):
        self.assertEqual(to_pascal_case("btn_EndTurn"), "EndTurn")
        self.assertEqual(to_pascal_case("tg_permanentPing"), "PermanentPing")
        self.assertEqual(to_pascal_case("lbl_seconds"), "Seconds")
        self.assertEqual(to_pascal_case("lbl_turnCount"), "TurnCount")

    def test_find_class_in_dump(self):
        info = find_class_in_dump(SAMPLE_DUMP_CLASS, "EndTurnWidgetUI")
        self.assertIsNotNone(info)
        self.assertEqual(info["class_name"], "EndTurnWidgetUI")
        self.assertEqual(info["binding_name"], "EndTurnWidgetBinding")
        self.assertTrue(len(info["methods"]) >= 4)
        method_names = [m["name"] for m in info["methods"]]
        self.assertIn("OnReadyOrSkipClicked", method_names)
        self.assertIn("SetTurn", method_names)

    def test_generate_controller_csharp(self):
        methods = [
            {"name": "SetTurn", "params": "uint count"},
            {"name": "UpdateTimer", "params": "float remaining"},
            {"name": "OnReadyOrSkipClicked", "params": ""}
        ]
        code = generate_controller_csharp(
            "EndTurnWidgetUI",
            "EndTurnWidgetBinding",
            SAMPLE_BINDING_FIELDS,
            methods,
            uxml_name="endTurnWidget.uxml"
        )

        self.assertIn("class EndTurnWidgetController", code)
        self.assertIn("GeneratedEndTurnWidgetBinding _binding", code)
        self.assertIn("public event Action OnEndTurnClicked;", code)
        self.assertIn("public event Action OnLeaveClicked;", code)
        self.assertIn("public event Action<bool> OnPermanentPingToggled;", code)
        self.assertIn("public void SetSecondsText(string text)", code)
        self.assertIn("public void SetTurnCountText(string text)", code)
        self.assertIn("public void SetTurn()", code)
        self.assertIn("public void UpdateTimer()", code)


if __name__ == "__main__":
    unittest.main()
