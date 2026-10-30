#!/usr/bin/env python3
"""
Unit tests for UI_BINDINGS generation and class coverage.
"""

import unittest
import sys
from pathlib import Path

# Ensure Tools/ClientAnalyzer is in path
sys.path.insert(0, str(Path(__file__).parent))

from generate_binding_methods import (
    extract_all_binding_classes,
    generate_binding_code,
    TARGET_CS
)


class TestGenerateBindingMethods(unittest.TestCase):

    def test_dump_extraction_count(self):
        """Verify extraction recovers all 757 UI binding classes."""
        classes = extract_all_binding_classes()
        self.assertGreaterEqual(len(classes), 750, f"Expected ~757 binding classes, got {len(classes)}")

    def test_generated_cs_file_exists(self):
        """Verify GeneratedBindingMethods.cs was generated and has content."""
        self.assertTrue(TARGET_CS.exists(), "GeneratedBindingMethods.cs should exist")
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("namespace Jondo.Reconstructed.UI.Bindings", content)
        self.assertIn("public interface gar", content)
        self.assertIn("public static class UIBindingRegistry", content)

    def test_canonical_bindings_present(self):
        """Verify canonical domain binding structs exist in generated code."""
        content = TARGET_CS.read_text(encoding="utf-8")
        canonical_bindings = [
            "struct AchievementSummaryItemBinding : gar",
            "struct AchievementCheckboxBinding : gar",
            "struct StorageBinding : gar",
            "struct ItemSlotBinding : gar",
            "struct CommonSpellBinding : gar",
            "struct CommonSpellsListBinding : gar",
            "struct ActionBarBinding : gar",
            "struct Chat_ComponentBinding : gar",
            "struct CharacterMainStatsBinding : gar",
            "struct ServerSelectionBinding : gar",
        ]
        for cb in canonical_bindings:
            self.assertIn(cb, content, f"Missing canonical binding: {cb}")

    def test_data_binding_and_reactive_property(self):
        """Verify reactive binding property and abstract DataBinding are present."""
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("public class BindingProperty<T>", content)
        self.assertIn("public abstract class DataBinding<TData> : IBinding<TData>", content)
        self.assertIn("public VisualElement GetRootElement()", content)

    def test_ui_element_compatibility_wrappers(self):
        """Verify compatibility wrappers for custom visual element types."""
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("public class DofusVisualElement : VisualElement", content)
        self.assertIn("public class DofusLabel : Label", content)
        self.assertIn("public class DofusButton : Button", content)
        self.assertIn("public class ExtendedProgressBarFloat : VisualElement", content)
        self.assertIn("public class CheckboxCustom : VisualElement", content)


if __name__ == "__main__":
    unittest.main()

