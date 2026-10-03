# V05: Dofus 3 WebGL — CSS Reconstruction, Fonts and Login Screen

## Summary

Session focused on CSS reconstruction, fonts, and login screen. We started from V4 (USS not applying, text not rendering) and arrived at:
1. CSS reconstruction working (Unity 6 per-rule selectors) — 228/242 USS reconstructed, 202 verified
2. Text renders with Lexend-Regular font (130 glyphs, 1024x1024 SDF atlas)
3. Krosmoz title screen background visible
4. PLAY button visible and clickable with text
5. Typed factories: DofusLabel→Label, PlayButton→Button, TextInput→TextField
6. Complete WebSocket bridge (.jslib + C#) with wiring on PLAY button
7. Font spam eliminated (TSS skipped, USS selective loading)

---

## 1. CSS Reconstruction — Unity 6 Per-Rule Selectors (RESOLVED)

### Problem (V4)
`StyleSheet.m_ComplexSelectors` did not exist — 942 USS loaded but zero styling.

### Discovery: Unity 6 changed the architecture
In Unity 6 (6000.x), `m_ComplexSelectors` **does not exist** in `StyleSheet`. It was moved to **each `StyleRule`**:

```
StyleSheet (Unity 6):
  m_Rules: StyleRule[]     ← [SerializeField]
  floats, dimensions, colors, strings, assets   ← flat value arrays
  m_Tables: Dictionary<string, StyleComplexSelector>[]  ← NOT serialized, computed at runtime
  
StyleRule:
  m_ComplexSelectors: StyleComplexSelector[]  ← HERE are the selectors
  m_Properties: StyleProperty[]
  
StyleProperty:
  m_Id: StylePropertyId (enum)  ← NOT string name!
  name: string (property getter converts m_Id → CSS name)
  m_Values: StyleValueHandle[]
```

### Solution: RebuildBundlesForWebGL v3

Rewritten to:
1. **Remove `.uss` from ImporterExtensions** — USS saved as CSS text, not as .uss.asset YAML
2. **Defer StyleSheet extraction** until refMap is complete (to resolve url() for assets)
3. **Try `StyleSheetToUss` built-in** via reflection (not found in Unity 6)
4. **Manual CSS reconstruction** from internal arrays:
   - Reads `m_Rules` → for each rule, reads `m_ComplexSelectors` (selectors) + `m_Properties` (properties)
   - Uses property `name` getter (converts `StylePropertyId` enum → CSS name)
   - Reconstructs selector text from `StyleSelector.m_Parts[]` (class/id/type/pseudo)
   - Reconstructs values: Float, Dimension (with unit), Color (rgba), Enum, Variable, Function, String
   - Generates imports from `ImportStruct.styleSheet`
5. **Saves as `.uss` text** → Unity reimports → generates `m_ComplexSelectors` automatically
6. **Forces re-extraction** of .uss (ignores "already extracted" check)
7. **Verifies** reimport: checks selectors per-rule in the imported asset

### Result
- 228/242 USS successfully reconstructed
- 202/228 verified with valid selectors after reimport
- 14 failed (no selectors = inline styles)
- 3 minor parsing errors (parentheses in administrablePopup, itemsSet, illustratedPopup)
- Inspection of original Win64 bundles: 229 SS with selectors, 714 without, 14571 rules, 3588 selectors

### CSS Reconstruction Errors
- `url()` with MissingAssetReference → `none` (cross-bundle asset not found)
- Functions without closing paren → funcDepth tracking
- Variables: `var(--name)` vs custom property declaration

---

## 2. Font: Lexend-Regular (RESOLVED)

### Problem
Text did not render. TSS (ThemeStyleSheet) had imports with zeroed GUIDs (broken cross-bundle refs). No font → no text.

### Investigation
1. USS files reference `url("Lexend-Regular")` for `-unity-font-definition`
2. `Find Font Assets in Bundles` → ZERO FontAsset found by type
3. But found: `uielements_assets_all: Assets/UI/Utils/Style/Fonts/verdana-normal SDF.asset`
4. The FontAsset in the bundle does NOT load via `LoadAsset` (platform-specific serialization)
5. Sub-assets DO load: Material + Texture2D (atlas)
6. LegacyRuntime font → FontAsset with `atlas=1x1 glyphs=0` (DontSave flags)
7. `Font.CreateDynamicFontFromOSFont("Arial", 32)` → dynamic=True but CreateFontAsset → 0 glyphs

### Solution: Download Lexend-Regular.ttf
```
Build > 1. Download Lexend Font  → downloads Lexend-Regular.ttf from Google Fonts (97KB)
Build > 2. Create FontAsset from Lexend → FontAsset SDF (90pt, SDFAA, 1024x1024)
```
- **130 glyphs** pre-populated (A-Z, a-z, 0-9, symbols, PT/ES accents)
- Saved in `Assets/Resources/DefaultFont.asset` with atlas + material as sub-assets
- Loaded in probe via `Resources.Load<FontAsset>("DefaultFont")`
- Applied to root: `root.style.unityFontDefinition = new StyleFontDefinition(fontAsset)`
- **Text renders!** "PLAY" visible on blue button

### What did NOT work for fonts
| Attempt | Result |
|---|---|
| `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` | Finds it, but atlas=1x1, glyphs=0 |
| `new Font("Arial")` | dynamic=False, 0 glyphs |
| `FontAsset.CreateFontAsset(builtinFont)` runtime | Returns FA but without functional atlas |
| `style.unityFont = legacyFont` | UI Toolkit ignores legacy Font |
| `FontDefinition.FromFont(font)` | Font→FontDefinition cast fails for FontAsset |
| `FontDefinition.FromObject(obj)` | Method does not exist in Unity 6 |
| Extract FontAsset from bundle | LoadAsset returns null (platform serialization) |
| Sub-assets from bundle (atlas+mat) | Without glyph table, useless |

---

## 3. Typed Factories (UPDATED)

### V4: Everything → VisualElement
The factory created `VisualElement` for all 13 types. DofusLabel did not render text because VisualElement has no text rendering.

### V5: Correct type per element

| Custom Type | Factory | Base Type | Why |
|---|---|---|---|
| DofusLabel | LabelFactory | `Label` | Renders text |
| DofusButtonCustom | ButtonFactory | `Button` | Clickable + text |
| PlayButton | ButtonFactory | `Button` | PLAY button |
| DropdownButton | ButtonFactory | `Button` | Clickable |
| TextInput | TextFieldFactory | `TextField` | Text input |
| TextInputToValidate | TextFieldFactory | `TextField` | Input |
| DofusVisualElement | VEFactory | `VisualElement` | Container |
| WindowFigma | VEFactory | `VisualElement` | Container |
| Divider | VEFactory | `VisualElement` | Separator |
| Dropdown | VEFactory | `VisualElement` | Container |
| DofusIcon | VEFactory | `VisualElement` | Icon |
| CircularGauge | VEFactory | `VisualElement` | Gauge |
| DofusBindableElement | VEFactory | `VisualElement` | Container |

### Implementation
```csharp
class VEFactory : UxmlFactory<VisualElement> { ... }
class LabelFactory : UxmlFactory<Label> { ... }
class ButtonFactory : UxmlFactory<Button> { ... }
class TextFieldFactory : UxmlFactory<TextField> { ... }
```
Each calls `base.Create(bag, cc)` which creates the correct type + applies traits (name, classes, styles).

---

## 4. Login Screen: Correct UXMLs (FIXED)

### V4: preGameMainMenu.uxml (WRONG)
We were loading the in-game menu, not the login screen.

### V5: connectionBackground.uxml + login.uxml (CORRECT)

Actual decompiled flow:
```
Compositor.Compose → bkqk:
  1. ConnectionBackground (AuthUis.8): connectionBackground.uxml + ParallaxBackground.prefab
  2. Login (AuthUis.0): login.uxml (Dofus logo, gradient, Play button, host dropdown)
```

The probe now:
1. Loads `connectionBackground.uxml` as base UIDocument
2. Loads Krosmoz `background.jpg` from `titlescreen_assets_all` → `root.style.backgroundImage`
3. Clones `login.uxml` onto root (overlay)
4. Populates elements with text/colors (PopulateLoginScreen)

### LoginBinding (from dump.cs line 195777)
```
#login, #ctr_gradient, #tx_dofusLogo, #ctr_error, #ctr_message, #lbl_message,
#ctr_login, #btn_play (PlayButton), #ctr_portSelection, #dd_host (Dropdown)
```

---

## 5. Theme Colors: FigmaThemeScriptable (PARTIAL)

### Problem
Classes `backgroundColor_*` and `textColor_*` do NOT exist in any USS file. They are generated at runtime by `FigmaThemeScriptable`:
```
FigmaThemeScriptable : ScriptableObject
  m_figmaColorsData: List<FigmaThemeColorData>
    varName: string  (e.g.: "background_medium90")
    color: Vector4   (RGBA)
```

### Current solution: hardcoded defaults (TEMPORARY)
ApplyThemeColors recursive walk applies inline colors based on class prefix:
- `backgroundColor_X` → `style.backgroundColor`
- `textColor_X` → `style.color`

### Fix needed
Find FigmaThemeScriptable in bundles and extract real colors. Or parse constants from dump.cs.

---

## 6. Font Spam (RESOLVED)

### Cause
USS files with `-unity-font-definition: none` (MissingAssetReference in CSS reconstruction) + TSS with zeroed imports → per-frame "Invalid value for font MissingAssetReference"

### Solution
1. **TSS skipped** — do not apply LargeFont_Standard.tss (broken imports)
2. **USS loading disabled** — not even the 202 with selectors (broken font refs)
3. Font via Resources/DefaultFont.asset (Lexend-Regular SDF)

---

## 7. WebSocket Bridge (READY, NOT TESTED)

### Infrastructure
- `Assets/Plugins/WebSocketBridge.jslib` — JS side complete (create/send/close/poll)
- `Assets/WebSocketBridge.cs` — C# side with Connection tracking + callbacks
- `WebGLProbe.Update()` calls `WebSocketBridge.PollAll()` every frame

### Wiring
PLAY button → `WebSocketBridge.Connect("ws://localhost:5555/game-ws")`:
- onOpen: "CONNECTED", TODO SpinProtocol handshake
- onData: log bytes, TODO protobuf parsing
- onClose/onError: status update

### Emulator
JondoEmu port 5555 accepts WebSocket (peek "GET " handshake). Node proxy on `/game-ws` → TCP 5555.

---

## 8. Complete Boot Flow (DECOMPILED)

### Actual sequence
```
Compositor.Awake → bkqj (InitCompositor)
  → bkpy (PreInitServices)
  → bkpz (InitializeServices) — [ServiceInit] methods
  → bkqa (GameCompositor.RegisterServices)
  → bkqe (LoadDataDependencies) — Addressables
  → bkqg → bkqk (Compose):
      1. Load DofusUISettings.asset
      2. Open ConnectionBackground (fyr): connectionBackground.uxml + ParallaxBackground.prefab
      3. Open Login (fys): login.uxml
```

### Launcher Parameters
```
--port 15881 --gameName dofus --gameRelease dofus3 --instanceId 12
--hash 84e6834e10e544acbb913fb679528425 --canLogin true
--langCode es --autoConnectType 1 --connectionPort 5555
```
Without launcher params → popup "connection to Ankama Launcher lost"
For WebGL: bypass (simulate params via probe)

### Auth Screen System
```
AuthUis enum: Login=0, ServerSelection=1, CharacterSelection=2,
  CharacterCreation=3, PreGameMenu=4, SecretPopup=5, PseudoChoice=6,
  CharacterHeader=7, ConnectionBackground=8, OptionLoadingError=9

fpm (Auth Screen Manager): manages Dictionary<AuthUis, screen handle>
fyr: ConnectionBackground handler
fys: Login handler (desktop)
fyt: Login handler (mobile)
fyv: PreGameMainMenu handler
```

### Connection Flow
```
Click Play → fys handler → eyl.xnv(user, pass) [get token]
  → ewz.vql(host, port) [connect]
    → SpinConnection.ConnectAsync
      → TcpConnectionLayer.ConnectAsync → TcpClient
        → SpinProtocol handshake → Authentication → ConnectionOpened
```

### Localization System
```
Content/I18n/es.bin, en.bin, fr.bin, pt.bin, de.bin
LocalizationTable.ReadFrom(path) → binary file
LocalizationTableHeader: Dictionary<int, uint> + Dictionary<Hash64, uint>
LocalizedStringUtilities.Localized(key) → resolved text
```

---

## 9. Key Type Mapping (Obfuscated → Real)

| Obfuscated | Purpose |
|---|---|
| ezp / glo | Service bus (bdgo=open screen, bdgw=send msg) |
| ewz | Network connection service (vql=connect, vrb=send) |
| eyl | Auth/token service (xnv=get token) |
| ewy | App config service |
| ewm | Options/settings service |
| faa | Server/account state |
| fbz | Auth screen state service (AuthUis enum) |
| fpm | Auth screen manager (concrete) |
| fca | Auth UI context |
| fzy\<T\> | Screen handler base class |
| gax\<T\> | Screen manager base class |
| fyr | ConnectionBackground screen handler |
| fys | Login screen handler (desktop) |
| fyt | Login screen handler (mobile) |
| fyv | PreGameMainMenu screen handler |
| ecr | Loading screen controller |
| ekz | Display/resolution service |
| fyh | Loading progress text |
| ecs | Loading phase enum (0-6) |

---

## 10. What NOT to Repeat (accumulated V1-V5)

| Attempt | Result |
|---|---|
| StyleSheet.m_ComplexSelectors | DOES NOT EXIST in Unity 6 (it is per-rule) |
| USS brute-force load 928 files | Font MissingAssetReference spam per-frame |
| TSS (LargeFont_Standard.tss) | Imports with zeroed GUIDs → font spam |
| GetUninitializedObject + ctor | Virtual dispatch corruption |
| FontAsset from LegacyRuntime | atlas=1x1 glyphs=0 (DontSave flags) |
| new Font("Arial") | dynamic=False |
| FontDefinition.FromObject() | Does not exist in Unity 6 |
| LoadAsset\<FontAsset\> from Win64 bundle | Platform-specific serialization, returns null |
| preGameMainMenu.uxml as login | WRONG — it is the in-game menu |

---

## 11. Current State (Sep 18, 2026)

```
[done] Krosmoz title screen background
[done] Text renders (Lexend-Regular, 130 glyphs, 1024x1024 SDF)
[done] connectionBackground.uxml + login.uxml (correct UXMLs)
[done] PLAY button visible with text, clickable
[done] 13/13 typed factories (Label/Button/TextField/VE)
[done] CSS reconstruction: 228/242 → 202 verified (Unity 6 per-rule)
[done] Theme colors inline (darkStone defaults)
[done] Font spam eliminated
[done] WebSocket bridge complete (jslib + C#)
[done] Console clean (only missing scripts one-time)
[done] Service graph ALIVE
[done] Probe v25

[pending] USS loading disabled (broken font refs in 202 USS)
[pending] Theme colors hardcoded (FigmaThemeScriptable not extracted)
[pending] Element text hardcoded (localization not implemented)
[pending] WebSocket not tested with emulator
[pending] Screen flow simplified (no splash/loading)
```

---

## 12. Next Steps (Priority)

### No hardcoding — extraction tools:

1. **Font Path Fix** — place Lexend FontAsset where USS expects it → re-enable USS → real game visual
2. **I18n Parser** (Python) — parse `Content/I18n/en.bin` and `pt.bin` → JSON → apply at runtime
3. **Theme Color Extractor** — parse FigmaThemeScriptable from bundles or constants from dump.cs → generate USS
4. **Screen Registry Parser** — parse dump.cs → extract all fzy\<T\>, bindings, UXML paths → JSON
5. **WebSocket connection** — test with JondoEmu, implement SpinProtocol handshake
6. **USS font fix** — replace `url("Lexend-Regular")` in reconstructed USS with correct FontAsset path

---

## 13. Key Files

| File | Description |
|---|---|
| `C:\DofusWeb\Assets\WebGLProbe.cs` | Probe v25 — typed factories, login flow, font loading |
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | v3 — CSS reconstruction Unity 6 per-rule |
| `C:\DofusWeb\Assets\Editor\CreateDefaultFontAsset.cs` | Download Lexend + create SDF FontAsset |
| `C:\DofusWeb\Assets\Resources\DefaultFont.asset` | Lexend-Regular SDF (130 glyphs, 1024x1024) |
| `C:\DofusWeb\Assets\Resources\Fonts\Lexend-Regular.ttf` | Source font file |
| `C:\DofusWeb\Assets\link.xml` | preserve="all" for 13 types + UxmlSerializedData |
| `C:\DofusWeb\Assets\WebSocketBridge.cs` | C# WebSocket bridge |
| `C:\DofusWeb\Assets\Plugins\WebSocketBridge.jslib` | JS WebSocket implementation |
| `Cliente 3.6.10.11\Dofus_Data\StreamingAssets\Content\I18n\*.bin` | Localization binary files |
| `Il2CppDumper\...\dump.cs` | 1.98M lines, all class structures |
| `Il2CppDumper\...\decompiled_all.c` | 964MB Ghidra pseudo-C |
