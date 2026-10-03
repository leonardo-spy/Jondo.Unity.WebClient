# V13: Dofus 3 WebGL — Session 9 (30/Sep-02/Oct/2026)

## Summary

Massive session: ~35 builds. Full automated decompiled->browser pipeline. 548/548 methods transcribed (100%). 1905 UI ops extracted. 114 Gen_ C# methods transpiled from decompiled. FNV-1a hash for I18n. UniTask Cpp2IL with real IL. FMOD Studio linked natively. FixComObject auto-detect for corrupted IL. BootLogic.json + ScreenLogic.json + ScreenBehavior.json.

---

## 1. Transcription Tool — 9 Bugs Fixed, 548/548 = 100%

`Tools/ClientAnalyzer/transcribe_methods.py` — 9 bugs found and fixed:

1. **Inner class**: `ServerSelection.<>c` matched before `ServerSelection` (dump.cs L262621 vs L262799)
2. **Namespace**: dump.cs uses `// Namespace: X.Y.Z` (comment, not keyword)
3. **grep offset**: `-b -n` format is `LINE:BYTE:CONTENT`, not `BYTE:LINE:CONTENT`
4. **Paren wrapping**: Ghidra breaks long signatures — `(` on the next line
5. **grep -F**: lambda names with `<>` need fixed-string, not regex
6. **Paren-free grep**: remove `(` from pattern because it can be on the next line
7. **Pipe->underscore**: Ghidra substitutes `|` with `_` in C identifiers (`|86_1` -> `_86_1`)
8. **RVA alias**: IL2CPP deduplicates methods — `OnCancelModifications` = `<.ctor>b__72_0` (same RVA)
9. **Lambda name regex**: `(<\w+>[\w|]+)` to capture `<InitializeTooltips>g__ShowEquipmentManipulator|86_1`

### Result: 548 methods, 100% coverage

| Type | OK | Coverage |
|---|---|---|
| fys (Login) | 49 | 100% |
| fyv (PreGameMainMenu) | 26 | 100% |
| fyr (ConnectionBackground) | 16 | 100% |
| fpm (Auth manager) | 161 | 100% |
| ServerSelection | 97 | 100% |
| CharacterSelection | 64 | 100% |
| CharacterCreation | 124 | 100% |
| CharacterHeader | 11 | 100% |

---

## 2. Decompiled -> Browser Pipeline

```
decompiled_all.c (964MB)
  | transcribe_methods.py (548/548)
transcribed/*.json (1.7MB, 8 files)
  | translate_screens.py (NEW)
ScreenLogic.json (297KB, 1905 ops: 854 BIND_GET, 81 DISPLAY, 71 I18N_SET, 70 EVENT, 130 ENABLE)
  | generate_probe_methods.py (NEW)
GeneratedScreenMethods.cs (1167 lines, 114 C# methods)
  | synthesize_screens.py (expanded)
ScreenBehavior.json (86KB, 7 screens, 312 elements, 35 I18n)
  | parse_i18n.py (FNV-1a hash)
I18n/*.json (147 keys x 4 langs: en/es/fr/pt)
  | translate_boot.py (NEW)
BootLogic.json (42KB, 6 phases, 182 services, 8 audio steps)
  | WebGLProbe.cs (partial) + GeneratedScreenMethods.cs (partial)
  | Unity WebGL Build
Browser OK
```

---

## 3. New Tools (Session 9)

### translate_screens.py
Extracts ALL UI ops from decompiled_all.c for each screen handler. 1905 ops:
- BIND_GET (854) — binding field accesses
- I18N_SET (71) — Localized() -> set_text with I18n key
- DISPLAY (81) — pca(el,bool) + DOTween pdb/pdd fallback
- EVENT (70) — RegisterCallback, add_clicked
- ENABLE (130) — SetEnabled(true/false)
- UIUtils mapped: pca=display, pdb/pdd/pdc=DOTween->Flex, pcc/pcd=picking, pbg=cleanup

### generate_probe_methods.py
Transpiles ScreenLogic.json -> 114 C# methods. Selective dispatcher calls only INIT methods (OnEnable, Localize, ctor). State-change methods remain available but are not called during init.

WebGL-specific: `SKIP_WEBGL = {("Login", "OnEnable")}` — fys.OnEnable disables btn_play/dd_host expecting the launcher. In WebGL there is no launcher.

### translate_boot.py
Traces the boot chain from Compositor.Compose with 6 phases + audio init.

### synthesize_screens.py (expanded)
Merges ScreenLogic (automatic ops) + bindings + I18n + screen_handlers. ZERO hardcoded I18n — everything comes from the decompiled source.

### parse_i18n.py (FNV-1a hash)
Discovery: `Core.Localization.Utils.HashHelper.Fnv1A` uses standard FNV-1a 64-bit.
- Offset basis: `0xcbf29ce484222325`, Prime: `0x100000001b3`
- Operates on UTF-16 chars (each char is uint16)
- 147/147 string keys resolved for 4 languages

---

## 4. UniTask — Cpp2IL with Real IL

**Problem**: UniTask.dll merged had only 4% of IL (255KB vs 1.4MB Cpp2IL).
**Solution**: Replace with Cpp2IL output directly.
**Result**: UniTask works in WebGL (1 harmless warning: "UNITASK: Object reference not set").

The warning comes from UniTask's .cctor referencing something uninitialized. Does not block boot or flow. UniTask 2.5.x is officially compatible with WebGL (runs on PlayerLoop, no threads).

**Files**:
- `C:\DofusWeb\Assets\Plugins\UniTask.dll` — 1.4MB (Cpp2IL, was 255KB merged)
- `C:\DofusWeb\Assets\Plugins\UniTask.Addressables.dll` — 39KB (Cpp2IL, was 7KB merged)
- Backups: `UniTask.dll.bak.merged`

---

## 5. FMOD Audio — Linked but Marshalling Pending

### What works
- `libfmodstudio.a` (6.7MB, FMOD 2.03.14, emscripten 3.1.39) linked in WASM
- 585 FMOD P/Invokes restored:
  - 391 `FMOD5_*` (core API) — were already P/Invoke with `__Internal`
  - 194 `FMOD_Studio_*` (studio API) — were STUBS, restored by RevertFMODStubs
- `FMOD_Studio_System_Create` P/Invoke found and called via reflection
- Bank files served via HTTP: `/assets/Audio/Banks/Desktop/Master.bank` (138MB)

### What does NOT work yet
- `FMOD_Studio_System_Create` via reflection returns error: `Cannot marshal type 'FMOD.RESULT'`
- Cause: FMOD.RESULT is an enum in Ankama.AudioManagement.dll and marshalling via reflection with enum return type fails in IL2CPP WebGL
- FMOD init is in `OnPlayClicked` (deferred for user gesture — browser WebAudio policy)
- Should be in boot (Phase 2) but needs marshalling resolved first

### Bug found thanks to another AI
`RevertFMODStubs` only looked for `FMOD5_*` (391 methods). The Studio methods had prefix `FMOD_Studio_*` (194 methods) that were NOT detected -> remained as stubs `return default`. Fixed by adding both prefixes.

### Next steps for FMOD
1. **Marshalling**: instead of reflection, use a direct C# wrapper (create FMODProxy.cs that calls P/Invokes without reflection)
2. **AudioManager assets**: AudioManagerSettings + AudioManagerLibrary are not in the bundles (MonoScript GUID mismatch). Extract via UnityPy or create a wrapper that initializes FMOD without them
3. **Bank loading**: Master.bank needs to be loaded asynchronously via HTTP. FMOD WebGL uses fetch -> memory -> loadBankMemory
4. **Move init to boot**: remove from OnPlayClicked and place in Phase 2 (after bundles)

### FMOD Files
- `C:\DofusWeb\Assets\Plugins\WebGL\libfmodstudio.a` — native lib (6.7MB)
- `C:\DofusWeb\Assets\Plugins\Ankama.AudioManagement.dll` — 585 P/Invokes (391 core + 194 studio)
- `Tools/RevertFMODStubs/` — tool to restore FMOD P/Invokes
- `Tools/FMODUnity/` — clone of the FMOD for Unity repo (reference only)
- `Tools/FMODFix/README.md` — process documentation

---

## 6. FixComObject — Auto-detect Corrupted IL

### Root Causes of Corrupted IL (from decompiled_all.c)
- **DG.Tweening** refs: Cpp2IL generates IL with DOTween generics (`TweenerCore<Color,Color,ColorOptions>`) that the linker rejects
- **`__Il2CppFullySharedGenericType`**: IL2CPP placeholder that does not exist in managed assemblies

### Auto-detect (no hardcoded list)
FixComObject scans ALL instructions and looks for "poison patterns":
```csharp
var poisonPatterns = new[] { "DG.Tweening", "__Il2CppFullySharedGeneric", "__Generated" };
```
Finds 12 methods (DofusTweenerVisualElement getters/setters) and replaces with clean bodies. Build passes.

### Types investigated
| Type | Cpp2IL raw (44MB) | Merged (7.2MB) | Root Cause |
|---|---|---|---|
| TitleScreenTweener | Corrupted X | OK | DOTween |
| StyledTextElement | Corrupted X | OK | SharedGeneric |
| TextElement | Corrupted X | OK | DOTween |
| EntityIcon | Corrupted X (metadata) | **OK without fix** OK | Cpp2IL metadata only |
| FrustumShape | Corrupted X | OK | DOTween |
| MapTransition | Corrupted X | OK | SharedGeneric |
| DofusTweenerVisualElement | N/A | **Auto-fixed** | DOTween Ease/LoopType |

### Core.dll Cpp2IL raw (44MB)
- Mono.Cecil CANNOT rewrite: "Value does not fall within the expected range"
- MergeIL transfers 106997/106997 methods (100%) but many bodies are empty
- The merged version (7.2MB) works and CAN be rewritten by Cecil
- FixComObject auto-detect works on the merged version

---

## 7. UIUtils Function Map (from decompiled_all.c)

| Function | Signature | Purpose |
|---|---|---|
| pca(el, bool) | display toggle | `b^1` -> DisplayStyle (true=Flex, false=None) |
| pcc(el) | pickingMode | set Ignore |
| pcd(el) | pickingMode | set Position |
| pdb(el, dur, offset) | Tween | fade-in (DOTween) -> fallback Flex |
| pdc(el, dur, offset) | Tween | slide (DOTween) -> fallback Flex |
| pdd(el, dur, offset) | Tween | fade-in (DOTween) -> fallback Flex |
| pbg(el) | cleanup | remove event registrations |
| ozz(el, bool) | pickingMode | toggle |
| pad(el, bool) | custom data | store on element |
| pai(el) | clear styles | reset inline |

---

## 8. Boot Chain (from decompiled Compositor.Compose)

```
Compositor.Awake -> bkqj (InitCompositor async)
  -> bkqa: RegisterServices (glo service bus, ecr coordinator, fyh debug)
  -> bkpy: PreInitServices (iterate dxnz Dictionary<Type, gao>)
  -> bkpz: InitializeServices (182 services with [ServiceInit])
    -> AudioManager.Initialize(settings, library) — FMOD Studio init
  -> bkqe: LoadDataDependencies (Addressables.InitializeAsync)
  -> bkqk: GameCompositor specific
    -> Load DofusUISettings.asset
    -> fpm opens ConnectionBackground (AuthUis.8 -> fyr.OnEnable)
    -> fpm opens Login (AuthUis.0 -> fys.OnEnable)
  -> bkqc(3) -> bkqh -> bkqc(4) -> isRunning=true
```

Probe replicates with synchronous phases (UniTask async state machines corrupted in Core.dll merged).

---

## 9. Playwright Testing via SendMessage

### Setup
- `index.html` patched: `window.unityInstance = unityInstance;`
- `WebGLProbe.TestAction(string action)` — callable via `SendMessage`
- `Probe_ExposeTestHook()` in jslib — exposes `window.triggerProbe`

### Flow
```javascript
window.unityInstance.SendMessage('__Probe', 'TestAction', 'enter');
// Login -> Play -> Server -> Character -> World Entry
```

---

## 10. Files Changed/Created

### New Tools
| Tool | Function |
|---|---|
| `Tools/ClientAnalyzer/translate_screens.py` | decompiled -> 1905 UI ops |
| `Tools/ClientAnalyzer/generate_probe_methods.py` | ScreenLogic -> 114 C# methods |
| `Tools/ClientAnalyzer/translate_boot.py` | Boot chain -> BootLogic.json |
| `Tools/FixComObject/` | Auto-detect + fix corrupted IL (poison patterns) |
| `Tools/RevertFMODStubs/` | Restore FMOD P/Invokes (585 total) |

### Unity Project (C:\DofusWeb\)
| File | Change |
|---|---|
| `WebGLProbe.cs` | 3601L. partial class, FMOD init, BootLogic, provenance comments |
| `GeneratedScreenMethods.cs` | 1167L. 114 Gen_ methods transpiled from decompiled |
| `WebSocketBridge.cs` | Probe_ExposeTestHook |
| `WebSocketBridge.jslib` | triggerProbe hook |
| `Plugins/UniTask.dll` | 1.4MB Cpp2IL (was 255KB merged) |
| `Plugins/WebGL/libfmodstudio.a` | 6.7MB FMOD native |
| `Plugins/Ankama.AudioManagement.dll` | 585 FMOD P/Invokes restored |
| `Resources/ScreenBehavior.json` | 86KB, 7 screens, 35 I18n from decompiled |
| `Resources/I18n/*.json` | 147 string keys x 4 langs (FNV-1a) |

### Generated data
| File | Size | Content |
|---|---|---|
| `output/ScreenLogic.json` | 297KB | 1905 ops, 308 methods, 8 screens |
| `output/ScreenBehavior.json` | 86KB | 7 screens, 312 elements, tabs, flow |
| `output/BootLogic.json` | 42KB | 6 phases, 182 services, audio init |
| `output/GeneratedScreenMethods.cs` | 1167L | 114 C# methods transpiled |
| `output/transcribed/*.json` | 1.7MB | 548 methods in 8 types |

---

## 11. What NOT to Repeat (V1-V13)

Everything from V1-V12 plus:
- Search for FMOD P/Invokes only with prefix `FMOD5_` (V13) — Studio API uses `FMOD_Studio_*` (194 methods)
- Apply enables indiscriminately from all methods (V13) — state-change methods disable everything
- Modify Cpp2IL Core.dll raw with Cecil (V13) — Cecil does not support the complex metadata
- Try to set `const` fields via reflection (V13) — `VERSION.number` is const, inlined in caller
- Call FMOD P/Invokes via reflection with enum return type (V13) — marshalling fails in IL2CPP WebGL
- Trust `result=0, ptr=0` as FMOD success (V13) — stub `return default` produces exactly that
- Ignore inner classes in `find_type_methods` (V9-V13) — `ServerSelection.<>c` vs `ServerSelection`

---

## 12. Pending for Next Session

### FMOD Audio (PRIORITY)
1. Create `FMODProxy.cs` with direct calls (no reflection) to avoid marshalling error
2. Or use the official fmod.cs/fmod_studio.cs wrappers from FMOD 2.03.14 (same version)
3. Resolve AudioManagerSettings/AudioManagerLibrary (UnityPy or wrapper without them)
4. Move init from OnPlayClicked to boot (Phase 2)
5. Test with real user click to validate WebAudio context

### Boot Faithful to Decompiled
1. BootLogic.json has the complete chain — use it to guide boot instead of hardcode
2. The 182 ServiceInit methods need to be called in the correct sequence
3. The Compositor.Compose async state machine is in the decompiled — transcribe it to coroutine

### Full Game Flow
1. Expand translate_screens.py for more screen handlers (inventory, map, fight, chat)
2. Transcribe World screen handlers (jru, jss, lwt, etc.)
3. Implement map rendering (lwt terrain data)
4. Implement character rendering (kub characteristics)

### Core.dll IL Recovery
1. Investigate why MergeIL transfers 100% methods but few bytes (empty bodies in Cpp2IL)
2. Re-run Cpp2IL with `--use-processor attributeinjector` for more IL recovery
3. Or use IL2CppInspector as an alternative to Cpp2IL
