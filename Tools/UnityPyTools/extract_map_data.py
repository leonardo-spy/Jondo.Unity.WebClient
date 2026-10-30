#!/usr/bin/env python3
"""
Extract MapMetadata from Win64 map bundles as JSON for WebGL consumption.

The Win64 map bundles can't be loaded in WebGL (build target mismatch).
This tool extracts the MapMetadata ScriptableObjects as JSON files that
the WebGLProbe can load directly via HTTP.

Usage:
    py extract_map_data.py                    # extract all maps
    py extract_map_data.py 191104002          # extract specific map
    py extract_map_data.py --world 729        # extract all maps in world 729

Output:
    Content/Map/Json/{mapId}.json             # per-map JSON files (served via /assets/)
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
MAP_DATA_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content", "Map", "Data")
OUTPUT_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content", "Map", "Json")


def extract_map(bundle_path, target_map_id=None):
    """Extract MapMetadata from a bundle as JSON."""
    env = UnityPy.load(bundle_path)
    results = []

    for obj in env.objects:
        if obj.type.name != 'MonoBehaviour':
            continue

        tree = obj.read_typetree()
        name = tree.get('m_Name', '')
        if not name.startswith('map_'):
            continue

        map_id_str = name.replace('map_', '')
        if target_map_id and map_id_str != str(target_map_id):
            continue

        md = tree.get('mapData', {})
        if not md:
            continue

        map_json = {
            'mapId': int(map_id_str),
            'name': name,
            'neighbors': {
                'top': md.get('topNeighbourId', 0),
                'bottom': md.get('bottomNeighbourId', 0),
                'left': md.get('leftNeighbourId', 0),
                'right': md.get('rightNeighbourId', 0),
            },
            'backgroundColor': md.get('backgroundColor', {}),
            'cells': [],
            'backgroundElements': [],
            'sortableElements': [],
            'foregroundElements': [],
            'backgroundAtlas': extract_atlas(md.get('backgroundMaterialData', {})),
            'sortableAtlas': extract_atlas(md.get('sortableMaterialData', {})),
            'foregroundAtlas': extract_atlas(md.get('foregroundMaterialData', {})),
            'allowMapEffects': tree.get('allowMapEffects', False),
        }

        for cell in md.get('cellsData', []):
            map_json['cells'].append({
                'n': cell.get('cellNumber', 0),
                'm': 1 if cell.get('mov', False) else 0,
                'l': 1 if cell.get('los', False) else 0,
                'f': cell.get('floor', 0),
                's': cell.get('speed', 0),
                'a': cell.get('arrow', 0),
            })

        for layer_name in ['backgroundElements', 'sortableElements', 'foregroundElements']:
            for el in md.get(layer_name, []):
                elem = {
                    'g': el.get('gfxId', 0),
                    'c': el.get('color', {}).get('value', 0xFFFFFFFF),
                    't': [
                        el.get('transform', {}).get('m11', 1.0),
                        el.get('transform', {}).get('m12', 0.0),
                        el.get('transform', {}).get('m21', 0.0),
                        el.get('transform', {}).get('m22', 1.0),
                        el.get('transform', {}).get('m31', 0.0),
                        el.get('transform', {}).get('m32', 0.0),
                    ],
                    'mi': el.get('materialIndex', 0),
                }
                if 'cellId' in el:
                    elem['ci'] = el['cellId']
                map_json[layer_name].append(elem)

        results.append(map_json)

    return results


def extract_atlas(material_data):
    """Extract atlas UV rect mapping from MaterialData."""
    if not material_data:
        return None

    atlas = material_data.get('atlas1x', {})
    if not atlas:
        return None

    rects = atlas.get('idsToRects', {})
    keys = rects.get('m_keys', [])
    values = rects.get('m_values', [])

    if not keys or not values:
        return None

    return {
        'width': atlas.get('width', 0),
        'height': atlas.get('height', 0),
        'rects': {str(k): {'x': v['x'], 'y': v['y'], 'w': v['width'], 'h': v['height']}
                  for k, v in zip(keys, values)},
    }


def main():
    target_map = None
    target_world = None

    for i, arg in enumerate(sys.argv[1:], 1):
        if arg == '--world' and i < len(sys.argv) - 1:
            target_world = int(sys.argv[i + 1])
        elif arg.isdigit():
            target_map = int(arg)

    os.makedirs(OUTPUT_DIR, exist_ok=True)

    if not os.path.isdir(MAP_DATA_DIR):
        print(f"ERROR: Map data dir not found: {MAP_DATA_DIR}")
        sys.exit(1)

    bundles = sorted(f for f in os.listdir(MAP_DATA_DIR) if f.endswith('.bundle'))
    print(f"Found {len(bundles)} map bundles in {MAP_DATA_DIR}")

    total_maps = 0
    start = time.time()

    for bundle_name in bundles:
        if target_world is not None:
            world_str = bundle_name.replace('mapdata_assets_world_', '').replace('.bundle', '')
            if world_str.isdigit() and int(world_str) != target_world:
                continue

        bundle_path = os.path.join(MAP_DATA_DIR, bundle_name)
        try:
            maps = extract_map(bundle_path, target_map)
            for m in maps:
                map_id = m['mapId']
                out_path = os.path.join(OUTPUT_DIR, f"{map_id}.json")
                with open(out_path, 'w', encoding='utf-8') as f:
                    json.dump(m, f, separators=(',', ':'))
                total_maps += 1

                if target_map:
                    print(f"  Extracted map {map_id}: {len(m['cells'])} cells, "
                          f"{len(m['backgroundElements'])} bg, {len(m['sortableElements'])} sort, "
                          f"{len(m['foregroundElements'])} fg")
        except Exception as e:
            print(f"  ERROR {bundle_name}: {e}")

        if target_map and total_maps > 0:
            break

    elapsed = time.time() - start
    print(f"\nExtracted {total_maps} maps in {elapsed:.1f}s → {OUTPUT_DIR}")


if __name__ == '__main__':
    main()
