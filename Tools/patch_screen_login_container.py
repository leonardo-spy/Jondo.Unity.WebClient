file_path = "UnityProject/Assets/WebGLProbe.cs"

with open(file_path, "r", encoding="utf-8") as f:
    code = f.read()

# Replace ShowLoginOverlay clone
old_clone = '''        if (_loginUxml != null)
        {
            _loginUxml.CloneTree(root);
            Log($"  login.uxml cloned → {root.childCount} children");
        }'''

new_clone = '''        VisualElement loginContainer = null;
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
        }'''

if old_clone in code:
    code = code.replace(old_clone, new_clone)
    print("Patched ShowLoginOverlay clone to wrap in screen_Login")
else:
    print("Warning: old_clone not found exactly, trying with CRLF/LF")
    code = code.replace(old_clone.replace("\r\n", "\n"), new_clone.replace("\r\n", "\n"))

# In ShowLoginOverlay, use targetEl = loginContainer ?? root
old_login_calls = '''        ApplyScreenBehavior("Login", root);
        DispatchGen_Ctor("Login", root);
        DispatchGen_Localize("Login", root);
        DispatchGen_OnEnable("Login", root);
        PopulateLoginScreen(root);
        SetLoginState(root, 0);'''

new_login_calls = '''        var targetLogin = loginContainer ?? root;
        ApplyScreenBehavior("Login", targetLogin);
        DispatchGen_Ctor("Login", targetLogin);
        DispatchGen_Localize("Login", targetLogin);
        DispatchGen_OnEnable("Login", targetLogin);
        PopulateLoginScreen(targetLogin);
        SetLoginState(targetLogin, 0);'''

if old_login_calls in code:
    code = code.replace(old_login_calls, new_login_calls)
    print("Patched ShowLoginOverlay calls to targetLogin")
else:
    code = code.replace(old_login_calls.replace("\r\n", "\n"), new_login_calls.replace("\r\n", "\n"))

with open(file_path, "w", encoding="utf-8") as f:
    f.write(code)

print("Done patching login container.")

