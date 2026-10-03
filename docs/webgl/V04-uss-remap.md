# V04: Dofus 3 WebGL — Full Session 2 (15-17/Sep/2026)

## Session Summary

This session focused on making the UI visible in the browser. We started from the black screen (V2) and reached:
1. Programmatic login screen visible and interactive (window, inputs, clickable PLAY button)
2. Game UXML instantiating with correct names, classes, and dimensions
3. 13/13 custom element factories registered
4. 1397 cross-asset references remapped in RebuildBundlesForWebGL
5. Root cause of USS not applying: m_ComplexSelectors absent from extracted StyleSheets

---

## 1. Camera (RESOLVED)
WebGL without a camera = black screen. Solution: Camera with `CameraClearFlags.SolidColor` (dark indigo), depth -100, cullingMask 0.

## 2. Custom Element Factories (RESOLVED)
13/13 types registered: DofusVisualElement, DofusBindableElement, DofusButtonCustom, WindowFigma, Divider, PlayButton, TextInput, TextInputToValidate, Dropdown, DropdownButton, DofusIcon, CircularGauge, DofusLabel.

- `CustomElementFactory` inherits `UxmlFactory<VisualElement>` (deprecated but functional as fallback)
- Injected directly into `VisualElementFactoryRegistry.s_Factories` under each type's fullname
- `base.Create(bag, cc)` creates a safe VisualElement + applies traits (name, classes, inline styles)
- Does NOT use GetUninitializedObject (virtual dispatch in VisualElement's ctor routes to broken overrides in derived types, corrupting contentContainer)

## 3. Stripped Constructors (ROOT CAUSE)
Medium stripping + `preserve="nothing"` removed parameterless constructors from ALL VisualElement subclasses except DofusVisualElement.
- `FormatterServices.GetUninitializedObject` works for all 13 types BUT cannot be used (virtual dispatch corruption)
- `link.xml preserve="all"` on types + UxmlSerializedData makes `m_SerializedData=null` -> forces factory path

## 4. m_SerializedData (UNDERSTOOD, NOT FIXABLE AT RUNTIME)
- Uses `[SerializeReference]` in Unity native memory — cannot be nullified via managed reflection
- `CreateInstance()` returns a plain VisualElement (broken IL), `Deserialize()` applies nothing
- WITH `link.xml preserve="all"`: m_SerializedData becomes null -> factory IS called -> traits apply UXML attributes

## 5. link.xml preserve="all" (KEY DISCOVERY)
Adding `preserve="all"` to the 13 types + 13 UxmlSerializedData inner classes:
- Makes m_SerializedData = null at runtime
- Forces CloneTree to use the factory path
- Factory creates a safe VisualElement + traits apply name/classes/styles
- Elements have names (#preGameMainMenu), classes (.backgroundColor_background_medium90), dimensions ([360x72])

## 6. RebuildBundlesForWebGL v2 — Reference Remap (IMPLEMENTED)
### Problem
`Object.Instantiate + AssetDatabase.CreateAsset` lost ALL cross-asset references:
- UXML->USS: 389 UXMLs with m_Stylesheets: [{fileID: 0}]
- TSS->USS: 33 TSS with guid: 00000000...
- USS->textures: 1994 broken references

### Solution: 3 passes
1. **Extract**: Same as before, but keeps bundles loaded + builds refMap (originalInstanceID -> savedPath)
2. **Remap**: For each extracted asset, uses SerializedObject on the ORIGINAL (live in memory) to find references. Looks up target in refMap. Updates the reference in the saved asset. **Skips self-references** (sub-assets like inlineSheet)
3. **Build**: Builds WebGL bundles

Result: **1397 references remapped, 0 failures**.

## 7. inlineSheet Sub-Asset (RESOLVED)
The remap pass initially corrupted the inlineSheet reference (sub-asset of itself). Fix: `if (targetSavePath == savePath) continue;` in the remap. Dimensions restored: [360x72], [360x48], [360x24].

## 8. USS NOT APPLYING (REMAINING BLOCKER)
942 USS loaded from all bundles, but bg.a=0.00. CSS rules exist in the files (229/242 have non-empty m_Rules) BUT the top-level `m_ComplexSelectors` array is absent — the runtime cannot perform selector matching.

**Root cause**: When StyleSheet is saved as a ScriptableObject YAML (.uss.asset), the m_ComplexSelectors (compiled binary format) is not preserved. Unity only generates it during import of .uss text -> CSS, not during loading of a serialized ScriptableObject.

**Required fix**: Recreate the USS as real CSS text (.uss) and let Unity reimport, OR find a way to force recompilation of m_ComplexSelectors in the StyleSheet after loading.

## 9. Programmatic Login Screen (FUNCTIONAL)
Bypass of the broken UXML/USS with UI built via code:
- Dark theme window with border radius
- "DOFUS" title + "JondoEmu -- WebGL" subtitle
- Green dot + "localhost:5555"
- Username/Password fields
- Clickable blue "PLAY" button (logs "connecting to emulator")
- Text does NOT render (TSS with broken font references -> no font loaded)

## 10. What Did NOT Work
| Attempt | Result |
|---------|--------|
| UxmlSerializedDataRegistry.Register() | Method not found |
| VisualElementFactoryRegistry.RegisterFactory() | Works but factory never called (m_SerializedData priority) |
| RuntimeHelpers.RunClassConstructor() | .cctor empty |
| GetUninitializedObject + base ctor | Virtual dispatch corruption |
| NullifySerializedData via reflection | Native [SerializeReference], managed field already null |
| link.xml + UxmlFactory<T> (new T()) | Broken ctor body corrupts contentContainer |
| link.xml + GetUninitializedObject + direct ctor | Same virtual dispatch |
| USS brute-force (942 files on root) | m_ComplexSelectors absent, zero matches |

## 11. Current State
```
[OK] Camera dark indigo
[OK] 13/13 factories (CustomFactory -> base.Create safe)
[OK] link.xml preserve="all" (m_SerializedData null -> factory path)
[OK] UXML CloneTree works (17 elements, names, classes, dimensions)
[OK] inlineSheet preserved ([360x72] etc.)
[OK] RebuildBundles with remap (1397 refs, 0 failures)
[OK] Programmatic login visible (window, inputs, PLAY button)
[BLOCKED] USS not applying (m_ComplexSelectors absent)
[BLOCKED] Text not rendering (font not loaded by TSS)
[BLOCKED] Real types not created (VisualElement fallback)
[BLOCKED] WebSocket connection to emulator (wired but not implemented)
```

## 12. Key Files
| File | Description |
|------|-------------|
| `C:\DofusWeb\Assets\WebGLProbe.cs` | Probe v18, CustomElementFactory, BuildLoginUI |
| `C:\DofusWeb\Assets\link.xml` | preserve="all" for 13 types + UxmlSerializedData |
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | v2 with 3-pass remap |
| `PlayBrowser/GUIA_WEBGL_DOFUS3.md` | V1 — full pipeline |
| `PlayBrowser/GUIA_WEBGL_DOFUS3_V2.md` | V2 — bundles, UI, bootstrap |
| `PlayBrowser/GUIA_WEBGL_DOFUS3_V3.md` | V3 — factories, discoveries |

## 13. Next Steps (Priority)
1. **Fix USS m_ComplexSelectors**: Extract CSS rules as .uss text and reimport, or force StyleSheet.compiledSelectors rebuild
2. **Fix font/TSS**: Load font from bundle and apply to PanelSettings
3. **WebSocket connection**: Implement OnPlayClicked -> WebSocketBridge -> emulator 5555
4. **Real types**: Fix constructor IL via Mono.Cecil (Tools/FixCtors)
