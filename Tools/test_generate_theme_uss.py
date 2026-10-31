#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
test_generate_theme_uss.py
Testes unitários para o gerador de estilos USS (generate_theme_uss.py).
"""

import unittest
from generate_theme_uss import format_px, extract_uss_rules, generate_uss_content

class TestGenerateThemeUss(unittest.TestCase):

    def test_format_px(self):
        self.assertEqual(format_px(10), "10px")
        self.assertEqual(format_px(10.5), "10.5px")
        self.assertIsNone(format_px(None))
        self.assertEqual(format_px("auto"), "auto")

    def test_extract_rules_basic(self):
        node = {
            "name": "btn_play",
            "layout": {
                "display": "Flex",
                "position": "Absolute",
                "width": 280,
                "height": 132.2,
                "flexDirection": "Row",
                "justifyContent": "Center",
                "marginBottom": 50
            },
            "borders": {
                "widthLeft": 2, "widthRight": 2, "widthTop": 2, "widthBottom": 2,
                "radiusTopLeft": 8, "radiusTopRight": 8, "radiusBottomRight": 8, "radiusBottomLeft": 8,
                "colorLeft": "rgba(255,255,255,1.00)"
            },
            "background": {
                "color": "rgba(0,0,0,0.50)",
                "sliceLeft": 10, "sliceRight": 10, "sliceTop": 10, "sliceBottom": 10
            },
            "typography": {
                "fontSize": 36,
                "color": "rgba(255,255,255,1.00)"
            },
            "children": []
        }
        
        rules = {}
        extract_uss_rules(node, rules)
        
        self.assertIn("btn_play", rules)
        r = rules["btn_play"]
        self.assertEqual(r["position"], "absolute")
        self.assertEqual(r["width"], "280px")
        self.assertEqual(r["height"], "132.2px")
        self.assertEqual(r["flex-direction"], "row")
        self.assertEqual(r["justify-content"], "center")
        self.assertEqual(r["margin-bottom"], "50px")
        self.assertEqual(r["border-width"], "2px")
        self.assertEqual(r["border-radius"], "8px")
        self.assertEqual(r["background-color"], "rgba(0,0,0,0.50)")
        self.assertEqual(r["-unity-slice-left"], "10")
        self.assertEqual(r["font-size"], "36px")

    def test_ignore_unnamed_and_empty(self):
        node = {
            "name": "(unnamed)",
            "layout": {"display": "Flex"},
            "children": [
                {
                    "name": "valid_element",
                    "layout": {"display": "None"},
                    "children": []
                }
            ]
        }
        rules = {}
        extract_uss_rules(node, rules)
        self.assertNotIn("(unnamed)", rules)
        self.assertIn("valid_element", rules)
        self.assertEqual(rules["valid_element"]["display"], "none")

    def test_generate_uss_content_syntax(self):
        rules = {
            "header": {
                "height": "204px",
                "flex-direction": "row"
            }
        }
        uss = generate_uss_content(rules)
        self.assertIn("#header {", uss)
        self.assertIn("height: 204px;", uss)
        self.assertIn("flex-direction: row;", uss)

if __name__ == "__main__":
    unittest.main()

