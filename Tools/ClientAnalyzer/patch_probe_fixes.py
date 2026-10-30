with open('UnityProject/Assets/WebGLProbe.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. PopulateLoginScreen
old1 = '''    void PopulateLoginScreen(VisualElement root)
    {
        var btnPlay = root.Q("btn_play"); // LoginBinding._btn_play
        if (btnPlay is Button playBtn)
            playBtn.clicked += () => OnPlayClicked(root);
        else if (btnPlay != null)
            btnPlay.RegisterCallback<ClickEvent>(evt => OnPlayClicked(root));

        Log($"  PopulateLogin: click handler wired");
    }'''

new1 = '''    void PopulateLoginScreen(VisualElement root)
    {
        var btnPlay = root.Q("btn_play"); // LoginBinding._btn_play
        if (btnPlay != null)
        {
            btnPlay.SetEnabled(true);
            if (btnPlay is Button playBtn)
                playBtn.clicked += () => OnPlayClicked(root);
            else
                btnPlay.RegisterCallback<ClickEvent>(evt => OnPlayClicked(root));
        }

        Log($"  PopulateLogin: click handler wired and button enabled");
    }'''

assert old1 in text, "old1 not found"
text = text.replace(old1, new1, 1)

# 2. SetupConnectionBackground
old2 = '''        VisualTreeAsset bgUxml = null;
        _loginUxml = null;
        foreach (var kv in _loadedBundles)
        {
            if (bgUxml == null) bgUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/connectionbackground.uxml");
            if (_loginUxml == null) _loginUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/login.uxml");
        }'''

new2 = '''        VisualTreeAsset bgUxml = null;
        _loginUxml = null;
        foreach (var kv in _loadedBundles)
        {
            if (bgUxml == null)
            {
                bgUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/connectionbackground.uxml");
                if (bgUxml == null) bgUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/connectionbackground.uxml.asset");
                if (bgUxml == null) bgUxml = kv.Value.LoadAsset<VisualTreeAsset>("connectionbackground");
            }
            if (_loginUxml == null)
            {
                _loginUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/login.uxml");
                if (_loginUxml == null) _loginUxml = kv.Value.LoadAsset<VisualTreeAsset>("assets/ui/auth/login.uxml.asset");
                if (_loginUxml == null) _loginUxml = kv.Value.LoadAsset<VisualTreeAsset>("login");
            }
        }'''

assert old2 in text, "old2 not found"
text = text.replace(old2, new2, 1)

# 3. ShowScreen
old3 = '''            VisualTreeAsset vta = null;
            string uxmlLower = uxmlPath.ToLower();
            foreach (var kv in new List<KeyValuePair<string, AssetBundle>>(_loadedBundles))
            {
                vta = kv.Value.LoadAsset<VisualTreeAsset>(uxmlLower);
                if (vta != null) break;
            }'''

new3 = '''            VisualTreeAsset vta = null;
            string uxmlLower = uxmlPath.ToLower();
            foreach (var kv in new List<KeyValuePair<string, AssetBundle>>(_loadedBundles))
            {
                vta = kv.Value.LoadAsset<VisualTreeAsset>(uxmlLower);
                if (vta == null) vta = kv.Value.LoadAsset<VisualTreeAsset>(uxmlLower + ".asset");
                if (vta == null) vta = kv.Value.LoadAsset<VisualTreeAsset>(screenName);
                if (vta == null) vta = kv.Value.LoadAsset<VisualTreeAsset>(screenName.ToLower());
                if (vta != null) break;
            }'''

assert old3 in text, "old3 not found"
text = text.replace(old3, new3, 1)

# 4. LoadMapData
old4 = '''        string baseUrl = Application.absoluteURL;
        int lastSlash = baseUrl.LastIndexOf('/');
        if (lastSlash > 0) baseUrl = baseUrl.Substring(0, lastSlash + 1);'''

new4 = '''        string baseUrl = Application.absoluteURL;
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
        }'''

assert old4 in text, "old4 not found"
text = text.replace(old4, new4, 1)

with open('UnityProject/Assets/WebGLProbe.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("Successfully applied all 4 fixes to WebGLProbe.cs!")

