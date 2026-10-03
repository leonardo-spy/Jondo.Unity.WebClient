"""
Extract font data from bundles using UnityPy.
Exports .ttf files and FontAsset metadata (glyph tables, atlas info).
"""
import sys, json, os, glob

import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"

BUNDLE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11", "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64")
BUNDLE_DIR_ALT = r"C:\DofusWeb\Assets\StreamingAssets\aa\StandaloneWindows64"
OUT_DIR = r"C:\DofusWeb\Assets\Resources\Fonts"

bundle_dir = BUNDLE_DIR if os.path.isdir(BUNDLE_DIR) else BUNDLE_DIR_ALT
os.makedirs(OUT_DIR, exist_ok=True)

bundles = glob.glob(os.path.join(bundle_dir, "*.bundle"))
print(f"Scanning {len(bundles)} bundles for fonts...")

extracted = 0

for bp in bundles:
    name = os.path.basename(bp).replace(".bundle", "")
    if "monoscripts" in name or "unitybuiltinassets" in name:
        continue

    try:
        env = UnityPy.load(bp)
    except:
        continue

    for obj in env.objects:
        # Extract raw Font (.ttf/.otf) data
        if obj.type.name == "Font":
            try:
                data = obj.read()
                font_name = data.m_Name
                # Try to get the raw font data
                if hasattr(data, 'm_FontData') and data.m_FontData:
                    font_bytes = bytes(data.m_FontData)
                    if len(font_bytes) > 100:
                        ext = ".otf" if font_bytes[:4] == b"OTTO" else ".ttf"
                        out_path = os.path.join(OUT_DIR, f"{font_name}{ext}")
                        if not os.path.exists(out_path):
                            with open(out_path, "wb") as f:
                                f.write(font_bytes)
                            print(f"  Font: {font_name}{ext} ({len(font_bytes)} bytes) from {name}")
                            extracted += 1
                        else:
                            print(f"  Font: {font_name}{ext} (exists, skipped)")
                    else:
                        print(f"  Font: {font_name} (no embedded data, {len(font_bytes)} bytes)")
                else:
                    print(f"  Font: {font_name} (no m_FontData)")
            except Exception as e:
                print(f"  Font error: {e}")

        # Extract Texture2D that might be font atlases
        if obj.type.name == "Texture2D":
            try:
                data = obj.read()
                tex_name = data.m_Name
                if any(kw in tex_name.lower() for kw in ["atlas", "lexend", "sdf", "font"]):
                    w, h = data.m_Width, data.m_Height
                    if w > 1 and h > 1:
                        img = data.image
                        out_path = os.path.join(OUT_DIR, f"{tex_name}.png")
                        if not os.path.exists(out_path):
                            img.save(out_path)
                            print(f"  Atlas: {tex_name}.png ({w}x{h}) from {name}")
                            extracted += 1
            except Exception as e:
                pass

        # Extract FontAsset (TMP/SDF) metadata
        if obj.type.name == "MonoBehaviour":
            try:
                tree = obj.read_typetree()
                if not tree:
                    continue
                if "m_FaceInfo" in tree and "m_GlyphTable" in tree:
                    fa_name = tree.get("m_Name", "unknown")
                    face = tree.get("m_FaceInfo", {})
                    glyphs = tree.get("m_GlyphTable", [])
                    chars = tree.get("m_CharacterTable", [])

                    info = {
                        "name": fa_name,
                        "bundle": name,
                        "faceInfo": {
                            "familyName": face.get("m_FamilyName", ""),
                            "styleName": face.get("m_StyleName", ""),
                            "pointSize": face.get("m_PointSize", 0),
                            "lineHeight": face.get("m_LineHeight", 0),
                            "ascentLine": face.get("m_AscentLine", 0),
                            "descentLine": face.get("m_DescentLine", 0),
                        },
                        "glyphCount": len(glyphs),
                        "charCount": len(chars),
                    }
                    print(f"  FontAsset: {fa_name} ({len(glyphs)} glyphs, {len(chars)} chars) from {name}")

                    # Save metadata
                    meta_path = os.path.join(OUT_DIR, f"{fa_name}_meta.json")
                    with open(meta_path, "w") as f:
                        json.dump(info, f, indent=2)
                    extracted += 1

                    # Try to extract atlas texture reference
                    atlas_ref = tree.get("m_AtlasTextures", [])
                    if atlas_ref:
                        print(f"    Atlas refs: {len(atlas_ref)}")

            except:
                pass

print(f"\nTotal extracted: {extracted}")
