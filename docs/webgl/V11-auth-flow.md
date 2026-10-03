# V11: Dofus 3 WebGL — Full Session 7 (22-29/Sep/2026)

## Summary

Massive session focused on MergedUSS, CSS var(), sprites, variable tracing, real protocol, and test infrastructure. We started from V10 (PlayButton visible but without full flow) and arrived at:
1. **Full auth flow**: Login -> ServerSelection -> CharacterSelection with real protocol
2. **Opcode parsing**: kof, kvi, kqz detected by URI prefix `type.ankama.com/`
3. **2-phase reconnect**: ticket extracted from ServerSelected, reconnection with kqz
4. **Universal variable tracing**: NAM/CLS/SPR keys from decompiled_all.c
5. **~340 lines dead code removed**: BuildLoginUI, AddTypeClasses, _screenUXML, etc.
6. **Infrastructure**: Unity MCP + Playwright MCP headless + start-webgl.sh

---

## 1. MergedUSS Extraction (COMPLETE)

### Problem
MergedUSS (4255 rules) was a sub-asset of ThemeStyleSheets in the Win64 tss_assets_all. ThemeStyleSheet inherits from StyleSheet in Unity 6 — `LoadAllAssets<StyleSheet>()` returned the TSS, not the MergedUSS.

### Solution
1. `asset is ThemeStyleSheet` check BEFORE `asset is StyleSheet` in the main extraction loop -> SaveAsAsset
2. Pass 1.25: SerializedObject scan of each ThemeStyleSheet -> finds StyleSheet refs (MergedUSS, default, font-size variants, theme-color variants)
3. CSS reconstruction + bundle build

### Result
- 15 TSS import StyleSheets extracted
- tss_assets_all: 1271KB (was 4KB)
- MergedUSS: 4242 rules loaded in the browser

---

## 2. CSS var() Fix (COMPLETE)

### Root Cause (verified via real debug on MergedUSS)
```
Function handle (vType=10):
  strings[valueIndex] = "transparent"  <- initial value, NOT the funcId
  floats[valueIndex]  = 0             <- does NOT contain StyleValueFunction

Next Float handle (vType=2):
  floats[valueIndex]  = 1.0           <- StyleValueFunction.Var IS HERE
```

### Fix: peek at next Float handle
```csharp
case 10: // Function
    int funcId = 0;
    if (i + 1 < values.Length) {
        var peekH = values.GetValue(i + 1);
        if (peekType == 2) funcId = (int)floats[peekIdx];
    }
    switch (funcId) { case 1: "var("; case 2: "env("; ... }
```

### StyleValueFunction enum (dump.cs L1131756)
Unknown=0, Var=1, Env=2, LinearGradient=3, filters=4-13, MaterialProperty=14

---

## 3. Universal Variable Tracing (COMPLETE)

### Problem
Ghidra places `__a__` assignments at the top of the function, consumers 100+ lines later. Proximity-based detection fails.

### Solution: trace pSVarX -> consumer
```python
# In analyze_client.py:
# 1. Map variable assignments: pSVarX = __a__KEY
# 2. Check which consumer uses pSVarX (10-line window):
#    - set_name(el, pSVarX) -> NAM key (element name)
#    - AddToClassList(el, pSVarX) -> CLS key (USS class)
#    - AddressableEntry___ctor(..., pSVarX, ...) -> SPR key (sprite)
```

### Result
- PlayButton: 9/9 sprites correct (NAM->element names, SPR->sprite names)
- Dropdown: dropdown_dropdown instead of dropdown (NAM/CLS corrected)
- PortraitRanking: 0 false positives (SPR uses Enum.ToString, not OPS string)

---

## 4. Real Protocol (COMPLETE)

### Connection Server (port 5555 phase 1)
- Raw protobuf messages (no Push wrapper, no opcode)
- AuthAccepted: f2.f3 present (account info) — distinguished from ServerSelected (f2.f4)
- ServerSelected: f2.f4.f1.f1 = ticket string

### Game Server (port 5555 phase 2, reconnect with ticket)
- Wrapped messages: Push("opcode", payload) with `type.ankama.com/` prefix
- kqz: { f2: ticket_string } — from GameNodeProxy.HandleTicketPresentation L1204
- Welcome burst: kra, lqu, hoy, kqu, mgq, mgt, hpd, krs, mgz
- CharacterListFrames: kqp x3, kvi (character list), jtg (gift catalogue)
- kvw: { f1: characterId } — character select

### Sources
| Format | File | Line |
|--------|------|------|
| Push envelope | ConnectionProtocol.cs | L36-44 |
| AuthAccepted | ConnectionProtocol.cs | L48-98 |
| ServerSelected | ConnectionProtocol.cs | L169-184 |
| Welcome burst | ConnectionProtocol.cs | L229-288 |
| kvi (char list) | ConnectionProtocol.cs | L373-385 |
| kqz (ticket) | GameNodeProxy.cs | L1200-1218 |
| Phase detection | GameServerProxy.cs | L108-121 |

---

## 5. Login Flow (from dump.cs fys handler)

### Based on:
- `fys` handler (dump.cs L386161): `fzy<LoginBinding>`
- `fyq` enum (L386068): idle=0, connecting=1, connected=2
- `ConnectionBackgroundBinding` (L25814): _connectionBackground, _img, _iconLoading

### Implementation:
1. Click Play -> btn_play.SetEnabled(false) + iconLoading visible
2. HAAPI auth (POST /api/entrar) -> token
3. Launch (POST /api/lanzamiento) -> session
4. WS1 connect -> auth frame -> AuthAccepted (kof equivalent)
5. Server select -> ServerSelected -> ticket -> WS1 close
6. WS2 connect -> kqz(ticket) -> welcome burst -> kvi -> CharacterSelection

---

## 6. Dead Code Removed (~340 lines)

| Removed | Lines | Reason |
|---------|-------|--------|
| `BuildLoginUI` | ~120 | Programmatic login with made-up colors |
| `StyleTextField` | ~30 | Hardcoded inline styles |
| `ApplyLoginVisuals_DISABLED` | ~120 | Texture lookup by made-up name |
| `AddTypeClasses` | ~30 | Made-up CSS classes ("play-button", "figma-button") |
| `ApplyDebugStyles` | ~40 | Hardcoded colors/borders |
| `_screenUXML` dictionary | ~10 | Hardcoded UXML paths -> assetPaths from ClientData |
| `SetLoginStatus` (old) | ~5 | Changed button text -> now uses iconLoading |

---

## 7. Test Infrastructure

### Services
```bash
bash PlayBrowser/start-webgl.sh  # Kills ports 3000/5555/8888, starts emulator + node
```

### Unity MCP (builds)
- `unity open C:/DofusWeb` — opens Editor
- `mcp__unity-editor-mcp__build target=WebGL` — build (~7 min)
- `mcp__unity-editor-mcp__menu path="Build/1 - Extract + Rebuild for WebGL"` — rebuild bundles

### Playwright MCP (headless validation)
- `mcp__playwright-browser__browser_navigate url=http://localhost:3000`
- `mcp__playwright-browser__browser_take_screenshot` — visual capture
- `mcp__playwright-browser__browser_console_messages level=info` — probe logs

---

## 8. Confirmed Visual State (Playwright screenshot)

Login screen captured automatically shows:
- Dofus logo with sprites
- Krosmoz background (parallax)
- Green PlayButton [280x132] with "Play" text and 9 sprites
- Dropdown [234x12] with chevron
- Vertical gradient
- 0 errors, 56 warnings (pre-existing missing scripts)

---

## 9. Accumulated Knowledge V1-V10 (Critical for Future Sessions)

This session (7) built on discoveries from sessions 1-6. Summary of what is resolved and what NOT to repeat:

### Resolved Architecture (DO NOT touch)
| Component | Session | State |
|---|---|---|
| Pipeline IL2CPP -> WebGL (MergeIL, FixPInvoke, FixComObject) | V1 | STABLE — 158k methods, 779 P/Invokes, 654 [Preserve] |
| Bundle rebuild Win64 -> WebGL (RebuildBundlesForWebGL) | V2-V5 | v5+ — two-pass, inline tex, USS tex, remap 1397 refs |
| Custom element factories (s_Factories injection) | V3-V9 | 4 DDFactory data-driven (226 types), zero hardcode |
| link.xml preserve="all" for UxmlSerializedData | V3-V4 | CRITICAL — without this m_SerializedData blocks factory path |
| CSS reconstruction Unity 6 per-rule selectors | V5 | 228/242 USS, MergedUSS 4255 rules added V10 |
| Font Lexend-Regular SDF (Resources/DefaultFont) | V5-V6 | 130 glyphs, atlas can be 1x1 in build (V7 note) |
| I18n parser (339k strings, 5 languages) | V6 | Tools/I18nParser/, Resources/I18n/{en,pt,es,fr}.json |
| Theme colors (25 Standard colors via UnityPy) | V8 | Resources/ThemeColors.json, 11 themes extracted |
| ClientAnalyzer + OPS decrypt (28,517 strings) | V9 | Tools/ClientAnalyzer/, ClientData_compact.json |
| MergedUSS + CSS var() fix | V10 | SerializedObject scan, peek next Float for funcId |
| Variable tracing NAM/CLS/SPR | V10-V11 | pSVarX->consumer, zero false positives |

### What Does NOT Work / Do NOT Repeat (accumulated V1-V11)
| Attempt | Result | Session |
|---|---|---|
| `preserve="all"` on ALL assemblies | VTable duplicate (__Il2CppComObject) | V1 |
| `GetUninitializedObject` + ctor | Virtual dispatch corruption (contentContainer null) | V3 |
| `UxmlSerializedDataRegistry.Register()` | Method not found in IL2CPP | V3 |
| Nullify m_SerializedData via reflection | Native [SerializeReference], managed field is a facade | V3 |
| `UxmlFactory<T>` with T=custom type | Ctor body IL corrupted | V3 |
| `StyleSheet.m_ComplexSelectors` top-level | DOES NOT EXIST in Unity 6 (is per-rule) | V4-V5 |
| USS brute-force load (928 files) | Font MissingAssetReference spam per-frame | V4 |
| TSS imports with zeroed GUIDs | Font spam | V5 |
| `FontAsset` from Win64 bundle via LoadAsset | Platform-specific serialization, returns null | V5 |
| `Font.CreateDynamicFontFromOSFont` | 0 glyphs | V5 |
| `preGameMainMenu.uxml` as login | WRONG — it is the in-game menu, login is `login.uxml` | V5 |
| FigmaThemeScriptable from bundles via Editor | MonoScript GUID mismatch (V8 -> UnityPy fix) | V7-V8 |
| `data_assets_all` in WebGL | Never existed (V8 confirmed GUID mismatch) | V8-V10 |
| Made-up element names | Do not match USS — use OPS strings (28,517) | V9 |
| Made-up USS classes ("dofus-button") | Do not exist — use const fields from dump.cs | V9 |
| Heuristics by name ("contains Button") | Fails — use real inheritance from dump.cs | V9 |
| Hardcode regex for CSS keywords | Rejected — read real binary format | V10 |
| Detect messages by size (data.Length) | Made up — parse protobuf structure (f2.f3 vs f2.f4) | V11 |
| Hardcode _screenUXML dictionary | Made up — use assetPaths from ClientData.json (385 paths) | V11 |

### m_SerializedData (V3-V4, V9 — understood, not fixed)
Managed references from the SerializedFile contain attribute overrides (e.g., `loginButton` in login.uxml). With `preserve="all"`, m_SerializedData becomes null -> factory path -> DEFAULT names from constructor (playButton_*). The MergedUSS matches the default names. To get the overrides (loginButton), we would need to parse the SerializedFile binary format — complex, not a priority.

---

## 10. Known Issues

### InvalidOperationException: Collection was modified
- Happens during WS reconnection (ShowScreen iterates _loadedBundles while callback modifies)
- Partial fix: ToList() in FindUxmlPath and ShowScreen
- Does not block — flow continues

### No characters on server 290
- kvi payload empty (26 bytes = envelope only)
- Need to create a character or select a server with existing characters

### Auto-select server/character
- Selects first automatically (temporary for testing flow)
- Original: user interacts with ServerSelection UI

### Font atlas 1x1 in WebGL build (V7)
- DefaultFont.asset loads but atlas texture is 1x1 in build — glyphs via fallback (LegacyRuntime)
- Rebuilding the FontAsset SDF with complete glyphs may fix it

---

## 11. Key Files

| File | Description |
|------|-------------|
| `C:\DofusWeb\Assets\WebGLProbe.cs` | Probe v28: factories, sprites, protocol, screen transitions |
| `C:\DofusWeb\Assets\WebSocketBridge.cs` | WS + HTTP + protobuf helpers (BuildProtobufFrame, kqz) |
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | ThemeStyleSheet routing, TSS imports, CSS Function fix |
| `Tools/ClientAnalyzer/analyze_client.py` | Variable tracing NAM/CLS/SPR, build_child_tree |
| `Tools/ClientAnalyzer/output/ClientData_compact.json` | 287 factories with spr fields and correct names |
| `PlayBrowser/start-webgl.sh` | Start emulator + node server |
| `PlayBrowser/screenshots/login-screen.png` | Screenshot validated via Playwright |

---

## 12. Next Steps (Priority)

1. **Create character** — server 290 has no chars. Implement creation flow (kvb) or select a server with existing chars
2. **Populate ServerSelection UI** — show server names in the list (data in f2.f3.f1.f4 of AuthAccepted)
3. **Populate CharacterSelection UI** — show character names/levels (data in kvi)
4. **World entry** — after kvw, process kva (character selected) and world messages
5. **Fix InvalidOperationException** — more ToList() copies on collection iterations
6. **Remove auto-select** — wiring click handlers on ServerSelection and CharacterSelection UXMLs
7. **Font atlas fix** — rebuild SDF with complete glyphs, or use original .ttf extracted via UnityPy (V8)
8. **m_SerializedData extraction** (optional) — parse managed references from SerializedFile via UnityPy for attribute overrides of ALL UXMLs
