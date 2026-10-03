// Auto-generated from decompiled_all.c via generate_probe_methods.py
// DO NOT EDIT — regenerate with: py Tools/ClientAnalyzer/generate_probe_methods.py
using UnityEngine;
using UnityEngine.UIElements;

public partial class WebGLProbe
{
    void SetElementText(VisualElement root, string elementName, string text)
    {
        var el = root.Q(elementName);
        if (el == null) return;
        if (el is Label lbl) lbl.text = text;
        else if (el is Button btn) btn.text = text;
        else
        {
            var inner = el.Q("playButton_label");
            if (inner is Label il) il.text = text;
        }
    }

    // Generated from decompiled fys.bjdo
    // 241 lines → 5 ops
    void Gen_Login_bjdo(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.gdj
    // 299 lines → 6 ops
    void Gen_Login_gdj(VisualElement root)
    {
        // CALL fys.bjdl
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.mx
    // 317 lines → 12 ops
    void Gen_Login_mx(VisualElement root)
    {
        // CALL fys.bjdh
        // SET_TEXT on lbl_message (dynamic text from data)
        // CALL fys.bjdh
    }

    // Generated from decompiled fys.bjdp
    // 295 lines → 5 ops
    void Gen_Login_bjdp(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.bjdk
    // 299 lines → 6 ops
    void Gen_Login_bjdk(VisualElement root)
    {
        // CALL fys.bjdl
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.bjdm
    // 317 lines → 12 ops
    void Gen_Login_bjdm(VisualElement root)
    {
        // CALL fys.bjdh
        // SET_TEXT on lbl_message (dynamic text from data)
        // SET_TEXT on lbl_message (dynamic text from data)
        // CALL fys.bjdh
    }

    // Generated from decompiled fys.ogx
    // 207 lines → 6 ops
    void Gen_Login_ogx(VisualElement root)
    {
    }

    // Generated from decompiled fys.jei
    // 299 lines → 6 ops
    void Gen_Login_jei(VisualElement root)
    {
        // CALL fys.bjdl
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.OnEnable
    // 473 lines → 10 ops
    void Gen_Login_OnEnable(VisualElement root)
    {
        // CALL fys.bjds
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.lgs
    // 299 lines → 6 ops
    void Gen_Login_lgs(VisualElement root)
    {
        // CALL fys.bjdl
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.fdk
    // 207 lines → 6 ops
    void Gen_Login_fdk(VisualElement root)
    {
    }

    // Generated from decompiled fys.bjdt
    // 299 lines → 6 ops
    void Gen_Login_bjdt(VisualElement root)
    {
        // CALL fys.bjdl
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.jus
    // 207 lines → 6 ops
    void Gen_Login_jus(VisualElement root)
    {
    }

    // Generated from decompiled fys.mft
    // 295 lines → 5 ops
    void Gen_Login_mft(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.oqh
    // 241 lines → 5 ops
    void Gen_Login_oqh(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.eqo
    // 317 lines → 12 ops
    void Gen_Login_eqo(VisualElement root)
    {
        // CALL fys.bjdh
        // SET_TEXT on lbl_message (dynamic text from data)
        // CALL fys.bjdh
    }

    // Generated from decompiled fys.cev
    // 317 lines → 12 ops
    void Gen_Login_cev(VisualElement root)
    {
        // CALL fys.bjdh
        // SET_TEXT on lbl_message (dynamic text from data)
        // CALL fys.bjdh
    }

    // Generated from decompiled fys.ctor
    // 319 lines → 14 ops
    void Gen_Login_ctor(VisualElement root)
    {
        // EVENT: btn_play callback (paw)
        SetElementText(root, "btn_play", I18n("ui.common.play"));
        // CALL fys.bjdj
        // CALL fys.bjdh
        // CALL fys.bjdn
        // SET_TEXT on lbl_message (dynamic text from data)
        // SET_TEXT on lbl_message (dynamic text from data)
        // CALL fys.bjdh
    }

    // Generated from decompiled fys.nso
    // 295 lines → 5 ops
    void Gen_Login_nso(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.odi
    // 317 lines → 12 ops
    void Gen_Login_odi(VisualElement root)
    {
        // CALL fys.bjdh
        // SET_TEXT on lbl_message (dynamic text from data)
        // CALL fys.bjdh
    }

    // Generated from decompiled fys.fig
    // 299 lines → 6 ops
    void Gen_Login_fig(VisualElement root)
    {
        // CALL fys.bjdl
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.irx
    // 295 lines → 5 ops
    void Gen_Login_irx(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.bjdn
    // 207 lines → 6 ops
    void Gen_Login_bjdn(VisualElement root)
    {
    }

    // Generated from decompiled fys.lef
    // 295 lines → 5 ops
    void Gen_Login_lef(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled fys.nqz
    // 207 lines → 6 ops
    void Gen_Login_nqz(VisualElement root)
    {
    }

    // Generated from decompiled ServerSelection.set_m_friendSearchButtonEnableState
    // 153 lines → 8 ops
    void Gen_ServerSelection_set_m_friendSearchButtonEnableState(VisualElement root)
    {
        { var el = root.Q("btn_search"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_search"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled ServerSelection.ctor
    // 2599 lines → 83 ops
    void Gen_ServerSelection_ctor(VisualElement root)
    {
        { var el = root.Q("head_character"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: head_character")); }
        { var el = root.Q("head_name"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: head_name")); }
        { var el = root.Q("head_community"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: head_community")); }
        { var el = root.Q("head_friends"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: head_friends")); }
        // EVENT: btn_search callback (paw)
        // EVENT: btn_freeAccount callback (paw)
        // EVENT: btn_play callback (paw)
        SetElementText(root, "btn_play", I18n("ui.common.play"));
        // EVENT: btn_friends callback (paw)
        { var el = root.Q("cb_onlyCharacter"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: cb_onlyCharacter")); }
    }

    // Generated from decompiled ServerSelection.FilterOnlyCharacter
    // 559 lines → 6 ops
    void Gen_ServerSelection_FilterOnlyCharacter(VisualElement root)
    {
    }

    // Generated from decompiled ServerSelection.Localize
    // 315 lines → 34 ops
    void Gen_ServerSelection_Localize(VisualElement root)
    {
        SetElementText(root, "lbl_noServer", I18n("ui.sersel.noServer"));
        { var el = root.Q("inp_search"); if (el is TextField tf) tf.label = I18n("ui.sersel.findFriend"); }
        SetElementText(root, "lbl_title", I18n("ui.sersel.title").ToUpper());
        SetElementText(root, "btn_freeAccount", I18n("ui.forceAccount.freeAccount"));
        SetElementText(root, "lbl_errorForce", I18n("ui.forceAccount.wrongId"));
        SetElementText(root, "lbl_warning", I18n("ui.serverSelection.characterWarning"));
        // SET_TEXT on lbl_warning (dynamic text from data)
    }

    // Generated from decompiled ServerSelection.OnSelectionChanged
    // 545 lines → 10 ops
    void Gen_ServerSelection_OnSelectionChanged(VisualElement root)
    {
    }

    // Generated from decompiled ServerSelection.OnServerFriendList
    // 87 lines → 5 ops
    void Gen_ServerSelection_OnServerFriendList(VisualElement root)
    {
        { var el = root.Q("btn_friends"); if (el != null) el.SetEnabled(false); }
        // CALL ServerSelection.UpdateDisplayProvider
        // CALL ServerSelection.OnUpdateSearchTimerCompleted
    }

    // Generated from decompiled ServerSelection.OnFriendSelected
    // 257 lines → 9 ops
    void Gen_ServerSelection_OnFriendSelected(VisualElement root)
    {
        // CALL ServerSelection.CheckSearchValidity
        { var el = root.Q("ctr_searchResult"); if (el != null) el.style.display = DisplayStyle.None; }
        { var el = root.Q("lbl_errorSearch"); if (el != null) el.style.display = DisplayStyle.None; }
    }

    // Generated from decompiled ServerSelection.InitServer
    // 299 lines → 4 ops
    void Gen_ServerSelection_InitServer(VisualElement root)
    {
        // DISPLAY ctr_warning = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_warning"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled ServerSelection.DisplayFilter
    // 689 lines → 11 ops
    void Gen_ServerSelection_DisplayFilter(VisualElement root)
    {
        // CALL ServerSelection.CreateTabFilter
        { var el = root.Q("icon_left"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // CALL ServerSelection.FilterTab
    }

    // Generated from decompiled ServerSelection.FilterTab
    // 419 lines → 5 ops
    void Gen_ServerSelection_FilterTab(VisualElement root)
    {
        // DISPLAY cb_onlyCharacter = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("cb_onlyCharacter"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled ServerSelection.Scroll
    // 277 lines → 10 ops
    void Gen_ServerSelection_Scroll(VisualElement root)
    {
        { var el = root.Q("sv_filter"); if (el != null) el.style.visibility = Visibility.Hidden; }
        { var el = root.Q("sv_filter"); if (el != null) el.style.visibility = Visibility.Hidden; }
    }

    // Generated from decompiled ServerSelection.DisplayServer
    // 711 lines → 11 ops
    void Gen_ServerSelection_DisplayServer(VisualElement root)
    {
        // DISPLAY lbl_noServer = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("lbl_noServer"); if (el != null) el.style.display = DisplayStyle.Flex; }
        // DISPLAY gd_searchResults = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("gd_searchResults"); if (el != null) el.style.display = DisplayStyle.Flex; }
        // DISPLAY ctr_columns = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_columns"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled ServerSelection.BindSearchResult
    // 263 lines → 10 ops
    void Gen_ServerSelection_BindSearchResult(VisualElement root)
    {
        // SET_TEXT on lbl_resultName (dynamic text from data)
        // SET_TEXT on lbl_resultTag (dynamic text from data)
        // EVENT: ctr_resultFriend callback (paw)
    }

    // Generated from decompiled ServerSelection.OnEnable
    // 1075 lines → 11 ops
    void Gen_ServerSelection_OnEnable(VisualElement root)
    {
        // CALL ServerSelection.Localize
        // DISPLAY ctr_force = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_force"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("ctr_header"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("ctr_listMode"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled ServerSelection.SpawnEffect
    // 215 lines → 5 ops
    void Gen_ServerSelection_SpawnEffect(VisualElement root)
    {
        { var el = root.Q("ctr_header"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("ctr_listMode"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled ServerSelection.HideEffect
    // 215 lines → 5 ops
    void Gen_ServerSelection_HideEffect(VisualElement root)
    {
        { var el = root.Q("ctr_header"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled ServerSelection.OnValidUI
    // 447 lines → 14 ops
    void Gen_ServerSelection_OnValidUI(VisualElement root)
    {
        // CALL ServerSelection.CheckSearchValidity
        { var el = root.Q("btn_search"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("inp_search"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled ServerSelection.OnForceAccountStatus
    // 231 lines → 9 ops
    void Gen_ServerSelection_OnForceAccountStatus(VisualElement root)
    {
        { var el = root.Q("ctr_forceAccount"); if (el != null) el.style.display = DisplayStyle.None; }
        // DISPLAY ctr_freeAccount = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_freeAccount"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("lbl_errorForce"); if (el != null) el.style.display = DisplayStyle.None; }
        SetElementText(root, "lbl_accountName", I18n("ui.forceAccount.accountFullName"));
    }

    // Generated from decompiled ServerSelection.SwapForceMode
    // 155 lines → 5 ops
    void Gen_ServerSelection_SwapForceMode(VisualElement root)
    {
        { var el = root.Q("ctr_forceAccount"); if (el != null) el.style.display = DisplayStyle.None; }
        // DISPLAY ctr_freeAccount = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_freeAccount"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled ServerSelection.StartSearch
    // 277 lines → 9 ops
    void Gen_ServerSelection_StartSearch(VisualElement root)
    {
        // CALL ServerSelection.CheckSearchValidity
        { var el = root.Q("btn_search"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("inp_search"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled ServerSelection.OnAcquaintanceServerList
    // 239 lines → 9 ops
    void Gen_ServerSelection_OnAcquaintanceServerList(VisualElement root)
    {
        { var el = root.Q("ctr_searchResult"); if (el != null) el.style.display = DisplayStyle.None; }
        { var el = root.Q("lbl_errorSearch"); if (el != null) el.style.display = DisplayStyle.None; }
        { var el = root.Q("inp_search"); if (el != null) el.SetEnabled(false); }
        // CALL ServerSelection.CheckSearchValidity
    }

    // Generated from decompiled ServerSelection.EndSearch
    // 213 lines → 13 ops
    void Gen_ServerSelection_EndSearch(VisualElement root)
    {
        { var el = root.Q("inp_search"); if (el != null) el.SetEnabled(false); }
        // CALL ServerSelection.CheckSearchValidity
        { var el = root.Q("btn_search"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_search"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled ServerSelection.OnAcquaintanceServersError
    // 977 lines → 20 ops
    void Gen_ServerSelection_OnAcquaintanceServersError(VisualElement root)
    {
        SetElementText(root, "lbl_errorSearch", I18n("ui.sersel.searchError.no_result"));
        { var el = root.Q("lbl_errorSearch"); if (el != null) el.style.display = DisplayStyle.Flex; }
        // CALL ServerSelection.EndSearch
        // CALL ServerSelection.CreateTabFilter
        { var el = root.Q("icon_left"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // CALL ServerSelection.FilterTab
    }

    // Generated from decompiled ServerSelection.OnTimeOut
    // 711 lines → 12 ops
    void Gen_ServerSelection_OnTimeOut(VisualElement root)
    {
        // CALL ServerSelection.EndSearch
        // CALL ServerSelection.CreateTabFilter
        { var el = root.Q("icon_left"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // CALL ServerSelection.FilterTab
    }

    // Generated from decompiled ServerSelection.CancelSearch
    // 711 lines → 12 ops
    void Gen_ServerSelection_CancelSearch(VisualElement root)
    {
        // CALL ServerSelection.EndSearch
        // CALL ServerSelection.CreateTabFilter
        { var el = root.Q("icon_left"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // CALL ServerSelection.FilterTab
    }

    // Generated from decompiled ServerSelection.OnUpdateSearchTimerCompleted
    // 593 lines → 21 ops
    void Gen_ServerSelection_OnUpdateSearchTimerCompleted(VisualElement root)
    {
        // CALL ServerSelection.CheckSearchValidity
        { var el = root.Q("ctr_searchResult"); if (el != null) el.style.display = DisplayStyle.None; }
        { var el = root.Q("lbl_errorSearch"); if (el != null) el.style.display = DisplayStyle.None; }
        // CALL ServerSelection.EndSearch
        // DISPLAY ctr_searchResult = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_searchResult"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled ServerSelection.OnServerConnectionStarted
    // 277 lines → 10 ops
    void Gen_ServerSelection_OnServerConnectionStarted(VisualElement root)
    {
        { var el = root.Q("btn_moveLeft"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_moveRight"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("lv_server"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled ServerSelection.OnSelectedServerRefused
    // 87 lines → 2 ops
    void Gen_ServerSelection_OnSelectedServerRefused(VisualElement root)
    {
        { var el = root.Q("lv_server"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterSelection.ctor
    // 831 lines → 25 ops
    void Gen_CharacterSelection_ctor(VisualElement root)
    {
        { var el = root.Q("head_class"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: head_class")); }
        { var el = root.Q("head_level"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: head_level")); }
        { var el = root.Q("head_name"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: head_name")); }
        // EVENT: btn_play callback (paw)
        // EVENT: btn_createCharacter callback (paw)
        // EVENT: ed_main ? ()
        // EVENT: ed_main ? ()
        // EVENT: ed_main ? ()
        // EVENT: ed_main ? ()
        // EVENT: dd_servers callback (paw)
    }

    // Generated from decompiled CharacterSelection.Localize
    // 197 lines → 17 ops
    void Gen_CharacterSelection_Localize(VisualElement root)
    {
        SetElementText(root, "lbl_title", I18n("ui.charsel.onServer"));
        SetElementText(root, "btn_createCharacter", I18n("ui.charsel.createCharacter"));
    }

    // Generated from decompiled CharacterSelection.OnPressEnter
    // 499 lines → 5 ops
    void Gen_CharacterSelection_OnPressEnter(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterSelection.ValidateCharacterChoice
    // 489 lines → 5 ops
    void Gen_CharacterSelection_ValidateCharacterChoice(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterSelection.InitServersList
    // 561 lines → 6 ops
    void Gen_CharacterSelection_InitServersList(VisualElement root)
    {
        { var el = root.Q("dd_servers"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterSelection.OnSelectionChanged
    // 709 lines → 23 ops
    void Gen_CharacterSelection_OnSelectionChanged(VisualElement root)
    {
        // CALL CharacterSelection.SetSelection
        SetElementText(root, "btn_play", I18n("ui.common.playAgain"));
        SetElementText(root, "btn_play", I18n("ui.common.play"));
        // DISPLAY tx_bonusEpic = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("tx_bonusEpic"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled CharacterSelection.SetSelection
    // 217 lines → 2 ops
    void Gen_CharacterSelection_SetSelection(VisualElement root)
    {
    }

    // Generated from decompiled CharacterSelection.BindItem
    // 1085 lines → 47 ops
    void Gen_CharacterSelection_BindItem(VisualElement root)
    {
        // EVENT: btn_delete callback (paw)
        { var el = root.Q("tx_warning"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("tx_warning"); if (el != null) el.style.display = DisplayStyle.None; }
        // EVENT: ctr_characterLine callback (pay)
        // SET_TEXT on lbl_class (dynamic text from data)
        // SET_TEXT on lbl_level (dynamic text from data)
        // SET_TEXT on lbl_level (dynamic text from data)
        // SET_TEXT on lbl_level (dynamic text from data)
    }

    // Generated from decompiled CharacterSelection.DisplayCharacter
    // 169 lines → 10 ops
    void Gen_CharacterSelection_DisplayCharacter(VisualElement root)
    {
        { var el = root.Q("ed_character"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("ed_character_notPlayable"); if (el != null) el.style.display = DisplayStyle.None; }
        { var el = root.Q("ed_character"); if (el != null) el.style.display = DisplayStyle.None; }
        { var el = root.Q("ed_character_notPlayable"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled CharacterSelection.Init
    // 577 lines → 15 ops
    void Gen_CharacterSelection_Init(VisualElement root)
    {
        // CALL CharacterSelection.GetServerWrapper
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterSelection.CreateDataProvider
    // 535 lines → 13 ops
    void Gen_CharacterSelection_CreateDataProvider(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterSelection.DisplayCharacters
    // 381 lines → 4 ops
    void Gen_CharacterSelection_DisplayCharacters(VisualElement root)
    {
    }

    // Generated from decompiled CharacterSelection.OnEnable
    // 1079 lines → 27 ops
    void Gen_CharacterSelection_OnEnable(VisualElement root)
    {
        // CALL CharacterSelection.Localize
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        // SET_TEXT on lbl_serverName (dynamic text from data)
        // SET_TEXT on lbl_serverName (dynamic text from data)
        { var el = root.Q("leftPart"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("ctr_infoMain"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("socle"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("ctr_entity"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled CharacterSelection.EnableEffect
    // 515 lines → 20 ops
    void Gen_CharacterSelection_EnableEffect(VisualElement root)
    {
        { var el = root.Q("leftPart"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("ctr_infoMain"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("socle"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("ctr_entity"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled CharacterSelection.UpdateCreateButton
    // 457 lines → 11 ops
    void Gen_CharacterSelection_UpdateCreateButton(VisualElement root)
    {
        { var el = root.Q("btn_createCharacter"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_createCharacter"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: btn_createCharacter")); }
    }

    // Generated from decompiled CharacterSelection.OnCharacterImpossibleSelection
    // 201 lines → 5 ops
    void Gen_CharacterSelection_OnCharacterImpossibleSelection(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled CharacterSelection.OnCharacterDeletionError
    // 145 lines → 4 ops
    void Gen_CharacterSelection_OnCharacterDeletionError(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled CharacterSelection.OnCharactersListUpdated
    // 79 lines → 4 ops
    void Gen_CharacterSelection_OnCharactersListUpdated(VisualElement root)
    {
        // CALL CharacterSelection.InitServersList
        // SET_TEXT on lbl_serverName (dynamic text from data)
        // SET_TEXT on lbl_serverName (dynamic text from data)
    }

    // Generated from decompiled CharacterSelection.DisplayEntity
    // 137 lines → 1 ops
    void Gen_CharacterSelection_DisplayEntity(VisualElement root)
    {
    }

    // Generated from decompiled CharacterSelection.UpdateSelectedCharacter
    // 689 lines → 22 ops
    void Gen_CharacterSelection_UpdateSelectedCharacter(VisualElement root)
    {
        SetElementText(root, "btn_play", I18n("ui.common.playAgain"));
        SetElementText(root, "btn_play", I18n("ui.common.play"));
        // DISPLAY tx_bonusEpic = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("tx_bonusEpic"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled CharacterCreation.ctor
    // 905 lines → 4 ops
    void Gen_CharacterCreation_ctor(VisualElement root)
    {
        // EVENT: btnLeave callback (paw)
        // CALL CharacterCreation.InitializeUI
    }

    // Generated from decompiled CharacterCreation.SetData
    // 1035 lines → 14 ops
    void Gen_CharacterCreation_SetData(VisualElement root)
    {
        // CALL CharacterCreation.LocalizeTexts
        // CALL CharacterCreation.InitializeBinding
        // CALL CharacterCreation.InitializeBreeds
        // CALL CharacterCreation.InitializeTooltips
    }

    // Generated from decompiled CharacterCreation.InitializeUI
    // 1033 lines → 14 ops
    void Gen_CharacterCreation_InitializeUI(VisualElement root)
    {
        // CALL CharacterCreation.LocalizeTexts
        // CALL CharacterCreation.InitializeBinding
        // CALL CharacterCreation.InitializeBreeds
        // CALL CharacterCreation.InitializeTooltips
    }

    // Generated from decompiled CharacterCreation.LocalizeTexts
    // 469 lines → 62 ops
    void Gen_CharacterCreation_LocalizeTexts(VisualElement root)
    {
        SetElementText(root, "lbl_title", I18n("ui.charsel.onServer"));
        SetElementText(root, "lbl_breedTitle", I18n("ui.charcrea.breed"));
        // SET_TEXT on lbl_breedTitle (dynamic text from data)
        SetElementText(root, "lbl_playerNameTitle", I18n("ui.common.characterName"));
        // SET_TEXT on lbl_playerNameTitle (dynamic text from data)
        SetElementText(root, "lbl_corps", I18n("ui.charcrea.body"));
        // SET_TEXT on lbl_corps (dynamic text from data)
        SetElementText(root, "lbl_visage", I18n("ui.charcrea.face"));
        // SET_TEXT on lbl_visage (dynamic text from data)
        SetElementText(root, "lbl_colorSkin", I18n("ui.charcrea.skin"));
        // SET_TEXT on lbl_colorSkin (dynamic text from data)
        SetElementText(root, "lbl_colorHair", I18n("ui.charcrea.hair"));
        // SET_TEXT on lbl_colorCloth4 (dynamic text from data)
        SetElementText(root, "lbl_attitudes", I18n("ui.common.emotes"));
        { var el = root.Q("window_spells"); if (el is Label lbl) lbl.text = I18n("ui.common.breedSpell"); }
        SetElementText(root, "btn_showSpells", I18n("ui.charcrea.spells"));
        SetElementText(root, "btn_mechanics", I18n("ui.grimoire.mecanics"));
        SetElementText(root, "btn_play", I18n("ui.common.play"));
        SetElementText(root, "lbl_stuffs", I18n("ui.common.equipments"));
        // SET_TEXT on lbl_stuffs (dynamic text from data)
    }

    // Generated from decompiled CharacterCreation.InitializeBinding
    // 3791 lines → 134 ops
    void Gen_CharacterCreation_InitializeBinding(VisualElement root)
    {
        // EVENT: dd_servers callback (paw)
        // CALL CharacterCreation.ResetFilters
        // EVENT: btn_randomBreed callback (paw)
        // EVENT: btn_randomizeColorSet callback (paw)
        // EVENT: btn_previous callback (paw)
        // EVENT: btn_next callback (paw)
        // EVENT: btn_rotateLeft callback (paw)
        // EVENT: btn_rotateRight callback (paw)
        // EVENT: btn_generateName callback (paw)
        // EVENT: btn_mechanics callback (paw)
        // EVENT: btn_showSpells callback (paw)
        // EVENT: window_spells callback (paw)
        // EVENT: btn_resetColorSet callback (paw)
        { var el = root.Q("btn_mechanics"); if (el != null) el.SetEnabled(false); }
        // EVENT: btn_stuff1 ? ()
        { var el = root.Q("btn_stuff1"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: btn_stuff1")); }
        // EVENT: btn_stuff2 ? ()
        { var el = root.Q("btn_stuff2"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: btn_stuff2")); }
        // EVENT: btn_stuff3 ? ()
        { var el = root.Q("btn_stuff3"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: btn_stuff3")); }
    }

    // Generated from decompiled CharacterCreation.ToggleSpells
    // 75 lines → 4 ops
    void Gen_CharacterCreation_ToggleSpells(VisualElement root)
    {
        // DISPLAY window_spells = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("window_spells"); if (el != null) el.style.display = DisplayStyle.Flex; }
        // CALL CharacterCreation.DisplayBreedSpells
    }

    // Generated from decompiled CharacterCreation.FilterBreed
    // 255 lines → 2 ops
    void Gen_CharacterCreation_FilterBreed(VisualElement root)
    {
        // CALL CharacterCreation.ResetFilters
    }

    // Generated from decompiled CharacterCreation.ResetFilters
    // 143 lines → 1 ops
    void Gen_CharacterCreation_ResetFilters(VisualElement root)
    {
    }

    // Generated from decompiled CharacterCreation.OnEnable
    // 1037 lines → 22 ops
    void Gen_CharacterCreation_OnEnable(VisualElement root)
    {
        // CALL CharacterCreation.Reset
        // CALL CharacterCreation.EnableTweeners
        // CALL CharacterCreation.InitServersList
        // SET_TEXT on lbl_serverName (dynamic text from data)
        // SET_TEXT on lbl_serverName (dynamic text from data)
        // CALL CharacterCreation.StopTweensSpawn
        { var el = root.Q("leftPart"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("rightPart"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("middlePart"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled CharacterCreation.SpawnEffect
    // 595 lines → 16 ops
    void Gen_CharacterCreation_SpawnEffect(VisualElement root)
    {
        // CALL CharacterCreation.StopTweensSpawn
        { var el = root.Q("leftPart"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("rightPart"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("middlePart"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled CharacterCreation.UpdateUIBySelectedMode
    // 919 lines → 24 ops
    void Gen_CharacterCreation_UpdateUIBySelectedMode(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(true); }
        // CALL CharacterCreation.BindBreedsSlots
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.ModifyCharacterSetUp
    // 1835 lines → 70 ops
    void Gen_CharacterCreation_ModifyCharacterSetUp(VisualElement root)
    {
        // SET_TEXT on input_name (dynamic text from data)
        // DISPLAY parentChangeServer = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("parentChangeServer"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("btnLeave"); if (el != null) el.style.display = DisplayStyle.None; }
        SetElementText(root, "btn_leaveModif", I18n("ui.common.cancel"));
        { var el = root.Q("breedsParent"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_next"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_previous"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("switch_gender"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_heads"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("bloc_customization"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_colors"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("input_name"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_generateName"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("switch_gender"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("breedsParent"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_previous"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_next"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("input_name"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_generateName"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("ctr_colors"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("bloc_customization"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_heads"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("breedsParent"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_next"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_previous"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("switch_gender"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_heads"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("bloc_customization"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_colors"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("input_name"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_generateName"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.Reset
    // 387 lines → 13 ops
    void Gen_CharacterCreation_Reset(VisualElement root)
    {
        { var el = root.Q("breedsParent"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_previous"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_next"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("switch_gender"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_heads"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("bloc_customization"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.SetUpGenderUI
    // 51 lines → 2 ops
    void Gen_CharacterCreation_SetUpGenderUI(VisualElement root)
    {
        { var el = root.Q("switch_gender"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.SetUpBreedUI
    // 85 lines → 6 ops
    void Gen_CharacterCreation_SetUpBreedUI(VisualElement root)
    {
        { var el = root.Q("breedsParent"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_previous"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_next"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.SetUpNameUI
    // 387 lines → 10 ops
    void Gen_CharacterCreation_SetUpNameUI(VisualElement root)
    {
        { var el = root.Q("input_name"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_generateName"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.SetUpColorsUI
    // 51 lines → 2 ops
    void Gen_CharacterCreation_SetUpColorsUI(VisualElement root)
    {
        { var el = root.Q("ctr_colors"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.SetUpCosmeticUI
    // 63 lines → 4 ops
    void Gen_CharacterCreation_SetUpCosmeticUI(VisualElement root)
    {
        { var el = root.Q("bloc_customization"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("ctr_heads"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.SetUpAll
    // 531 lines → 24 ops
    void Gen_CharacterCreation_SetUpAll(VisualElement root)
    {
        { var el = root.Q("breedsParent"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_next"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_previous"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("switch_gender"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_heads"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("bloc_customization"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_colors"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("input_name"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_generateName"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.DisableAll
    // 531 lines → 24 ops
    void Gen_CharacterCreation_DisableAll(VisualElement root)
    {
        { var el = root.Q("breedsParent"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_next"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_previous"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("switch_gender"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_heads"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("bloc_customization"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("ctr_colors"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("input_name"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_generateName"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.ResetInputName
    // 917 lines → 10 ops
    void Gen_CharacterCreation_ResetInputName(VisualElement root)
    {
    }

    // Generated from decompiled CharacterCreation.BindBreedsSlots
    // 517 lines → 6 ops
    void Gen_CharacterCreation_BindBreedsSlots(VisualElement root)
    {
    }

    // Generated from decompiled CharacterCreation.BindHeadsSlots
    // 343 lines → 5 ops
    void Gen_CharacterCreation_BindHeadsSlots(VisualElement root)
    {
    }

    // Generated from decompiled CharacterCreation.RefreshBreedInfo
    // 291 lines → 15 ops
    void Gen_CharacterCreation_RefreshBreedInfo(VisualElement root)
    {
        // SET_TEXT on lbl_breed (dynamic text from data)
        // SET_TEXT on lbl_breed (dynamic text from data)
        // SET_TEXT on lbl_archetype (dynamic text from data)
        // SET_TEXT on lbl_archetype (dynamic text from data)
        // SET_TEXT on lbl_breedInfo (dynamic text from data)
        // SET_TEXT on lbl_breedInfo (dynamic text from data)
        // CALL CharacterCreation.DisplayRoleIcons
        // CALL CharacterCreation.DisplayLimitedSpells
        // CALL CharacterCreation.DisplayHeads
        // CALL CharacterCreation.DisplayBreedSpells
    }

    // Generated from decompiled CharacterCreation.DisplayRoleIcons
    // 527 lines → 3 ops
    void Gen_CharacterCreation_DisplayRoleIcons(VisualElement root)
    {
    }

    // Generated from decompiled CharacterCreation.InitServersList
    // 561 lines → 6 ops
    void Gen_CharacterCreation_InitServersList(VisualElement root)
    {
        { var el = root.Q("dd_servers"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.OnCharacterCreationResult
    // 381 lines → 12 ops
    void Gen_CharacterCreation_OnCharacterCreationResult(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_generateName"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled CharacterCreation.OnCharacterNameSuggested
    // 319 lines → 4 ops
    void Gen_CharacterCreation_OnCharacterNameSuggested(VisualElement root)
    {
        // DISPLAY input_name = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("input_name"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled CharacterCreation.OnCharacterImpossibleSelection
    // 191 lines → 4 ops
    void Gen_CharacterCreation_OnCharacterImpossibleSelection(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled CharacterCreation.DisplayComplexity
    // 73 lines → 1 ops
    void Gen_CharacterCreation_DisplayComplexity(VisualElement root)
    {
    }

    // Generated from decompiled CharacterCreation.OnInput
    // 1095 lines → 10 ops
    void Gen_CharacterCreation_OnInput(VisualElement root)
    {
        // SET_TEXT on input_name (dynamic text from data)
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.CreateCharacter
    // 699 lines → 12 ops
    void Gen_CharacterCreation_CreateCharacter(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled CharacterCreation.<InitializeBinding>b__78_31
    // 75 lines → 4 ops
    void Gen_CharacterCreation__InitializeBinding_b__78_31(VisualElement root)
    {
        // DISPLAY window_spells = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("window_spells"); if (el != null) el.style.display = DisplayStyle.Flex; }
        // CALL CharacterCreation.DisplayBreedSpells
    }

    // Generated from decompiled CharacterCreation.<InitializeBinding>b__78_32
    // 75 lines → 4 ops
    void Gen_CharacterCreation__InitializeBinding_b__78_32(VisualElement root)
    {
        // DISPLAY window_spells = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("window_spells"); if (el != null) el.style.display = DisplayStyle.Flex; }
        // CALL CharacterCreation.DisplayBreedSpells
    }

    // Generated from decompiled fyv.ctor
    // 511 lines → 37 ops
    void Gen_PreGameMainMenu_ctor(VisualElement root)
    {
        { var el = root.Q("preGameMainMenu"); if (el is Label lbl) lbl.text = I18n("ui.common.mainMenu"); }
        SetElementText(root, "btn_options", I18n("ui.common.options"));
        SetElementText(root, "btn_quit", I18n("ui.common.quitGame"));
        SetElementText(root, "btn_close", I18n("ui.common.cancel"));
        SetElementText(root, "btn_sub", I18n("ui.header.subscribe"));
        // SET_TEXT on lbl_buildNumber (dynamic text from data)
        // EVENT: tx_shield callback (paw)
    }

    // Generated from decompiled CharacterHeader.ctor
    // 415 lines → 16 ops
    void Gen_CharacterHeader_ctor(VisualElement root)
    {
        // EVENT: btn_subscribe ? ()
        { var el = root.Q("btn_subscribe"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: btn_subscribe")); }
        // DISPLAY ctr_admin = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_admin"); if (el != null) el.style.display = DisplayStyle.Flex; }
        SetElementText(root, "btn_subscribe", I18n("ui.header.subscribe"));
        SetElementText(root, "lbl_admin", I18n("ui.common.admin"));
        // SET_TEXT on lbl_admin (dynamic text from data)
    }

    // Generated from decompiled CharacterHeader.OnEnable
    // 385 lines → 15 ops
    void Gen_CharacterHeader_OnEnable(VisualElement root)
    {
        // SET_TEXT on lbl_name (dynamic text from data)
        // SET_TEXT on lbl_name (dynamic text from data)
        // SET_TEXT on lbl_tag (dynamic text from data)
        // DISPLAY ctr_admin = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_admin"); if (el != null) el.style.display = DisplayStyle.Flex; }
        SetElementText(root, "lbl_subscription", I18n("ui.common.infiniteSubscription"));
        // SET_TEXT on lbl_subscription (dynamic text from data)
    }

    // Generated from decompiled CharacterHeader.Toggle
    // 131 lines → 5 ops
    void Gen_CharacterHeader_Toggle(VisualElement root)
    {
        { var el = root.Q("CharacterHeader_Container"); if (el != null) el.style.visibility = Visibility.Hidden; }
        { var el = root.Q("btn_subscribe"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // CALL CharacterHeader.PlayerDisplay
    }

    // Generated from decompiled CharacterHeader.ShowHeader
    // 107 lines → 5 ops
    void Gen_CharacterHeader_ShowHeader(VisualElement root)
    {
        { var el = root.Q("CharacterHeader_Container"); if (el != null) el.style.visibility = Visibility.Hidden; }
        { var el = root.Q("btn_subscribe"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // CALL CharacterHeader.PlayerDisplay
    }

    // Generated from decompiled CharacterHeader.OnServersList
    // 369 lines → 15 ops
    void Gen_CharacterHeader_OnServersList(VisualElement root)
    {
        // SET_TEXT on lbl_name (dynamic text from data)
        // SET_TEXT on lbl_name (dynamic text from data)
        // SET_TEXT on lbl_tag (dynamic text from data)
        // DISPLAY ctr_admin = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_admin"); if (el != null) el.style.display = DisplayStyle.Flex; }
        SetElementText(root, "lbl_subscription", I18n("ui.common.infiniteSubscription"));
        // SET_TEXT on lbl_subscription (dynamic text from data)
    }

    // Generated from decompiled CharacterHeader.PlayerDisplay
    // 369 lines → 15 ops
    void Gen_CharacterHeader_PlayerDisplay(VisualElement root)
    {
        // SET_TEXT on lbl_name (dynamic text from data)
        // SET_TEXT on lbl_name (dynamic text from data)
        // SET_TEXT on lbl_tag (dynamic text from data)
        // DISPLAY ctr_admin = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_admin"); if (el != null) el.style.display = DisplayStyle.Flex; }
        SetElementText(root, "lbl_subscription", I18n("ui.common.infiniteSubscription"));
        // SET_TEXT on lbl_subscription (dynamic text from data)
    }

    // Lifecycle-aware dispatch — replaces flat ApplyGeneratedMethods.
    // Phases match the decompiled handler lifecycle (dump.cs fzy<T> base class):
    //   Ctor: once when screen UXML is cloned (constructor init)
    //   OnEnable: each time screen becomes visible
    //   Localize: I18n text application (on show + language change)
    //   Event: state changes and UI interactions (called from protocol handlers)

    void DispatchGen_Ctor(string screen, VisualElement root)
    {
        switch (screen)
        {
            case "Login":
                Gen_Login_ctor(root);
                break;
            case "ServerSelection":
                Gen_ServerSelection_ctor(root);
                break;
            case "CharacterSelection":
                Gen_CharacterSelection_ctor(root);
                break;
            case "CharacterCreation":
                Gen_CharacterCreation_ctor(root);
                Gen_CharacterCreation_InitializeUI(root);
                Gen_CharacterCreation_InitializeBinding(root);
                break;
            case "PreGameMainMenu":
                Gen_PreGameMainMenu_ctor(root);
                break;
            case "CharacterHeader":
                Gen_CharacterHeader_ctor(root);
                break;
        }
    }

    void DispatchGen_OnEnable(string screen, VisualElement root)
    {
        switch (screen)
        {
            case "Login":
                Gen_Login_OnEnable(root);
                break;
            case "ServerSelection":
                Gen_ServerSelection_OnEnable(root);
                Gen_ServerSelection_SpawnEffect(root);
                Gen_ServerSelection_InitServer(root);
                break;
            case "CharacterSelection":
                Gen_CharacterSelection_OnEnable(root);
                Gen_CharacterSelection_EnableEffect(root);
                Gen_CharacterSelection_Init(root);
                Gen_CharacterSelection_DisplayCharacters(root);
                Gen_CharacterSelection_DisplayCharacter(root);
                break;
            case "CharacterCreation":
                Gen_CharacterCreation_OnEnable(root);
                Gen_CharacterCreation_Reset(root);
                Gen_CharacterCreation_SetUpAll(root);
                Gen_CharacterCreation_SpawnEffect(root);
                break;
            case "CharacterHeader":
                Gen_CharacterHeader_OnEnable(root);
                break;
        }
    }

    void DispatchGen_Localize(string screen, VisualElement root)
    {
        switch (screen)
        {
            case "ServerSelection":
                Gen_ServerSelection_Localize(root);
                break;
            case "CharacterSelection":
                Gen_CharacterSelection_Localize(root);
                break;
            case "CharacterCreation":
                Gen_CharacterCreation_LocalizeTexts(root);
                break;
        }
    }

    void DispatchGen_Event(string screen, string evt, VisualElement root)
    {
        switch (screen)
        {
            case "Login":
                switch (evt)
                {
                    case "mx": Gen_Login_mx(root); break;
                    case "bjdp": Gen_Login_bjdp(root); break;
                    case "bjdo": Gen_Login_bjdo(root); break;
                    case "bjdk": Gen_Login_bjdk(root); break;
                    case "bjdm": Gen_Login_bjdm(root); break;
                    case "bjdn": Gen_Login_bjdn(root); break;
                    case "bjdt": Gen_Login_bjdt(root); break;
                    case "gdj": Gen_Login_gdj(root); break;
                    case "oqh": Gen_Login_oqh(root); break;
                    case "nso": Gen_Login_nso(root); break;
                    case "irx": Gen_Login_irx(root); break;
                    case "lef": Gen_Login_lef(root); break;
                    case "mft": Gen_Login_mft(root); break;
                    case "fig": Gen_Login_fig(root); break;
                    case "jei": Gen_Login_jei(root); break;
                    case "lgs": Gen_Login_lgs(root); break;
                    case "cev": Gen_Login_cev(root); break;
                    case "eqo": Gen_Login_eqo(root); break;
                    case "odi": Gen_Login_odi(root); break;
                    case "nqz": Gen_Login_nqz(root); break;
                    case "ogx": Gen_Login_ogx(root); break;
                    case "fdk": Gen_Login_fdk(root); break;
                    case "jus": Gen_Login_jus(root); break;
                }
                break;
            case "ServerSelection":
                switch (evt)
                {
                    case "OnServerConnectionStarted": Gen_ServerSelection_OnServerConnectionStarted(root); break;
                    case "OnSelectedServerRefused": Gen_ServerSelection_OnSelectedServerRefused(root); break;
                    case "OnSelectionChanged": Gen_ServerSelection_OnSelectionChanged(root); break;
                    case "DisplayServer": Gen_ServerSelection_DisplayServer(root); break;
                    case "DisplayFilter": Gen_ServerSelection_DisplayFilter(root); break;
                    case "FilterTab": Gen_ServerSelection_FilterTab(root); break;
                    case "OnForceAccountStatus": Gen_ServerSelection_OnForceAccountStatus(root); break;
                    case "StartSearch": Gen_ServerSelection_StartSearch(root); break;
                    case "EndSearch": Gen_ServerSelection_EndSearch(root); break;
                    case "CancelSearch": Gen_ServerSelection_CancelSearch(root); break;
                    case "OnAcquaintanceServersError": Gen_ServerSelection_OnAcquaintanceServersError(root); break;
                    case "OnTimeOut": Gen_ServerSelection_OnTimeOut(root); break;
                    case "OnUpdateSearchTimerCompleted": Gen_ServerSelection_OnUpdateSearchTimerCompleted(root); break;
                    case "OnValidUI": Gen_ServerSelection_OnValidUI(root); break;
                    case "OnFriendSelected": Gen_ServerSelection_OnFriendSelected(root); break;
                    case "OnServerFriendList": Gen_ServerSelection_OnServerFriendList(root); break;
                    case "BindSearchResult": Gen_ServerSelection_BindSearchResult(root); break;
                    case "Scroll": Gen_ServerSelection_Scroll(root); break;
                    case "SwapForceMode": Gen_ServerSelection_SwapForceMode(root); break;
                    case "HideEffect": Gen_ServerSelection_HideEffect(root); break;
                    case "FilterOnlyCharacter": Gen_ServerSelection_FilterOnlyCharacter(root); break;
                    case "OnAcquaintanceServerList": Gen_ServerSelection_OnAcquaintanceServerList(root); break;
                    case "set_m_friendSearchButtonEnableState": Gen_ServerSelection_set_m_friendSearchButtonEnableState(root); break;
                }
                break;
            case "CharacterSelection":
                switch (evt)
                {
                    case "OnPressEnter": Gen_CharacterSelection_OnPressEnter(root); break;
                    case "ValidateCharacterChoice": Gen_CharacterSelection_ValidateCharacterChoice(root); break;
                    case "OnSelectionChanged": Gen_CharacterSelection_OnSelectionChanged(root); break;
                    case "BindItem": Gen_CharacterSelection_BindItem(root); break;
                    case "CreateDataProvider": Gen_CharacterSelection_CreateDataProvider(root); break;
                    case "UpdateCreateButton": Gen_CharacterSelection_UpdateCreateButton(root); break;
                    case "OnCharacterImpossibleSelection": Gen_CharacterSelection_OnCharacterImpossibleSelection(root); break;
                    case "OnCharacterDeletionError": Gen_CharacterSelection_OnCharacterDeletionError(root); break;
                    case "OnCharactersListUpdated": Gen_CharacterSelection_OnCharactersListUpdated(root); break;
                    case "UpdateSelectedCharacter": Gen_CharacterSelection_UpdateSelectedCharacter(root); break;
                    case "SetSelection": Gen_CharacterSelection_SetSelection(root); break;
                    case "DisplayEntity": Gen_CharacterSelection_DisplayEntity(root); break;
                    case "InitServersList": Gen_CharacterSelection_InitServersList(root); break;
                }
                break;
            case "CharacterCreation":
                switch (evt)
                {
                    case "SetData": Gen_CharacterCreation_SetData(root); break;
                    case "FilterBreed": Gen_CharacterCreation_FilterBreed(root); break;
                    case "ToggleSpells": Gen_CharacterCreation_ToggleSpells(root); break;
                    case "SetUpBreedUI": Gen_CharacterCreation_SetUpBreedUI(root); break;
                    case "SetUpGenderUI": Gen_CharacterCreation_SetUpGenderUI(root); break;
                    case "SetUpNameUI": Gen_CharacterCreation_SetUpNameUI(root); break;
                    case "SetUpColorsUI": Gen_CharacterCreation_SetUpColorsUI(root); break;
                    case "SetUpCosmeticUI": Gen_CharacterCreation_SetUpCosmeticUI(root); break;
                    case "DisableAll": Gen_CharacterCreation_DisableAll(root); break;
                    case "ModifyCharacterSetUp": Gen_CharacterCreation_ModifyCharacterSetUp(root); break;
                    case "UpdateUIBySelectedMode": Gen_CharacterCreation_UpdateUIBySelectedMode(root); break;
                    case "RefreshBreedInfo": Gen_CharacterCreation_RefreshBreedInfo(root); break;
                    case "OnCharacterCreationResult": Gen_CharacterCreation_OnCharacterCreationResult(root); break;
                    case "OnCharacterNameSuggested": Gen_CharacterCreation_OnCharacterNameSuggested(root); break;
                    case "OnCharacterImpossibleSelection": Gen_CharacterCreation_OnCharacterImpossibleSelection(root); break;
                    case "OnInput": Gen_CharacterCreation_OnInput(root); break;
                    case "CreateCharacter": Gen_CharacterCreation_CreateCharacter(root); break;
                    case "ResetInputName": Gen_CharacterCreation_ResetInputName(root); break;
                    case "BindBreedsSlots": Gen_CharacterCreation_BindBreedsSlots(root); break;
                    case "BindHeadsSlots": Gen_CharacterCreation_BindHeadsSlots(root); break;
                    case "DisplayRoleIcons": Gen_CharacterCreation_DisplayRoleIcons(root); break;
                    case "DisplayComplexity": Gen_CharacterCreation_DisplayComplexity(root); break;
                    case "InitServersList": Gen_CharacterCreation_InitServersList(root); break;
                    case "ResetFilters": Gen_CharacterCreation_ResetFilters(root); break;
                }
                break;
            case "CharacterHeader":
                switch (evt)
                {
                    case "Toggle": Gen_CharacterHeader_Toggle(root); break;
                    case "ShowHeader": Gen_CharacterHeader_ShowHeader(root); break;
                    case "OnServersList": Gen_CharacterHeader_OnServersList(root); break;
                    case "PlayerDisplay": Gen_CharacterHeader_PlayerDisplay(root); break;
                }
                break;
        }
    }

}