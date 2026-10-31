import json

with open('LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

from dump_server_header import find_elem

ss = find_elem(data[0]["root"], "ctr_serverSelection")
hdr = find_elem(ss, "ctr_header")
bg = find_elem(hdr, "ctr_background")

def dump_all_fields(node, name=""):
    print(f"\n==================== {name or node.get('name')} ({node.get('type')}) ====================")
    for k, v in node.items():
        if k != "children":
            print(f"  {k}: {json.dumps(v, indent=4)}")

dump_all_fields(bg, "ctr_background")
for idx, c in enumerate(bg.get("children", [])):
    dump_all_fields(c, f"ctr_background -> Child [{idx}] {c.get('name')}")
    if c.get("name") == "ctr_tabSection":
        for s_idx, sc in enumerate(c.get("children", [])):
            dump_all_fields(sc, f"  tabSection -> Child [{s_idx}] {sc.get('name')}")
            for ssc_idx, ssc in enumerate(sc.get("children", [])):
                dump_all_fields(ssc, f"    cornerContainer -> Child [{ssc_idx}] {ssc.get('name')}")

