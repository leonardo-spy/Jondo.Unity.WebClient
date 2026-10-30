import sys

path = r'UnityProject/Assets/GeneratedScreenMethods.cs'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    if '{ var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }' in line:
        new_lines.append('        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }\n')
    elif '{ var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }' in line:
        new_lines.append('        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }\n')
    else:
        new_lines.append(line)

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("Updated GeneratedScreenMethods.cs successfully")

