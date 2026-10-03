using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

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
    long _currentMapId;

    const int MAX = 300;
    const float INTERVAL = 10f;
    static readonly BindingFlags ALL = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly BindingFlags STATIC = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        Log("PROBE v31: lifecycle Gen_ dispatch (114 methods), audio pause, data-driven boot");
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
    static bool _banksLoaded;

    static void LoadBootLogic()
    {
        var ta = Resources.Load<UnityEngine.TextAsset>("BootLogic");
        if (ta == null) { Log("  BootLogic.json not found"); return; }
        var json = ta.text;
        _bankPath = ExtractJsonString(json, "bankPath") ?? "Content/Audio/Banks/Desktop/";
        _masterBank = ExtractJsonString(json, "masterBank") ?? "Master.bank";
        _stringsBank = ExtractJsonString(json, "stringsBank") ?? "Master.strings.bank";

        // Count phases and services from BootLogic for data-driven boot logging
        int pIdx = json.IndexOf("\"phases\":");
        if (pIdx >= 0) { int a = json.IndexOf('[', pIdx); if (a >= 0) { int count = 0; int p = a; while ((p = json.IndexOf("\"name\":", p + 1)) >= 0 && p < json.IndexOf(']', a)) count++; _bootPhaseCount = count; } }
        int sIdx = json.IndexOf("\"serviceInits\":");
        if (sIdx >= 0) { int a = json.IndexOf('[', sIdx); if (a >= 0) { int count = 0; int p = a; int end = json.IndexOf(']', a); while ((p = json.IndexOf("\"type\":", p + 1)) >= 0 && p < end) count++; _bootServiceCount = count; } }

        Log($"  BootLogic: {_bootPhaseCount} phases, {_bootServiceCount} services, bank={_bankPath}");
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
            if (_tree != null) { _currentFactoryType = _cls; foreach (var ch in _tree) el.Add(CreateChild(ch)); }
            LoadComponentUSS(el, "Core.UILogic.Components." + _cls);
            AddClickable(el);
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

    void Start()
    {
        Log("PROBE: Start");
        DumpScene();
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
        RegisterServices();
        yield return null;

        Log($"=== Phase 1: InitializeServices ({_bootServiceCount} from BootLogic) ===");
        CallServiceInitMethods();
        yield return null;

        Log("=== Phase 2: LoadDataDependencies (bundles + FMOD) ===");
        yield return StartCoroutine(LoadBundles());

        // FMOD: System.create at boot (decompiled elv.<InitializeAudioManager>d__68, dump.cs L308193)
        // Bank loading deferred to first user interaction — FMOD WebGL returns ERR_FORMAT (70)
        // when loading banks with suspended AudioContext. Banks load on first click (Play button).
        // SOURCE: BootLogic.json Phase 2 → AudioManager.Initialize → FMOD.Studio.System.create
        InitFMODAudio();

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

    void RegisterServices()
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
        if (dxnx != null && glo != null)
        {
            RegisterInDict(dxnx, FindType("ezp") ?? glo.GetType(), glo, "glo→ezp");
            RegisterInDict(dxnx, glo.GetType(), glo, "glo→glo");
        }
        if (dxnx != null && ecr != null) RegisterInDict(dxnx, ecr.GetType(), ecr, "ecr");
        if (dxnx != null && fyh != null) RegisterInDict(dxnx, fyh.GetType(), fyh, "fyh");

        Log($"  dxnx count = {DictCount(GetF(_compositor, _baseType, "dxnx"))}");
        Inspect("after-register");
    }

    void RegisterInDict(object dict, Type key, object service, string label)
    {
        try
        {
            var addMethod = dict.GetType().GetMethod("set_Item") ?? dict.GetType().GetMethod("Add");
            if (addMethod == null) return;
            var valueType = dict.GetType().GetGenericArguments()[1];
            if (valueType.IsAssignableFrom(service.GetType()) || valueType.IsInterface)
            {
                addMethod.Invoke(dict, new[] { key, service });
                Log($"  registered {label}");
            }
        }
        catch (Exception e) { Err($"  register {label}: {e.InnerException?.Message ?? e.Message}"); }
    }

    object ConstructService(string typeName, string assembly)
    {
        var type = FindType(typeName) ?? ScanForType(typeName);
        if (type == null) { Err($"  {typeName}: type not found"); return null; }
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

        VisualTreeAsset bgUxml = null;
        _loginUxml = null;
        foreach (var kv in _loadedBundles)
        {
            if (bgUxml == null) bgUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/connectionbackground.uxml");
            if (_loginUxml == null) _loginUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/login.uxml");
        }
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

        if (_loginUxml != null)
        {
            _loginUxml.CloneTree(root);
            Log($"  login.uxml cloned → {root.childCount} children");
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

        ApplyScreenBehavior("Login", root);
        DispatchGen_Ctor("Login", root);
        DispatchGen_Localize("Login", root);
        DispatchGen_OnEnable("Login", root);
        PopulateLoginScreen(root);

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
        var btnPlay = root.Q("btn_play"); // LoginBinding._btn_play
        if (btnPlay is Button playBtn)
            playBtn.clicked += () => OnPlayClicked(root);
        else if (btnPlay != null)
            btnPlay.RegisterCallback<ClickEvent>(evt => OnPlayClicked(root));

        Log($"  PopulateLogin: click handler wired");
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

    void PopulateServerTabs(VisualElement root)
    {
        // Read tabs from ScreenBehavior.json (decompiled GetTabWithFilter switch mapping)
        if (_screenSpecs == null || !_screenSpecs.TryGetValue("ServerSelection", out var spec)) return;
        if (spec.rawJson == null) return;

        int tabsIdx = spec.rawJson.IndexOf("\"tabs\":");
        if (tabsIdx < 0) return;
        int arrStart = spec.rawJson.IndexOf('[', tabsIdx);
        if (arrStart < 0) return;
        int arrEnd = arrStart;
        int depth = 0;
        for (int j = arrStart; j < spec.rawJson.Length; j++)
        {
            if (spec.rawJson[j] == '[') depth++;
            else if (spec.rawJson[j] == ']') { depth--; if (depth == 0) { arrEnd = j; break; } }
        }

        // Count servers per gameType from HAAPI data
        var typeCounts = new Dictionary<int, int>();
        if (_serverList != null)
        {
            foreach (var srv in _serverList)
            {
                int typeId = 0;
                if (_serverTypes != null && _serverTypes.TryGetValue(srv.id.ToString(), out typeId)) { }
                if (!typeCounts.ContainsKey(typeId)) typeCounts[typeId] = 0;
                typeCounts[typeId]++;
            }
        }
        int totalServers = _serverList?.Count ?? 0;

        string arr = spec.rawJson.Substring(arrStart, arrEnd - arrStart + 1);
        int pos = 0;
        int tabsPopulated = 0;
        while (pos < arr.Length)
        {
            int objS = arr.IndexOf('{', pos);
            if (objS < 0) break;
            int objE = arr.IndexOf('}', objS);
            if (objE < 0) break;
            string obj = arr.Substring(objS, objE - objS + 1);

            string elName = ExtractJsonString(obj, "element");
            string i18nKey = ExtractJsonString(obj, "i18n");
            string gameTypeStr = ExtractJsonString(obj, "gameType");

            if (elName != null)
            {
                var tabEl = root.Q(elName);
                if (tabEl != null)
                {
                    // Set tab label: "TabName (count/total)"
                    int gameType = 0;
                    if (gameTypeStr != null) int.TryParse(gameTypeStr, out gameType);
                    int count = typeCounts.ContainsKey(gameType) ? typeCounts[gameType] : 0;
                    string label = i18nKey != null ? I18n(i18nKey, elName) : elName;
                    string tabText = $"{label} ({count}/{totalServers})";

                    // Try set text on the tab (DofusTab uses inner label)
                    var tabLabel = tabEl.Q<Label>();
                    if (tabLabel != null) tabLabel.text = tabText;
                    else if (tabEl is Label tl) tl.text = tabText;

                    tabEl.style.display = DisplayStyle.Flex;
                    tabsPopulated++;
                }
            }
            pos = objE + 1;
        }
        Log($"  Tabs populated: {tabsPopulated}, server counts: {string.Join(",", typeCounts.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }

    // ──────────────────── Server Selection Population ────────────────────
    // From ServerSelection handler (dump.cs L262799), ServerCardBinding (dump.cs L206154)
    // ServerSelectionBinding (dump.cs L206570): 7 server cards + list view + play button

    // PopulateServerSelection: data loop (servers → cards) from HAAPI protocol data.
    // SOURCE: decompiled ServerSelection.DisplayServer (711L) + BindServerLine (189L)
    //   — iterates List<ServerInformation>, sets lbl_name/lbl_characters/tag per card
    //   — original uses BigRoundedCarousel + ServerInformation binding
    // WHY STATIC: the data comes from HAAPI WebSocket (protocol), not from decompiled constants.
    //   The decompiled shows the PATTERN (iterate servers, set binding fields) but the DATA
    //   is runtime (server names, char counts from AuthAccepted protobuf).
    // IF CLIENT UPDATES: check ServerCardBinding in dump.cs for new/renamed fields,
    //   and ServerSelection.BindServerLine for changed data mapping.
    // Element names from: ScreenBehavior.json → templates.ServerCard.elements
    void PopulateServerSelection(VisualElement root)
    {
        if (_serverList == null || _serverList.Count == 0)
        {
            Log("  PopulateServerSelection: no servers");
            return;
        }

        PopulateServerTabs(root);

        VisualTreeAsset cardTemplate = null;
        foreach (var kv in new List<KeyValuePair<string, AssetBundle>>(_loadedBundles))
        {
            cardTemplate = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/serverselection/servercard.uxml");
            if (cardTemplate != null) break;
        }

        _selectedServerCardIdx = -1;
        for (int i = 0; i < 7 && i < _serverList.Count; i++)
        {
            var srv = _serverList[i];
            var card = root.Q($"serverCard_{i}");
            if (card == null) continue;

            card.style.display = DisplayStyle.Flex;

            if (card.childCount == 0 && cardTemplate != null)
            {
                cardTemplate.CloneTree(card);
                var colorMap = LoadThemeColorMap();
                int tc = 0;
                ApplyThemeRecursive(card, colorMap, ref tc);
                var fontAsset = Resources.Load<FontAsset>("DefaultFont");
                if (fontAsset != null) { RestoreFontAtlas(fontAsset); ApplyFontRecursive(card, fontAsset); }
            }

            // Data → card binding (from decompiled BindServerLine/DisplayServer pattern)
            SetElementText(card, "lbl_name", ServerName(srv.id).ToUpper());
            string charLabel = I18n(1092679, "Character");
            SetElementText(card, "lbl_characters", $"{charLabel.ToUpper()} ({srv.charCount})");
            if (_serverTypes != null && _serverTypes.TryGetValue(srv.id.ToString(), out int typeId))
                SetElementText(card, "tag", ServerTypeName(typeId));
            if (srv.charNames != null && srv.charNames.Count > 0)
                SetElementText(card, "lbl_newCharacter", string.Join(", ", srv.charNames));

            int serverIdx = i;
            int serverId = srv.id;
            var clickArea = card.Q("ctr_cardClickableArea") ?? card;
            clickArea.RegisterCallback<ClickEvent>(evt => SelectServerCard(root, serverIdx, serverId));

            if (_selectedServerCardIdx < 0 && (srv.charCount > 0 || i == 0))
                SelectServerCard(root, i, srv.id);
        }

        for (int i = _serverList.Count; i < 7; i++)
        {
            var card = root.Q($"serverCard_{i}");
            if (card != null) card.style.display = DisplayStyle.None;
        }

        // Wire Play button (ServerSelection.OnValidateButton, dump.cs L262872)
        var btnPlay = root.Q("btn_play");
        if (btnPlay is Button ssBtn)
        {
            ssBtn.clicked += () =>
            {
                if (_pendingServerId > 0 && _wsConn != null)
                {
                    Log($"  Server select: {_pendingServerId}");
                    SetLoginState(root, 1);
                    var selectFrame = WebSocketBridge.BuildServerSelectFrame(_pendingServerId, "en");
                    WebSocketBridge.Send(_wsConn, selectFrame);
                }
            };
            var ssLabel = btnPlay.Q("playButton_label");
            if (ssLabel is Label sl) sl.text = I18n("ui.common.play", "Play");
        }

        // Wire carousel navigation (ServerSelectionBinding._btn_moveLeft/_btn_moveRight, dump.cs L206570)
        // Start carousel centered on the selected server (with characters)
        if (_selectedServerCardIdx > 0)
            _carouselOffset = Math.Max(0, _selectedServerCardIdx - 3); // center in 7-card view
        else
            _carouselOffset = 0;
        var btnLeft = root.Q("btn_moveLeft");
        var btnRight = root.Q("btn_moveRight");
        if (btnLeft != null)
            btnLeft.RegisterCallback<ClickEvent>(evt =>
            {
                if (_carouselOffset > 0) { _carouselOffset--; RefreshCarouselCards(root, cardTemplate); }
            });
        if (btnRight != null)
            btnRight.RegisterCallback<ClickEvent>(evt =>
            {
                if (_carouselOffset + 7 < _serverList.Count) { _carouselOffset++; RefreshCarouselCards(root, cardTemplate); }
            });

        Log($"  ServerSelection: {_serverList.Count} servers, offset={_carouselOffset}, selected={_pendingServerId}");
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
        // Server name (from decompiled CharacterSelection.Localize: lbl_serverName = server name)
        SetElementText(root, "lbl_serverName", _pendingServerId > 0 ? ServerName(_pendingServerId) : "");

        if (_characterList == null || _characterList.Count == 0)
        {
            // Empty state: show create button, hide play
            var btnCreate = root.Q("btn_createCharacter");
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
        var scrollView = root.Q("gd_character");
        _selectedCharIdx = -1;

        // Try to load characterLine.uxml template (from characterSelection/ dir)
        VisualTreeAsset charLineUxml = null;
        foreach (var kv in new List<KeyValuePair<string, AssetBundle>>(_loadedBundles))
        {
            charLineUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/characterselection/characterline.uxml");
            if (charLineUxml != null) break;
        }

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
        var btnPlay = root.Q("btn_play");
        if (btnPlay is Button pb)
        {
            pb.clicked += () =>
            {
                int idx = _selectedCharIdx >= 0 ? _selectedCharIdx : 0;
                if (idx < _characterList.Count && _wsConn != null)
                {
                    int cid = _characterList[idx].id;
                    Log($"  Selecting character {_characterList[idx].name} (id={cid})...");
                    var kvwPayload = new List<byte>();
                    kvwPayload.Add(0x08); // field 1, varint
                    int v = cid;
                    while (v > 0x7F) { kvwPayload.Add((byte)(v | 0x80)); v >>= 7; }
                    kvwPayload.Add((byte)v);
                    var kvwFrame = WebSocketBridge.BuildProtobufFrame("kvw", kvwPayload.ToArray());
                    if (kvwFrame != null) WebSocketBridge.Send(_wsConn, kvwFrame);
                }
            };
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
            var btnPlay = root.Q("btn_play");
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
                if (code == 1000)
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
                Log($"  nickname={nickname}");

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
                DispatchGen_Event("CharacterSelection", "OnCharactersListUpdated", root);
            }
            else if (opcode == "jru")
            {
                // jru: map load command (WorldEntry.SendMapAsync, field 2 = mapId)
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    long mapId = PbVarField(payload, 2);
                    Log($"  jru: map {mapId}");
                    _currentMapId = mapId;
                }
            }
            else if (opcode == "kub")
            {
                // kub: character characteristics (ConnectionProtocol.BuildCharacteristics)
                var payload = ExtractPayload(data);
                if (payload != null)
                    Log($"  kub: characteristics ({data.Length}b)");
            }
            else if (opcode == "jss")
            {
                // jss: map actors response (MapLoadHandler.HandleMapLoadRequest)
                var payload = ExtractPayload(data);
                if (payload != null)
                {
                    var actors = PbRepeated(payload, 1);
                    Log($"  jss: {actors.Count} actors on map");
                }
            }
            else if (opcode == "kva")
            {
                // kva: character selected confirmation — triggers world entry
                // From WorldEntry.SendAfterCharacterAsync — this is the first message after kvw
                Log($"  kva: character confirmed, world entry starting");
                _currentScreen = "World";
            }
            else if (opcode != null)
            {
                Log($"  Game: {opcode} ({data.Length}b)");
            }
        }
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
        if (uxmlPath == null)
        {
            Log($"  Screen '{screenName}' UXML not found in assetPaths");
            return;
        }
        try
        {
            VisualTreeAsset vta = null;
            string uxmlLower = uxmlPath.ToLower();
            foreach (var kv in new List<KeyValuePair<string, AssetBundle>>(_loadedBundles))
            {
                vta = kv.Value.LoadAsset<VisualTreeAsset>(uxmlLower);
                if (vta != null) break;
            }
            if (vta == null)
            {
                Log($"  {screenName}: UXML '{uxmlPath}' not found in bundles");
                return;
            }

            // Hide all children except connectionBackground (the persistent bg).
            // Also hide by name: login screen elements from the previous CloneTree.
            for (int ci = 0; ci < root.childCount; ci++)
            {
                var child = root[ci];
                if (child.name == "connectionBackground" || child.name == "img") continue;
                child.style.display = DisplayStyle.None;
            }
            // Hide named overlays from previous screens
            var loginEl = root.Q("login");
            if (loginEl != null) loginEl.style.display = DisplayStyle.None;

            var screenUI = vta.CloneTree();
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

    void Update()
    {
        if (_compositor != null && Time.time - _lastInspect >= INTERVAL)
        { _lastInspect = Time.time; Inspect($"t{Time.time:F0}"); }
        WebSocketBridge.PollAll();
        FMODProxy.Update();

        if (Input.GetKeyDown(KeyCode.H)) _showOverlay = !_showOverlay;
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

        string hdr = $"<b><color=#0f8>Probe v28</color></b> t={Time.time:F0} [H F]";
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
    }

    // Called via SendMessage from Playwright tests (window.unityInstance.SendMessage)
    public void TestAction(string action)
    {
        Log($"  [TEST] Action: {action}");
        if (action == "enter")
        {
            if (_currentScreen == "Login" || _currentScreen == null)
                OnPlayClicked(_uiRoot);
            else if (_currentScreen == "ServerSelection" && _pendingServerId > 0 && _wsConn != null)
            {
                SetLoginState(_uiRoot, 1);
                var frame = WebSocketBridge.BuildServerSelectFrame(_pendingServerId, "en");
                WebSocketBridge.Send(_wsConn, frame);
            }
            else if (_currentScreen == "CharacterSelection" && _characterList != null && _characterList.Count > 0 && _wsConn != null)
            {
                int idx = _selectedCharIdx >= 0 ? _selectedCharIdx : 0;
                int cid = _characterList[idx].id;
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
