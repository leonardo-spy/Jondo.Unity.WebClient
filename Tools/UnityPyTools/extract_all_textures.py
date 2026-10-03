"""
Universal texture extractor — extracts ALL textures from ALL bundles via UnityPy.
Covers textures that RebuildBundlesForWebGL might miss (MonoScript issues, sub-assets, etc.)
Outputs to Assets/UI/USSTextures/ (same as CSS reconstruction output).
"""
import sys, os, glob

import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"

BUNDLE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11",
    "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64")
BUNDLE_DIR_ALT = r"C:\DofusWeb\Assets\StreamingAssets\aa\StandaloneWindows64"
OUT_DIR = r"C:\DofusWeb\Assets\UI\USSTextures"

bundle_dir = BUNDLE_DIR if os.path.isdir(BUNDLE_DIR) else BUNDLE_DIR_ALT
os.makedirs(OUT_DIR, exist_ok=True)

bundles = glob.glob(os.path.join(bundle_dir, "*.bundle"))
print(f"Scanning {len(bundles)} bundles for ALL textures...")

extracted = 0
skipped = 0
errors = 0

for bp in bundles:
    name = os.path.basename(bp).replace(".bundle", "")
    if "monoscripts" in name or "unitybuiltinassets" in name:
        continue
    try:
        env = UnityPy.load(bp)
    except:
        continue

    for obj in env.objects:
        if obj.type.name == "Texture2D":
            try:
                data = obj.read()
                tex_name = data.m_Name
                w, h = data.m_Width, data.m_Height
                if w <= 1 or h <= 1:
                    continue

                out_path = os.path.join(OUT_DIR, f"{tex_name}.png")
                if os.path.exists(out_path):
                    skipped += 1
                    continue

                img = data.image
                img.save(out_path)
                extracted += 1
            except Exception as e:
                errors += 1

    bundle_count = extracted + skipped
    if bundle_count % 100 == 0 and bundle_count > 0:
        print(f"  Progress: {extracted} extracted, {skipped} skipped, {errors} errors")

print(f"\nDone: {extracted} extracted, {skipped} already exist, {errors} errors")
print(f"Total unique textures in {OUT_DIR}: {len(glob.glob(os.path.join(OUT_DIR, '*.png')))}")
