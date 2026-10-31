import json

with open('LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data_carousel = json.load(f)

with open('LiveDofusDump/Dump_20261031_152414_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data_list = json.load(f)

def find_elem(n, name):
    if n.get("name") == name:
        return n
    for c in n.get("children", []):
        r = find_elem(c, name)
        if r:
            return r
    return None

def summarize_children(elem, depth=0, max_depth=3):
    indent = "  " * depth
    name = elem.get("name") or "(unnamed)"
    etype = elem.get("type")
    layout = elem.get("layout", {})
    w, h = layout.get("width", 0), layout.get("height", 0)
    pos = layout.get("position")
    disp = layout.get("display")
    flex_dir = layout.get("flexDirection")
    align = layout.get("alignItems")
    justify = layout.get("justifyContent")
    classes = elem.get("classes", [])
    cls_str = f"[{', '.join(classes)}]" if classes else ""
    
    print(f"{indent}- {name} ({etype}) {cls_str} | {w:.1f}x{h:.1f} | pos={pos} disp={disp} dir={flex_dir} align={align} just={justify}")
    if depth < max_depth:
        for c in elem.get("children", []):
            summarize_children(c, depth + 1, max_depth)

print("=== CAROUSEL MODE ===")
c_elem = find_elem(data_carousel[0]["root"], "ctr_carouselMode")
if c_elem:
    summarize_children(c_elem, 0, 4)

print("\n=== LIST MODE ===")
l_elem = find_elem(data_list[0]["root"], "ctr_listMode")
if l_elem:
    summarize_children(l_elem, 0, 4)

print("\n=== PLAY BUTTON ===")
p_elem = find_elem(data_carousel[0]["root"], "btn_play")
if p_elem:
    summarize_children(p_elem, 0, 4)

