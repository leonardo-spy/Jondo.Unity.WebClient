import sys
import re

file_path = "UnityProject/Assets/WebGLProbe.cs"

with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Hide ctr_force, lbl_noServer completely and BringToFront lblTitle
old_header_part = """        // Hide warning and empty server labels
        var lblNoServer = root.Q("lbl_noServer");
        if (lblNoServer != null) lblNoServer.style.display = DisplayStyle.None;
        var ctrWarning = root.Q("ctr_warning");
        if (ctrWarning != null) ctrWarning.style.display = DisplayStyle.None;

        // 1. Header Frame: Authentic Background + Title + Tabs
        var ctrH = root.Q("ctr_header");
        if (ctrH != null)
        {
            ctrH.style.position = Position.Absolute;
            ctrH.style.top = 0;
            ctrH.style.left = 0;
            ctrH.style.right = 0;
            ctrH.style.height = 140;
            ctrH.style.alignItems = Align.Center;
        }

        // Title: ELECCIÓN DE UN SERVIDOR
        var lblTitle = root.Q<Label>("lbl_title") ?? root.Q("lbl_title") as Label;
        if (lblTitle != null)
        {
            lblTitle.text = "ELECCIÓN DE UN SERVIDOR";
            lblTitle.style.position = Position.Absolute;
            lblTitle.style.top = 20;
            lblTitle.style.left = Length.Percent(50);
            lblTitle.style.marginLeft = -350;
            lblTitle.style.width = 700;
            lblTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            lblTitle.style.color = Color.white;
            lblTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            lblTitle.style.fontSize = 24;
            lblTitle.style.letterSpacing = 2;
        }"""

new_header_part = """        // Hide debug ctr_force, warnings, scrollers and empty server labels
        var ctrForce = root.Q("ctr_force");
        if (ctrForce != null)
        {
            ctrForce.style.display = DisplayStyle.None;
            ctrForce.style.visibility = Visibility.Hidden;
        }
        var ctrWarning = root.Q("ctr_warning");
        if (ctrWarning != null)
        {
            ctrWarning.style.display = DisplayStyle.None;
            ctrWarning.style.visibility = Visibility.Hidden;
        }
        root.Query<Label>("lbl_noServer").ForEach(l =>
        {
            l.style.display = DisplayStyle.None;
            l.style.visibility = Visibility.Hidden;
            l.text = "";
            l.visible = false;
        });
        root.Query(className: "unity-scroller").ForEach(s => s.style.display = DisplayStyle.None);

        // 1. Header Frame: Authentic Background + Title + Tabs
        var ctrH = root.Q("ctr_header");
        if (ctrH != null)
        {
            ctrH.style.position = Position.Absolute;
            ctrH.style.top = 0;
            ctrH.style.left = 0;
            ctrH.style.right = 0;
            ctrH.style.height = 140;
            ctrH.style.alignItems = Align.Center;
        }

        // Title: ELECCIÓN DE UN SERVIDOR
        var lblTitle = root.Q<Label>("lbl_title") ?? root.Q("lbl_title") as Label;
        if (lblTitle != null)
        {
            lblTitle.BringToFront();
            lblTitle.text = "ELECCIÓN DE UN SERVIDOR";
            lblTitle.style.display = DisplayStyle.Flex;
            lblTitle.style.visibility = Visibility.Visible;
            lblTitle.style.position = Position.Absolute;
            lblTitle.style.top = 20;
            lblTitle.style.left = Length.Percent(50);
            lblTitle.style.marginLeft = -350;
            lblTitle.style.width = 700;
            lblTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            lblTitle.style.color = Color.white;
            lblTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            lblTitle.style.fontSize = 24;
            lblTitle.style.letterSpacing = 2;
        }"""

assert old_header_part in content, "old_header_part not found!"
content = content.replace(old_header_part, new_header_part)

# 2. Carousel dynamic ordering to match selecaoServ.png (TYLEZIA at 0, TAL KASHA at 1, HELL MINA at 2)
old_carousel_servers = """        // 4. Data-driven Carousel: Matching selecaoServ.png (Left: TYLEZIA, Center: TAL KASHA, Right: HELL MINA)
        var servers = _serverList;
        if (servers == null || servers.Count == 0)
        {
            servers = new List<ServerInfo>
            {
                new ServerInfo { id = 292, charCount = 0, charNames = new List<string>() }, // TYLEZIA
                new ServerInfo { id = 290, charCount = 1, charNames = new List<string> { "Leonardo" } }, // TAL KASHA
                new ServerInfo { id = 291, charCount = 0, charNames = new List<string>() }  // HELL MINA
            };
        }"""

new_carousel_servers = """        // 4. Data-driven Carousel: Matching selecaoServ.png (Left: TYLEZIA, Center: TAL KASHA, Right: HELL MINA)
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
        }"""

assert old_carousel_servers in content, "old_carousel_servers not found!"
content = content.replace(old_carousel_servers, new_carousel_servers)

# 3. Selected index and pendingServerId
old_sel_index = """        // Selected index: default to the server with characters, or index 1 (TAL KASHA in center)
        if (_selectedServerCardIdx < 0)
        {
            _selectedServerCardIdx = 1;
            for (int s = 0; s < servers.Count; s++)
            {
                if (servers[s].charCount > 0) { _selectedServerCardIdx = s; break; }
            }
        }
        if (_selectedServerCardIdx >= 0 && _selectedServerCardIdx < servers.Count)
            _pendingServerId = servers[_selectedServerCardIdx].id;"""

new_sel_index = """        // Selected index: index 1 (TAL KASHA in center)
        _selectedServerCardIdx = (servers.Count > 1) ? 1 : 0;
        if (_selectedServerCardIdx >= 0 && _selectedServerCardIdx < servers.Count)
            _pendingServerId = servers[_selectedServerCardIdx].id;"""

assert old_sel_index in content, "old_sel_index not found!"
content = content.replace(old_sel_index, new_sel_index)

# 4. BringToFront on btnPlay
old_play_btn = """            btnPlay.style.borderTopWidth = 0;
            btnPlay.style.borderBottomWidth = 0;

            var pContainer = btnPlay.Q("playButton_container") ?? btnPlay;"""

new_play_btn = """            btnPlay.style.borderTopWidth = 0;
            btnPlay.style.borderBottomWidth = 0;
            btnPlay.BringToFront();

            var pContainer = btnPlay.Q("playButton_container") ?? btnPlay;"""

assert old_play_btn in content, "old_play_btn not found!"
content = content.replace(old_play_btn, new_play_btn)

# 5. Clear _uiRoot background in MountInGameHud to reveal 3D map
old_hud_mount = """    void MountInGameHud(VisualElement root = null)
    {
        var targetRoot = root ?? _uiRoot;
        if (targetRoot == null) return;
        if (_uiRoot != null) _uiRoot.style.display = DisplayStyle.Flex;

        // Hide auth background and connection overlays so the game world is visible
        for (int ci = 0; ci < targetRoot.childCount; ci++)
        {
            var child = targetRoot[ci];
            bool isAuth = child.name != null && (
                child.name.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("selection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("header", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name == "img"
            );
            if (isAuth) child.style.display = DisplayStyle.None;
        }"""

new_hud_mount = """    void MountInGameHud(VisualElement root = null)
    {
        var targetRoot = root ?? _uiRoot;
        if (targetRoot == null) return;
        if (_uiRoot != null)
        {
            _uiRoot.style.display = DisplayStyle.Flex;
            _uiRoot.style.backgroundImage = StyleKeyword.None;
            _uiRoot.style.backgroundColor = Color.clear;
        }
        targetRoot.style.backgroundImage = StyleKeyword.None;
        targetRoot.style.backgroundColor = Color.clear;

        // Hide auth background and connection overlays so the game world is visible
        for (int ci = 0; ci < targetRoot.childCount; ci++)
        {
            var child = targetRoot[ci];
            bool isAuth = child.name != null && (
                child.name.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("selection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("header", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.StartsWith("screen_") ||
                child.name == "img"
            );
            if (isAuth) child.style.display = DisplayStyle.None;
        }"""

assert old_hud_mount in content, "old_hud_mount not found!"
content = content.replace(old_hud_mount, new_hud_mount)

# 6. Entity Y inversion to match upright map
old_player_spawn = """        Vector2 startPos = IsometricGrid.CellIdToWorldPos(_playerCellId);
        _playerEntityGO.transform.position = new Vector3(startPos.x, startPos.y, 0);"""

new_player_spawn = """        Vector2 startPos = IsometricGrid.CellIdToWorldPos(_playerCellId);
        _playerEntityGO.transform.position = new Vector3(startPos.x, -startPos.y, 0);"""

assert old_player_spawn in content, "old_player_spawn not found!"
content = content.replace(old_player_spawn, new_player_spawn)

old_hover = """        Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
        _hoverCellGO.transform.position = new Vector3(pos.x, pos.y, 0);"""

new_hover = """        Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
        _hoverCellGO.transform.position = new Vector3(pos.x, -pos.y, 0);"""

assert old_hover in content, "old_hover not found!"
content = content.replace(old_hover, new_hover)

old_actor = """        Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
        actorGO.transform.position = new Vector3(pos.x, pos.y, 0);"""

new_actor = """        Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
        actorGO.transform.position = new Vector3(pos.x, -pos.y, 0);"""

assert old_actor in content, "old_actor not found!"
content = content.replace(old_actor, new_actor)

old_player_pos = """                Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
                _playerEntityGO.transform.position = new Vector3(pos.x, pos.y, 0);"""

new_player_pos = """                Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
                _playerEntityGO.transform.position = new Vector3(pos.x, -pos.y, 0);"""

assert old_player_pos in content, "old_player_pos not found!"
content = content.replace(old_player_pos, new_player_pos)

old_move_step = """            Vector3 startPos = IsometricGrid.CellIdToWorldPos(_playerCellId);
            Vector3 targetPos = IsometricGrid.CellIdToWorldPos(nextCell);"""

new_move_step = """            Vector2 posStart = IsometricGrid.CellIdToWorldPos(_playerCellId);
            Vector2 posTarget = IsometricGrid.CellIdToWorldPos(nextCell);
            Vector3 startPos = new Vector3(posStart.x, -posStart.y, 0);
            Vector3 targetPos = new Vector3(posTarget.x, -posTarget.y, 0);"""

assert old_move_step in content, "old_move_step not found!"
content = content.replace(old_move_step, new_move_step)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("[OK] All 6 parity fixes applied successfully to WebGLProbe.cs!")

