file_path = "UnityProject/Assets/WebGLProbe.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update Search & Friends
old_search_block = """        var btnSearch = root.Q("btn_search");
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
            var fTex = Resources.Load<Texture2D>("FigmaIcons/party") ?? Resources.Load<Texture2D>("FigmaIcons/lockFriends");
            if (fTex != null) btnFriends.style.backgroundImage = new StyleBackground(fTex);
        }"""

new_search_block = """        var btnSearch = root.Q("btn_search");
        if (btnSearch != null)
        {
            var sTex = Resources.Load<Texture2D>("UI/Icons/magnifierShadowOutline") ?? Resources.Load<Texture2D>("FigmaIcons/magnifier");
            if (sTex != null)
            {
                var iconEl = btnSearch.Q("button_iconLeft_Vector") ?? btnSearch;
                iconEl.style.backgroundImage = new StyleBackground(sTex);
                btnSearch.style.width = 24;
                btnSearch.style.height = 24;
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
                btnFriends.style.width = 24;
                btnFriends.style.height = 24;
                btnFriends.style.marginLeft = 8;
            }
        }"""

if old_search_block in content:
    content = content.replace(old_search_block, new_search_block, 1)
    print("1. Search & Friends updated")
else:
    print("1. WARNING: search block not matched")

# 2. Update Card Textures
old_card_styling = """            // Dynamic illustration loading
            var imgEl = card.Q("img");
            string cleanName = srvName.ToLower().Replace(" ", "").Replace("-", "");
            var tex = Resources.Load<Texture2D>($"ServerIllustrations/{cleanName}")
                   ?? Resources.Load<Texture2D>($"ServerIllustrations/tx_serverGameType{typeId}")
                   ?? Resources.Load<Texture2D>("ServerIllustrations/talkasha");
            if (imgEl != null && tex != null)
            {
                imgEl.style.backgroundImage = new StyleBackground(tex);
                imgEl.style.width = Length.Percent(100);
                imgEl.style.height = Length.Percent(100);
                imgEl.style.position = Position.Absolute;
                imgEl.style.top = 0;
                imgEl.style.left = 0;
            }"""

new_card_styling = """            // Authentic serverCard background layers from live dump
            var scBg = card.Q("serverCard");
            var texCardBg = Resources.Load<Texture2D>("UI/ServerCard/containCardServerDefault");
            if (scBg != null && texCardBg != null) scBg.style.backgroundImage = new StyleBackground(texCardBg);

            var shadowBg = card.Q("shadow");
            var texShadowCard = Resources.Load<Texture2D>("UI/ServerCard/innerShadowCard");
            if (shadowBg != null && texShadowCard != null) shadowBg.style.backgroundImage = new StyleBackground(texShadowCard);

            var titleBg1 = card.Q("title_bg");
            var texTitleBg1 = Resources.Load<Texture2D>("UI/ServerCard/containTitleBg");
            if (titleBg1 != null && texTitleBg1 != null) titleBg1.style.backgroundImage = new StyleBackground(texTitleBg1);

            var titleBg2 = card.Q("title_bg2");
            var texTitleBg2 = Resources.Load<Texture2D>("UI/ServerCard/containTitle");
            if (titleBg2 != null && texTitleBg2 != null) titleBg2.style.backgroundImage = new StyleBackground(texTitleBg2);

            var gemStone = card.Q("gemStone");
            var texGem = Resources.Load<Texture2D>("UI/ServerCard/gemBackground");
            if (gemStone != null && texGem != null) gemStone.style.backgroundImage = new StyleBackground(texGem);

            var infoServer = card.Q("ctr_infoServer");
            var texInfo = Resources.Load<Texture2D>("UI/ServerCard/containInfoServer");
            if (infoServer != null && texInfo != null) infoServer.style.backgroundImage = new StyleBackground(texInfo);

            // Dynamic illustration loading
            var imgEl = card.Q("img");
            string cleanName = srvName.ToLower().Replace(" ", "").Replace("-", "");
            var tex = Resources.Load<Texture2D>($"ServerIllustrations/{cleanName}")
                   ?? Resources.Load<Texture2D>($"ServerIllustrations/tx_serverGameType{typeId}")
                   ?? Resources.Load<Texture2D>("ServerIllustrations/talkasha");
            if (imgEl != null && tex != null)
            {
                imgEl.style.backgroundImage = new StyleBackground(tex);
                imgEl.style.width = Length.Percent(100);
                imgEl.style.height = Length.Percent(100);
                imgEl.style.position = Position.Absolute;
                imgEl.style.top = 0;
                imgEl.style.left = 0;
            }"""

if old_card_styling in content:
    content = content.replace(old_card_styling, new_card_styling, 1)
    print("2. ServerCard layers updated")
else:
    print("2. WARNING: card styling not matched")

# 3. Update status dot and tag icon
old_status_tag = """            // Online green dot
            var statusEl = card.Q("icon_status");
            if (statusEl != null)
            {
                statusEl.style.backgroundColor = new Color(0.2f, 0.9f, 0.35f, 1f);
                statusEl.style.width = 10;
                statusEl.style.height = 10;
                statusEl.style.borderTopLeftRadius = 5;
                statusEl.style.borderTopRightRadius = 5;
                statusEl.style.borderBottomLeftRadius = 5;
                statusEl.style.borderBottomRightRadius = 5;
            }

            // Tag: dynamic server type
            var tagEl = card.Q("tag");
            if (tagEl != null)
            {
                SetElementText(card, "tag", typeName.ToUpper());
                tagEl.style.backgroundColor = new Color(0.12f, 0.38f, 0.58f, 0.9f);
                tagEl.style.borderTopLeftRadius = 10;
                tagEl.style.borderTopRightRadius = 10;
                tagEl.style.borderBottomLeftRadius = 10;
                tagEl.style.borderBottomRightRadius = 10;
                tagEl.style.paddingLeft = 10;
                tagEl.style.paddingRight = 10;
            }"""

new_status_tag = """            // Status icon from live dump
            var statusEl = card.Q("icon_status");
            var texCircle = Resources.Load<Texture2D>("UI/Icons/circleShadowOutline");
            if (statusEl != null && texCircle != null)
            {
                statusEl.style.backgroundImage = new StyleBackground(texCircle);
                statusEl.style.width = 18;
                statusEl.style.height = 18;
            }

            // Tag: dynamic server type with authentic diamond icon
            var tagEl = card.Q("tag");
            if (tagEl != null)
            {
                SetElementText(card, "tag", typeName.ToUpper());
                var tagIcon = tagEl.Q("tag_iconLeft_Vector") ?? tagEl.Q(className: "icon");
                var texDiamond = Resources.Load<Texture2D>("UI/Icons/diamondFillShadowOutline");
                if (tagIcon != null && texDiamond != null)
                {
                    tagIcon.style.backgroundImage = new StyleBackground(texDiamond);
                    tagIcon.style.width = 14;
                    tagIcon.style.height = 14;
                }
            }

            // Plus icon for new character
            var newCharEl = card.Q("ctr_newCharacter");
            var iconPlus = newCharEl?.Q("icon") ?? card.Q("plusShadowOutline");
            var texPlus = Resources.Load<Texture2D>("UI/Icons/plusShadowOutline");
            if (iconPlus != null && texPlus != null)
            {
                iconPlus.style.backgroundImage = new StyleBackground(texPlus);
                iconPlus.style.width = 18;
                iconPlus.style.height = 18;
            }"""

if old_status_tag in content:
    content = content.replace(old_status_tag, new_status_tag, 1)
    print("3. Status dot and tag icon updated")
else:
    print("3. WARNING: status_tag block not matched")

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)
print("Saved update_card_and_search.py changes!")

