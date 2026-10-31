import json

with open('LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

from dump_server_header import dump_elem_recursive, find_elem

ch = find_elem(data[0]["root"], "CharacterHeader")
if ch:
    print("=== CharacterHeader ===")
    dump_elem_recursive(ch, 0, 5)

