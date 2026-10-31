from PIL import Image
import os

brain_dir = r"C:\Users\Leona\.gemini\antigravity\brain\1a870abb-bfe8-4a17-bba7-05cc00341d90"

# 1. 02_server_selection crops
img02_path = os.path.join(brain_dir, "02_server_selection.png")
if os.path.exists(img02_path):
    img02 = Image.open(img02_path)
    w, h = img02.size
    print(f"02_server_selection size: {w}x{h}")

    # Top header (y: 0 to 80)
    crop_header = img02.crop((0, 0, w, 80))
    crop_header.save(os.path.join(brain_dir, "crop_p3_header.png"))

    # Cards carousel (y: 120 to 650)
    crop_cards = img02.crop((0, 120, w, 650))
    crop_cards.save(os.path.join(brain_dir, "crop_p3_cards.png"))

    # Bottom controls & Play button (y: 650 to h)
    crop_bottom = img02.crop((0, 650, w, h))
    crop_bottom.save(os.path.join(brain_dir, "crop_p3_bottom.png"))
    print("02_server_selection crops saved successfully.")

# 2. 04_world_map_hud crops
img04_path = os.path.join(brain_dir, "04_world_map_hud.png")
if os.path.exists(img04_path):
    img04 = Image.open(img04_path)
    w, h = img04.size
    print(f"04_world_map_hud size: {w}x{h}")

    # Center map (w*0.2 to w*0.8, h*0.2 to h*0.8)
    crop_map = img04.crop((int(w * 0.2), int(h * 0.2), int(w * 0.8), int(h * 0.8)))
    crop_map.save(os.path.join(brain_dir, "crop_p3_map_center.png"))

    # Bottom HUD (chat & action bar: y: h - 180 to h)
    crop_bottom_hud = img04.crop((0, h - 180, w, h))
    crop_bottom_hud.save(os.path.join(brain_dir, "crop_p3_bottom_hud.png"))

    # Top HUD (character info & banner menu: y: 0 to 120)
    crop_top_hud = img04.crop((0, 0, w, 120))
    crop_top_hud.save(os.path.join(brain_dir, "crop_p3_top_hud.png"))
    print("04_world_map_hud crops saved successfully.")

