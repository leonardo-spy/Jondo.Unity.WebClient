#!/usr/bin/env python3
"""
Unit tests for UI_SCREENS generation and class coverage.
"""

import unittest
import sys
from pathlib import Path

# Ensure Tools/ClientAnalyzer is in path
sys.path.insert(0, str(Path(__file__).parent))

from generate_screen_methods import (
    extract_all_screen_classes,
    generate_screen_code,
    TARGET_CS
)


class TestGenerateScreenMethods(unittest.TestCase):

    def test_dump_screen_extraction_count(self):
        """Verify extraction recovers all ~1,159 UI screen classes."""
        classes = extract_all_screen_classes()
        self.assertGreaterEqual(len(classes), 1150, f"Expected ~1,159 screen classes, got {len(classes)}")

    def test_generated_cs_file_exists(self):
        """Verify GeneratedScreenSubsystemMethods.cs was generated and has content."""
        self.assertTrue(TARGET_CS.exists(), "GeneratedScreenSubsystemMethods.cs should exist")
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("namespace Jondo.Reconstructed.UI.Screens", content)
        self.assertIn("public abstract class BaseUiController<TBinding>", content)
        self.assertIn("public static class UIScreenRegistry", content)

    def test_canonical_screens_present(self):
        """Verify canonical screen controllers exist in generated code."""
        content = TARGET_CS.read_text(encoding="utf-8")
        canonical_screens = [
            "class LoginScreenController : BaseUiController<gar>",
            "class ServerSelectionScreenController : BaseUiController<ServerSelectionBinding>",
            "class CharacterSelectionScreenController : BaseUiController<gar>",
            "class CharacterCreationScreenController : BaseUiController<gar>",
            "class PlayerInventoryUiController : BaseUiController<StorageBinding>",
            "class BankUiController : BaseUiController<StorageBinding>",
            "class SpellBaseUiController : BaseUiController<CommonSpellsListBinding>",
            "class UIActionBarController : BaseUiController<ActionBarBinding>",
            "class ChatViewController : BaseUiController<Chat_ComponentBinding>",
            "class NpcDialogController : BaseUiController<gar>",
        ]
        for cs in canonical_screens:
            self.assertIn(cs, content, f"Missing canonical screen: {cs}")

    def test_base_controller_lifecycle(self):
        """Verify BaseUiController implements full UI Toolkit lifecycle."""
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("public virtual void Show()", content)
        self.assertIn("public virtual void Hide()", content)
        self.assertIn("public virtual void Toggle()", content)
        self.assertIn("public virtual void InitializeUI()", content)
        self.assertIn("public virtual void LocalizeTexts()", content)

    def test_registry_indexing(self):
        """Verify UIScreenRegistry contains descriptors and query methods."""
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("public struct ScreenDescriptor", content)
        self.assertIn("public static int RegisteredCount", content)
        self.assertIn("public static object CreateScreen", content)


if __name__ == "__main__":
    unittest.main()

