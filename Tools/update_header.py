import sys

file_path = "UnityProject/Assets/WebGLProbe.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update MountCharacterHeader
old_header_target = """        // Top-Right Utility Bar (5 circular buttons: Settings, Fullscreen, Sound, Log out, Menu)
        var topActions = new VisualElement();
        topActions.name = "TopRightUtilityBar";
        topActions.style.flexDirection = FlexDirection.Row;
        topActions.style.alignItems = Align.Center;
        topActions.style.position = Position.Absolute;
        topActions.style.top = 2;
        topActions.style.right = 0;

        string[] actionIcons = { "cog", "expand", "loudspeakerOn", "shutDown", "menu" };
        foreach (var act in actionIcons)
        {
            var btn = new Button();
            btn.name = $"btn_top_{act}";
            btn.style.width = 34;
            btn.style.height = 34;
            btn.style.marginLeft = 8;
            btn.style.borderTopLeftRadius = 17;
            btn.style.borderTopRightRadius = 17;
            btn.style.borderBottomLeftRadius = 17;
            btn.style.borderBottomRightRadius = 17;
            btn.style.backgroundColor = new Color(0.12f, 0.14f, 0.17f, 0.95f);
            btn.style.borderLeftColor = new Color(0.25f, 0.28f, 0.32f, 1f);
            btn.style.borderRightColor = new Color(0.25f, 0.28f, 0.32f, 1f);
            btn.style.borderTopColor = new Color(0.25f, 0.28f, 0.32f, 1f);
            btn.style.borderBottomColor = new Color(0.25f, 0.28f, 0.32f, 1f);
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.justifyContent = Justify.Center;
            btn.style.alignItems = Align.Center;

            var actTex = Resources.Load<Texture2D>($"FigmaIcons/{act}");
            if (actTex != null)
            {
                var iconImg = new VisualElement();
                iconImg.style.backgroundImage = new StyleBackground(actTex);
                iconImg.style.width = 18;
                iconImg.style.height = 18;
                btn.Add(iconImg);
            }
            topActions.Add(btn);
        }

        _characterHeaderRoot.Add(topActions);"""

new_header_replacement = """        // Authentic Top-Right GameMenu (bug, options/cog, quit/menu) from live dump
        var topActions = new VisualElement();
        topActions.name = "GameMenu_Overlay";
        topActions.style.flexDirection = FlexDirection.Row;
        topActions.style.alignItems = Align.Center;
        topActions.style.position = Position.Absolute;
        topActions.style.top = 4;
        topActions.style.right = 0;

        var menuBg = new VisualElement();
        menuBg.name = "ctr_bg";
        menuBg.style.flexDirection = FlexDirection.Row;
        menuBg.style.alignItems = Align.Center;
        menuBg.style.backgroundColor = new Color(0.1f, 0.12f, 0.15f, 0.9f);
        menuBg.style.borderTopLeftRadius = 16;
        menuBg.style.borderTopRightRadius = 16;
        menuBg.style.borderBottomLeftRadius = 16;
        menuBg.style.borderBottomRightRadius = 16;
        menuBg.style.paddingLeft = 4;
        menuBg.style.paddingRight = 4;
        menuBg.style.paddingTop = 2;
        menuBg.style.paddingBottom = 2;

        (string act, string iconName)[] menuButtons = {
            ("bug", "bugShadowOutline"),
            ("options", "cogShadowOutline"),
            ("menu", "shutDownShadowOutline")
        };

        foreach (var (act, iconName) in menuButtons)
        {
            var btn = new Button();
            btn.name = $"btn_{act}";
            btn.style.width = 32;
            btn.style.height = 32;
            btn.style.marginLeft = 4;
            btn.style.marginRight = 4;
            btn.style.borderTopLeftRadius = 16;
            btn.style.borderTopRightRadius = 16;
            btn.style.borderBottomLeftRadius = 16;
            btn.style.borderBottomRightRadius = 16;
            btn.style.backgroundColor = new Color(0.16f, 0.18f, 0.22f, 0.95f);
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.justifyContent = Justify.Center;
            btn.style.alignItems = Align.Center;

            var actTex = Resources.Load<Texture2D>($"UI/Icons/{iconName}") ?? Resources.Load<Texture2D>($"FigmaIcons/{act}");
            if (actTex != null)
            {
                var iconImg = new VisualElement();
                iconImg.style.backgroundImage = new StyleBackground(actTex);
                iconImg.style.width = 20;
                iconImg.style.height = 20;
                btn.Add(iconImg);
            }
            menuBg.Add(btn);
        }

        topActions.Add(menuBg);
        _characterHeaderRoot.Add(topActions);"""

if old_header_target in content:
    content = content.replace(old_header_target, new_header_replacement, 1)
    print("1. Replaced Top-Right menu successfully")
else:
    print("1. WARNING: old_header_target not matched")

# 2. Update character header icons
old_icons_target = """        // Set character avatar icon
        var charIcon = _characterHeaderRoot.Q("icon_character");
        var texChar = Resources.Load<Texture2D>("FigmaIcons/character");
        if (charIcon != null && texChar != null)
        {
            charIcon.style.backgroundImage = new StyleBackground(texChar);
            charIcon.style.width = 34;
            charIcon.style.height = 34;
        }

        // Set subscription icon
        var subIcon = _characterHeaderRoot.Q("icon_subscription");
        var texSub = Resources.Load<Texture2D>("FigmaIcons/subscription");
        if (subIcon != null && texSub != null)
        {
            subIcon.style.backgroundImage = new StyleBackground(texSub);
            subIcon.style.width = 18;
            subIcon.style.height = 18;
        }"""

new_icons_replacement = """        // Set character avatar icon from live dump
        var charIcon = _characterHeaderRoot.Q("icon_character");
        var texChar = Resources.Load<Texture2D>("UI/Icons/playerShadowOutline") ?? Resources.Load<Texture2D>("FigmaIcons/character");
        if (charIcon != null && texChar != null)
        {
            charIcon.style.backgroundImage = new StyleBackground(texChar);
            charIcon.style.width = 28;
            charIcon.style.height = 28;
        }

        // Set subscription icon from live dump
        var subIcon = _characterHeaderRoot.Q("icon_subscription");
        var texSub = Resources.Load<Texture2D>("UI/Icons/ticketShadowOutline") ?? Resources.Load<Texture2D>("FigmaIcons/subscription");
        if (subIcon != null && texSub != null)
        {
            subIcon.style.backgroundImage = new StyleBackground(texSub);
            subIcon.style.width = 20;
            subIcon.style.height = 20;
        }

        var btnSubIcon = _characterHeaderRoot.Q("button_iconLeft_Vector") ?? _characterHeaderRoot.Q("btn_subscribe")?.Q(className: "icon");
        var texExt = Resources.Load<Texture2D>("UI/Icons/externalShadowOutline");
        if (btnSubIcon != null && texExt != null)
        {
            btnSubIcon.style.backgroundImage = new StyleBackground(texExt);
            btnSubIcon.style.width = 16;
            btnSubIcon.style.height = 16;
        }"""

if old_icons_target in content:
    content = content.replace(old_icons_target, new_icons_replacement, 1)
    print("2. Replaced character header icons successfully")
else:
    print("2. WARNING: old_icons_target not matched")

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)
print("Updated WebGLProbe.cs successfully!")

