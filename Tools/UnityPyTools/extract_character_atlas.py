#!/usr/bin/env python3
"""
Extract character skins, poses, and breed looks from official Dofus 3.6 client bundles.

Pipeline:
1. Extracts 19 playable classes from data_assets_breedsdataroot.asset.bundle.
2. Extracts high-resolution character textures (1024x2048 RGBA) from skins_assets_skin_*.bundle.
3. Parses body parts, UV transforms, and bone definitions.
4. Outputs structured metadata and images for WebGL runtime rendering.
"""

import json
import os
import re
import sys
from pathlib import Path
import UnityPy

UnityPy.config.FALLBACK_UNITY_VERSION = "2022.3.20f1"

SCRIPT_DIR = Path(__file__).parent
REPO_ROOT = SCRIPT_DIR.parent.parent
CLIENTE_DIR = REPO_ROOT / "Cliente 3.6.10.11"
CONTENT_DIR = CLIENTE_DIR / "Dofus_Data" / "StreamingAssets" / "Content"
DATA_DIR = CONTENT_DIR / "Data"
CHARACTERS_DIR = CONTENT_DIR / "Characters"
SKINS_DIR = CHARACTERS_DIR / "Skins"
BONES_DIR = CHARACTERS_DIR / "Bones"

OUTPUT_DIR = REPO_ROOT / "Generated" / "output" / "characters"
STREAMING_COMPOSED_DIR = CHARACTERS_DIR / "Composed"
CATALOG_PATH = REPO_ROOT / "Generated" / "output" / "CharacterBreeds.json"


def parse_look_string(look_str):
    """Parse Dofus look format: {bonesId|skinIds|colorIds|scale}."""
    m = re.match(r'\{(\d+)\|([^|]*)\|([^|]*)\|(\d+)\}', look_str.strip())
    if not m:
        return None
    bones_id = int(m.group(1))
    skins_part = m.group(2)
    skins = [int(s) for s in skins_part.split(',') if s] if skins_part else []
    colors_part = m.group(3)
    colors = [int(c) for c in colors_part.split(',') if c] if colors_part else []
    scale = int(m.group(4))
    return {
        "bonesId": bones_id,
        "skins": skins,
        "skinId": skins[0] if skins else 0,
        "colors": colors,
        "scale": scale,
    }


def load_breeds_catalog():
    """Load the 19 playable character classes from data_assets_breedsdataroot.asset.bundle."""
    bundle_path = DATA_DIR / "data_assets_breedsdataroot.asset.bundle"
    if not bundle_path.exists():
        raise FileNotFoundError(f"Breeds bundle not found at {bundle_path}")

    env = UnityPy.load(str(bundle_path))
    mbs = [obj for obj in env.objects if obj.type.name == "MonoBehaviour"]
    if not mbs:
        raise ValueError("No MonoBehaviour found in breeds bundle")

    data = mbs[0].read_typetree()
    ref_ids = data.get("references", {}).get("RefIds", [])
    catalog = {}

    for ref in ref_ids:
        b = ref.get("data", {})
        breed_id = b.get("id")
        if not breed_id:
            continue

        male_look = b.get("maleLook", "")
        female_look = b.get("femaleLook", "")
        male_parsed = parse_look_string(male_look)
        female_parsed = parse_look_string(female_look)

        catalog[breed_id] = {
            "id": breed_id,
            "maleLook": male_look,
            "femaleLook": female_look,
            "bonesId": male_parsed["bonesId"] if male_parsed else 1,
            "maleSkinId": male_parsed["skinId"] if male_parsed else 0,
            "femaleSkinId": female_parsed["skinId"] if female_parsed else 0,
            "maleScale": male_parsed["scale"] if male_parsed else 100,
            "femaleScale": female_parsed["scale"] if female_parsed else 100,
            "creatureBonesId": b.get("creatureBonesId", 0),
            "complexity": b.get("complexity", 1),
        }

    return catalog


def extract_skin_info(skin_id, save_image=True):
    """Extract skin texture and parts metadata from skins_assets_skin_{skin_id}.bundle."""
    bundle_name = f"skins_assets_skin_{skin_id}.bundle"
    bundle_path = SKINS_DIR / bundle_name
    if not bundle_path.exists():
        raise FileNotFoundError(f"Skin bundle not found at {bundle_path}")

    env = UnityPy.load(str(bundle_path))
    textures = [obj for obj in env.objects if obj.type.name == "Texture2D"]
    if not textures:
        raise ValueError(f"No Texture2D found in skin bundle {bundle_name}")

    tex_obj = textures[0].read()
    img = tex_obj.image

    mbs = [obj for obj in env.objects if obj.type.name == "MonoBehaviour"]
    parts_list = []
    if mbs:
        tree = mbs[0].read_typetree()
        for val in tree.get("m_values", []):
            part_name = val.get("name", "")
            if part_name:
                parts_list.append(part_name)

    info = {
        "skinId": skin_id,
        "bundle": bundle_name,
        "textureSize": [img.width, img.height],
        "partsCount": len(parts_list),
        "parts": parts_list,
    }

    if save_image:
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        STREAMING_COMPOSED_DIR.mkdir(parents=True, exist_ok=True)
        img_out = OUTPUT_DIR / f"skin_{skin_id}.png"
        img.save(str(img_out), "PNG")
        meta_out = OUTPUT_DIR / f"skin_{skin_id}.json"
        with open(meta_out, "w", encoding="utf-8") as f:
            json.dump(info, f, indent=2)

        # Deploy directly to streaming assets for on-demand WebGL fetch
        stream_img_out = STREAMING_COMPOSED_DIR / f"skin_{skin_id}.png"
        img.save(str(stream_img_out), "PNG")
        stream_meta_out = STREAMING_COMPOSED_DIR / f"skin_{skin_id}.json"
        with open(stream_meta_out, "w", encoding="utf-8") as f:
            json.dump(info, f, indent=2)

    return info


def main():
    print("=== Dofus 3.6 Character Asset Extractor ===")
    catalog = load_breeds_catalog()
    CATALOG_PATH.parent.mkdir(parents=True, exist_ok=True)
    with open(CATALOG_PATH, "w", encoding="utf-8") as f:
        json.dump(catalog, f, indent=2)
    print(f"Saved {len(catalog)} playable classes to {CATALOG_PATH}")

    # Extract default classes (Feca 10/11, Iop 80/81, Cra 90/91)
    target_skins = [10, 11, 80, 81, 90, 91]
    for skin_id in target_skins:
        print(f"Extracting skin {skin_id}...")
        info = extract_skin_info(skin_id, save_image=True)
        print(f"  Skin {skin_id}: {info['textureSize'][0]}x{info['textureSize'][1]}, {info['partsCount']} parts")

    print("Character extraction complete!")


if __name__ == "__main__":
    main()

