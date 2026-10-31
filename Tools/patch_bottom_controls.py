file_path = "UnityProject/Assets/WebGLProbe.cs"

with open(file_path, "r", encoding="utf-8") as f:
    text = f.read()

# Replace cbOnly
old_cb = """        var cbOnly = root.Q("cb_onlyCharacter");
        if (cbOnly != null)
        {
            cbOnly.style.position = Position.Absolute;"""

new_cb = """        var cbOnly = root.Q("cb_onlyCharacter");
        if (cbOnly != null)
        {
            if (ctrSel != null && cbOnly.parent != ctrSel) ctrSel.Add(cbOnly);
            cbOnly.style.position = Position.Absolute;"""

# Replace btnPlay
old_play = """        var btnPlay = root.Q("btn_play");
        if (btnPlay != null)
        {
            btnPlay.SetEnabled(true);"""

new_play = """        var btnPlay = root.Q("btn_play");
        if (btnPlay != null)
        {
            if (ctrSel != null && btnPlay.parent != ctrSel) ctrSel.Add(btnPlay);
            btnPlay.SetEnabled(true);"""

assert old_cb in text, "old_cb not found!"
assert old_play in text, "old_play not found!"

text = text.replace(old_cb, new_cb)
text = text.replace(old_play, new_play)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(text)

print("[OK] Successfully reparented btn_play and cb_onlyCharacter to ctrSel!")

