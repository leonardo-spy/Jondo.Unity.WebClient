import json

with open('LiveDofusDump/Dump_20261031_152414_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

from dump_server_header import dump_elem_recursive, find_elem

lm = find_elem(data[0]["root"], "ctr_listMode")
if lm:
    print("=== ctr_listMode in List Mode ===")
    dump_elem_recursive(lm, 0, 5)

