#!/usr/bin/env python3
"""
Unit tests for PROTOCOL_GAME generation and class coverage.
"""

import unittest
import sys
from pathlib import Path

# Ensure Tools/ClientAnalyzer is in path
sys.path.insert(0, str(Path(__file__).parent))

from generate_protocol_methods import (
    extract_all_protocol_classes,
    generate_protocol_code,
    TARGET_CS
)


class TestGenerateProtocolMethods(unittest.TestCase):

    def test_dump_protocol_extraction_count(self):
        """Verify extraction recovers all ~905 protocol classes."""
        classes = extract_all_protocol_classes()
        self.assertGreaterEqual(len(classes), 900, f"Expected ~905 protocol classes, got {len(classes)}")

    def test_generated_cs_file_exists(self):
        """Verify GeneratedProtocolMethods.cs was generated and has content."""
        self.assertTrue(TARGET_CS.exists(), "GeneratedProtocolMethods.cs should exist")
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("namespace Jondo.Reconstructed.Protocol", content)
        self.assertIn("public interface IProtocolMessage", content)
        self.assertIn("public static class ProtocolMessageDispatcher", content)
        self.assertIn("public static class ProtocolRegistry", content)

    def test_canonical_messages_present(self):
        """Verify canonical game opcodes exist in generated code."""
        content = TARGET_CS.read_text(encoding="utf-8")
        canonical_msgs = [
            "class CharacterListMessage : IProtocolMessage",
            "class MapLoadMessage : IProtocolMessage",
            "class MapTerrainMessage : IProtocolMessage",
            "class CharacterCharacteristicsMessage : IProtocolMessage",
            "class MapActorsMessage : IProtocolMessage",
            "class EntityMovementMessage : IProtocolMessage",
            "class ChatServerMessage : IProtocolMessage",
            "class NpcDialogQuestionMessage : IProtocolMessage",
            "class InventoryContentMessage : IProtocolMessage",
            "class FightTurnMessage : IProtocolMessage",
        ]
        for cm in canonical_msgs:
            self.assertIn(cm, content, f"Missing canonical message: {cm}")

    def test_haapi_models_present(self):
        """Verify Haapi models for web auth and server roster."""
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("public class HaapiSessionToken", content)
        self.assertIn("public class HaapiServerRosterEntry", content)

    def test_dispatcher_and_registry(self):
        """Verify dispatcher and registry registration."""
        content = TARGET_CS.read_text(encoding="utf-8")
        self.assertIn("public static string ExtractOpcode", content)
        self.assertIn("public static bool Dispatch", content)
        self.assertIn("public static int RegisteredCount", content)
        self.assertIn("public static IProtocolMessage CreateMessage", content)


if __name__ == "__main__":
    unittest.main()

