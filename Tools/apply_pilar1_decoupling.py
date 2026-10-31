import re

file_path = "UnityProject/Assets/WebGLProbe.cs"

with open(file_path, "r", encoding="utf-8") as f:
    code = f.read()

# 1. Helper FormatSubscriptionDisplay
if "FormatSubscriptionDisplay" not in code:
    helper_code = '''    static string FormatSubscriptionDisplay(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        if (DateTimeOffset.TryParse(raw, out var dto))
            return $"Hasta el {dto.ToString("dd/MM/yyyy, HH:mm")}";
        return raw.StartsWith("Hasta el") ? raw : $"Hasta el {raw}";
    }
'''
    # insert before MountCharacterHeader
    code = code.replace("    VisualElement _characterHeaderRoot;", "    VisualElement _characterHeaderRoot;\r\n" + helper_code)

# 2. Parse tag (f3) and subscription (f5) in AuthAccepted
old_auth = '''                string nickname = f2f3f1 != null ? PbStr(f2f3f1, 2) : null;
                if (!string.IsNullOrEmpty(nickname)) _accountNickname = nickname;
                Log($"  nickname={nickname}");'''

new_auth = '''                string nickname = f2f3f1 != null ? PbStr(f2f3f1, 2) : null;
                if (!string.IsNullOrEmpty(nickname)) _accountNickname = nickname;
                string tag = f2f3f1 != null ? PbStr(f2f3f1, 3) : null;
                if (!string.IsNullOrEmpty(tag)) _accountTag = tag.StartsWith("#") ? tag : ("#" + tag);
                string subEnd = f2f3f1 != null ? PbStr(f2f3f1, 5) : null;
                if (!string.IsNullOrEmpty(subEnd)) _subscriptionEndDate = FormatSubscriptionDisplay(subEnd);
                Log($"  nickname={nickname}, tag={tag}, subscription={subEnd}");'''

if old_auth in code:
    code = code.replace(old_auth, new_auth)
    print("Fix 1 applied: AuthAccepted f3 and f5 parsing")
else:
    print("Warning: old_auth not found exactly, trying CRLF / LF normalization")
    code = code.replace(old_auth.replace("\r\n", "\n"), new_auth.replace("\r\n", "\n"))

# 3. Dynamic tag & subscription in MountCharacterHeader
old_hdr = '''        SetElementText(_characterHeaderRoot, "lbl_tag", "#0001");
        var tagLbl = _characterHeaderRoot.Q<Label>("lbl_tag");
        if (tagLbl != null)
        {
            tagLbl.text = "#0001";
            tagLbl.style.color = new Color(0.55f, 0.62f, 0.70f, 1f);
            tagLbl.style.fontSize = 12;
            tagLbl.style.marginLeft = 4;
        }

        SetElementText(_characterHeaderRoot, "lbl_subscription", "Hasta el 15/10/2027, 13:11");
        var subLbl = _characterHeaderRoot.Q<Label>("lbl_subscription");
        if (subLbl != null)
        {
            subLbl.text = "Hasta el 15/10/2027, 13:11";
            subLbl.style.color = new Color(0.80f, 0.85f, 0.90f, 1f);
            subLbl.style.fontSize = 12;
            subLbl.style.marginLeft = 6;
        }'''

new_hdr = '''        string tag = !string.IsNullOrEmpty(_accountTag) ? _accountTag : "#0001";
        SetElementText(_characterHeaderRoot, "lbl_tag", tag);
        var tagLbl = _characterHeaderRoot.Q<Label>("lbl_tag");
        if (tagLbl != null)
        {
            tagLbl.text = tag;
            tagLbl.style.color = new Color(0.55f, 0.62f, 0.70f, 1f);
            tagLbl.style.fontSize = 12;
            tagLbl.style.marginLeft = 4;
        }

        string sub = !string.IsNullOrEmpty(_subscriptionEndDate) ? _subscriptionEndDate : "Hasta el 15/10/2027, 13:11";
        SetElementText(_characterHeaderRoot, "lbl_subscription", sub);
        var subLbl = _characterHeaderRoot.Q<Label>("lbl_subscription");
        if (subLbl != null)
        {
            subLbl.text = sub;
            subLbl.style.color = new Color(0.80f, 0.85f, 0.90f, 1f);
            subLbl.style.fontSize = 12;
            subLbl.style.marginLeft = 6;
        }'''

if old_hdr in code:
    code = code.replace(old_hdr, new_hdr)
    print("Fix 2 applied: MountCharacterHeader dynamic tag & subscription")
else:
    print("Warning: old_hdr not found exactly, trying CRLF / LF normalization")
    code = code.replace(old_hdr.replace("\r\n", "\n"), new_hdr.replace("\r\n", "\n"))

# 4. Remove hardcoded fallback & generic carousel in PopulateServerSelection
old_carousel = '''        // 4. Data-driven Carousel: Matching selecaoServ.png (Left: TYLEZIA, Center: TAL KASHA, Right: HELL MINA)
        var servers = _serverList;
        if (servers == null || servers.Count == 0)
        {
            servers = new List<ServerInfo>
            {
                new ServerInfo { id = 292, charCount = 0, charNames = new List<string>() }, // TYLEZIA
                new ServerInfo { id = 290, charCount = 1, charNames = new List<string> { "Leonardo" } }, // TAL KASHA
                new ServerInfo { id = 291, charCount = 0, charNames = new List<string>() }  // HELL MINA
            };
        }
        else
        {
            // Dynamic arrangement matching selecaoServ.png:
            // Center = server with character (290 Tal Kasha), Left = 292 (Tylezia), Right = 291 (Hell Mina)
            int charServerIdx = -1;
            for (int s = 0; s < servers.Count; s++)
            {
                if (servers[s].charCount > 0) { charServerIdx = s; break; }
            }
            if (charServerIdx >= 0)
            {
                var charServer = servers[charServerIdx];
                var others = new List<ServerInfo>();
                for (int s = 0; s < servers.Count; s++)
                {
                    if (s != charServerIdx) others.Add(servers[s]);
                }

                ServerInfo? left = null;
                for (int s = 0; s < others.Count; s++)
                {
                    if (others[s].id == 292) { left = others[s]; others.RemoveAt(s); break; }
                }
                if (!left.HasValue && others.Count > 0) { left = others[0]; others.RemoveAt(0); }

                ServerInfo? right = null;
                for (int s = 0; s < others.Count; s++)
                {
                    if (others[s].id == 291) { right = others[s]; others.RemoveAt(s); break; }
                }
                if (!right.HasValue && others.Count > 0) { right = others[0]; others.RemoveAt(0); }

                var reordered = new List<ServerInfo>();
                if (left.HasValue) reordered.Add(left.Value);
                reordered.Add(charServer);
                if (right.HasValue) reordered.Add(right.Value);
                reordered.AddRange(others);
                servers = reordered;
            }
        }'''

new_carousel = '''        // 4. Data-driven Carousel: 100% dynamic without hardcoded IDs or mock fallbacks (Pilar 1)
        var servers = _serverList;
        var lblNoServer = root.Q<Label>("lbl_noServer");

        if (servers == null || servers.Count == 0)
        {
            if (lblNoServer != null)
            {
                lblNoServer.style.display = DisplayStyle.Flex;
                lblNoServer.style.visibility = Visibility.Visible;
                lblNoServer.visible = true;
                lblNoServer.text = "Ningún servidor disponible";
            }
            var ctrCar = root.Q("ctr_carouselMode");
            if (ctrCar != null) ctrCar.style.display = DisplayStyle.None;
            return;
        }

        if (lblNoServer != null)
        {
            lblNoServer.style.display = DisplayStyle.None;
            lblNoServer.style.visibility = Visibility.Hidden;
            lblNoServer.visible = false;
        }

        // Generic arrangement: server with characters in the center, flanked by remaining servers
        int primaryIdx = 0;
        for (int s = 0; s < servers.Count; s++)
        {
            if (servers[s].charCount > 0) { primaryIdx = s; break; }
        }

        var primaryServer = servers[primaryIdx];
        var remaining = new List<ServerInfo>();
        for (int s = 0; s < servers.Count; s++)
        {
            if (s != primaryIdx) remaining.Add(servers[s]);
        }

        ServerInfo? leftServer = remaining.Count > 0 ? remaining[0] : (ServerInfo?)null;
        ServerInfo? rightServer = remaining.Count > 1 ? remaining[1] : (ServerInfo?)null;
        var otherServers = remaining.Count > 2 ? remaining.GetRange(2, remaining.Count - 2) : new List<ServerInfo>();

        var reordered = new List<ServerInfo>();
        if (leftServer.HasValue) reordered.Add(leftServer.Value);
        reordered.Add(primaryServer);
        if (rightServer.HasValue) reordered.Add(rightServer.Value);
        reordered.AddRange(otherServers);
        servers = reordered;'''

if old_carousel in code:
    code = code.replace(old_carousel, new_carousel)
    print("Fix 3 applied: Generic carousel reordering & lbl_noServer")
else:
    print("Warning: old_carousel not found exactly, trying CRLF / LF normalization")
    code = code.replace(old_carousel.replace("\r\n", "\n"), new_carousel.replace("\r\n", "\n"))

# 5. Remove : 290 fallback on action "enter"
old_enter = '''            else if (_currentScreen == "ServerSelection" && _wsConn != null)
            {
                int servId = _pendingServerId > 0 ? _pendingServerId : (_serverList != null && _serverList.Count > 0 ? _serverList[0].id : 290);
                Log($"  [TEST] Selecting server {servId}");'''

new_enter = '''            else if (_currentScreen == "ServerSelection" && _wsConn != null)
            {
                if (_serverList == null || _serverList.Count == 0)
                {
                    Log("  [TEST] Cannot select server: server list is empty");
                    return;
                }
                int servId = _pendingServerId > 0 ? _pendingServerId : _serverList[0].id;
                Log($"  [TEST] Selecting server {servId}");'''

if old_enter in code:
    code = code.replace(old_enter, new_enter)
    print("Fix 4 applied: Removed : 290 fallback on enter action")
else:
    print("Warning: old_enter not found exactly, trying CRLF / LF normalization")
    code = code.replace(old_enter.replace("\r\n", "\n"), new_enter.replace("\r\n", "\n"))

with open(file_path, "w", encoding="utf-8") as f:
    f.write(code)

print("Finished applying Pilar 1 fixes.")
