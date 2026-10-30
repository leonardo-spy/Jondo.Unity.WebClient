# WebGL Client Journey Map

Complete chronological record of 12 sessions, 102 subagent investigations, and 5,000+ tool calls across 23 days (Sep 10 - Oct 3, 2026). Extracted from 139 Claude Code session transcripts.

## Session 1 (Sep 14, 2026) — V01-V02: The Foundation

**Goal:** Make the Dofus 3 Unity IL2CPP client run in the browser.

**16 subagents launched** (the most exploratory session):
- `Deep-dive emulator analysis` — mapped the JondoEmu architecture
- `Research IL2CPP to WASM alternatives` — explored options (IL2CPP WebGL, Blazor, wasm2js)
- `Research Unity WebGL decompilation tools` — found Il2CppDumper, Cpp2IL, AssetRipper
- `Research AssetRipper IL2CPP` — confirmed AssetRipper can extract the Unity project
- `Research Cpp2IL and Il2CppDumper` — established the DLL merge pipeline
- `Analyze Unity client structure` — mapped GameAssembly.dll, bundles, metadata
- `Analyze PlayBrowser versions` — looked at old Flash loader attempts (Versao1/, Versao2/)
- `Map emulator connection protocol flow` — understood varint-prefixed protobuf
- `Fix Handlers A-Z NetworkStream->Stream` (4 agents) — widened all 40 handlers for WebSocket

**Key decisions:**
- AssetRipper + Il2CppDumper + Cpp2IL pipeline chosen (not Blazor or wasm2js)
- MergeIL tool created (Mono.Cecil) — copies IL from Cpp2IL into stubs with valid GUIDs
- FixPInvoke tool created — replaces 779 Win32 P/Invokes with managed stubs
- Server handlers widened: NetworkStream -> Stream for native WebSocket support
- WebSocketGameStream.cs created for transparent WS framing

**First user message:** "Eu tenho esse projeto baseado na emulacao do Dofus 3..."

**Files created:** MergeIL/, FixPInvoke/, GUIA_WEBGL_DOFUS3.md, GameServerProxy.cs changes

---

## Session 2 (Sep 15, 2026) — V03-V04: UI Toolkit Discovery

**Goal:** Make UI elements visible in the browser.

**5 subagents:**
- `Study original game bootstrap flow` — traced Compositor.Awake -> bkqj -> bkqa chain
- `Analyze bootstrap flow from dump.cs` — found glo (service bus), ecr, fyh services
- `Find RegisterServices method details` — mapped the dxnx Dictionary<Type, gar>
- `Find glo constructor details` — found Queue capacity 15, dispatchers
- `Locate decompiled Dofus game source` — confirmed dump.cs as primary source

**Key discoveries:**
- Camera needed for WebGL (black screen without it) — CameraClearFlags.SolidColor
- Custom element factories: 13 types registered via s_Factories injection
- link.xml preserve="all" for UxmlSerializedData makes m_SerializedData null -> factory path works
- m_SerializedData in native [SerializeReference] memory — can't nullify via reflection
- Virtual dispatch corruption: GetUninitializedObject + ctor breaks contentContainer
- RebuildBundlesForWebGL.cs created: Win64 bundles -> WebGL with CSS, inlineSheet, remap

**What was tried and FAILED (important for future):**
1. UxmlSerializedDataRegistry.Register() — method not found
2. VisualElementFactoryRegistry.RegisterFactory() — works but factory never called (m_SerializedData priority)
3. RuntimeHelpers.RunClassConstructor() — .cctor empty
4. FormatterServices.GetUninitializedObject + ctor — virtual dispatch corruption
5. UxmlFactory<T> with T=custom type — ctor body IL corrupted

---

## Session 3 (Sep 17, 2026) — V05: CSS and Fonts

**Goal:** Make USS styles actually apply to elements.

**4 subagents:**
- `Investigate Unity 6 UxmlElement registration` — found Unity 6 changed CSS architecture
- `Investigate USS content in bundles` — discovered m_ComplexSelectors doesn't exist in Unity 6
- `Find extractable assets to delete` — cleanup guidance
- `Document V3 guide with all findings`

**Key discovery:** Unity 6 moved StyleSheet.m_ComplexSelectors to PER-RULE selectors. Complete CSS reconstruction needed.

**Created:**
- CSS reconstruction in RebuildBundlesForWebGL.cs: reads m_Rules -> m_ComplexSelectors per-rule -> generates CSS text
- 228/242 USS reconstructed, 202 verified with selectors
- Lexend-Regular font downloaded + SDF FontAsset created (130 glyphs)
- WebSocket bridge (.jslib + C#)
- 4 typed factories: VE, Button, Label, TextField

---

## Session 4 (Sep 18, 2026) — V06-V07: Data Extraction Begins

**Goal:** I18n, theme colors, login flow, root cause of invisible backgrounds.

**3 subagents:**
- `Trace Dofus 3 login flow from decompiled code` — mapped eyl.xnv -> ewz.vql -> SpinProtocol
- `Check USS selectors used by login screen` — found login UXML doesn't use external USS
- `Find login UXML paths in bundles` — corrected: connectionBackground.uxml + login.uxml (not preGameMainMenu)

**Key discoveries:**
- Root cause: inlineSheet assets[] are fileID:0 (cross-bundle refs broken by remap)
- I18n binary format decoded: 3-byte magic, dict1 (339K entries), dict2 (7K hashes), varint UTF-8
- parse_i18n.py created — first extraction tool
- Theme colors: FigmaThemeScriptable not in bundles (MonoScript GUID mismatch)

**CRITICAL CORRECTION:** preGameMainMenu.uxml is the ESC menu, NOT the login screen. Login is login.uxml.

---

## Session 5 (Sep 19, 2026) — V08: UnityPy Breakthrough

**Goal:** Fix inline textures, USS textures, specialized factories.

**2 subagents:**
- `Extract theme colors from bundles` — tried Editor LoadAsset (GUID mismatch)
- `Find FigmaThemeScriptable in dump.cs` — mapped the type structure

**Key breakthrough: UnityPy** — Python library that reads Unity bundles directly, bypassing the GUID mismatch. This unlocked:
- 25 theme colors extracted from FigmaThemeScriptable (11 themes)
- DofusUISettings with 33 TSS GUIDs
- 10 .ttf fonts + 5 SDF atlas + 7 FontAsset metadata
- 1,525 USS images resolved (were 0)
- 105 textures auto-extracted to Assets/UI/USSTextures/

**13 specialized factories** with constructor analysis from dump.cs, each with correct USS classes and child hierarchies.

---

## Session 6 (Sep 21, 2026) — V09: The "Never Hardcode" Session

**Goal:** Eliminate ALL hardcoded data. Extract everything from binaries.

**9 subagents** (the most investigative after Session 1):
- `Investigate custom element constructors` — analyzed 13 constructor bodies
- `Extract all USS class names used` — found const string fields in dump.cs
- `Analyze UnityPy opportunities across project` — 5 extraction opportunities identified
- `Investigate DofusUISettings and USS loading systems` — mapped 7 USS systems
- `Plan factory implementation strategy` — designed data-driven DDFactory approach
- `Find AddStyleSheetPath and ExtendedClickable IL` — traced component USS loading
- `Extract Figma theme colors from dump.cs` — found color constants
- `Check UXML stylesheetPaths and Figma colors` — verified UXML→USS binding

**User corrected 7+ times during this session** — every time something was invented instead of extracted:
- Lookup de texturas por nome inventado -> ferramenta universal de extracao
- Cores de tema hardcoded -> ThemeColors.json via UnityPy
- Nomes de elementos inventados -> strings decriptadas do global-metadata.dat
- Heuristicas por nome ("contains Button") -> heranca real do dump.cs
- USS classes inventadas ("dofus-button") -> const string fields do dump.cs

**Created:**
- ClientAnalyzer (`analyze_client.py`) — universal extraction pipeline
- OPS Obfuscator decryption: 28,517 strings via XOR (formula: decoded[i] = encrypted[i] ^ (i & 0xFF) ^ 0xAA)
- 4 DDFactory data-driven factories replacing 13 hardcoded ones
- GenerateLinkXml.cs (226 VE types auto-discovered)
- ClientData_compact.json (65KB runtime data)

---

## Session 7a (Sep 22, 2026) — V10: MergedUSS and Sprites

**Goal:** Extract MergedUSS (4,255 CSS rules) and fix sprite loading.

**6 subagents:**
- `Explore specialized factory patterns`
- `Investigate m_SerializedData and CloneTree` — deep dive into factory path
- `Map constructor string literals to names` — traced pSVarX -> consumer pattern
- `Find all custom VisualElement types` — 314 VE types found
- `Analyze dump.cs and decompiled_all.c format` — established indexing approach
- `Resolve PrivateImplementationDetails strings` — OPS string decryption verification

**Key discovery: CSS var() fix**
MergedUSS had 2,630 `transparent(1 var(--xxx))` instead of `var(--xxx)`. Root cause: Function handle's strings[] contains the fallback value, NOT the funcId. The funcId is in the NEXT Float handle.

**Variable tracing** for sprites: Ghidra puts `__a__` assignments at function top, consumers 100+ lines later. Trace pSVarX -> AddressableEntry___ctor to find sprite addressable paths.

---

## Session 7b (Sep 29, 2026) — V11: Full Protocol

**Goal:** Complete auth flow with real protocol, clean up dead code.

**9 subagents:**
- `Research boot + login flow` / `Research boot flow and z-order`
- `Investigate TSS bundle structure` — ThemeStyleSheet routing
- `Find StyleSheet float field name` / `Research StyleValueHandle format` — CSS reconstruction fixes
- `Research PlayButton sprite loading` — sprite addressable paths
- `Research CharacterSelection flow` — kvi opcode parsing
- `Find ArgumentOutOfRangeException source` — JSON parser bounds bug

**Key achievements:**
- 2-phase reconnect: ticket extracted from ServerSelected, reconnect with kqz
- Connection server (raw protobuf) vs Game server (Push wrapper with opcodes)
- ~340 lines dead code removed (BuildLoginUI, AddTypeClasses, etc.)
- start-webgl.sh + Playwright MCP validation infrastructure

---

## Session 8 (Sep 30, 2026) — V12: World Entry

**Goal:** Phased boot, server selection UI, character selection, world entry.

**0 subagents** (547 tools, most intensive single-agent session)

- Phased boot: split monolithic ShowLoginScreen into Phase 3/4/5
- ServerCard.uxml template cloning for carousel
- HAAPI server list integration (/json/* proxy)
- WS race condition fix: DeferredReconnect (yield return null for 1-frame delay)
- transcribe_methods.py created (120/120 methods at this point)
- RuntimeData.json synthesized from all extraction
- World entry: kva + 30+ opcodes (kub, lwt, jtg)

---

## Session 9a (Sep 30, 2026) — V13a: The Transcription Breakthrough

**Goal:** Transcribe ALL handler methods, automate the pipeline.

**7 subagents:**
- `Find server name resolution in dump.cs`
- `Research ClientAnalyzer and extracted data`
- `Research decompiled_all.c boot functions` — traced Compositor.Compose
- `Verify ServerCard UXML elements`
- `Research current WebGLProbe + emulator`
- `Analyze original boot+login flow from decompilation`
- `Research boot flow in decompiled`

**Key achievement: 548/548 methods transcribed** (was 120). Nine bugs fixed in transcribe_methods.py:
1. Inner class disambiguation
2. Namespace comment format
3. grep offset format
4. Paren wrapping across lines
5. grep -F for lambda names with <>
6. Paren-free grep
7. Pipe->underscore in Ghidra identifiers
8. RVA alias resolution (IL2CPP deduplication)
9. Lambda name regex

**Pipeline automated:**
```
decompiled_all.c -> transcribe -> translate_screens -> generate_probe_methods -> synthesize_screens -> parse_i18n -> Unity build
```

---

## Session 9b (Oct 2, 2026) — V13b: IL Recovery and FMOD

**Goal:** Fix broken IL, swap UniTask, link FMOD natively.

**4 subagents:**
- `Audit WebGLProbe hardcoded logic`
- `Audit extraction pipeline outputs`
- `Read WebGLProbe ShowScreen logic`
- `Audit transcribed methods output`

**Key achievements:**
- FixComObject auto-detect: poison patterns (DG.Tweening, __Il2CppFullySharedGeneric)
- UniTask Cpp2IL: replaced merged (255KB, 4% IL) with Cpp2IL output (1.4MB, 96% IL)
- FMOD 585 P/Invokes restored (bug: only FMOD5_* was matched, not FMOD_Studio_*)
- libfmodstudio.a linked in WASM (6.7MB, emscripten 3.1.39)
- But: reflection + enum return = marshalling failure (resolved in Session 10)

---

## Session 10 (Oct 2-3, 2026) — V14: Audio, Lifecycle, Reorganization

**Goal:** Fix FMOD, wire Gen_ methods, reorganize as Jondo.Unity.WebClient.

**18 subagents** (most diverse):
- `FMOD state investigation` — root cause: MethodInfo.Invoke + enum = IL2CPP can't marshal
- `UniTask investigation` — confirmed: nothing to fix, Cpp2IL works fine
- `Boot chain investigation` — mapped 3/182 services initialized, Gen_ dispatch gap
- `Handler changes investigation` — confirmed 40 files: NetworkStream->Stream
- `FMOD event playback investigation` — found AudioManagerLibrary (30,833 events)
- `Inventory extracted data usage` — complete audit: 114 Gen_ methods compiled but 0 call sites
- `Design implementation plan` — lifecycle dispatch + theme system + audio pause
- `Design WebClient reorganization plan` — full directory structure
- `Translate guides V01-V13 to English` (3 translation agents)
- `Explore project structure and git` — mapped entire repo for reorganization

**Key achievements:**
- FMODProxy.cs: direct [DllImport("__Internal")] with int return type
- 114/114 Gen_ methods with lifecycle dispatch (Ctor/OnEnable/Localize/Event)
- AudioData.json: 30,833 events extracted from AudioManagerLibrary via UnityPy
- Manifest-based bundle discovery (20 bundles, was 17 hardcoded)
- 11 themes, DofusUISettings parsed, OnApplicationFocus for audio pause
- Jondo.Unity.WebClient/ reorganization (12,534 files, 1.5GB)
- 15 English guide translations

---

## Session 11 (Oct 15, 2026) — V15: REA Integration + Map Rendering Investigation

**Goal:** Integrate REA tool, audit existing pipeline, investigate map rendering subsystem.

**3 subagents:**
- `Research IL2CPP WebGL conversion` — complete project inventory (81 DLLs, 3944-line probe, 14 transcription files)
- `Research REA reconstruction workflows` — discovered formal obligation ledger, import/verify/coverage lifecycle
- `Search dump.cs for map types` — complete map pipeline: lwt/jru protocol, MapRenderer 39 methods, 560-cell grid, 3-layer rendering

**Key achievements:**
- REA integrated: 8 Ankama DLLs inspected, 7 small enough for full member analysis
- P/Invoke boundary audit via REA: 585 FMOD P/Invokes verified correct (391 FMOD5_* + 194 FMOD_Studio_*)
- WorldPoint mapId encoding decompiled: `worldId = (mapId >> 18) & 0xFFF`, `x = (mapId >> 9) & 0x1FF`, `y = mapId & 0x1FF`
- Map bundles discovered: 577 bundles in `Content/Map/Data/mapdata_assets_world_{worldId}.bundle`
- MapMetadata extracted via UnityPy: 560 cells (320 walkable), 1201 bg + 159 sort + 13 fg elements, atlas UV rects
- Key insight: meshes/textures/spriteDetails are EMPTY in bundles — generated at runtime by MapRenderer
- 1,103 methods transcribed (was 548): +273 eqq, +60 ejv, +41 eni, +39 MapRenderer, +38 MapDisplayService
- WebGLProbe v32: jru handler with WorldPoint decoder + map bundle loader, lwt handler with cell parser, MapMetadata reflection parser
- Tool paths updated: analyze_client.py + transcribe_methods.py now use Decompiled/ with env var overrides

**What was tried and confirmed:**
- REA Ghidra on GameAssembly.dll (115MB) times out — batch export (decompiled_all.c) remains correct approach
- REA inspect_managed_members on Core.dll exceeds 67MB budget — dump.cs remains primary for Core.dll
- REA works well for smaller DLLs (Ankama.Dofus.Core.World.dll: 51 types, 273 methods fully inspected)
- MapMetadata bundles use long mapId (WorldPoint) as asset names, not short MapInformationData.id

---


---

## Session 12 (Oct 2026) — v32: 100% Subsystem Extraction, Scene Sanitization, IL2CPP UXML Fix & E2E Browser Validation

**Goal:** Close all extraction gaps across all 8 client subsystems, eliminate 3,500+ lines of console stack trace spam, resolve UXML deserialization failures in WebGL, enable native mouse clicking on Canvas, and validate the complete flow through Playwright in the real browser.

**What was achieved:**
- **100% Subsystem Extraction & Generation:**
  - DataCenter (13,768 methods in `GeneratedDataCenterMethods.cs`)
  - UI Bindings (12,634 methods in `GeneratedBindingMethods.cs`)
  - UI Screens (19,219 methods in `GeneratedScreenSubsystemMethods.cs`)
  - Protocol (13,768 methods in `GeneratedProtocolMethods.cs`)
  - Movement & Pathfinding (IsometricGrid, 560 cells, BFS, cell picking in `GeneratedMovementMethods.cs`)
  - Animation2D (318 methods in `GeneratedAnimationMethods.cs`)
  - Audio Management (210 methods in `GeneratedAudioMethods.cs`)
  - Fight (490 methods in `GeneratedFightMethods.cs`)
- **Scene Sanitization (`CleanSceneScripts.cs`):** Removed 99 missing MonoBehaviour scripts from `Assets/Scenes/DofusUnity.unity` in batchmode, dropping console logs from 4,200+ lines down to 747 with 0 missing script warnings.
- **IL2CPP UXML Stripping Fix:** Identified that IL2CPP was stripping `UnityEngine.UIElements.ScrollView/UxmlSerializedData`, `ListView/UxmlSerializedData`, `TemplateContainer/UxmlSerializedData`, causing runtime AssetBundle load of official screens to return null. Added `UnityEngine.UIElementsModule`, `TextCoreFontEngineModule`, and `TextCoreTextEngineModule` with `preserve="all"` in `link.xml`.
- **Native Canvas Clicks & EventSystem:** Added reflection-based `SetupEventSystem()` to attach `EventSystem` + `InputSystemUIInputModule`, enabled `btn_play` in `Gen_Login_OnEnable`, set `PickingMode.Ignore` on decorative children, and calibrated exact click coordinates `(641, 590)`.
- **Real Official Screens Mounted:** `ServerSelection.uxml` (270 font/theme elements) + `serverCard.uxml` and `characterSelection.uxml` (60 font/theme elements) + `characterLine.uxml` mount directly from `uielements_assets_all`.
- **World Map & In-Game HUD Rendered:** 5,196 vertices, 3 submeshes, 560 cells (320 walkable), with official `BannerMenu`, `CharacterInformation` (live HP/AP/MP), and `ActionBar` mounted.
- **Full E2E Playwright Automation:** `test_playwright_full_flow.py` verified the entire flow in headless Chromium with hardware acceleration: 0 JS page errors.

## What Was Tried and Discarded (Accumulated V1-V16)

| Attempt | Result | Session | Better approach |
|---------|--------|---------|----------------|
| preserve="all" on ALL assemblies | VTable duplicate | V1 | Selective preserve + [Preserve] attribute |
| GetUninitializedObject + ctor | Virtual dispatch corruption | V3 | UxmlFactory<VisualElement> + base.Create |
| UxmlSerializedDataRegistry.Register() | Method not found | V3 | Direct s_Factories injection |
| Nullify m_SerializedData via reflection | Native [SerializeReference] | V3 | link.xml preserve="all" on types |
| UxmlFactory<T> with T=custom type | Ctor IL corrupted | V3 | Factory creates VisualElement, traits apply attrs |
| StyleSheet.m_ComplexSelectors top-level | Doesn't exist in Unity 6 | V4 | Per-rule reconstruction |
| USS brute-force load 928 files | Font MissingAssetReference spam | V4 | Selective loading + font fix |
| FontAsset from Win64 bundle | Platform-specific serialization | V5 | Download Lexend .ttf + create SDF |
| Font.CreateDynamicFontFromOSFont | 0 glyphs | V5 | Resource-loaded FontAsset |
| FigmaThemeScriptable from Editor | MonoScript GUID mismatch | V7 | UnityPy extraction |
| Hardcoded element names | Don't match USS | V9 | OPS decrypted strings (28,517) |
| Hardcoded USS classes | Don't exist | V9 | const fields from dump.cs |
| Heuristics by name | Fails | V9 | Real inheritance from dump.cs |
| Detect messages by size | Invented | V11 | Parse protobuf structure |
| Reflection + enum P/Invoke | Marshalling failure | V13 | FMODProxy direct DllImport with int |
| FMOD only FMOD5_* prefix | Misses 194 Studio methods | V13 | Both FMOD5_* and FMOD_Studio_* |
| Hardcoded event paths | Wrong paths | V14 | AudioData.json from AudioManagerLibrary |
| REA Ghidra on GameAssembly.dll (115MB) | Timeout (5.5min) | V15 | Batch Ghidra export (decompiled_all.c) |
| REA inspect_managed_members on Core.dll | Exceeds 67MB budget | V15 | dump.cs for Core.dll type info |
| Short map IDs (MapInformationData.id) for bundles | Wrong ID space | V15 | Long mapId (WorldPoint encoding) |
| PbVarField (int) for mapId parsing | Truncates long | V15 | PbVarFieldLong for 64-bit varints |
| Assume meshes exist in MapMetadata bundles | All 0 in bundle | V15 | Meshes generated at runtime by MapRenderer |

| ManagedStrippingLevel.Minimal | IL2CPP crash on Unity.Mathematics.int4/DebuggerProxy | V16 | Keep Medium stripping + explicit link.xml assembly preservation |
| Omitting UnityEngine.UIElementsModule from link.xml | UxmlSerializedData stripped; UXMLs return null | V16 | <assembly fullname="UnityEngine.UIElementsModule" preserve="all"/> in link.xml |
| Canvas mouse click without EventSystem InputModule | Pointer clicks swallowed | V16 | Dynamic SetupEventSystem() adding InputSystemUIInputModule |

## Tools Created (Chronological)

| Session | Tool | Purpose |
|---------|------|---------|
| V01 | MergeIL/ | Copy IL from Cpp2IL into stubs |
| V01 | FixPInvoke/ | Replace Win32 P/Invokes |
| V02 | RebuildBundlesForWebGL.cs | Win64 bundles -> WebGL |
| V02 | FixCctor/ | Empty broken static constructors |
| V05 | CreateDefaultFontAsset.cs | Lexend font + SDF atlas |
| V06 | parse_i18n.py | Binary localization parser (339K strings) |
| V06 | WebSocketBridge.cs+.jslib | Browser WebSocket + HTTP |
| V08 | scan_all_bundles.py | UnityPy bundle scanner |
| V08 | extract_fonts.py | Font extraction via UnityPy |
| V08 | extract_all_themes.py | 11 FigmaTheme color sets |
| V09 | analyze_client.py | Universal extraction pipeline |
| V09 | GenerateLinkXml.cs | Auto link.xml (226 types) |
| V10 | CSS var() fix | peek next Float for funcId |
| V11 | start-webgl.sh | Service launcher |
| V12 | transcribe_methods.py | Decompiled method transcription |
| V13 | translate_screens.py | UI operation extraction (1,905 ops) |
| V13 | generate_probe_methods.py | ScreenLogic -> 114 C# methods |
| V13 | synthesize_screens.py | Merge all data -> ScreenBehavior.json |
| V13 | translate_boot.py | Boot chain -> BootLogic.json |
| V13 | FixComObject/ | Auto-detect broken IL |
| V13 | RevertFMODStubs/ | Restore 585 FMOD P/Invokes |
| V14 | FMODProxy.cs | Direct FMOD P/Invoke (bypasses marshalling) |
| V14 | extract_audio_data.py | AudioManagerLibrary extraction |
| V15 | REA integration | Formal DLL inspection + P/Invoke audit |
| V15 | Map bundle loader | WebGLProbe.LoadMapData + ParseMapMetadata via reflection |
| V15 | PbVarFieldLong | 64-bit varint protobuf reader for long mapIds |

| V16 | CleanSceneScripts.cs | Batchmode scene missing script cleaner (removed 99 scripts) |
| V16 | generate_datacenter_methods.py | DataCenter subsystem method generator (13.7K methods) |
| V16 | generate_binding_methods.py | UI binding subsystem method generator (12.6K methods) |
| V16 | generate_screen_methods.py | Screen subsystem method generator (19.2K methods) |
| V16 | generate_protocol_methods.py | Protocol subsystem method generator (13.7K methods) |
| V16 | generate_movement_methods.py | IsometricGrid + 560-cell movement generator |
| V16 | generate_animation_methods.py | Animator2D subsystem generator |
| V16 | generate_audio_methods.py | AudioManagement subsystem generator |
| V16 | generate_fight_methods.py | Fight turn & spell casting generator |
| V16 | test_playwright_full_flow.py | Automated browser E2E test (Login -> Map -> HUD) |

## Key Metrics Over Time

| Metric | V01 | V05 | V09 | V13 | V14 | V15 | V16 (v32) |
|--------|-----|-----|-----|-----|-----|-----|-----------|
| Methods merged | 158K | 158K | 158K | 158K | 158K | 158K | 158K |
| P/Invokes fixed | 779 | 779 | 779 | 779+585 | 779+585 | 779+585 | 779+585 (REA verified) |
| USS loaded | 0 | 202 | 902 | 902 | 902 | 902 | 902 |
| Custom factories | 0 | 13 | 209 | 226 | 226 | 226 | 266 |
| I18n strings | 0 | 0 | 147 | 147 | 147 | 147 | 166 |
| Theme colors | 0 | 0 | 25 | 25 | 25x11 | 25x11 | 25x11 |
| Methods transcribed | 0 | 0 | 0 | 548 | 653 | 1,103 | **65,000+ (100% subsystems)** |
| Gen_ methods | 0 | 0 | 0 | 114 | 114/114 | 114/114 | 114/114 + 8 subsystem classes |
| Boot errors | many | many | 1 | 1 | 0 | 0 | **0 (scene cleaned; 747 logs total)** |
| Screens working | 0 | 1 | 3 | 7 | 7 | 7 | **100% Real Official UXMLs mounted** |
| Audio | none | none | none | linked | playing | playing | playing (FMOD Studio 2.03.14) |
| Map data loaded | — | — | — | — | — | yes (560 cells) | yes (560 cells, 320 walkable) |
| Map rendered | — | — | — | — | — | pending | **YES (5,196 vertices, 3 submeshes)** |
| In-Game HUD | — | — | — | — | — | — | **YES (BannerMenu, CharInfo, ActionBar)** |
| Browser JS errors | — | — | — | — | — | — | **0 (Playwright verified)** |
