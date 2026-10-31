path = 'UnityProject/Assets/WebGLProbe.cs'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
in_set_login = False
in_char_sel = False

for line in lines:
    if 'void PopulateCharacterSelection(' in line:
        in_char_sel = True
    elif in_char_sel and ('void ' in line or 'IEnumerator ' in line):
        in_char_sel = False

    if 'void SetLoginState(' in line:
        in_set_login = True
    elif in_set_login and ('void ' in line or 'IEnumerator ' in line):
        in_set_login = False

    if in_char_sel and '_serverSelectionBinding?.btn_play' in line:
        line = line.replace('_serverSelectionBinding?.btn_play', '_charSelectionBinding?.btn_play')
    elif in_set_login and '_serverSelectionBinding?.btn_play' in line:
        line = line.replace('_serverSelectionBinding?.btn_play', '_loginBinding?.btn_play')

    new_lines.append(line)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.writelines(new_lines)

print("Replacement complete.")

