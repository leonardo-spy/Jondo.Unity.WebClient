import json
import os

dumps = [d for d in sorted(os.listdir('LiveDofusDump')) if '20261031' in d and os.path.exists(os.path.join('LiveDofusDump', d, 'active_ui_documents.json'))]

def find_elem(node, name):
    if node.get("name") == name:
        return node
    for c in node.get("children", []):
        r = find_elem(c, name)
        if r:
            return r
    return None

for d in dumps:
    p = os.path.join('LiveDofusDump', d, 'active_ui_documents.json')
    with open(p, 'r', encoding='utf-8-sig') as f:
        data = json.load(f)
    root = data[0]["root"]
    
    server_sel = find_elem(root, "ServerSelection")
    char_sel = find_elem(root, "CharacterSelection")
    carousel = find_elem(root, "ctr_carouselMode")
    list_mode = find_elem(root, "ctr_listMode")
    
    c_disp = carousel.get("layout", {}).get("display") if carousel else "N/A"
    l_disp = list_mode.get("layout", {}).get("display") if list_mode else "N/A"
    ss_disp = server_sel.get("layout", {}).get("display") if server_sel else "N/A"
    cs_disp = char_sel.get("layout", {}).get("display") if char_sel else "N/A"
    
    print(f"{d}:")
    print(f"  ServerSel: {ss_disp} | CharSel: {cs_disp} | Carousel: {c_disp} | ListMode: {l_disp}")

