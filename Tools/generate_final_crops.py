from PIL import Image
import os

brain_dir = r"C:\Users\Leona\.gemini\antigravity\brain\1a870abb-bfe8-4a17-bba7-05cc00341d90"

# 1. Server Selection crops
s2_path = os.path.join(brain_dir, "02_server_selection.png")
if os.path.exists(s2_path):
    img = Image.open(s2_path)
    w, h = img.size
    img.crop((0, 0, w, 85)).save(os.path.join(brain_dir, "crop_p3_header_final.png"))
    img.crop((0, 120, w, 680)).save(os.path.join(brain_dir, "crop_p3_cards_final.png"))
    img.crop((0, 680, w, h)).save(os.path.join(brain_dir, "crop_p3_bottom_final.png"))
    print("02_server_selection crops saved.")

# 2. Character Selection crop
s3_path = os.path.join(brain_dir, "03_character_selection.png")
if os.path.exists(s3_path):
    img = Image.open(s3_path)
    w, h = img.size
    img.crop((int(w * 0.05), int(h * 0.05), int(w * 0.95), int(h * 0.95))).save(os.path.join(brain_dir, "crop_p3_charsel_final.png"))
    print("03_character_selection crop saved.")

# 3. World Map & HUD crops
s4_path = os.path.join(brain_dir, "04_world_map_hud.png")
if os.path.exists(s4_path):
    img = Image.open(s4_path)
    w, h = img.size
    # Center map (w: 25% to 75%, h: 20% to 75%)
    img.crop((int(w * 0.25), int(h * 0.20), int(w * 0.75), int(h * 0.75))).save(os.path.join(brain_dir, "crop_p3_map_center_final.png"))
    # Top HUD
    img.crop((0, 0, w, 120)).save(os.path.join(brain_dir, "crop_p3_top_hud_final.png"))
    # Bottom HUD
    img.crop((0, h - 160, w, h)).save(os.path.join(brain_dir, "crop_p3_bottom_hud_final.png"))
    print("04_world_map_hud crops saved.")
