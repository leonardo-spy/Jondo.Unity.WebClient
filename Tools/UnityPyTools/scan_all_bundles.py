"""
Scan ALL Win64 bundles with UnityPy to find custom ScriptableObjects
that Unity can't load (MonoScript GUID mismatch).
Extracts DofusUISettings, FontAssets, and any other blocked data.
"""
import sys, json, os, glob

import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"

BUNDLE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11", "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64")
BUNDLE_DIR_ALT = r"C:\DofusWeb\Assets\StreamingAssets\aa\StandaloneWindows64"

bundle_dir = BUNDLE_DIR if os.path.isdir(BUNDLE_DIR) else BUNDLE_DIR_ALT
OUT_DIR = r"C:\DofusWeb\Assets\Resources"

print(f"Scanning: {bundle_dir}")

bundles = glob.glob(os.path.join(bundle_dir, "*.bundle"))
print(f"Found {len(bundles)} bundles")

# Collect findings
dofus_ui_settings = None
font_assets = []
custom_sos = []
all_colors = {}

for bp in bundles:
    name = os.path.basename(bp).replace(".bundle", "")
    if "monoscripts" in name or "unitybuiltinassets" in name:
        continue

    try:
        env = UnityPy.load(bp)
    except Exception as e:
        continue

    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            # Check for FontAsset type
            if obj.type.name == "Font":
                try:
                    data = obj.read()
                    tree = obj.read_typetree()
                    if tree:
                        font_name = tree.get("m_Name", "unknown")
                        font_assets.append({"name": font_name, "bundle": name, "type": "Font"})
                except:
                    pass
            continue

        try:
            tree = obj.read_typetree()
            if not tree:
                continue

            obj_name = tree.get("m_Name", "")
            keys = list(tree.keys())

            # DofusUISettings — has TSS references
            if any(k for k in keys if "TssRef" in k or "tssRef" in k or "FontStandard" in str(tree.get(k, ""))):
                print(f"  [DofusUISettings?] '{obj_name}' in {name}")
                print(f"    Keys: {keys[:15]}")
                dofus_ui_settings = {"name": obj_name, "bundle": name, "data": {}}
                for k in keys:
                    v = tree[k]
                    if isinstance(v, (str, int, float, bool)):
                        dofus_ui_settings["data"][k] = v
                    elif isinstance(v, dict) and len(v) < 5:
                        dofus_ui_settings["data"][k] = str(v)

            # FigmaTheme colors
            if "m_figmaColorsData" in keys:
                for entry in tree["m_figmaColorsData"]:
                    var_name = entry.get("varName", "")
                    color = entry.get("color", {})
                    if var_name and var_name not in all_colors:
                        r = int(max(0, min(1, color.get("x", 0))) * 255)
                        g = int(max(0, min(1, color.get("y", 0))) * 255)
                        b = int(max(0, min(1, color.get("z", 0))) * 255)
                        a = max(0, min(1, color.get("w", 1)))
                        if a >= 0.999:
                            all_colors[var_name] = f"rgb({r}, {g}, {b})"
                        else:
                            all_colors[var_name] = f"rgba({r}, {g}, {b}, {a:.4f})".rstrip('0').rstrip('.')

            # Any interesting custom SO
            interesting_keys = [k for k in keys if k not in ("m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_EditorHideFlags", "m_EditorClassIdentifier")]
            if interesting_keys and obj_name:
                custom_sos.append({"name": obj_name, "bundle": name, "keys": interesting_keys[:10]})

        except Exception as e:
            pass

print(f"\n=== Results ===")
print(f"Custom ScriptableObjects: {len(custom_sos)}")
print(f"Font assets: {len(font_assets)}")
print(f"Theme colors: {len(all_colors)}")
print(f"DofusUISettings: {'FOUND' if dofus_ui_settings else 'NOT FOUND'}")

# Save theme colors
if all_colors:
    path = os.path.join(OUT_DIR, "ThemeColors.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(all_colors, f, indent=2, ensure_ascii=False)
    print(f"\nThemeColors.json: {len(all_colors)} colors -> {path}")

# Save DofusUISettings
if dofus_ui_settings:
    path = os.path.join(OUT_DIR, "DofusUISettings.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(dofus_ui_settings, f, indent=2, ensure_ascii=False)
    print(f"DofusUISettings.json -> {path}")

# Print custom SOs summary
if custom_sos:
    print(f"\nCustom ScriptableObjects found:")
    seen = set()
    for so in custom_sos:
        key = f"{so['name']}@{so['bundle']}"
        if key not in seen:
            seen.add(key)
            print(f"  '{so['name']}' in {so['bundle']}: {so['keys'][:5]}")
        if len(seen) > 30:
            print(f"  ... and {len(custom_sos) - 30} more")
            break

if font_assets:
    print(f"\nFont assets:")
    for fa in font_assets:
        print(f"  '{fa['name']}' in {fa['bundle']} type={fa['type']}")
