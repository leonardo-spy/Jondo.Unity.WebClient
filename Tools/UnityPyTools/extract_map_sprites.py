#!/usr/bin/env python3
"""
Extract map sprite textures as PNG from Win64 texture bundles.

Win64 bundles can't be loaded in WebGL. This extracts sprites as PNGs
served via HTTP for the WebGLProbe to load directly.

Usage:
    py extract_map_sprites.py 191104002           # sprites for a specific map
    py extract_map_sprites.py --all-maps           # sprites for all extracted map JSONs
    py extract_map_sprites.py --gfx 900 60985      # specific gfxIds

Output:
    Content/Map/Sprites/1x/{gfxId}.png
"""

import json
import os
import sys
import time

try:
    import UnityPy
    UnityPy.config.FALLBACK_UNITY_VERSION = '6000.3.16f1'
except ImportError:
    print("ERROR: pip install UnityPy")
    sys.exit(1)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.join(SCRIPT_DIR, "..", "..")
CLIENT_DIR = os.environ.get("DOFUS_CLIENT_DIR", os.path.join(REPO_ROOT, "Cliente 3.6.10.11"))
TEX_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content", "Map", "Textures", "1x")
JSON_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content", "Map", "Json")
OUTPUT_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content", "Map", "Sprites", "1x")


def get_gfx_ids_for_map(map_id):
    json_path = os.path.join(JSON_DIR, f"{map_id}.json")
    if not os.path.exists(json_path):
        print(f"ERROR: {json_path} not found. Run extract_map_data.py {map_id} first.")
        return set()
    with open(json_path) as f:
        data = json.load(f)
    gfx = set()
    for layer in ['backgroundElements', 'sortableElements', 'foregroundElements']:
        for el in data.get(layer, []):
            gfx.add(el['g'])
    return gfx


AVAILABLE_RANGES = [
    0, 10000, 20000, 30000, 40000, 50000, 60000, 70000, 80000, 90000,
    100000, 110000, 120000, 130000, 140000, 150000, 200000, 250000, 260000,
    300000, 400000, 500000, 550000, 600000, 700000, 800000, 900000,
    3060000, 13370000
]


def gfx_to_bundle_range(gfx_id):
    best = 0
    for r in AVAILABLE_RANGES:
        if r <= gfx_id:
            best = r
        else:
            break
    return best


def extract_sprites(needed_gfx, output_dir):
    os.makedirs(output_dir, exist_ok=True)

    already = set()
    for f in os.listdir(output_dir):
        if f.endswith('.png'):
            gid = f.replace('.png', '')
            if gid.isdigit():
                already.add(int(gid))

    remaining = needed_gfx - already
    if not remaining:
        print(f"All {len(needed_gfx)} sprites already extracted")
        return len(needed_gfx)

    print(f"Need {len(remaining)} sprites ({len(already)} already cached)")

    ranges = {}
    for gfx in remaining:
        r = gfx_to_bundle_range(gfx)
        ranges.setdefault(r, set()).add(gfx)

    total = 0
    for r, gfx_set in sorted(ranges.items()):
        bundle_name = f"mapgfx_1x_{r}_assets_all.bundle"
        bundle_path = os.path.join(TEX_DIR, bundle_name)
        if not os.path.exists(bundle_path):
            print(f"  SKIP: {bundle_name} not found")
            continue

        env = UnityPy.load(bundle_path)
        extracted = 0
        for obj in env.objects:
            if obj.type.name == 'Texture2D':
                tex = obj.read()
                name = tex.m_Name
                if name.isdigit() and int(name) in gfx_set:
                    img = tex.image
                    out_path = os.path.join(output_dir, f"{name}.png")
                    img.save(out_path)
                    extracted += 1
                    gfx_set.discard(int(name))

        total += extracted
        print(f"  {bundle_name}: {extracted}/{len(gfx_set) + extracted} sprites")

    return total + len(already)


def main():
    needed_gfx = set()

    if len(sys.argv) > 1 and sys.argv[1] == '--gfx':
        needed_gfx = {int(x) for x in sys.argv[2:]}
    elif len(sys.argv) > 1 and sys.argv[1] == '--all-maps':
        if os.path.isdir(JSON_DIR):
            for f in os.listdir(JSON_DIR):
                if f.endswith('.json'):
                    mid = f.replace('.json', '')
                    needed_gfx |= get_gfx_ids_for_map(mid)
        print(f"All maps: {len(needed_gfx)} unique gfxIds")
    elif len(sys.argv) > 1 and sys.argv[1].isdigit():
        map_id = int(sys.argv[1])
        needed_gfx = get_gfx_ids_for_map(map_id)
        print(f"Map {map_id}: {len(needed_gfx)} unique gfxIds")
    else:
        print(__doc__)
        return

    if not needed_gfx:
        print("No gfxIds to extract")
        return

    start = time.time()
    total = extract_sprites(needed_gfx, OUTPUT_DIR)
    elapsed = time.time() - start
    print(f"\n{total} sprites available in {elapsed:.1f}s -> {OUTPUT_DIR}")


if __name__ == '__main__':
    main()
