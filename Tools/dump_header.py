import json
import os

def dump_elem_recursive(elem, depth=0):
    indent = "  " * depth
    name = elem.get("name") or "(unnamed)"
    etype = elem.get("type")
    layout = elem.get("layout", {})
    borders = elem.get("borders", {})
    bg = elem.get("background", {})
    typo = elem.get("typography", {})
    classes = elem.get("classes", [])
    
    w = layout.get("width")
    h = layout.get("height")
    pos = layout.get("position")
    disp = layout.get("display")
    flex_dir = layout.get("flexDirection")
    align_items = layout.get("alignItems")
    justify = layout.get("justifyContent")
    left = layout.get("left")
    top = layout.get("top")
    right = layout.get("right")
    bottom = layout.get("bottom")
    m_top = layout.get("marginTop")
    m_bottom = layout.get("marginBottom")
    m_left = layout.get("marginLeft")
    m_right = layout.get("marginRight")
    p_top = layout.get("paddingTop")
    p_bottom = layout.get("paddingBottom")
    p_left = layout.get("paddingLeft")
    p_right = layout.get("paddingRight")
    
    color = typo.get("color")
    font_size = typo.get("fontSize")
    
    bg_color = bg.get("color")
    slice_l = bg.get("sliceLeft")
    slice_r = bg.get("sliceRight")
    slice_t = bg.get("sliceTop")
    slice_b = bg.get("sliceBottom")
    
    border_w = (borders.get("leftWidth"), borders.get("topWidth"), borders.get("rightWidth"), borders.get("bottomWidth"))
    border_r = (borders.get("topLeftRadius"), borders.get("topRightRadius"), borders.get("bottomRightRadius"), borders.get("bottomLeftRadius"))
    border_c = borders.get("leftColor")
    
    print(f"{indent}* {name} <{etype}> cls={classes}")
    print(f"{indent}  Size: {w:.1f}x{h:.1f} | Pos: {pos} (L:{left}, T:{top}, R:{right}, B:{bottom}) | Disp: {disp}")
    print(f"{indent}  Flex: dir={flex_dir}, align={align_items}, justify={justify}")
    if any(x != 0 for x in [m_top, m_bottom, m_left, m_right]):
        print(f"{indent}  Margin: T:{m_top}, B:{m_bottom}, L:{m_left}, R:{m_right}")
    if any(x != 0 for x in [p_top, p_bottom, p_left, p_right]):
        print(f"{indent}  Padding: T:{p_top}, B:{p_bottom}, L:{p_left}, R:{p_right}")
    if bg_color and bg_color != "rgba(0,0,0,0.00)":
        print(f"{indent}  BgColor: {bg_color}")
    if any(x != 0 for x in [slice_l, slice_r, slice_t, slice_b]):
        print(f"{indent}  9-Slice: L:{slice_l}, R:{slice_r}, T:{slice_t}, B:{slice_b}")
    if any(x != 0 for x in border_w):
        print(f"{indent}  BorderWidth: {border_w}, Color: {border_c}")
    if any(x != 0 for x in border_r):
        print(f"{indent}  BorderRadius: {border_r}")
    if font_size:
        print(f"{indent}  Typo: size={font_size}, color={color}")
        
    for c in elem.get("children", []):
        dump_elem_recursive(c, depth + 1)

with open('LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

def find(n, name):
    if n.get("name") == name:
        return n
    for c in n.get("children", []):
        r = find(c, name)
        if r:
            return r
    return None

header = find(data[0]["root"], "ctr_header")
if header:
    dump_elem_recursive(header)

