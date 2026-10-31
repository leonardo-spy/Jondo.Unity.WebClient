import json

with open('LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

from dump_server_header import find_elem

ch = find_elem(data[0]["root"], "CharacterHeader")
ch_c = find_elem(ch, "CharacterHeader_Container")

for idx, c in enumerate(ch_c.get("children", [])):
    print(f"Child [{idx}]: {c.get('name')} ({c.get('type')}) | layout={c.get('layout')}")
    for sub in c.get("children", []):
        print(f"   -> {sub.get('name')} ({sub.get('type')}) | layout={sub.get('layout')}")

