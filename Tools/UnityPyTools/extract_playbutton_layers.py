"""
Extract PlayButton layer textures from bundles.
The PlayButton uses overlaid visual layers (shadow, light, gems, border, background)
that are referenced by the constructor but not available as standalone files.
"""
import sys, os, glob

import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"

BUNDLE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11",
    "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64")
BUNDLE_DIR_ALT = r"C:\DofusWeb\Assets\StreamingAssets\aa\StandaloneWindows64"
OUT_DIR = r"C:\DofusWeb\Assets\UI\USSTextures"

bundle_dir = BUNDLE_DIR if os.path.isdir(BUNDLE_DIR) else BUNDLE_DIR_ALT

# Keywords for PlayButton-related textures
PLAY_KEYWORDS = ["playbutton", "play_button", "bigplay", "loginbutton",
                  "playbtn", "btn_play", "buttonplay"]
# Keywords for layer textures (shadow, light, gems, border)
LAYER_KEYWORDS = ["shadow", "light", "gems", "border", "glow", "shine",
                   "effect", "hover", "pressed", "normal"]

bundles = glob.glob(os.path.join(bundle_dir, "*.bundle"))
print(f"Scanning {len(bundles)} bundles for PlayButton textures...")

extracted = 0
play_textures = []

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
                tex_name = data.m_Name.lower()

                # Check if this is a PlayButton-related texture
                is_play = any(kw in tex_name for kw in PLAY_KEYWORDS)
                is_layer = any(kw in tex_name for kw in LAYER_KEYWORDS) and any(
                    kw in tex_name for kw in ["btn", "button", "play"])

                if is_play or is_layer:
                    w, h = data.m_Width, data.m_Height
                    if w > 1 and h > 1:
                        play_textures.append({
                            "name": data.m_Name,
                            "size": f"{w}x{h}",
                            "bundle": name
                        })

                        out_path = os.path.join(OUT_DIR, f"{data.m_Name}.png")
                        if not os.path.exists(out_path):
                            img = data.image
                            img.save(out_path)
                            print(f"  {data.m_Name}.png ({w}x{h}) from {name}")
                            extracted += 1

            except:
                pass

        # Also check Sprite type
        if obj.type.name == "Sprite":
            try:
                data = obj.read()
                spr_name = data.m_Name.lower()
                is_play = any(kw in spr_name for kw in PLAY_KEYWORDS)
                if is_play:
                    play_textures.append({
                        "name": data.m_Name,
                        "type": "Sprite",
                        "bundle": name
                    })
                    print(f"  Sprite: {data.m_Name} from {name}")
            except:
                pass

print(f"\nPlayButton-related textures found: {len(play_textures)}")
for t in play_textures:
    print(f"  {t['name']} ({t.get('size', 'sprite')}) from {t['bundle']}")

print(f"\nExtracted: {extracted}")

# Also search for textures by scanning all texture names for "play" or "login"
print(f"\n--- All textures matching 'play' or 'login' ---")
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
                tn = data.m_Name.lower()
                if "play" in tn or "login" in tn:
                    print(f"  {data.m_Name} ({data.m_Width}x{data.m_Height}) from {name}")
            except:
                pass
