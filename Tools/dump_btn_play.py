import json

with open('LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10/active_ui_documents.json', 'r', encoding='utf-8-sig') as f:
    data = json.load(f)

from dump_server_header import dump_elem_recursive, find_elem

ss = find_elem(data[0]["root"], "ctr_serverSelection")
if ss:
    btn_play = find_elem(ss, "btn_play")
    if btn_play:
        print("=== btn_play in ServerSelection ===")
        dump_elem_recursive(btn_play, 0, 4)

