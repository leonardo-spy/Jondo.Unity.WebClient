"""
Extract Figma theme colors from data_assets_all.bundle using UnityPy.
Parses the bundle binary directly — no dependency on Unity's type system.
Outputs ThemeColors.json to DofusWeb/Assets/Resources/.
"""
import sys, json, os

try:
    import UnityPy
    UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"
except ImportError:
    print("pip install UnityPy")
    sys.exit(1)

BUNDLE = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11", "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64", "data_assets_all.bundle")

# Also check DofusWeb path
BUNDLE_ALT = r"C:\DofusWeb\Assets\StreamingAssets\aa\StandaloneWindows64\data_assets_all.bundle"

bundle_path = BUNDLE if os.path.exists(BUNDLE) else BUNDLE_ALT
if not os.path.exists(bundle_path):
    print(f"Bundle not found at {BUNDLE} or {BUNDLE_ALT}")
    sys.exit(1)

print(f"Loading: {bundle_path}")
env = UnityPy.load(bundle_path)

colors = {}
total_objects = 0

for path, obj in env.container.items():
    print(f"  Container: {path} type={obj.type.name}")
    total_objects += 1

    try:
        data = obj.read()
        tree = obj.read_typetree()

        if tree is None:
            print(f"    No typetree")
            continue

        print(f"    Keys: {list(tree.keys())[:10]}")

        # Look for m_figmaColorsData directly
        if "m_figmaColorsData" in tree:
            for entry in tree["m_figmaColorsData"]:
                var_name = entry.get("varName", "")
                color = entry.get("color", {})
                if var_name:
                    r = int(max(0, min(1, color.get("x", 0))) * 255)
                    g = int(max(0, min(1, color.get("y", 0))) * 255)
                    b = int(max(0, min(1, color.get("z", 0))) * 255)
                    a = max(0, min(1, color.get("w", 1)))
                    if a >= 0.999:
                        colors[var_name] = f"rgb({r}, {g}, {b})"
                    else:
                        colors[var_name] = f"rgba({r}, {g}, {b}, {a:.4f})".rstrip('0').rstrip('.')
            print(f"    Direct colors: {len(colors)}")

        # Look for m_figmaThemeManagerScriptable → m_figmaThemeData → themeScriptable → m_figmaColorsData
        if "m_figmaThemeManagerScriptable" in tree:
            mgr_ref = tree["m_figmaThemeManagerScriptable"]
            print(f"    ThemeManager ref: {type(mgr_ref)}")

        # Look for m_figmaStylesData
        if "m_figmaStylesData" in tree:
            print(f"    Styles count: {len(tree['m_figmaStylesData'])}")
            for s in tree["m_figmaStylesData"][:3]:
                print(f"      Style: {s}")

    except Exception as e:
        print(f"    Error: {e}")

# Also scan ALL objects (not just container)
print(f"\nScanning all objects...")
for obj in env.objects:
    try:
        if obj.type.name in ("MonoBehaviour",):
            data = obj.read()
            tree = obj.read_typetree()
            if tree and "m_figmaColorsData" in tree:
                print(f"  Found colors in MonoBehaviour!")
                for entry in tree["m_figmaColorsData"]:
                    var_name = entry.get("varName", "")
                    color = entry.get("color", {})
                    if var_name and var_name not in colors:
                        r = int(max(0, min(1, color.get("x", 0))) * 255)
                        g = int(max(0, min(1, color.get("y", 0))) * 255)
                        b = int(max(0, min(1, color.get("z", 0))) * 255)
                        a = max(0, min(1, color.get("w", 1)))
                        if a >= 0.999:
                            colors[var_name] = f"rgb({r}, {g}, {b})"
                        else:
                            colors[var_name] = f"rgba({r}, {g}, {b}, {a:.4f})".rstrip('0').rstrip('.')
                print(f"  Total colors now: {len(colors)}")
            elif tree and "m_figmaThemeData" in tree:
                print(f"  Found ThemeManager! themes={len(tree['m_figmaThemeData'])}")
                for td in tree["m_figmaThemeData"]:
                    print(f"    Theme: {td.get('theme', '?')}")
                    ts = td.get("themeScriptable", {})
                    if isinstance(ts, dict) and "m_figmaColorsData" in ts:
                        for entry in ts["m_figmaColorsData"]:
                            var_name = entry.get("varName", "")
                            color = entry.get("color", {})
                            if var_name and var_name not in colors:
                                r = int(max(0, min(1, color.get("x", 0))) * 255)
                                g = int(max(0, min(1, color.get("y", 0))) * 255)
                                b = int(max(0, min(1, color.get("z", 0))) * 255)
                                a = max(0, min(1, color.get("w", 1)))
                                if a >= 0.999:
                                    colors[var_name] = f"rgb({r}, {g}, {b})"
                                else:
                                    colors[var_name] = f"rgba({r}, {g}, {b}, {a:.4f})".rstrip('0').rstrip('.')
                        print(f"  Colors from theme: {len(colors)}")
    except Exception as e:
        pass

print(f"\nTotal colors extracted: {len(colors)}")

if colors:
    out_path = r"C:\DofusWeb\Assets\Resources\ThemeColors.json"
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(colors, f, indent=2, ensure_ascii=False)
    print(f"Written to {out_path}")
    print(f"Sample: {dict(list(colors.items())[:5])}")
else:
    print("No colors found. Dumping all object types:")
    for obj in env.objects:
        print(f"  type={obj.type.name} pathID={obj.path_id}")
