# -*- coding: utf-8 -*-
from PIL import Image
import os
import shutil

brain_dir = r"C:\Users\Leona\.gemini\antigravity\brain\1a870abb-bfe8-4a17-bba7-05cc00341d90"
screenshots_dir = "Screenshots"

# Copy screenshots to brain dir
for fname in ["01_login_screen.png", "02_server_selection.png", "03_character_selection.png", "04_world_map_hud.png"]:
    src = os.path.join(screenshots_dir, fname)
    dst = os.path.join(brain_dir, fname)
    if os.path.exists(src):
        shutil.copy2(src, dst)
        print(f"Copied {fname} to brain directory.")

# Crop 02_server_selection
s2_path = os.path.join(brain_dir, "02_server_selection.png")
if os.path.exists(s2_path):
    img = Image.open(s2_path)
    w, h = img.size
    print(f"ServerSelection size: {w}x{h}")
    # Header: Y 0 to 180
    img.crop((0, 0, w, min(180, h))).save(os.path.join(brain_dir, "crop_p4_header.png"))
    # Cards: Y 120 to 680
    img.crop((0, min(120, h), w, min(680, h))).save(os.path.join(brain_dir, "crop_p4_cards.png"))
    # Bottom: Y 680 to h
    img.crop((0, min(680, h), w, h)).save(os.path.join(brain_dir, "crop_p4_bottom.png"))
    print("Saved crop_p4_header, crop_p4_cards, crop_p4_bottom")

# Crop 03_character_selection
s3_path = os.path.join(brain_dir, "03_character_selection.png")
if os.path.exists(s3_path):
    img = Image.open(s3_path)
    w, h = img.size
    print(f"CharacterSelection size: {w}x{h}")
    img.crop((0, 0, w, min(200, h))).save(os.path.join(brain_dir, "crop_p4_char_header.png"))
    img.crop((0, min(200, h), w, min(700, h))).save(os.path.join(brain_dir, "crop_p4_char_center.png"))
    img.crop((0, min(700, h), w, h)).save(os.path.join(brain_dir, "crop_p4_char_bottom.png"))
    print("Saved crop_p4_char_header, crop_p4_char_center, crop_p4_char_bottom")

