from PIL import Image
import os
import numpy as np

brain_dir = r"C:\Users\Leona\.gemini\antigravity\brain\1a870abb-bfe8-4a17-bba7-05cc00341d90"

img3_path = os.path.join(brain_dir, "03_character_selection.png")
if os.path.exists(img3_path):
    img3 = Image.open(img3_path)
    w, h = img3.size
    print(f"03_character_selection size: {w}x{h}")
    # Save a thumbnail or crop
    crop_center = img3.crop((int(w*0.1), int(h*0.1), int(w*0.9), int(h*0.9)))
    crop_center.save(os.path.join(brain_dir, "crop_p3_char_sel.png"))

img4_path = os.path.join(brain_dir, "04_world_map_hud.png")
if os.path.exists(img4_path):
    img4 = Image.open(img4_path)
    w, h = img4.size
    print(f"04_world_map_hud size: {w}x{h}")
    crop_map = img4.crop((int(w*0.2), int(h*0.2), int(w*0.8), int(h*0.8)))
    crop_map.save(os.path.join(brain_dir, "crop_p3_map4.png"))

    img2 = Image.open(os.path.join(brain_dir, "02_server_selection.png"))
    diff_2_3 = np.mean(np.abs(np.array(img3, dtype=int) - np.array(img2, dtype=int)))
    diff_2_4 = np.mean(np.abs(np.array(img4, dtype=int) - np.array(img2, dtype=int)))
    print(f"Mean pixel diff (03 vs 02): {diff_2_3:.2f}")
    print(f"Mean pixel diff (04 vs 02): {diff_2_4:.2f}")

