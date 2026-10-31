import sys

file_path = "UnityProject/Assets/WebGLProbe.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update PlayButton
old_play_target = """        var btnPlay = root.Q("btn_play");
        if (btnPlay != null)
        {
            btnPlay.SetEnabled(true);
            btnPlay.RemoveFromClassList("disabled");
            btnPlay.pickingMode = PickingMode.Position;
            btnPlay.style.position = Position.Absolute;
            btnPlay.style.bottom = 30;
            btnPlay.style.left = 830;
            btnPlay.style.width = 260;
            btnPlay.style.height = 56;
            btnPlay.style.backgroundColor = new Color(0.13f, 0.75f, 0.35f, 1f);
            btnPlay.style.borderLeftColor = new Color(0.95f, 0.77f, 0.25f, 1f);
            btnPlay.style.borderRightColor = new Color(0.95f, 0.77f, 0.25f, 1f);
            btnPlay.style.borderTopColor = new Color(0.95f, 0.77f, 0.25f, 1f);
            btnPlay.style.borderBottomColor = new Color(0.95f, 0.77f, 0.25f, 1f);
            btnPlay.style.borderLeftWidth = 2;
            btnPlay.style.borderRightWidth = 2;
            btnPlay.style.borderTopWidth = 2;
            btnPlay.style.borderBottomWidth = 2;
            btnPlay.style.borderTopLeftRadius = 10;
            btnPlay.style.borderTopRightRadius = 10;
            btnPlay.style.borderBottomLeftRadius = 10;
            btnPlay.style.borderBottomRightRadius = 10;
            btnPlay.style.justifyContent = Justify.Center;
            btnPlay.style.alignItems = Align.Center;

            Action triggerServerPlay = () =>
            {
                int targetId = _pendingServerId > 0 ? _pendingServerId : 290;
                if (_wsConn != null)
                {
                    Log($"  Server select: {targetId}");
                    SetLoginState(root, 1);
                    var selectFrame = WebSocketBridge.BuildServerSelectFrame(targetId, "en");
                    WebSocketBridge.Send(_wsConn, selectFrame);
                }
            };

            btnPlay.Query().ForEach(child =>
            {
                if (child != btnPlay)
                {
                    child.pickingMode = PickingMode.Ignore;
                    child.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); triggerServerPlay(); });
                    child.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) { evt.StopPropagation(); triggerServerPlay(); } });
                }
            });

            if (btnPlay is Button ssBtn) ssBtn.clicked += () => triggerServerPlay();
            btnPlay.RegisterCallback<ClickEvent>(evt => triggerServerPlay());
            btnPlay.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) triggerServerPlay(); });

            var ssLabel = btnPlay.Q("playButton_label") as Label ?? btnPlay.Q<Label>();
            if (ssLabel != null)
            {
                ssLabel.text = "JUGAR";
                ssLabel.style.color = Color.white;
                ssLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                ssLabel.style.fontSize = 22;
            }
        }"""

new_play_replacement = """        var btnPlay = root.Q("btn_play");
        if (btnPlay != null)
        {
            btnPlay.SetEnabled(true);
            btnPlay.RemoveFromClassList("disabled");
            btnPlay.pickingMode = PickingMode.Position;
            btnPlay.style.position = Position.Absolute;
            btnPlay.style.bottom = 20;
            btnPlay.style.left = Length.Percent(50);
            btnPlay.style.marginLeft = -140; // Center 280px width
            btnPlay.style.width = 280;
            btnPlay.style.height = 110;
            btnPlay.style.backgroundColor = Color.clear;
            btnPlay.style.borderLeftWidth = 0;
            btnPlay.style.borderRightWidth = 0;
            btnPlay.style.borderTopWidth = 0;
            btnPlay.style.borderBottomWidth = 0;

            var pContainer = btnPlay.Q("playButton_container") ?? btnPlay;
            pContainer.style.width = Length.Percent(100);
            pContainer.style.height = Length.Percent(100);
            pContainer.style.position = Position.Relative;

            // Load 10-layer authentic sprites from live dump
            var texBg = Resources.Load<Texture2D>("UI/PlayButton/defaultBackgoundButton");
            var texBorder = Resources.Load<Texture2D>("UI/PlayButton/defaultBorderButton");
            var texShadow = Resources.Load<Texture2D>("UI/PlayButton/defaultShadowEffect");
            var texShadowHov = Resources.Load<Texture2D>("UI/PlayButton/hoverShadowEffect");
            var texShadowPrs = Resources.Load<Texture2D>("UI/PlayButton/pressedShadowEffect");
            var texLight = Resources.Load<Texture2D>("UI/PlayButton/defaultLightEffect");
            var texLightHov = Resources.Load<Texture2D>("UI/PlayButton/hoverLightEffect");
            var texLightPrs = Resources.Load<Texture2D>("UI/PlayButton/pressedLightEffect");
            var texGems = Resources.Load<Texture2D>("UI/PlayButton/defaultGems");

            void SetSprite(string elName, Texture2D texture)
            {
                var el = btnPlay.Q(elName);
                if (el != null && texture != null)
                {
                    el.style.backgroundImage = new StyleBackground(texture);
                    el.style.position = Position.Absolute;
                    el.style.top = 0; el.style.left = 0;
                    el.style.width = Length.Percent(100);
                    el.style.height = Length.Percent(100);
                    el.style.display = DisplayStyle.Flex;
                }
            }

            SetSprite("playButton_background", texBg);
            SetSprite("playButton_border", texBorder);
            SetSprite("playButton_shadowEffect", texShadow);
            SetSprite("playButton_lightEffect", texLight);
            SetSprite("playButton_gems", texGems);

            var shadowEl = btnPlay.Q("playButton_shadowEffect");
            var lightEl = btnPlay.Q("playButton_lightEffect");

            btnPlay.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (shadowEl != null && texShadowHov != null) shadowEl.style.backgroundImage = new StyleBackground(texShadowHov);
                if (lightEl != null && texLightHov != null) lightEl.style.backgroundImage = new StyleBackground(texLightHov);
            });
            btnPlay.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (shadowEl != null && texShadow != null) shadowEl.style.backgroundImage = new StyleBackground(texShadow);
                if (lightEl != null && texLight != null) lightEl.style.backgroundImage = new StyleBackground(texLight);
            });
            btnPlay.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (shadowEl != null && texShadowPrs != null) shadowEl.style.backgroundImage = new StyleBackground(texShadowPrs);
                if (lightEl != null && texLightPrs != null) lightEl.style.backgroundImage = new StyleBackground(texLightPrs);
            });
            btnPlay.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (shadowEl != null && texShadowHov != null) shadowEl.style.backgroundImage = new StyleBackground(texShadowHov);
                if (lightEl != null && texLightHov != null) lightEl.style.backgroundImage = new StyleBackground(texLightHov);
            });

            Action triggerServerPlay = () =>
            {
                int targetId = _pendingServerId > 0 ? _pendingServerId : 290;
                if (_wsConn != null)
                {
                    Log($"  Server select: {targetId}");
                    SetLoginState(root, 1);
                    var selectFrame = WebSocketBridge.BuildServerSelectFrame(targetId, "en");
                    WebSocketBridge.Send(_wsConn, selectFrame);
                }
            };

            btnPlay.Query().ForEach(child =>
            {
                if (child != btnPlay)
                {
                    child.pickingMode = PickingMode.Ignore;
                    child.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); triggerServerPlay(); });
                    child.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) { evt.StopPropagation(); triggerServerPlay(); } });
                }
            });

            if (btnPlay is Button ssBtn) ssBtn.clicked += () => triggerServerPlay();
            btnPlay.RegisterCallback<ClickEvent>(evt => triggerServerPlay());
            btnPlay.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) triggerServerPlay(); });

            var ssLabel = btnPlay.Q("playButton_label") as Label ?? btnPlay.Q<Label>();
            if (ssLabel != null)
            {
                ssLabel.text = "JUGAR";
                ssLabel.style.color = new Color(1f, 0.92f, 0.7f, 1f);
                ssLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                ssLabel.style.fontSize = 24;
                ssLabel.style.letterSpacing = 2;
                ssLabel.style.position = Position.Absolute;
                ssLabel.style.top = 0; ssLabel.style.left = 0;
                ssLabel.style.width = Length.Percent(100);
                ssLabel.style.height = Length.Percent(100);
                ssLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
        }"""

if old_play_target in content:
    content = content.replace(old_play_target, new_play_replacement, 1)
    print("1. Replaced PlayButton successfully")
else:
    print("1. WARNING: old_play_target not matched")

# 2. Update search & friends icons
old_search_target = """        var btnSearch = root.Q("btn_search");
        if (btnSearch != null)
        {
            var sTex = Resources.Load<Texture2D>("FigmaIcons/magnifier");
            if (sTex != null)
            {
                btnSearch.style.backgroundImage = new StyleBackground(sTex);
                btnSearch.style.width = 22;
                btnSearch.style.height = 22;
            }
        }

        var btnFriends = root.Q("btn_friends");
        if (btnFriends != null)
        {
            btnFriends.style.backgroundColor = new Color(0.95f, 0.62f, 0.15f, 1f);
            btnFriends.style.borderTopLeftRadius = 8;
            btnFriends.style.borderTopRightRadius = 8;
            btnFriends.style.borderBottomLeftRadius = 8;
            btnFriends.style.borderBottomRightRadius = 8;
            btnFriends.style.width = 36;
            btnFriends.style.height = 32;
            btnFriends.style.marginLeft = 8;
            btnFriends.style.justifyContent = Justify.Center;
            btnFriends.style.alignItems = Align.Center;

            var fTex = Resources.Load<Texture2D>("FigmaIcons/party") ?? Resources.Load<Texture2D>("FigmaIcons/lockFriends");
            if (fTex != null)
            {
                var iconImg = new VisualElement();
                iconImg.style.backgroundImage = new StyleBackground(fTex);
                iconImg.style.width = 18;
                iconImg.style.height = 18;
                btnFriends.Add(iconImg);
            }
        }"""

new_search_replacement = """        var btnSearch = root.Q("btn_search");
        if (btnSearch != null)
        {
            var sTex = Resources.Load<Texture2D>("UI/Icons/magnifierShadowOutline") ?? Resources.Load<Texture2D>("FigmaIcons/magnifier");
            if (sTex != null)
            {
                var iconEl = btnSearch.Q("button_iconLeft_Vector") ?? btnSearch;
                iconEl.style.backgroundImage = new StyleBackground(sTex);
                iconEl.style.width = 20;
                iconEl.style.height = 20;
            }
        }

        var btnFriends = root.Q("btn_friends");
        if (btnFriends != null)
        {
            var fTex = Resources.Load<Texture2D>("UI/Icons/partyShadowOutline") ?? Resources.Load<Texture2D>("FigmaIcons/party");
            if (fTex != null)
            {
                var iconEl = btnFriends.Q("button_iconLeft_Vector") ?? btnFriends;
                iconEl.style.backgroundImage = new StyleBackground(fTex);
                iconEl.style.width = 20;
                iconEl.style.height = 20;
            }
        }"""

if old_search_target in content:
    content = content.replace(old_search_target, new_search_replacement, 1)
    print("2. Replaced search & friends icons successfully")
else:
    print("2. WARNING: old_search_target not matched")

# 3. Update chevrons
old_chevrons_target = """        var btnLeft = root.Q("btn_moveLeft");
        var btnRight = root.Q("btn_moveRight");"""

new_chevrons_replacement = """        var btnLeft = root.Q("btn_moveLeft");
        var btnRight = root.Q("btn_moveRight");

        var texArrowL = Resources.Load<Texture2D>("UI/Icons/arrowBigLeftShadowOutline");
        if (btnLeft != null && texArrowL != null)
        {
            var arrowEl = btnLeft.Q("carousel_arrow") ?? btnLeft;
            arrowEl.style.backgroundImage = new StyleBackground(texArrowL);
            btnLeft.style.width = 64;
            btnLeft.style.height = 64;
        }

        var texArrowR = Resources.Load<Texture2D>("UI/Icons/arrowBigRightShadowOutline");
        if (btnRight != null && texArrowR != null)
        {
            var arrowEl = btnRight.Q("carousel_arrow") ?? btnRight;
            arrowEl.style.backgroundImage = new StyleBackground(texArrowR);
            btnRight.style.width = 64;
            btnRight.style.height = 64;
        }"""

if old_chevrons_target in content:
    content = content.replace(old_chevrons_target, new_chevrons_replacement, 1)
    print("3. Replaced chevrons successfully")
else:
    print("3. WARNING: old_chevrons_target not matched")

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)
print("Updated WebGLProbe.cs successfully!")

