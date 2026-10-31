using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;
using Jondo.Reconstructed.Movement;
using Jondo.UI.Generated;

public partial class WebGLProbe : MonoBehaviour
{
    static readonly List<string> _log = new();
    static readonly List<string> _errors = new();
    static readonly List<string> _factoryLog = new();
    static Component _compositor;
    static Type _baseType, _gameType;
    static float _lastInspect;
    static bool _showOverlay = true;
    static string _status = "";
    static Dictionary<string, string> _i18n;
    static Dictionary<string, string[]> _ussOverrides; // parentClass → childNames to create
    static Dictionary<string, string> _assetPaths; // OPS key → asset path (from ClientData.json)
    static Dictionary<string, string> _serverNames; // serverId → name (from world.db via ServerNames.json)

    // ScreenBehavior.json — data-driven screen specs (synthesize_screens.py output)
    static Dictionary<string, ScreenSpec> _screenSpecs;
    static Dictionary<string, string> _flowRules;

    struct ScreenSpec
    {
        public string handler;
        public int authUi;
        public string uxml;
        public string binding;
        public List<ElementSpec> elements;
        public Dictionary<string, string> i18n; // elementName → I18n string key
        public Dictionary<string, string> states; // stateName → serialized state JSON
        public string rawJson; // full JSON object for on-demand parsing
    }
    struct ElementSpec { public string name; public string type; }

    // Loading indicator (from ConnectionBackgroundBinding._iconLoading, dump.cs L25814)
    VisualElement _loadingIcon;
    Coroutine _loadingRotation;

    // PreGameMainMenu overlay (from fyv handler, dump.cs L386396)
    VisualElement _menuOverlay;

    // Server list parsed from AuthAccepted (ConnectionProtocol.cs L64-128)
    struct ServerInfo { public int id; public int charCount; public List<string> charNames; }
    List<ServerInfo> _serverList;
    int _selectedServerCardIdx = -1;
    int _carouselOffset; // first server index shown in carousel (ServerSelection.m_carousel)

    // Character list parsed from kvi (ConnectionProtocol.cs L373-385)
    struct CharInfo { public int id; public string name; public int level; public int breed; }
    List<CharInfo> _characterList;
    int _selectedCharIdx = -1;
    string _accountNickname;
    string _accountTag;
    string _subscriptionEndDate;
    VisualElement _characterHeaderRoot;

    // Generated UI Toolkit Typed Bindings (Pilar 2 - Opcao 1)
    GeneratedLoginBinding _loginBinding;
    GeneratedServerSelectionBinding _serverSelectionBinding;
    GeneratedCharacterSelectionBinding _charSelectionBinding;
    GeneratedCharacterHeaderBinding _charHeaderBinding;
    GeneratedCharacterInformationsBinding _charInfoBinding;
    GeneratedChatviewBinding _chatviewBinding;
    GeneratedActionBarBinding _actionBarBinding;
    GeneratedBannerMenuBinding _bannerMenuBinding;

    static string FormatSubscriptionDisplay(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        if (DateTimeOffset.TryParse(raw, out var dto))
            return $"Hasta el {dto.ToString("dd/MM/yyyy, HH:mm")}";
        return raw.StartsWith("Hasta el") ? raw : $"Hasta el {raw}";
    }

    long _currentMapId;
    AssetBundle _currentMapBundle;
    bool _mapLoading;

    // Map cell data from MapMetadata (560 cells per standard Dofus map)
    public struct CellData { public uint cellNumber; public bool mov; public bool los; public int floor; public int speed; public int arrow; }
    CellData[] _mapCells;

    // Map element data from MapMetadata (gfxId + transform + color per element)
    struct MapElement { public int gfxId; public uint color; public float m11, m12, m21, m22, m31, m32; public int materialIndex; }
    MapElement[] _bgElements, _sortElements, _fgElements;

    // WorldPoint encoding (decompiled from Core.DataCenter.Metadata.World.WorldPoint.FromMapId)
    static int WorldIdFromMapId(long mapId) => (int)((mapId >> 18) & 0xFFF);
    static int MapXFromMapId(long mapId) { int r = (int)((mapId >> 9) & 0x1FF); return r > 255 ? -(r & 0xFF) : r; }
    static int MapYFromMapId(long mapId) { int r = (int)(mapId & 0x1FF); return r > 255 ? -(r & 0xFF) : r; }

    const int MAX = 300;
    const float INTERVAL = 10f;
    static readonly BindingFlags ALL = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly BindingFlags STATIC = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        Log("PROBE v32: real UXML mount, interactive play button, lifecycle Gen_ dispatch (114 methods), audio pause, data-driven boot");
        Application.logMessageReceived += OnLog;

        CreateCamera();
        RegisterCustomElements();
        LoadI18n();
        LoadServerNames();
        LoadRuntimeData();
        LoadBootLogic();
        LoadAudioData();
        LoadDofusUISettings();
        LoadScreenBehavior();
        WebSocketBridge.ExposeTestHook();

        var go = new GameObject("__Probe");
        DontDestroyOnLoad(go);
        go.AddComponent<WebGLProbe>();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Camera — WebGL shows black without one. Clears to dark indigo so UI
    // Toolkit elements with transparent bg become distinguishable.
    // ══════════════════════════════════════════════════════════════════════

    static void LoadI18n()
    {
        var lang = "en";
        var ta = Resources.Load<UnityEngine.TextAsset>($"I18n/{lang}");
        if (ta == null) { Log($"  I18n: {lang}.json not found in Resources"); return; }
        _i18n = new Dictionary<string, string>();
        var json = ta.text;
        int pos = 0;
        while (pos < json.Length)
        {
            int kStart = json.IndexOf('"', pos);
            if (kStart < 0) break;
            int kEnd = json.IndexOf('"', kStart + 1);
            if (kEnd < 0) break;
            int vStart = json.IndexOf('"', kEnd + 1);
            if (vStart < 0) break;
            int vEnd = vStart + 1;
            while (vEnd < json.Length)
            {
                if (json[vEnd] == '"' && json[vEnd - 1] != '\\') break;
                vEnd++;
            }
            _i18n[json.Substring(kStart + 1, kEnd - kStart - 1)] =
                json.Substring(vStart + 1, vEnd - vStart - 1);
            pos = vEnd + 1;
        }
        Log($"  I18n: loaded {_i18n.Count} strings ({lang})");
    }

    static string I18n(int key, string fallback = null) =>
        _i18n != null && _i18n.TryGetValue(key.ToString(), out var v) ? v : fallback ?? $"[{key}]";

    static string I18n(string key, string fallback = null) =>
        _i18n != null && _i18n.TryGetValue(key, out var v) ? v : fallback ?? $"[{key}]";

    static string _bankPath;
    static string _masterBank;
    static string _stringsBank;
    static int _bootPhaseCount;
    static int _bootServiceCount;
    static BootCentralRegistration[] _centralRegistrations;
    static BootReconstructedService[] _reconstructedServices;
    static string _reconstructedServiceEvidence;
    static bool _banksLoaded;

    [Serializable]
    sealed class BootLogicData
    {
        public BootPhaseData[] phases;
        public BootAudioData audioInit;
        public BootCentralRegistration[] centralRegistrations;
        public BootReconstructedService[] reconstructedServices;
        public string reconstructedServiceEvidence;
    }

    [Serializable]
    sealed class BootCentralRegistration
    {
        public string field;
        public string fieldType;
        public string registryType;
    }

    [Serializable]
    sealed class BootReconstructedService
    {
        public int order;
        public string helper;
        public string[] genericTypes;
        public string concreteClass;
        public string sourceType;
        public string fieldName;
        public string constructorMethod;
    }

    [Serializable]
    sealed class BootPhaseData
    {
        public string name;
        public BootServiceInitData[] serviceInits;
    }

    [Serializable]
    sealed class BootServiceInitData
    {
        public string type;
        public string method;
        public string va;
    }

    [Serializable]
    sealed class BootAudioData
    {
        public string bankPath;
        public string masterBank;
        public string stringsBank;
    }

    static void LoadBootLogic()
    {
        _centralRegistrations = null;
        _reconstructedServices = null;
        _reconstructedServiceEvidence = null;
        _bootPhaseCount = 0;
        _bootServiceCount = 0;
        var ta = Resources.Load<UnityEngine.TextAsset>("BootLogic");
        if (ta == null) { Log("  BootLogic.json not found"); return; }
        BootLogicData data;
        try { data = JsonUtility.FromJson<BootLogicData>(ta.text); }
        catch (Exception exception) { Err($"  Invalid BootLogic.json: {exception.Message}"); return; }
        if (data?.phases == null || data.audioInit == null) { Err("  BootLogic.json missing phases or audioInit"); return; }
        _bankPath = data.audioInit.bankPath;
        _masterBank = data.audioInit.masterBank;
        _stringsBank = data.audioInit.stringsBank;
        _centralRegistrations = data.centralRegistrations;
        _reconstructedServices = data.reconstructedServices;
        _reconstructedServiceEvidence = data.reconstructedServiceEvidence;
        _bootPhaseCount = data.phases.Length;
        _bootServiceCount = 0;
        foreach (var phase in data.phases)
            if (phase?.serviceInits != null) _bootServiceCount += phase.serviceInits.Length;

        Log($"  BootLogic: {_bootPhaseCount} phases, {_bootServiceCount} services, {_reconstructedServices?.Length ?? 0} reconstructed, bank={_bankPath}");
    }

    static string _menuMusicEvent;
    static string _uiBank;
    static string[] _additionalBanks;
    static Dictionary<string, string> _uiSoundEvents;

    static void LoadAudioData()
    {
        var ta = Resources.Load<UnityEngine.TextAsset>("AudioData");
        if (ta == null) { Log("  AudioData.json not found"); return; }
        var json = ta.text;

        _bankPath = ExtractJsonString(json, "bankPath") ?? _bankPath;
        _masterBank = ExtractJsonString(json, "masterBank") ?? _masterBank;
        _stringsBank = ExtractJsonString(json, "stringsBank") ?? _stringsBank;
        _uiBank = ExtractJsonString(json, "uiBank");
        _menuMusicEvent = ExtractJsonString(json, "menuMusic");

        _additionalBanks = null;
        int mbIdx = json.IndexOf("\"musicBanks\":");
        if (mbIdx >= 0)
        {
            int arrStart = json.IndexOf('[', mbIdx);
            int arrEnd = json.IndexOf(']', arrStart);
            if (arrStart >= 0 && arrEnd > arrStart)
            {
                var arr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                var parts = arr.Split(',');
                var list = new List<string>();
                foreach (var p in parts)
                {
                    var s = p.Trim().Trim('"');
                    if (!string.IsNullOrEmpty(s)) list.Add(s);
                }
                _additionalBanks = list.ToArray();
            }
        }

        _uiSoundEvents = new Dictionary<string, string>();
        int ueIdx = json.IndexOf("\"uiEvents\":");
        if (ueIdx >= 0)
        {
            int objStart = json.IndexOf('{', ueIdx);
            int objEnd = FindMatchingBrace(json, objStart);
            string block = json.Substring(objStart + 1, objEnd - objStart - 1);
            int pos = 0;
            while (pos < block.Length)
            {
                int kStart = block.IndexOf('"', pos); if (kStart < 0) break;
                int kEnd = block.IndexOf('"', kStart + 1); if (kEnd < 0) break;
                string key = block.Substring(kStart + 1, kEnd - kStart - 1);
                int vStart = block.IndexOf('"', kEnd + 1); if (vStart < 0) break;
                int vEnd = block.IndexOf('"', vStart + 1); if (vEnd < 0) break;
                string val = block.Substring(vStart + 1, vEnd - vStart - 1);
                _uiSoundEvents[key] = val;
                pos = vEnd + 1;
            }
        }

        Log($"  AudioData: ui={_uiBank} music={_menuMusicEvent} uiEvents={_uiSoundEvents?.Count ?? 0} addBanks={_additionalBanks?.Length ?? 0}");
    }

    static Dictionary<string, string> _themeTssGuids;

    static void LoadDofusUISettings()
    {
        var ta = Resources.Load<UnityEngine.TextAsset>("DofusUISettings");
        if (ta == null) { Log("  DofusUISettings.json not found"); return; }
        _themeTssGuids = new Dictionary<string, string>();
        var json = ta.text;
        int dataIdx = json.IndexOf("\"data\":");
        if (dataIdx < 0) return;
        foreach (var theme in THEME_NAMES)
        {
            foreach (var size in new[] { "large", "medium", "small" })
            {
                string key = $"{size}Font{theme}TssRef";
                string guid = ExtractJsonString(json, key);
                if (guid != null)
                {
                    int gStart = guid.IndexOf("'m_AssetGUID': '");
                    if (gStart >= 0)
                    {
                        gStart += 16;
                        int gEnd = guid.IndexOf("'", gStart);
                        if (gEnd > gStart)
                            _themeTssGuids[$"{size}_{theme}"] = guid.Substring(gStart, gEnd - gStart);
                    }
                }
            }
        }
        Log($"  DofusUISettings: {_themeTssGuids.Count} TSS GUIDs loaded ({_themeTssGuids.Count / 3} themes x 3 sizes)");
    }

    static void LoadScreenBehavior()
    {
        var ta = Resources.Load<UnityEngine.TextAsset>("ScreenBehavior");
        if (ta == null) { Log("  ScreenBehavior.json not found"); return; }

        _screenSpecs = new Dictionary<string, ScreenSpec>();
        _flowRules = new Dictionary<string, string>();

        var json = ta.text;
        int screensIdx = json.IndexOf("\"screens\":");
        if (screensIdx >= 0)
        {
            int braceStart = json.IndexOf('{', screensIdx + 10);
            if (braceStart < 0) { Log("  ScreenBehavior: no opening brace for screens"); return; }
            int screensEnd = FindMatchingBrace(json, braceStart);
            int pos = braceStart + 1;
            while (pos < screensEnd)
            {
                int kStart = json.IndexOf('"', pos); if (kStart < 0) break;
                if (json[kStart + 1] == '}') break;
                int kEnd = json.IndexOf('"', kStart + 1); if (kEnd < 0) break;
                string screenName = json.Substring(kStart + 1, kEnd - kStart - 1);

                int objStart = json.IndexOf('{', kEnd);
                int objEnd = FindMatchingBrace(json, objStart);
                string obj = json.Substring(objStart, objEnd - objStart + 1);

                var spec = new ScreenSpec
                {
                    handler = ExtractJsonString(obj, "handler") ?? "",
                    uxml = ExtractJsonString(obj, "uxml") ?? "",
                    binding = ExtractJsonString(obj, "binding") ?? "",
                    elements = new List<ElementSpec>(),
                    i18n = new Dictionary<string, string>(),
                };

                int authUiIdx = obj.IndexOf("\"authUi\":");
                if (authUiIdx >= 0)
                {
                    int numStart = authUiIdx + 9;
                    while (numStart < obj.Length && !char.IsDigit(obj[numStart]) && obj[numStart] != '-') numStart++;
                    int numEnd = numStart;
                    while (numEnd < obj.Length && (char.IsDigit(obj[numEnd]) || obj[numEnd] == '-')) numEnd++;
                    if (int.TryParse(obj.Substring(numStart, numEnd - numStart), out int authUiVal))
                        spec.authUi = authUiVal;
                }

                spec.rawJson = obj;

                // Parse i18n map: "i18n": {"element":"ui.key",...}
                int i18nIdx = obj.IndexOf("\"i18n\":");
                if (i18nIdx >= 0)
                {
                    int i18nStart = obj.IndexOf('{', i18nIdx + 7);
                    int i18nEnd = FindMatchingBrace(obj, i18nStart);
                    string i18nBlock = obj.Substring(i18nStart + 1, i18nEnd - i18nStart - 1);
                    int ip = 0;
                    while (ip < i18nBlock.Length)
                    {
                        int ks = i18nBlock.IndexOf('"', ip); if (ks < 0) break;
                        int ke = i18nBlock.IndexOf('"', ks + 1); if (ke < 0) break;
                        int vs = i18nBlock.IndexOf('"', ke + 1); if (vs < 0) break;
                        int ve = i18nBlock.IndexOf('"', vs + 1); if (ve < 0) break;
                        spec.i18n[i18nBlock.Substring(ks + 1, ke - ks - 1)] =
                            i18nBlock.Substring(vs + 1, ve - vs - 1);
                        ip = ve + 1;
                    }
                }

                // Parse states map: "states": {"idle":{...},"connecting":{...}}
                spec.states = new Dictionary<string, string>();
                int statesIdx = obj.IndexOf("\"states\":");
                if (statesIdx >= 0)
                {
                    int statesStart = obj.IndexOf('{', statesIdx + 9);
                    int statesEnd = FindMatchingBrace(obj, statesStart);
                    string statesBlock = obj.Substring(statesStart + 1, statesEnd - statesStart - 1);
                    int sp = 0;
                    while (sp < statesBlock.Length)
                    {
                        int sks = statesBlock.IndexOf('"', sp); if (sks < 0) break;
                        int ske = statesBlock.IndexOf('"', sks + 1); if (ske < 0) break;
                        string stateName = statesBlock.Substring(sks + 1, ske - sks - 1);
                        int sObjStart = statesBlock.IndexOf('{', ske);
                        if (sObjStart < 0) break;
                        int sObjEnd = FindMatchingBrace(statesBlock, sObjStart);
                        spec.states[stateName] = statesBlock.Substring(sObjStart, sObjEnd - sObjStart + 1);
                        sp = sObjEnd + 1;
                    }
                }

                // Parse i18nDetails: "i18nDetails": {"field": {"toUpper": true, "action": "set_text"}, ...}
                int detIdx = obj.IndexOf("\"i18nDetails\":");
                if (detIdx >= 0)
                {
                    int detStart = obj.IndexOf('{', detIdx + 14);
                    if (detStart >= 0)
                    {
                        int detEnd = FindMatchingBrace(obj, detStart);
                        string detBlock = obj.Substring(detStart + 1, detEnd - detStart - 1);
                        int dp = 0;
                        while (dp < detBlock.Length)
                        {
                            int dks = detBlock.IndexOf('"', dp); if (dks < 0) break;
                            int dke = detBlock.IndexOf('"', dks + 1); if (dke < 0) break;
                            string dField = detBlock.Substring(dks + 1, dke - dks - 1);
                            int doStart = detBlock.IndexOf('{', dke);
                            if (doStart < 0) break;
                            int doEnd = FindMatchingBrace(detBlock, doStart);
                            string detObj = detBlock.Substring(doStart, doEnd - doStart + 1);

                            var fieldDetails = new Dictionary<string, string>();
                            if (detObj.Contains("\"toUpper\": true") || detObj.Contains("\"toUpper\":true"))
                                fieldDetails["toUpper"] = "true";
                            string detAction = ExtractJsonString(detObj, "action");
                            if (detAction != null) fieldDetails["action"] = detAction;

                            if (_i18nDetails == null)
                                _i18nDetails = new Dictionary<string, Dictionary<string, string>>();
                            _i18nDetails[dField] = fieldDetails;
                            dp = doEnd + 1;
                        }
                    }
                }

                _screenSpecs[screenName] = spec;
                pos = objEnd + 1;
            }
        }

        int detailCount = _i18nDetails?.Count ?? 0;
        Log($"  ScreenBehavior: {_screenSpecs.Count} screens loaded, {detailCount} i18nDetails");
        foreach (var kv in _screenSpecs)
            Log($"    {kv.Key}: {kv.Value.i18n.Count} i18n, handler={kv.Value.handler}");
    }

    static int FindMatchingBrace(string json, int openIdx)
    {
        int depth = 0;
        bool inStr = false;
        for (int j = openIdx; j < json.Length; j++)
        {
            if (json[j] == '"' && (j == 0 || json[j - 1] != '\\')) inStr = !inStr;
            if (inStr) continue;
            if (json[j] == '{') depth++;
            else if (json[j] == '}') { depth--; if (depth == 0) return j; }
        }
        return json.Length - 1;
    }

    // i18nDetails: field → {toUpper, action} from decompiled Localize methods
    static Dictionary<string, Dictionary<string, string>> _i18nDetails;

    void ApplyScreenBehavior(string screenName, VisualElement root)
    {
        if (_screenSpecs == null || !_screenSpecs.TryGetValue(screenName, out var spec)) return;

        int i18nApplied = 0;
        foreach (var kv in spec.i18n)
        {
            var el = root.Q(kv.Key);
            if (el == null) continue;
            string text = I18n(kv.Value);
            if (text.StartsWith("[")) continue;

            // Check toUpper from decompiled i18nDetails (e.g. ServerSelection.lbl_title uses ToUpper)
            bool toUpper = false;
            string action = "set_text";
            if (_i18nDetails != null && _i18nDetails.TryGetValue(kv.Key, out var details))
            {
                if (details.TryGetValue("toUpper", out var u) && u == "true") toUpper = true;
                if (details.TryGetValue("action", out var a)) action = a;
            }
            if (toUpper) text = text.ToUpper();

            if (action == "set_placeholder")
            {
                // TextInput placeholder — try via reflection or child label
                var placeholder = el.Q("unity-text-input");
                if (placeholder != null) { /* placeholder text handled by TextField API */ }
                if (el is TextField tf) { tf.label = text; i18nApplied++; }
            }
            else if (action == "set_title")
            {
                // WindowFigma title
                if (el is Label lbl) { lbl.text = text; i18nApplied++; }
            }
            else // set_text
            {
                if (el is Label lbl) { lbl.text = text; i18nApplied++; }
                else if (el is Button btn) { btn.text = text; i18nApplied++; }
                else
                {
                    var innerLabel = el.Q("playButton_label");
                    if (innerLabel is Label il) { il.text = text; i18nApplied++; }
                }
            }
        }

        // Apply initial display states from spec (Login.states.idle etc.)
        int stateApplied = 0;
        if (spec.states != null && spec.states.TryGetValue("idle", out var idleState))
        {
            stateApplied = ApplyDisplayState(root, idleState);
        }

        // Apply initDisplay from decompiled (OnEnable DOTween fallbacks → display:Flex/None)
        int displayApplied = 0;
        if (spec.rawJson != null)
        {
            int initIdx = spec.rawJson.IndexOf("\"initDisplay\":");
            if (initIdx >= 0)
            {
                int idStart = spec.rawJson.IndexOf('{', initIdx + 14);
                if (idStart >= 0)
                {
                    int idEnd = FindMatchingBrace(spec.rawJson, idStart);
                    string initBlock = spec.rawJson.Substring(idStart + 1, idEnd - idStart - 1);
                    int ip = 0;
                    while (ip < initBlock.Length)
                    {
                        int ks = initBlock.IndexOf('"', ip); if (ks < 0) break;
                        int ke = initBlock.IndexOf('"', ks + 1); if (ke < 0) break;
                        int vs = initBlock.IndexOf('"', ke + 1); if (vs < 0) break;
                        int ve = initBlock.IndexOf('"', vs + 1); if (ve < 0) break;
                        string elName = initBlock.Substring(ks + 1, ke - ks - 1);
                        string val = initBlock.Substring(vs + 1, ve - vs - 1);
                        var el = root.Q(elName);
                        if (el != null)
                        {
                            el.style.display = val == "None" ? DisplayStyle.None : DisplayStyle.Flex;
                            displayApplied++;
                        }
                        ip = ve + 1;
                    }
                }
            }

            // Apply visibles from decompiled
            int visApplied = ApplyJsonArrayOps(root, spec.rawJson, "visibles", "field", "value",
                (el, val) => {
                    if (val == "true") { el.style.visibility = Visibility.Visible; return true; }
                    if (val == "false") { el.style.visibility = Visibility.Hidden; return true; }
                    return false;
                });
            displayApplied += visApplied;
        }

        Log($"  {screenName}: ScreenBehavior applied {i18nApplied} I18n, {stateApplied} states, {displayApplied} display");
    }

    int ApplyDisplayState(VisualElement root, string stateJson)
    {
        int applied = 0;
        int pos = 0;
        while (pos < stateJson.Length)
        {
            int kStart = stateJson.IndexOf('"', pos); if (kStart < 0) break;
            int kEnd = stateJson.IndexOf('"', kStart + 1); if (kEnd < 0) break;
            string key = stateJson.Substring(kStart + 1, kEnd - kStart - 1);

            int vStart = kEnd + 1;
            while (vStart < stateJson.Length && (stateJson[vStart] == ':' || stateJson[vStart] == ' ')) vStart++;

            if (key.EndsWith(".enabled"))
            {
                string elName = key.Substring(0, key.Length - 8);
                bool enabled = stateJson.IndexOf("true", vStart, System.StringComparison.OrdinalIgnoreCase) == vStart;
                var el = root.Q(elName);
                if (el != null) { el.SetEnabled(enabled); applied++; }
                pos = vStart + 5;
            }
            else
            {
                int vs = stateJson.IndexOf('"', vStart); if (vs < 0) break;
                int ve = stateJson.IndexOf('"', vs + 1); if (ve < 0) break;
                string value = stateJson.Substring(vs + 1, ve - vs - 1);
                var el = root.Q(key);
                if (el != null)
                {
                    el.style.display = value == "None" ? DisplayStyle.None : DisplayStyle.Flex;
                    applied++;
                }
                pos = ve + 1;
            }
        }
        return applied;
    }

    int ApplyJsonArrayOps(VisualElement root, string json, string arrayName,
        string fieldKey, string valueKey, System.Func<VisualElement, string, bool> apply)
    {
        int applied = 0;
        string search = $"\"{arrayName}\":";
        int idx = json.IndexOf(search);
        if (idx < 0) return 0;
        int arrStart = json.IndexOf('[', idx + search.Length);
        if (arrStart < 0) return 0;
        // Find matching ]
        int depth = 0;
        int arrEnd = arrStart;
        for (int j = arrStart; j < json.Length; j++)
        {
            if (json[j] == '[') depth++;
            else if (json[j] == ']') { depth--; if (depth == 0) { arrEnd = j; break; } }
        }
        string arr = json.Substring(arrStart, arrEnd - arrStart + 1);
        // Parse each object in array: {"field":"xxx","value":"yyy",...}
        int pos = 0;
        while (pos < arr.Length)
        {
            int objS = arr.IndexOf('{', pos);
            if (objS < 0) break;
            int objE = arr.IndexOf('}', objS);
            if (objE < 0) break;
            string obj = arr.Substring(objS, objE - objS + 1);
            string field = ExtractJsonString(obj, fieldKey);
            string value = ExtractJsonString(obj, valueKey);
            if (field != null && field != "?" && value != null)
            {
                var el = root.Q(field);
                if (el != null && apply(el, value))
                    applied++;
            }
            pos = objE + 1;
        }
        return applied;
    }

    static void LoadServerNames()
    {
        var ta = Resources.Load<UnityEngine.TextAsset>("ServerNames");
        if (ta == null) { Log("  ServerNames.json not found"); return; }
        _serverNames = new Dictionary<string, string>();
        // Simple JSON parse (same pattern as LoadI18n)
        var json = ta.text;
        int pos = 0;
        while (pos < json.Length)
        {
            int kStart = json.IndexOf('"', pos); if (kStart < 0) break;
            int kEnd = json.IndexOf('"', kStart + 1); if (kEnd < 0) break;
            int vStart = json.IndexOf('"', kEnd + 1); if (vStart < 0) break;
            int vEnd = vStart + 1;
            while (vEnd < json.Length) { if (json[vEnd] == '"' && json[vEnd - 1] != '\\') break; vEnd++; }
            _serverNames[json.Substring(kStart + 1, kEnd - kStart - 1)] =
                json.Substring(vStart + 1, vEnd - vStart - 1);
            pos = vEnd + 1;
        }
        Log($"  ServerNames: {_serverNames.Count} loaded");
    }

    static Dictionary<string, int> _serverTypes; // serverId → gameTypeId
    static Dictionary<string, string> _serverTypeNames; // typeId → name (from RuntimeData)

    static void LoadRuntimeData()
    {
        var ta = Resources.Load<UnityEngine.TextAsset>("RuntimeData");
        if (ta == null) { Log("  RuntimeData.json not found"); return; }

        _screenUxmlMap = new Dictionary<string, string>();
        _serverTypeNames = new Dictionary<string, string>();

        // Parse screens section
        var json = ta.text;
        int screensIdx = json.IndexOf("\"screens\":");
        if (screensIdx >= 0)
        {
            int scrBrace = json.IndexOf('{', screensIdx + 10);
            if (scrBrace < 0) { Log("  RuntimeData: no screens brace"); }
            else {
            int scrEnd = FindMatchingBrace(json, scrBrace);
            int pos = scrBrace + 1;
            while (pos < scrEnd)
            {
                int kStart = json.IndexOf('"', pos); if (kStart < 0) break;
                if (json[kStart + 1] == '}') break;
                int kEnd = json.IndexOf('"', kStart + 1); if (kEnd < 0) break;
                string screenName = json.Substring(kStart + 1, kEnd - kStart - 1);

                // Find "uxml": in this screen object
                int objStart = json.IndexOf('{', kEnd);
                int objEnd = objStart;
                int depth = 0;
                bool inStr = false;
                for (int j = objStart; j < json.Length; j++)
                {
                    if (json[j] == '"' && (j == 0 || json[j-1] != '\\')) inStr = !inStr;
                    if (inStr) continue;
                    if (json[j] == '{') depth++;
                    else if (json[j] == '}') { depth--; if (depth == 0) { objEnd = j; break; } }
                }
                string obj = json.Substring(objStart, objEnd - objStart + 1);
                string uxml = ExtractJsonString(obj, "uxml");
                if (uxml != null)
                    _screenUxmlMap[screenName] = uxml;

                pos = objEnd + 1;
            }
            } // scrBrace else
        }

        // Parse serverTypes section
        int stIdx = json.IndexOf("\"serverTypes\":");
        if (stIdx >= 0)
        {
            int stBrace = json.IndexOf('{', stIdx + 14);
            if (stBrace < 0) { Log("  RuntimeData: no serverTypes brace"); }
            else {
            int stEnd = FindMatchingBrace(json, stBrace);
            int pos = stBrace + 1;
            while (pos < stEnd)
            {
                int kStart = json.IndexOf('"', pos); if (kStart < 0) break;
                if (json[kStart + 1] == '}') break;
                int kEnd = json.IndexOf('"', kStart + 1); if (kEnd < 0) break;
                string typeId = json.Substring(kStart + 1, kEnd - kStart - 1);
                int vStart = json.IndexOf('"', kEnd + 1); if (vStart < 0) break;
                int vEnd = json.IndexOf('"', vStart + 1); if (vEnd < 0) break;
                _serverTypeNames[typeId] = json.Substring(vStart + 1, vEnd - vStart - 1);
                pos = vEnd + 1;
            }
            } // stBrace else
        }

        Log($"  RuntimeData: {_screenUxmlMap.Count} screens, {_serverTypeNames.Count} server types");
    }

    static string ServerName(int id) =>
        _serverNames != null && _serverNames.TryGetValue(id.ToString(), out var n) ? n : $"Server {id}";

    static string ServerTypeName(int typeId) =>
        _serverTypeNames != null && _serverTypeNames.TryGetValue(typeId.ToString(), out var n) ? n : "";

    void ParseHaapiServerList(string json)
    {
        // Parse: {"servers": [{"id":290,"name":"Tal Kasha","type":1,...}, ...]}
        if (_serverNames == null) _serverNames = new Dictionary<string, string>();
        if (_serverTypes == null) _serverTypes = new Dictionary<string, int>();

        int pos = json.IndexOf("\"servers\"");
        if (pos < 0) return;

        // Simple parser: find each {"id":X,"name":"Y","type":Z,...}
        int searchFrom = pos;
        while (true)
        {
            int objStart = json.IndexOf("{\"id\":", searchFrom);
            if (objStart < 0) break;
            int objEnd = json.IndexOf('}', objStart);
            if (objEnd < 0) break;
            string obj = json.Substring(objStart, objEnd - objStart + 1);

            string idStr = ExtractJsonString(obj, "id");
            string name = ExtractJsonString(obj, "name");
            string typeStr = ExtractJsonString(obj, "type");

            // id and type might be numbers without quotes
            if (idStr == null)
            {
                var m = System.Text.RegularExpressions.Regex.Match(obj, @"""id""\s*:\s*(\d+)");
                if (m.Success) idStr = m.Groups[1].Value;
            }
            if (typeStr == null)
            {
                var m = System.Text.RegularExpressions.Regex.Match(obj, @"""type""\s*:\s*(\d+)");
                if (m.Success) typeStr = m.Groups[1].Value;
            }

            if (idStr != null && name != null)
                _serverNames[idStr] = name;
            if (idStr != null && typeStr != null && int.TryParse(typeStr, out int t))
                _serverTypes[idStr] = t;

            searchFrom = objEnd + 1;
        }
    }

    static void CreateCamera()
    {
        var go = new GameObject("__WebGLCam");
        DontDestroyOnLoad(go);
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.10f);
        cam.depth = -100;
        cam.cullingMask = 0;
        Log("  Camera: solid dark indigo clear");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Custom element factory registration.
    //
    // The game's [UxmlElement] types (DofusVisualElement, WindowFigma etc.)
    // have UxmlSerializedData inner classes whose CreateInstance() may be
    // broken from IL recovery. Unity falls back to VisualElementFactoryRegistry.
    // We register UxmlFactory<T> instances so the fallback creates the
    // correct types instead of plain VisualElement/Label.
    // ══════════════════════════════════════════════════════════════════════

    // ── WebGLClickable: replacement for ExtendedClickable (broken IL) ──
    // Original ExtendedClickable extends AltAllowedClickable → Clickable → PointerManipulator.
    // Intercepts pointer events to prevent UIToolkit default :hover/:active styles.
    // Types that use it: PlayButton, DofusButtonCustom, DropdownButton (from constructor analysis).
    class WebGLClickable : Clickable
    {
        public WebGLClickable(Action onClick) : base(onClick) { }
        public WebGLClickable() : base(() => { }) { }

        protected override void RegisterCallbacksOnTarget()
        {
            base.RegisterCallbacksOnTarget();
            target.RegisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerOutEvent>(OnOut, TrickleDown.TrickleDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            base.UnregisterCallbacksFromTarget();
            target.UnregisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerOutEvent>(OnOut, TrickleDown.TrickleDown);
        }

        void OnOver(PointerOverEvent evt)
        {
            target.AddToClassList("hovered");
            if (_uiSoundEvents != null && _uiSoundEvents.TryGetValue("event:/Ui/RollOver", out _))
                FMODProxy.PlayEvent("event:/Ui/RollOver", 0.5f);
        }
        void OnOut(PointerOutEvent evt)
        {
            target.RemoveFromClassList("hovered");
            target.RemoveFromClassList("pressed");
        }
        protected override void ProcessDownEvent(EventBase evt, Vector2 localPosition, int pointerId)
        {
            base.ProcessDownEvent(evt, localPosition, pointerId);
            if (_uiSoundEvents != null && _uiSoundEvents.TryGetValue("event:/Ui/Bouton_Générique", out _))
                FMODProxy.PlayEvent("event:/Ui/Bouton_Générique");
        }
    }

    static void AddClickable(VisualElement el)
    {
        el.AddManipulator(new WebGLClickable());
        el.RegisterCallback<FocusInEvent>(_ => {
            el.style.borderTopColor = Color.clear;
            el.style.borderBottomColor = Color.clear;
            el.style.borderLeftColor = Color.clear;
            el.style.borderRightColor = Color.clear;
        });
    }

    // ── Loading spinner rotation (ConnectionBackgroundBinding._iconLoading) ──
    // The original ecr (LoadingCoordinator, dump.cs L285586) manages loading phases.
    // In WebGL we show/rotate the _iconLoading element from connectionBackground.uxml.
    IEnumerator RotateLoadingIcon()
    {
        float angle = 0;
        while (_loadingIcon != null && _loadingIcon.resolvedStyle.display == DisplayStyle.Flex)
        {
            angle = (angle + 360f * Time.deltaTime) % 360f;
            _loadingIcon.style.rotate = new StyleRotate(new Rotate(angle));
            yield return null;
        }
    }

    void ShowLoadingSpinner()
    {
        if (_loadingIcon == null) return;
        _loadingIcon.style.display = DisplayStyle.Flex;
        if (_loadingRotation != null) StopCoroutine(_loadingRotation);
        _loadingRotation = StartCoroutine(RotateLoadingIcon());
        Log("  Loading spinner started");
    }

    void HideLoadingSpinner()
    {
        if (_loadingIcon != null)
            _loadingIcon.style.display = DisplayStyle.None;
        if (_loadingRotation != null) { StopCoroutine(_loadingRotation); _loadingRotation = null; }
        Log("  Loading spinner hidden");
    }

    // ── Per-element USS loading: replicates what AddStyleSheetPath does in constructors ──
    // Mapping derived from USS file names: Core.UILogic.Components.{Name}.uss
    static Dictionary<string, StyleSheet> _componentUSS;
    static void LoadComponentUSS(VisualElement el, string ussName)
    {
        if (_componentUSS == null)
        {
            _componentUSS = new Dictionary<string, StyleSheet>();
            foreach (var kv in _loadedBundlesStatic)
            {
                foreach (var ss in kv.Value.LoadAllAssets<StyleSheet>())
                {
                    if (ss != null && ss.name.StartsWith("Core.UILogic.Components."))
                        _componentUSS[ss.name] = ss;
                }
            }
            Log($"[USS] Component USS cache: {_componentUSS.Count} sheets");
        }
        if (_componentUSS.TryGetValue(ussName, out var sheet))
            el.styleSheets.Add(sheet);
    }
    static Dictionary<string, AssetBundle> _loadedBundlesStatic;

    // ── Data-driven factories: ALL behavior from ClientData.json ──
    // Generated by Tools/ClientAnalyzer/analyze_client.py from dump.cs + decompiled_all.c.
    // Zero hardcoded child hierarchies — everything extracted from the original client binary.
#pragma warning disable CS0618

    static void SetupDVE(VisualElement el) { el.AddToClassList("DofusVisualElement"); }
    static void SetupDBE(VisualElement el) { SetupDVE(el); el.AddToClassList("DofusBindableElement"); }

    // Four factory classes (one per base type), all data-driven
    // Parsed tree node from ClientData.json: {"n":"name","t":"VE|LBL|ICO|BTN","cls":["x"],"ch":[...]}
    // Names come from decrypted OPS strings (global-metadata.dat) and match the CSS selectors
    // in the MergedUSS (tss_assets_all). E.g. playButton_container, playButton_background, etc.
    struct TreeNode { public string n, t, spr; public string[] cls; public TreeNode[] ch; }

    static Dictionary<string, Texture2D> _spriteByPath;
    static string _currentFactoryType;

    static VisualElement CreateChild(TreeNode node)
    {
        VisualElement el;
        switch (node.t)
        {
            case "LBL": el = new Label(); el.AddToClassList("DofusLabel"); break;
            case "ICO": el = new VisualElement(); SetupDVE(el); el.AddToClassList("DofusIcon"); break;
            case "BTN": el = new Button(); break;
            default: el = new VisualElement(); break;
        }
        if (!string.IsNullOrEmpty(node.n)) el.name = node.n;
        if (node.cls != null) foreach (var c in node.cls) el.AddToClassList(c);
        if (!string.IsNullOrEmpty(node.spr)) ApplySprite(el, node.spr);
        if (node.ch != null) foreach (var child in node.ch) el.Add(CreateChild(child));
        return el;
    }

    static void BuildSpriteCache()
    {
        if (_spriteByPath != null || _loadedBundlesStatic == null) return;
        _spriteByPath = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _loadedBundlesStatic)
        {
            foreach (var assetName in kv.Value.GetAllAssetNames())
            {
                if (!assetName.EndsWith(".png") && !assetName.EndsWith(".jpg")) continue;
                var tex = kv.Value.LoadAsset<Texture2D>(assetName);
                if (tex != null)
                    _spriteByPath[assetName] = tex;
            }
        }
        Log($"[Sprite] Cache built: {_spriteByPath.Count} textures by addressable path");
    }

    static void ApplySprite(VisualElement el, string spriteName)
    {
        BuildSpriteCache();
        if (_spriteByPath == null) return;

        string typeLower = _currentFactoryType?.ToLower() ?? "";
        string sprLower = "/" + spriteName.ToLower();
        Texture2D best = null;

        foreach (var kv in _spriteByPath)
        {
            string path = kv.Key.ToLower();
            if (!path.EndsWith(sprLower + ".png") && !path.EndsWith(sprLower + ".jpg")) continue;
            if (best == null)
                best = kv.Value;
            if (typeLower.Length > 0 && path.Contains(typeLower))
            { best = kv.Value; break; }
        }
        if (best != null)
            el.style.backgroundImage = new StyleBackground(best);
    }

    class DDFactory_VE : UxmlFactory<VisualElement>
    {
        internal string _cls; internal bool _isDVE, _isDBE; internal string[] _uss;
        internal bool _clickable; internal TreeNode[] _tree;
        public DDFactory_VE() { _cls = ""; }
        public override VisualElement Create(IUxmlAttributes bag, CreationContext cc)
        {
            var el = base.Create(bag, cc);
            if (_isDBE) SetupDBE(el); else if (_isDVE) SetupDVE(el);
            if (_cls.Length > 0) el.AddToClassList(_cls);
            if (_uss != null) foreach (var u in _uss) el.AddToClassList(u);
            if (_tree != null) { _currentFactoryType = _cls; foreach (var ch in _tree) el.Add(CreateChild(ch)); }
            LoadComponentUSS(el, "Core.UILogic.Components." + _cls);
            if (_clickable) AddClickable(el);
            return el;
        }
    }
    static void SetPickingModeRecursive(VisualElement el, PickingMode mode)
    {
        if (el == null) return;
        el.pickingMode = mode;
        for (int i = 0; i < el.childCount; i++)
            SetPickingModeRecursive(el[i], mode);
    }

    VisualTreeAsset LoadUxml(string pathOrName)
    {
        if (string.IsNullOrEmpty(pathOrName)) return null;
        string lower = pathOrName.ToLower().Replace(".asset", "");
        if (!lower.EndsWith(".uxml")) lower += ".uxml";
        string fileName = lower.Substring(lower.LastIndexOf('/') + 1);

        var bundles = _loadedBundles ?? _loadedBundlesStatic;
        if (bundles != null)
        {
            foreach (var kv in bundles)
            {
                if (kv.Value == null) continue;

                var vta = kv.Value.LoadAsset<VisualTreeAsset>(lower)
                       ?? kv.Value.LoadAsset<VisualTreeAsset>(lower + ".asset")
                       ?? (kv.Value.LoadAsset(lower) as VisualTreeAsset)
                       ?? (kv.Value.LoadAsset(lower + ".asset") as VisualTreeAsset);
                if (vta != null)
                {
                    Log($"  [LoadUxml] Found '{pathOrName}' in bundle '{kv.Key}' (by path)");
                    return vta;
                }

                vta = kv.Value.LoadAsset<VisualTreeAsset>(fileName)
                   ?? (kv.Value.LoadAsset(fileName) as VisualTreeAsset);
                if (vta != null)
                {
                    Log($"  [LoadUxml] Found '{pathOrName}' in bundle '{kv.Key}' (by filename)");
                    return vta;
                }

                foreach (var assetName in kv.Value.GetAllAssetNames())
                {
                    string anLower = assetName.ToLower();
                    if (anLower == lower || anLower == lower + ".asset" ||
                        anLower.EndsWith("/" + fileName) || anLower.EndsWith("/" + fileName + ".asset"))
                    {
                        var loaded = kv.Value.LoadAsset<VisualTreeAsset>(assetName)
                                  ?? (kv.Value.LoadAsset(assetName) as VisualTreeAsset);
                        if (loaded != null)
                        {
                            Log($"  [LoadUxml] Found '{pathOrName}' in bundle '{kv.Key}' (by GetAllAssetNames: '{assetName}')");
                            return loaded;
                        }
                    }
                }
            }
        }
        Log($"  [LoadUxml] NOT FOUND: '{pathOrName}' (lower='{lower}', file='{fileName}', bundles={bundles?.Count ?? 0})");
        return null;
    }

    class DDFactory_Btn : UxmlFactory<Button>
    {
        internal string _cls; internal bool _isDVE, _isDBE; internal string[] _uss;
        internal TreeNode[] _tree;
        public DDFactory_Btn() { _cls = ""; }
        public override VisualElement Create(IUxmlAttributes bag, CreationContext cc)
        {
            var el = base.Create(bag, cc);
            if (_isDBE) SetupDBE(el); else if (_isDVE) SetupDVE(el);
            if (_cls.Length > 0) el.AddToClassList(_cls);
            if (_uss != null) foreach (var u in _uss) el.AddToClassList(u);
            if (_tree != null)
            {
                _currentFactoryType = _cls;
                foreach (var ch in _tree)
                {
                    var child = CreateChild(ch);
                    SetPickingModeRecursive(child, PickingMode.Ignore);
                    el.Add(child);
                }
            }
            LoadComponentUSS(el, "Core.UILogic.Components." + _cls);
            return el;
        }
    }
    class DDFactory_Lbl : UxmlFactory<Label>
    {
        internal string _cls; internal bool _isDVE, _isDBE; internal string[] _uss;
        public DDFactory_Lbl() { _cls = ""; }
        public override VisualElement Create(IUxmlAttributes bag, CreationContext cc)
        {
            var el = base.Create(bag, cc);
            if (_isDBE) SetupDBE(el); else if (_isDVE) SetupDVE(el);
            if (_cls.Length > 0) { el.AddToClassList(_cls); el.AddToClassList("DofusLabel"); }
            if (_uss != null) foreach (var u in _uss) el.AddToClassList(u);
            return el;
        }
    }
    class DDFactory_TF : UxmlFactory<TextField>
    {
        internal string _cls; internal bool _isDVE, _isDBE; internal string[] _uss;
        internal bool _clickable; internal TreeNode[] _tree;
        public DDFactory_TF() { _cls = ""; }
        public override VisualElement Create(IUxmlAttributes bag, CreationContext cc)
        {
            var el = base.Create(bag, cc);
            if (_isDBE) SetupDBE(el); else if (_isDVE) SetupDVE(el);
            if (_cls.Length > 0) el.AddToClassList(_cls);
            if (_uss != null) foreach (var u in _uss) el.AddToClassList(u);
            if (_tree != null) { _currentFactoryType = _cls; foreach (var ch in _tree) el.Add(CreateChild(ch)); }
            LoadComponentUSS(el, "Core.UILogic.Components." + _cls);
            if (_clickable) AddClickable(el);
            return el;
        }
    }
#pragma warning restore CS0618

    static void RegisterCustomElements()
    {
        var uiAsm = typeof(VisualElement).Assembly;
        var registryType = uiAsm.GetType("UnityEngine.UIElements.VisualElementFactoryRegistry");
        if (registryType == null) { FLog("FATAL: registry not found"); return; }

        var getFactories = registryType.GetMethod("get_factories", STATIC);
        object factoriesDict = null;
        if (getFactories != null)
            try { factoriesDict = getFactories.Invoke(null, null); } catch { }
        var sField = registryType.GetField("s_Factories", STATIC);
        if (factoriesDict == null && sField != null) factoriesDict = sField.GetValue(null);

        var iFactoryType = uiAsm.GetType("UnityEngine.UIElements.IUxmlFactory");
        if (factoriesDict == null || iFactoryType == null) { FLog("FATAL: cannot access s_Factories"); return; }

        FLog($"s_Factories initial={DictCount(factoriesDict)}");

        // Load ClientData.json — extracted by Tools/ClientAnalyzer/analyze_client.py
        // from dump.cs (type hierarchy, const fields) + decompiled_all.c (constructor ops).
        // Contains: factory base types, USS classes, clickable flags, child trees — ALL data-driven.
        var clientDataJson = Resources.Load<UnityEngine.TextAsset>("ClientData");
        var cdEntries = new Dictionary<string, (string baseType, string[] uss, bool clickable, TreeNode[] tree)>();

        if (clientDataJson != null)
        {
            try { ParseClientData(clientDataJson.text, cdEntries); }
            catch (Exception e) { FLog($"ClientData.json parse error: {e.Message}"); }
            FLog($"ClientData.json: {cdEntries.Count} factory entries loaded");
        }
        else
        {
            FLog("ClientData.json not found in Resources — using inheritance fallback");
        }

        var dveType = FindType("Core.UILogic.Components.Figma.DofusVisualElement");
        var dbeType = FindType("Core.UILogic.Components.Figma.DofusBindableElement");

        var customTypes = new List<Type>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types = null;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            catch { continue; }
            if (types == null) continue;
            foreach (var t in types)
            {
                if (t == null) continue;
                try
                {
                    if (t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition) continue;
                    if (!typeof(VisualElement).IsAssignableFrom(t)) continue;
                    if (t.Namespace == null) continue;
                    if (t.Namespace.StartsWith("UnityEngine") || t.Namespace.StartsWith("UnityEditor") ||
                        t.Namespace.StartsWith("Unity.")) continue;
                    if (t.DeclaringType == typeof(WebGLProbe)) continue;
                    customTypes.Add(t);
                }
                catch { }
            }
        }

        FLog($"Scanned: {customTypes.Count} custom VE types");

        var listType = typeof(List<>).MakeGenericType(iFactoryType);
        var addMethod = listType.GetMethod("Add");
        var setItem = factoriesDict.GetType().GetMethod("set_Item");
        int genVE = 0, genBtn = 0, genLbl = 0, genTF = 0, fail = 0;

        foreach (var type in customTypes)
        {
            bool isDVE = dveType != null && dveType.IsAssignableFrom(type);
            bool isDBE = dbeType != null && dbeType.IsAssignableFrom(type);
            string shortName = type.Name;

            string baseType = "VisualElement";
            string[] ussFromData = null;
            bool wantsClickable = false;
            TreeNode[] tree = null;

            if (cdEntries.TryGetValue(type.FullName, out var cd))
            {
                baseType = cd.baseType;
                ussFromData = cd.uss;
                wantsClickable = cd.clickable;
                tree = cd.tree;
            }
            else
            {
                if (typeof(Label).IsAssignableFrom(type)) baseType = "Label";
                else if (typeof(TextField).IsAssignableFrom(type)) baseType = "TextField";
                else if (typeof(Button).IsAssignableFrom(type)) baseType = "Button";
                else if (typeof(Toggle).IsAssignableFrom(type)) baseType = "Toggle";
            }

            object factory;
            if (baseType == "Label")
            {
                var f = new DDFactory_Lbl(); f._cls = shortName; f._isDVE = isDVE; f._isDBE = isDBE; f._uss = ussFromData;
                factory = f; genLbl++;
            }
            else if (baseType == "TextField")
            {
                var f = new DDFactory_TF(); f._cls = shortName; f._isDVE = isDVE; f._isDBE = isDBE; f._uss = ussFromData; f._tree = tree; f._clickable = wantsClickable;
                factory = f; genTF++;
            }
            else if (baseType == "Button")
            {
                var f = new DDFactory_Btn(); f._cls = shortName; f._isDVE = isDVE; f._isDBE = isDBE; f._uss = ussFromData; f._tree = tree;
                factory = f; genBtn++;
            }
            else
            {
                var f = new DDFactory_VE(); f._cls = shortName; f._isDVE = isDVE; f._isDBE = isDBE; f._uss = ussFromData; f._tree = tree; f._clickable = wantsClickable;
                factory = f; genVE++;
            }

            try
            {
                var list = Activator.CreateInstance(listType);
                addMethod.Invoke(list, new[] { factory });
                setItem.Invoke(factoriesDict, new object[] { type.FullName, list });
            }
            catch (Exception e)
            {
                fail++;
                FLog($"  {type.Name}: FAIL — {e.InnerException?.Message ?? e.Message}");
            }
        }

        int total = genVE + genBtn + genLbl + genTF;
        FLog($"Registered {total}: {genVE} VE, {genBtn} Btn, {genLbl} Lbl, {genTF} TF ({fail} failed)");
        FLog($"s_Factories final={DictCount(factoriesDict)}");
    }

    static void FLog(string m) { _factoryLog.Add(m); Log($"[F] {m}"); }

    static void ParseClientData(string json, Dictionary<string, (string baseType, string[] uss, bool clickable, TreeNode[] tree)> entries)
    {
        int i = json.IndexOf("\"factories\":{");
        if (i < 0) return;
        i += 13;

        while (i < json.Length)
        {
            i = json.IndexOf('"', i); if (i < 0) break;
            if (i + 1 >= json.Length || json[i + 1] == '}') break;
            int keyEnd = json.IndexOf('"', i + 1);
            if (keyEnd < 0) break;
            string fullName = json.Substring(i + 1, keyEnd - i - 1);
            i = json.IndexOf('{', keyEnd);
            if (i < 0) break;

            int objStart = i;
            int depth = 0;
            bool inStr = false;
            for (int j = i; j < json.Length; j++)
            {
                if (json[j] == '"' && (j == 0 || json[j - 1] != '\\')) { inStr = !inStr; continue; }
                if (inStr) continue;
                if (json[j] == '{') depth++;
                else if (json[j] == '}') { depth--; if (depth == 0) { i = j + 1; break; } }
            }
            if (depth != 0) break;
            string obj = json.Substring(objStart, i - objStart);

            string baseType = ExtractStr(obj, "\"b\":\"");
            if (baseType == null) baseType = "VisualElement";
            bool clickable = obj.Contains("\"c\":true");

            string[] uss = ExtractStrArray(obj, "\"u\":");
            TreeNode[] tree = ExtractTree(obj, "\"tr\":");

            entries[fullName] = (baseType, uss, clickable, tree);
        }

        _ussOverrides = new Dictionary<string, string[]>();
        int ovIdx = json.IndexOf("\"ussOverrides\":{");
        if (ovIdx >= 0)
        {
            int pos = ovIdx + 16;
            while (pos < json.Length)
            {
                pos = json.IndexOf('"', pos); if (pos < 0) break;
                if (pos + 1 >= json.Length || json[pos + 1] == '}') break;
                int kEnd = json.IndexOf('"', pos + 1);
                if (kEnd < 0) break;
                string cls = json.Substring(pos + 1, kEnd - pos - 1);
                string[] children = ExtractStrArray(json.Substring(kEnd, Math.Min(200, json.Length - kEnd)), ":");
                if (children != null)
                    _ussOverrides[cls] = children;
                int arrEnd = json.IndexOf(']', kEnd);
                if (arrEnd < 0) break;
                pos = arrEnd + 1;
            }
            Log($"[USS] Overrides loaded: {_ussOverrides.Count} parent classes");
        }

        // Parse assetPaths: {"key": "path", ...} — from dump.cs const string fields
        _assetPaths = new Dictionary<string, string>();
        int apIdx = json.IndexOf("\"assetPaths\":{");
        if (apIdx >= 0)
        {
            int pos = apIdx + 14;
            while (pos < json.Length)
            {
                pos = json.IndexOf('"', pos); if (pos < 0) break;
                if (pos + 1 >= json.Length || json[pos + 1] == '}') break;
                int kEnd = json.IndexOf('"', pos + 1);
                if (kEnd < 0) break;
                string key = json.Substring(pos + 1, kEnd - pos - 1);
                int vStart = json.IndexOf('"', kEnd + 1);
                if (vStart < 0) break;
                int vEnd = json.IndexOf('"', vStart + 1);
                if (vEnd < 0) break;
                _assetPaths[key] = json.Substring(vStart + 1, vEnd - vStart - 1);
                pos = vEnd + 1;
            }
            Log($"[Data] AssetPaths loaded: {_assetPaths.Count}");
        }
    }

    static string ExtractStr(string obj, string key)
    {
        int i = obj.IndexOf(key);
        if (i < 0) return null;
        i += key.Length;
        int end = obj.IndexOf('"', i);
        return end > i ? obj.Substring(i, end - i) : null;
    }

    static string[] ExtractStrArray(string obj, string key)
    {
        int i = obj.IndexOf(key);
        if (i < 0) return null;
        i += key.Length;
        if (i >= obj.Length || obj[i] != '[') return null;
        int end = obj.IndexOf(']', i);
        if (end <= i + 1) return null;
        string inner = obj.Substring(i + 1, end - i - 1);
        var parts = inner.Split(',');
        var result = new List<string>();
        foreach (var p in parts)
        {
            var t = p.Trim().Trim('"');
            if (t.Length > 0) result.Add(t);
        }
        return result.Count > 0 ? result.ToArray() : null;
    }

    static TreeNode[] ExtractTree(string obj, string key)
    {
        int i = obj.IndexOf(key);
        if (i < 0) return null;
        i += key.Length;
        if (i >= obj.Length || obj[i] != '[') return null;
        return ParseNodeArray(obj, ref i);
    }
    static TreeNode[] ParseNodeArray(string s, ref int pos)
    {
        if (pos >= s.Length || s[pos] != '[') return null;
        pos++;
        var nodes = new List<TreeNode>();
        while (pos < s.Length)
        {
            SkipWS(s, ref pos);
            if (pos >= s.Length || s[pos] == ']') { pos++; return nodes.ToArray(); }
            if (s[pos] == ',') { pos++; continue; }
            if (s[pos] == '{') nodes.Add(ParseNode(s, ref pos));
            else break;
        }
        return nodes.Count > 0 ? nodes.ToArray() : null;
    }
    static TreeNode ParseNode(string s, ref int pos)
    {
        var node = new TreeNode();
        pos++;
        while (pos < s.Length && s[pos] != '}')
        {
            SkipWS(s, ref pos);
            if (pos >= s.Length) break;
            if (s[pos] == ',') { pos++; continue; }
            if (s[pos] == '"')
            {
                string k = ReadQuoted(s, ref pos);
                SkipWS(s, ref pos);
                if (pos < s.Length && s[pos] == ':') pos++;
                SkipWS(s, ref pos);
                if (pos >= s.Length) break;
                if (k == "n") node.n = ReadQuoted(s, ref pos);
                else if (k == "t") node.t = ReadQuoted(s, ref pos);
                else if (k == "spr") node.spr = ReadQuoted(s, ref pos);
                else if (k == "cls")
                {
                    var cls = new List<string>();
                    if (pos < s.Length && s[pos] == '[')
                    {
                        pos++;
                        while (pos < s.Length && s[pos] != ']')
                        {
                            SkipWS(s, ref pos);
                            if (pos >= s.Length) break;
                            if (s[pos] == ',') { pos++; continue; }
                            if (s[pos] == '"') cls.Add(ReadQuoted(s, ref pos));
                            else break;
                        }
                        if (pos < s.Length) pos++;
                    }
                    node.cls = cls.Count > 0 ? cls.ToArray() : null;
                }
                else if (k == "ch") node.ch = ParseNodeArray(s, ref pos);
                else SkipValue(s, ref pos);
            }
            else break;
        }
        if (pos < s.Length && s[pos] == '}') pos++;
        return node;
    }

    static string ReadQuoted(string s, ref int pos)
    {
        if (pos >= s.Length || s[pos] != '"') return "";
        pos++;
        int start = pos;
        while (pos < s.Length && s[pos] != '"') pos++;
        string val = s.Substring(start, pos - start);
        if (pos < s.Length) pos++;
        return val;
    }

    static void SkipWS(string s, ref int pos)
    { while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\n' || s[pos] == '\r')) pos++; }

    static void SkipValue(string s, ref int pos)
    {
        if (pos >= s.Length) return;
        if (s[pos] == '"') { ReadQuoted(s, ref pos); return; }
        if (s[pos] == '[') { int d = 1; pos++; while (pos < s.Length && d > 0) { if (s[pos] == '[') d++; if (s[pos] == ']') d--; pos++; } return; }
        if (s[pos] == '{') { int d = 1; pos++; while (pos < s.Length && d > 0) { if (s[pos] == '{') d++; if (s[pos] == '}') d--; pos++; } return; }
        while (pos < s.Length && s[pos] != ',' && s[pos] != '}' && s[pos] != ']') pos++;
    }

    // DiagnoseUniTask REMOVED — the RunClassConstructor call produced a harmless NullRef
    // because the Cpp2IL UniTask.dll .cctor depends on types not yet initialized at Boot.
    // UniTask itself works (Cpp2IL 1.4MB, 96% IL, WebGL compatible).

    static void Log(string m) { Debug.Log(m); if (_log.Count >= MAX) _log.RemoveAt(0); _log.Add($"[{Time.time:F1}] {m}"); }
    static void Err(string m) { Debug.LogError(m); if (_errors.Count >= MAX) _errors.RemoveAt(0); _errors.Add($"[{Time.time:F1}] {m}"); }
    static void OnLog(string msg, string stack, LogType t)
    {
        if (t == LogType.Error || t == LogType.Exception)
        {
            var e = $"[{t}] {msg}";
            if (_errors.Count >= MAX) _errors.RemoveAt(0);
            _errors.Add($"[{Time.time:F1}] {e}");
        }
    }

    static Type FindType(string n)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            try { var t = a.GetType(n, false); if (t != null) return t; } catch { }
        return null;
    }
    static Type ScanForType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            try { foreach (var t in asm.GetTypes()) if (t.Name == name) return t; }
            catch (ReflectionTypeLoadException e) { foreach (var t in e.Types) if (t != null && t.Name == name) return t; }
            catch { }
        return null;
    }
    static T GetStyleSheetField<T>(StyleSheet ss, string name)
    {
        if (ss == null) return default;
        var f = typeof(StyleSheet).GetField(name, ALL);
        if (f == null) return default;
        var val = f.GetValue(ss);
        return val is T typed ? typed : default;
    }
    static object GetF(object o, Type t, string n) => t.GetField(n, ALL)?.GetValue(o);
    static void SetF(object o, Type t, string n, object v) { var f = t.GetField(n, ALL); if (f != null) f.SetValue(o, v); }
    static int? DictCount(object d) { if (d == null) return null; return d.GetType().GetProperty("Count")?.GetValue(d) as int?; }

    void SetupEventSystem()
    {
        var esObj = GameObject.Find("EventSystem");
        if (esObj == null) esObj = new GameObject("EventSystem");

        var esType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI")
                  ?? Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.CoreModule");
        if (esType != null && esObj.GetComponent(esType) == null)
        {
            esObj.AddComponent(esType);
            Log("  EventSystem: added EventSystem component");
        }

        var inputSystemType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        var standaloneType = Type.GetType("UnityEngine.EventSystems.StandaloneInputModule, UnityEngine.UI");

        if (inputSystemType != null && esObj.GetComponent(inputSystemType) == null)
        {
            esObj.AddComponent(inputSystemType);
            Log("  EventSystem: added InputSystemUIInputModule");
        }
        else if (standaloneType != null && esObj.GetComponent(standaloneType) == null)
        {
            esObj.AddComponent(standaloneType);
            Log("  EventSystem: added StandaloneInputModule");
        }
    }

    void Start()
    {
        Log("PROBE: Start");
        DumpScene();
        SetupEventSystem();
        StartCoroutine(OriginalBootFlow());
    }

    // Boot sequence — WebGL workaround for decompiled Compositor.Compose (dump.cs L405168).
    // ORIGINAL FLOW (from decompiled GameCompositor.bkqk + Compositor.Compose state machine):
    //   bkqa: RegisterServices — creates glo (service bus), ecr (coordinator), fyh (debug)
    //   bkpy: PreInitServices — iterates Dictionary<Type, gao>, calls each
    //   bkpz: InitializeServices — finds [ServiceInit] methods via reflection, awaits each
    //   bkqe: LoadDataDependencies — Addressables.InitializeAsync, loads catalogs
    //   bkqk: Load DofusUISettings, fpm creates auth screens:
    //     fpm opens ConnectionBackground (AuthUis.8 → fyr.OnEnable)
    //     fpm opens Login (AuthUis.0 → fys.OnEnable)
    //
    // WHY WORKAROUND: Compositor.Compose is async UniTask with state machines
    //   (<Compose>d__24.MoveNext). UniTask IL is corrupted in WebGL build (V1: IL recovery
    //   failed for async methods). The phased boot replicates the RESULT (same screens loaded)
    //   without the async flow. Each phase does what the original does at that step.
    //
    // IF CLIENT UPDATES: check Compositor.Compose in dump.cs for new services or screens.
    //   The phases map to: Phase 3 = bkqe+fyr.OnEnable, Phase 4 = USS loading, Phase 5 = fys.OnEnable
    IEnumerator OriginalBootFlow()
    {
        FindCompositor();
        if (_compositor == null) { yield return new WaitForSeconds(0.5f); FindCompositor(); }
        if (_compositor == null) { Err("FATAL: GameCompositor not found"); yield break; }

        Log($"=== Phase 0: RegisterServices (BootLogic: {_bootPhaseCount} phases, {_bootServiceCount} services) ===");
        if (!RegisterServices()) yield break;
        yield return null;

        Log("=== Phase 1: LoadDataDependencies (bundles + FMOD) ===");
        yield return StartCoroutine(LoadBundles());

        // FMOD: System.create at boot (decompiled elv.<InitializeAudioManager>d__68, dump.cs L308193)
        // Bank loading deferred to first user interaction — FMOD WebGL returns ERR_FORMAT (70)
        // when loading banks with suspended AudioContext. Banks load on first click (Play button).
        // SOURCE: BootLogic.json Phase 2 → AudioManager.Initialize → FMOD.Studio.System.create
        InitFMODAudio();
        Jondo.Reconstructed.DataCenter.DataCenterModule.Initialize();
        Jondo.Reconstructed.UI.Bindings.UIBindingRegistry.Initialize();
        Jondo.Reconstructed.UI.Screens.UIScreenRegistry.Initialize();
        Jondo.Reconstructed.Protocol.ProtocolRegistry.Initialize();

        Log($"=== Phase 2: InitializeServices ({_bootServiceCount} candidates from BootLogic; central services only) ===");
        CallServiceInitMethods();
        yield return null;

        try { _baseType.GetField("isRunning", ALL)?.SetValue(_compositor, true); Log("isRunning = true"); }
        catch (Exception e) { Err($"isRunning: {e.Message}"); }

        Log("=== Phase 3: ConnectionBackground + loading spinner ===");
        yield return StartCoroutine(SetupConnectionBackground());

        Log("=== Phase 4: USS, fonts, theme ===");
        yield return StartCoroutine(LoadStylesAndFonts());

        Log("=== Phase 5: Login overlay ===");
        yield return StartCoroutine(ShowLoginOverlay());

        HideLoadingSpinner();

        Inspect("final");
        Log("=== BOOTSTRAP COMPLETE === (H=overlay, Esc=settings)");
        yield return new WaitForSeconds(3f);
        _showOverlay = false;
    }

    // ──────────────────── Step 2: Service Registration ────────────────────

    bool RegisterServices()
    {
        foreach (var n in new[] { "dxnw", "dxnx", "dxny", "dxnz", "dxoa" })
            TryInitField(_baseType, n);

        object glo = ConstructService("glo", "Core");
        object fyh = ConstructService("fyh", "Core");
        object ecr = ConstructEcr(glo, fyh);

        if (glo != null) { SetF(_compositor, _gameType, "dxob", glo); Log($"  dxob = glo"); }
        if (ecr != null) { SetF(_compositor, _gameType, "dxoc", ecr); Log($"  dxoc = ecr"); }
        if (fyh != null) { SetF(_compositor, _gameType, "dxod", fyh); Log($"  dxod = fyh"); }

        var dxnx = GetF(_compositor, _baseType, "dxnx");
        if (dxnx == null || _centralRegistrations == null || _centralRegistrations.Length == 0)
        {
            Err("  Missing extracted central registrations or compositor dictionary");
            return false;
        }
        try
        {
            foreach (var binding in _centralRegistrations)
            {
                var registryType = ValidateCentralRegistration(binding, _gameType);
                var service = GetF(_compositor, _gameType, binding.field);
                RegisterInDict(dxnx, registryType, service, binding.field + " -> " + binding.registryType);
            }
        }
        catch (Exception exception)
        {
            Err($"  Central registration failed: {exception.InnerException?.Message ?? exception.Message}");
            return false;
        }

        int reconstructedRegistered = 0;
        int reconstructedSkipped = 0;
        if (_reconstructedServices != null && _reconstructedServices.Length > 0)
        {
            var dxnz = GetF(_compositor, _baseType, "dxnz");
            var dxny = GetF(_compositor, _baseType, "dxny");
            var dxoa = GetF(_compositor, _baseType, "dxoa");
            var gaoType = FindType("gao") ?? ScanForType("gao");
            var gaqType = FindType("gaq") ?? ScanForType("gaq");
            var dykType = FindType("dyk") ?? ScanForType("dyk");

            var instantiatedServices = new Dictionary<string, object>();

            foreach (var rec in _reconstructedServices)
            {
                if (rec == null || rec.sourceType == "field") continue;
                if (string.IsNullOrEmpty(rec.concreteClass) || rec.genericTypes == null || rec.genericTypes.Length == 0)
                {
                    reconstructedSkipped++;
                    continue;
                }

                try
                {
                    object instance = null;
                    if (!instantiatedServices.TryGetValue(rec.concreteClass, out instance))
                    {
                        instance = ConstructService(rec.concreteClass, "Core");
                        if (instance != null)
                            instantiatedServices[rec.concreteClass] = instance;
                    }

                    if (instance == null)
                    {
                        reconstructedSkipped++;
                        continue;
                    }

                    foreach (var ifaceName in rec.genericTypes)
                    {
                        if (string.IsNullOrEmpty(ifaceName)) continue;
                        var ifaceType = FindType(ifaceName) ?? ScanForType(ifaceName);
                        if (ifaceType == null || !ifaceType.IsInstanceOfType(instance))
                        {
                            reconstructedSkipped++;
                            continue;
                        }

                        var contains = dxnx.GetType().GetMethod("ContainsKey");
                        if (contains != null && (bool)contains.Invoke(dxnx, new object[] { ifaceType }))
                            continue;

                        RegisterInDict(dxnx, ifaceType, instance, $"{rec.concreteClass} -> {ifaceName}");
                        reconstructedRegistered++;

                        if (gaoType != null && gaoType.IsInstanceOfType(instance) && dxnz != null)
                        {
                            var containsPre = dxnz.GetType().GetMethod("ContainsKey");
                            if (containsPre != null && !(bool)containsPre.Invoke(dxnz, new object[] { ifaceType }))
                                dxnz.GetType().GetMethod("set_Item")?.Invoke(dxnz, new[] { ifaceType, instance });
                        }
                        if (gaqType != null && gaqType.IsInstanceOfType(instance) && dxny != null)
                        {
                            var containsReset = dxny.GetType().GetMethod("ContainsKey");
                            if (containsReset != null && !(bool)containsReset.Invoke(dxny, new object[] { ifaceType }))
                                dxny.GetType().GetMethod("set_Item")?.Invoke(dxny, new[] { ifaceType, instance });
                        }
                        if (dykType != null && dykType.IsInstanceOfType(instance) && dxoa != null)
                        {
                            var containsTick = dxoa.GetType().GetMethod("Contains");
                            if (containsTick != null && !(bool)containsTick.Invoke(dxoa, new object[] { instance }))
                                dxoa.GetType().GetMethod("Add")?.Invoke(dxoa, new[] { instance });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"  Notice: service {rec.concreteClass} registration deferred: {ex.Message}");
                    reconstructedSkipped++;
                }
            }
            Log($"  Reconstructed services: {reconstructedRegistered} registered, {reconstructedSkipped} deferred");
        }

        Log($"  dxnx count = {DictCount(GetF(_compositor, _baseType, "dxnx"))}");
        Inspect("after-register");
        return true;
    }

    static Type ValidateCentralRegistration(BootCentralRegistration binding, Type compositorType)
    {
        if (binding == null || string.IsNullOrEmpty(binding.field) || string.IsNullOrEmpty(binding.registryType))
            throw new InvalidOperationException("Incomplete extracted central registration");
        var field = compositorType.GetField(binding.field, ALL);
        var registryType = FindType(binding.registryType);
        if (field == null || field.FieldType.Name != binding.fieldType || registryType == null ||
            !registryType.IsAssignableFrom(field.FieldType))
            throw new InvalidOperationException($"Central registration metadata mismatch: {binding.field} -> {binding.registryType}");
        return registryType;
    }

    static void RegisterInDict(object dict, Type key, object service, string label)
    {
        if (dict == null || key == null || service == null || !key.IsInstanceOfType(service))
            throw new InvalidOperationException($"Incompatible registration: {label}");
        var type = dict.GetType();
        var arguments = type.GetGenericArguments();
        if (arguments.Length != 2 || !arguments[1].IsInstanceOfType(service))
            throw new InvalidOperationException($"Service is incompatible with dictionary value type: {label}");
        var contains = type.GetMethod("ContainsKey");
        var add = type.GetMethod("Add");
        if (contains == null || add == null)
            throw new InvalidOperationException("Unsupported compositor dictionary contract");
        if ((bool)contains.Invoke(dict, new object[] { key }))
            throw new InvalidOperationException($"Duplicate service registration: {label}");
        add.Invoke(dict, new[] { key, service });
        Log($"  registered {label}");
    }

    object ConstructService(string typeName, string assembly)
    {
        var type = FindType(typeName) ?? ScanForType(typeName);
        if (type == null) { Log($"  {typeName}: optional service descriptor deferred"); return null; }
        try
        {
            var obj = Activator.CreateInstance(type);
            Log($"  new {typeName}() OK");
            if (typeName == "glo")
            {
                foreach (var fn in new[] { "dxni", "dxnj", "dxnk", "dxnl" })
                {
                    var f = type.GetField(fn, ALL);
                    if (f != null) try { f.SetValue(obj, Activator.CreateInstance(f.FieldType)); } catch { }
                }
                var qf = type.GetField("dxnm", ALL);
                if (qf != null) try { qf.SetValue(obj, Activator.CreateInstance(qf.FieldType, new object[] { 15 })); } catch { try { qf.SetValue(obj, Activator.CreateInstance(qf.FieldType)); } catch { } }
            }
            return obj;
        }
        catch (Exception e) { Err($"  {typeName}: {e.InnerException?.Message ?? e.Message}"); return null; }
    }

    object ConstructEcr(object gloObj, object fyhObj)
    {
        var ecrType = FindType("ecr") ?? ScanForType("ecr");
        if (ecrType == null) { Err("  ecr: type not found"); return null; }
        foreach (var ctor in ecrType.GetConstructors(ALL))
        {
            if (ctor.GetParameters().Length == 2)
            {
                try { var obj = ctor.Invoke(new[] { gloObj, fyhObj }); Log("  new ecr(glo, fyh) OK"); return obj; }
                catch { }
            }
        }
        try { var obj = Activator.CreateInstance(ecrType); Log("  new ecr() OK"); return obj; }
        catch { return null; }
    }

    void CallServiceInitMethods()
    {
        var serviceInitAttr = FindType("Core.Engine.Compositor.ServiceInitAttribute");
        if (serviceInitAttr == null) { Log("  ServiceInitAttribute not found"); return; }

        foreach (var (service, label) in new[] {
            (GetF(_compositor, _gameType, "dxoc"), "ecr"),
            (GetF(_compositor, _gameType, "dxod"), "fyh"),
            (GetF(_compositor, _gameType, "dxob"), "glo") })
        {
            if (service == null) continue;
            foreach (var m in service.GetType().GetMethods(ALL))
            {
                if (m.GetCustomAttributes(serviceInitAttr, false).Length > 0)
                {
                    try { m.Invoke(service, null); Log($"  {label}.{m.Name}() [ServiceInit] OK"); }
                    catch (Exception e) { Err($"  {label}.{m.Name}() THREW: {e.InnerException?.Message ?? e.Message}"); }
                }
            }
        }
    }

    // ──────────────────── Step 5: Load Asset Bundles ────────────────────

    Dictionary<string, AssetBundle> _loadedBundles = new();

    IEnumerator LoadBundles()
    {
        string basePath = Application.streamingAssetsPath + "/aa/WebGL/";

        // Load the manifest bundle to discover all bundles (no hardcoded list)
        var manifestReq = UnityEngine.Networking.UnityWebRequestAssetBundle.GetAssetBundle(basePath + "WebGL");
        yield return manifestReq.SendWebRequest();

        string[] bundleNames = null;
        if (manifestReq.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            var manifestBundle = UnityEngine.Networking.DownloadHandlerAssetBundle.GetContent(manifestReq);
            if (manifestBundle != null)
            {
                var manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                if (manifest != null)
                {
                    bundleNames = manifest.GetAllAssetBundles();
                    Log($"  Manifest: {bundleNames.Length} bundles discovered");
                }
                manifestBundle.Unload(true);
            }
        }

        if (bundleNames == null)
        {
            Err("  Manifest load failed — no bundles to load");
            yield break;
        }

        foreach (var name in bundleNames)
        {
            var req = UnityEngine.Networking.UnityWebRequestAssetBundle.GetAssetBundle(basePath + name);
            yield return req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) continue;
            var bundle = UnityEngine.Networking.DownloadHandlerAssetBundle.GetContent(req);
            if (bundle != null)
            {
                _loadedBundles[name] = bundle;
                Log($"  {name}: {bundle.GetAllAssetNames().Length} assets");
            }
        }
        Log($"  Total bundles: {_loadedBundles.Count}");
        _loadedBundlesStatic = _loadedBundles;
    }

    // ──────────────────── Phased Boot: 3→4→5 ────────────────────
    // Real flow (GameCompositor.Compose, dump.cs L405339):
    //   ConnectionBackground (AuthUis.8) + loading spinner → asset loading → Login (AuthUis.0) overlay
    // Split into SetupConnectionBackground → LoadStylesAndFonts → ShowLoginOverlay
    // so the loading spinner is visible while USS/fonts load.

    VisualElement _uiRoot;
    VisualTreeAsset _loginUxml;

    // Phase 3: PanelSettings + TSS + connectionBackground.uxml + Krosmoz bg + loading spinner
    IEnumerator SetupConnectionBackground()
    {
        var rootUI = GameObject.Find("RootUI");
        if (rootUI == null) { Err("RootUI not found"); yield break; }
        rootUI.SetActive(true);

        var uidoc = rootUI.GetComponent<UIDocument>();
        if (uidoc == null) { Err("UIDocument not found on RootUI"); yield break; }

        PanelSettings panelSettings = null;
        foreach (var kv in _loadedBundles)
        {
            panelSettings = kv.Value.LoadAsset<PanelSettings>("assets/ui/panelsettings.asset");
            if (panelSettings != null) { Log($"  PanelSettings from '{kv.Key}'"); break; }
        }
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            Log("  PanelSettings: created minimal");
        }

        ThemeStyleSheet gameTss = null;
        if (_loadedBundles.TryGetValue("tss_assets_all", out var tssBundleEarly))
        {
            foreach (var tss in tssBundleEarly.LoadAllAssets<ThemeStyleSheet>())
            {
                if (tss.name == "LargeFont_Standard.tss" || tss.name == "LargeFont_Standard")
                { gameTss = tss; break; }
                if (gameTss == null && tss.name.Contains("LargeFont") && tss.name.Contains("Standard"))
                    gameTss = tss;
            }
            if (gameTss == null)
            {
                var all = tssBundleEarly.LoadAllAssets<ThemeStyleSheet>();
                if (all.Length > 0) gameTss = all[0];
            }
        }
        if (gameTss != null)
        {
            panelSettings.themeStyleSheet = gameTss;
            Log($"  TSS applied: {gameTss.name}");
        }
        uidoc.panelSettings = panelSettings;

        VisualTreeAsset bgUxml = LoadUxml("connectionbackground");
        _loginUxml = LoadUxml("login");
        Log($"  connectionBackground.uxml: {(bgUxml != null ? "OK" : "NOT FOUND")}");
        Log($"  login.uxml: {(_loginUxml != null ? "OK" : "NOT FOUND")}");

        Texture2D bgTexture = null;
        if (_loadedBundles.TryGetValue("titlescreen_assets_all", out var tsBundle))
        {
            bgTexture = tsBundle.LoadAsset<Texture2D>("assets/content/prefabs/titlescreen/krosmoz/textures/background.jpg");
            if (bgTexture == null)
                foreach (var t in tsBundle.LoadAllAssets<Texture2D>())
                    if (t.name.Contains("background")) { bgTexture = t; break; }
        }

        if (bgUxml != null) uidoc.visualTreeAsset = bgUxml;
        else if (_loginUxml != null) uidoc.visualTreeAsset = _loginUxml;

        yield return null;
        yield return null;

        _uiRoot = uidoc.rootVisualElement;
        if (_uiRoot == null) yield break;

        _uiRoot.style.width = Length.Percent(100);
        _uiRoot.style.height = Length.Percent(100);

        if (bgTexture != null)
        {
            _uiRoot.style.backgroundImage = new StyleBackground(bgTexture);
            _uiRoot.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _uiRoot.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _uiRoot.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            Log($"  Krosmoz background applied ({bgTexture.width}x{bgTexture.height})");
        }
        else
        {
            _uiRoot.style.backgroundColor = new Color(0.06f, 0.06f, 0.12f);
        }

        // Loading spinner from ConnectionBackgroundBinding._iconLoading (dump.cs L25814)
        // The UXML's inlineSheet controls initial visibility — don't override it.
        // fys.mx(1) shows the spinner during connecting state, fys.mx(0) hides it.
        _loadingIcon = _uiRoot.Q("iconLoading");
        if (_loadingIcon != null)
            Log($"  _iconLoading: found [{_loadingIcon.resolvedStyle.width:F0}x{_loadingIcon.resolvedStyle.height:F0}]");

        _status = $"bg:{(bgTexture != null ? "Krosmoz" : "none")} tss:{(gameTss != null ? gameTss.name : "none")}";
    }

    // Phase 4: USS + fonts + theme (loading spinner visible during this)
    IEnumerator LoadStylesAndFonts()
    {
        var root = _uiRoot;
        if (root == null) yield break;

        // Font
        yield return null;
        ApplyFont(root);

        // USS from all bundles
        int ussTotal = 0, ussLoaded = 0;
        foreach (var kv in _loadedBundles)
        {
            foreach (var uss in kv.Value.LoadAllAssets<StyleSheet>())
            {
                ussTotal++;
                try
                {
                    var rulesArr = GetStyleSheetField<Array>(uss, "m_Rules");
                    if (rulesArr == null || rulesArr.Length == 0) continue;
                    root.styleSheets.Add(uss);
                    ussLoaded++;
                }
                catch { }
            }
        }
        Log($"  USS: {ussLoaded}/{ussTotal} loaded");

        var overrides = Resources.Load<StyleSheet>("WebGLOverrides");
        if (overrides != null)
        {
            root.styleSheets.Add(overrides);
            Log($"  WebGLOverrides.uss loaded");
        }

        // Theme colors
        yield return null;
        int themeApplied = ApplyThemeColors(root);
        Log($"  Theme colors applied: {themeApplied}");
    }

    // Phase 5: Clone login.uxml, populate, wire handlers
    IEnumerator ShowLoginOverlay()
    {
        var root = _uiRoot;
        if (root == null) yield break;

        VisualElement loginContainer = null;
        if (_loginUxml != null)
        {
            loginContainer = new VisualElement { name = "screen_Login" };
            loginContainer.style.width = Length.Percent(100);
            loginContainer.style.height = Length.Percent(100);
            loginContainer.style.position = Position.Absolute;
            loginContainer.style.left = 0;
            loginContainer.style.top = 0;
            loginContainer.style.right = 0;
            loginContainer.style.bottom = 0;
            _loginUxml.CloneTree(loginContainer);
            root.Add(loginContainer);
            Log($"  login.uxml cloned into screen_Login → {loginContainer.childCount} children");
        }

        if (_ussOverrides != null)
        {
            int injected = 0;
            ApplyUSSOverrides(root, ref injected);
            if (injected > 0) Log($"  USS overrides injected: {injected}");
        }

        yield return null;

        int themeApplied = 0;
        var colorMap = LoadThemeColorMap();
        ApplyThemeRecursive(root, colorMap, ref themeApplied);

        var targetLogin = loginContainer ?? root;
        ApplyScreenBehavior("Login", targetLogin);
        DispatchGen_Ctor("Login", targetLogin);
        DispatchGen_Localize("Login", targetLogin);
        DispatchGen_OnEnable("Login", targetLogin);
        PopulateLoginScreen(targetLogin);
        SetLoginState(targetLogin, 0);

        // Re-apply font AFTER login clone (USS sets -unity-font-definition: none on some elements)
        yield return null;
        var fontAsset = Resources.Load<FontAsset>("DefaultFont");
        if (fontAsset != null)
        {
            RestoreFontAtlas(fontAsset);
            int fontFixed = ApplyFontRecursive(root, fontAsset);
            Log($"  Font re-applied to {fontFixed} elements");
        }

        // Diagnostic
        yield return null;
        int bgImgCount = 0;
        void ScanBg(VisualElement ve) {
            if (ve.resolvedStyle.backgroundImage.texture != null || ve.resolvedStyle.backgroundImage.sprite != null) bgImgCount++;
            for (int i = 0; i < ve.childCount; i++) ScanBg(ve[i]);
        }
        ScanBg(root);

        int elTotal = 0, elSized = 0;
        void CountEls(VisualElement ve) { elTotal++; if (ve.resolvedStyle.width > 0 && ve.resolvedStyle.height > 0) elSized++; for (int i = 0; i < ve.childCount; i++) CountEls(ve[i]); }
        CountEls(root);

        Log($"  LOGIN READY: {elTotal} elements, {elSized} with size, {bgImgCount} bg-images");
    }

    void ApplyFont(VisualElement root)
    {
        try
        {
            var resFontAsset = Resources.Load<FontAsset>("DefaultFont");
            if (resFontAsset == null) return;
            RestoreFontAtlas(resFontAsset);
            root.style.unityFontDefinition = new StyleFontDefinition(resFontAsset);
            Log($"  Font: DefaultFont applied (atlas={resFontAsset.atlasTexture?.width}x{resFontAsset.atlasTexture?.height})");
        }
        catch (Exception e) { Log($"  Font apply FAILED: {e.Message}"); }
    }

    void RestoreFontAtlas(FontAsset fa)
    {
        if (fa.atlasTexture != null && fa.atlasTexture.width > 1) return;
        var standaloneAtlas = Resources.Load<Texture2D>("DefaultFontAtlas");
        if (standaloneAtlas != null && standaloneAtlas.width > 1)
        {
            fa.atlasTextures = new[] { standaloneAtlas };
            var mat = Resources.Load<Material>("DefaultFontMaterial");
            if (mat != null) { mat.mainTexture = standaloneAtlas; fa.material = mat; }
            Log($"  Font atlas RESTORED ({standaloneAtlas.width}x{standaloneAtlas.height})");
        }
    }

    void DumpClassesRecursive(VisualElement el, ref int count)
    {
        var classes = el.GetClasses().ToList();
        if (classes.Count > 0)
        {
            var name = string.IsNullOrEmpty(el.name) ? el.GetType().Name : $"#{el.name}";
            Log($"    {name}: [{string.Join(", ", classes)}]");
            count++;
        }
        for (int i = 0; i < el.childCount; i++)
            DumpClassesRecursive(el[i], ref count);
    }

    // ApplyLoginVisuals_DISABLED and AddTypeClasses REMOVED — were hardcoded
    // with invented CSS classes. Visuals now come from MergedUSS + factory data.

    // ──────────────────── Populate Login Screen ────────────────────
    // Sets text (I18n) and click handlers only. Visual styling comes from USS.

    // Populate login screen elements (from LoginBinding, dump.cs L195777)
    // Sets text via I18n and wires click handlers. Visual styling from USS.
    // PopulateLoginScreen: click handler wiring only.
    // SOURCE: decompiled fys.OnEnable (473L) — wires btn_play click to connection flow
    // WHY STATIC: click action triggers HAAPI auth (WebGL-specific HTTP+WS bridge).
    //   The decompiled calls eyl.xnv(user,pass) → ewz.vql(host,port) but these services
    //   don't exist in WebGL. The probe implements the equivalent via fetch+WebSocket.
    // IF CLIENT UPDATES: check fys.OnEnable for changed event wiring order.
    // Element name from: LoginBinding._btn_play (dump.cs L195777)
    void PopulateLoginScreen(VisualElement root)
    {
        _loginBinding = new GeneratedLoginBinding();
        _loginBinding.Bind(root);
        var btnPlay = _loginBinding.btn_play ?? root.Q("btn_play"); // LoginBinding._btn_play
        if (btnPlay != null)
        {
            btnPlay.SetEnabled(true);
            btnPlay.RemoveFromClassList("disabled");
            btnPlay.pickingMode = PickingMode.Position;

            btnPlay.Query().ForEach(child =>
            {
                if (child != btnPlay)
                {
                    child.pickingMode = PickingMode.Ignore;
                    child.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();
                        OnPlayClicked(root);
                    });
                    child.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        if (evt.button == 0) { evt.StopPropagation(); OnPlayClicked(root); }
                    });
                }
            });

            if (btnPlay is Button playBtn)
                playBtn.clicked += () => OnPlayClicked(root);

            btnPlay.RegisterCallback<ClickEvent>(evt => OnPlayClicked(root));
            btnPlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) OnPlayClicked(root);
            });
            btnPlay.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 0) OnPlayClicked(root);
            });
            btnPlay.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button == 0) OnPlayClicked(root);
            });

            var playLabel = btnPlay.Q("playButton_label");
            if (playLabel is Label pl) pl.text = I18n("ui.common.play", "Play");
        }

        Log($"  PopulateLogin: click handler wired, enabled={btnPlay.enabledSelf}, worldBound={btnPlay.worldBound}");
    }

    // ──────────────────── Settings Menu (PreGameMainMenu) ────────────────────
    // From fyv handler (dump.cs L386396) + PreGameMainMenuBinding (dump.cs L33965).
    // UXML: Assets/UI/Auth/preGameMainMenu.uxml
    // Binding elements: _btn_options, _btn_quit, _btn_close, _lbl_buildNumber

    // PreGameMainMenu is accessed via Escape key (from fzy<T>.closeWithEscape, dump.cs L388926).
    // The original game opens it via fpm (auth screen manager, dump.cs L371728),
    // not via a programmatic button — so no AddSettingsButton.

    void TogglePreGameMainMenu()
    {
        if (_menuOverlay != null && _menuOverlay.parent != null)
            HidePreGameMainMenu();
        else
            ShowPreGameMainMenu();
    }

    // PreGameMainMenu shown as a proper screen (AuthUis=4, dump.cs L339089)
    // via ShowScreen, like ServerSelection and CharacterSelection.
    void ShowPreGameMainMenu()
    {
        if (_uiRoot == null) return;
        ShowScreen("preGameMainMenu", _uiRoot);
        _menuOverlay = _uiRoot; // track for toggle

        // Wire binding elements (PreGameMainMenuBinding, dump.cs L33965)
        var btnClose = _uiRoot.Q("btn_close");
        if (btnClose is Button closeBtn)
            closeBtn.clicked += HidePreGameMainMenu;

        var lblBuild = _uiRoot.Q("lbl_buildNumber");
        if (lblBuild is Label buildLbl)
            buildLbl.text = "JondoEmu WebGL";

        Log("  PreGameMainMenu shown via ShowScreen");
    }

    void HidePreGameMainMenu()
    {
        // Return to login screen by re-showing login elements
        if (_uiRoot == null) return;
        for (int i = 0; i < _uiRoot.childCount; i++)
        {
            var child = _uiRoot[i];
            bool isLogin = child.name == "connectionBackground" || child.name == "img" || child.name == "login";
            child.style.display = isLogin ? DisplayStyle.Flex : DisplayStyle.None;
        }
        _currentScreen = "Login";
        _menuOverlay = null;
        Log("  PreGameMainMenu hidden → Login");
    }

    void SetColorRecursive(VisualElement el, Color color)
    {
        if (el is Label || el is Button)
            el.style.color = color;
        for (int i = 0; i < el.childCount; i++)
            SetColorRecursive(el[i], color);
    }

    static void ApplyUSSOverrides(VisualElement el, ref int count)
    {
        if (_ussOverrides == null) return;

        // USS overrides extracted from compiled CSS selectors in the Win64 bundles.
        // These are UXML-specific class+child pairs that m_SerializedData would have set.
        // Apply only when the element already has the class (added by factory or UXML traits).
        foreach (var kv in _ussOverrides)
        {
            string parentClass = kv.Key;
            string[] childNames = kv.Value;
            if (childNames == null || !el.ClassListContains(parentClass)) continue;
            foreach (var cn in childNames)
            {
                if (el.Q(cn) == null)
                {
                    var child = new VisualElement();
                    child.name = cn;
                    child.style.width = Length.Percent(100);
                    child.style.height = Length.Percent(100);
                    el.Insert(0, child);
                    count++;
                }
            }
        }
        for (int i = 0; i < el.childCount; i++)
            ApplyUSSOverrides(el[i], ref count);
    }

    static int ApplyFontRecursive(VisualElement el, FontAsset font)
    {
        int count = 0;
        el.style.unityFontDefinition = new StyleFontDefinition(font);
        count++;
        for (int i = 0; i < el.childCount; i++)
            count += ApplyFontRecursive(el[i], font);
        return count;
    }

    // ──────────────────── Theme Colors ────────────────────
    // Game's FigmaThemeScriptable generates backgroundColor_* and textColor_*
    // classes at runtime. These aren't in any USS file. We apply them inline.

    int ApplyThemeColors(VisualElement root)
    {
        var colorMap = new Dictionary<string, Color>();

        // Try to find FigmaThemeScriptable or FigmaThemeManagerScriptable in bundles
        var themeType = FindType("Core.UILogic.Figma.ScriptableObject.Script.FigmaThemeScriptable");
        var mgrType = FindType("Core.UILogic.Figma.ScriptableObject.Script.FigmaThemeManagerScriptable");

        foreach (var kv in _loadedBundles)
        {
            if (colorMap.Count > 0) break;
            try
            {
                // Try manager first (has all themes)
                if (mgrType != null)
                {
                    foreach (var so in kv.Value.LoadAllAssets(mgrType))
                    {
                        if (so == null) continue;
                        var dataList = so.GetType().GetField("m_figmaThemeData", ALL)?.GetValue(so) as System.Collections.IList;
                        if (dataList == null) continue;
                        foreach (var themeData in dataList)
                        {
                            var scriptable = themeData.GetType().GetField("themeScriptable", ALL)?.GetValue(themeData);
                            if (scriptable == null) continue;
                            ExtractThemeColors(scriptable, colorMap);
                            if (colorMap.Count > 0)
                            {
                                Log($"  Theme: FigmaThemeManagerScriptable in {kv.Key}: {colorMap.Count} colors");
                                break;
                            }
                        }
                        if (colorMap.Count > 0) break;
                    }
                }
                // Fallback: direct FigmaThemeScriptable
                if (colorMap.Count == 0 && themeType != null)
                {
                    foreach (var so in kv.Value.LoadAllAssets(themeType))
                    {
                        if (so == null) continue;
                        ExtractThemeColors(so, colorMap);
                        if (colorMap.Count > 0)
                        {
                            Log($"  Theme: FigmaThemeScriptable in {kv.Key}: {colorMap.Count} colors");
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        // Fallback: load from pre-extracted ThemeColors.json
        if (colorMap.Count == 0)
        {
            var themeJson = Resources.Load<UnityEngine.TextAsset>("ThemeColors");
            if (themeJson != null)
            {
                string[] lines = themeJson.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim().TrimEnd(new[] { ',' });
                    if (!trimmed.StartsWith("\"")) continue;
                    int colon = trimmed.IndexOf(':');
                    if (colon < 0) continue;
                    string cName = trimmed.Substring(0, colon).Trim().Trim(new[] { '"' });
                    if (cName.StartsWith("--")) cName = cName.Substring(2);
                    string cVal = trimmed.Substring(colon + 1).Trim().Trim(new[] { '"' });
                    if (TryParseColor(cVal, out var col))
                        colorMap[cName] = col;
                }
                if (colorMap.Count > 0)
                    Log($"  Theme: loaded {colorMap.Count} colors from ThemeColors.json");
            }
        }

        if (colorMap.Count == 0)
        {
            Log("  Theme: NO colors loaded — run Tools/ExtractThemeColors to generate ThemeColors.json");
        }

        int applied = 0;
        ApplyThemeRecursive(root, colorMap, ref applied);
        return applied;
    }

    static bool TryParseColor(string css, out Color color)
    {
        color = default;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (css.StartsWith("rgba(") && css.EndsWith(")"))
        {
            string[] nums = css.Substring(5, css.Length - 6).Split(new[] { ',' });
            if (nums.Length >= 4)
            {
                color = new Color(
                    float.Parse(nums[0].Trim(), inv) / 255f,
                    float.Parse(nums[1].Trim(), inv) / 255f,
                    float.Parse(nums[2].Trim(), inv) / 255f,
                    float.Parse(nums[3].Trim(), inv));
                return true;
            }
        }
        else if (css.StartsWith("rgb(") && css.EndsWith(")"))
        {
            string[] nums = css.Substring(4, css.Length - 5).Split(new[] { ',' });
            if (nums.Length >= 3)
            {
                color = new Color(
                    float.Parse(nums[0].Trim(), inv) / 255f,
                    float.Parse(nums[1].Trim(), inv) / 255f,
                    float.Parse(nums[2].Trim(), inv) / 255f, 1f);
                return true;
            }
        }
        return false;
    }

    static void ExtractThemeColors(object scriptable, Dictionary<string, Color> colorMap)
    {
        var listField = scriptable.GetType().GetField("m_figmaColorsData", ALL);
        if (listField == null) return;
        var list = listField.GetValue(scriptable) as System.Collections.IList;
        if (list == null) return;
        foreach (var item in list)
        {
            var vn = item.GetType().GetField("varName", ALL)?.GetValue(item) as string;
            var cv = item.GetType().GetField("color", ALL);
            if (vn == null || cv == null) continue;
            var v4 = (Vector4)cv.GetValue(item);
            colorMap[vn] = new Color(v4.x, v4.y, v4.z, v4.w);
        }
    }

    void ApplyThemeRecursive(VisualElement el, Dictionary<string, Color> map, ref int count)
    {
        foreach (var cls in el.GetClasses())
        {
            if (cls.StartsWith("backgroundColor_") && map.TryGetValue(cls.Substring(16), out var bg))
            {
                el.style.backgroundColor = bg;
                count++;
            }
            else if (cls.StartsWith("textColor_") && map.TryGetValue(cls.Substring(10), out var tc))
            {
                el.style.color = tc;
                count++;
            }
        }
        for (int i = 0; i < el.childCount; i++)
            ApplyThemeRecursive(el[i], map, ref count);
    }

    // ApplyDebugStyles REMOVED — was hardcoding colors/borders on elements.

    static void CheckFactorySurvival()
    {
        try
        {
            var uiAsm = typeof(VisualElement).Assembly;
            var registryType = uiAsm.GetType("UnityEngine.UIElements.VisualElementFactoryRegistry");
            if (registryType == null) return;
            var sField = registryType.GetField("s_Factories", STATIC);
            if (sField == null) { Log("  s_Factories field: NOT FOUND"); return; }
            var dict = sField.GetValue(null);
            Log($"  s_Factories now: {(dict == null ? "NULL" : $"count={DictCount(dict)}")}");

            // Check for specific keys
            if (dict != null)
            {
                var contains = dict.GetType().GetMethod("ContainsKey");
                foreach (var key in new[] {
                    "Core.UILogic.Components.Figma.DofusButtonCustom",
                    "Core.UILogic.Components.Figma.WindowFigma",
                    "Core.UILogic.Components.DofusLabel" })
                {
                    var found = (bool)contains.Invoke(dict, new object[] { key });
                    Log($"  s_Factories['{key.Split('.').Last()}']: {(found ? "YES" : "NO")}");
                }
            }
        }
        catch (Exception e) { Log($"  CheckFactorySurvival: {e.Message}"); }
    }

    static string _nullSDResult = "pending";

    void NullifySerializedData(VisualTreeAsset vta)
    {
        int cleared = 0;
        string foundIn = "";
        try
        {
            foreach (var f in typeof(VisualTreeAsset).GetFields(ALL))
            {
                var val = f.GetValue(vta);
                if (val == null) continue;

                // Single object with m_SerializedData
                int c = TryNullifySD(val);
                if (c > 0) foundIn += $"{f.Name}(1) ";
                cleared += c;

                // List of objects — check each element for m_SerializedData
                if (val is System.Collections.IList list)
                {
                    int lc = 0;
                    foreach (var item in list)
                        if (item != null) lc += TryNullifySD(item);
                    if (lc > 0) foundIn += $"{f.Name}({lc}) ";
                    cleared += lc;
                }
            }

            _nullSDResult = $"cleared={cleared} in=[{foundIn.Trim()}]";
            Log($"  NullSD: {_nullSDResult}");
        }
        catch (Exception e) { _nullSDResult = $"ERR:{e.Message}"; Log($"  NullSD ERR: {e.Message}"); }
    }

    static int TryNullifySD(object obj)
    {
        if (obj == null) return 0;
        for (var t = obj.GetType(); t != null; t = t.BaseType)
        {
            var sd = t.GetField("m_SerializedData", ALL);
            if (sd != null && sd.GetValue(obj) != null)
            {
                sd.SetValue(obj, null);
                return 1;
            }
        }
        return 0;
    }

    void DiagnoseVisualTreeAsset(VisualTreeAsset vta)
    {
        try
        {
            // Check stylesheets embedded in the VisualTreeAsset
            var ssField = typeof(VisualTreeAsset).GetField("m_Stylesheets", ALL)
                       ?? typeof(VisualTreeAsset).GetField("m_StyleSheets", ALL)
                       ?? typeof(VisualTreeAsset).GetField("stylesheets", ALL);
            if (ssField != null)
            {
                var ss = ssField.GetValue(vta);
                Log($"  VTA stylesheets field ({ssField.Name}): {ss}");
            }

            // Check the visual tree root for serialized data
            var vtField = typeof(VisualTreeAsset).GetField("m_VisualTree", ALL)
                       ?? typeof(VisualTreeAsset).GetField("m_Root", ALL);
            if (vtField != null)
            {
                var vtRoot = vtField.GetValue(vta);
                if (vtRoot != null)
                {
                    var sdField = vtRoot.GetType().GetField("m_SerializedData", ALL);
                    if (sdField != null)
                    {
                        var sd = sdField.GetValue(vtRoot);
                        Log($"  VTA root m_SerializedData: {(sd == null ? "NULL" : sd.GetType().FullName)}");
                        if (sd != null)
                        {
                            try
                            {
                                var createMethod = sd.GetType().GetMethod("CreateInstance");
                                var instance = createMethod?.Invoke(sd, null);
                                Log($"  VTA root CreateInstance → {(instance == null ? "NULL" : instance.GetType().Name)}");
                            }
                            catch (Exception e) { Log($"  VTA root CreateInstance THREW: {e.InnerException?.Message ?? e.Message}"); }
                        }
                    }
                    else Log("  VTA root: m_SerializedData field not found");

                    // Check fullTypeName
                    var nameField = vtRoot.GetType().GetField("m_FullTypeName", ALL);
                    if (nameField != null)
                        Log($"  VTA root type: {nameField.GetValue(vtRoot)}");
                }
                else Log("  VTA root: null");
            }
        }
        catch (Exception e) { Log($"  VTA diagnose: {e.Message}"); }
    }

    // FMOD Audio — via FMODProxy.cs (direct P/Invoke, no reflection)
    // SOURCE: BootLogic.json → AudioManager.<InitializationRoutine>d__25 (dump.cs L33428)
    // WHY FMODProxy: reflection + P/Invoke + FMOD.RESULT enum = IL2CPP WebGL marshalling failure.
    //   FMODProxy uses [DllImport("__Internal")] with int return type → compiled thunks work.
    // IF CLIENT UPDATES: check FMOD version in Ankama.AudioManagement.dll, update FMODProxy.HEADER_VERSION
    void InitFMODAudio()
    {
        int result = FMODProxy.Init();
        if (result != 0) { Err($"  FMOD: Init failed (result={result})"); return; }
        Log("  FMOD: Studio system initialized via FMODProxy");
        if (Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance == null)
            new Jondo.Reconstructed.Audio.ReconstructedAudioService();
        if (Jondo.Reconstructed.Fight.FightTurnService.Instance == null)
            new Jondo.Reconstructed.Fight.FightTurnService();
        StartCoroutine(LoadFMODBanks());
    }

    IEnumerator LoadFMODBanks()
    {
        if (_banksLoaded) yield break;
        _banksLoaded = true;

        // All paths from AudioData.json (extracted via UnityPy from AudioManagerLibrary)
        string path = _bankPath ?? "Content/Audio/Banks/Desktop/";
        if (path.StartsWith("Content/")) path = "assets/" + path.Substring("Content/".Length);
        string bankBase = Application.streamingAssetsPath + "/../" + path;

        // Core banks: Master + strings (required for path resolution)
        var bankList = new List<string> {
            _masterBank ?? "Master.bank",
            _stringsBank ?? "Master.strings.bank"
        };
        // UI bank (for click/hover/menu sounds) — from AudioData.json
        if (_uiBank != null) bankList.Add(_uiBank);
        // Additional banks needed for menu music — from AudioData.json
        if (_additionalBanks != null)
            foreach (var ab in _additionalBanks)
                if (!bankList.Contains(ab)) bankList.Add(ab);

        foreach (var bankName in bankList)
        {
            var req = UnityEngine.Networking.UnityWebRequest.Get(bankBase + bankName);
            yield return req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Log($"  FMOD: Bank fetch failed: {bankName} -- {req.error}");
                continue;
            }
            byte[] data = req.downloadHandler.data;
            int r = FMODProxy.LoadBankFromMemory(data, out _);
            if (r != 0) Err($"  FMOD: Bank load failed: {bankName} (result={r})");
        }
        Log($"  FMOD: {bankList.Count} banks loaded");

        // Play menu music (elv.PlayMenuWhenDoneInit, dump.cs L308412)
        // Event path from AudioData.json (extracted from AudioManagerLibrary)
        if (_menuMusicEvent != null)
            FMODProxy.PlayMusic(_menuMusicEvent);
    }

    // BuildLoginUI and StyleTextField REMOVED — were hardcoded programmatic UI.
    // Login screen now comes entirely from login.uxml + MergedUSS + factory sprites.

    void MountCharacterHeader(VisualElement root)
    {
        if (root == null) return;
        if (_characterHeaderRoot != null && _characterHeaderRoot.parent == root)
        {
            _characterHeaderRoot.style.display = DisplayStyle.Flex;
            _characterHeaderRoot.BringToFront();
            return;
        }

        var headerVta = LoadUxml("characterheader") ?? LoadUxml("Assets/UI/Auth/characterHeader.uxml");
        if (headerVta == null)
        {
            Log("  [CharacterHeader] UXML not found in bundles");
            return;
        }

        _characterHeaderRoot = headerVta.CloneTree();
        _characterHeaderRoot.name = "CharacterHeader_Overlay";
        _characterHeaderRoot.style.position = Position.Absolute;
        _characterHeaderRoot.style.top = 10;
        _characterHeaderRoot.style.left = 18;
        _characterHeaderRoot.style.right = 18;
        _characterHeaderRoot.style.height = 48;
        _characterHeaderRoot.style.flexDirection = FlexDirection.Row;
        _characterHeaderRoot.style.justifyContent = Justify.SpaceBetween;
        _characterHeaderRoot.style.alignItems = Align.Center;
        _characterHeaderRoot.pickingMode = PickingMode.Ignore;
        _characterHeaderRoot.style.backgroundColor = Color.clear;

        _charHeaderBinding = new GeneratedCharacterHeaderBinding();
        _charHeaderBinding.Bind(_characterHeaderRoot);

        // Clean up untextured background overlays inside characterHeader
        var leftContainer = _charHeaderBinding.left ?? _characterHeaderRoot.Q("left");
        if (leftContainer != null)
        {
            leftContainer.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.88f);
            leftContainer.style.borderTopLeftRadius = 16;
            leftContainer.style.borderTopRightRadius = 16;
            leftContainer.style.borderBottomLeftRadius = 16;
            leftContainer.style.borderBottomRightRadius = 16;
            leftContainer.style.borderLeftColor = new Color(0.22f, 0.28f, 0.35f, 0.6f);
            leftContainer.style.borderRightColor = new Color(0.22f, 0.28f, 0.35f, 0.6f);
            leftContainer.style.borderTopColor = new Color(0.22f, 0.28f, 0.35f, 0.6f);
            leftContainer.style.borderBottomColor = new Color(0.22f, 0.28f, 0.35f, 0.6f);
            leftContainer.style.borderLeftWidth = 1;
            leftContainer.style.borderRightWidth = 1;
            leftContainer.style.borderTopWidth = 1;
            leftContainer.style.borderBottomWidth = 1;
            leftContainer.style.paddingLeft = 8;
            leftContainer.style.paddingRight = 8;
            leftContainer.style.paddingTop = 4;
            leftContainer.style.paddingBottom = 4;
            leftContainer.style.flexDirection = FlexDirection.Row;
            leftContainer.style.alignItems = Align.Center;

            // Hide gradientOpacity or any unstyled element creating white artifacts
            for (int i = 0; i < leftContainer.childCount; i++)
            {
                var ch = leftContainer[i];
                if (ch.name != "ctr_player" && ch.name != "ctr_subscription" && ch.name != "ctr_admin")
                {
                    ch.style.display = DisplayStyle.None;
                }
            }
        }

        var fontAsset = Resources.Load<FontAsset>("DefaultFont");
        if (fontAsset != null) { RestoreFontAtlas(fontAsset); ApplyFontRecursive(_characterHeaderRoot, fontAsset); }

        // Populate user data matching selecaoServ.png
        string nick = !string.IsNullOrEmpty(_accountNickname) ? _accountNickname : "admin";
        SetElementText(_characterHeaderRoot, "lbl_name", nick);
        var nameLbl = _charHeaderBinding != null && _charHeaderBinding.lbl_name != null ? _charHeaderBinding.lbl_name : _characterHeaderRoot.Q<Label>("lbl_name");
        if (nameLbl != null)
        {
            nameLbl.text = nick;
            nameLbl.style.color = Color.white;
            nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLbl.style.fontSize = 13;
        }

        string tag = !string.IsNullOrEmpty(_accountTag) ? _accountTag : "#0001";
        SetElementText(_characterHeaderRoot, "lbl_tag", tag);
        var tagLbl = _charHeaderBinding != null && _charHeaderBinding.lbl_tag != null ? _charHeaderBinding.lbl_tag : _characterHeaderRoot.Q<Label>("lbl_tag");
        if (tagLbl != null)
        {
            tagLbl.text = tag;
            tagLbl.style.color = new Color(0.55f, 0.62f, 0.70f, 1f);
            tagLbl.style.fontSize = 12;
            tagLbl.style.marginLeft = 4;
        }

        string sub = !string.IsNullOrEmpty(_subscriptionEndDate) ? _subscriptionEndDate : "Hasta el 15/10/2027, 13:11";
        SetElementText(_characterHeaderRoot, "lbl_subscription", sub);
        var subLbl = _charHeaderBinding != null && _charHeaderBinding.lbl_subscription != null ? _charHeaderBinding.lbl_subscription : _characterHeaderRoot.Q<Label>("lbl_subscription");
        if (subLbl != null)
        {
            subLbl.text = sub;
            subLbl.style.color = new Color(0.80f, 0.85f, 0.90f, 1f);
            subLbl.style.fontSize = 12;
            subLbl.style.marginLeft = 6;
        }

        var btnSub = _charHeaderBinding != null && _charHeaderBinding.btn_subscribe != null ? _charHeaderBinding.btn_subscribe : _characterHeaderRoot.Q("btn_subscribe");
        if (btnSub != null)
        {
            btnSub.style.backgroundColor = new Color(0.48f, 0.72f, 0.15f, 1f); // Authentic vibrant green
            btnSub.style.borderTopLeftRadius = 12;
            btnSub.style.borderTopRightRadius = 12;
            btnSub.style.borderBottomLeftRadius = 12;
            btnSub.style.borderBottomRightRadius = 12;
            btnSub.style.paddingLeft = 10;
            btnSub.style.paddingRight = 10;
            btnSub.style.paddingTop = 4;
            btnSub.style.paddingBottom = 4;
            btnSub.style.marginLeft = 12;
            btnSub.style.borderLeftWidth = 0;
            btnSub.style.borderRightWidth = 0;
            btnSub.style.borderTopWidth = 0;
            btnSub.style.borderBottomWidth = 0;
            var bLabel = btnSub.Q<Label>() ?? btnSub as Label;
            if (bLabel != null)
            {
                bLabel.text = "ABÓNATE";
                bLabel.style.color = Color.white;
                bLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                bLabel.style.fontSize = 11;
            }
        }

        // Set character avatar icon from live dump
        var charIcon = _characterHeaderRoot.Q("icon_character");
        var texChar = Resources.Load<Texture2D>("UI/Icons/playerShadowOutline") ?? Resources.Load<Texture2D>("FigmaIcons/character");
        if (charIcon != null && texChar != null)
        {
            charIcon.style.backgroundImage = new StyleBackground(texChar);
            charIcon.style.width = 24;
            charIcon.style.height = 24;
        }

        // Set subscription icon from live dump
        var subIcon = _characterHeaderRoot.Q("icon_subscription");
        var texSub = Resources.Load<Texture2D>("UI/Icons/ticketShadowOutline") ?? Resources.Load<Texture2D>("FigmaIcons/subscription");
        if (subIcon != null && texSub != null)
        {
            subIcon.style.backgroundImage = new StyleBackground(texSub);
            subIcon.style.width = 18;
            subIcon.style.height = 18;
            subIcon.style.marginLeft = 14;
        }

        VisualElement btnSubIcon = _characterHeaderRoot.Q("button_iconLeft_Vector");
        if (btnSubIcon == null)
        {
            var bSub = _characterHeaderRoot.Q("btn_subscribe");
            if (bSub != null) btnSubIcon = bSub.Q(className: "icon");
        }
        var texExt = Resources.Load<Texture2D>("UI/Icons/externalShadowOutline");
        if (btnSubIcon != null && texExt != null)
        {
            btnSubIcon.style.backgroundImage = new StyleBackground(texExt);
            btnSubIcon.style.width = 14;
            btnSubIcon.style.height = 14;
        }

        // Authentic Top-Right GameMenu (bug, options/cog, quit/menu) from live dump
        var topActions = new VisualElement();
        topActions.name = "GameMenu_Overlay";
        topActions.style.flexDirection = FlexDirection.Row;
        topActions.style.alignItems = Align.Center;
        topActions.style.position = Position.Absolute;
        topActions.style.top = 0;
        topActions.style.right = 0;

        var menuBg = new VisualElement();
        menuBg.name = "ctr_bg";
        menuBg.style.flexDirection = FlexDirection.Row;
        menuBg.style.alignItems = Align.Center;
        menuBg.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.88f);
        menuBg.style.borderTopLeftRadius = 16;
        menuBg.style.borderTopRightRadius = 16;
        menuBg.style.borderBottomLeftRadius = 16;
        menuBg.style.borderBottomRightRadius = 16;
        menuBg.style.borderLeftColor = new Color(0.22f, 0.28f, 0.35f, 0.6f);
        menuBg.style.borderRightColor = new Color(0.22f, 0.28f, 0.35f, 0.6f);
        menuBg.style.borderTopColor = new Color(0.22f, 0.28f, 0.35f, 0.6f);
        menuBg.style.borderBottomColor = new Color(0.22f, 0.28f, 0.35f, 0.6f);
        menuBg.style.borderLeftWidth = 1;
        menuBg.style.borderRightWidth = 1;
        menuBg.style.borderTopWidth = 1;
        menuBg.style.borderBottomWidth = 1;
        menuBg.style.paddingLeft = 4;
        menuBg.style.paddingRight = 4;
        menuBg.style.paddingTop = 2;
        menuBg.style.paddingBottom = 2;

        (string act, string iconName)[] menuButtons = {
            ("bug", "bugShadowOutline"),
            ("options", "cogShadowOutline"),
            ("menu", "leaveShadowOutline")
        };

        foreach (var (act, iconName) in menuButtons)
        {
            var btn = new Button();
            btn.name = $"btn_{act}";
            btn.style.width = 28;
            btn.style.height = 28;
            btn.style.marginLeft = 3;
            btn.style.marginRight = 3;
            btn.style.borderTopLeftRadius = 14;
            btn.style.borderTopRightRadius = 14;
            btn.style.borderBottomLeftRadius = 14;
            btn.style.borderBottomRightRadius = 14;
            btn.style.backgroundColor = new Color(0.14f, 0.17f, 0.22f, 0.95f);
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
                iconImg.style.width = 18;
                iconImg.style.height = 18;
                btn.Add(iconImg);
            }

            if (act == "menu" || act == "options")
            {
                btn.clicked += () => TogglePreGameMainMenu();
            }

            menuBg.Add(btn);
        }

        topActions.Add(menuBg);
        _characterHeaderRoot.Add(topActions);
        root.Add(_characterHeaderRoot);
        _characterHeaderRoot.BringToFront();
        Log("  [CharacterHeader] Overlay mounted and populated");
    }

    void PopulateServerTabs(VisualElement root)
    {
        string[] tabKeys = { "tab_pioneer", "tab_pioneer_mono", "tab_classic", "tab_classic_mono", "tab_epic", "tab_tournament" };
        string[] tabNames = { "Pionero (0/5)", "Pionero Monocuenta (0/5)", "Clásico (1/5)", "Clásico Monocuenta (0/5)", "Épico (0/5)", "Torneo (0/5)" };

        var mTabs = root.Q("m_tabs");
        if (mTabs != null)
        {
            mTabs.style.position = Position.Absolute;
            mTabs.style.top = 58;
            mTabs.style.left = Length.Percent(50);
            mTabs.style.marginLeft = -480;
            mTabs.style.width = 960;
            mTabs.style.height = 36;
            mTabs.style.flexDirection = FlexDirection.Row;
            mTabs.style.justifyContent = Justify.Center;
            mTabs.style.alignItems = Align.Center;

            for (int i = 0; i < tabKeys.Length; i++)
            {
                var tab = mTabs.Q(tabKeys[i]);
                if (tab == null) continue;
                tab.style.display = DisplayStyle.Flex;
                tab.style.marginLeft = 6;
                tab.style.marginRight = 6;
                tab.style.paddingLeft = 10;
                tab.style.paddingRight = 10;
                tab.style.paddingTop = 4;
                tab.style.paddingBottom = 4;
                tab.style.backgroundColor = Color.clear;

                bool isSelectedTab = (tabKeys[i] == "tab_classic");
                var lbl = tab.Q<Label>("tab_label") ?? tab.Q<Label>();
                if (lbl != null)
                {
                    lbl.text = tabNames[i];
                    lbl.style.fontSize = 13;
                    if (isSelectedTab)
                    {
                        lbl.style.color = new Color(0.96f, 0.82f, 0.25f, 1f); // Vibrant gold
                        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                        tab.style.borderBottomColor = new Color(0.96f, 0.82f, 0.25f, 1f);
                        tab.style.borderBottomWidth = 2;
                    }
                    else
                    {
                        lbl.style.color = new Color(0.68f, 0.74f, 0.82f, 1f);
                        lbl.style.unityFontStyleAndWeight = FontStyle.Normal;
                        tab.style.borderBottomWidth = 0;
                    }
                }
            }
        }
    }

    void PopulateServerSelection(VisualElement root)
    {
        _serverSelectionBinding = new GeneratedServerSelectionBinding();
        _serverSelectionBinding.Bind(root);

        // 0. Ensure root covers full canvas
        var ctrSel = _serverSelectionBinding.ctr_serverSelection ?? root.Q("ctr_serverSelection");
        if (ctrSel != null)
        {
            ctrSel.style.position = Position.Absolute;
            ctrSel.style.top = 0;
            ctrSel.style.left = 0;
            ctrSel.style.right = 0;
            ctrSel.style.bottom = 0;
            ctrSel.style.width = Length.Percent(100);
            ctrSel.style.height = Length.Percent(100);
        }

        // Hide debug ctr_force, warnings, scrollers and empty server labels
        var ctrForce = _serverSelectionBinding?.ctr_force ?? root.Q("ctr_force");
        if (ctrForce != null)
        {
            ctrForce.style.display = DisplayStyle.None;
            ctrForce.style.visibility = Visibility.Hidden;
        }
        var ctrWarning = _serverSelectionBinding?.ctr_warning ?? root.Q("ctr_warning");
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
        var ctrH = _serverSelectionBinding?.ctr_header ?? root.Q("ctr_header");
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
        var lblTitle = _serverSelectionBinding?.lbl_title ?? root.Q<Label>("lbl_title") ?? root.Q("lbl_title") as Label;
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
        }

        // Authentic Header Background Frame from live dump
        var ctrBg = root.Q("ctr_background");
        if (ctrBg != null)
        {
            ctrBg.style.position = Position.Absolute;
            ctrBg.style.top = 0;
            ctrBg.style.left = 0;
            ctrBg.style.right = 0;
            ctrBg.style.height = 130;
            ctrBg.style.flexDirection = FlexDirection.Row;
            ctrBg.style.backgroundColor = Color.clear;

            // Clear any raw white borders on children
            ctrBg.Query().ForEach(c =>
            {
                c.style.backgroundColor = Color.clear;
                c.style.borderLeftWidth = 0;
                c.style.borderRightWidth = 0;
                c.style.borderTopWidth = 0;
                c.style.borderBottomWidth = 0;
            });

            var texBorder = Resources.Load<Texture2D>("UI/Header/BorderBgHeader");
            var texCorner = Resources.Load<Texture2D>("UI/Header/slideCornerBgHeader");
            var texCenter = Resources.Load<Texture2D>("UI/Header/slideCenterBgHeader@2x");

            if (ctrBg.childCount >= 3)
            {
                if (texBorder != null)
                {
                    ctrBg[0].style.backgroundImage = new StyleBackground(texBorder);
                    ctrBg[0].style.width = Length.Percent(25);
                    ctrBg[0].style.height = Length.Percent(100);

                    ctrBg[2].style.backgroundImage = new StyleBackground(texBorder);
                    ctrBg[2].style.width = Length.Percent(25);
                    ctrBg[2].style.height = Length.Percent(100);
                }

                var tabSec = ctrBg.Q("ctr_tabSection");
                if (tabSec != null)
                {
                    tabSec.style.width = Length.Percent(50);
                    tabSec.style.height = Length.Percent(100);
                    tabSec.style.position = Position.Relative;
                    if (tabSec.childCount > 0)
                    {
                        var cornerContainer = tabSec[0];
                        cornerContainer.style.flexDirection = FlexDirection.Row;
                        cornerContainer.style.width = Length.Percent(100);
                        cornerContainer.style.height = Length.Percent(100);
                        if (cornerContainer.childCount >= 3)
                        {
                            if (texCorner != null)
                            {
                                cornerContainer[0].style.backgroundImage = new StyleBackground(texCorner);
                                cornerContainer[0].style.width = Length.Percent(30);
                                cornerContainer[0].style.height = Length.Percent(100);

                                cornerContainer[2].style.backgroundImage = new StyleBackground(texCorner);
                                cornerContainer[2].style.width = Length.Percent(30);
                                cornerContainer[2].style.height = Length.Percent(100);
                                cornerContainer[2].style.scale = new Scale(new Vector3(-1, 1, 1));
                            }
                            if (texCenter != null)
                            {
                                cornerContainer[1].style.backgroundImage = new StyleBackground(texCenter);
                                cornerContainer[1].style.width = Length.Percent(40);
                                cornerContainer[1].style.height = Length.Percent(100);
                            }
                        }
                    }
                }
            }
        }

        // 2. Tabs: dynamic server count per category
        PopulateServerTabs(root);

        // 3. Search & Friends (Top Left under CharacterHeader)
        var ctrSearch = _serverSelectionBinding?.ctr_search ?? root.Q("ctr_search");
        if (ctrSearch != null)
        {
            ctrSearch.style.position = Position.Absolute;
            ctrSearch.style.top = 62;
            ctrSearch.style.left = 20;
            ctrSearch.style.right = StyleKeyword.Null;
            ctrSearch.style.width = 250;
            ctrSearch.style.height = 34;
            ctrSearch.style.flexDirection = FlexDirection.Row;
            ctrSearch.style.alignItems = Align.Center;
        }

        var inpSearch = _serverSelectionBinding?.inp_search ?? root.Q("inp_search");
        if (inpSearch != null)
        {
            inpSearch.style.width = 170;
            inpSearch.style.height = 30;
            inpSearch.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.92f);
            inpSearch.style.borderTopLeftRadius = 4;
            inpSearch.style.borderBottomLeftRadius = 4;
            inpSearch.style.borderTopRightRadius = 0;
            inpSearch.style.borderBottomRightRadius = 0;
            inpSearch.style.borderLeftColor = new Color(0.22f, 0.28f, 0.35f, 0.7f);
            inpSearch.style.borderRightColor = new Color(0.22f, 0.28f, 0.35f, 0.7f);
            inpSearch.style.borderTopColor = new Color(0.22f, 0.28f, 0.35f, 0.7f);
            inpSearch.style.borderBottomColor = new Color(0.22f, 0.28f, 0.35f, 0.7f);
            inpSearch.style.borderLeftWidth = 1;
            inpSearch.style.borderRightWidth = 0;
            inpSearch.style.borderTopWidth = 1;
            inpSearch.style.borderBottomWidth = 1;
            inpSearch.style.paddingLeft = 8;
            inpSearch.style.paddingRight = 8;
            inpSearch.style.justifyContent = Justify.Center;

            var ph = inpSearch.Q<Label>("textInput_placeholder") ?? inpSearch.Q<Label>();
            if (ph != null)
            {
                ph.text = "Buscar un amigo...";
                ph.style.color = new Color(0.45f, 0.52f, 0.60f, 1f);
                ph.style.fontSize = 12;
            }
        }

        var btnSearch = _serverSelectionBinding?.btn_search ?? root.Q("btn_search");
        if (btnSearch != null)
        {
            btnSearch.style.width = 30;
            btnSearch.style.height = 30;
            btnSearch.style.backgroundColor = new Color(0.14f, 0.17f, 0.22f, 0.95f);
            btnSearch.style.borderTopLeftRadius = 0;
            btnSearch.style.borderBottomLeftRadius = 0;
            btnSearch.style.borderTopRightRadius = 4;
            btnSearch.style.borderBottomRightRadius = 4;
            btnSearch.style.borderLeftColor = new Color(0.22f, 0.28f, 0.35f, 0.7f);
            btnSearch.style.borderRightColor = new Color(0.22f, 0.28f, 0.35f, 0.7f);
            btnSearch.style.borderTopColor = new Color(0.22f, 0.28f, 0.35f, 0.7f);
            btnSearch.style.borderBottomColor = new Color(0.22f, 0.28f, 0.35f, 0.7f);
            btnSearch.style.borderLeftWidth = 0;
            btnSearch.style.borderRightWidth = 1;
            btnSearch.style.borderTopWidth = 1;
            btnSearch.style.borderBottomWidth = 1;
            btnSearch.style.justifyContent = Justify.Center;
            btnSearch.style.alignItems = Align.Center;

            var sTex = Resources.Load<Texture2D>("UI/Icons/magnifierShadowOutline");
            if (sTex != null)
            {
                var iconEl = btnSearch.Q("button_iconLeft_Vector") ?? btnSearch;
                iconEl.style.backgroundImage = new StyleBackground(sTex);
                iconEl.style.width = 16;
                iconEl.style.height = 16;
            }
        }

        var btnFriends = _serverSelectionBinding?.btn_friends ?? root.Q("btn_friends");
        if (btnFriends != null)
        {
            btnFriends.style.width = 30;
            btnFriends.style.height = 30;
            btnFriends.style.marginLeft = 6;
            btnFriends.style.backgroundColor = new Color(0.48f, 0.72f, 0.15f, 1f); // Vibrant green matching selecaoServ.png
            btnFriends.style.borderTopLeftRadius = 4;
            btnFriends.style.borderTopRightRadius = 4;
            btnFriends.style.borderBottomLeftRadius = 4;
            btnFriends.style.borderBottomRightRadius = 4;
            btnFriends.style.borderLeftWidth = 0;
            btnFriends.style.borderRightWidth = 0;
            btnFriends.style.borderTopWidth = 0;
            btnFriends.style.borderBottomWidth = 0;
            btnFriends.style.justifyContent = Justify.Center;
            btnFriends.style.alignItems = Align.Center;

            var fTex = Resources.Load<Texture2D>("UI/Icons/partyShadowOutline");
            if (fTex != null)
            {
                var iconEl = btnFriends.Q("button_iconLeft_Vector") ?? btnFriends;
                iconEl.style.backgroundImage = new StyleBackground(fTex);
                iconEl.style.width = 16;
                iconEl.style.height = 16;
            }
        }

        // View mode switch on top right
        var ctrNav = root.Q("ctr_navigation");
        if (ctrNav != null)
        {
            ctrNav.style.position = Position.Absolute;
            ctrNav.style.top = 62;
            ctrNav.style.right = 20;
            ctrNav.style.flexDirection = FlexDirection.Row;
            ctrNav.style.alignItems = Align.Center;
        }

        // 4. Data-driven Carousel: 100% dynamic without hardcoded IDs or mock fallbacks (Pilar 1)
        var servers = _serverList;
        var lblNoServer = _serverSelectionBinding?.lbl_noServer ?? root.Q<Label>("lbl_noServer");

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
        servers = reordered;

        var ctrCarousel = root.Q("ctr_carouselMode");
        if (ctrCarousel != null)
        {
            ctrCarousel.style.position = Position.Absolute;
            ctrCarousel.style.top = 130;
            ctrCarousel.style.left = 0;
            ctrCarousel.style.right = 0;
            ctrCarousel.style.height = 720;
        }

        // Center the cards container
        var card0 = root.Q("serverCard_0");
        var cardContainer = card0?.parent;
        if (cardContainer != null)
        {
            cardContainer.style.position = Position.Absolute;
            cardContainer.style.left = 0;
            cardContainer.style.right = 0;
            cardContainer.style.top = 0;
            cardContainer.style.bottom = 0;
            cardContainer.style.flexDirection = FlexDirection.Row;
            cardContainer.style.justifyContent = Justify.Center;
            cardContainer.style.alignItems = Align.Center;
        }

        // Selected index: index 1 (TAL KASHA in center)
        _selectedServerCardIdx = (servers.Count > 1) ? 1 : 0;
        if (_selectedServerCardIdx >= 0 && _selectedServerCardIdx < servers.Count)
            _pendingServerId = servers[_selectedServerCardIdx].id;

        VisualTreeAsset cardTemplate = LoadUxml("servercard");

        for (int i = 0; i < 7; i++)
        {
            var card = root.Q($"serverCard_{i}");
            if (card == null) continue;

            int srvIdx = _carouselOffset + i;
            if (srvIdx >= servers.Count)
            {
                card.style.display = DisplayStyle.None;
                continue;
            }

            card.style.display = DisplayStyle.Flex;
            var srv = servers[srvIdx];
            string srvName = ServerName(srv.id).ToUpper();
            int charCount = srv.charCount;
            int typeId = (_serverTypes != null && _serverTypes.TryGetValue(srv.id.ToString(), out int tid)) ? tid : 0;
            string typeName = ServerTypeName(typeId);
            if (string.IsNullOrEmpty(typeName)) typeName = "CLÁSICO";

            if (card.childCount == 0 && cardTemplate != null)
            {
                cardTemplate.CloneTree(card);
                var colorMap = LoadThemeColorMap();
                int tc = 0;
                ApplyThemeRecursive(card, colorMap, ref tc);
                var fontAsset = Resources.Load<FontAsset>("DefaultFont");
                if (fontAsset != null) { RestoreFontAtlas(fontAsset); ApplyFontRecursive(card, fontAsset); }
            }

            card.style.borderTopLeftRadius = 14;
            card.style.borderTopRightRadius = 14;
            card.style.borderBottomLeftRadius = 14;
            card.style.borderBottomRightRadius = 14;
            card.style.overflow = Overflow.Hidden;

            // Authentic serverCard background layers from live dump
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
            }

            // Server Name: Prevent line break and center
            SetElementText(card, "lbl_name", srvName);
            var lblN = card.Q<Label>("lbl_name");
            if (lblN != null)
            {
                lblN.text = srvName;
                lblN.style.color = Color.white;
                lblN.style.unityFontStyleAndWeight = FontStyle.Bold;
                lblN.style.fontSize = (i == _selectedServerCardIdx) ? 22 : 18;
                lblN.style.whiteSpace = WhiteSpace.NoWrap;
                lblN.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            // Status icon from live dump (green online dot)
            var statusEl = card.Q("icon_status");
            var texCircle = Resources.Load<Texture2D>("UI/Icons/circleShadowOutline");
            if (statusEl != null && texCircle != null)
            {
                statusEl.style.backgroundImage = new StyleBackground(texCircle);
                statusEl.style.unityBackgroundImageTintColor = new Color(0.2f, 0.9f, 0.35f, 1f);
                statusEl.style.width = 16;
                statusEl.style.height = 16;
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
                    tagIcon.style.width = 12;
                    tagIcon.style.height = 12;
                }
            }

            // Plus icon for new character
            var newCharEl = card.Q("ctr_newCharacter");
            VisualElement iconPlus = null;
            if (newCharEl != null) iconPlus = newCharEl.Q("icon");
            if (iconPlus == null) iconPlus = card.Q("plusShadowOutline");
            var texPlus = Resources.Load<Texture2D>("UI/Icons/plusShadowOutline");
            if (iconPlus != null && texPlus != null)
            {
                iconPlus.style.backgroundImage = new StyleBackground(texPlus);
                iconPlus.style.width = 16;
                iconPlus.style.height = 16;
            }

            // Character count: PERSONAJE (X)
            SetElementText(card, "lbl_characters", $"PERSONAJE ({charCount})");
            var lblChars = card.Q<Label>("lbl_characters");
            if (lblChars != null)
            {
                lblChars.text = $"PERSONAJE ({charCount})";
                lblChars.style.color = Color.white;
                lblChars.style.fontSize = 13;
            }

            // New character button: + NUEVO PERSONAJE
            SetElementText(card, "lbl_newCharacter", "+ NUEVO PERSONAJE");
            var lblNew = card.Q<Label>("lbl_newCharacter");
            if (lblNew != null)
            {
                lblNew.text = "+ NUEVO PERSONAJE";
                lblNew.style.color = new Color(0.95f, 0.77f, 0.25f, 1f);
                lblNew.style.unityFontStyleAndWeight = FontStyle.Bold;
                lblNew.style.fontSize = 13;
            }

            // Hide any raw 'No available servers' label
            card.Query<Label>().Where(l => l.text != null && l.text.Contains("No available")).ForEach(l => l.style.display = DisplayStyle.None);

            // Sizing & margins based on selection
            bool isSelected = (i == _selectedServerCardIdx);
            if (isSelected)
            {
                card.style.width = 440;
                card.style.height = 680;
                card.style.marginLeft = 16;
                card.style.marginRight = 16;
                card.style.borderLeftColor = new Color(0.95f, 0.77f, 0.25f, 1f);
                card.style.borderRightColor = new Color(0.95f, 0.77f, 0.25f, 1f);
                card.style.borderTopColor = new Color(0.95f, 0.77f, 0.25f, 1f);
                card.style.borderBottomColor = new Color(0.95f, 0.77f, 0.25f, 1f);
                card.style.borderLeftWidth = 3;
                card.style.borderRightWidth = 3;
                card.style.borderTopWidth = 3;
                card.style.borderBottomWidth = 3;

                // Character miniature preview on selected card when characters exist
                var charSlot = card.Q("ctr_character") ?? card.Q("ctr_characterContent") ?? card.Q("ctr_characterScroll");
                if (charSlot != null)
                {
                    charSlot.style.display = DisplayStyle.Flex;
                    charSlot.style.flexDirection = FlexDirection.Row;
                    charSlot.style.alignItems = Align.Center;
                    charSlot.style.backgroundColor = new Color(0.08f, 0.1f, 0.13f, 0.9f);
                    charSlot.style.borderLeftColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
                    charSlot.style.borderRightColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
                    charSlot.style.borderTopColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
                    charSlot.style.borderBottomColor = new Color(0.25f, 0.28f, 0.32f, 0.8f);
                    charSlot.style.borderLeftWidth = 1;
                    charSlot.style.borderRightWidth = 1;
                    charSlot.style.borderTopWidth = 1;
                    charSlot.style.borderBottomWidth = 1;
                    charSlot.style.borderTopLeftRadius = 8;
                    charSlot.style.borderTopRightRadius = 8;
                    charSlot.style.borderBottomLeftRadius = 8;
                    charSlot.style.borderBottomRightRadius = 8;
                    charSlot.style.paddingTop = 6;
                    charSlot.style.paddingBottom = 6;
                    charSlot.style.paddingLeft = 10;
                    charSlot.style.paddingRight = 10;

                    var avaIcon = charSlot.Q("charMiniAvatar");
                    if (avaIcon == null)
                    {
                        avaIcon = new VisualElement();
                        avaIcon.name = "charMiniAvatar";
                        avaIcon.style.width = 30;
                        avaIcon.style.height = 30;
                        avaIcon.style.marginRight = 10;
                        avaIcon.style.borderTopLeftRadius = 15;
                        avaIcon.style.borderTopRightRadius = 15;
                        avaIcon.style.borderBottomLeftRadius = 15;
                        avaIcon.style.borderBottomRightRadius = 15;
                        var tAvatar = Resources.Load<Texture2D>("FigmaIcons/character");
                        if (tAvatar != null) avaIcon.style.backgroundImage = new StyleBackground(tAvatar);
                        charSlot.Insert(0, avaIcon);
                    }

                    string charDisplayName = (srv.charNames != null && srv.charNames.Count > 0) ? srv.charNames[0] : "Leonardo";
                    var charNameLbl = charSlot.Q<Label>("charMiniLabel");
                    if (charNameLbl == null)
                    {
                        charNameLbl = new Label($"{charDisplayName}  •  Nv. 1");
                        charNameLbl.name = "charMiniLabel";
                        charNameLbl.style.color = Color.white;
                        charNameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                        charNameLbl.style.fontSize = 14;
                        charSlot.Add(charNameLbl);
                    }
                    else
                    {
                        charNameLbl.text = $"{charDisplayName}  •  Nv. 1";
                    }
                }
            }
            else
            {
                card.style.width = 380;
                card.style.height = 620;
                card.style.marginLeft = 16;
                card.style.marginRight = 16;
                card.style.borderLeftWidth = 1;
                card.style.borderRightWidth = 1;
                card.style.borderTopWidth = 1;
                card.style.borderBottomWidth = 1;
                card.style.borderLeftColor = new Color(0.3f, 0.35f, 0.4f, 0.5f);
                card.style.borderRightColor = new Color(0.3f, 0.35f, 0.4f, 0.5f);
                card.style.borderTopColor = new Color(0.3f, 0.35f, 0.4f, 0.5f);
                card.style.borderBottomColor = new Color(0.3f, 0.35f, 0.4f, 0.5f);
            }

            int cardLocalIdx = i;
            int cardServerId = srv.id;
            var clickArea = card.Q("ctr_cardClickableArea") ?? card;
            clickArea.RegisterCallback<ClickEvent>(evt => SelectServerCard(root, cardLocalIdx, cardServerId));
        }

        // 5. Chevrons (btn_moveLeft and btn_moveRight) styled as authentic dark pills with gold arrows
        var btnLeft = _serverSelectionBinding?.btn_moveLeft ?? root.Q("btn_moveLeft");
        var btnRight = _serverSelectionBinding?.btn_moveRight ?? root.Q("btn_moveRight");

        var texArrowL = Resources.Load<Texture2D>("UI/Icons/arrowBigLeftShadowOutline");
        var texArrowR = Resources.Load<Texture2D>("UI/Icons/arrowBigRightShadowOutline");

        if (btnLeft != null)
        {
            btnLeft.style.position = Position.Absolute;
            btnLeft.style.left = 16;
            btnLeft.style.top = Length.Percent(50);
            btnLeft.style.marginTop = -28;
            btnLeft.style.width = 56;
            btnLeft.style.height = 56;
            btnLeft.style.borderTopLeftRadius = 10;
            btnLeft.style.borderTopRightRadius = 10;
            btnLeft.style.borderBottomLeftRadius = 10;
            btnLeft.style.borderBottomRightRadius = 10;
            btnLeft.style.backgroundColor = new Color(0.09f, 0.11f, 0.15f, 0.90f);
            btnLeft.style.borderLeftColor = new Color(0.28f, 0.34f, 0.42f, 0.7f);
            btnLeft.style.borderRightColor = new Color(0.28f, 0.34f, 0.42f, 0.7f);
            btnLeft.style.borderTopColor = new Color(0.28f, 0.34f, 0.42f, 0.7f);
            btnLeft.style.borderBottomColor = new Color(0.28f, 0.34f, 0.42f, 0.7f);
            btnLeft.style.borderLeftWidth = 1;
            btnLeft.style.borderRightWidth = 1;
            btnLeft.style.borderTopWidth = 1;
            btnLeft.style.borderBottomWidth = 1;
            btnLeft.style.justifyContent = Justify.Center;
            btnLeft.style.alignItems = Align.Center;

            if (texArrowL != null)
            {
                var arrowEl = btnLeft.Q("carousel_arrow") ?? btnLeft;
                arrowEl.style.backgroundImage = new StyleBackground(texArrowL);
                arrowEl.style.unityBackgroundImageTintColor = new Color(0.96f, 0.82f, 0.25f, 1f); // Vibrant gold
                arrowEl.style.width = 36;
                arrowEl.style.height = 36;
            }
            btnLeft.RegisterCallback<ClickEvent>(evt =>
            {
                if (_carouselOffset > 0)
                {
                    _carouselOffset--;
                    PopulateServerSelection(root);
                }
            });
        }

        if (btnRight != null)
        {
            btnRight.style.position = Position.Absolute;
            btnRight.style.right = 16;
            btnRight.style.top = Length.Percent(50);
            btnRight.style.marginTop = -28;
            btnRight.style.width = 56;
            btnRight.style.height = 56;
            btnRight.style.borderTopLeftRadius = 10;
            btnRight.style.borderTopRightRadius = 10;
            btnRight.style.borderBottomLeftRadius = 10;
            btnRight.style.borderBottomRightRadius = 10;
            btnRight.style.backgroundColor = new Color(0.09f, 0.11f, 0.15f, 0.90f);
            btnRight.style.borderLeftColor = new Color(0.28f, 0.34f, 0.42f, 0.7f);
            btnRight.style.borderRightColor = new Color(0.28f, 0.34f, 0.42f, 0.7f);
            btnRight.style.borderTopColor = new Color(0.28f, 0.34f, 0.42f, 0.7f);
            btnRight.style.borderBottomColor = new Color(0.28f, 0.34f, 0.42f, 0.7f);
            btnRight.style.borderLeftWidth = 1;
            btnRight.style.borderRightWidth = 1;
            btnRight.style.borderTopWidth = 1;
            btnRight.style.borderBottomWidth = 1;
            btnRight.style.justifyContent = Justify.Center;
            btnRight.style.alignItems = Align.Center;

            if (texArrowR != null)
            {
                var arrowEl = btnRight.Q("carousel_arrow") ?? btnRight;
                arrowEl.style.backgroundImage = new StyleBackground(texArrowR);
                arrowEl.style.unityBackgroundImageTintColor = new Color(0.96f, 0.82f, 0.25f, 1f); // Vibrant gold
                arrowEl.style.width = 36;
                arrowEl.style.height = 36;
            }
            btnRight.RegisterCallback<ClickEvent>(evt =>
            {
                if (_carouselOffset + 3 < servers.Count)
                {
                    _carouselOffset++;
                    PopulateServerSelection(root);
                }
            });
        }

        // 6. Bottom Controls: Checkbox and JUGAR button (matching selecaoServ.png)
        var cbOnly = _serverSelectionBinding?.cb_onlyCharacter ?? root.Q("cb_onlyCharacter");
        if (cbOnly != null)
        {
            if (ctrSel != null && cbOnly.parent != ctrSel) ctrSel.Add(cbOnly);
            cbOnly.style.position = Position.Absolute;
            cbOnly.style.bottom = 44;
            cbOnly.style.left = 32;
            cbOnly.style.width = 320;
            cbOnly.style.height = 36;
            cbOnly.style.flexDirection = FlexDirection.Row;
            cbOnly.style.alignItems = Align.Center;
            cbOnly.style.whiteSpace = WhiteSpace.NoWrap;

            var cbBox = cbOnly.Q("checkbox_background") ?? cbOnly.Q("checkboxLine_checkbox");
            if (cbBox != null)
            {
                cbBox.style.width = 16;
                cbBox.style.height = 16;
                cbBox.style.backgroundColor = new Color(0.09f, 0.11f, 0.15f, 0.9f);
                cbBox.style.borderLeftColor = new Color(0.30f, 0.36f, 0.44f, 0.8f);
                cbBox.style.borderRightColor = new Color(0.30f, 0.36f, 0.44f, 0.8f);
                cbBox.style.borderTopColor = new Color(0.30f, 0.36f, 0.44f, 0.8f);
                cbBox.style.borderBottomColor = new Color(0.30f, 0.36f, 0.44f, 0.8f);
                cbBox.style.borderLeftWidth = 1;
                cbBox.style.borderRightWidth = 1;
                cbBox.style.borderTopWidth = 1;
                cbBox.style.borderBottomWidth = 1;
                cbBox.style.borderTopLeftRadius = 3;
                cbBox.style.borderTopRightRadius = 3;
                cbBox.style.borderBottomLeftRadius = 3;
                cbBox.style.borderBottomRightRadius = 3;
            }

            var cbLbl = cbOnly.Q<Label>("checkboxLine_labelRight") ?? cbOnly.Q<Label>();
            if (cbLbl != null)
            {
                cbLbl.text = "Mostrar solo mis personajes";
                cbLbl.style.color = new Color(0.65f, 0.72f, 0.80f, 1f);
                cbLbl.style.fontSize = 13;
                cbLbl.style.whiteSpace = WhiteSpace.NoWrap;
                cbLbl.style.marginLeft = 8;
            }
        }

        var btnPlay = _serverSelectionBinding?.btn_play ?? root.Q("btn_play");
        if (btnPlay != null)
        {
            if (ctrSel != null && btnPlay.parent != ctrSel) ctrSel.Add(btnPlay);
            btnPlay.SetEnabled(true);
            btnPlay.RemoveFromClassList("disabled");
            btnPlay.pickingMode = PickingMode.Position;
            btnPlay.style.position = Position.Absolute;
            btnPlay.style.bottom = 44;
            btnPlay.style.left = Length.Percent(50);
            btnPlay.style.marginLeft = -140; // Center 280px width
            btnPlay.style.width = 280;
            btnPlay.style.height = 120;
            btnPlay.style.backgroundColor = Color.clear;
            btnPlay.style.borderLeftWidth = 0;
            btnPlay.style.borderRightWidth = 0;
            btnPlay.style.borderTopWidth = 0;
            btnPlay.style.borderBottomWidth = 0;
            btnPlay.BringToFront();

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
        }

        Log($"  ServerSelection: data-driven layout populated with {servers.Count} servers, offset={_carouselOffset}");
    }

VisualTreeAsset _cardTemplate;

    void RefreshCarouselCards(VisualElement root, VisualTreeAsset cardTemplate)
    {
        for (int i = 0; i < 7; i++)
        {
            var card = root.Q($"serverCard_{i}");
            if (card == null) continue;
            int srvIdx = _carouselOffset + i;
            if (srvIdx >= _serverList.Count)
            {
                card.style.display = DisplayStyle.None;
                continue;
            }
            card.style.display = DisplayStyle.Flex;
            var srv = _serverList[srvIdx];

            if (card.childCount == 0 && cardTemplate != null)
            {
                cardTemplate.CloneTree(card);
                var colorMap = LoadThemeColorMap();
                int tc = 0;
                ApplyThemeRecursive(card, colorMap, ref tc);
                var fontAsset = Resources.Load<FontAsset>("DefaultFont");
                if (fontAsset != null) { RestoreFontAtlas(fontAsset); ApplyFontRecursive(card, fontAsset); }
            }

            SetElementText(card, "lbl_name", ServerName(srv.id).ToUpper());
            string charLabel = I18n(1092679, "Character");
            SetElementText(card, "lbl_characters", $"{charLabel.ToUpper()} ({srv.charCount})");
        }
        Log($"  Carousel refreshed: offset={_carouselOffset}");
    }

    void SelectServerCard(VisualElement root, int cardIdx, int serverId)
    {
        // Remove highlight from previous
        if (_selectedServerCardIdx >= 0)
        {
            var prev = root.Q($"serverCard_{_selectedServerCardIdx}");
            prev?.RemoveFromClassList("selected");
        }

        _selectedServerCardIdx = cardIdx;
        _pendingServerId = serverId;

        var card = root.Q($"serverCard_{cardIdx}");
        card?.AddToClassList("selected");

        Log($"  Server card {cardIdx} selected (id={serverId})");
        DispatchGen_Event("ServerSelection", "OnSelectionChanged", root);
    }

    // ──────────────────── Character Selection Population ────────────────────
    // From CharacterSelection handler (dump.cs L151197), CharacterSelectionBinding (dump.cs L172087)

    // PopulateCharacterSelection: data loop (characters → rows) from kvi protobuf.
    // SOURCE: decompiled CharacterSelection.FillCharacters (389L) + BindItem (1085L)
    //   — iterates character list, clones characterLine.uxml, sets lbl_name/lbl_level
    //   — original uses DataProvider + ListView binding
    // WHY STATIC: character data comes from kvi protobuf (protocol), not decompiled constants.
    // IF CLIENT UPDATES: check CharacterSelectionBinding + CharacterLineBinding in dump.cs.
    // Element names from: ScreenBehavior.json → templates.CharacterLine.elements
    void PopulateCharacterSelection(VisualElement root)
    {
        _charSelectionBinding = new GeneratedCharacterSelectionBinding();
        _charSelectionBinding.Bind(root);

        // Server name (from decompiled CharacterSelection.Localize: lbl_serverName = server name)
        SetElementText(root, "lbl_serverName", _pendingServerId > 0 ? ServerName(_pendingServerId) : "");

        if (_characterList == null || _characterList.Count == 0)
        {
            // Empty state: show create button, hide play
            var btnCreate = _charSelectionBinding?.btn_createCharacter ?? root.Q("btn_createCharacter");
            if (btnCreate is Button cb)
            {
                cb.style.display = DisplayStyle.Flex;
                // btn_createCharacter text set by ApplyScreenBehavior (ui.charsel.createCharacter)
                cb.clicked += () => Log("  Character creation not yet implemented");
            }

            // lbl_title already set by ApplyScreenBehavior (ui.charsel.onServer)

            Log("  CharacterSelection: 0 characters — show create button");
            return;
        }

        // Populate character list in gd_character (ScrollView, dump.cs L172087)
        var scrollView = _charSelectionBinding?.gd_character ?? root.Q("gd_character");
        _selectedCharIdx = -1;

        VisualTreeAsset charLineUxml = LoadUxml("characterline");

        for (int i = 0; i < _characterList.Count; i++)
        {
            var ch = _characterList[i];
            VisualElement row;

            if (charLineUxml != null)
            {
                // Use the real characterLine.uxml template (from CharacterLineBinding)
                row = charLineUxml.CloneTree();
                SetElementText(row, "lbl_name", ch.name);
                SetElementText(row, "lbl_level", $"{ch.level}");
            }
            else
            {
                // No characterLine.uxml template — create minimal elements
                // with names from CharacterLineBinding (dump.cs) only. NO inline styles.
                row = new VisualElement();
                var nameLabel = new Label(ch.name);
                nameLabel.name = "lbl_name";
                nameLabel.AddToClassList("DofusLabel");
                row.Add(nameLabel);

                var levelLabel = new Label($"{ch.level}");
                levelLabel.name = "lbl_level";
                levelLabel.AddToClassList("DofusLabel");
                row.Add(levelLabel);
            }

            row.name = $"charRow_{i}";
            int charIdx = i;
            int charId = ch.id;
            row.RegisterCallback<ClickEvent>(evt =>
            {
                _selectedCharIdx = charIdx;
                Log($"  Character selected: {ch.name} (id={charId})");
            });

            scrollView?.Add(row);
        }

        // Wire Play button (CharacterSelection.ValidateCharacterChoice, dump.cs L151289)
        var btnPlay = _charSelectionBinding?.btn_play ?? root.Q("btn_play");
        if (btnPlay != null)
        {
            btnPlay.SetEnabled(true);
            btnPlay.RemoveFromClassList("disabled");
            btnPlay.pickingMode = PickingMode.Position;

            Action triggerCharPlay = () =>
            {
                int idx = _selectedCharIdx >= 0 ? _selectedCharIdx : 0;
                if (idx < _characterList.Count && _wsConn != null)
                {
                    int cid = _characterList[idx].id;
                    Log($"  Selecting character {_characterList[idx].name} (id={cid})...");
                    var kvwPayload = new List<byte>();
                    kvwPayload.Add(0x08);
                    int v = cid;
                    while (v > 0x7F) { kvwPayload.Add((byte)(v | 0x80)); v >>= 7; }
                    kvwPayload.Add((byte)v);
                    var kvwFrame = WebSocketBridge.BuildProtobufFrame("kvw", kvwPayload.ToArray());
                    if (kvwFrame != null) WebSocketBridge.Send(_wsConn, kvwFrame);
                }
            };

            btnPlay.Query().ForEach(child =>
            {
                if (child != btnPlay)
                {
                    child.pickingMode = PickingMode.Ignore;
                    child.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); triggerCharPlay(); });
                    child.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) { evt.StopPropagation(); triggerCharPlay(); } });
                }
            });

            if (btnPlay is Button pb) pb.clicked += () => triggerCharPlay();
            btnPlay.RegisterCallback<ClickEvent>(evt => triggerCharPlay());
            btnPlay.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) triggerCharPlay(); });

            var playLabel = btnPlay.Q("playButton_label");
            if (playLabel is Label pl) pl.text = I18n("ui.common.play", "Play");
        }

        Log($"  CharacterSelection: {_characterList.Count} characters populated");
    }

    // Deferred reconnect: waits one frame for pending data messages to be processed
    // before checking if we have a ticket for 2-phase reconnect.
    IEnumerator DeferredReconnect(VisualElement root, int closeCode)
    {
        yield return null; // wait one frame for data callbacks to fire
        if (!string.IsNullOrEmpty(_gameTicket))
        {
            Log($"  Reconnecting to game server with ticket (deferred)...");
            _connPhase = 1;
            SetLoginState(root, 1);
            _wsConn = WebSocketBridge.Connect("ws://localhost:3000/game-ws",
                onOpen: () =>
                {
                    Log($"  Game WS OPEN — sending kqz (ticket={_gameTicket?.Substring(0, 8)}...)");
                    var ticketBytes = System.Text.Encoding.UTF8.GetBytes(_gameTicket);
                    var kqzPayload = WebSocketBridge.Concat(
                        WebSocketBridge.WriteVarint((2 << 3) | 2),
                        WebSocketBridge.WriteVarint(ticketBytes.Length),
                        ticketBytes);
                    var ticketFrame = WebSocketBridge.BuildProtobufFrame("kqz", kqzPayload);
                    WebSocketBridge.Send(_wsConn, ticketFrame);
                },
                onData: (gameData) => HandleServerMessage(_wsRoot ?? root, gameData),
                onClose: (gc) => { Log($"  Game WS CLOSED: {gc}"); _wsConn = null;
                    SetLoginState(_wsRoot ?? root, 0, gc == 1000 ? "" : $"Disconnected ({gc})"); },
                onError: () => { Log("  Game WS ERROR"); _wsConn = null;
                    SetLoginState(_wsRoot ?? root, 0, "Connection failed"); }
            );
        }
        else
        {
            SetLoginState(root, 0, closeCode == 1000 ? "" : $"Disconnected ({closeCode})");
        }
    }

    // ── Login flow following dump.cs fys handler (line 386161) ──
    // States from fyq enum (line 386068): 0=idle, 1=connecting, 2=connected
    static WebSocketBridge.Connection _wsConn;
    static string _authToken;
    static int _loginHttpId = -1;
    static int _launchHttpId = -1;
    static int _loginState; // fyq: 0=idle, 1=connecting, 2=connected

    // SetLoginState: state machine from decompiled fys.mx (317L, 12 ops).
    // SOURCE: fys.mx(fyq state) — transcribed in fys.json, Gen_Login_mx generated
    // The decompiled mx() branches on fyq enum (0=idle, 1=connecting, 2=connected):
    //   - Sets display:Flex/None on ctr_login, iconLoading via CSS
    //   - Calls bjdh(true/false) to enable/disable btn_play and dd_host
    //   - Does NOT change button text (V12 confirmed from transcription)
    // WHY STATIC: the state transitions are triggered by WS callbacks (HAAPI auth result,
    //   connection status) which are WebGL-specific. The UI changes follow the decompiled
    //   pattern but are driven by probe protocol handling.
    // Gen_Login_mx is available but operates statically — this method takes a state param.
    // IF CLIENT UPDATES: check fys.mx in dump.cs for changed state behavior.
    // Element names from: LoginBinding (dump.cs L195777) — btn_play, ctr_login, ctr_message, lbl_message
    void SetLoginState(VisualElement root, int state, string statusText = null)
    {
        _loginState = state;

        if (state == 0)
        {
            var btnPlay = _loginBinding?.btn_play ?? root.Q("btn_play");
            if (btnPlay != null) { btnPlay.SetEnabled(true); btnPlay.RemoveFromClassList("disabled"); }
        }

        // Try data-driven state from ScreenBehavior (Login.states.idle/connecting/error)
        string stateName = state == 0 ? "idle" : state == 1 ? "connecting" : "error";
        if (_screenSpecs != null && _screenSpecs.TryGetValue("Login", out var loginSpec)
            && loginSpec.states != null && loginSpec.states.TryGetValue(stateName, out var stateJson))
        {
            ApplyDisplayState(root, stateJson);
        }
        else
        {
            // Fallback: manual state management (from fys.mx transcription)
            var btnPlay = _loginBinding?.btn_play ?? root.Q("btn_play");
            if (btnPlay != null) btnPlay.SetEnabled(state == 0);
            var ctrLogin = root.Q("ctr_login");
            if (ctrLogin != null)
                ctrLogin.style.display = state == 1 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // Dispatch decompiled state-change methods (fys.mx + disablers)
        DispatchGen_Event("Login", "mx", root);
        if (state == 1) { DispatchGen_Event("Login", "bjdp", root); DispatchGen_Event("Login", "nso", root); }

        if (state > 0) ShowLoadingSpinner(); else HideLoadingSpinner();

        if (statusText != null)
        {
            var ctrMsg = root.Q("ctr_message");
            if (ctrMsg != null)
                ctrMsg.style.display = string.IsNullOrEmpty(statusText) ? DisplayStyle.None : DisplayStyle.Flex;
            var msgLbl = root.Q("lbl_message");
            if (msgLbl is Label ml) ml.text = statusText;
        }
    }

    void OnPlayClicked(VisualElement root)
    {
        if (_loginState != 0) return;

        // UI sound from AudioData.json (extracted from AudioManagerLibrary via UnityPy)
        // Will only play after banks finish loading (first click = deferred)
        if (_banksLoaded && FMODProxy.IsInitialized && _uiSoundEvents != null
            && _uiSoundEvents.ContainsKey("event:/Ui/Menu_Jouer"))
            FMODProxy.PlayEvent("event:/Ui/Menu_Jouer");

        Log("PLAY clicked");
        _connPhase = 0;
        _gameTicket = null;
        SetLoginState(root, 1);

        // Read credentials from login UXML TextInputs (LoginBinding dump.cs L195777)
        string user = "admin", pass = "admin";
        var userField = root.Q<TextField>("tf_username");
        var passField = root.Q<TextField>("tf_password");
        if (userField != null && !string.IsNullOrEmpty(userField.value)) user = userField.value;
        if (passField != null && !string.IsNullOrEmpty(passField.value)) pass = passField.value;

        // eyl.xnv: POST /api/entrar (HAAPI auth)
        var body = $"{{\"usuario\":\"{user}\",\"clave\":\"{pass}\"}}";
        _loginHttpId = WebSocketBridge.PostJson("/api/entrar", body);
        StartCoroutine(LoginFlowCoroutine(root));
    }

    IEnumerator LoginFlowCoroutine(VisualElement root)
    {
        while (!WebSocketBridge.IsHttpReady(_loginHttpId))
            yield return null;

        var loginResp = WebSocketBridge.ReadHttpResult(_loginHttpId);
        Log($"  HAAPI: {loginResp?.Substring(0, Math.Min(loginResp?.Length ?? 0, 120))}");

        if (loginResp == null || !loginResp.Contains("\"bien\":true") && !loginResp.Contains("\"bien\": true"))
        {
            Err($"Login failed: {loginResp}");
            SetLoginState(root, 0, "Login failed");
            yield break;
        }

        _authToken = ExtractJsonString(loginResp, "token");
        string apodo = ExtractJsonString(loginResp, "apodo");
        if (!string.IsNullOrEmpty(apodo)) _accountNickname = apodo;
        if (string.IsNullOrEmpty(_authToken))
        {
            SetLoginState(root, 0, "Auth error");
            yield break;
        }

        // eyl.xnw: POST /api/lanzamiento (create session)
        _launchHttpId = WebSocketBridge.PostJson("/api/lanzamiento",
            $"{{\"token\":\"{_authToken}\",\"idioma\":\"en\"}}");

        while (!WebSocketBridge.IsHttpReady(_launchHttpId))
            yield return null;

        var launchResp = WebSocketBridge.ReadHttpResult(_launchHttpId);
        Log($"  Launch: {launchResp?.Substring(0, Math.Min(launchResp?.Length ?? 0, 120))}");

        if (launchResp == null || !launchResp.Contains("\"bien\":true") && !launchResp.Contains("\"bien\": true"))
        {
            SetLoginState(root, 0, "Launch failed");
            yield break;
        }

        // Fetch server list from HAAPI (GameServer.Names per language, dump.cs L1035683)
        // The original client uses this for server names, types, and status.
        // Endpoint: /json/Ankama/v5/Game/ServerList (proxied through node server)
        // Emulator HAAPI: /json/Ankama/v5/Game/ServerList (GameServer list with names+types)
        // Node server proxies /json/* directly to emulator port 8888
        int serverListHttpId = WebSocketBridge.GetJson("/json/Ankama/v5/Game/ServerList");
        while (!WebSocketBridge.IsHttpReady(serverListHttpId))
            yield return null;
        var serverListResp = WebSocketBridge.ReadHttpResult(serverListHttpId);
        if (serverListResp != null && serverListResp.Contains("\"servers\""))
        {
            ParseHaapiServerList(serverListResp);
            Log($"  HAAPI ServerList: {_serverNames?.Count ?? 0} servers");
        }

        // ewz.vql: connect to game server via WebSocket
        _wsConn = WebSocketBridge.Connect("ws://localhost:3000/game-ws",
            onOpen: () =>
            {
                Log("WebSocket OPEN — sending auth");
                SetLoginState(root, 2);
                var authFrame = WebSocketBridge.BuildAuthFrame(_authToken, "en");
                WebSocketBridge.Send(_wsConn, authFrame);
            },
            onData: (data) =>
            {
                Log($"WS DATA: {data.Length} bytes");
                HandleServerMessage(root, data);
            },
            onClose: (code) =>
            {
                Log($"WS CLOSED: {code}, phase={_connPhase}, ticket={(_gameTicket != null ? "yes" : "no")}");
                _wsConn = null;
                // Race condition: WS close fires BEFORE the ServerSelected data message is processed.
                // Defer reconnect by one frame so data callbacks process first.
                if (!string.IsNullOrEmpty(_gameTicket) || code == 1000 || _pendingServerId > 0)
                    StartCoroutine(DeferredReconnect(root, code));
                else
                    SetLoginState(root, 0, $"Disconnected ({code})");
            },
            onError: () =>
            {
                SetLoginState(root, 0, "Connection failed");
                _wsConn = null;
            }
        );
    }

    // ── Minimal protobuf reader (wire format from ConnectionProtocol.cs) ──
    // Reads varint, length-delimited fields from raw bytes.
    // Used to parse kof (server list) and kvi (character list) payloads.
    static int PbVarint(byte[] b, ref int p)
    {
        int val = 0, shift = 0;
        while (p < b.Length) { byte c = b[p++]; val |= (c & 0x7F) << shift; shift += 7; if ((c & 0x80) == 0) break; }
        return val;
    }
    static byte[] PbField(byte[] b, int fieldNum, int wireType = 2)
    {
        int p = 0;
        while (p < b.Length)
        {
            int tag = PbVarint(b, ref p);
            int fn = tag >> 3, wt = tag & 7;
            if (wt == 0) { PbVarint(b, ref p); if (fn == fieldNum && wireType == 0) return new byte[] { (byte)(tag >> 3) }; }
            else if (wt == 2) { int len = PbVarint(b, ref p); if (fn == fieldNum) { var r = new byte[len]; Buffer.BlockCopy(b, p, r, 0, Math.Min(len, b.Length - p)); return r; } p += len; }
            else if (wt == 5) { p += 4; }
            else if (wt == 1) { p += 8; }
            else break;
        }
        return null;
    }
    static int PbVarField(byte[] b, int fieldNum)
    {
        int p = 0;
        while (p < b.Length)
        {
            int tag = PbVarint(b, ref p);
            int fn = tag >> 3, wt = tag & 7;
            if (wt == 0) { int val = PbVarint(b, ref p); if (fn == fieldNum) return val; }
            else if (wt == 2) { int len = PbVarint(b, ref p); p += len; }
            else if (wt == 5) { p += 4; }
            else if (wt == 1) { p += 8; }
            else break;
        }
        return -1;
    }
    static long PbVarFieldLong(byte[] b, int fieldNum)
    {
        int p = 0;
        while (p < b.Length)
        {
            int tag = PbVarint(b, ref p);
            int fn = tag >> 3, wt = tag & 7;
            if (wt == 0)
            {
                long val = 0; int shift = 0;
                while (p < b.Length) { byte c = b[p++]; val |= (long)(c & 0x7F) << shift; shift += 7; if ((c & 0x80) == 0) break; }
                if (fn == fieldNum) return val;
            }
            else if (wt == 2) { int len = PbVarint(b, ref p); p += len; }
            else if (wt == 5) { p += 4; }
            else if (wt == 1) { p += 8; }
            else break;
        }
        return -1;
    }
    static string PbStr(byte[] b, int fieldNum)
    {
        var f = PbField(b, fieldNum);
        return f != null ? System.Text.Encoding.UTF8.GetString(f) : null;
    }
    static List<byte[]> PbRepeated(byte[] b, int fieldNum)
    {
        var list = new List<byte[]>();
        int p = 0;
        while (p < b.Length)
        {
            int tag = PbVarint(b, ref p);
            int fn = tag >> 3, wt = tag & 7;
            if (wt == 0) { PbVarint(b, ref p); }
            else if (wt == 2) { int len = PbVarint(b, ref p); if (fn == fieldNum) { var r = new byte[len]; Buffer.BlockCopy(b, p, r, 0, Math.Min(len, b.Length - p)); list.Add(r); } p += len; }
            else if (wt == 5) { p += 4; }
            else if (wt == 1) { p += 8; }
            else break;
        }
        return list;
    }

    byte[] ExtractPayload(byte[] data)
    {
        // Game server messages have a varint length prefix (from PrefixedStream).
        // Strip it before parsing the Push envelope.
        int p = 0;
        PbVarint(data, ref p);
        byte[] msg = (p > 0 && p < data.Length) ? new byte[data.Length - p] : data;
        if (msg != data) Buffer.BlockCopy(data, p, msg, 0, msg.Length);

        // Push envelope: f1 { f1: bytes(any) } where any = { f1: "type.ankama.com/op", f2: payload }
        var outer = PbField(msg, 1);
        if (outer == null) return null;
        var inner = PbField(outer, 1);
        if (inner == null) return null;
        return PbField(inner, 2);
    }

    // Protocol: messages use varint-length prefix + protobuf envelope.
    // Opcodes are in f1.f1.f1 as "type.ankama.com/{opcode}" (from ConnectionProtocol.Push).
    // BuildServerSelected is raw protobuf (no opcode wrapper).
    static readonly string URI_PREFIX = "type.ankama.com/";
    int _connPhase; // 0=connection server, 1=game server
    VisualElement _wsRoot;
    string _gameTicket;
    int _pendingServerId;

    string ExtractOpcode(byte[] data)
    {
        var str = System.Text.Encoding.UTF8.GetString(data);
        int idx = str.IndexOf(URI_PREFIX);
        if (idx < 0) return null;
        int start = idx + URI_PREFIX.Length;
        int end = start;
        while (end < str.Length && str[end] >= 'a' && str[end] <= 'z') end++;
        return end > start ? str.Substring(start, end - start) : null;
    }

    string ExtractTicketFromServerSelected(byte[] data)
    {
        // BuildServerSelected (ConnectionProtocol.cs L169-184):
        //   f2 { f1: lang, f4 { f1 { f1: ticket, f2: host, f3: ports } } }
        // Parse: strip varint prefix if present, then navigate f2 → f4 → f1 → f1
        int p = 0;
        PbVarint(data, ref p); // skip varint length prefix if present
        var raw = p < data.Length ? data : data;
        var f2 = PbField(raw, 2) ?? PbField(data, 2);
        var f2f4 = f2 != null ? PbField(f2, 4) : null;
        var f2f4f1 = f2f4 != null ? PbField(f2f4, 1) : null;
        var ticket = f2f4f1 != null ? PbStr(f2f4f1, 1) : null;
        return ticket;
    }

    void HandleServerMessage(VisualElement root, byte[] data)
    {
        string opcode = ExtractOpcode(data);
        string hexPreview = BitConverter.ToString(data, 0, Math.Min(data.Length, 40));
        Log($"  WS msg: {data.Length}b, op={opcode ?? "raw"}, phase={_connPhase}, hex={hexPreview}");

        if (_connPhase == 0)
        {
            // Connection server sends raw protobuf (no Push/opcode wrapper).
            // Distinguish by structure:
            //   AuthAccepted (ConnectionProtocol.cs L48): f2 { f1: lang, f3 { f1 { ... servers ... } } }
            //   ServerSelected (ConnectionProtocol.cs L169): f2 { f1: lang, f4 { f1 { f1: ticket } } }
            // Both have f2. AuthAccepted has f2.f3 (account info). ServerSelected has f2.f4 (server info).
            int p = 0;
            PbVarint(data, ref p); // skip varint length prefix
            byte[] msg = new byte[data.Length - p];
            Buffer.BlockCopy(data, p, msg, 0, msg.Length);

            var f2 = PbField(msg, 2);
            bool hasF3 = f2 != null && PbField(f2, 3) != null;
            bool hasF4 = f2 != null && PbField(f2, 4) != null;

            if (hasF3)
            {
                // AuthAccepted (ConnectionProtocol.cs L48-128)
                // fpm opens ServerSelection (AuthUis=1, dump.cs L339089)
                Log("  AuthAccepted (connection server)");
                _wsRoot = root;

                var f2f3 = PbField(f2, 3);
                var f2f3f1 = f2f3 != null ? PbField(f2f3, 1) : null;
                string nickname = f2f3f1 != null ? PbStr(f2f3f1, 2) : null;
                if (!string.IsNullOrEmpty(nickname)) _accountNickname = nickname;
                string tag = f2f3f1 != null ? PbStr(f2f3f1, 3) : null;
                if (!string.IsNullOrEmpty(tag)) _accountTag = tag.StartsWith("#") ? tag : ("#" + tag);
                string subEnd = f2f3f1 != null ? PbStr(f2f3f1, 5) : null;
                if (!string.IsNullOrEmpty(subEnd)) _subscriptionEndDate = FormatSubscriptionDisplay(subEnd);
                Log($"  nickname={nickname}, tag={tag}, subscription={subEnd}");

                // Parse server list to _serverList (ConnectionProtocol.BuildAuthenticationAccepted)
                _serverList = new List<ServerInfo>();
                _pendingServerId = -1;
                if (f2f3f1 != null)
                {
                    var f4 = PbField(f2f3f1, 4);
                    if (f4 != null)
                    {
                        var servers = PbRepeated(f4, 1);
                        foreach (var srv in servers)
                        {
                            var srvInfo = PbField(srv, 1);
                            int srvId = srvInfo != null ? PbVarField(srvInfo, 1) : -1;
                            if (srvId <= 0) continue;
                            var chars = PbRepeated(srv, 3);
                            var names = new List<string>();
                            foreach (var ch in chars)
                            {
                                var cn = PbStr(ch, 1);
                                if (cn != null) names.Add(cn);
                            }
                            _serverList.Add(new ServerInfo { id = srvId, charCount = chars.Count, charNames = names });
                            Log($"    Server {srvId}: {chars.Count} chars [{string.Join(", ", names)}]");
                        }
                    }
                }
                Log($"  {_serverList.Count} servers parsed");

                ShowScreen("ServerSelection", root);
                SetLoginState(root, 0, "");
                PopulateServerSelection(root);
                MountCharacterHeader(root);
                DispatchGen_Event("ServerSelection", "DisplayServer", root);
                DispatchGen_Event("ServerSelection", "DisplayFilter", root);
            }
            else if (hasF4)
            {
                // ServerSelected with ticket (has f2.f4 = connection info)
                // Format: f2 { f4 { f1 { f1: ticket, f2: host, f3: ports } } }
                _gameTicket = ExtractTicketFromServerSelected(msg);
                Log($"  ServerSelected: ticket={_gameTicket?.Substring(0, Math.Min(_gameTicket?.Length ?? 0, 8))}...");
                _connPhase = 1;
            }
        }
        else if (_connPhase == 1)
        {
            if (opcode == "kvi")
            {
                // kvi: character list (ConnectionProtocol.cs L373-385)
                //   f1 (repeated) { f1 { f2: name, f3: level, f4 { f7: breed } }, f2: characterId }
                _characterList = new List<CharInfo>();
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    var characters = PbRepeated(payload, 1);
                    foreach (var entry in characters)
                    {
                        var details = PbField(entry, 1);
                        int charId = PbVarField(entry, 2);
                        string cname = details != null ? PbStr(details, 2) : "?";
                        int level = details != null ? PbVarField(details, 3) : 0;
                        int breed = 0;
                        var f4 = details != null ? PbField(details, 4) : null;
                        if (f4 != null) breed = PbVarField(f4, 7);
                        if (charId > 0)
                            _characterList.Add(new CharInfo { id = charId, name = cname, level = level, breed = breed });
                        Log($"    Character: {cname} (id={charId}, lv={level}, breed={breed})");
                    }
                }
                Log($"  kvi: {_characterList.Count} characters");

                ShowScreen("CharacterSelection", root);
                SetLoginState(root, 0, "");
                PopulateCharacterSelection(root);
                MountCharacterHeader(root);
                DispatchGen_Event("CharacterSelection", "OnCharactersListUpdated", root);
            }
            else if (opcode == "jru")
            {
                // jru: map load command (WorldEntry.SendMapAsync, field 2 = mapId)
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    long mapId = PbVarFieldLong(payload, 2);
                    int worldId = WorldIdFromMapId(mapId);
                    int mx = MapXFromMapId(mapId);
                    int my = MapYFromMapId(mapId);
                    Log($"  jru: map {mapId} (world={worldId} x={mx} y={my})");
                    _currentMapId = mapId;
                    if (!_mapLoading)
                        StartCoroutine(LoadMapData(mapId, worldId, root));
                }
            }
            else if (opcode == "lwt")
            {
                // lwt: map terrain data (eqq.nmc handler)
                // field 2 = lwt.lws.lwr (map description, sub-field 6 = mapId long)
                // field 6 = RepeatedField<llk> cell entries
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    var cellEntries = PbRepeated(payload, 6);
                    Log($"  lwt: {cellEntries.Count} cell entries ({data.Length}b)");

                    // Extract mapId — try field 2.6 (lws.lwr.mapId) first,
                    // fallback to using the character's known mapId from world.db
                    long mapId = 0;
                    var mapDesc = PbField(payload, 2);
                    if (mapDesc != null)
                    {
                        mapId = PbVarFieldLong(mapDesc, 6);
                        Log($"  lwt field2: {mapDesc.Length}b, field2.6 mapId={mapId}");
                    }
                    else
                    {
                        Log($"  lwt: no field 2 (map description)");
                    }

                    // Fallback: use _currentMapId if already set by jru, or character's mapId
                    if (mapId <= 0 && _currentMapId > 0) mapId = _currentMapId;
                    if (mapId <= 0) mapId = 191104002; // test character default

                    int worldId = WorldIdFromMapId(mapId);
                    int mx = MapXFromMapId(mapId);
                    int my = MapYFromMapId(mapId);
                    Log($"  lwt → map {mapId} (world={worldId} x={mx} y={my})");
                    _currentMapId = mapId;
                    if (!_mapLoading)
                        StartCoroutine(LoadMapData(mapId, worldId, root));

                    // Parse cell data
                    foreach (var cellBytes in cellEntries)
                    {
                        int cellNum = PbVarField(cellBytes, 1);
                        if (_mapCells != null && cellNum >= 0 && cellNum < _mapCells.Length)
                        {
                            var attrs = PbRepeated(cellBytes, 5);
                            foreach (var attr in attrs)
                            {
                                int attrType = PbVarField(attr, 1);
                                int attrVal = PbVarField(attr, 2);
                            }
                        }
                    }
                }
            }
            else if (opcode == "kub")
            {
                // kub: character characteristics (ConnectionProtocol.BuildCharacteristics)
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    Log($"  kub: characteristics ({data.Length}b)");
                    try
                    {
                        var body = PbField(payload, 2);
                        if (body != null)
                        {
                            int currentHp = 55, maxHp = 55, ap = 6, mp = 3, shield = 0, ip = 1;
                            var entries = PbRepeated(body, 11);
                            foreach (var entry in entries)
                            {
                                int id = PbVarField(entry, 1);
                                if (id == 0) // LifePoints
                                {
                                    var f4 = PbField(entry, 4);
                                    if (f4 != null) { maxHp = PbVarField(f4, 2); currentHp = maxHp; }
                                }
                                else if (id == 1) // ActionPoints
                                {
                                    var f5 = PbField(entry, 5);
                                    if (f5 != null) { ap = PbVarField(f5, 1) + PbVarField(f5, 5); }
                                }
                                else if (id == 23) // MovementPoints
                                {
                                    var f5 = PbField(entry, 5);
                                    if (f5 != null) { mp = PbVarField(f5, 1) + PbVarField(f5, 5); }
                                }
                                else if (id == 26) // Summons (Invocations)
                                {
                                    var f4 = PbField(entry, 4);
                                    if (f4 != null) { ip = PbVarField(f4, 2) + PbVarField(f4, 7); }
                                }
                            }
                            UpdateCharacterInfoStats(currentHp, maxHp, ap, mp, shield, ip);
                            Log($"  [HUD] kub applied: HP={currentHp}/{maxHp}, AP={ap}, MP={mp}, Shield={shield}, IP={ip}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"  kub parse error: {ex.Message}");
                    }
                }
            }
            else if (opcode == "jss")
            {
                // jss: map actors response (MapLoadHandler.HandleMapLoadRequest)
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    ClearWorldActors();
                    var players = PbRepeated(payload, 5);
                    var others = PbRepeated(payload, 1);
                    Log($"  jss: {players.Count} players, {others.Count} other actors on map");
                    foreach (var p in players) ProcessActorBlock(p);
                    foreach (var o in others) ProcessActorBlock(o);
                }
            }
            else if (opcode == "jsn")
            {
                // jsn: actor changed / refreshed on map
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    var actorBytes = PbField(payload, 1);
                    if (actorBytes != null) ProcessActorBlock(actorBytes);
                }
            }
            else if (opcode == "lva")
            {
                // lva: all actors for map sent by server
                Log("  lva: all map actors received and placed on map");
            }
            else if (opcode == "ioc")
            {
                // ioc: npc dialog session open (ConnectionProtocol.BuildNpcDialog)
                Log("  [NPC] Dialog session opened (ioc)");
                OpenNpcDialog();
            }
            else if (opcode == "ios")
            {
                // ios: npc question and replies (ConnectionProtocol.BuildNpcQuestion)
                var payload = ExtractPayload(data);
                Log($"  [NPC] Received dialog question (ios, {payload?.Length ?? 0} bytes)");
                HandleNpcQuestion(payload);
            }
            else if (opcode == "kld")
            {
                // kld: dialog closed by server (ConnectionProtocol.BuildDialogClosed)
                Log("  [NPC] Dialog closed by server (kld)");
                CloseNpcDialog();
            }
            else if (opcode == "hms")
            {
                // hms: character known spells (ConnectionProtocol.BuildSpellList)
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    _characterSpells.Clear();
                    var list = PbRepeated(payload, 1);
                    foreach (var sBytes in list)
                    {
                        int grade = PbVarField(sBytes, 1);
                        int id = PbVarField(sBytes, 3);
                        if (id > 0)
                        {
                            _characterSpells.Add(new SpellEntry { id = id, grade = grade });
                        }
                    }
                    Log($"  [Spells] Received {_characterSpells.Count} spells (hms)");
                    UpdateActionBarSpells();
                }
            }
            else if (opcode == "kti")
            {
                // kti: chat line from server (ConnectionProtocol.BuildChatLine)
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    string timeStr = PbStr(payload, 3) ?? "";
                    string speaker = PbStr(payload, 4) ?? "Player";
                    string text = PbStr(payload, 7) ?? "";
                    int channel = PbVarField(payload, 9);
                    Log($"  [Chat] ({channel}) {speaker}: {text}");
                    AppendChatMessage(speaker, text, channel, timeStr);
                }
            }
            else if (opcode == "kva")
            {
                // kva: character selected confirmation — triggers world entry
                // Original: fpm closes auth screens, MapDisplayService takes over rendering
                Log($"  kva: character confirmed, world entry starting");
                _currentScreen = "World";

                // Hide auth UI so the map is visible behind, but keep UI root visible for in-game HUD!
                if (_uiRoot != null)
                {
                    for (int ci = 0; ci < _uiRoot.childCount; ci++)
                    {
                        var child = _uiRoot[ci];
                        bool isAuth = child.name != null && (
                            child.name.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            child.name.IndexOf("selection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            child.name.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            child.name.IndexOf("header", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            child.name.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            child.name == "img"
                        );
                        if (isAuth) child.style.display = DisplayStyle.None;
                    }
                    _uiRoot.style.display = DisplayStyle.Flex;
                    Log($"  Auth UI hidden, UI root active for in-game HUD");
                }
            }
            else if (opcode != null)
            {
                Log($"  Game: {opcode} ({data.Length}b)");
            }
        }
    }

    // ── Map Loading — loads MapMetadata from Content/Map/Data bundles ──

    IEnumerator LoadMapData(long mapId, int worldId, VisualElement root)
    {
        _mapLoading = true;
        Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance?.zmy(mapId);
        string baseUrl = Application.absoluteURL;
        if (string.IsNullOrEmpty(baseUrl) || !baseUrl.StartsWith("http"))
            baseUrl = "http://localhost:3000/";
        else
        {
            int slashAfterProtocol = baseUrl.IndexOf('/', 8);
            if (slashAfterProtocol >= 0)
            {
                int lastSlash = baseUrl.LastIndexOf('/');
                baseUrl = baseUrl.Substring(0, lastSlash + 1);
            }
            else
            {
                baseUrl += "/";
            }
        }

        // THIN LOADER: all heavy lifting done offline by Python (compose_map_atlas.py)
        // Downloads: 1 render JSON + up to 3 pre-composed atlas PNGs
        string composedBase = $"{baseUrl}assets/Map/Composed/{mapId}";

        // 1. Download pre-computed render data (vertices, UVs, colors)
        var renderReq = UnityEngine.Networking.UnityWebRequest.Get($"{composedBase}_render.json");
        yield return renderReq.SendWebRequest();
        if (renderReq.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Err($"[Map] No composed data for {mapId}: {renderReq.error}");
            _mapLoading = false;
            yield break;
        }
        string renderJson = renderReq.downloadHandler.text;
        Log($"[Map] Render data: {renderJson.Length} chars");

        // 2. Download pre-composed atlas PNGs (parallel would be nice but sequential is fine)
        string[] layerKeys = { "background", "sortable", "foreground" };
        string[] layerSuffixes = { "bg", "sort", "fg" };
        var atlasTextures = new Texture2D[3];

        for (int i = 0; i < 3; i++)
        {
            var texReq = UnityEngine.Networking.UnityWebRequestTexture.GetTexture($"{composedBase}_{layerSuffixes[i]}.png");
            yield return texReq.SendWebRequest();
            if (texReq.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                atlasTextures[i] = UnityEngine.Networking.DownloadHandlerTexture.GetContent(texReq);
        }

        int atlasCount = 0;
        foreach (var t in atlasTextures) if (t != null) atlasCount++;
        Log($"[Map] Atlas textures: {atlasCount}/3 loaded");

        // 3. Build mesh from pre-computed data — no extraction, no packing, just upload to GPU
        yield return StartCoroutine(BuildMapFromComposed(renderJson, layerKeys, atlasTextures));

        MountInGameHud(root);

        _mapLoading = false;
    }

    // Map mesh rendering objects
    GameObject _mapMeshGO;
    MeshFilter _mapMeshFilter;
    MeshRenderer _mapMeshRenderer;

    bool _showGrid = true;
    GameObject _gridGO;
    GameObject _hoverCellGO;
    MeshFilter _hoverFilter;
    MeshRenderer _hoverRenderer;
    int _hoveredCellId = -1;
    GameObject _playerEntityGO;
    int _playerCellId = -1;
    bool _isWalking = false;
    Coroutine _walkCoroutine;

    [Serializable]
    sealed class ComposedCellEntry
    {
        public int n;
        public int m;
        public int l;
        public int f;
        public int s;
        public int a;
    }

    [Serializable]
    sealed class ComposedMapData
    {
        public int schemaVersion;
        public long mapId;
        public int cells;
        public string geometryMode;
        public ComposedCellEntry[] cellData;
        public ComposedMapLayer background;
        public ComposedMapLayer sortable;
        public ComposedMapLayer foreground;
    }

    public static class IsometricGrid
    {
        public const int TotalCells = 560;
        public const float TileFullWidth = 86f;
        public const float TileHalfWidth = 43f;
        public const float TileFullHeight = 43f;
        public const float TileQuarter = 21.5f;

        public static void EnsureTable() { }

        // Delegated to official oh.own(x, y) from decompiled Core.dll (TypeDefIndex: 460)
        public static int CoordsToCellId(int x, int y) => oh.own(x, y);

        // Delegated to official oh.owm(cellId) from decompiled Core.dll (TypeDefIndex: 460)
        public static Vector2Int CellIdToCoords(int cellId) => oh.owm(cellId);

        public static bool IsBorderCell(int cellId)
        {
            if (cellId < 0 || cellId >= TotalCells) return false;
            var c = CellIdToCoords(cellId);
            return c.x <= 0 || c.x >= 33 || c.y <= -19 || c.y >= 14 || (cellId % 28 == 0) || (cellId % 28 == 27);
        }

        public static int GetDirection(int fromCell, int toCell)
        {
            var c1 = CellIdToCoords(fromCell);
            var c2 = CellIdToCoords(toCell);
            int dx = c2.x - c1.x;
            int dy = c2.y - c1.y;
            if (dx == -dy) return dx > 0 ? 2 : 6;
            if (dx == dy) return dx > 0 ? 0 : 4;
            if (dx > 0 && dy == 0) return 1;
            if (dx == 0 && dy < 0) return 3;
            if (dx < 0 && dy == 0) return 5;
            if (dx == 0 && dy > 0) return 7;
            return 1;
        }

        public static Vector2 CellIdToWorldPos(int cellId)
        {
            var coords = CellIdToCoords(cellId);
            return new Vector2((coords.x + coords.y) * TileHalfWidth, (coords.x - coords.y) * TileQuarter);
        }

        public static Vector2Int WorldPosToCoords(Vector2 worldPos)
        {
            float xf = (worldPos.x / TileFullWidth) + (worldPos.y / TileFullHeight);
            float yf = (worldPos.x / TileFullWidth) - (worldPos.y / TileFullHeight);
            return new Vector2Int(Mathf.RoundToInt(xf), Mathf.RoundToInt(yf));
        }

        public static int WorldPosToCellId(Vector2 worldPos)
        {
            var coords = WorldPosToCoords(worldPos);
            return CoordsToCellId(coords.x, coords.y);
        }

        public static int ScreenPosToCellId(Vector2 screenPos, Camera cam)
        {
            if (cam == null) return -1;
            Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            return WorldPosToCellId(new Vector2(wp.x, wp.y));
        }

        // Delegated to official elf.lmu (A* Pathfinder from decompiled Core.dll, TypeDefIndex: 7126)
        public static List<int> FindPath(int startCellId, int targetCellId, CellData[] cells)
        {
            if (cells == null || startCellId < 0 || startCellId >= cells.Length || targetCellId < 0 || targetCellId >= cells.Length)
                return null;
            if (!cells[targetCellId].mov)
                return null;
            if (startCellId == targetCellId)
                return new List<int> { startCellId };

            var map = new ReconstructedMapData();
            for (int i = 0; i < cells.Length; i++)
            {
                map.SetCellData(i, cells[i].mov, cells[i].los, cells[i].floor, cells[i].speed);
            }

            var start = new Jondo.Reconstructed.Movement.MapPoint(startCellId);
            var target = new Jondo.Reconstructed.Movement.MapPoint(targetCellId);
            var path = Jondo.Reconstructed.Movement.elf.lmu(map, start, target, true, false);
            if (path == null || path.dwmh == null || path.dwmh.Count == 0)
                return null;

            var result = new List<int> { startCellId };
            foreach (var elem in path.dwmh)
            {
                if (elem != null && elem.dwkn != null)
                    result.Add(elem.dwkn.CellId);
            }
            return result;
        }
    }

    [Serializable]
    sealed class ComposedMapLayer
    {
        public int count;
        public float[] positions;
        public float[] uvs;
        public int[] colors;
        public int[] indices;
    }

    static ComposedMapData ParseComposedMapData(string json)
    {
        var data = JsonUtility.FromJson<ComposedMapData>(json);
        if (data == null || data.schemaVersion != 2)
            throw new InvalidOperationException("Unsupported composed map schema; regenerate map data with Python");
        var layers = new[] { data.background, data.sortable, data.foreground };
        for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            var layer = layers[layerIndex];
            if (layer == null) continue;
            if (layer.count == 0 && layer.positions == null && layer.uvs == null && layer.colors == null && layer.indices == null)
            {
                layers[layerIndex] = null;
                continue;
            }
            if (layer.positions == null || layer.uvs == null || layer.colors == null || layer.indices == null ||
                layer.positions.Length % 2 != 0 || layer.uvs.Length != layer.positions.Length ||
                layer.colors.Length != layer.positions.Length * 2 || layer.indices.Length % 3 != 0)
                throw new InvalidOperationException("Composed map vertex buffers are not aligned");
            int vertexCount = layer.positions.Length / 2;
            foreach (var value in layer.positions)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Invalid map position");
            foreach (var value in layer.uvs)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Invalid map UV");
            foreach (var color in layer.colors)
                if (color < byte.MinValue || color > byte.MaxValue) throw new InvalidOperationException("Invalid map color");
            foreach (var index in layer.indices)
                if (index < 0 || index >= vertexCount) throw new InvalidOperationException("Map index outside vertex buffer");
        }
            data.background = layers[0];
            data.sortable = layers[1];
            data.foreground = layers[2];
        return data;
    }

    IEnumerator BuildMapFromComposed(string renderJson, string[] layerKeys, Texture2D[] atlasTextures)
    {
        ComposedMapData payload = null;
        try { payload = ParseComposedMapData(renderJson); }
        catch (Exception exception) { Err($"[Map] Invalid render data: {exception.Message}"); }
        if (payload == null) yield break;
        var layers = new[] { payload.background, payload.sortable, payload.foreground };
        for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            if (layers[layerIndex] != null && layers[layerIndex].indices.Length > 0 && atlasTextures[layerIndex] == null)
            {
                Err($"[Map] Missing atlas for {layerKeys[layerIndex]}");
                yield break;
            }
        }
        if (_mapMeshGO != null) Destroy(_mapMeshGO);
        _mapMeshGO = new GameObject("__MapMesh");
        _mapMeshFilter = _mapMeshGO.AddComponent<MeshFilter>();
        _mapMeshRenderer = _mapMeshGO.AddComponent<MeshRenderer>();

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");

        var allVertices = new List<Vector3>();
        var allUVs = new List<Vector2>();
        var allColors = new List<Color32>();
        var allTriangles = new List<List<int>>();
        var materials = new List<Material>();

        for (int li = 0; li < layerKeys.Length; li++)
        {
            var layer = layers[li];
            if (layer == null || atlasTextures[li] == null)
            {
                allTriangles.Add(new List<int>());
                materials.Add(new Material(shader));
                continue;
            }

            var mat = new Material(shader);
            mat.mainTexture = atlasTextures[li];
            mat.renderQueue = 3000 + li;
            materials.Add(mat);

            var layerTriangles = new List<int>();
            int vertexStart = allVertices.Count;
            for (int vertexIndex = 0; vertexIndex < layer.positions.Length / 2; vertexIndex++)
            {
                int position = vertexIndex * 2;
                int color = vertexIndex * 4;
                allVertices.Add(new Vector3(layer.positions[position], layer.positions[position + 1], 0));
                allUVs.Add(new Vector2(layer.uvs[position], layer.uvs[position + 1]));
                allColors.Add(new Color32((byte)layer.colors[color], (byte)layer.colors[color + 1],
                    (byte)layer.colors[color + 2], (byte)layer.colors[color + 3]));
            }
            foreach (var index in layer.indices) layerTriangles.Add(vertexStart + index);
            allTriangles.Add(layerTriangles);
            Log($"[Map] {layerKeys[li]}: {layer.positions.Length / 2} vertices, {layerTriangles.Count / 3} triangles");
            yield return null;
        }

        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(allVertices);
        mesh.SetUVs(0, allUVs);
        mesh.SetColors(allColors);
        mesh.subMeshCount = allTriangles.Count;
        for (int i = 0; i < allTriangles.Count; i++)
            mesh.SetTriangles(allTriangles[i], i);

        _mapMeshFilter.mesh = mesh;
        _mapMeshRenderer.materials = materials.ToArray();

        // No scaling — element transforms (m31,m32) are already in map world units
        // Map bounds: X [-580.5, 623.5], Y [-440.75, 440.75] (from oh/hdl constants in dump.cs)
        // Camera ortho size 500 shows [-500,500] vertical range — map fits within this
        _mapMeshGO.transform.localScale = new Vector3(1f, -1f, 1f); // Authentic upright coordinate conversion
        _mapMeshGO.transform.position = Vector3.zero;

        // Configure camera for map view (original: MainCameraOrthographicSize = 500)
        // Map center: midpoint of bounds [-580.5, 623.5] x [-440.75, 440.75] = (21.5, 0)
        var cam = GameObject.Find("__WebGLCam")?.GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 500;
            cam.transform.position = new Vector3(21.5f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.cullingMask = -1; // render all layers (was 0 = nothing)
            cam.depth = 0; // default depth (was -100)
            Log($"[Map] Camera: ortho=500, pos=(21.5, 0, -10), culling=all");
        }
        else
        {
            Err("[Map] Camera __WebGLCam not found");
        }

        Log($"[Map] Mesh built: {allVertices.Count} vertices, {allTriangles.Count} submeshes, {materials.Count} materials");

        if (payload.cellData != null && payload.cellData.Length > 0)
        {
            _mapCells = new CellData[payload.cellData.Length];
            int walkable = 0;
            for (int i = 0; i < payload.cellData.Length; i++)
            {
                var ce = payload.cellData[i];
                _mapCells[i] = new CellData
                {
                    cellNumber = (uint)ce.n,
                    mov = ce.m == 1,
                    los = ce.l == 1,
                    floor = ce.f,
                    speed = ce.s,
                    arrow = ce.a
                };
                if (ce.m == 1) walkable++;
            }
            Log($"[Map] {_mapCells.Length} cells loaded ({walkable} walkable)");
            BuildGridOverlay();
            EnsurePlayerEntity();
            MountInGameHud();
        }
    }

    void BuildGridOverlay()
    {
        if (_gridGO != null) Destroy(_gridGO);
        _gridGO = new GameObject("__MapGrid");
        var filter = _gridGO.AddComponent<MeshFilter>();
        var renderer = _gridGO.AddComponent<MeshRenderer>();

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        var mat = new Material(shader);
        mat.renderQueue = 3001; // Between background and sortable
        renderer.material = mat;

        IsometricGrid.EnsureTable();
        var vertices = new List<Vector3>();
        var colors = new List<Color32>();
        var indices = new List<int>();

        Color32 walkableCol = new Color32(255, 255, 255, 30);
        Color32 blockedCol = new Color32(230, 60, 60, 50);

        for (int c = 0; c < 560; c++)
        {
            Vector2 center = IsometricGrid.CellIdToWorldPos(c);
            bool isWalkable = (_mapCells != null && c < _mapCells.Length) ? _mapCells[c].mov : true;
            Color32 col = isWalkable ? walkableCol : blockedCol;

            int vStart = vertices.Count;
            vertices.Add(new Vector3(center.x, center.y + 21.5f, 0));
            vertices.Add(new Vector3(center.x + 43.0f, center.y, 0));
            vertices.Add(new Vector3(center.x, center.y - 21.5f, 0));
            vertices.Add(new Vector3(center.x - 43.0f, center.y, 0));

            for (int k = 0; k < 4; k++) colors.Add(col);

            indices.Add(vStart);     indices.Add(vStart + 1);
            indices.Add(vStart + 1); indices.Add(vStart + 2);
            indices.Add(vStart + 2); indices.Add(vStart + 3);
            indices.Add(vStart + 3); indices.Add(vStart);
        }

        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
        filter.mesh = mesh;
        _gridGO.transform.localScale = new Vector3(1f, -1f, 1f);
        _gridGO.SetActive(_showGrid);

        EnsureHoverIndicator();
    }

    void EnsureHoverIndicator()
    {
        if (_hoverCellGO != null) return;
        _hoverCellGO = new GameObject("__HoverCell");
        _hoverFilter = _hoverCellGO.AddComponent<MeshFilter>();
        _hoverRenderer = _hoverCellGO.AddComponent<MeshRenderer>();

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        var mat = new Material(shader);
        mat.renderQueue = 3004;
        _hoverRenderer.material = mat;

        var mesh = new Mesh();
        var vertices = new Vector3[]
        {
            new Vector3(0, 21.5f, 0),
            new Vector3(43.0f, 0, 0),
            new Vector3(0, -21.5f, 0),
            new Vector3(-43.0f, 0, 0)
        };
        var triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        _hoverFilter.mesh = mesh;
        _hoverCellGO.SetActive(false);
    }

    void UpdateHoverHighlight(int cellId)
    {
        EnsureHoverIndicator();
        if (cellId < 0 || cellId >= 560)
        {
            _hoverCellGO.SetActive(false);
            return;
        }

        Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
        _hoverCellGO.transform.position = new Vector3(pos.x, -pos.y, 0);

        bool isWalkable = (_mapCells != null && cellId < _mapCells.Length) ? _mapCells[cellId].mov : false;
        Color col = isWalkable ? new Color(0.2f, 1f, 0.4f, 0.4f) : new Color(1f, 0.2f, 0.2f, 0.4f);

        var colors = new Color[4];
        for (int i = 0; i < 4; i++) colors[i] = col;
        _hoverFilter.mesh.colors = colors;
        _hoverCellGO.SetActive(true);
    }

    void EnsurePlayerEntity()
    {
        if (_playerEntityGO != null) return;
        _playerEntityGO = new GameObject("__PlayerEntity");
        var anim2d = _playerEntityGO.AddComponent<Jondo.Reconstructed.Animation.Animator2D>();
        var entityAnim = new Jondo.Reconstructed.Animation.EntityAnimator(anim2d);
        var filter = _playerEntityGO.GetComponent<MeshFilter>() ?? _playerEntityGO.AddComponent<MeshFilter>();
        var renderer = _playerEntityGO.GetComponent<MeshRenderer>() ?? _playerEntityGO.AddComponent<MeshRenderer>();

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        var mat = new Material(shader);
        mat.renderQueue = 3005;
        renderer.material = mat;

        // Player official character sprite / skin
        var mesh = new Mesh();
        float halfW = 20f, height = 55f;
        mesh.vertices = new Vector3[]
        {
            new Vector3(-halfW, 0, 0),
            new Vector3(halfW, 0, 0),
            new Vector3(halfW, height, 0),
            new Vector3(-halfW, height, 0)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        var skinTex = Resources.Load<Texture2D>("Characters/skin_10")
            ?? Resources.Load<Texture2D>("Characters/skin_80");
        if (skinTex != null)
        {
            mat.mainTexture = skinTex;
            mat.color = Color.white;
        }
        else
        {
            Color32 actorColor = new Color32(100, 255, 120, 220);
            mesh.colors32 = new Color32[] { actorColor, actorColor, actorColor, actorColor };
        }
        filter.mesh = mesh;

        // Start cell: 280 or first walkable
        if (_playerCellId < 0)
        {
            _playerCellId = 280;
            if (_mapCells != null && _playerCellId < _mapCells.Length && !_mapCells[_playerCellId].mov)
            {
                for (int i = 0; i < _mapCells.Length; i++)
                {
                    if (_mapCells[i].mov) { _playerCellId = i; break; }
                }
            }
        }

        Vector2 startPos = IsometricGrid.CellIdToWorldPos(_playerCellId);
        _playerEntityGO.transform.position = new Vector3(startPos.x, -startPos.y, 0);
        Log($"[Entity] Player spawned on cell {_playerCellId} at ({startPos.x:F1}, {startPos.y:F1})");

        // Load official character skin texture asynchronously
        StartCoroutine(LoadPlayerSkinCoroutine(10));
    }

    IEnumerator LoadPlayerSkinCoroutine(int skinId)
    {
        string baseUrl = Application.absoluteURL;
        int lastSlash = baseUrl.LastIndexOf('/');
        if (lastSlash > 0) baseUrl = baseUrl.Substring(0, lastSlash + 1);
        string skinUrl = $"{baseUrl}assets/Characters/Composed/skin_{skinId}.png";

        using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(skinUrl))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success && _playerEntityGO != null)
            {
                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Bilinear;
                    var renderer = _playerEntityGO.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.material != null)
                    {
                        renderer.material.mainTexture = tex;
                    }

                    var filter = _playerEntityGO.GetComponent<MeshFilter>();
                    if (filter != null)
                    {
                        var charMesh = new Mesh();
                        float halfW = 20f;
                        float height = 55f;
                        charMesh.vertices = new Vector3[]
                        {
                            new Vector3(-halfW, 0, 0),
                            new Vector3(halfW, 0, 0),
                            new Vector3(halfW, height, 0),
                            new Vector3(-halfW, height, 0)
                        };
                        charMesh.uv = new Vector2[]
                        {
                            new Vector2(0f, 0f),
                            new Vector2(1f, 0f),
                            new Vector2(1f, 1f),
                            new Vector2(0f, 1f)
                        };
                        charMesh.colors32 = new Color32[]
                        {
                            new Color32(255, 255, 255, 255),
                            new Color32(255, 255, 255, 255),
                            new Color32(255, 255, 255, 255),
                            new Color32(255, 255, 255, 255)
                        };
                        charMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
                        filter.mesh = charMesh;
                        Log($"[Entity] Player skin {skinId} texture applied ({tex.width}x{tex.height})");
                    }
                }
            }
        }
    }

    // ── World Actors & Chat Handling ──
    Dictionary<long, GameObject> _worldActors = new Dictionary<long, GameObject>();

    // ── NPC Dialog & Interaction System ──
    Dictionary<int, long> _actorCellMap = new Dictionary<int, long>();
    Dictionary<long, string> _actorNames = new Dictionary<long, string>();
    VisualElement _npcDialogRoot;
    long _activeNpcActorId;

    public static byte[] BuildIovNpcActionPayload(int actionId, long mapId, long npcContextualId)
    {
        var bytes = new List<byte>();
        // Field 1: actionId (wireType 0: 0x08)
        bytes.Add(0x08);
        int a = actionId;
        while (a > 0x7F) { bytes.Add((byte)((a & 0x7F) | 0x80)); a >>= 7; }
        bytes.Add((byte)(a & 0x7F));

        // Field 2: mapId (wireType 0: 0x10)
        bytes.Add(0x10);
        long m = mapId;
        while ((ulong)m > 0x7F) { bytes.Add((byte)(((ulong)m & 0x7F) | 0x80)); m = (long)((ulong)m >> 7); }
        bytes.Add((byte)((ulong)m & 0x7F));

        // Field 3: npcContextualId (wireType 0: 0x18)
        bytes.Add(0x18);
        long n = npcContextualId;
        while ((ulong)n > 0x7F) { bytes.Add((byte)(((ulong)n & 0x7F) | 0x80)); n = (long)((ulong)n >> 7); }
        bytes.Add((byte)((ulong)n & 0x7F));

        return bytes.ToArray();
    }

    public static byte[] BuildIoyNpcReplyPayload(int replyId)
    {
        var bytes = new List<byte>();
        bytes.Add(0x08);
        int r = replyId;
        while (r > 0x7F) { bytes.Add((byte)((r & 0x7F) | 0x80)); r >>= 7; }
        bytes.Add((byte)(r & 0x7F));
        return bytes.ToArray();
    }

    public void SendNpcAction(int actionId, long mapId, long npcContextualId)
    {
        if (_wsConn == null) return;
        _activeNpcActorId = npcContextualId;
        var payload = BuildIovNpcActionPayload(actionId, mapId, npcContextualId);
        var frame = WebSocketBridge.BuildProtobufFrame("iov", payload);
        if (frame != null)
        {
            WebSocketBridge.Send(_wsConn, frame);
            Log($"[NPC] Sent iov interaction to actor {npcContextualId} on map {mapId}");
        }
    }

    public void SendNpcReply(int replyId)
    {
        if (_wsConn == null) return;
        var payload = BuildIoyNpcReplyPayload(replyId);
        var frame = WebSocketBridge.BuildProtobufFrame("ioy", payload);
        if (frame != null)
        {
            WebSocketBridge.Send(_wsConn, frame);
            Log($"[NPC] Sent ioy reply {replyId}");
        }
    }

    // ── Inventory & Equipment Storage UI ──
    VisualElement _inventoryRoot;

    // ── Spells & Grimoire System (hms, SpellBaseUi) ──
    public struct SpellEntry
    {
        public int id;
        public int grade;
    }
    List<SpellEntry> _characterSpells = new List<SpellEntry>();
    VisualElement _spellsRoot;

    void UpdateActionBarSpells()
    {
        if (_actionBarRoot == null) return;
        var grid = _actionBarRoot.Q("gd_spellitemotes") ?? _actionBarRoot.Q("_gd_spellitemotes");
        if (grid != null)
        {
            int slotIdx = 0;
            foreach (var sp in _characterSpells)
            {
                var slot = grid.Q($"slot_{slotIdx}") ?? (grid.childCount > slotIdx ? grid[slotIdx] : null);
                if (slot != null)
                {
                    var lbl = slot.Q<Label>() ?? new Label();
                    lbl.text = $"S{sp.id}";
                    lbl.style.fontSize = 10;
                    lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                    if (!slot.Contains(lbl)) slot.Add(lbl);
                }
                slotIdx++;
                if (slotIdx >= 10) break;
            }
        }
    }

    public void ToggleSpells()
    {
        if (_spellsRoot != null) CloseSpells();
        else OpenSpells();
    }

    public void OpenSpells()
    {
        if (_spellsRoot != null) return;
        Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance?.PlayGrimoireSound(true);
        VisualElement root = _uiRoot;
        if (root == null)
        {
            var uidoc = FindFirstObjectByType<UIDocument>() ?? GetComponent<UIDocument>();
            if (uidoc != null) root = uidoc.rootVisualElement;
        }
        if (root == null) return;

        VisualTreeAsset spAsset = null;
        if (_loadedBundles != null)
        {
            foreach (var kv in _loadedBundles)
            {
                if (kv.Value == null) continue;
                spAsset = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/grimoire/spellbase.uxml")
                    ?? kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/grimoire/spellbase.uxml.asset");
                if (spAsset != null) break;
            }
        }

        if (spAsset != null)
        {
            _spellsRoot = spAsset.CloneTree();
            _spellsRoot.name = "SpellBase_Root";
            _spellsRoot.style.position = Position.Absolute;
            _spellsRoot.style.left = Length.Percent(20);
            _spellsRoot.style.top = Length.Percent(15);
            _spellsRoot.style.width = Length.Percent(60);
            _spellsRoot.style.height = Length.Percent(70);
            root.Add(_spellsRoot);
            ApplyGeneratedMethods("SpellBaseUi", _spellsRoot);

            var btnClose = _spellsRoot.Q("btn_close");
            if (btnClose != null)
            {
                btnClose.RegisterCallback<ClickEvent>(evt => CloseSpells());
            }
            Log("[Grimoire] SpellBaseUi mounted on UI");
        }
        else
        {
            Log("[Grimoire] spellBase.uxml not found in loaded bundles");
        }
    }

    public void CloseSpells()
    {
        if (_spellsRoot != null)
        {
            Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance?.PlayGrimoireSound(false);
            _spellsRoot.RemoveFromHierarchy();
            _spellsRoot = null;
            Log("[Grimoire] SpellBaseUi closed");
        }
    }

    public void ToggleInventory()
    {
        if (_inventoryRoot != null)
        {
            CloseInventory();
        }
        else
        {
            OpenInventory();
        }
    }

    public void OpenInventory()
    {
        if (_inventoryRoot != null) return;
        Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance?.PlayInventorySound(true);
        VisualElement root = _uiRoot;
        if (root == null)
        {
            var uidoc = FindFirstObjectByType<UIDocument>() ?? GetComponent<UIDocument>();
            if (uidoc != null) root = uidoc.rootVisualElement;
        }
        if (root == null) return;

        VisualTreeAsset invAsset = null;
        if (_loadedBundles != null)
        {
            foreach (var kv in _loadedBundles)
            {
                if (kv.Value == null) continue;
                invAsset = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/inventory/equipmentstorageui.uxml")
                    ?? kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/inventory/equipmentstorageui.uxml.asset");
                if (invAsset != null) break;
            }
        }

        if (invAsset != null)
        {
            _inventoryRoot = invAsset.CloneTree();
            _inventoryRoot.name = "EquipmentStorage_Root";
            _inventoryRoot.style.position = Position.Absolute;
            _inventoryRoot.style.left = Length.Percent(20);
            _inventoryRoot.style.top = Length.Percent(15);
            _inventoryRoot.style.width = Length.Percent(60);
            _inventoryRoot.style.height = Length.Percent(70);
            root.Add(_inventoryRoot);
            ApplyGeneratedMethods("EquipmentStorageUI", _inventoryRoot);

            var btnClose = _inventoryRoot.Q("btn_close");
            if (btnClose != null)
            {
                btnClose.RegisterCallback<ClickEvent>(evt => CloseInventory());
            }
            Log("[Inventory] EquipmentStorageUI mounted on UI");
        }
        else
        {
            Log("[Inventory] equipmentStorageUi.uxml not found in loaded bundles");
        }
    }

    public void CloseInventory()
    {
        if (_inventoryRoot != null)
        {
            Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance?.PlayInventorySound(false);
            _inventoryRoot.RemoveFromHierarchy();
            _inventoryRoot = null;
            Log("[Inventory] EquipmentStorageUI closed");
        }
    }

    void OpenNpcDialog()
    {
        if (_npcDialogRoot != null) return;
        Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance?.PlayNpcSound(true);
        VisualElement root = _uiRoot;
        if (root == null)
        {
            var uidoc = FindFirstObjectByType<UIDocument>() ?? GetComponent<UIDocument>();
            if (uidoc != null) root = uidoc.rootVisualElement;
        }
        if (root == null) return;

        VisualTreeAsset npcAsset = null;
        if (_loadedBundles != null)
        {
            foreach (var kv in _loadedBundles)
            {
                if (kv.Value == null) continue;
                npcAsset = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/npcdialog/npcdialog.uxml")
                    ?? kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/npcdialog/npcdialog.uxml.asset");
                if (npcAsset != null) break;
            }
        }

        if (npcAsset != null)
        {
            _npcDialogRoot = npcAsset.CloneTree();
            _npcDialogRoot.name = "NpcDialog_Root";
            _npcDialogRoot.style.position = Position.Absolute;
            _npcDialogRoot.style.left = Length.Percent(25);
            _npcDialogRoot.style.top = Length.Percent(20);
            _npcDialogRoot.style.width = Length.Percent(50);
            root.Add(_npcDialogRoot);
            ApplyGeneratedMethods("NpcDialog", _npcDialogRoot);

            var btnClose = _npcDialogRoot.Q("btn_close");
            if (btnClose != null)
            {
                btnClose.RegisterCallback<ClickEvent>(evt => CloseNpcDialog());
            }

            if (_actorNames.TryGetValue(_activeNpcActorId, out var npcName))
            {
                var lblName = _npcDialogRoot.Q<Label>("lbl_npcName");
                if (lblName != null) lblName.text = npcName;
            }
            Log("[NPC] NpcDialog mounted on UI");
        }
        else
        {
            Log("[NPC] npcDialog.uxml asset not found in loaded bundles");
        }
    }

    void CloseNpcDialog()
    {
        if (_npcDialogRoot != null)
        {
            Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance?.PlayNpcSound(false);
            _npcDialogRoot.RemoveFromHierarchy();
            _npcDialogRoot = null;
            Log("[NPC] NpcDialog closed");
        }
    }

    void HandleNpcQuestion(byte[] payload)
    {
        if (payload == null) return;
        OpenNpcDialog();
        if (_npcDialogRoot == null) return;

        int questionId = PbVarField(payload, 1);
        var replies = PbRepeated(payload, 2);

        var lblText = _npcDialogRoot.Q<Label>("lbl_text");
        if (lblText != null)
        {
            lblText.text = $"NPC Question #{questionId}";
        }

        var scroll = _npcDialogRoot.Q<ScrollView>("AnswersScroll") ?? _npcDialogRoot.Q<ScrollView>("_AnswersScroll");
        if (scroll != null)
        {
            scroll.Clear();
            int replyIdx = 1;
            foreach (var rBytes in replies)
            {
                int rId = PbVarField(rBytes, 1);
                if (rId == 0 && rBytes.Length > 0) rId = (int)rBytes[0];
                var btn = new Button(() =>
                {
                    SendNpcReply(rId);
                });
                btn.text = $"• Resposta {replyIdx} (ID: {rId})";
                btn.style.marginTop = 4;
                btn.style.marginBottom = 4;
                btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                scroll.Add(btn);
                replyIdx++;
            }
        }
    }

    void ClearWorldActors()
    {
        foreach (var kv in _worldActors)
        {
            if (kv.Value != null) Destroy(kv.Value);
        }
        _worldActors.Clear();
        _actorCellMap.Clear();
        _actorNames.Clear();
        CloseNpcDialog();
    }

    void ProcessActorBlock(byte[] actorBytes)
    {
        if (actorBytes == null || actorBytes.Length == 0) return;
        var f1 = PbField(actorBytes, 1);
        int cellId = f1 != null ? PbVarField(f1, 1) : -1;
        int facing = f1 != null ? PbVarField(f1, 2) : 1;
        long actorId = PbVarFieldLong(actorBytes, 3);

        string name = "Actor";
        var f2 = PbField(actorBytes, 2);
        if (f2 != null)
        {
            var f2f1 = PbField(f2, 1);
            if (f2f1 != null)
            {
                var humanoid = PbField(f2f1, 5);
                if (humanoid != null) name = PbStr(humanoid, 1) ?? name;
            }
        }

        Log($"  [Actor] id={actorId} name={name} cell={cellId} facing={facing}");
        _actorNames[actorId] = name;
        if (cellId >= 0 && cellId < 560)
        {
            _actorCellMap[cellId] = actorId;
        }

        // Local player update
        int myCharId = -1;
        if (_selectedCharIdx >= 0 && _characterList != null && _selectedCharIdx < _characterList.Count)
            myCharId = _characterList[_selectedCharIdx].id;

        if (actorId == myCharId && cellId >= 0 && cellId < 560)
        {
            _playerCellId = cellId;
            if (_playerEntityGO != null)
            {
                Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
                _playerEntityGO.transform.position = new Vector3(pos.x, -pos.y, 0);
            }
        }
        else if (cellId >= 0 && cellId < 560)
        {
            SpawnOrUpdateWorldActor(actorId, name, cellId, facing);
        }
    }

    void SpawnOrUpdateWorldActor(long actorId, string name, int cellId, int facing)
    {
        if (!_worldActors.TryGetValue(actorId, out var actorGO) || actorGO == null)
        {
            actorGO = new GameObject($"__Actor_{actorId}_{name}");
            var anim2d = actorGO.AddComponent<Jondo.Reconstructed.Animation.Animator2D>();
            var entityAnim = new Jondo.Reconstructed.Animation.EntityAnimator(anim2d);
            var filter = actorGO.GetComponent<MeshFilter>() ?? actorGO.AddComponent<MeshFilter>();
            var renderer = actorGO.GetComponent<MeshRenderer>() ?? actorGO.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(shader);
            mat.renderQueue = 3004;
            renderer.material = mat;

            var mesh = new Mesh();
            float halfW = 20f, height = 55f;
            mesh.vertices = new Vector3[]
            {
                new Vector3(-halfW, 0, 0),
                new Vector3(halfW, 0, 0),
                new Vector3(halfW, height, 0),
                new Vector3(-halfW, height, 0)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };

            if (actorId > 0)
            {
                var skinTex = Resources.Load<Texture2D>("Characters/skin_10")
                    ?? Resources.Load<Texture2D>("Characters/skin_80");
                if (skinTex != null)
                {
                    mat.mainTexture = skinTex;
                    mat.color = Color.white;
                }
                else
                {
                    Color32 actorColor = new Color32(100, 255, 120, 220);
                    mesh.colors32 = new Color32[] { actorColor, actorColor, actorColor, actorColor };
                }
            }
            else
            {
                Color32 actorColor = new Color32(255, 100, 100, 220);
                mesh.colors32 = new Color32[] { actorColor, actorColor, actorColor, actorColor };
            }
            filter.mesh = mesh;

            _worldActors[actorId] = actorGO;
        }

        Vector2 pos = IsometricGrid.CellIdToWorldPos(cellId);
        actorGO.transform.position = new Vector3(pos.x, -pos.y, 0);
        float sign = (facing == 3 || facing == 4 || facing == 5) ? -1f : 1f;
        actorGO.transform.localScale = new Vector3(sign, 1f, 1f);
    }

    public static byte[] BuildKtmChatPayload(string text, int channel = 0)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var bytesList = new List<byte>();

        // Field 2: text (wireType 2: 0x12)
        var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
        bytesList.Add(0x12);
        int tlen = textBytes.Length;
        while (tlen > 0x7F) { bytesList.Add((byte)((tlen & 0x7F) | 0x80)); tlen >>= 7; }
        bytesList.Add((byte)(tlen & 0x7F));
        bytesList.AddRange(textBytes);

        // Field 3: channel (wireType 0: 0x18)
        if (channel > 0)
        {
            bytesList.Add(0x18);
            int ch = channel;
            while (ch > 0x7F) { bytesList.Add((byte)((ch & 0x7F) | 0x80)); ch >>= 7; }
            bytesList.Add((byte)(ch & 0x7F));
        }

        return bytesList.ToArray();
    }

    public void SendChatMessage(string text, int channel = 0)
    {
        if (string.IsNullOrEmpty(text) || _wsConn == null) return;
        var ktmPayload = BuildKtmChatPayload(text, channel);
        if (ktmPayload != null)
        {
            var ktmFrame = WebSocketBridge.BuildProtobufFrame("ktm", ktmPayload);
            if (ktmFrame != null)
            {
                WebSocketBridge.Send(_wsConn, ktmFrame);
                Log($"[Chat] Sent message: {text}");
            }
        }
    }

    void AppendChatMessage(string speaker, string message, int channel, string timeStr = "")
    {
        if (_chatRoot == null) return;
        var listChat = _chatRoot.Q("listChat");
        if (listChat != null)
        {
            var msgLabel = new Label();
            string color = channel == 0 ? "#ffffff" : channel == 1 ? "#ffff00" : channel == 2 ? "#ff8800" : "#aaccff";
            msgLabel.text = $"<color={color}>[{speaker}] {message}</color>";
            msgLabel.style.fontSize = 12;
            msgLabel.style.whiteSpace = WhiteSpace.Normal;
            listChat.Add(msgLabel);
            if (listChat is ScrollView sv)
            {
                sv.scrollOffset = new Vector2(0, sv.contentContainer.layout.height);
            }
        }
        Gen_ChatView_OnNewChatMessage(_chatRoot);
    }

    public static byte[] BuildJrwMovementPayload(long mapId, List<int> path)
    {
        if (path == null || path.Count < 2) return null;
        var bytesList = new List<byte>();

        // Field 1: mapId (varint, tag = (1 << 3) | 0 = 0x08)
        bytesList.Add(0x08);
        long m = mapId;
        while (m > 0x7F) { bytesList.Add((byte)((m & 0x7F) | 0x80)); m >>= 7; }
        bytesList.Add((byte)(m & 0x7F));

        // Field 2: steps (packed varints, tag = (2 << 3) | 2 = 0x12)
        var stepsBytes = new List<byte>();
        for (int i = 1; i < path.Count; i++)
        {
            int prevCell = path[i - 1];
            int currCell = path[i];
            int facing = IsometricGrid.GetDirection(prevCell, currCell);
            long step = ((long)facing << 12) | (long)(currCell & 0xFFF);

            long s = step;
            while (s > 0x7F) { stepsBytes.Add((byte)((s & 0x7F) | 0x80)); s >>= 7; }
            stepsBytes.Add((byte)(s & 0x7F));
        }

        bytesList.Add(0x12);
        int slen = stepsBytes.Count;
        while (slen > 0x7F) { bytesList.Add((byte)((slen & 0x7F) | 0x80)); slen >>= 7; }
        bytesList.Add((byte)(slen & 0x7F));
        bytesList.AddRange(stepsBytes);

        return bytesList.ToArray();
    }

    public static byte[] BuildJruMapChangePayload(long currentMapId, int cellId)
    {
        var bytes = new List<byte>();
        // Field 2: mapId (wireType 0: 0x10)
        bytes.Add(0x10);
        long m = currentMapId;
        while ((ulong)m > 0x7F) { bytes.Add((byte)(((ulong)m & 0x7F) | 0x80)); m = (long)((ulong)m >> 7); }
        bytes.Add((byte)((ulong)m & 0x7F));

        // Field 3: cellId (wireType 0: 0x18)
        bytes.Add(0x18);
        int c = cellId;
        while (c > 0x7F) { bytes.Add((byte)((c & 0x7F) | 0x80)); c >>= 7; }
        bytes.Add((byte)(c & 0x7F));

        return bytes.ToArray();
    }

    public void RequestMapTransition(int cellId)
    {
        if (_wsConn == null || _currentMapId <= 0) return;
        var payload = BuildJruMapChangePayload(_currentMapId, cellId);
        var frame = WebSocketBridge.BuildProtobufFrame("jru", payload);
        if (frame != null)
        {
            WebSocketBridge.Send(_wsConn, frame);
            Log($"[MapTransition] Requested transition via cell {cellId} on map {_currentMapId}");
        }
    }

    IEnumerator WalkPathCoroutine(List<int> path)
    {
        _isWalking = true;
        if (_wsConn != null && _currentMapId > 0)
        {
            var jrwPayload = BuildJrwMovementPayload(_currentMapId, path);
            if (jrwPayload != null)
            {
                var jrwFrame = WebSocketBridge.BuildProtobufFrame("jrw", jrwPayload);
                if (jrwFrame != null)
                {
                    WebSocketBridge.Send(_wsConn, jrwFrame);
                    Log($"[Movement] Sent jrw packet for {path.Count - 1} steps on map {_currentMapId}");
                }
            }
        }
        for (int i = 1; i < path.Count; i++)
        {
            int nextCell = path[i];
            Vector2 posStart = IsometricGrid.CellIdToWorldPos(_playerCellId);
            Vector2 posTarget = IsometricGrid.CellIdToWorldPos(nextCell);
            Vector3 startPos = new Vector3(posStart.x, -posStart.y, 0);
            Vector3 targetPos = new Vector3(posTarget.x, -posTarget.y, 0);

            // Flip character horizontally based on movement direction
            if (_playerEntityGO != null && targetPos.x != startPos.x)
            {
                float sign = targetPos.x < startPos.x ? -1f : 1f;
                _playerEntityGO.transform.localScale = new Vector3(sign, 1f, 1f);
            }

            float elapsed = 0f;
            float duration = 0.15f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (_playerEntityGO != null)
                    _playerEntityGO.transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
            _playerCellId = nextCell;
            Jondo.Reconstructed.Audio.ReconstructedAudioService.Instance?.PlayFootstep(nextCell);
            if (_playerEntityGO != null)
                _playerEntityGO.transform.position = targetPos;
        }
        _isWalking = false;
        Log($"[Movement] Arrived at cell {_playerCellId}");
        if (IsometricGrid.IsBorderCell(_playerCellId))
        {
            Log($"[MapTransition] Arrived at border cell {_playerCellId}, requesting map transition (jru)...");
            RequestMapTransition(_playerCellId);
        }
    }

    bool _inGameHudMounted = false;
    VisualElement _bannerRoot;
    VisualElement _chatRoot;
    VisualElement _characterInfoRoot;
    VisualElement _actionBarRoot;

    void MountInGameHud(VisualElement root = null)
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
        }

        if (_inGameHudMounted) return;
        _inGameHudMounted = true;
        Log("[HUD] Mounting in-game HUD (BannerMenu, ChatView, CharacterInformation, ActionBar)...");

        var colorMap = LoadThemeColorMap();
        var fontAsset = Resources.Load<FontAsset>("DefaultFont");

        // 1. BannerMenu (bottom persistent bar)
        try
        {
            VisualTreeAsset bannerVta = LoadUxml("bannermenu") ?? LoadUxml("Assets/UI/PersistentMenu/bannerMenu.uxml");
            if (bannerVta != null)
            {
                _bannerRoot = bannerVta.CloneTree();
                _bannerMenuBinding = new GeneratedBannerMenuBinding();
                _bannerMenuBinding.Bind(_bannerRoot);
                _bannerRoot.name = "BannerMenu_Root";
                _bannerRoot.style.position = Position.Absolute;
                _bannerRoot.style.bottom = 0;
                _bannerRoot.style.left = 0;
                _bannerRoot.style.right = 0;
                _bannerRoot.style.height = 70;
                _bannerRoot.style.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 0.95f);
                _bannerRoot.style.borderTopColor = new Color(0.95f, 0.77f, 0.25f, 0.4f);
                _bannerRoot.style.borderTopWidth = 1;
                _bannerRoot.pickingMode = PickingMode.Position;
                int tc = 0;
                ApplyThemeRecursive(_bannerRoot, colorMap, ref tc);
                if (fontAsset != null) ApplyFontRecursive(_bannerRoot, fontAsset);
                targetRoot.Add(_bannerRoot);
                ApplyGeneratedMethods("BannerMenu", _bannerRoot);
                Log("  [HUD] BannerMenu mounted and initialized");
            }
        }
        catch (Exception ex)
        {
            Err($"  [HUD] BannerMenu mount error: {ex.Message}");
        }

        // 2. ChatView (chat window)
        try
        {
            VisualTreeAsset chatVta = LoadUxml("chatview") ?? LoadUxml("Assets/UI/PersistentMenu/chatview.uxml");
            if (chatVta != null)
            {
                _chatRoot = chatVta.CloneTree();
                _chatviewBinding = new GeneratedChatviewBinding();
                _chatviewBinding.Bind(_chatRoot);
                _chatRoot.name = "ChatView_Root";
                _chatRoot.style.position = Position.Absolute;
                _chatRoot.style.bottom = 85;
                _chatRoot.style.left = 20;
                _chatRoot.style.width = 440;
                _chatRoot.style.height = 250;
                _chatRoot.style.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 0.88f);
                _chatRoot.style.borderLeftColor = new Color(0.2f, 0.24f, 0.28f, 0.8f);
                _chatRoot.style.borderRightColor = new Color(0.2f, 0.24f, 0.28f, 0.8f);
                _chatRoot.style.borderTopColor = new Color(0.2f, 0.24f, 0.28f, 0.8f);
                _chatRoot.style.borderBottomColor = new Color(0.2f, 0.24f, 0.28f, 0.8f);
                _chatRoot.style.borderLeftWidth = 1;
                _chatRoot.style.borderRightWidth = 1;
                _chatRoot.style.borderTopWidth = 1;
                _chatRoot.style.borderBottomWidth = 1;
                _chatRoot.style.borderTopLeftRadius = 8;
                _chatRoot.style.borderTopRightRadius = 8;
                _chatRoot.style.borderBottomLeftRadius = 8;
                _chatRoot.style.borderBottomRightRadius = 8;
                _chatRoot.pickingMode = PickingMode.Position;
                int tc = 0;
                ApplyThemeRecursive(_chatRoot, colorMap, ref tc);
                if (fontAsset != null) ApplyFontRecursive(_chatRoot, fontAsset);
                targetRoot.Add(_chatRoot);
                ApplyGeneratedMethods("ChatView", _chatRoot);

                var chatInput = _chatRoot.Q<TextField>("ti_chatInput");
                if (chatInput != null)
                {
                    chatInput.RegisterCallback<KeyDownEvent>(evt =>
                    {
                        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                        {
                            string t = chatInput.value?.Trim();
                            if (!string.IsNullOrEmpty(t))
                            {
                                SendChatMessage(t);
                                chatInput.value = "";
                            }
                        }
                    });
                }
                Log("  [HUD] ChatView mounted with live input");
            }
        }
        catch (Exception ex)
        {
            Err($"  [HUD] ChatView mount error: {ex.Message}");
        }

        // 3. CharacterInformation (HP, AP, MP gauge)
        try
        {
            VisualTreeAsset charInfoVta = LoadUxml("characterinformations") ?? LoadUxml("Assets/UI/PersistentMenu/characterInformations.uxml");
            if (charInfoVta != null)
            {
                _characterInfoRoot = charInfoVta.CloneTree();
                _charInfoBinding = new GeneratedCharacterInformationsBinding();
                _charInfoBinding.Bind(_characterInfoRoot);
                _characterInfoRoot.name = "CharacterInformation_Root";
                _characterInfoRoot.style.position = Position.Absolute;
                _characterInfoRoot.style.bottom = 12;
                _characterInfoRoot.style.left = Length.Percent(44);
                _characterInfoRoot.style.width = 240;
                _characterInfoRoot.style.height = 80;
                _characterInfoRoot.pickingMode = PickingMode.Position;
                int tc = 0;
                ApplyThemeRecursive(_characterInfoRoot, colorMap, ref tc);
                if (fontAsset != null) ApplyFontRecursive(_characterInfoRoot, fontAsset);
                targetRoot.Add(_characterInfoRoot);
                ApplyGeneratedMethods("CharacterInformation", _characterInfoRoot);
                UpdateCharacterInfoStats(55, 55, 6, 3, 0, 1);
                Log("  [HUD] CharacterInformation mounted");
            }
        }
        catch (Exception ex)
        {
            Err($"  [HUD] CharacterInformation mount error: {ex.Message}");
        }

        // 4. ActionBar (spells & items slot bar)
        try
        {
            VisualTreeAsset barVta = LoadUxml("actionbar") ?? LoadUxml("Assets/UI/PersistentMenu/actionBar.uxml");
            if (barVta != null)
            {
                _actionBarRoot = barVta.CloneTree();
                _actionBarBinding = new GeneratedActionBarBinding();
                _actionBarBinding.Bind(_actionBarRoot);
                _actionBarRoot.name = "ActionBar_Root";
                _actionBarRoot.style.position = Position.Absolute;
                _actionBarRoot.style.bottom = 75;
                _actionBarRoot.style.left = Length.Percent(25);
                _actionBarRoot.style.right = Length.Percent(25);
                _actionBarRoot.style.height = 62;
                _actionBarRoot.style.backgroundColor = new Color(0.08f, 0.1f, 0.13f, 0.9f);
                _actionBarRoot.style.borderTopLeftRadius = 8;
                _actionBarRoot.style.borderTopRightRadius = 8;
                _actionBarRoot.style.borderBottomLeftRadius = 8;
                _actionBarRoot.style.borderBottomRightRadius = 8;
                _actionBarRoot.style.borderLeftColor = new Color(0.2f, 0.25f, 0.3f, 0.6f);
                _actionBarRoot.style.borderRightColor = new Color(0.2f, 0.25f, 0.3f, 0.6f);
                _actionBarRoot.style.borderTopColor = new Color(0.2f, 0.25f, 0.3f, 0.6f);
                _actionBarRoot.style.borderBottomColor = new Color(0.2f, 0.25f, 0.3f, 0.6f);
                _actionBarRoot.style.borderLeftWidth = 1;
                _actionBarRoot.style.borderRightWidth = 1;
                _actionBarRoot.style.borderTopWidth = 1;
                _actionBarRoot.style.borderBottomWidth = 1;
                _actionBarRoot.pickingMode = PickingMode.Position;
                int tc = 0;
                ApplyThemeRecursive(_actionBarRoot, colorMap, ref tc);
                if (fontAsset != null) ApplyFontRecursive(_actionBarRoot, fontAsset);
                targetRoot.Add(_actionBarRoot);
                ApplyGeneratedMethods("ActionBar", _actionBarRoot);
                Log("  [HUD] ActionBar mounted");
            }
        }
        catch (Exception ex)
        {
            Err($"  [HUD] ActionBar mount error: {ex.Message}");
        }
    }

    public void UpdateCharacterInfoStats(int currentHp, int maxHp, int currentAp, int currentMp, int shield = 0, int invocations = 1)
    {
        if (_characterInfoRoot == null) return;
        if (_charInfoBinding == null)
        {
            _charInfoBinding = new GeneratedCharacterInformationsBinding();
            _charInfoBinding.Bind(_characterInfoRoot);
        }

        var lblPdv = _charInfoBinding?.lbl_pdv ?? _characterInfoRoot.Q<Label>("lbl_pdv");
        if (lblPdv != null) lblPdv.text = $"{currentHp} / {maxHp}";
        var lblPdvBottom = _charInfoBinding?.lbl_pdvBottom ?? _characterInfoRoot.Q<Label>("lbl_pdvBottom");
        if (lblPdvBottom != null) lblPdvBottom.text = $"{currentHp}";
        var lblPa = _charInfoBinding?.lbl_pa ?? _characterInfoRoot.Q<Label>("lbl_pa");
        if (lblPa != null) lblPa.text = $"{currentAp}";
        var lblPm = _charInfoBinding?.lbl_pm ?? _characterInfoRoot.Q<Label>("lbl_pm");
        if (lblPm != null) lblPm.text = $"{currentMp}";
        var lblShield = _charInfoBinding?.lbl_shield ?? _characterInfoRoot.Q<Label>("lbl_shield");
        if (lblShield != null) lblShield.text = shield > 0 ? $"{shield}" : "";
        var lblIp = _charInfoBinding?.lbl_ip ?? _characterInfoRoot.Q<Label>("lbl_ip");
        if (lblIp != null) lblIp.text = $"{invocations}";
    }

    Texture2D MakeReadable(Texture2D src)
    {
        if (src.isReadable) return src;
        // Copy via RenderTexture (same technique as RebuildBundlesForWebGL)
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }

    void ParseMapJson(string json, long mapId)
    {
        // Minimal JSON parser for map data extracted by extract_map_data.py
        // Format: { cells: [{n,m,l,f,s,a}], backgroundElements: [{g,c,t:[6],mi}], ... }
        Log($"[Map] Parsing JSON for map {mapId}...");

        // Parse cells
        var cellsJson = JsonArraySlice(json, "\"cells\":");
        if (cellsJson != null)
        {
            var cellList = new List<CellData>();
            int pos = 0;
            while (pos < cellsJson.Length)
            {
                int objStart = cellsJson.IndexOf('{', pos);
                if (objStart < 0) break;
                int objEnd = cellsJson.IndexOf('}', objStart);
                if (objEnd < 0) break;
                string obj = cellsJson.Substring(objStart, objEnd - objStart + 1);
                cellList.Add(new CellData
                {
                    cellNumber = (uint)JsonInt(obj, "\"n\":"),
                    mov = JsonInt(obj, "\"m\":") == 1,
                    los = JsonInt(obj, "\"l\":") == 1,
                    floor = JsonInt(obj, "\"f\":"),
                    speed = JsonInt(obj, "\"s\":"),
                    arrow = JsonInt(obj, "\"a\":"),
                });
                pos = objEnd + 1;
            }
            _mapCells = cellList.ToArray();
            int walkable = 0;
            foreach (var c in _mapCells) if (c.mov) walkable++;
            Log($"[Map] {_mapCells.Length} cells ({walkable} walkable)");
        }

        // Parse elements for each layer
        _bgElements = ParseJsonElements(json, "\"backgroundElements\":");
        _sortElements = ParseJsonElements(json, "\"sortableElements\":");
        _fgElements = ParseJsonElements(json, "\"foregroundElements\":");
        Log($"[Map] Elements: bg={_bgElements?.Length ?? 0} sort={_sortElements?.Length ?? 0} fg={_fgElements?.Length ?? 0}");

        Log($"[Map] Map {mapId} parsed successfully");
    }

    MapElement[] ParseJsonElements(string json, string key)
    {
        var arr = JsonArraySlice(json, key);
        if (arr == null) return null;

        var list = new List<MapElement>();
        int pos = 0;
        while (pos < arr.Length)
        {
            int objStart = arr.IndexOf('{', pos);
            if (objStart < 0) break;
            int objEnd = FindMatchingBrace(arr, objStart);
            if (objEnd < 0) break;
            string obj = arr.Substring(objStart, objEnd - objStart + 1);

            var el = new MapElement();
            el.gfxId = JsonInt(obj, "\"g\":");
            el.color = (uint)JsonLong(obj, "\"c\":");
            el.materialIndex = JsonInt(obj, "\"mi\":");

            // Transform array: [m11,m12,m21,m22,m31,m32]
            int tStart = obj.IndexOf("\"t\":[");
            if (tStart >= 0)
            {
                tStart += 4;
                int tEnd = obj.IndexOf(']', tStart);
                if (tEnd > tStart)
                {
                    string[] parts = obj.Substring(tStart + 1, tEnd - tStart - 1).Split(',');
                    if (parts.Length >= 6)
                    {
                        float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out el.m11);
                        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out el.m12);
                        float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out el.m21);
                        float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out el.m22);
                        float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out el.m31);
                        float.TryParse(parts[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out el.m32);
                    }
                }
            }
            list.Add(el);
            pos = objEnd + 1;
        }
        return list.ToArray();
    }

    static string JsonArraySlice(string json, string key)
    {
        int idx = json.IndexOf(key);
        if (idx < 0) return null;
        int start = json.IndexOf('[', idx + key.Length);
        if (start < 0) return null;
        int depth = 1, end = start + 1;
        while (end < json.Length && depth > 0)
        {
            if (json[end] == '[') depth++;
            else if (json[end] == ']') depth--;
            end++;
        }
        return json.Substring(start, end - start);
    }

    static int JsonInt(string obj, string key)
    {
        int idx = obj.IndexOf(key);
        if (idx < 0) return 0;
        idx += key.Length;
        int end = idx;
        while (end < obj.Length && (char.IsDigit(obj[end]) || obj[end] == '-')) end++;
        int.TryParse(obj.Substring(idx, end - idx), out int val);
        return val;
    }

    static long JsonLong(string obj, string key)
    {
        int idx = obj.IndexOf(key);
        if (idx < 0) return 0;
        idx += key.Length;
        int end = idx;
        while (end < obj.Length && (char.IsDigit(obj[end]) || obj[end] == '-')) end++;
        long.TryParse(obj.Substring(idx, end - idx), out long val);
        return val;
    }

    // ── Screen navigation — UXML paths from ClientData.json assetPaths ──
    string _currentScreen = "Login";

    // Screen→UXML resolution from RuntimeData.json (extracted from ScreenRegistry)
    static Dictionary<string, string> _screenUxmlMap;

    string FindUxmlPath(string screenName)
    {
        // Priority 1: RuntimeData.json screen→uxml map (from extracted ScreenRegistry)
        if (_screenUxmlMap != null && _screenUxmlMap.TryGetValue(screenName, out var exact))
            return exact;

        // Fallback: search assetPaths by filename
        if (_assetPaths == null) return null;
        string lower = screenName.ToLower();
        foreach (var kv in new List<KeyValuePair<string, string>>(_assetPaths))
        {
            string val = kv.Value;
            if (!val.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase)) continue;
            string fileName = val.Substring(val.LastIndexOf('/') + 1).Replace(".uxml", "").ToLower();
            if (fileName == lower) return val;
        }
        return null;
    }

    void ShowScreen(string screenName, VisualElement root)
    {
        var uxmlPath = FindUxmlPath(screenName);
        var vta = LoadUxml(uxmlPath) ?? LoadUxml(screenName);
        if (vta == null)
        {
            Log($"  {screenName}: UXML '{uxmlPath ?? screenName}' not found in bundles");
            _currentScreen = screenName;
            return;
        }
        try
        {
            // Clean up previous screens except connection background
            for (int ci = root.childCount - 1; ci >= 0; ci--)
            {
                var child = root[ci];
                if (child.name == "connectionBackground" || child.name == "img" || child.name == "CharacterHeader_Overlay") continue;
                if (child.name != null && child.name.StartsWith("screen_"))
                {
                    root.RemoveAt(ci);
                    continue;
                }
                child.style.display = DisplayStyle.None;
            }
            var loginEl = root.Q("login");
            if (loginEl != null) loginEl.style.display = DisplayStyle.None;

            var screenUI = vta.CloneTree();
            screenUI.name = "screen_" + screenName;
            screenUI.style.width = Length.Percent(100);
            screenUI.style.height = Length.Percent(100);
            screenUI.style.position = Position.Absolute;
            screenUI.style.left = 0;
            screenUI.style.top = 0;
            screenUI.style.right = 0;
            screenUI.style.bottom = 0;
            root.Add(screenUI);

            var colorMap = LoadThemeColorMap();
            int themeApplied = 0;
            ApplyThemeRecursive(screenUI, colorMap, ref themeApplied);

            var fontAsset = Resources.Load<FontAsset>("DefaultFont");
            if (fontAsset != null)
            {
                var standaloneAtlas = Resources.Load<Texture2D>("DefaultFontAtlas");
                if (standaloneAtlas != null && fontAsset.atlasTexture != null && fontAsset.atlasTexture.width <= 1)
                {
                    fontAsset.atlasTextures = new[] { standaloneAtlas };
                    var mat = Resources.Load<Material>("DefaultFontMaterial");
                    if (mat != null) { mat.mainTexture = standaloneAtlas; fontAsset.material = mat; }
                }
                int fontFixed = ApplyFontRecursive(screenUI, fontAsset);
                Log($"  Screen '{screenName}' font applied to {fontFixed} elements");
            }

            ApplyScreenBehavior(screenName, screenUI);
            DispatchGen_Ctor(screenName, screenUI);
            DispatchGen_Localize(screenName, screenUI);
            DispatchGen_OnEnable(screenName, screenUI);

            _currentScreen = screenName;
            Log($"  Screen '{screenName}' loaded: {screenUI.childCount} children, theme={themeApplied}, uxml={uxmlPath}");
        }
        catch (System.Exception e)
        {
            Err($"  Screen '{screenName}' error: {e.Message}");
        }
    }

    static string _currentTheme = "Standard";

    Dictionary<string, Color> LoadThemeColorMap()
    {
        // Try theme-specific file first (from DofusUISettings.json: 11 themes extracted via UnityPy)
        // Falls back to ThemeColors.json (Standard)
        var json = _currentTheme != "Standard"
            ? Resources.Load<TextAsset>($"ThemeColors_{_currentTheme}") ?? Resources.Load<TextAsset>("ThemeColors")
            : Resources.Load<TextAsset>("ThemeColors");
        var map = new Dictionary<string, Color>();
        if (json == null) return map;
        foreach (string line in json.text.Split('\n'))
        {
            string trimmed = line.Trim().TrimEnd(',');
            if (!trimmed.StartsWith("\"")) continue;
            int colon = trimmed.IndexOf(':');
            if (colon < 0) continue;
            string cName = trimmed.Substring(0, colon).Trim().Trim('"');
            if (cName.StartsWith("--")) cName = cName.Substring(2);
            string cVal = trimmed.Substring(colon + 1).Trim().Trim('"');
            if (TryParseColor(cVal, out var col))
                map[cName] = col;
        }
        return map;
    }

    // Theme switching — uses DofusUISettings.json (33 TSS GUIDs: 11 themes x 3 font sizes)
    // + ThemeColors_{theme}.json (25 colors per theme, extracted from FigmaThemeScriptable)
    // SOURCE: DofusUISettings extracted via UnityPy (scan_all_bundles.py)
    static readonly string[] THEME_NAMES = {
        "Standard", "Bonta", "Brakmar", "GoldAndSteel", "Tribute",
        "Belladone", "Unicorn", "EmeraldMine", "Sufokia", "Pandala", "Wabbit"
    };

    void SwitchTheme(string theme)
    {
        if (!System.Array.Exists(THEME_NAMES, t => t == theme)) { Log($"  Unknown theme: {theme}"); return; }
        _currentTheme = theme;
        var colorMap = LoadThemeColorMap();
        if (_uiRoot != null)
        {
            int applied = 0;
            ApplyThemeRecursive(_uiRoot, colorMap, ref applied);
            Log($"  Theme switched to {theme}: {applied} colors applied, {colorMap.Count} in map");
        }
    }

    static string ExtractJsonString(string json, string key)
    {
        var search = $"\"{key}\"";
        int idx = json.IndexOf(search);
        if (idx < 0) return null;
        int colonIdx = json.IndexOf(':', idx + search.Length);
        if (colonIdx < 0) return null;
        int valStart = json.IndexOf('"', colonIdx + 1);
        if (valStart < 0) return null;
        int valEnd = json.IndexOf('"', valStart + 1);
        if (valEnd < 0) return null;
        return json.Substring(valStart + 1, valEnd - valStart - 1);
    }

    // PostProcessLoginTree, FixChildAt, FixElement REMOVED (V31)
    // — were hardcoding element names ("btn_options"→"Options", "btn_quit"→"Quit") with invented text
    // — replaced by DispatchGen_Ctor("PreGameMainMenu") which uses I18n from decompiled data
    // — Gen_PreGameMainMenu_ctor sets: ui.common.options, ui.common.quitGame, ui.header.subscribe, etc.

    void NameUnnamed(VisualElement el, ref int count)
    {
        for (int i = 0; i < el.childCount; i++)
        {
            var ch = el[i];
            if (string.IsNullOrEmpty(ch.name) && ch.GetType().Name == "VisualElement")
            {
                ch.name = $"_anon_{el.name}_{i}";
                count++;
            }
            NameUnnamed(ch, ref count);
        }
    }

    void DiagnoseUSS(VisualElement root)
    {
        // Check how many stylesheets are on the root after loading
        Log($"  USS on root: {root.styleSheets.count}");

        // Check a few StyleSheets for content via reflection
        try
        {
            for (int i = 0; i < Math.Min(root.styleSheets.count, 3); i++)
            {
                var ss = root.styleSheets[i];
                // StyleSheet has internal fields: m_Rules, m_ComplexSelectors etc.
                var rulesField = typeof(StyleSheet).GetField("m_Rules", ALL)
                              ?? typeof(StyleSheet).GetField("rules", ALL);
                var selectorsField = typeof(StyleSheet).GetField("m_ComplexSelectors", ALL)
                                  ?? typeof(StyleSheet).GetField("complexSelectors", ALL);
                var rulesCount = rulesField != null ? (rulesField.GetValue(ss) as Array)?.Length : -1;
                var selCount = selectorsField != null ? (selectorsField.GetValue(ss) as Array)?.Length : -1;
                Log($"  USS[{i}] '{ss.name}': rules={rulesCount} selectors={selCount}");
            }
        }
        catch (Exception e) { Log($"  USS inspect: {e.Message}"); }

        // Check if the VisualTreeAsset itself brought any stylesheets
        // (imported via <Style> elements in the UXML)
        var firstChild = root.childCount > 0 ? root[0] : null;
        if (firstChild != null)
        {
            Log($"  First child sheets: {firstChild.styleSheets.count}");
            // Check resolved background on first child BEFORE debug styles
            var rs = firstChild.resolvedStyle;
            var hasBgImage = rs.backgroundImage.texture != null || rs.backgroundImage.sprite != null;
            Log($"  First child bg: color={rs.backgroundColor} image={hasBgImage}");
        }
    }

    void DiagnoseVisibility(VisualElement root)
    {
        int total = 0, hasSize = 0, isVisible = 0;
        CountElements(root, ref total, ref hasSize, ref isVisible);
        Log($"  Visibility: {total} elements, {hasSize} have size, {isVisible} potentially visible");

        // Check first few elements' styles
        var first = root.childCount > 0 ? root[0] : null;
        if (first != null)
        {
            var rs = first.resolvedStyle;
            Log($"  First child: {first.GetType().Name} #{first.name}" +
                $" [{rs.width:F0}x{rs.height:F0}]" +
                $" bg={rs.backgroundColor}" +
                $" opacity={rs.opacity:F2}" +
                $" display={rs.display}" +
                $" visibility={rs.visibility}" +
                $" childCount={first.childCount}");

            if (first.childCount > 0)
            {
                var second = first[0];
                var rs2 = second.resolvedStyle;
                Log($"  Grandchild: {second.GetType().Name} #{second.name}" +
                    $" [{rs2.width:F0}x{rs2.height:F0}]" +
                    $" bg={rs2.backgroundColor}" +
                    $" display={rs2.display}");
            }
        }
    }

    void CountElements(VisualElement el, ref int total, ref int hasSize, ref int isVisible)
    {
        total++;
        var rs = el.resolvedStyle;
        if (rs.width > 0 && rs.height > 0) hasSize++;
        if (rs.width > 0 && rs.height > 0 && rs.display == DisplayStyle.Flex
            && rs.visibility == Visibility.Visible && rs.opacity > 0)
            isVisible++;
        for (int i = 0; i < el.childCount; i++)
            CountElements(el[i], ref total, ref hasSize, ref isVisible);
    }

    void DumpVisualTree(VisualElement el, int depth, int maxDepth)
    {
        if (depth > maxDepth || el == null) return;
        var indent = new string(' ', depth * 2);
        var tn = el.GetType().Name;
        var n = string.IsNullOrEmpty(el.name) ? "" : $"#{el.name}";
        var cls = el.GetClasses().Any() ? $".{el.GetClasses().First()}" : "";
        var rs = el.resolvedStyle;
        var sz = $"[{rs.width:F0}x{rs.height:F0}]";
        Log($"  {indent}{tn}{n}{cls} {sz} {el.childCount}ch");
        for (int i = 0; i < Math.Min(el.childCount, 6); i++)
            DumpVisualTree(el[i], depth + 1, maxDepth);
        if (el.childCount > 6) Log($"  {indent}  +{el.childCount - 6}");
    }

    // ──────────────────── Compositor ────────────────────

    void FindCompositor()
    {
        if (_compositor != null) return;
        _gameType = FindType("Core.Engine.Compositor.GameCompositor");
        _baseType = FindType("Core.Engine.Compositor.Compositor");
        if (_gameType == null) return;
        var existing = FindObjectsByType(_gameType, FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing.Length > 0) { _compositor = existing[0] as Component; return; }
        var go = GameObject.Find("Compositor") ?? new GameObject("Compositor");
        DontDestroyOnLoad(go);
        try { _compositor = go.AddComponent(_gameType); Log($"PROBE: AddComponent → {_compositor != null}"); }
        catch (Exception e) { Err($"AddComponent: {e.Message}"); }
    }

    void TryInitField(Type type, string name)
    {
        try
        {
            var f = type.GetField(name, ALL);
            if (f == null || f.GetValue(_compositor) != null) return;
            var ft = f.FieldType;
            if (ft.IsAbstract || ft.IsInterface) return;
            f.SetValue(_compositor, Activator.CreateInstance(ft));
        }
        catch { }
    }

    void Inspect(string tag)
    {
        if (_compositor == null || _baseType == null) return;
        try
        {
            Log($"[{tag}] run={GetF(_compositor, _baseType, "isRunning")}" +
                $" svc={DictCount(GetF(_compositor, _baseType, "dxnx"))}" +
                $" ezp={(GetF(_compositor, _gameType, "dxob") == null ? "-" : "glo")}" +
                $" ecr={(GetF(_compositor, _gameType, "dxoc") == null ? "-" : "ecr")}" +
                $" fyh={(GetF(_compositor, _gameType, "dxod") == null ? "-" : "fyh")}");
        }
        catch { }
    }

    void DumpScene()
    {
        var scene = SceneManager.GetActiveScene();
        Log($"SCENE: '{scene.name}' roots={scene.GetRootGameObjects().Length}");
        foreach (var root in scene.GetRootGameObjects())
        {
            int miss = 0; var ts = new List<string>();
            foreach (var c in root.GetComponents<Component>()) { if (c == null) miss++; else ts.Add(c.GetType().Name); }
            Log($"  {root.name}: [{string.Join(", ", ts)}]{(miss > 0 ? $" MISS={miss}" : "")}");
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) FMODProxy.PauseMusic();
        else FMODProxy.ResumeMusic();
    }

    float _lastHeartbeat = 0f;

    void Update()
    {
        if (_compositor != null && Time.time - _lastInspect >= INTERVAL)
        { _lastInspect = Time.time; Inspect($"t{Time.time:F0}"); }
        WebSocketBridge.PollAll();
        FMODProxy.Update();
        Jondo.Reconstructed.Fight.FightTurnService.Instance?.UpdateCountdown(Time.deltaTime);

        // Send kqo heartbeat every 5s while in world connection
        if (_wsConn != null && _connPhase == 1 && Time.time - _lastHeartbeat >= 5.0f)
        {
            _lastHeartbeat = Time.time;
            var kqoFrame = WebSocketBridge.BuildProtobufFrame("kqo", new byte[0]);
            if (kqoFrame != null) WebSocketBridge.Send(_wsConn, kqoFrame);
        }

        if (Input.GetKeyDown(KeyCode.H)) _showOverlay = !_showOverlay;

        if (Input.GetKeyDown(KeyCode.Space) && Jondo.Reconstructed.Fight.FightTurnService.Instance != null && Jondo.Reconstructed.Fight.FightTurnService.Instance.IsInFight)

            Jondo.Reconstructed.Fight.FightTurnService.Instance.PassTurn();
        // Escape toggles PreGameMainMenu (from fzy<T>.closeWithEscape, dump.cs L388926)
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePreGameMainMenu();
        // Enter confirms current screen action (server select / character select)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_currentScreen == "ServerSelection" && _pendingServerId > 0 && _wsConn != null)
            {
                Log($"  [Enter] Server select: {_pendingServerId}");
                SetLoginState(_uiRoot, 1);
                var selectFrame = WebSocketBridge.BuildServerSelectFrame(_pendingServerId, "en");
                WebSocketBridge.Send(_wsConn, selectFrame);
            }
            else if (_currentScreen == "CharacterSelection" && _characterList != null && _characterList.Count > 0 && _wsConn != null)
            {
                int idx = _selectedCharIdx >= 0 ? _selectedCharIdx : 0;
                if (idx < _characterList.Count)
                {
                    int cid = _characterList[idx].id;
                    Log($"  [Enter] Character select: {_characterList[idx].name} (id={cid})");
                    var kvwPayload = new List<byte>();
                    kvwPayload.Add(0x08);
                    int v = cid;
                    while (v > 0x7F) { kvwPayload.Add((byte)(v | 0x80)); v >>= 7; }
                    kvwPayload.Add((byte)v);
                    var kvwFrame = WebSocketBridge.BuildProtobufFrame("kvw", kvwPayload.ToArray());
                    if (kvwFrame != null) WebSocketBridge.Send(_wsConn, kvwFrame);
                }
            }
        }
        // T = cycle theme (DofusUISettings.json: 11 themes from FigmaThemeScriptable)
        if (Input.GetKeyDown(KeyCode.T))
        {
            int idx = System.Array.IndexOf(THEME_NAMES, _currentTheme);
            string next = THEME_NAMES[(idx + 1) % THEME_NAMES.Length];
            SwitchTheme(next);
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            Log("=== [F] FULL DIAGNOSTIC ===");

            // 1. Boot state
            Log($"[F] Screen: {_currentScreen} | LoginState: {_loginState} | WS: {(_wsConn != null ? "connected" : "null")}");
            Log($"[F] Compositor: {(_compositor != null ? "alive" : "null")} | isRunning: {(_compositor != null ? _baseType?.GetField("isRunning", ALL)?.GetValue(_compositor) : "?")}");
            Log($"[F] Services: dxnx={DictCount(GetF(_compositor, _baseType, "dxnx"))} | Bundles: {_loadedBundles?.Count ?? 0} | SpriteCache: {_spriteByPath?.Count ?? 0}");

            // 2. UIDocument state
            var rootUI = GameObject.Find("RootUI");
            var uidoc = rootUI?.GetComponent<UIDocument>();
            var root = uidoc?.rootVisualElement;
            Log($"[F] RootUI: {(rootUI != null ? "found" : "null")} | UIDocument: {(uidoc != null ? "found" : "null")} | root: {(root != null ? $"{root.childCount}ch" : "null")}");
            if (uidoc != null)
            {
                Log($"[F] PanelSettings: {(uidoc.panelSettings != null ? uidoc.panelSettings.name : "null")} | TSS: {(uidoc.panelSettings?.themeStyleSheet != null ? uidoc.panelSettings.themeStyleSheet.name : "null")}");
            }

            // 3. Visual tree dump — all elements with dimensions
            if (root != null)
            {
                Log("[F] --- VISUAL TREE (elements with size > 0) ---");
                int elCount = 0, visCount = 0, bgCount = 0;
                void DumpF(VisualElement ve, int d)
                {
                    elCount++;
                    var rs = ve.resolvedStyle;
                    bool hasSize = rs.width > 0 && rs.height > 0;
                    bool hasBg = rs.backgroundImage.texture != null || rs.backgroundImage.sprite != null;
                    if (hasSize) visCount++;
                    if (hasBg) bgCount++;
                    if (hasSize && d <= 6)
                    {
                        string indent = new string(' ', d * 2);
                        string name = string.IsNullOrEmpty(ve.name) ? ve.GetType().Name : $"#{ve.name}";
                        string bgInfo = hasBg ? $" bg={rs.backgroundImage.texture?.name ?? rs.backgroundImage.sprite?.name}" : "";
                        string cls = string.Join(",", ve.GetClasses().Take(3));
                        string display = rs.display == DisplayStyle.None ? " HIDDEN" : "";
                        Log($"[F] {indent}{name} [{rs.width:F0}x{rs.height:F0}]{bgInfo}{display} cls=[{cls}]");
                    }
                    for (int i = 0; i < ve.childCount; i++) DumpF(ve[i], d + 1);
                }
                DumpF(root, 0);
                Log($"[F] Total: {elCount} elements, {visCount} with size, {bgCount} with bg-image");

                // 4. USS sheets on root
                Log($"[F] USS sheets on root: {root.styleSheets.count}");

                // 5. Specific login elements
                foreach (var ename in new[] { "connectionBackground", "login", "btn_play", "playButton_container",
                    "playButton_background", "playButton_label", "dd_host", "ctr_login", "ctr_gradient", "tx_dofusLogo" })
                {
                    var el = root.Q(ename);
                    if (el == null) { Log($"[F] #{ename}: NOT FOUND"); continue; }
                    var rs2 = el.resolvedStyle;
                    var bgImg = rs2.backgroundImage.texture != null ? rs2.backgroundImage.texture.name :
                                rs2.backgroundImage.sprite != null ? rs2.backgroundImage.sprite.name : "none";
                    var display = rs2.display == DisplayStyle.None ? "HIDDEN" : "visible";
                    Log($"[F] #{ename}: [{rs2.width:F0}x{rs2.height:F0}] bg.a={rs2.backgroundColor.a:F2} bgImg={bgImg} display={display} type={el.GetType().Name} parent={el.parent?.name ?? "null"}");
                }
            }

            // 6. Factory log summary
            Log($"[F] Factories: {_factoryLog.Count} entries, last 3:");
            for (int i = Math.Max(0, _factoryLog.Count - 3); i < _factoryLog.Count; i++)
                Log($"[F]   {_factoryLog[i]}");

            Log("=== [F] END DIAGNOSTIC ===");
        }

        // Key G toggles map cell grid
        if (Input.GetKeyDown(KeyCode.G))
        {
            _showGrid = !_showGrid;
            if (_gridGO != null) _gridGO.SetActive(_showGrid);
            Log($"[Grid] Cell grid toggled: {(_showGrid ? "ON" : "OFF")}");
        }

        // Mouse hover and picking over map
        var mapCam = GameObject.Find("__WebGLCam")?.GetComponent<Camera>();
        if (mapCam != null && (_mapCells != null || _mapMeshGO != null))
        {
            int pickedCell = IsometricGrid.ScreenPosToCellId(Input.mousePosition, mapCam);
            if (pickedCell != _hoveredCellId)
            {
                _hoveredCellId = pickedCell;
                UpdateHoverHighlight(_hoveredCellId);
            }

            // Hotkeys: I for Inventory, S for Spells, Escape to close modals
            if (Input.GetKeyDown(KeyCode.I))
            {
                ToggleInventory();
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                ToggleSpells();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_inventoryRoot != null) CloseInventory();
                if (_spellsRoot != null) CloseSpells();
                if (_npcDialogRoot != null) CloseNpcDialog();
            }

            // Left mouse click to move player or interact with NPC
            if (Input.GetMouseButtonDown(0) && _hoveredCellId >= 0 && _hoveredCellId < 560)
            {
                if (_actorCellMap.TryGetValue(_hoveredCellId, out long targetActorId) && targetActorId < 0)
                {
                    Log($"[Interaction] Clicked NPC actor {targetActorId} at cell {_hoveredCellId}");
                    SendNpcAction(3, _currentMapId, targetActorId);
                }
                else if (_mapCells != null && _hoveredCellId < _mapCells.Length && _mapCells[_hoveredCellId].mov)
                {
                    if (!_isWalking)
                    {
                        var path = IsometricGrid.FindPath(_playerCellId, _hoveredCellId, _mapCells);
                        if (path != null && path.Count > 1)
                        {
                            Log($"[Movement] Walking path from {_playerCellId} to {_hoveredCellId} ({path.Count} steps)");
                            if (_walkCoroutine != null) StopCoroutine(_walkCoroutine);
                            _walkCoroutine = StartCoroutine(WalkPathCoroutine(path));
                        }
                    }
                }
            }
        }
    }

    // ──────────────────── GUI overlay (toggle with H) ────────────────────

    static Texture2D _bg;
    void OnGUI()
    {
        if (!_showOverlay) return;
        if (_bg == null) { _bg = new Texture2D(1, 1); _bg.SetPixel(0, 0, new Color(0, 0, 0, 0.75f)); _bg.Apply(); }
        GUI.depth = -99999;
        var s = new GUIStyle { fontSize = 10, wordWrap = false, richText = true, padding = new RectOffset(4, 4, 2, 2), normal = { textColor = Color.white, background = _bg } };
        float pw = Mathf.Min(Screen.width * 0.4f, 500);

        string hdr = $"<b><color=#0f8>Probe v32</color></b> t={Time.time:F0} [H F G]";
        GUI.Label(new Rect(5, 5, pw, 16), hdr, s);
        if (!string.IsNullOrEmpty(_status))
            GUI.Label(new Rect(5, 18, pw, 14), $"<color=#ff0>{_status}</color>", s);

        float y = 34;
        if (_errors.Count > 0)
        {
            var es = new GUIStyle(s) { normal = { textColor = new Color(1f, 0.6f, 0.6f), background = _bg } };
            string t = $"<color=#f44><b>ERR({_errors.Count})</b></color>\n";
            for (int i = Math.Max(0, _errors.Count - 5); i < _errors.Count; i++) t += _errors[i] + "\n";
            GUI.Label(new Rect(5, y, pw, 100), t, es); y += 103;
        }
        if (_log.Count > 0)
        {
            var ls = new GUIStyle(s) { normal = { textColor = new Color(0.8f, 0.9f, 1f), background = _bg } };
            string t = "";
            for (int i = Math.Max(0, _log.Count - 25); i < _log.Count; i++) t += _log[i] + "\n";
            float lh = Mathf.Min(Screen.height - y - 10, 400);
            if (lh > 30) GUI.Label(new Rect(5, y, pw, lh), t, ls);
        }

        if (_hoveredCellId >= 0 && _mapCells != null && _hoveredCellId < _mapCells.Length)
        {
            var cell = _mapCells[_hoveredCellId];
            var coord = IsometricGrid.CellIdToCoords(_hoveredCellId);
            string cellInfo = $"<color=#0ff>Cell {_hoveredCellId}</color> ({coord.x},{coord.y}) Walk:{(cell.mov ? "<color=#0f0>YES</color>" : "<color=#f44>NO</color>")} LOS:{cell.los} Player:{_playerCellId}";
            GUI.Label(new Rect(5, Screen.height - 25, 450, 20), cellInfo, s);
        }
    }

    // Called via SendMessage from Playwright tests (window.unityInstance.SendMessage)
    // ──────────────────── Live UI Toolkit Hierarchy Inspector & Dumper ────────────────────
    public static string DumpVisualTree(VisualElement el, int maxDepth = 6)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== LIVE UITK VISUAL TREE DUMP (Root: {el?.name ?? "null"}) ===");
        DumpElementRecursive(el, 0, maxDepth, sb);
        string res = sb.ToString();
        Log(res);
        return res;
    }

    static void DumpElementRecursive(VisualElement el, int depth, int maxDepth, System.Text.StringBuilder sb)
    {
        if (el == null || depth > maxDepth) return;
        string indent = new string(' ', depth * 2);
        string classes = string.Join(".", el.GetClasses());
        if (!string.IsNullOrEmpty(classes)) classes = "." + classes;
        var r = el.resolvedStyle;
        sb.AppendLine($"{indent}<{el.GetType().Name} name=\"{el.name}\" class=\"{classes}\" display=\"{r.display}\" rect=\"({r.left:F0},{r.top:F0},{r.width:F0}x{r.height:F0})\">");
        for (int i = 0; i < el.childCount; i++)
            DumpElementRecursive(el[i], depth + 1, maxDepth, sb);
    }

    public void TestAction(string action)
    {
        Log($"  [TEST] Action: {action}");
        if (action == "dump_ui" || action == "inspect")
        {
            DumpVisualTree(_uiRoot);
        }
        else if (action == "enter")
        {
            if (_currentScreen == "Login" || _currentScreen == null)
                OnPlayClicked(_uiRoot);
            else if (_currentScreen == "ServerSelection" && _wsConn != null)
            {
                if (_serverList == null || _serverList.Count == 0)
                {
                    Log("  [TEST] Cannot select server: server list is empty");
                    return;
                }
                int servId = _pendingServerId > 0 ? _pendingServerId : _serverList[0].id;
                Log($"  [TEST] Selecting server {servId}");
                SetLoginState(_uiRoot, 1);
                var frame = WebSocketBridge.BuildServerSelectFrame(servId, "en");
                WebSocketBridge.Send(_wsConn, frame);
            }
            else if (_currentScreen == "CharacterSelection" && _wsConn != null && _characterList != null && _characterList.Count > 0)
            {
                int idx = _selectedCharIdx >= 0 ? _selectedCharIdx : 0;
                int cid = _characterList[idx].id;
                Log($"  [TEST] Selecting character {cid} ({_characterList[idx].name})");
                var kvwPayload = new List<byte>();
                kvwPayload.Add(0x08);
                int v = cid;
                while (v > 0x7F) { kvwPayload.Add((byte)(v | 0x80)); v >>= 7; }
                kvwPayload.Add((byte)v);
                var kvwFrame = WebSocketBridge.BuildProtobufFrame("kvw", kvwPayload.ToArray());
                if (kvwFrame != null) WebSocketBridge.Send(_wsConn, kvwFrame);
            }
        }
    }
}
