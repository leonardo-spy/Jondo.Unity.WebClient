#!/usr/bin/env python3
"""
apply_typed_bindings_integration.py — Integração dos Bindings Fortemente Tipados no WebGLProbe.cs (Opção 1)

Substitui consultas manuais soltas (root.Q("...")) pelas instâncias geradas do Pilar 2:
- GeneratedLoginBinding
- GeneratedServerSelectionBinding
- GeneratedCharacterSelectionBinding
- GeneratedCharacterHeaderBinding
- GeneratedCharacterInformationsBinding
- GeneratedChatviewBinding
- GeneratedActionBarBinding
- GeneratedBannerMenuBinding
"""

import sys

PATH = "UnityProject/Assets/WebGLProbe.cs"

def main():
    print(f"[*] Lendo {PATH}...")
    with open(PATH, "rb") as f:
        data = f.read()

    # Detect CRLF
    is_crlf = b"\r\n" in data
    eol = b"\r\n" if is_crlf else b"\n"
    text = data.decode("utf-8")

    # 1. Usings
    using_target = "using Jondo.Reconstructed.Movement;"
    using_repl = "using Jondo.Reconstructed.Movement;\r\nusing Jondo.UI.Generated;" if is_crlf else "using Jondo.Reconstructed.Movement;\nusing Jondo.UI.Generated;"
    if "using Jondo.UI.Generated;" not in text:
        if using_target in text:
            text = text.replace(using_target, using_repl, 1)
            print("[+] Added 'using Jondo.UI.Generated;'")
        else:
            print("[-] Could not find using target")
            return 1

    # 2. Field declarations
    field_target = "VisualElement _characterHeaderRoot;"
    field_repl = (
        "VisualElement _characterHeaderRoot;\r\n"
        "\r\n"
        "    // Generated UI Toolkit Typed Bindings (Pilar 2 - Opcao 1)\r\n"
        "    GeneratedLoginBinding _loginBinding;\r\n"
        "    GeneratedServerSelectionBinding _serverSelectionBinding;\r\n"
        "    GeneratedCharacterSelectionBinding _charSelectionBinding;\r\n"
        "    GeneratedCharacterHeaderBinding _charHeaderBinding;\r\n"
        "    GeneratedCharacterInformationsBinding _charInfoBinding;\r\n"
        "    GeneratedChatviewBinding _chatviewBinding;\r\n"
        "    GeneratedActionBarBinding _actionBarBinding;\r\n"
        "    GeneratedBannerMenuBinding _bannerMenuBinding;"
    ) if is_crlf else (
        "VisualElement _characterHeaderRoot;\n"
        "\n"
        "    // Generated UI Toolkit Typed Bindings (Pilar 2 - Opcao 1)\n"
        "    GeneratedLoginBinding _loginBinding;\n"
        "    GeneratedServerSelectionBinding _serverSelectionBinding;\n"
        "    GeneratedCharacterSelectionBinding _charSelectionBinding;\n"
        "    GeneratedCharacterHeaderBinding _charHeaderBinding;\n"
        "    GeneratedCharacterInformationsBinding _charInfoBinding;\n"
        "    GeneratedChatviewBinding _chatviewBinding;\n"
        "    GeneratedActionBarBinding _actionBarBinding;\n"
        "    GeneratedBannerMenuBinding _bannerMenuBinding;"
    )
    if "_serverSelectionBinding" not in text:
        if field_target in text:
            text = text.replace(field_target, field_repl, 1)
            print("[+] Added typed binding fields to WebGLProbe")
        else:
            print("[-] Could not find field target")
            return 1

    # 3. PopulateLoginScreen
    login_target = "    void PopulateLoginScreen(VisualElement root)\r\n    {\r\n        var btnPlay = root.Q(\"btn_play\"); // LoginBinding._btn_play" if is_crlf else "    void PopulateLoginScreen(VisualElement root)\n    {\n        var btnPlay = root.Q(\"btn_play\"); // LoginBinding._btn_play"
    login_repl = (
        "    void PopulateLoginScreen(VisualElement root)\r\n"
        "    {\r\n"
        "        _loginBinding = new GeneratedLoginBinding();\r\n"
        "        _loginBinding.Bind(root);\r\n"
        "        var btnPlay = _loginBinding.btn_play ?? root.Q(\"btn_play\"); // LoginBinding._btn_play"
    ) if is_crlf else (
        "    void PopulateLoginScreen(VisualElement root)\n"
        "    {\n"
        "        _loginBinding = new GeneratedLoginBinding();\n"
        "        _loginBinding.Bind(root);\n"
        "        var btnPlay = _loginBinding.btn_play ?? root.Q(\"btn_play\"); // LoginBinding._btn_play"
    )
    if login_target in text:
        text = text.replace(login_target, login_repl, 1)
        print("[+] Wired _loginBinding in PopulateLoginScreen")

    # 4. MountCharacterHeader
    header_mount_target = "_characterHeaderRoot.style.backgroundColor = Color.clear;\r\n\r\n        // Clean up untextured background overlays inside characterHeader\r\n        var leftContainer = _characterHeaderRoot.Q(\"left\");" if is_crlf else "_characterHeaderRoot.style.backgroundColor = Color.clear;\n\n        // Clean up untextured background overlays inside characterHeader\n        var leftContainer = _characterHeaderRoot.Q(\"left\");"
    header_mount_repl = (
        "_characterHeaderRoot.style.backgroundColor = Color.clear;\r\n"
        "\r\n"
        "        _charHeaderBinding = new GeneratedCharacterHeaderBinding();\r\n"
        "        _charHeaderBinding.Bind(_characterHeaderRoot);\r\n"
        "\r\n"
        "        // Clean up untextured background overlays inside characterHeader\r\n"
        "        var leftContainer = _charHeaderBinding.left ?? _characterHeaderRoot.Q(\"left\");"
    ) if is_crlf else (
        "_characterHeaderRoot.style.backgroundColor = Color.clear;\n"
        "\n"
        "        _charHeaderBinding = new GeneratedCharacterHeaderBinding();\n"
        "        _charHeaderBinding.Bind(_characterHeaderRoot);\n"
        "\n"
        "        // Clean up untextured background overlays inside characterHeader\n"
        "        var leftContainer = _charHeaderBinding.left ?? _characterHeaderRoot.Q(\"left\");"
    )
    if header_mount_target in text:
        text = text.replace(header_mount_target, header_mount_repl, 1)
        print("[+] Wired _charHeaderBinding in MountCharacterHeader")

    # CharacterHeader label queries
    text = text.replace(
        'var nameLbl = _characterHeaderRoot.Q<Label>("lbl_name");',
        'var nameLbl = _charHeaderBinding != null && _charHeaderBinding.lbl_name != null ? _charHeaderBinding.lbl_name : _characterHeaderRoot.Q<Label>("lbl_name");'
    )
    text = text.replace(
        'var tagLbl = _characterHeaderRoot.Q<Label>("lbl_tag");',
        'var tagLbl = _charHeaderBinding != null && _charHeaderBinding.lbl_tag != null ? _charHeaderBinding.lbl_tag : _characterHeaderRoot.Q<Label>("lbl_tag");'
    )
    text = text.replace(
        'var subLbl = _characterHeaderRoot.Q<Label>("lbl_subscription");',
        'var subLbl = _charHeaderBinding != null && _charHeaderBinding.lbl_subscription != null ? _charHeaderBinding.lbl_subscription : _characterHeaderRoot.Q<Label>("lbl_subscription");'
    )
    text = text.replace(
        'var btnSub = _characterHeaderRoot.Q("btn_subscribe");',
        'var btnSub = _charHeaderBinding != null && _charHeaderBinding.btn_subscribe != null ? _charHeaderBinding.btn_subscribe : _characterHeaderRoot.Q("btn_subscribe");'
    )
    print("[+] Updated CharacterHeader labels to use typed binding")

    # 5. PopulateServerSelection
    server_target = "    void PopulateServerSelection(VisualElement root)\r\n    {\r\n        // 0. Ensure root covers full canvas\r\n        var ctrSel = root.Q(\"ctr_serverSelection\");" if is_crlf else "    void PopulateServerSelection(VisualElement root)\n    {\n        // 0. Ensure root covers full canvas\n        var ctrSel = root.Q(\"ctr_serverSelection\");"
    server_repl = (
        "    void PopulateServerSelection(VisualElement root)\r\n"
        "    {\r\n"
        "        _serverSelectionBinding = new GeneratedServerSelectionBinding();\r\n"
        "        _serverSelectionBinding.Bind(root);\r\n"
        "\r\n"
        "        // 0. Ensure root covers full canvas\r\n"
        "        var ctrSel = _serverSelectionBinding.ctr_serverSelection ?? root.Q(\"ctr_serverSelection\");"
    ) if is_crlf else (
        "    void PopulateServerSelection(VisualElement root)\n"
        "    {\n"
        "        _serverSelectionBinding = new GeneratedServerSelectionBinding();\n"
        "        _serverSelectionBinding.Bind(root);\n"
        "\n"
        "        // 0. Ensure root covers full canvas\n"
        "        var ctrSel = _serverSelectionBinding.ctr_serverSelection ?? root.Q(\"ctr_serverSelection\");"
    )
    if server_target in text:
        text = text.replace(server_target, server_repl, 1)
        print("[+] Wired _serverSelectionBinding in PopulateServerSelection")

    # Update individual queries in PopulateServerSelection
    text = text.replace(
        'var ctrForce = root.Q("ctr_force");',
        'var ctrForce = _serverSelectionBinding?.ctr_force ?? root.Q("ctr_force");'
    )
    text = text.replace(
        'var ctrWarning = root.Q("ctr_warning");',
        'var ctrWarning = _serverSelectionBinding?.ctr_warning ?? root.Q("ctr_warning");'
    )
    text = text.replace(
        'var ctrH = root.Q("ctr_header");',
        'var ctrH = _serverSelectionBinding?.ctr_header ?? root.Q("ctr_header");'
    )
    text = text.replace(
        'var lblTitle = root.Q<Label>("lbl_title") ?? root.Q("lbl_title") as Label;',
        'var lblTitle = _serverSelectionBinding?.lbl_title ?? root.Q<Label>("lbl_title") ?? root.Q("lbl_title") as Label;'
    )
    text = text.replace(
        'var ctrSearch = root.Q("ctr_search");',
        'var ctrSearch = _serverSelectionBinding?.ctr_search ?? root.Q("ctr_search");'
    )
    text = text.replace(
        'var inpSearch = root.Q("inp_search");',
        'var inpSearch = _serverSelectionBinding?.inp_search ?? root.Q("inp_search");'
    )
    text = text.replace(
        'var btnSearch = root.Q("btn_search");',
        'var btnSearch = _serverSelectionBinding?.btn_search ?? root.Q("btn_search");'
    )
    text = text.replace(
        'var btnFriends = root.Q("btn_friends");',
        'var btnFriends = _serverSelectionBinding?.btn_friends ?? root.Q("btn_friends");'
    )
    text = text.replace(
        'var lblNoServer = root.Q<Label>("lbl_noServer");',
        'var lblNoServer = _serverSelectionBinding?.lbl_noServer ?? root.Q<Label>("lbl_noServer");'
    )
    text = text.replace(
        'var cbOnly = root.Q("cb_onlyCharacter");',
        'var cbOnly = _serverSelectionBinding?.cb_onlyCharacter ?? root.Q("cb_onlyCharacter");'
    )
    text = text.replace(
        'var btnPlay = root.Q("btn_play");',
        'var btnPlay = _serverSelectionBinding?.btn_play ?? root.Q("btn_play");'
    )
    text = text.replace(
        'var btnLeft = root.Q("btn_moveLeft");',
        'var btnLeft = _serverSelectionBinding?.btn_moveLeft ?? root.Q("btn_moveLeft");'
    )
    text = text.replace(
        'var btnRight = root.Q("btn_moveRight");',
        'var btnRight = _serverSelectionBinding?.btn_moveRight ?? root.Q("btn_moveRight");'
    )
    print("[+] Updated ServerSelection elements to use typed binding")

    # 6. PopulateCharacterSelection
    charsel_target = "    void PopulateCharacterSelection(VisualElement root)\r\n    {\r\n        // Server name" if is_crlf else "    void PopulateCharacterSelection(VisualElement root)\n    {\n        // Server name"
    charsel_repl = (
        "    void PopulateCharacterSelection(VisualElement root)\r\n"
        "    {\r\n"
        "        _charSelectionBinding = new GeneratedCharacterSelectionBinding();\r\n"
        "        _charSelectionBinding.Bind(root);\r\n"
        "\r\n"
        "        // Server name"
    ) if is_crlf else (
        "    void PopulateCharacterSelection(VisualElement root)\n"
        "    {\n"
        "        _charSelectionBinding = new GeneratedCharacterSelectionBinding();\n"
        "        _charSelectionBinding.Bind(root);\n"
        "\n"
        "        // Server name"
    )
    if charsel_target in text:
        text = text.replace(charsel_target, charsel_repl, 1)
        print("[+] Wired _charSelectionBinding in PopulateCharacterSelection")

    text = text.replace(
        'var btnCreate = root.Q("btn_createCharacter");',
        'var btnCreate = _charSelectionBinding?.btn_createCharacter ?? root.Q("btn_createCharacter");'
    )
    text = text.replace(
        'var scrollView = root.Q("gd_character");',
        'var scrollView = _charSelectionBinding?.gd_character ?? root.Q("gd_character");'
    )
    print("[+] Updated CharacterSelection elements to use typed binding")

    # 7. MountInGameHud
    text = text.replace(
        '_bannerRoot = bannerVta.CloneTree();',
        '_bannerRoot = bannerVta.CloneTree();\r\n                _bannerMenuBinding = new GeneratedBannerMenuBinding();\r\n                _bannerMenuBinding.Bind(_bannerRoot);' if is_crlf else '_bannerRoot = bannerVta.CloneTree();\n                _bannerMenuBinding = new GeneratedBannerMenuBinding();\n                _bannerMenuBinding.Bind(_bannerRoot);'
    )
    text = text.replace(
        '_chatRoot = chatVta.CloneTree();',
        '_chatRoot = chatVta.CloneTree();\r\n                _chatviewBinding = new GeneratedChatviewBinding();\r\n                _chatviewBinding.Bind(_chatRoot);' if is_crlf else '_chatRoot = chatVta.CloneTree();\n                _chatviewBinding = new GeneratedChatviewBinding();\n                _chatviewBinding.Bind(_chatRoot);'
    )
    text = text.replace(
        '_characterInfoRoot = charInfoVta.CloneTree();',
        '_characterInfoRoot = charInfoVta.CloneTree();\r\n                _charInfoBinding = new GeneratedCharacterInformationsBinding();\r\n                _charInfoBinding.Bind(_characterInfoRoot);' if is_crlf else '_characterInfoRoot = charInfoVta.CloneTree();\n                _charInfoBinding = new GeneratedCharacterInformationsBinding();\n                _charInfoBinding.Bind(_characterInfoRoot);'
    )
    text = text.replace(
        '_actionBarRoot = barVta.CloneTree();',
        '_actionBarRoot = barVta.CloneTree();\r\n                _actionBarBinding = new GeneratedActionBarBinding();\r\n                _actionBarBinding.Bind(_actionBarRoot);' if is_crlf else '_actionBarRoot = barVta.CloneTree();\n                _actionBarBinding = new GeneratedActionBarBinding();\n                _actionBarBinding.Bind(_actionBarRoot);'
    )
    print("[+] Wired HUD bindings in MountInGameHud")

    # 8. UpdateCharacterInfoStats
    stats_target = (
        "    public void UpdateCharacterInfoStats(int currentHp, int maxHp, int currentAp, int currentMp, int shield = 0, int invocations = 1)\r\n"
        "    {\r\n"
        "        if (_characterInfoRoot == null) return;\r\n"
        "        var lblPdv = _characterInfoRoot.Q<Label>(\"lbl_pdv\");\r\n"
        "        if (lblPdv != null) lblPdv.text = $\"{currentHp} / {maxHp}\";\r\n"
        "        var lblPdvBottom = _characterInfoRoot.Q<Label>(\"lbl_pdvBottom\");\r\n"
        "        if (lblPdvBottom != null) lblPdvBottom.text = $\"{currentHp}\";\r\n"
        "        var lblPa = _characterInfoRoot.Q<Label>(\"lbl_pa\");\r\n"
        "        if (lblPa != null) lblPa.text = $\"{currentAp}\";\r\n"
        "        var lblPm = _characterInfoRoot.Q<Label>(\"lbl_pm\");\r\n"
        "        if (lblPm != null) lblPm.text = $\"{currentMp}\";\r\n"
        "        var lblShield = _characterInfoRoot.Q<Label>(\"lbl_shield\");\r\n"
        "        if (lblShield != null) lblShield.text = shield > 0 ? $\"{shield}\" : \"\";\r\n"
        "        var lblIp = _characterInfoRoot.Q<Label>(\"lbl_ip\");\r\n"
        "        if (lblIp != null) lblIp.text = $\"{invocations}\";\r\n"
        "    }"
    ) if is_crlf else (
        "    public void UpdateCharacterInfoStats(int currentHp, int maxHp, int currentAp, int currentMp, int shield = 0, int invocations = 1)\n"
        "    {\n"
        "        if (_characterInfoRoot == null) return;\n"
        "        var lblPdv = _characterInfoRoot.Q<Label>(\"lbl_pdv\");\n"
        "        if (lblPdv != null) lblPdv.text = $\"{currentHp} / {maxHp}\";\n"
        "        var lblPdvBottom = _characterInfoRoot.Q<Label>(\"lbl_pdvBottom\");\n"
        "        if (lblPdvBottom != null) lblPdvBottom.text = $\"{currentHp}\";\n"
        "        var lblPa = _characterInfoRoot.Q<Label>(\"lbl_pa\");\n"
        "        if (lblPa != null) lblPa.text = $\"{currentAp}\";\n"
        "        var lblPm = _characterInfoRoot.Q<Label>(\"lbl_pm\");\n"
        "        if (lblPm != null) lblPm.text = $\"{currentMp}\";\n"
        "        var lblShield = _characterInfoRoot.Q<Label>(\"lbl_shield\");\n"
        "        if (lblShield != null) lblShield.text = shield > 0 ? $\"{shield}\" : \"\";\n"
        "        var lblIp = _characterInfoRoot.Q<Label>(\"lbl_ip\");\n"
        "        if (lblIp != null) lblIp.text = $\"{invocations}\";\n"
        "    }"
    )
    stats_repl = (
        "    public void UpdateCharacterInfoStats(int currentHp, int maxHp, int currentAp, int currentMp, int shield = 0, int invocations = 1)\r\n"
        "    {\r\n"
        "        if (_characterInfoRoot == null) return;\r\n"
        "        if (_charInfoBinding == null)\r\n"
        "        {\r\n"
        "            _charInfoBinding = new GeneratedCharacterInformationsBinding();\r\n"
        "            _charInfoBinding.Bind(_characterInfoRoot);\r\n"
        "        }\r\n"
        "\r\n"
        "        var lblPdv = _charInfoBinding?.lbl_pdv ?? _characterInfoRoot.Q<Label>(\"lbl_pdv\");\r\n"
        "        if (lblPdv != null) lblPdv.text = $\"{currentHp} / {maxHp}\";\r\n"
        "        var lblPdvBottom = _charInfoBinding?.lbl_pdvBottom ?? _characterInfoRoot.Q<Label>(\"lbl_pdvBottom\");\r\n"
        "        if (lblPdvBottom != null) lblPdvBottom.text = $\"{currentHp}\";\r\n"
        "        var lblPa = _charInfoBinding?.lbl_pa ?? _characterInfoRoot.Q<Label>(\"lbl_pa\");\r\n"
        "        if (lblPa != null) lblPa.text = $\"{currentAp}\";\r\n"
        "        var lblPm = _charInfoBinding?.lbl_pm ?? _characterInfoRoot.Q<Label>(\"lbl_pm\");\r\n"
        "        if (lblPm != null) lblPm.text = $\"{currentMp}\";\r\n"
        "        var lblShield = _charInfoBinding?.lbl_shield ?? _characterInfoRoot.Q<Label>(\"lbl_shield\");\r\n"
        "        if (lblShield != null) lblShield.text = shield > 0 ? $\"{shield}\" : \"\";\r\n"
        "        var lblIp = _charInfoBinding?.lbl_ip ?? _characterInfoRoot.Q<Label>(\"lbl_ip\");\r\n"
        "        if (lblIp != null) lblIp.text = $\"{invocations}\";\r\n"
        "    }"
    ) if is_crlf else (
        "    public void UpdateCharacterInfoStats(int currentHp, int maxHp, int currentAp, int currentMp, int shield = 0, int invocations = 1)\n"
        "    {\n"
        "        if (_characterInfoRoot == null) return;\n"
        "        if (_charInfoBinding == null)\n"
        "        {\n"
        "            _charInfoBinding = new GeneratedCharacterInformationsBinding();\n"
        "            _charInfoBinding.Bind(_characterInfoRoot);\n"
        "        }\n"
        "\n"
        "        var lblPdv = _charInfoBinding?.lbl_pdv ?? _characterInfoRoot.Q<Label>(\"lbl_pdv\");\n"
        "        if (lblPdv != null) lblPdv.text = $\"{currentHp} / {maxHp}\";\n"
        "        var lblPdvBottom = _charInfoBinding?.lbl_pdvBottom ?? _characterInfoRoot.Q<Label>(\"lbl_pdvBottom\");\n"
        "        if (lblPdvBottom != null) lblPdvBottom.text = $\"{currentHp}\";\n"
        "        var lblPa = _charInfoBinding?.lbl_pa ?? _characterInfoRoot.Q<Label>(\"lbl_pa\");\n"
        "        if (lblPa != null) lblPa.text = $\"{currentAp}\";\n"
        "        var lblPm = _charInfoBinding?.lbl_pm ?? _characterInfoRoot.Q<Label>(\"lbl_pm\");\n"
        "        if (lblPm != null) lblPm.text = $\"{currentMp}\";\n"
        "        var lblShield = _charInfoBinding?.lbl_shield ?? _characterInfoRoot.Q<Label>(\"lbl_shield\");\n"
        "        if (lblShield != null) lblShield.text = shield > 0 ? $\"{shield}\" : \"\";\n"
        "        var lblIp = _charInfoBinding?.lbl_ip ?? _characterInfoRoot.Q<Label>(\"lbl_ip\");\n"
        "        if (lblIp != null) lblIp.text = $\"{invocations}\";\n"
        "    }"
    )
    if stats_target in text:
        text = text.replace(stats_target, stats_repl, 1)
        print("[+] Wired _charInfoBinding in UpdateCharacterInfoStats")

    with open(PATH, "wb") as f:
        f.write(text.encode("utf-8"))

    print("[OK] Typed bindings integration applied successfully!")
    return 0

if __name__ == "__main__":
    sys.exit(main())

