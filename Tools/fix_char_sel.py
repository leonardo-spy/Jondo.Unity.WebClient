with open('UnityProject/Assets/WebGLProbe.cs', 'r', encoding='utf-8') as f:
    text = f.read()

bad = """        // Wire Play button (CharacterSelection.ValidateCharacterChoice, dump.cs L151289)
        var btnPlay = root.Q("btn_play");
        if (btnPlay != null)
        {
            if (ctrSel != null && btnPlay.parent != ctrSel) ctrSel.Add(btnPlay);
            btnPlay.SetEnabled(true);"""

good = """        // Wire Play button (CharacterSelection.ValidateCharacterChoice, dump.cs L151289)
        var btnPlay = root.Q("btn_play");
        if (btnPlay != null)
        {
            btnPlay.SetEnabled(true);"""

assert bad in text, "bad string not found"
text = text.replace(bad, good)

with open('UnityProject/Assets/WebGLProbe.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("[OK] Fixed CharacterSelection btnPlay!")

