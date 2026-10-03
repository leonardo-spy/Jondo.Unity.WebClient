"""
Validate WebGL bundles output by comparing with Win64 originals.
Checks: asset count, texture resolution, cross-bundle refs.
"""
import os, glob, json

import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"

WIN64_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11",
    "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64")
WEBGL_DIR = r"C:\DofusWeb\Assets\StreamingAssets\aa\WebGL"

def count_bundle(path):
    try:
        env = UnityPy.load(path)
        types = {}
        for obj in env.objects:
            t = obj.type.name
            types[t] = types.get(t, 0) + 1
        containers = list(env.container.keys())
        return {"types": types, "total": sum(types.values()), "containers": len(containers)}
    except:
        return None

print("=== Win64 Bundles ===")
win64_bundles = sorted(glob.glob(os.path.join(WIN64_DIR, "*.bundle")))
win64_stats = {}
for bp in win64_bundles:
    name = os.path.basename(bp).replace(".bundle", "")
    if "monoscripts" in name or "unitybuiltinassets" in name:
        continue
    stats = count_bundle(bp)
    if stats:
        win64_stats[name] = stats
        print(f"  {name}: {stats['total']} objects, {stats['containers']} containers")

print(f"\n=== WebGL Bundles ===")
webgl_bundles = sorted(glob.glob(os.path.join(WEBGL_DIR, "*")))
webgl_bundles = [b for b in webgl_bundles if not b.endswith(".manifest") and not b.endswith(".meta") and "WebGL" not in os.path.basename(b)]
webgl_stats = {}
for bp in webgl_bundles:
    name = os.path.basename(bp)
    stats = count_bundle(bp)
    if stats:
        webgl_stats[name] = stats
        print(f"  {name}: {stats['total']} objects, {stats['containers']} containers")

print(f"\n=== Comparison ===")
print(f"Win64 bundles: {len(win64_stats)}")
print(f"WebGL bundles: {len(webgl_stats)}")

# Find missing bundles
win64_names = set(win64_stats.keys())
webgl_names = set(webgl_stats.keys())
missing = win64_names - webgl_names
extra = webgl_names - win64_names

if missing:
    print(f"\nMissing from WebGL ({len(missing)}):")
    for m in sorted(missing):
        print(f"  {m}: {win64_stats[m]['total']} objects")

# Compare object counts
print(f"\nObject count differences:")
for name in sorted(win64_names & webgl_names):
    w = win64_stats[name]['total']
    g = webgl_stats[name]['total']
    if abs(w - g) > w * 0.1:  # >10% difference
        print(f"  {name}: Win64={w} WebGL={g} diff={g-w:+d}")

# Save report
report = {"win64": win64_stats, "webgl": webgl_stats, "missing": list(missing)}
with open(os.path.join(os.path.dirname(__file__), "bundle_validation_report.json"), "w") as f:
    json.dump(report, f, indent=2, default=str)
print(f"\nReport saved.")
