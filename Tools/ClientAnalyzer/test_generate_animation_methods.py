import json
import os
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).parent))
import generate_animation_methods


class GenerateAnimationMethodsTests(unittest.TestCase):
    def test_animation_code_contains_anim_transform_matrix(self):
        code = generate_animation_methods.generate_animation_code()
        self.assertIn("public struct AnimTransform : IEquatable<AnimTransform>", code)
        self.assertIn("public float rX;", code)
        self.assertIn("public float uX;", code)
        self.assertIn("public float rY;", code)
        self.assertIn("public float uY;", code)
        self.assertIn("public float tX;", code)
        self.assertIn("public float tY;", code)
        self.assertIn("public static AnimTransform Multiply(AnimTransform parent, AnimTransform child)", code)

    def test_animation_code_contains_skin_assets(self):
        code = generate_animation_methods.generate_animation_code()
        self.assertIn("public class SkinAssetPart", code)
        self.assertIn("public class SkinAsset : ScriptableObject", code)
        self.assertIn("public class AnimatedObjectDefinition", code)
        self.assertIn("Anchor_fight", code)
        self.assertIn("Anchor_icons", code)

    def test_animation_code_contains_renderers_and_animators(self):
        code = generate_animation_methods.generate_animation_code()
        self.assertIn("public interface IAnimator2DRenderer", code)
        self.assertIn("public class Animator2DRenderer : IAnimator2DRenderer", code)
        self.assertIn("public sealed class Animator2D : MonoBehaviour", code)
        self.assertIn("public class EntityAnimator", code)

    def test_animation_code_preserves_method_signatures(self):
        code = generate_animation_methods.generate_animation_code()
        self.assertIn("public void SetSkin(SkinAsset skin)", code)
        self.assertIn("public void SetFacing(rc direction)", code)
        self.assertIn("public void PlayAnimation(string animationName, bool loop = true)", code)
        self.assertIn("public void SetDirection(rc direction)", code)
        self.assertIn("public void ApplySkin(SkinAsset skin)", code)

    def test_target_file_is_written_and_non_empty(self):
        target = generate_animation_methods.TARGET_CS
        self.assertTrue(target.exists())
        self.assertGreater(target.stat().st_size, 3000)


if __name__ == "__main__":
    unittest.main()

