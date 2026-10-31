import json

with open('LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

from dump_server_header import dump_elem_recursive, find_elem

carousel = find_elem(data[0]["root"], "ctr_carouselMode")
if carousel:
    print("=== Server Cards in Carousel Mode ===")
    for c in carousel.get("children", []):
        for sub in c.get("children", []):
            if "serverCard" in sub.get("name", ""):
                name = sub.get("name")
                layout = sub.get("layout", {})
                disp = layout.get("display")
                w, h = layout.get("width"), layout.get("height")
                l, t = layout.get("left"), layout.get("top")
                tr = sub.get("transform", {})
                op = layout.get("opacity")
                
                # Title inside card
                title_elem = find_elem(sub, "ctr_serverTitle")
                lbl_name = find_elem(title_elem, "lbl_serverName") if title_elem else None
                sname = lbl_name.get("typography", {}).get("text") if lbl_name else "?"
                
                print(f"{name}: disp={disp} | size={w}x{h} | pos=({l}, {t}) | op={op} | transform={tr}")
                
                # Check inner elements
                for card_child in sub.get("children", []):
                    # Check ctr_serverCard
                    for inner in card_child.get("children", []):
                        in_name = inner.get("name")
                        in_disp = inner.get("layout", {}).get("display")
                        in_w, in_h = inner.get("layout", {}).get("width"), inner.get("layout", {}).get("height")
                        print(f"    - {in_name}: disp={in_disp} {in_w}x{in_h}")

