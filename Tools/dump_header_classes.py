import json

with open('LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

from dump_server_header import find_elem

def inspect_bg_and_classes(elem, depth=0):
    indent = "  " * depth
    name = elem.get("name") or "(unnamed)"
    etype = elem.get("type")
    classes = elem.get("classes", [])
    bg = elem.get("background", {})
    borders = elem.get("borders", {})
    layout = elem.get("layout", {})
    
    cls_str = f" classes={classes}" if classes else ""
    bg_color = bg.get("color")
    bg_c_str = f" bg={bg_color}" if bg_color and bg_color != "rgba(0,0,0,0.00)" else ""
    
    # 9-slice
    slice_l = bg.get("sliceLeft")
    slice_r = bg.get("sliceRight")
    slice_t = bg.get("sliceTop")
    slice_b = bg.get("sliceBottom")
    slice_str = f" 9slice=({slice_l},{slice_r},{slice_t},{slice_b})" if any([slice_l, slice_r, slice_t, slice_b]) else ""
    
    w, h = layout.get("width"), layout.get("height")
    
    print(f"{indent}- {name} ({etype}){cls_str} [{w:.1f}x{h:.1f}]{bg_c_str}{slice_str}")
    for c in elem.get("children", []):
        inspect_bg_and_classes(c, depth + 1)

ss = find_elem(data[0]["root"], "ctr_serverSelection")
if ss:
    hdr = find_elem(ss, "ctr_header")
    if hdr:
        print("=== Detailed bg & classes for ctr_header ===")
        inspect_bg_and_classes(hdr)

