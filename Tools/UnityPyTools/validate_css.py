"""
Validate CSS reconstruction by comparing original StyleSheet type trees
with the reconstructed .uss files.
Reports mismatches in selector count, property count, and asset references.
"""
import sys, json, os, glob

import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"

BUNDLE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11",
    "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64")
BUNDLE_DIR_ALT = r"C:\DofusWeb\Assets\StreamingAssets\aa\StandaloneWindows64"
USS_DIR = r"C:\DofusWeb\Assets"

bundle_dir = BUNDLE_DIR if os.path.isdir(BUNDLE_DIR) else BUNDLE_DIR_ALT

bundles = glob.glob(os.path.join(bundle_dir, "*.bundle"))

original_stats = {}
total_ss = 0
total_rules = 0
total_selectors = 0
total_assets = 0
total_imports = 0

print("=== Phase 1: Reading original StyleSheets from Win64 bundles ===")

for bp in bundles:
    name = os.path.basename(bp).replace(".bundle", "")
    if "monoscripts" in name or "unitybuiltinassets" in name:
        continue
    try:
        env = UnityPy.load(bp)
    except:
        continue

    for path_id, obj in enumerate(env.objects):
        if obj.type.name != "MonoBehaviour":
            # StyleSheet is a native type, check for it
            pass
        # Try reading as generic object with type tree
        try:
            tree = obj.read_typetree()
            if not tree:
                continue
            # Check if this looks like a StyleSheet (has m_Rules, floats, etc.)
            if "m_Rules" not in tree:
                continue

            ss_name = tree.get("m_Name", f"unknown_{path_id}")
            rules = tree.get("m_Rules", [])
            assets = tree.get("assets", [])
            imports = tree.get("imports", [])
            scalable = tree.get("scalableImages", [])

            sel_count = 0
            prop_count = 0
            for rule in rules:
                sels = rule.get("m_ComplexSelectors", [])
                props = rule.get("m_Properties", [])
                sel_count += len(sels)
                prop_count += len(props)

            if sel_count > 0 or prop_count > 0:
                original_stats[ss_name] = {
                    "rules": len(rules),
                    "selectors": sel_count,
                    "properties": prop_count,
                    "assets": len(assets) if isinstance(assets, list) else 0,
                    "imports": len(imports) if isinstance(imports, list) else 0,
                    "scalableImages": len(scalable) if isinstance(scalable, list) else 0,
                    "bundle": name,
                }
                total_ss += 1
                total_rules += len(rules)
                total_selectors += sel_count
                total_assets += len(assets) if isinstance(assets, list) else 0
                total_imports += len(imports) if isinstance(imports, list) else 0

        except:
            pass

print(f"  StyleSheets with selectors: {total_ss}")
print(f"  Total rules: {total_rules}")
print(f"  Total selectors: {total_selectors}")
print(f"  Total asset refs: {total_assets}")
print(f"  Total imports: {total_imports}")

# Phase 2: Check reconstructed USS files
print(f"\n=== Phase 2: Checking reconstructed .uss files ===")
uss_files = glob.glob(os.path.join(USS_DIR, "**", "*.uss"), recursive=True)
print(f"  Reconstructed USS files: {len(uss_files)}")

# Count rules in reconstructed USS (approximate: count '{' at start of line)
reconstructed_rules = 0
reconstructed_with_content = 0
for uss in uss_files:
    try:
        with open(uss, 'r', encoding='utf-8') as f:
            content = f.read()
        # Count CSS rule blocks (lines ending with '{')
        blocks = content.count('{')
        if blocks > 0:
            reconstructed_with_content += 1
            reconstructed_rules += blocks
    except:
        pass

print(f"  USS with content: {reconstructed_with_content}")
print(f"  Approximate rule blocks: {reconstructed_rules}")

# Phase 3: Compare
print(f"\n=== Phase 3: Comparison ===")
print(f"  Original StyleSheets with selectors: {total_ss}")
print(f"  Reconstructed USS files: {len(uss_files)}")
print(f"  Original rules: {total_rules}")
print(f"  Reconstructed blocks: {reconstructed_rules}")
print(f"  Original asset refs: {total_assets}")

# Find missing USS
uss_names = set()
for uss in uss_files:
    base = os.path.splitext(os.path.basename(uss))[0]
    uss_names.add(base.lower())

missing = []
for ss_name, stats in original_stats.items():
    if ss_name.lower() not in uss_names and ss_name != "inlineStyle":
        missing.append((ss_name, stats))

if missing:
    print(f"\n  MISSING USS ({len(missing)}):")
    for name, stats in missing[:10]:
        print(f"    {name}: {stats['rules']} rules, {stats['selectors']} selectors (from {stats['bundle']})")
else:
    print(f"\n  All StyleSheets with selectors have matching USS files!")

# Save report
report = {
    "original": {"stylesheets": total_ss, "rules": total_rules, "selectors": total_selectors, "assets": total_assets},
    "reconstructed": {"files": len(uss_files), "with_content": reconstructed_with_content, "rule_blocks": reconstructed_rules},
    "missing": [{"name": n, "stats": s} for n, s in missing],
    "per_stylesheet": original_stats,
}
out_path = os.path.join(os.path.dirname(__file__), "css_validation_report.json")
with open(out_path, "w") as f:
    json.dump(report, f, indent=2)
print(f"\nReport saved: {out_path}")
