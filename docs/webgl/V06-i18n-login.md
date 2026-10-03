# V06: Dofus 3 WebGL — Font+USS Fix, I18n Parser, Theme Extractor, WebSocket Login

Session 4 (Sep 18, 2026). We started from V5 (USS disabled, text disappeared) and arrived at:
1. Recursive font AFTER USS loading — text comes back even with USS active
2. CSS reconstruction fix — MissingAssetReference in fonts → `resource("DefaultFont")` instead of `none`
3. I18n parser (Python) — parses localization binaries (339k strings/language)
4. Theme color extractor — extracts FigmaThemeScriptable from bundles → JSON
5. Complete WebSocket login — HAAPI login → launch → protobuf auth → server list

---

## 1. Font + USS Fix (RESOLVED)

### Problem (V5)
Re-enabling USS loading killed the text. The 202 USS with selectors had `-unity-font-definition: none` (from MissingAssetReference in CSS reconstruction). USS rules overwrite the font inherited from root on child elements.

### Solution: two fronts

**Runtime (WebGLProbe v26):**
`ApplyFontRecursive()` — walks the ENTIRE tree AFTER USS loading, theme colors, and PopulateLoginScreen. Sets inline font on each element. Inline styles have higher specificity than USS rules.

**Build-time (RebuildBundlesForWebGL v4):**
In `PropertyToCSS`, case 13 (MissingAssetReference): if the property name is `-unity-font-definition` or `-unity-font`, generates `resource("DefaultFont")` instead of `none`. This way Unity resolves the reference via the Resources folder.

---

## 2. I18n Parser

### Binary format (discovered)
```
[3 bytes]  Magic/version (02 66 72)
[4 bytes]  LE uint32: dict1_count
[8*N]      Dict1: (int32 key, uint32 offset) — 339,342 entries
[4 bytes]  LE uint32: dict2_count  
[12*M]     Dict2: (uint64 hash, uint32 offset) — 7,346 entries
[rest]     String data: varint-length-prefixed UTF-8
```

### Tool: `Tools/I18nParser/parse_i18n.py`
```bash
# Login strings
python parse_i18n.py en.bin --login

# Filter by term
python parse_i18n.py en.bin --filter "server"

# All languages
python parse_i18n.py --all "Content/I18n/" --output-dir output/

# Statistics
python parse_i18n.py en.bin --stats
```

### Login keys
| Key | EN | PT |
|---|---|---|
| 60264 | Play | Jogar |
| 1094627 | Options | Opcoes |
| 1094651 | Log in | Conectar-se |
| 1094084 | Loading | Carregando |
| 1094100 | Close | Fechar |
| 1093664 | Server | Servidor |
| 1093155 | Select a server | Selecionar um servidor |

### Runtime
Compact JSONs in `Assets/Resources/I18n/{en,pt,es,fr}.json` (~1KB each). Probe loads via `Resources.Load<TextAsset>("I18n/en")` on boot. Helper `I18n(key, fallback)` resolves strings.

---

## 3. Theme Color Extractor

### Menu: `Build > Extract Theme Colors`
Scans Win64 bundles looking for FigmaThemeScriptable/FigmaThemeManagerScriptable. Extracts `m_figmaColorsData` (varName + Vector4 color) via reflection. Generates `Assets/Resources/ThemeColors.json`:
```json
{
  "background_medium90": "rgba(20, 23, 38, 0.9)",
  "white_white100": "rgb(255, 255, 255)",
  ...
}
```

### Runtime
Probe tries: bundles (FigmaThemeManagerScriptable) → ThemeColors.json → hardcoded defaults.

---

## 4. WebSocket Login Flow (IMPLEMENTED)

### Complete flow
```
1. POST /api/entrar {"usuario":"admin","clave":"admin"}
   → {bien: true, token: "abc...", apodo: "Jondo", cuenta: 1}

2. POST /api/lanzamiento {"token":"abc...","idioma":"en"}
   → {bien: true, instancia: 12, hash: "def...", cuenta: 1}

3. WebSocket ws://localhost:3000/game-ws
   → onOpen: send protobuf AuthFrame(token, lang)

4. Server sends AuthenticationAccepted (server list + characters)
   → Auto-select server 1

5. Server sends ServerSelected (ticket + IP + port)
   → TODO: reconnect with ticket for game phase
```

### Protobuf (hand-encoded, no library)
```
GameMessage {
  auth: AuthenticationTicketMessage {
    lang: "en"           // field 1
    ticket: {            // field 3
      tokenData: {       // field 3
        token: "abc..."  // field 1
      }
    }
  }
}
```

Frame format: varint-length-prefix + protobuf payload.

### HTTP Bridge
New `HTTP_Post`/`HTTP_Get` in jslib + C# wrappers. Uses `fetch()` in the browser.

---

## 5. Current State

```
[done] Recursive font (ApplyFontRecursive) — text comes back with USS active
[done] CSS reconstruction: font MissingAssetRef → resource("DefaultFont")
[done] I18n parser: 339k strings, 5 languages, compact JSONs
[done] I18n runtime: LoadI18n() + I18n(key, fallback)
[done] Theme extractor: Build > Extract Theme Colors → JSON
[done] Theme runtime: bundles → JSON → defaults fallback chain
[done] WebSocket login: HAAPI → protobuf auth → server message handling
[done] HTTP bridge: jslib fetch + C# wrappers
[done] Protobuf hand-encoding: BuildAuthFrame + BuildServerSelectFrame
[done] Probe v26

[pending] Server select → ticket → game reconnect (step 5 of the flow)
[pending] Character list + selection
[pending] World entry
```

## 6. Steps to Test

In the Unity Editor:
1. `Build > Extract Theme Colors` (optional — extracts real colors from bundles)
2. `Build > 2. Create FontAsset from Lexend` (recreates font + copies to USS dir)
3. Delete `Assets/StreamingAssets/aa/WebGL/*` (old bundles)
4. `Build > 1 - Extract + Rebuild for WebGL` (rebuild with font fix in CSS)
5. Build WebGL

To test the login:
1. Start JondoEmu (the emulator)
2. `cd PlayBrowser/WebClient && node server.js`
3. Open `http://localhost:3000` in the browser
4. Click PLAY (login with admin/admin)
5. Check browser console for WebSocket logs

## 7. Key Files

| File | Description |
|---|---|
| `C:\DofusWeb\Assets\WebGLProbe.cs` | Probe v26 — font fix, I18n, HAAPI login |
| `C:\DofusWeb\Assets\WebSocketBridge.cs` | WS + HTTP + protobuf helpers |
| `C:\DofusWeb\Assets\Plugins\WebSocketBridge.jslib` | JS: WebSocket + fetch |
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | v4: CSS font fix + theme extractor |
| `C:\DofusWeb\Assets\Editor\CreateDefaultFontAsset.cs` | Font tools |
| `C:\DofusWeb\Assets\Resources/I18n/{en,pt,es,fr}.json` | Login strings |
| `Tools/I18nParser/parse_i18n.py` | Binary localization parser |
| `Jondo.Unity.Server/Protocol/GameProtocol.proto` | Protobuf definitions |
| `PlayBrowser/WebClient/server.js` | Node.js: WS proxy + HAAPI proxy |
