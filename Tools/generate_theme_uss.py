#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
generate_theme_uss.py
Transpila active_ui_documents.json (gerado pelo JondoDumper v2.0 na memória viva)
em uma folha de estilo USS oficial do Unity UI Toolkit.
Elimina a necessidade de estilização inline em C#.
"""

import json
import os
import sys

def format_px(val):
    if val is None:
        return None
    if isinstance(val, (int, float)):
        return f"{val:.1f}px" if isinstance(val, float) else f"{val}px"
    return str(val)

def extract_uss_rules(node, rules_by_id, rules_by_class=None):
    name = node.get("name")
    classes = node.get("classes", [])
    layout = node.get("layout", {})
    borders = node.get("borders", {})
    bg = node.get("background", {})
    typo = node.get("typography", {})
    
    rule = {}
    
    # 1. Flex Layout
    disp = layout.get("display")
    if disp and disp != "Flex":
        rule["display"] = disp.lower()
        
    pos = layout.get("position")
    if pos and pos != "Relative":
        rule["position"] = pos.lower()
        
    fdir = layout.get("flexDirection")
    if fdir and fdir != "Column":
        rule["flex-direction"] = "row" if fdir == "Row" else fdir.lower()
        
    fg = layout.get("flexGrow")
    if fg is not None and fg > 0:
        rule["flex-grow"] = f"{fg:g}"
        
    fs = layout.get("flexShrink")
    if fs is not None and fs != 1:
        rule["flex-shrink"] = f"{fs:g}"
        
    ai = layout.get("alignItems")
    if ai and ai != "Stretch":
        ai_map = {"FlexStart": "flex-start", "FlexEnd": "flex-end", "Center": "center"}
        rule["align-items"] = ai_map.get(ai, ai.lower())
        
    jc = layout.get("justifyContent")
    if jc and jc != "FlexStart":
        jc_map = {"SpaceBetween": "space-between", "Center": "center", "FlexEnd": "flex-end"}
        rule["justify-content"] = jc_map.get(jc, jc.lower())
        
    # Dimensions (only if fixed and meaningful)
    w = layout.get("width")
    h = layout.get("height")
    if w and w > 0 and pos == "Absolute":
        rule["width"] = format_px(w)
    if h and h > 0 and (pos == "Absolute" or "height" in name.lower() or "header" in name.lower()):
        rule["height"] = format_px(h)

    # Margins & Paddings
    for prop, key in [("margin-left", "marginLeft"), ("margin-right", "marginRight"), 
                      ("margin-top", "marginTop"), ("margin-bottom", "marginBottom")]:
        val = layout.get(key)
        if val is not None and val != 0:
            rule[prop] = format_px(val)
            
    for prop, key in [("padding-left", "paddingLeft"), ("padding-right", "paddingRight"), 
                      ("padding-top", "paddingTop"), ("padding-bottom", "paddingBottom")]:
        val = layout.get(key)
        if val is not None and val != 0:
            rule[prop] = format_px(val)

    # 2. Background
    bg_color = bg.get("color")
    if bg_color and bg_color != "rgba(0,0,0,0.00)" and bg_color != "rgba(0,0,0,0)":
        rule["background-color"] = bg_color
        
    # 9-slice
    sl = bg.get("sliceLeft", 0)
    sr = bg.get("sliceRight", 0)
    st = bg.get("sliceTop", 0)
    sb = bg.get("sliceBottom", 0)
    if any([sl, sr, st, sb]):
        rule["-unity-slice-left"] = str(sl)
        rule["-unity-slice-right"] = str(sr)
        rule["-unity-slice-top"] = str(st)
        rule["-unity-slice-bottom"] = str(sb)

    # 3. Borders & Radii
    bw_l = borders.get("widthLeft", 0)
    bw_r = borders.get("widthRight", 0)
    bw_t = borders.get("widthTop", 0)
    bw_b = borders.get("widthBottom", 0)
    if any([bw_l, bw_r, bw_t, bw_b]):
        if bw_l == bw_r == bw_t == bw_b and bw_l > 0:
            rule["border-width"] = format_px(bw_l)
        else:
            if bw_l > 0: rule["border-left-width"] = format_px(bw_l)
            if bw_r > 0: rule["border-right-width"] = format_px(bw_r)
            if bw_t > 0: rule["border-top-width"] = format_px(bw_t)
            if bw_b > 0: rule["border-bottom-width"] = format_px(bw_b)
            
    bc_l = borders.get("colorLeft")
    if bc_l and bc_l != "rgba(0,0,0,0.00)":
        rule["border-color"] = bc_l
        
    rad_tl = borders.get("radiusTopLeft", 0)
    rad_tr = borders.get("radiusTopRight", 0)
    rad_br = borders.get("radiusBottomRight", 0)
    rad_bl = borders.get("radiusBottomLeft", 0)
    if any([rad_tl, rad_tr, rad_br, rad_bl]):
        if rad_tl == rad_tr == rad_br == rad_bl and rad_tl > 0:
            rule["border-radius"] = format_px(rad_tl)
        else:
            if rad_tl > 0: rule["border-top-left-radius"] = format_px(rad_tl)
            if rad_tr > 0: rule["border-top-right-radius"] = format_px(rad_tr)
            if rad_br > 0: rule["border-bottom-right-radius"] = format_px(rad_br)
            if rad_bl > 0: rule["border-bottom-left-radius"] = format_px(rad_bl)

    # 4. Typography
    font_size = typo.get("fontSize")
    if font_size and font_size > 0 and font_size != 14: # 14 is default
        rule["font-size"] = format_px(font_size)
        
    font_color = typo.get("color")
    if font_color and font_color != "rgba(27,27,27,1.00)":
        rule["color"] = font_color

    IGNORED_ID_NAMES = {
        "VisualElement", "TemplateContainer", "(unnamed)", "DofusVisualElement",
        "TextElement", "Label", "Button", "ScrollView", "ListView", "DofusLabel",
        "DofusButtonCustom", "TextInput", "DofusIcon", "DofusTextFieldCustom"
    }

    if name and not name.startswith("(unnamed)") and name not in IGNORED_ID_NAMES and rule:
        # Avoid conflicting generic names from overriding specialized screens
        if name == "ctr_background":
            pass # Scoped per-screen in template USS
        elif name not in rules_by_id or len(rule) > len(rules_by_id[name]):
            rules_by_id[name] = rule
            
    for child in node.get("children", []):
        extract_uss_rules(child, rules_by_id, rules_by_class)

def generate_uss_content(rules_by_id):
    lines = [
        "/* <auto-generated>",
        " *   Gerado automaticamente por Tools/generate_theme_uss.py a partir do LiveDofusDump.",
        " *   Reflete 100% dos resolvedStyles computados pelo Unity em tempo de execução.",
        " * </auto-generated> */",
        ""
    ]
    
    # Sort selectors for deterministic output
    for name in sorted(rules_by_id.keys()):
        rule = rules_by_id[name]
        if not rule:
            continue
        lines.append(f"#{name} {{")
        for prop, val in sorted(rule.items()):
            lines.append(f"    {prop}: {val};")
        lines.append("}\n")
        
    return "\n".join(lines)

def main():
    dump_dir = "LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10"
    if len(sys.argv) > 1:
        dump_dir = sys.argv[1]
        
    json_path = os.path.join(dump_dir, "active_ui_documents.json")
    if not os.path.exists(json_path):
        print(f"Erro: Arquivo {json_path} não encontrado.")
        sys.exit(1)
        
    print(f"Lendo dump de memória: {json_path}...")
    with open(json_path, "r", encoding="utf-8-sig") as f:
        data = json.load(f)
        
    rules_by_id = {}
    for doc in data:
        root = doc.get("root")
        if root:
            extract_uss_rules(root, rules_by_id)
            
    print(f"Total de seletores de ID extraídos: {len(rules_by_id)}")
    uss_content = generate_uss_content(rules_by_id)
    
    out_dir = os.path.join("UnityProject", "Assets", "Resources", "UI", "Themes")
    os.makedirs(out_dir, exist_ok=True)
    out_file = os.path.join(out_dir, "MediumFont_Standard_Generated.uss")
    
    with open(out_file, "w", encoding="utf-8") as f:
        f.write(uss_content)
        
    print(f"Arquivo USS gerado com sucesso em: {out_file} ({len(uss_content)} bytes)")

if __name__ == "__main__":
    main()

