#!/usr/bin/env python3
"""
Compose pre-built atlas PNG for a map using extracted sprites and atlas rects.

Instead of runtime PackTextures in C#, this builds the EXACT atlas offline.
The C# side just downloads the atlas PNG + vertex data JSON — zero computation.

Usage:
    py compose_map_atlas.py 191104002              # compose atlas for one map
    py compose_map_atlas.py --all                  # compose for all extracted maps

Output per map:
    Content/Map/Composed/{mapId}_bg.png            # background layer atlas
    Content/Map/Composed/{mapId}_sort.png          # sortable layer atlas
    Content/Map/Composed/{mapId}_fg.png            # foreground layer atlas
    Content/Map/Composed/{mapId}_render.json       # pre-computed vertex + UV data
"""

import json
import math
import os
import sys
import time

try:
    from PIL import Image
except ImportError:
    print("ERROR: pip install Pillow")
    sys.exit(1)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.join(SCRIPT_DIR, "..", "..")
CLIENT_DIR = os.environ.get("DOFUS_CLIENT_DIR", os.path.join(REPO_ROOT, "Cliente 3.6.10.11"))
JSON_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content", "Map", "Json")
SPRITE_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content", "Map", "Sprites", "1x")
OUTPUT_DIR = os.path.join(CLIENT_DIR, "Dofus_Data", "StreamingAssets", "Content", "Map", "Composed")


def load_sprite(gfx_id):
    path = os.path.join(SPRITE_DIR, f"{gfx_id}.png")
    if os.path.exists(path):
        return Image.open(path).convert("RGBA")
    return None


def compose_layer_atlas(elements, atlas_info, layer_name):
    """Compose atlas PNG using the pre-computed rects from MapMetadata."""
    if not atlas_info or not atlas_info.get('rects'):
        return None, None

    width = atlas_info['width']
    height = atlas_info['height']
    rects = atlas_info['rects']

    if width <= 0 or height <= 0:
        return None, None

    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    placed = 0

    for gfx_str, rect in rects.items():
        gfx_id = int(gfx_str)
        sprite = load_sprite(gfx_id)
        if sprite is None:
            continue

        # Rect is normalized (0-1), convert to pixels
        px = int(rect['x'] * width)
        py = int(rect['y'] * height)
        pw = int(rect['w'] * width)
        ph = int(rect['h'] * height)

        # Resize sprite to match atlas rect (may differ from original dimensions)
        if sprite.width != pw or sprite.height != ph:
            sprite = sprite.resize((max(1, pw), max(1, ph)), Image.LANCZOS)

        # Paste into atlas (flip Y because Unity textures are bottom-up)
        atlas_y = height - py - ph
        atlas.paste(sprite, (px, atlas_y), sprite)
        placed += 1

    return atlas, placed


def build_render_data(map_data, map_id):
    """Build pre-computed vertex + UV data for each layer."""
    layers = {}

    for layer_key, atlas_key in [
        ('backgroundElements', 'backgroundAtlas'),
        ('sortableElements', 'sortableAtlas'),
        ('foregroundElements', 'foregroundAtlas')
    ]:
        elements = map_data.get(layer_key, [])
        atlas_info = map_data.get(atlas_key)
        if not elements or not atlas_info:
            continue

        rects = atlas_info.get('rects', {})
        width = atlas_info.get('width', 1)
        height = atlas_info.get('height', 1)

        vertices = []
        for el in elements:
            gfx = el['g']
            gfx_str = str(gfx)
            if gfx_str not in rects:
                continue

            rect = rects[gfx_str]
            color = el['c']
            t = el['t']  # [m11, m12, m21, m22, m31, m32]

            # Sprite dimensions from atlas rect (in pixels)
            pw = rect['w'] * width
            ph = rect['h'] * height
            hw = pw * 0.5
            hh = ph * 0.5

            # UV coordinates (normalized, already correct from MapMetadata)
            u0 = rect['x']
            v0 = rect['y']
            u1 = rect['x'] + rect['w']
            v1 = rect['y'] + rect['h']

            # Quad corners in local space (centered on sprite)
            local = [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)]

            # Apply affine transform
            quad_verts = []
            for lx, ly in local:
                wx = t[0] * lx + t[2] * ly + t[4]  # m11*x + m21*y + m31
                wy = t[1] * lx + t[3] * ly + t[5]  # m12*x + m22*y + m32
                quad_verts.append([round(wx, 2), round(-wy, 2)])  # negate Y for screen coords

            # Color as [r,g,b,a] bytes
            a_byte = (color >> 24) & 0xFF
            r_byte = (color >> 16) & 0xFF
            g_byte = (color >> 8) & 0xFF
            b_byte = color & 0xFF

            vertices.append({
                'v': quad_verts,  # 4 vertex positions [[x,y], ...]
                'uv': [[round(u0, 5), round(v0, 5)],
                       [round(u1, 5), round(v0, 5)],
                       [round(u1, 5), round(v1, 5)],
                       [round(u0, 5), round(v1, 5)]],
                'c': [r_byte, g_byte, b_byte, a_byte],
            })

        layer_short = layer_key.replace('Elements', '')
        positions = []
        uvs = []
        colors = []
        indices = []
        for quad in vertices:
            vertex_start = len(positions) // 2
            positions.extend(coordinate for vertex in quad['v'] for coordinate in vertex)
            uvs.extend(coordinate for uv in quad['uv'] for coordinate in uv)
            colors.extend(quad['c'] * len(quad['v']))
            indices.extend(vertex_start + offset for offset in (0, 1, 2, 0, 2, 3))
        layers[layer_short] = {
            'count': len(vertices),
            'atlasWidth': width,
            'atlasHeight': height,
            'quads': vertices,
            'positions': positions,
            'uvs': uvs,
            'colors': colors,
            'indices': indices,
        }

    raw_cells = map_data.get('cells', [])
    cell_data = [
        {
            'n': c.get('n', i),
            'm': c.get('m', 0),
            'l': c.get('l', 0),
            'f': c.get('f', 0),
            's': c.get('s', 0),
            'a': c.get('a', 0),
        }
        for i, c in enumerate(raw_cells)
    ]

    # Flat structure — no 'layers' wrapper — easier for C# minimal JSON parser
    result = {
        'schemaVersion': 2,
        'geometryMode': 'quad-approximation',
        'mapId': map_id,
        'cells': len(raw_cells),
        'cellData': cell_data,
    }
    result.update(layers)
    return result


def process_map(map_id):
    json_path = os.path.join(JSON_DIR, f"{map_id}.json")
    if not os.path.exists(json_path):
        print(f"ERROR: {json_path} not found")
        return False

    with open(json_path) as f:
        map_data = json.load(f)

    os.makedirs(OUTPUT_DIR, exist_ok=True)

    # Compose atlas PNGs
    for layer_key, atlas_key, suffix in [
        ('backgroundElements', 'backgroundAtlas', 'bg'),
        ('sortableElements', 'sortableAtlas', 'sort'),
        ('foregroundElements', 'foregroundAtlas', 'fg'),
    ]:
        atlas_info = map_data.get(atlas_key)
        elements = map_data.get(layer_key, [])
        if atlas_info and elements:
            atlas_img, placed = compose_layer_atlas(elements, atlas_info, suffix)
            if atlas_img:
                out_path = os.path.join(OUTPUT_DIR, f"{map_id}_{suffix}.png")
                atlas_img.save(out_path, optimize=True)
                size_kb = os.path.getsize(out_path) // 1024
                print(f"  {suffix}: {atlas_info['width']}x{atlas_info['height']}, {placed} sprites -> {size_kb}KB")

    # Build render data JSON
    render = build_render_data(map_data, map_id)
    render_path = os.path.join(OUTPUT_DIR, f"{map_id}_render.json")
    with open(render_path, 'w') as f:
        json.dump(render, f, separators=(',', ':'))
    layer_count = sum(key in render for key in ('background', 'sortable', 'foreground'))
    print(f"  render: {layer_count} layers, {os.path.getsize(render_path) // 1024}KB")

    return True


def main():
    if len(sys.argv) > 1 and sys.argv[1] == '--all':
        if not os.path.isdir(JSON_DIR):
            print(f"No JSON dir: {JSON_DIR}")
            return
        maps = [f.replace('.json', '') for f in os.listdir(JSON_DIR) if f.endswith('.json')]
        print(f"Processing {len(maps)} maps...")
        for mid in maps:
            print(f"Map {mid}:")
            process_map(int(mid))
    elif len(sys.argv) > 1 and sys.argv[1].isdigit():
        map_id = int(sys.argv[1])
        print(f"Map {map_id}:")
        process_map(map_id)
    else:
        print(__doc__)


if __name__ == '__main__':
    main()
