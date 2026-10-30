import json
import os
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).parent))
import generate_audio_methods


class GenerateAudioMethodsTests(unittest.TestCase):
    def test_audio_code_contains_status_enum(self):
        code = generate_audio_methods.generate_audio_code()
        self.assertIn("public enum AudioManagerStatus", code)
        self.assertIn("Uninitialized = 0,", code)
        self.assertIn("Initializing = 1,", code)
        self.assertIn("Initialized = 2,", code)

    def test_audio_code_contains_transpiled_classes(self):
        code = generate_audio_methods.generate_audio_code()
        self.assertIn("public struct AudioEventReference : IEquatable<AudioEventReference>", code)
        self.assertIn("public interface ewc", code)
        self.assertIn("public static class AudioManager", code)
        self.assertIn("public class ReconstructedAudioService : ewc", code)

    def test_audio_code_preserves_method_signatures(self):
        code = generate_audio_methods.generate_audio_code()
        self.assertIn("public static bool PlayOneShot(string eventId, float volume = 1.0f)", code)
        self.assertIn("public static bool PlayMusic(string musicEvent, float volume = 0.5f)", code)
        self.assertIn("public static bool SetVolume(string busId, float volume)", code)
        self.assertIn("public static float GetBusVolume(string busId)", code)
        self.assertIn("public void zlx(string playlistId, bool restart = false)", code)
        self.assertIn("public void zmb()", code)
        self.assertIn("public void zmi()", code)
        self.assertIn("public float zml(string busName)", code)
        self.assertIn("public void zmm(string busName, float volume)", code)
        self.assertIn("public void zmy(long mapId)", code)
        self.assertIn("public void znd(string eventPath)", code)

    def test_audio_code_contains_game_sound_helpers(self):
        code = generate_audio_methods.generate_audio_code()
        self.assertIn("public void PlayFootstep(int cellId)", code)
        self.assertIn("public void PlayInventorySound(bool open)", code)
        self.assertIn("public void PlayGrimoireSound(bool open)", code)
        self.assertIn("public void PlayNpcSound(bool open)", code)
        self.assertIn("public void PlayButtonClick()", code)
        self.assertIn("public void PlayRollover()", code)

    def test_target_file_is_written_and_non_empty(self):
        target = generate_audio_methods.TARGET_CS
        self.assertTrue(target.exists())
        self.assertGreater(target.stat().st_size, 3000)


if __name__ == "__main__":
    unittest.main()
