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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled fys.gdj
    // 299 lines → 6 ops
    void Gen_Login_gdj(VisualElement root)
    {
        // CALL fys.bjdl
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled fys.OnEnable
    // 473 lines → 10 ops
    void Gen_Login_OnEnable(VisualElement root)
    {
        // CALL fys.bjds
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled fys.lgs
    // 299 lines → 6 ops
    void Gen_Login_lgs(VisualElement root)
    {
        // CALL fys.bjdl
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled fys.oqh
    // 241 lines → 5 ops
    void Gen_Login_oqh(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
    }

    // Generated from decompiled fys.irx
    // 295 lines → 5 ops
    void Gen_Login_irx(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("dd_host"); if (el != null) el.SetEnabled(true); }
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
    // 437 lines → 9 ops
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
    }

    // Generated from decompiled CharacterSelection.ValidateCharacterChoice
    // 489 lines → 5 ops
    void Gen_CharacterSelection_ValidateCharacterChoice(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
    }

    // Generated from decompiled CharacterSelection.CreateDataProvider
    // 535 lines → 13 ops
    void Gen_CharacterSelection_CreateDataProvider(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) el.SetEnabled(true); }
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
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
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
    }

    // Generated from decompiled CharacterCreation.CreateCharacter
    // 699 lines → 12 ops
    void Gen_CharacterCreation_CreateCharacter(VisualElement root)
    {
        { var el = root.Q("btn_play"); if (el != null) { el.SetEnabled(true); el.RemoveFromClassList("disabled"); } }
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

    // Generated from decompiled BannerMenu.OnEnable
    // 1071 lines → 5 ops
    void Gen_BannerMenu_OnEnable(VisualElement root)
    {
        // EVENT: BannerMenu ? ()
        // EVENT: BannerMenu ? ()
    }

    // Generated from decompiled BannerMenu.bgrl
    // 477 lines → 13 ops
    void Gen_BannerMenu_bgrl(VisualElement root)
    {
        // CALL BannerMenu.bgut
    }

    // Generated from decompiled BannerMenu.ctor
    // 1051 lines → 18 ops
    void Gen_BannerMenu_ctor(VisualElement root)
    {
        // EVENT: BannerMenu ? ()
        { var el = root.Q("BannerMenu"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: BannerMenu")); }
        // EVENT: BannerMenu callback (paz)
        { var el = root.Q("BannerMenu"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: BannerMenu")); }
    }

    // Generated from decompiled BannerMenu.bgsc
    // 199 lines → 3 ops
    void Gen_BannerMenu_bgsc(VisualElement root)
    {
        // CALL BannerMenu.bgsd
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.FoldChatMessage
    // 421 lines → 7 ops
    void Gen_ChatView_FoldChatMessage(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.ScrollToBottom
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.BindItem
    // 497 lines → 16 ops
    void Gen_ChatView_BindItem(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.GetBinding
        // SET_TEXT on content (dynamic text from data)
        // SET_TEXT on content (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.IsFolded
        // CALL Core.UILogic.Chat.ChatView.IsFolded
        // SET_TEXT on subContent (dynamic text from data)
        // SET_TEXT on subContent (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.CreateTab
    // 413 lines → 3 ops
    void Gen_ChatView_CreateTab(VisualElement root)
    {
        { var el = root.Q("btn_addChannel"); if (el != null) el.style.display = DisplayStyle.None; }
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.DeleteTab
    // 205 lines → 3 ops
    void Gen_ChatView_DeleteTab(VisualElement root)
    {
        { var el = root.Q("btn_addChannel"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.NavigateOnMessageHistory
    // 219 lines → 6 ops
    void Gen_ChatView_NavigateOnMessageHistory(VisualElement root)
    {
        // SET_TEXT on ti_chatInput (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.NavigateOnRecipientHistory
    // 209 lines → 11 ops
    void Gen_ChatView_NavigateOnRecipientHistory(VisualElement root)
    {
        // SET_TEXT on ti_chatInput (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.ReFocusChat
        // CALL Core.UILogic.Chat.ChatView.SetCursorToEnd
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.AppendTextToInputAndFocus
    // 135 lines → 5 ops
    void Gen_ChatView_AppendTextToInputAndFocus(VisualElement root)
    {
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.ApplyInputChannel
    // 325 lines → 8 ops
    void Gen_ChatView_ApplyInputChannel(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.TryGetChannelInText
        // SET_TEXT on btn_dialect (dynamic text from data)
        // SET_TEXT on btn_dialect (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.OnValidateInput
    // 117 lines → 8 ops
    void Gen_ChatView_OnValidateInput(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.TrySendMessage
        // SET_TEXT on ti_chatInput (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.ReFocusChat
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.AutoCompleteChat
    // 223 lines → 6 ops
    void Gen_ChatView_AutoCompleteChat(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.ReFocusChat
        // SET_TEXT on ti_chatInput (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.BindUiElements
    // 1893 lines → 48 ops
    void Gen_ChatView_BindUiElements(VisualElement root)
    {
        // EVENT: Chat callback (paw)
        // EVENT: btn_addChannel callback (paw)
        // EVENT: ti_chatInput callback (paw)
        // EVENT: ti_chatInput ? ()
        // EVENT: ti_chatInput ? ()
        // EVENT: Chat ? ()
        { var el = root.Q("Chat"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: Chat")); }
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.KeyUpEvent
    // 367 lines → 10 ops
    void Gen_ChatView_KeyUpEvent(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.TryGetChannelInText
        // SET_TEXT on btn_dialect (dynamic text from data)
        // SET_TEXT on btn_dialect (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.ScrollToBottom
    // 81 lines → 4 ops
    void Gen_ChatView_ScrollToBottom(VisualElement root)
    {
        { var el = root.Q("btn_news"); if (el != null) el.style.display = DisplayStyle.None; }
        // CALL Core.UILogic.Chat.ChatView.ScrollLater
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.OnScrollValueChange
    // 79 lines → 2 ops
    void Gen_ChatView_OnScrollValueChange(VisualElement root)
    {
        { var el = root.Q("btn_news"); if (el != null) el.style.display = DisplayStyle.None; }
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.PreviousMessageHistoryShortcut
    // 205 lines → 6 ops
    void Gen_ChatView_PreviousMessageHistoryShortcut(VisualElement root)
    {
        // SET_TEXT on ti_chatInput (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.NextMessageHistoryShortcut
    // 205 lines → 6 ops
    void Gen_ChatView_NextMessageHistoryShortcut(VisualElement root)
    {
        // SET_TEXT on ti_chatInput (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.PreviousRecipientHistoryShortcut
    // 193 lines → 11 ops
    void Gen_ChatView_PreviousRecipientHistoryShortcut(VisualElement root)
    {
        // SET_TEXT on ti_chatInput (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.ReFocusChat
        // CALL Core.UILogic.Chat.ChatView.SetCursorToEnd
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.NextRecipientHistoryShortcut
    // 193 lines → 11 ops
    void Gen_ChatView_NextRecipientHistoryShortcut(VisualElement root)
    {
        // SET_TEXT on ti_chatInput (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.ReFocusChat
        // CALL Core.UILogic.Chat.ChatView.SetCursorToEnd
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.SendToPartyShortcut
    // 119 lines → 8 ops
    void Gen_ChatView_SendToPartyShortcut(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.TrySendMessage
        // SET_TEXT on ti_chatInput (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.ReFocusChat
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.SendToGuildShortcut
    // 119 lines → 8 ops
    void Gen_ChatView_SendToGuildShortcut(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.TrySendMessage
        // SET_TEXT on ti_chatInput (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.ReFocusChat
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.SendToTeamShortcut
    // 119 lines → 8 ops
    void Gen_ChatView_SendToTeamShortcut(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.TrySendMessage
        // SET_TEXT on ti_chatInput (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.ReFocusChat
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.OnNewChatMessage
    // 319 lines → 7 ops
    void Gen_ChatView_OnNewChatMessage(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.SearchRecipient
        { var el = root.Q("btn_news"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.OnChatFocus
    // 159 lines → 4 ops
    void Gen_ChatView_OnChatFocus(VisualElement root)
    {
        // SET_TEXT on ti_chatInput (dynamic text from data)
        // CALL Core.UILogic.Chat.ChatView.ChangeCurrentChannel
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.OnCrossGameChatFocus
    // 189 lines → 4 ops
    void Gen_ChatView_OnCrossGameChatFocus(VisualElement root)
    {
        // CALL Core.UILogic.Chat.ChatView.ChangeCurrentChannel
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.OnChatColorUpdated
    // 207 lines → 4 ops
    void Gen_ChatView_OnChatColorUpdated(VisualElement root)
    {
        // SET_TEXT on btn_dialect (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.SetInterline
    // 149 lines → 5 ops
    void Gen_ChatView_SetInterline(VisualElement root)
    {
        { var el = root.Q("btn_news"); if (el != null) el.style.display = DisplayStyle.None; }
        // CALL Core.UILogic.Chat.ChatView.ScrollLater
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.ChangeFontSize
    // 249 lines → 5 ops
    void Gen_ChatView_ChangeFontSize(VisualElement root)
    {
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.UpdateStyleForFont
    // 229 lines → 5 ops
    void Gen_ChatView_UpdateStyleForFont(VisualElement root)
    {
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.ChangeOpacity
    // 203 lines → 3 ops
    void Gen_ChatView_ChangeOpacity(VisualElement root)
    {
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.UpdateOpacity
    // 183 lines → 3 ops
    void Gen_ChatView_UpdateOpacity(VisualElement root)
    {
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.ChangeDefaultChannel
    // 229 lines → 7 ops
    void Gen_ChatView_ChangeDefaultChannel(VisualElement root)
    {
        // SET_TEXT on btn_dialect (dynamic text from data)
        // SET_TEXT on btn_dialect (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Chat.ChatView.ChangeCurrentChannel
    // 227 lines → 7 ops
    void Gen_ChatView_ChangeCurrentChannel(VisualElement root)
    {
        // SET_TEXT on btn_dialect (dynamic text from data)
        // SET_TEXT on btn_dialect (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.Inventory.Uis.PlayerInventory.PlayerInventoryUi.ctor
    // 437 lines → 10 ops
    void Gen_PlayerInventoryUi_ctor(VisualElement root)
    {
        { var el = root.Q("PlayerInventory"); if (el is Label lbl) lbl.text = I18n("ui.common.character"); }
        // EVENT: PlayerInventory callback (paw)
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.AddMouseClick
    // 461 lines → 15 ops
    void Gen_CharacterInformation_AddMouseClick(VisualElement root)
    {
        // EVENT: tx_hp ? ()
        { var el = root.Q("tx_hp"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: tx_hp")); }
        // EVENT: tx_ap ? ()
        { var el = root.Q("tx_ap"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: tx_ap")); }
        // EVENT: tx_mp ? ()
        { var el = root.Q("tx_mp"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: tx_mp")); }
        // EVENT: tx_ip ? ()
        { var el = root.Q("tx_ip"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: tx_ip")); }
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.DisplayPI
    // 191 lines → 4 ops
    void Gen_CharacterInformation_DisplayPI(VisualElement root)
    {
        // DISPLAY ctr_ip = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_ip"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.SetToDefaultWidthAndHeight
    // 273 lines → 4 ops
    void Gen_CharacterInformation_SetToDefaultWidthAndHeight(VisualElement root)
    {
        // DISPLAY ctr_ornaments = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_ornaments"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.SetHealthPoints
    // 421 lines → 15 ops
    void Gen_CharacterInformation_SetHealthPoints(VisualElement root)
    {
        // CALL Core.UILogic.MainMenu.CharacterInformation.UpdateHpMask
        // SET_TEXT on lbl_pdv (dynamic text from data)
        // SET_TEXT on lbl_pdvBottom (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.SetShieldPoints
    // 345 lines → 12 ops
    void Gen_CharacterInformation_SetShieldPoints(VisualElement root)
    {
        // CALL Core.UILogic.MainMenu.CharacterInformation.UpdateHpDisplay
        // CALL Core.UILogic.MainMenu.CharacterInformation.UpdateHpDisplay
        // SET_TEXT on lbl_shield (dynamic text from data)
        // CALL Core.UILogic.MainMenu.CharacterInformation.UpdateIconHpSprite
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.UpdateHpDisplay
    // 277 lines → 10 ops
    void Gen_CharacterInformation_UpdateHpDisplay(VisualElement root)
    {
        // SET_TEXT on lbl_pdvBottom (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.SetActionPoints
    // 67 lines → 3 ops
    void Gen_CharacterInformation_SetActionPoints(VisualElement root)
    {
        // SET_TEXT on lbl_pa (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.SetInvocationPoints
    // 79 lines → 3 ops
    void Gen_CharacterInformation_SetInvocationPoints(VisualElement root)
    {
        // SET_TEXT on lbl_ip (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.SetMovementPoints
    // 67 lines → 3 ops
    void Gen_CharacterInformation_SetMovementPoints(VisualElement root)
    {
        // SET_TEXT on lbl_pm (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.UpdateHpThreshold
    // 145 lines → 2 ops
    void Gen_CharacterInformation_UpdateHpThreshold(VisualElement root)
    {
        // DISPLAY threshold_mask = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("threshold_mask"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.OnChange
    // 169 lines → 7 ops
    void Gen_CharacterInformation_OnChange(VisualElement root)
    {
        // CALL Core.UILogic.MainMenu.CharacterInformation.GetPlayerId
        // CALL Core.UILogic.MainMenu.CharacterInformation.SetShieldPoints
        // CALL Core.UILogic.MainMenu.CharacterInformation.SetActionPoints
        // CALL Core.UILogic.MainMenu.CharacterInformation.GetPlayerId
        // SET_TEXT on lbl_ip (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.MainMenu.CharacterInformation.<BuildContextualMenu>b__56_3
    // 207 lines → 4 ops
    void Gen_CharacterInformation__BuildContextualMenu_b__56_3(VisualElement root)
    {
        // DISPLAY ctr_ip = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("ctr_ip"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled Core.UILogic.GameUICore.UIActionBar.ctor
    // 699 lines → 18 ops
    void Gen_ActionBar_ctor(VisualElement root)
    {
        // EVENT: ActionBar callback (paw)
        // EVENT: ActionBar callback (pay)
    }

    // Generated from decompiled Core.UILogic.GameUICore.UIActionBar.SubscribeInputs
    // 2301 lines → 3 ops
    void Gen_ActionBar_SubscribeInputs(VisualElement root)
    {
    }

    // Generated from decompiled Core.UILogic.GameUICore.UIActionBar.RefreshPageIndex
    // 123 lines → 8 ops
    void Gen_ActionBar_RefreshPageIndex(VisualElement root)
    {
        // SET_TEXT on lbl_pageIndex (dynamic text from data)
        { var el = root.Q("btn_prev"); if (el != null) el.SetEnabled(false); }
        { var el = root.Q("btn_next"); if (el != null) el.SetEnabled(false); }
    }

    // Generated from decompiled Core.UILogic.GameUICore.UIActionBar.SubscribeClicked
    // 505 lines → 13 ops
    void Gen_ActionBar_SubscribeClicked(VisualElement root)
    {
        // EVENT: gd_spellitemotes ? ()
        // EVENT: gd_spellitemotes ? ()
        // EVENT: btn_type callback (paw)
        // EVENT: btn_prev callback (paw)
        { var el = root.Q("btn_next"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: btn_next")); }
    }

    // Generated from decompiled Core.UILogic.GameUICore.UIActionBar.RefreshDisplayedLinesElements
    // 147 lines → 2 ops
    void Gen_ActionBar_RefreshDisplayedLinesElements(VisualElement root)
    {
    }

    // Generated from decompiled Core.UILogic.GameUICore.UIActionBar.RefreshFightPhase
    // 97 lines → 2 ops
    void Gen_ActionBar_RefreshFightPhase(VisualElement root)
    {
        { var el = root.Q("gd_spellitemotes"); if (el != null) el.style.visibility = Visibility.Hidden; }
    }

    // Generated from decompiled Core.UILogic.NpcDialog.NpcDialog.ctor
    // 245 lines → 3 ops
    void Gen_NpcDialog_ctor(VisualElement root)
    {
        // EVENT: btn_close callback (paw)
    }

    // Generated from decompiled Core.UILogic.NpcDialog.NpcDialog.OnEnable
    // 39 lines → 2 ops
    void Gen_NpcDialog_OnEnable(VisualElement root)
    {
        { var el = root.Q("npcDialogBubble"); if (el != null) el.style.visibility = Visibility.Hidden; }
    }

    // Generated from decompiled Core.UILogic.NpcDialog.NpcDialog.Open
    // 859 lines → 12 ops
    void Gen_NpcDialog_Open(VisualElement root)
    {
        // CALL Core.UILogic.NpcDialog.NpcDialog.AssignHooks
        SetElementText(root, "lbl_npcName", I18n("ui.zaap.prism"));
        // SET_TEXT on lbl_npcName (dynamic text from data)
        // SET_TEXT on lbl_npcName (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.NpcDialog.NpcDialog.SetBubbleSkin
    // 3285 lines → 75 ops
    void Gen_NpcDialog_SetBubbleSkin(VisualElement root)
    {
        { var el = root.Q("tx_outline"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("encoche"); if (el != null) el.style.display = DisplayStyle.Flex; }
        { var el = root.Q("npcDialogBubble"); el?.RemoveFromClassList("?"); }
        { var el = root.Q("tx_background"); el?.RemoveFromClassList("?"); }
        { var el = root.Q("encoche"); el?.RemoveFromClassList("?"); }
        // DISPLAY tx_outline = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("tx_outline"); if (el != null) el.style.display = DisplayStyle.Flex; }
        // DISPLAY encoche = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("encoche"); if (el != null) el.style.display = DisplayStyle.Flex; }
        // DISPLAY tx_glow = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("tx_glow"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled Core.UILogic.NpcDialog.NpcDialog.DisplayReplies
    // 1187 lines → 9 ops
    void Gen_NpcDialog_DisplayReplies(VisualElement root)
    {
    }

    // Generated from decompiled Core.UILogic.NpcDialog.NpcDialog.DisplayBubbleOnPortal
    // 69 lines → 5 ops
    void Gen_NpcDialog_DisplayBubbleOnPortal(VisualElement root)
    {
        { var el = root.Q("npcDialogBubble"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // SET_TEXT on lbl_text (dynamic text from data)
        // SET_TEXT on lbl_text (dynamic text from data)
    }

    // Generated from decompiled Core.UILogic.NpcDialog.NpcDialog.DisplayBubbleOnNpc
    // 59 lines → 5 ops
    void Gen_NpcDialog_DisplayBubbleOnNpc(VisualElement root)
    {
        { var el = root.Q("npcDialogBubble"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // SET_TEXT on lbl_text (dynamic text from data)
        // CALL Core.UILogic.NpcDialog.NpcDialog.GetEntity
    }

    // Generated from decompiled Core.UILogic.NpcDialog.NpcDialog.DisplayBubbleOnPrism
    // 111 lines → 5 ops
    void Gen_NpcDialog_DisplayBubbleOnPrism(VisualElement root)
    {
        { var el = root.Q("npcDialogBubble"); if (el != null) el.style.visibility = Visibility.Hidden; }
        // SET_TEXT on lbl_text (dynamic text from data)
        // SET_TEXT on lbl_text (dynamic text from data)
    }

    // Generated from decompiled EquipmentStorageUI.ctor
    // 195 lines → 3 ops
    void Gen_EquipmentStorageUI_ctor(VisualElement root)
    {
        // EVENT: btnActionSmall callback (paw)
    }

    // Generated from decompiled EquipmentStorageUI.OnMouseEnterGrid
    // 149 lines → 4 ops
    void Gen_EquipmentStorageUI_OnMouseEnterGrid(VisualElement root)
    {
        // CALL EquipmentStorageUI.CanMoveCosmetics
        // DISPLAY btnActionSmall = ? (via pca, depends on runtime state — default Flex for DOTween fallback)
        { var el = root.Q("btnActionSmall"); if (el != null) el.style.display = DisplayStyle.Flex; }
    }

    // Generated from decompiled EquipmentStorageUI.FillSort
    // 535 lines → 36 ops
    void Gen_EquipmentStorageUI_FillSort(VisualElement root)
    {
        SetElementText(root, "cb_sort", I18n("ui.common.sortBy.default"));
        // SET_TEXT on cb_sort (dynamic text from data)
    }

    // Generated from decompiled SpellBaseUi.ctor
    // 933 lines → 15 ops
    void Gen_SpellBaseUi_ctor(VisualElement root)
    {
        // EVENT: btn_gameplay callback (paw)
    }

    // Generated from decompiled SpellBaseUi.RefreshTabName
    // 103 lines → 5 ops
    void Gen_SpellBaseUi_RefreshTabName(VisualElement root)
    {
    }

    // Generated from decompiled SpellBaseUi.OnSizeSelected
    // 323 lines → 6 ops
    void Gen_SpellBaseUi_OnSizeSelected(VisualElement root)
    {
        SetElementText(root, "btn_gameplay", I18n("ui.grimoire.mecanics"));
    }

    // Generated from decompiled SpellBaseUi.SetSize
    // 297 lines → 6 ops
    void Gen_SpellBaseUi_SetSize(VisualElement root)
    {
        SetElementText(root, "btn_gameplay", I18n("ui.grimoire.mecanics"));
    }

    void ApplyGeneratedMethods(string screenName, VisualElement root)
    {
        switch (screenName)
        {
            case "Login":
                Gen_Login_ctor(root);
                break;
            case "ServerSelection":
                Gen_ServerSelection_ctor(root);
                Gen_ServerSelection_Localize(root);
                Gen_ServerSelection_OnEnable(root);
                break;
            case "CharacterSelection":
                Gen_CharacterSelection_ctor(root);
                Gen_CharacterSelection_Localize(root);
                Gen_CharacterSelection_DisplayCharacter(root);
                Gen_CharacterSelection_Init(root);
                Gen_CharacterSelection_DisplayCharacters(root);
                Gen_CharacterSelection_OnEnable(root);
                break;
            case "CharacterCreation":
                Gen_CharacterCreation_ctor(root);
                Gen_CharacterCreation_InitializeUI(root);
                Gen_CharacterCreation_LocalizeTexts(root);
                Gen_CharacterCreation_InitializeBinding(root);
                Gen_CharacterCreation_OnEnable(root);
                Gen_CharacterCreation_Reset(root);
                Gen_CharacterCreation_SetUpAll(root);
                break;
            case "PreGameMainMenu":
                Gen_PreGameMainMenu_ctor(root);
                break;
            case "CharacterHeader":
                Gen_CharacterHeader_ctor(root);
                Gen_CharacterHeader_OnEnable(root);
                Gen_CharacterHeader_Toggle(root);
                Gen_CharacterHeader_ShowHeader(root);
                Gen_CharacterHeader_OnServersList(root);
                Gen_CharacterHeader_PlayerDisplay(root);
                break;
            case "BannerMenu":
                Gen_BannerMenu_OnEnable(root);
                Gen_BannerMenu_ctor(root);
                break;
            case "ChatView":
                Gen_ChatView_BindUiElements(root);
                break;
            case "PlayerInventoryUi":
                Gen_PlayerInventoryUi_ctor(root);
                break;
            case "CharacterInformation":
                Gen_CharacterInformation_AddMouseClick(root);
                Gen_CharacterInformation_SetToDefaultWidthAndHeight(root);
                break;
            case "ActionBar":
                Gen_ActionBar_ctor(root);
                Gen_ActionBar_SubscribeInputs(root);
                Gen_ActionBar_SubscribeClicked(root);
                break;
            case "NpcDialog":
                Gen_NpcDialog_ctor(root);
                Gen_NpcDialog_OnEnable(root);
                break;
            case "EquipmentStorageUI":
                Gen_EquipmentStorageUI_ctor(root);
                break;
            case "SpellBaseUi":
                Gen_SpellBaseUi_ctor(root);
                break;
        }
    }

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
            case "BannerMenu":
                Gen_BannerMenu_ctor(root);
                break;
            case "PlayerInventoryUi":
                Gen_PlayerInventoryUi_ctor(root);
                break;
            case "ActionBar":
                Gen_ActionBar_ctor(root);
                break;
            case "NpcDialog":
                Gen_NpcDialog_ctor(root);
                break;
            case "EquipmentStorageUI":
                Gen_EquipmentStorageUI_ctor(root);
                break;
            case "SpellBaseUi":
                Gen_SpellBaseUi_ctor(root);
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
                Gen_CharacterCreation_SpawnEffect(root);
                Gen_CharacterCreation_Reset(root);
                Gen_CharacterCreation_SetUpAll(root);
                break;
            case "CharacterHeader":
                Gen_CharacterHeader_OnEnable(root);
                break;
            case "BannerMenu":
                Gen_BannerMenu_OnEnable(root);
                break;
            case "NpcDialog":
                Gen_NpcDialog_OnEnable(root);
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
                    case "bjdo": Gen_Login_bjdo(root); break;
                    case "gdj": Gen_Login_gdj(root); break;
                    case "mx": Gen_Login_mx(root); break;
                    case "bjdp": Gen_Login_bjdp(root); break;
                    case "bjdm": Gen_Login_bjdm(root); break;
                    case "ogx": Gen_Login_ogx(root); break;
                    case "jei": Gen_Login_jei(root); break;
                    case "lgs": Gen_Login_lgs(root); break;
                    case "fdk": Gen_Login_fdk(root); break;
                    case "bjdt": Gen_Login_bjdt(root); break;
                    case "jus": Gen_Login_jus(root); break;
                    case "mft": Gen_Login_mft(root); break;
                    case "oqh": Gen_Login_oqh(root); break;
                    case "eqo": Gen_Login_eqo(root); break;
                    case "cev": Gen_Login_cev(root); break;
                    case "nso": Gen_Login_nso(root); break;
                    case "odi": Gen_Login_odi(root); break;
                    case "fig": Gen_Login_fig(root); break;
                    case "irx": Gen_Login_irx(root); break;
                    case "bjdn": Gen_Login_bjdn(root); break;
                    case "lef": Gen_Login_lef(root); break;
                    case "nqz": Gen_Login_nqz(root); break;
                }
                break;
            case "ServerSelection":
                switch (evt)
                {
                    case "set_m_friendSearchButtonEnableState": Gen_ServerSelection_set_m_friendSearchButtonEnableState(root); break;
                    case "FilterOnlyCharacter": Gen_ServerSelection_FilterOnlyCharacter(root); break;
                    case "OnSelectionChanged": Gen_ServerSelection_OnSelectionChanged(root); break;
                    case "OnServerFriendList": Gen_ServerSelection_OnServerFriendList(root); break;
                    case "OnFriendSelected": Gen_ServerSelection_OnFriendSelected(root); break;
                    case "InitServer": Gen_ServerSelection_InitServer(root); break;
                    case "DisplayFilter": Gen_ServerSelection_DisplayFilter(root); break;
                    case "FilterTab": Gen_ServerSelection_FilterTab(root); break;
                    case "Scroll": Gen_ServerSelection_Scroll(root); break;
                    case "DisplayServer": Gen_ServerSelection_DisplayServer(root); break;
                    case "BindSearchResult": Gen_ServerSelection_BindSearchResult(root); break;
                    case "SpawnEffect": Gen_ServerSelection_SpawnEffect(root); break;
                    case "HideEffect": Gen_ServerSelection_HideEffect(root); break;
                    case "OnValidUI": Gen_ServerSelection_OnValidUI(root); break;
                    case "OnForceAccountStatus": Gen_ServerSelection_OnForceAccountStatus(root); break;
                    case "SwapForceMode": Gen_ServerSelection_SwapForceMode(root); break;
                    case "StartSearch": Gen_ServerSelection_StartSearch(root); break;
                    case "OnAcquaintanceServerList": Gen_ServerSelection_OnAcquaintanceServerList(root); break;
                    case "EndSearch": Gen_ServerSelection_EndSearch(root); break;
                    case "OnAcquaintanceServersError": Gen_ServerSelection_OnAcquaintanceServersError(root); break;
                    case "CancelSearch": Gen_ServerSelection_CancelSearch(root); break;
                    case "OnUpdateSearchTimerCompleted": Gen_ServerSelection_OnUpdateSearchTimerCompleted(root); break;
                    case "OnServerConnectionStarted": Gen_ServerSelection_OnServerConnectionStarted(root); break;
                    case "OnSelectedServerRefused": Gen_ServerSelection_OnSelectedServerRefused(root); break;
                }
                break;
            case "CharacterSelection":
                switch (evt)
                {
                    case "OnPressEnter": Gen_CharacterSelection_OnPressEnter(root); break;
                    case "ValidateCharacterChoice": Gen_CharacterSelection_ValidateCharacterChoice(root); break;
                    case "InitServersList": Gen_CharacterSelection_InitServersList(root); break;
                    case "OnSelectionChanged": Gen_CharacterSelection_OnSelectionChanged(root); break;
                    case "SetSelection": Gen_CharacterSelection_SetSelection(root); break;
                    case "BindItem": Gen_CharacterSelection_BindItem(root); break;
                    case "DisplayCharacter": Gen_CharacterSelection_DisplayCharacter(root); break;
                    case "Init": Gen_CharacterSelection_Init(root); break;
                    case "CreateDataProvider": Gen_CharacterSelection_CreateDataProvider(root); break;
                    case "DisplayCharacters": Gen_CharacterSelection_DisplayCharacters(root); break;
                    case "EnableEffect": Gen_CharacterSelection_EnableEffect(root); break;
                    case "UpdateCreateButton": Gen_CharacterSelection_UpdateCreateButton(root); break;
                    case "OnCharacterImpossibleSelection": Gen_CharacterSelection_OnCharacterImpossibleSelection(root); break;
                    case "OnCharacterDeletionError": Gen_CharacterSelection_OnCharacterDeletionError(root); break;
                    case "OnCharactersListUpdated": Gen_CharacterSelection_OnCharactersListUpdated(root); break;
                    case "DisplayEntity": Gen_CharacterSelection_DisplayEntity(root); break;
                    case "UpdateSelectedCharacter": Gen_CharacterSelection_UpdateSelectedCharacter(root); break;
                }
                break;
            case "CharacterCreation":
                switch (evt)
                {
                    case "SetData": Gen_CharacterCreation_SetData(root); break;
                    case "InitializeUI": Gen_CharacterCreation_InitializeUI(root); break;
                    case "InitializeBinding": Gen_CharacterCreation_InitializeBinding(root); break;
                    case "ToggleSpells": Gen_CharacterCreation_ToggleSpells(root); break;
                    case "FilterBreed": Gen_CharacterCreation_FilterBreed(root); break;
                    case "ResetFilters": Gen_CharacterCreation_ResetFilters(root); break;
                    case "SpawnEffect": Gen_CharacterCreation_SpawnEffect(root); break;
                    case "UpdateUIBySelectedMode": Gen_CharacterCreation_UpdateUIBySelectedMode(root); break;
                    case "ModifyCharacterSetUp": Gen_CharacterCreation_ModifyCharacterSetUp(root); break;
                    case "Reset": Gen_CharacterCreation_Reset(root); break;
                    case "SetUpGenderUI": Gen_CharacterCreation_SetUpGenderUI(root); break;
                    case "SetUpBreedUI": Gen_CharacterCreation_SetUpBreedUI(root); break;
                    case "SetUpNameUI": Gen_CharacterCreation_SetUpNameUI(root); break;
                    case "SetUpColorsUI": Gen_CharacterCreation_SetUpColorsUI(root); break;
                    case "SetUpCosmeticUI": Gen_CharacterCreation_SetUpCosmeticUI(root); break;
                    case "SetUpAll": Gen_CharacterCreation_SetUpAll(root); break;
                    case "DisableAll": Gen_CharacterCreation_DisableAll(root); break;
                    case "ResetInputName": Gen_CharacterCreation_ResetInputName(root); break;
                    case "BindBreedsSlots": Gen_CharacterCreation_BindBreedsSlots(root); break;
                    case "BindHeadsSlots": Gen_CharacterCreation_BindHeadsSlots(root); break;
                    case "RefreshBreedInfo": Gen_CharacterCreation_RefreshBreedInfo(root); break;
                    case "DisplayRoleIcons": Gen_CharacterCreation_DisplayRoleIcons(root); break;
                    case "InitServersList": Gen_CharacterCreation_InitServersList(root); break;
                    case "OnCharacterCreationResult": Gen_CharacterCreation_OnCharacterCreationResult(root); break;
                    case "OnCharacterNameSuggested": Gen_CharacterCreation_OnCharacterNameSuggested(root); break;
                    case "OnCharacterImpossibleSelection": Gen_CharacterCreation_OnCharacterImpossibleSelection(root); break;
                    case "DisplayComplexity": Gen_CharacterCreation_DisplayComplexity(root); break;
                    case "OnInput": Gen_CharacterCreation_OnInput(root); break;
                    case "CreateCharacter": Gen_CharacterCreation_CreateCharacter(root); break;
                    case "<InitializeBinding>b__78_32": Gen_CharacterCreation__InitializeBinding_b__78_32(root); break;
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
            case "BannerMenu":
                switch (evt)
                {
                    case "bgrl": Gen_BannerMenu_bgrl(root); break;
                    case "bgsc": Gen_BannerMenu_bgsc(root); break;
                }
                break;
            case "ChatView":
                switch (evt)
                {
                    case "FoldChatMessage": Gen_ChatView_FoldChatMessage(root); break;
                    case "BindItem": Gen_ChatView_BindItem(root); break;
                    case "CreateTab": Gen_ChatView_CreateTab(root); break;
                    case "DeleteTab": Gen_ChatView_DeleteTab(root); break;
                    case "NavigateOnMessageHistory": Gen_ChatView_NavigateOnMessageHistory(root); break;
                    case "NavigateOnRecipientHistory": Gen_ChatView_NavigateOnRecipientHistory(root); break;
                    case "AppendTextToInputAndFocus": Gen_ChatView_AppendTextToInputAndFocus(root); break;
                    case "ApplyInputChannel": Gen_ChatView_ApplyInputChannel(root); break;
                    case "OnValidateInput": Gen_ChatView_OnValidateInput(root); break;
                    case "AutoCompleteChat": Gen_ChatView_AutoCompleteChat(root); break;
                    case "BindUiElements": Gen_ChatView_BindUiElements(root); break;
                    case "KeyUpEvent": Gen_ChatView_KeyUpEvent(root); break;
                    case "ScrollToBottom": Gen_ChatView_ScrollToBottom(root); break;
                    case "OnScrollValueChange": Gen_ChatView_OnScrollValueChange(root); break;
                    case "PreviousMessageHistoryShortcut": Gen_ChatView_PreviousMessageHistoryShortcut(root); break;
                    case "NextMessageHistoryShortcut": Gen_ChatView_NextMessageHistoryShortcut(root); break;
                    case "PreviousRecipientHistoryShortcut": Gen_ChatView_PreviousRecipientHistoryShortcut(root); break;
                    case "NextRecipientHistoryShortcut": Gen_ChatView_NextRecipientHistoryShortcut(root); break;
                    case "SendToPartyShortcut": Gen_ChatView_SendToPartyShortcut(root); break;
                    case "SendToGuildShortcut": Gen_ChatView_SendToGuildShortcut(root); break;
                    case "SendToTeamShortcut": Gen_ChatView_SendToTeamShortcut(root); break;
                    case "OnNewChatMessage": Gen_ChatView_OnNewChatMessage(root); break;
                    case "OnChatFocus": Gen_ChatView_OnChatFocus(root); break;
                    case "OnCrossGameChatFocus": Gen_ChatView_OnCrossGameChatFocus(root); break;
                    case "OnChatColorUpdated": Gen_ChatView_OnChatColorUpdated(root); break;
                    case "SetInterline": Gen_ChatView_SetInterline(root); break;
                    case "ChangeFontSize": Gen_ChatView_ChangeFontSize(root); break;
                    case "UpdateStyleForFont": Gen_ChatView_UpdateStyleForFont(root); break;
                    case "ChangeOpacity": Gen_ChatView_ChangeOpacity(root); break;
                    case "UpdateOpacity": Gen_ChatView_UpdateOpacity(root); break;
                    case "ChangeDefaultChannel": Gen_ChatView_ChangeDefaultChannel(root); break;
                    case "ChangeCurrentChannel": Gen_ChatView_ChangeCurrentChannel(root); break;
                }
                break;
            case "CharacterInformation":
                switch (evt)
                {
                    case "AddMouseClick": Gen_CharacterInformation_AddMouseClick(root); break;
                    case "DisplayPI": Gen_CharacterInformation_DisplayPI(root); break;
                    case "SetToDefaultWidthAndHeight": Gen_CharacterInformation_SetToDefaultWidthAndHeight(root); break;
                    case "SetHealthPoints": Gen_CharacterInformation_SetHealthPoints(root); break;
                    case "SetShieldPoints": Gen_CharacterInformation_SetShieldPoints(root); break;
                    case "UpdateHpDisplay": Gen_CharacterInformation_UpdateHpDisplay(root); break;
                    case "SetActionPoints": Gen_CharacterInformation_SetActionPoints(root); break;
                    case "SetInvocationPoints": Gen_CharacterInformation_SetInvocationPoints(root); break;
                    case "SetMovementPoints": Gen_CharacterInformation_SetMovementPoints(root); break;
                    case "UpdateHpThreshold": Gen_CharacterInformation_UpdateHpThreshold(root); break;
                    case "OnChange": Gen_CharacterInformation_OnChange(root); break;
                    case "<BuildContextualMenu>b__56_3": Gen_CharacterInformation__BuildContextualMenu_b__56_3(root); break;
                }
                break;
            case "ActionBar":
                switch (evt)
                {
                    case "SubscribeInputs": Gen_ActionBar_SubscribeInputs(root); break;
                    case "RefreshPageIndex": Gen_ActionBar_RefreshPageIndex(root); break;
                    case "SubscribeClicked": Gen_ActionBar_SubscribeClicked(root); break;
                    case "RefreshDisplayedLinesElements": Gen_ActionBar_RefreshDisplayedLinesElements(root); break;
                    case "RefreshFightPhase": Gen_ActionBar_RefreshFightPhase(root); break;
                }
                break;
            case "NpcDialog":
                switch (evt)
                {
                    case "Open": Gen_NpcDialog_Open(root); break;
                    case "SetBubbleSkin": Gen_NpcDialog_SetBubbleSkin(root); break;
                    case "DisplayReplies": Gen_NpcDialog_DisplayReplies(root); break;
                    case "DisplayBubbleOnPortal": Gen_NpcDialog_DisplayBubbleOnPortal(root); break;
                    case "DisplayBubbleOnNpc": Gen_NpcDialog_DisplayBubbleOnNpc(root); break;
                    case "DisplayBubbleOnPrism": Gen_NpcDialog_DisplayBubbleOnPrism(root); break;
                }
                break;
            case "EquipmentStorageUI":
                switch (evt)
                {
                    case "OnMouseEnterGrid": Gen_EquipmentStorageUI_OnMouseEnterGrid(root); break;
                    case "FillSort": Gen_EquipmentStorageUI_FillSort(root); break;
                }
                break;
            case "SpellBaseUi":
                switch (evt)
                {
                    case "RefreshTabName": Gen_SpellBaseUi_RefreshTabName(root); break;
                    case "OnSizeSelected": Gen_SpellBaseUi_OnSizeSelected(root); break;
                    case "SetSize": Gen_SpellBaseUi_SetSize(root); break;
                }
                break;
        }
    }

}