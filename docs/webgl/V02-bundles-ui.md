# V02: Dofus 3 WebGL — Asset Bundles, UI Toolkit and Login Screen

This guide documents ALL progress from the 14-15/Sep/2026 session, continuing from V01 (pipeline). Here are the advances on bundles, UI Toolkit, decompiled bootstrap flow, and the current state.

---

## 1. Asset Bundles: Platform Mismatch (RESOLVED)

### Problem
The asset bundles extracted by AssetRipper were in `StreamingAssets/aa/StandaloneWindows64/` (build target 19). On WebGL (target 13), Unity refuses to load: `"The file can not be loaded because it was created for another build target. File's Build target is: 19"`.

### Solution: Extract and Rebuild for WebGL

**Tool created: `Assets/Editor/RebuildBundlesForWebGL.cs`**

Menu: **Build > 1 - Extract + Rebuild for WebGL**

Flow:
1. Loads each Win64 bundle in the Editor via `AssetBundle.LoadFromFile()` (works because the Editor runs on Windows)
2. For each asset in the bundle, extracts and saves to the project:
   - **Textures** -> `EncodeToPNG/JPG` (with RenderTexture blit for non-readable)
   - **TextAssets** -> `File.WriteAllBytes`
   - **Prefabs** -> `PrefabUtility.SaveAsPrefabAsset` (fails on prefabs with missing scripts, OK)
   - **UXML/USS/TSS/InputActions** -> Saved as `.asset` (NOT with original extension!), using `addressableNames` to map back to the original path
   - **ScriptableObjects** -> `AssetDatabase.CreateAsset`
3. Rebuild with `BuildPipeline.BuildAssetBundles(DstDir, builds.ToArray(), ChunkBasedCompression, BuildTarget.WebGL)`
4. Output: `Assets/StreamingAssets/aa/WebGL/`

### Why .asset and not .uxml/.uss?
`CreateAsset()` with .uxml/.uss extension makes Unity try to REIMPORT the file as XML/CSS. But the content is YAML serialized (ScriptableObject), not XML -> import fails -> empty asset. Saving as `.asset`, Unity treats it as a serialized ScriptableObject and preserves all compiled content.

### addressableNames
The `AssetBundleBuild` has an `addressableNames[]` field that maps load names. Thus `bundle.LoadAsset("assets/ui/auth/pregamemainmenu.uxml")` works even though the file on disk is `.uxml.asset`.

### inlineSheet Sub-Asset (CRITICAL)
VisualTreeAsset has a sub-asset `inlineSheet` (StyleSheet) that contains ALL inline styles defined in the UXML. When we do `Object.Instantiate()` + `CreateAsset()`, the sub-asset is lost because `CreateAsset` only saves the main object.

**Fix implemented via SerializedObject:**
```csharp
var so = new SerializedObject(obj);
var prop = so.FindProperty("inlineSheet");
if (prop != null && prop.objectReferenceValue != null) {
    var subCopy = Object.Instantiate(prop.objectReferenceValue);
    AssetDatabase.AddObjectToAsset(subCopy, savePath);
    // Fix reference on the saved copy
    var copySO = new SerializedObject(copy);
    copySO.FindProperty("inlineSheet").objectReferenceValue = subCopy;
    copySO.ApplyModifiedPropertiesWithoutUndo();
}
```

**Result:** Elements now have real dimensions ([360x32], [360x25]) instead of [960x0].

### Resulting Bundles (20 total)
| Bundle | Size | Assets |
|--------|------|--------|
| titlescreen_assets_all | 8.7MB | 44 (parallax textures Japan/Krosmoz) |
| uielements_assets_all | 1MB | 904 (UXML/USS/prefabs UI) |
| ornaments_assets_all | 21.6MB | 1068 (ornaments data) |
| uiassets_assets_ | 13.8MB | 399 (ornament textures, icons) |
| uidarkstone_assets_all | 1.5MB | 616 (darkStone theme textures) |
| icons_assets_1x/2x | 403KB/643KB | 643/651 (icons) |
| figs_assets_ | 1.5MB | 48 (Figma images) |
| tss_assets_all | 4KB | 33 (ThemeStyleSheets) |
| gameassets_assets_all | 27KB | 1-9 (audio config, prefabs) |
| worldassets_assets_all | 40KB | 9-16 (camera, map prefabs) |
| + others | varied | networking certs, sprites, mobile, etc. |

### Cleanup between re-extractions
Before re-running Extract+Rebuild, delete old files:
```bash
cd C:/DofusWeb/Assets
find . -name "*.uxml.asset" -delete
find . -name "*.uss.asset" -delete  
find . -name "*.tss.asset" -delete
find . -name "*.inputactions.asset" -delete
# And the corresponding .meta files
```
Keep: `UI/PanelSettings.asset`, `UI/SmallScreenPanelSettings.asset`, `UI/SpriteAssets/`

---

## 2. Decompiled Bootstrap Flow (from dump.cs and Ghidra)

### Compositor (base class) — dump.cs line 405168
```
Fields:
  dxnw: Dictionary<Type, List<Compositor.FieldEntry>>  // field entries
  dxnx: Dictionary<Type, gar>                          // main services
  dxny: Dictionary<Type, gaq>                          // resettable services  
  dxnz: Dictionary<Type, gao>                          // pre-init services
  dxoa: List<dyk>                                      // ???
  isRunning: bool

Methods:
  Awake()          -> bkqj() (InitCompositor async)
  bkqa()           -> abstract, override in GameCompositor (register services)
  bkpy()           -> PreInitServices (iterate dxnz, async UniTask<bool>)
  bkpz()           -> InitializeServices (find [ServiceInit] methods, async UniTask)
  bkqe()           -> LoadDataDependencies (Addressables.InitializeAsync, virtual async)
  bkqg()           -> Compose (virtual async UniTask<bool>)
```

### GameCompositor — dump.cs line 405339
```
Fields:
  dxob: ezp (= glo, service bus)     offset 0x50
  dxoc: ecr (coordinator)            offset 0x58
  dxod: fyh (debug/log)              offset 0x60
  console: QuantumConsole             offset 0x68

Methods:
  bkqa()  -> Register services (FAILED TO DECOMPILE in Ghidra)
  bkqg()  -> Compose override (async, calls bkqk)
  bkqk()  -> Internal startup flow
```

### InitCompositor State Machine — dump.cs line 405058
```
Compositor.<InitCompositor>d__28 : IBufferMessage
  Fields: <>1__state, <>t__builder, <>4__this, <>u__1
  
Chain: Awake -> bkqj -> bkqa -> bkpy -> bkpz -> bkqe -> bkqg -> done
```

### InitializeServices State Machine — dump.cs line 405078
```
Compositor.<InitializeServices>d__17 : IMessage
  - Enumerates all services in dxnx (HashSet<gar>)
  - For each service, gets its Type
  - Finds methods with [ServiceInit] attribute via reflection
  - Calls each [ServiceInit] method (may return UniTask -> await)
  
Key [ServiceInit] methods found:
  ecr.vrt()   -> void (loading screen init)
  fyh.bjcg()  -> void (debug service init)
```

### ServiceInitAttribute — dump.cs line 405388
```csharp
[Usage(64)]
public class ServiceInitAttribute : Attribute { }
```

### glo (Service Bus) — dump.cs line 403775
```
Implements: ezp (interface), IEquatable<jan>, IBufferMessage
Fields:
  [DependsOnService] ewz dxng  // connection service
  int dxnh
  readonly glo.glj dxni        // dispatcher 1
  readonly glo.glk dxnj        // dispatcher 2
  readonly glo.gll dxnk        // dispatcher 3
  readonly glo.glm dxnl        // dispatcher 4
  readonly Queue<glo.gln> dxnm // message queue (capacity 15)
  bool dxnn

Key methods (from ezp interface):
  bdgm() / bdgn()              // lifecycle
  bdgo<a>(a)                   // open screen
  bdgp<b>(Type, b)             // open screen by type
  bdgq() -> glr                // get network dispatcher
  bdgt() -> glx                // get UI dispatcher
  bdgv() -> gmd                // get event listener
  bdgw<f>(f, gkk) -> UniTask   // send network message to client
  bdgz() -> glu                // get game event dispatcher
```

### ezp Interface — dump.cs line 336581
14 abstract methods for screen management, message dispatch, event handling.

### ecr (Coordinator) — dump.cs line 285586
Constructor takes (ezp, fyh). Loading screen service.

### Login Screen Flow
```
1. glo.bdgo<fys>()                    // open login screen
2. fys : fzy<LoginBinding>            // screen handler (dump.cs line 386161)
3. fzy<a> : IMessage                  // base screen class (dump.cs line 388926)
     - dwpq: VisualElement            // screen root
     - dwpr: gak                      // window wrapper
     - dwqc: glx                      // UI event dispatcher
     - dwqo: glr                      // network event dispatcher
4. LoginBinding (struct) fields:      // dump.cs line 195777
     _____root_____: VisualElement
     _login: VisualElement
     _ctr_gradient: VisualElement
     _tx_dofusLogo: DofusVisualElement
     _ctr_error: VisualElement
     _ctr_message: VisualElement
     _lbl_message: DofusLabel
     _ctr_login: VisualElement
     _btn_play: PlayButton
     _ctr_portSelection: VisualElement
     _dd_host: Dropdown
```

### PreGameMainMenuBinding — dump.cs line 33965
```
_preGameMainMenu: WindowFigma
_ctr_content: DofusVisualElement
_ctr_mainPart: DofusVisualElement
_ctr_options: DofusVisualElement
_btn_options: DofusButtonCustom
_btn_quit: DofusButtonCustom
_ctr_subPart / _ctr_sub_txt / _tx_shield
_lbl_sub_text: DofusLabel
_btn_sub: DofusButtonCustom
_ctr_footer: DofusVisualElement
_btn_close: DofusButtonCustom
_lbl_buildNumber: DofusLabel
```

### Asset Paths Constants — dump.cs line 39808 (AssetsPaths)
```
ddqz = "Assets/UI/PanelSettings.asset"
ddra = "Assets/UI/SmallScreenPanelSettings.asset"
ddrc = "Assets/Configuration/Audio/AudioManagerLibrary.asset"
ddrd = "Assets/Configuration/Audio/AudioManagerSettings.asset"
ddrf = "Assets/UI/DofusUISettings.asset"
ddwv = "Assets/UI/Auth/preGameMainMenu.uxml"
```

### PanelSettings — dump.cs line 1107628
```
k_DefaultStyleSheetPath = "Packages/com.unity.ui/PackageResources/StyleSheets/Generated/Default.tss.asset"
themeUss: ThemeStyleSheet         // offset 0x18 -- WAS BROKEN (GUID all zeros)
m_ScaleMode: PanelScaleMode       // offset 0x3C
m_ReferenceResolution: Vector2Int // offset 0x54 (1920x1080)
m_ScreenMatchMode                 // offset 0x5C
```

### DofusUISettings — dump.cs line 221438
Has AssetReferenceThemeStyleSheet fields for all font size variants:
- largeFontStandardTssRef, largeFontBontaTssRef, largeFontBrakmarTssRef...
- mediumFont*, smallFont* (30+ TSS references total)

### Display Config Service — dump.cs line 306220
```
dofz: ezp                        // service bus reference
[DependsOnService] ewm doga      // display dependency
dogd: PanelSettings               // the game's PanelSettings
dogs/dogt = 1920/1080             // reference resolution
dogw/dogx = 800/600               // minimum resolution
```

---

## 3. WebGLProbe Evolution (v7 -> v11)

### Probe v7-v8: Service Graph + Bundle Loading
- Inject GameCompositor via AddComponent
- Manually construct glo, fyh, ecr services
- Set isRunning = true
- Load StandaloneWindows64 bundles -> FAILED (target 19 mismatch)

### Probe v9: Original Boot Flow
- Followed decompiled Compositor flow: bkqa -> bkpy -> bkpz -> bkqe -> bkqg
- Register services in dxnx dictionary (4 services)
- Call [ServiceInit] methods via reflection
- Load WebGL bundles (after rebuild)
- Load PanelSettings from bundle
- Load preGameMainMenu.uxml from bundle
- Set UIDocument.visualTreeAsset
- Result: **0 children** — missing ThemeStyleSheet

### Probe v10: ThemeStyleSheet Fix
- Load LargeFont_Standard.tss from tss_assets_all bundle
- Set panelSettings.themeStyleSheet = tss
- "No Theme Style Sheet set" warning GONE
- Result: **1 child, but 0 height** (960x0) — missing inlineSheet

### Probe v11: Custom Element Registration + Overlay Toggle
- Attempt to register custom element factories (DofusVisualElement etc.)
- Three approaches tried: UxmlSerializedDataRegistry, VisualElementFactoryRegistry, RunClassConstructor — NONE worked
- Overlay shrunk to 40% width, auto-hides after 3s, toggle with H key
- Visual tree dump includes resolved sizes [widthxheight]
- After inlineSheet fix: elements show **[360x32], [360x25]** — real dimensions!

---

## 4. Custom UI Elements (from decompilation)

### Inheritance Chain
```
VisualElement
  +-- DofusVisualElement (Core.UILogic.Components.Figma)
  |     +-- DofusBindableElement : DofusVisualElement, IEquatable<...>
  |     |     +-- DofusButtonCustom
  |     |     +-- PlayButton
  |     |     +-- TextInput
  |     |     +-- Dropdown
  |     |     +-- TextInputToValidate
  |     +-- (direct subclasses)
  +-- WindowFigma (Core.UILogic.Components.Figma)
  +-- Divider : BindableElement (Core.UILogic.Components.Figma)
  +-- Label
        +-- DofusLabel (Core.UILogic.Components)
```

### UxmlSerializedData
Each custom element has an inner class `UxmlSerializedData` that Unity 6 uses for UXML instantiation:
- `DofusVisualElement.UxmlSerializedData : VisualElement.UxmlSerializedData`
- `DofusButtonCustom.UxmlSerializedData : DofusBindableElement.UxmlSerializedData`
- etc.

These exist in Core.dll but their factory registration doesn't work because the IL from decompilation is empty/broken.

### Error at Runtime
```
Element 'Core.UILogic.Components.Figma.DofusVisualElement' is missing a UxmlElementAttribute 
and has no registered factory method. Please ensure that you have the correct namespace imported.
```
All custom elements fall back to Label, losing their visual representation and behavior.

---

## 5. ThemeStyleSheet (TSS) System

### How It Works
- PanelSettings.themeUss references a ThemeStyleSheet
- Without it: "No Theme Style Sheet set to PanelSettings, UI will not render properly"
- The game uses DofusUISettings with 30+ TSS variants (per font size x theme)
- Default: LargeFont_Standard from tss_assets_all bundle

### PanelSettings.asset Fix
Original PanelSettings had `themeUss: {fileID: -1632188537672802429, guid: 00000000...}` — GUID all zeros because extraction broke the reference. Fix: load TSS from bundle at runtime and assign to panelSettings.themeStyleSheet.

---

## 6. Scene Structure

```
DofusUnity scene roots:
  EventSystem: [Transform] MISS=2 (InputModule + EventSystem scripts missing)
  Compositor: [Transform] MISS=1 (GameCompositor script missing -> injected by probe)
  RootUI: [Transform, UIDocument] (existing UIDocument, used for login screen)
  Quantum Console (SRP): [RectTransform, Canvas] MISS=6 (debug console, all scripts missing)
```

The RootUI UIDocument is the key — it's where the login screen UXML gets instantiated. The probe loads PanelSettings + TSS + preGameMainMenu.uxml and sets them on this UIDocument.

---

## 7. Current State (2026-09-15)

### Working
- Build WebGL compiles and runs in browser
- 158k methods with real IL (MergeIL), 779 P/Invokes fixed, 6035 .cctors fixed
- Service graph ALIVE (glo+ecr+fyh, isRunning=true, 4 services in dxnx)
- 20 WebGL bundles load (platform mismatch SOLVED)
- ThemeStyleSheet (LargeFont_Standard) applied
- preGameMainMenu.uxml loads and instantiates (1 root child)
- inlineSheet sub-asset preserved — elements have real sizes: [360x32], [360x25]
- UniTask .cctor OK
- Debug overlay toggles with H

### Current Blockers

**1. Screen is black — elements invisible**
Elements have non-zero height but nothing renders visually. Possible causes:
- Root background is transparent (black on black)
- No camera clear color
- Custom elements fall back to empty Labels

**2. Custom element factories not registered**
DofusVisualElement, DofusButtonCustom, WindowFigma, DofusLabel, Divider, PlayButton, TextInput, Dropdown — all fall back to Label. Types exist in Core.dll with UxmlSerializedData inner classes, but factory registration is broken.

Attempted fixes that did NOT work:
- `UxmlSerializedDataRegistry.Register()` via reflection — method not found
- `VisualElementFactoryRegistry.RegisterFactory()` via reflection — method not found  
- `RuntimeHelpers.RunClassConstructor()` — .cctor empty, doesn't self-register

**3. "Invalid image specified"** — UI trying to load an image/texture with broken reference

---

## 8. Key Files

| File | Purpose |
|------|---------|
| `C:\DofusWeb\Assets\WebGLProbe.cs` | Probe v11 — bootstrap + UIDocument setup |
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | Extract Win64 bundles + rebuild for WebGL |
| `C:\DofusWeb\Assets\Editor\WebGLBuildConfig.cs` | Build config (medium stripping, debug IL2CPP) |
| `C:\DofusWeb\Assets\WebSocketBridge.cs` | WebSocket shim for TCP replacement |
| `C:\DofusWeb\Assets\Plugins\` | Merged DLLs with real IL |
| `Il2CppDumper/.../dump.cs` | 1.98M lines, all class structures |
| `Il2CppDumper/.../decompiled_all.c` | 964MB Ghidra pseudo-C |
| `Cpp2IL_Output/` | DLLs with IL recovery |
| `PlayBrowser/WebClient/server.js` | Node.js server with WS proxy |
| `PlayBrowser/WebClient/unity-build/` | WebGL build output |
| `Tools/MergeIL/`, `Tools/FixPInvoke/`, `Tools/FixCctor/` | Mono.Cecil tools |

---

## 9. Networking (unchanged from V1)

- `TcpConnectionLayer` -> needs WebSocket replacement
- Protocol: varint-prefixed protobuf with envelope `type.ankama.com/xxx`
- Emulator port 5555 accepts WebSocket (handshake detected)
- Node.js proxy: `/game-ws` -> TCP 5555

## 10. Next Steps

1. **Make something visible** — set background-color on root VisualElement, or create camera with clear color
2. **Register custom element factories** — investigate Unity 6's actual [UxmlElement] registration mechanism
3. **Fix "Invalid image specified"** — check broken texture references in USS
4. **Connect to emulator** — implement WebSocket bridge for TcpConnectionLayer
5. **Handle Addressables** — catalog loading bypass already in place (direct bundle loading)
