"""
Extract ALL Figma theme colors from data_assets_all.bundle.
Outputs per-theme JSONs + merged ThemeColors.json (Standard as default).
"""
import sys, json, os

import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"

BUNDLE = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11",
    "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64", "data_assets_all.bundle")
BUNDLE_ALT = r"C:\DofusWeb\Assets\StreamingAssets\aa\StandaloneWindows64\data_assets_all.bundle"
OUT_DIR = r"C:\DofusWeb\Assets\Resources"

bundle_path = BUNDLE if os.path.exists(BUNDLE) else BUNDLE_ALT
env = UnityPy.load(bundle_path)

THEME_NAMES = {0:"None", 1:"Standard", 2:"Tribute", 3:"Bonta", 4:"Brakmar",
               5:"GoldAndSteel", 6:"EmeraldMine", 7:"Belladone", 8:"Unicorn",
               9:"Sufokia", 10:"Pandala", 11:"Wabbit"}

def vec4_to_css(color):
    r = int(max(0, min(1, color.get("x", 0))) * 255)
    g = int(max(0, min(1, color.get("y", 0))) * 255)
    b = int(max(0, min(1, color.get("z", 0))) * 255)
    a = max(0, min(1, color.get("w", 1)))
    if a >= 0.999:
        return f"rgb({r}, {g}, {b})"
    return f"rgba({r}, {g}, {b}, {a:.2f})"

# Collect: theme_name -> {varName: css_color}
themes = {}
styles = []

for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    try:
        tree = obj.read_typetree()
        if not tree:
            continue
        name = tree.get("m_Name", "")

        # FigmaThemeScriptable — individual theme
        if "m_figmaColorsData" in tree:
            colors = {}
            for entry in tree["m_figmaColorsData"]:
                vn = entry.get("varName", "")
                if vn:
                    colors[vn] = vec4_to_css(entry.get("color", {}))
            if colors:
                themes[name] = colors
                print(f"  Theme '{name}': {len(colors)} colors")

        # FigmaStylesScriptableObject — styles
        if "m_figmaStylesData" in tree:
            for s in tree["m_figmaStylesData"]:
                styles.append({
                    "varName": s.get("varName", ""),
                    "name": s.get("name", ""),
                    "type": s.get("style_type", ""),
                })
            print(f"  Styles: {len(styles)}")

    except:
        pass

print(f"\n{len(themes)} themes found: {list(themes.keys())}")
print(f"{len(styles)} styles found")

os.makedirs(OUT_DIR, exist_ok=True)

# Save each theme
for tname, colors in themes.items():
    path = os.path.join(OUT_DIR, f"ThemeColors_{tname}.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(colors, f, indent=2, ensure_ascii=False)

# Standard as default ThemeColors.json (or first non-empty)
default = themes.get("Standard") or themes.get("Unity") or next(iter(themes.values()), {})
# Also add universal colors derived from naming convention
universal = {
    "black_black100": "rgb(0, 0, 0)",
    "black_black80": "rgba(0, 0, 0, 0.80)",
    "black_black50": "rgba(0, 0, 0, 0.50)",
    "black_black30": "rgba(0, 0, 0, 0.30)",
    "black_black15": "rgba(0, 0, 0, 0.15)",
    "white_white100": "rgb(255, 255, 255)",
    "white_white80": "rgba(255, 255, 255, 0.80)",
    "white_white50": "rgba(255, 255, 255, 0.50)",
    "white_white30": "rgba(255, 255, 255, 0.30)",
    "white_white15": "rgba(255, 255, 255, 0.15)",
}
merged = {**universal, **{k.lstrip('-'): v for k, v in default.items()}}

path = os.path.join(OUT_DIR, "ThemeColors.json")
with open(path, "w", encoding="utf-8") as f:
    json.dump(merged, f, indent=2, ensure_ascii=False)
print(f"\nThemeColors.json: {len(merged)} entries (Standard + universal)")

# Save styles
if styles:
    path = os.path.join(OUT_DIR, "FigmaStyles.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(styles, f, indent=2, ensure_ascii=False)
    print(f"FigmaStyles.json: {len(styles)} styles")

# Print sample
print(f"\nSample theme colors (Standard):")
for k, v in list(default.items())[:5]:
    print(f"  {k}: {v}")
print(f"\nSample universal colors:")
for k, v in list(universal.items())[:3]:
    print(f"  {k}: {v}")
