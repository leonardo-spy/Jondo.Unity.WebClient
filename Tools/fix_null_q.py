file_path = "UnityProject/Assets/WebGLProbe.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Fix line 2804: btnSubIcon
old_btn_sub = 'var btnSubIcon = _characterHeaderRoot.Q("button_iconLeft_Vector") ?? _characterHeaderRoot.Q("btn_subscribe")?.Q(className: "icon");'
new_btn_sub = '''VisualElement btnSubIcon = _characterHeaderRoot.Q("button_iconLeft_Vector");
        if (btnSubIcon == null)
        {
            var bSub = _characterHeaderRoot.Q("btn_subscribe");
            if (bSub != null) btnSubIcon = bSub.Q(className: "icon");
        }'''

if old_btn_sub in content:
    content = content.replace(old_btn_sub, new_btn_sub, 1)
    print("1. Fixed btnSubIcon")
else:
    print("1. WARNING: btnSubIcon not matched")

# 2. Fix line 3363: iconPlus
old_icon_plus = 'var iconPlus = newCharEl?.Q("icon") ?? card.Q("plusShadowOutline");'
new_icon_plus = '''VisualElement iconPlus = null;
            if (newCharEl != null) iconPlus = newCharEl.Q("icon");
            if (iconPlus == null) iconPlus = card.Q("plusShadowOutline");'''

if old_icon_plus in content:
    content = content.replace(old_icon_plus, new_icon_plus, 1)
    print("2. Fixed iconPlus")
else:
    print("2. WARNING: iconPlus not matched")

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)
print("Saved WebGLProbe.cs!")

