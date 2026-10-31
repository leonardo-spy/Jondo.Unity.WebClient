#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
integrate_hud_controllers.py — Integra todos os controllers desacoplados de HUD e Combate
no WebGLProbe.cs mantendo retrocompatibilidade e eliminando dependências acopladas.
"""

import os
import re

PROBE_PATH = os.path.join("UnityProject", "Assets", "WebGLProbe.cs")

def integrate():
    with open(PROBE_PATH, "r", encoding="utf-8") as f:
        code = f.read().replace("\r\n", "\n")

    # 1. Controller fields
    field_target = "    CharacterHeaderController _characterHeaderController;\n"
    field_replacement = """    CharacterHeaderController _characterHeaderController;
    BannerMenuController _bannerMenuController;
    ChatViewController _chatViewController;
    CharacterInformationController _characterInformationController;
    UIActionBarController _actionBarController;
    UITimelineController _timelineController;
    EndTurnWidgetController _endTurnWidgetController;
    VisualElement _fightTimelineRoot;
    VisualElement _endTurnWidgetRoot;
    bool _fightHudMounted;
"""
    if "_bannerMenuController;" not in code:
        code = code.replace(field_target, field_replacement)

    # Replace any leftover GetI18nText with null
    code = code.replace("GetI18nText", "null")

    # 2. UpdateActionBarSpells delegation
    bar_spells_target = """    void UpdateActionBarSpells()
    {
        if (_actionBarRoot == null) return;"""

    bar_spells_replacement = """    void UpdateActionBarSpells()
    {
        if (_actionBarController != null)
        {
            var ids = new List<int>();
            foreach (var sp in _characterSpells) ids.Add(sp.id);
            _actionBarController.UpdateSpellSlots(ids);
            return;
        }
        if (_actionBarRoot == null) return;"""

    if "if (_actionBarController != null)" not in code:
        code = code.replace(bar_spells_target, bar_spells_replacement)

    # 3. AppendChatMessage delegation
    chat_target = """    void AppendChatMessage(string speaker, string message, int channel, string timeStr = "")
    {
        if (_chatRoot == null) return;"""

    chat_replacement = """    void AppendChatMessage(string speaker, string message, int channel, string timeStr = "")
    {
        if (_chatViewController != null)
        {
            _chatViewController.AppendMessage(speaker, message, channel, timeStr);
            if (_chatRoot != null) Gen_ChatView_OnNewChatMessage(_chatRoot);
            return;
        }
        if (_chatRoot == null) return;"""

    if "if (_chatViewController != null)" not in code:
        code = code.replace(chat_target, chat_replacement)

    # 4. SendChatMessage /fight command support
    send_chat_target = """    public void SendChatMessage(string text, int channel = 0)
    {
        if (string.IsNullOrEmpty(text) || _wsConn == null) return;"""

    send_chat_replacement = """    public void SendChatMessage(string text, int channel = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (text.Equals("/fight", StringComparison.OrdinalIgnoreCase))
        {
            StartCombatSimulation();
            AppendChatMessage("FightSystem", "Combat mode simulation initiated! Press Space or click 'End Turn' to pass turn.", 2);
            return;
        }
        if (_wsConn == null) return;"""

    if 'text.Equals("/fight"' not in code:
        code = code.replace(send_chat_target, send_chat_replacement)

    # 5. MountInGameHUD banner controller wiring
    banner_mount_target = """                _bannerRoot = bannerVta.CloneTree();
                _bannerMenuBinding = new GeneratedBannerMenuBinding();
                _bannerMenuBinding.Bind(_bannerRoot);"""

    banner_mount_replacement = """                _bannerRoot = bannerVta.CloneTree();
                _bannerMenuController = new BannerMenuController(_bannerRoot, LoadUxml);
                _bannerMenuBinding = _bannerMenuController.Binding;"""

    if "_bannerMenuController = new BannerMenuController" not in code:
        code = code.replace(banner_mount_target, banner_mount_replacement)

    # 6. MountInGameHUD chat controller wiring
    chat_mount_target = """                _chatRoot = chatVta.CloneTree();
                _chatviewBinding = new GeneratedChatviewBinding();
                _chatviewBinding.Bind(_chatRoot);"""

    chat_mount_replacement = """                _chatRoot = chatVta.CloneTree();
                _chatViewController = new ChatViewController(_chatRoot, LoadUxml);
                _chatviewBinding = _chatViewController.Binding;
                _chatViewController.OnSendMessage += t => SendChatMessage(t);"""

    if "_chatViewController = new ChatViewController" not in code:
        code = code.replace(chat_mount_target, chat_mount_replacement)

    # 7. MountInGameHUD charinfo controller wiring
    charinfo_mount_target = """                _characterInfoRoot = charInfoVta.CloneTree();
                _charInfoBinding = new GeneratedCharacterInformationsBinding();
                _charInfoBinding.Bind(_characterInfoRoot);"""

    charinfo_mount_replacement = """                _characterInfoRoot = charInfoVta.CloneTree();
                _characterInformationController = new CharacterInformationController(_characterInfoRoot, LoadUxml);
                _charInfoBinding = _characterInformationController.Binding;"""

    if "_characterInformationController = new CharacterInformationController" not in code:
        code = code.replace(charinfo_mount_target, charinfo_mount_replacement)

    # 8. MountInGameHUD actionbar controller wiring
    actionbar_mount_target = """                _actionBarRoot = barVta.CloneTree();
                _actionBarBinding = new GeneratedActionBarBinding();
                _actionBarBinding.Bind(_actionBarRoot);"""

    actionbar_mount_replacement = """                _actionBarRoot = barVta.CloneTree();
                _actionBarController = new UIActionBarController(_actionBarRoot, LoadUxml);
                _actionBarBinding = _actionBarController.Binding;"""

    if "_actionBarController = new UIActionBarController" not in code:
        code = code.replace(actionbar_mount_target, actionbar_mount_replacement)

    # 9. UpdateCharacterInfoStats delegation
    charinfo_stats_target = """    public void UpdateCharacterInfoStats(int currentHp, int maxHp, int currentAp, int currentMp, int shield = 0, int invocations = 1)
    {
        if (_characterInfoRoot == null) return;"""

    charinfo_stats_replacement = """    public void UpdateCharacterInfoStats(int currentHp, int maxHp, int currentAp, int currentMp, int shield = 0, int invocations = 1)
    {
        if (_characterInformationController != null)
        {
            _characterInformationController.UpdateStats(currentHp, maxHp, currentAp, currentMp, shield, invocations);
            return;
        }
        if (_characterInfoRoot == null) return;"""

    if "if (_characterInformationController != null)" not in code:
        code = code.replace(charinfo_stats_target, charinfo_stats_replacement)

    # 10. Add MountFightHUD and StartCombatSimulation methods
    mount_fight_methods = """
    public void MountFightHUD()
    {
        if (_fightHudMounted) return;
        _fightHudMounted = true;

        var colorMap = LoadThemeColorMap();
        var fontAsset = Resources.Load<FontAsset>("DefaultFont");

        // 1. FightTimeline (Top-Center of screen)
        try
        {
            VisualTreeAsset tlVta = LoadUxml("fighttimeline") ?? LoadUxml("timeline") ?? LoadUxml("Assets/UI/Fight/timeline/FightTimeline.uxml");
            if (tlVta != null)
            {
                _fightTimelineRoot = tlVta.CloneTree();
                _timelineController = new UITimelineController(_fightTimelineRoot, LoadUxml);
                _fightTimelineRoot.name = "FightTimeline_Root";
                _fightTimelineRoot.style.position = Position.Absolute;
                _fightTimelineRoot.style.top = 20;
                _fightTimelineRoot.style.left = Length.Percent(20);
                _fightTimelineRoot.style.right = Length.Percent(20);
                _fightTimelineRoot.style.height = 70;
                _fightTimelineRoot.pickingMode = PickingMode.Position;
                int tc = 0;
                ApplyThemeRecursive(_fightTimelineRoot, colorMap, ref tc);
                if (fontAsset != null) ApplyFontRecursive(_fightTimelineRoot, fontAsset);
                _uiRoot.Add(_fightTimelineRoot);
                Log("  [Combat] FightTimeline mounted via UITimelineController");
            }
        }
        catch (Exception ex)
        {
            Err($"  [Combat] FightTimeline mount error: {ex.Message}");
        }

        // 2. EndTurnWidget (Bottom-Right, above banner)
        try
        {
            VisualTreeAsset etVta = LoadUxml("endturnwidget") ?? LoadUxml("Assets/UI/Fight/EndTurnWidget.uxml");
            if (etVta != null)
            {
                _endTurnWidgetRoot = etVta.CloneTree();
                _endTurnWidgetController = new EndTurnWidgetController(_endTurnWidgetRoot, LoadUxml);
                _endTurnWidgetRoot.name = "EndTurnWidget_Root";
                _endTurnWidgetRoot.style.position = Position.Absolute;
                _endTurnWidgetRoot.style.bottom = 85;
                _endTurnWidgetRoot.style.right = 30;
                _endTurnWidgetRoot.style.width = 160;
                _endTurnWidgetRoot.style.height = 90;
                _endTurnWidgetRoot.pickingMode = PickingMode.Position;
                int tc = 0;
                ApplyThemeRecursive(_endTurnWidgetRoot, colorMap, ref tc);
                if (fontAsset != null) ApplyFontRecursive(_endTurnWidgetRoot, fontAsset);
                _uiRoot.Add(_endTurnWidgetRoot);

                _endTurnWidgetController.OnEndTurnClicked += () =>
                {
                    Log("  [Combat] EndTurn clicked!");
                    Jondo.Reconstructed.Fight.FightTurnService.Instance?.PassTurn();
                };

                Log("  [Combat] EndTurnWidget mounted via EndTurnWidgetController");
            }
        }
        catch (Exception ex)
        {
            Err($"  [Combat] EndTurnWidget mount error: {ex.Message}");
        }
    }

    public void StartCombatSimulation()
    {
        if (Jondo.Reconstructed.Fight.FightTurnService.Instance == null)
            new Jondo.Reconstructed.Fight.FightTurnService();

        Jondo.Reconstructed.Fight.FightTurnService.Instance.StartFight();
        MountFightHUD();

        if (_timelineController != null)
        {
            _timelineController.ClearFighters();
            _timelineController.AddFighterSlot(1, _accountNickname ?? "Hero", 55, 55, 0, true);
            _timelineController.AddFighterSlot(2, "Bouftou", 30, 30, 1, false);
            _timelineController.AddFighterSlot(3, "Tofu", 15, 15, 2, false);
        }

        Log("[Combat] Combat simulation started with 3 combatants and timeline active!");
    }
"""

    if "public void MountFightHUD()" not in code:
        code = code.replace("    Texture2D MakeReadable(Texture2D src)", mount_fight_methods + "\n    Texture2D MakeReadable(Texture2D src)")

    # 11. Update method combat ticker & KeyCode.F
    update_ticker_target = "        Jondo.Reconstructed.Fight.FightTurnService.Instance?.UpdateCountdown(Time.deltaTime);\n"
    update_ticker_replacement = """        Jondo.Reconstructed.Fight.FightTurnService.Instance?.UpdateCountdown(Time.deltaTime);

        if (Jondo.Reconstructed.Fight.FightTurnService.Instance != null && Jondo.Reconstructed.Fight.FightTurnService.Instance.IsInFight)
        {
            if (_endTurnWidgetController != null)
            {
                _endTurnWidgetController.UpdateTimer(Jondo.Reconstructed.Fight.FightTurnService.Instance.TurnTimeLeft);
                _endTurnWidgetController.UpdateRound(Jondo.Reconstructed.Fight.FightTurnService.Instance.CurrentTurn.round);
            }
            if (_timelineController != null)
            {
                _timelineController.UpdateTurn(
                    Jondo.Reconstructed.Fight.FightTurnService.Instance.CurrentTurn.round,
                    Jondo.Reconstructed.Fight.FightTurnService.Instance.CurrentTurn.order
                );
            }
        }

        if (Input.GetKeyDown(KeyCode.F) && _inGameHudMounted && (Jondo.Reconstructed.Fight.FightTurnService.Instance == null || !Jondo.Reconstructed.Fight.FightTurnService.Instance.IsInFight))
        {
            StartCombatSimulation();
        }
"""
    if "if (_endTurnWidgetController != null)" not in code:
        code = code.replace(update_ticker_target, update_ticker_replacement)

    with open(PROBE_PATH, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(code.replace("\n", "\r\n"))
    print("WebGLProbe.cs integrated successfully with decoupled HUD & Combat controllers.")


if __name__ == "__main__":
    integrate()
