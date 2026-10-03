# V03: Dofus 3 WebGL — Custom Element Factories, UI Visibility and USS

This guide documents ALL progress from the 15-16/Sep/2026 session, continuing from V02 (bundles and UI). Here are the advances on camera, custom element registration, link.xml, stripping/IL corruption issues, and the current state.

---

## 1. Camera for Clear Color (RESOLVED)

### Problem
WebGL showed a black screen even with UI elements of real size ([360x32], [360x25]). Without a camera in the scene, the render background is pure black and UI elements (which may have transparent backgrounds) are invisible.

### Solution
Added via WebGLProbe a Camera with:
- `CameraClearFlags.SolidColor` — fills background with solid color
- Clear color: dark indigo `(0.06, 0.06, 0.10, 1.0)` — dark but not pure black, allows distinguishing elements
- `depth = -100` — renders before any other camera
- `cullingMask = 0` — does not render any 3D layer, only clears the background

Result: visible background, UI elements now contrast against the dark indigo instead of black-on-black.

---

## 2. Custom Element Factory Registration (RESOLVED)

### Problem (recap from V2)
13 custom Dofus types (DofusVisualElement, DofusButtonCustom, WindowFigma, etc.) had no registered factory. Unity 6 threw an error:
```
Element 'Core.UILogic.Components.Figma.DofusVisualElement' is missing a UxmlElementAttribute
and has no registered factory method.
```
All fell back to Label, losing visual representation and behavior.

### Solution: Direct injection into the s_Factories dictionary

**13/13 types registered:**

| # | Type | Namespace |
|---|------|-----------|
| 1 | DofusVisualElement | Core.UILogic.Components.Figma |
| 2 | DofusBindableElement | Core.UILogic.Components.Figma |
| 3 | DofusButtonCustom | Core.UILogic.Components.Figma |
| 4 | WindowFigma | Core.UILogic.Components.Figma |
| 5 | Divider | Core.UILogic.Components.Figma |
| 6 | PlayButton | Core.UILogic.Components.Figma |
| 7 | TextInput | Core.UILogic.Components.Figma |
| 8 | TextInputToValidate | Core.UILogic.Components.Figma |
| 9 | Dropdown | Core.UILogic.Components.Figma |
| 10 | DropdownButton | Core.UILogic.Components.Figma |
| 11 | DofusIcon | Core.UILogic.Components.Figma |
| 12 | CircularGauge | Core.UILogic.Components.Figma |
| 13 | DofusLabel | Core.UILogic.Components |

### How it works

1. Class `CustomElementFactory` inherits from `UxmlFactory<VisualElement>` (deprecated API but functional as fallback)
2. Via reflection, accesses `VisualElementFactoryRegistry.s_Factories` (private static field, `Dictionary<string, List<IUxmlFactory>>`)
3. For each type, creates an instance of `CustomElementFactory` and inserts into the dictionary using the type's fullname as key
4. Result: zero "missing factory" errors in the console

### Why UxmlFactory<VisualElement> and not UxmlFactory<T>?
Because the constructors of derived types are corrupted by stripping (see sections 3 and 5). The factory needs to create a plain `VisualElement` and apply traits (name, classes, styles) on top. Using `UxmlFactory<DofusButtonCustom>` would call the corrupted constructor and crash.

---

## 3. Stripped Constructors — Root Cause (FOUND)

### Problem
With `ManagedStrippingLevel.Medium` + `preserve="nothing"` in link.xml, the linker removed parameterless constructors (`.ctor()`) from ALL subclasses of VisualElement — except DofusVisualElement.

### Error
```
Default constructor not found for type X
```

### Investigation

**FormatterServices.GetUninitializedObject:** works for all 13 types — creates the instance without calling any constructor. BUT it cannot be used because:

- The `VisualElement` (base class) constructor has **virtual dispatch** that calls overrides in derived classes
- Those overrides have corrupted/empty IL from stripping
- Result: `contentContainer` becomes corrupted, the element doesn't work

### Conclusion
Stripping with `preserve="nothing"` is necessary to avoid VTable conflicts (see V1), but kills constructors of custom elements. The solution is via selective link.xml (see section 6).

---

## 4. m_SerializedData — Native Barrier (FOUND, NOT FIXABLE AT RUNTIME)

### Background
When Unity 6 does `CloneTree()` on a VisualTreeAsset, each `VisualElementAsset` has a field `m_SerializedData` decorated with `[SerializeReference]`. Unity uses this field to decide the instantiation path:

```
if (m_SerializedData != null)
    -> CreateInstance() directly (uses m_SerializedData, IGNORES factory)
else
    -> Factory path (calls the factory registered in s_Factories)
```

### Problem
- `m_SerializedData` is stored in **Unity's native memory** (backing store of [SerializeReference])
- `CreateInstance()` returns a plain `VisualElement` (corrupted IL body), and `Deserialize()` applies nothing (no names, no CSS classes)
- Via managed reflection, the field APPEARS null, but Unity reads from the native backing store
- **It is NOT possible to set it to null via reflection** — the managed field is just a facade

### Consequence
As long as `m_SerializedData` exists in the assets, the factory registered in `s_Factories` is NEVER called. Unity always takes the `CreateInstance()` shortcut.

### Solution: link.xml preserve="all" (see section 6)
When the UxmlSerializedData types are preserved with `preserve="all"`, paradoxically `m_SerializedData` becomes null at runtime — forcing the factory path.

---

## 5. IL Corruption in Constructors (FOUND)

### Problem
Even when constructors are preserved (via link.xml), the IL bodies recovered by Cpp2IL/MergeIL are corrupted for custom elements.

### Mechanism
1. `VisualElement` base ctor has virtual dispatch: calls virtual methods that are overridden in derived classes
2. The overrides in derived classes have empty/broken IL (recovery failed on those methods)
3. When the base ctor runs, it dispatches to the corrupted override
4. Result: `contentContainer` ends up null or pointing to an invalid object

### Manifestation
```csharp
// Any of these approaches corrupts contentContainer:
new DofusButtonCustom()                              // direct ctor
Activator.CreateInstance(typeof(DofusButtonCustom))   // reflection
GetUninitializedObject + ctor.Invoke()                // manual
```

### Only safe option
`base.Create(bag, cc)` inside the factory, which calls `new VisualElement()` directly — without virtual dispatch to corrupted overrides.

---

## 6. link.xml preserve="all" — The Key Discovery

### What changed
Adding `preserve="all"` not only for the 13 custom element types, but ALSO for their inner `UxmlSerializedData` classes:

```xml
<linker>
  <!-- Custom element types -->
  <assembly fullname="Core">
    <type fullname="Core.UILogic.Components.Figma.DofusVisualElement" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.DofusBindableElement" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.DofusButtonCustom" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.WindowFigma" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.Divider" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.PlayButton" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.TextInput" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.TextInputToValidate" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.Dropdown" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.DropdownButton" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.DofusIcon" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.CircularGauge" preserve="all"/>
    <type fullname="Core.UILogic.Components.DofusLabel" preserve="all"/>

    <!-- UxmlSerializedData inner classes (CRITICAL!) -->
    <type fullname="Core.UILogic.Components.Figma.DofusVisualElement/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.DofusBindableElement/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.DofusButtonCustom/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.WindowFigma/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.Divider/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.PlayButton/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.TextInput/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.TextInputToValidate/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.Dropdown/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.DropdownButton/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.DofusIcon/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.Figma.CircularGauge/UxmlSerializedData" preserve="all"/>
    <type fullname="Core.UILogic.Components.DofusLabel/UxmlSerializedData" preserve="all"/>
  </assembly>
  <!-- ... rest of assemblies with preserve="nothing" as before -->
</linker>
```

### Effect

| Without preserve="all" | With preserve="all" |
|-------------------------|----------------------|
| m_SerializedData populated (native) | m_SerializedData = null |
| CloneTree uses CreateInstance() | CloneTree uses Factory path |
| Factory ignored | Factory called |
| Elements without name/classes | Elements with name, classes, styles from UXML |
| CloneTree generates errors | CloneTree succeeds, 0 errors |

### Why does preserve="all" make m_SerializedData become null?
Hypothesis: when the linker preserves UxmlSerializedData types, IL2CPP generates different code for serialization. Without preserve, the types are stripped and the runtime can't deserialize -> the native backing store contains garbage that APPEARS populated. With preserve, the type exists but the original serialized data (which came from corrupted IL) can't populate the field -> genuine null.

---

## 7. What Did NOT Work (tried and discarded)

Complete list of approaches that failed, documented to avoid repeating them:

### 7.1 UxmlSerializedDataRegistry.Register()
- Attempt: register the UxmlSerializedData via public API
- Result: **method not found** via reflection (does not exist in the IL2CPP build)

### 7.2 VisualElementFactoryRegistry.RegisterFactory() via reflection
- Attempt: use the factory registration API
- Result: **worked** technically (factory inserted in dictionary), but the factory **was never called** because `m_SerializedData` has priority over the factory path

### 7.3 RuntimeHelpers.RunClassConstructor()
- Attempt: force execution of the .cctor (static constructor) of custom elements, expecting them to self-register
- Result: **.cctor empty** — no self-registration logic

### 7.4 FormatterServices.GetUninitializedObject + base ctor
- Attempt: create object without ctor, then call base ctor manually
- Result: **virtual dispatch corruption** — VisualElement ctor calls corrupted overrides, `contentContainer` breaks

### 7.5 NullifySerializedData via reflection
- Attempt: set `m_SerializedData = null` on VisualElementAsset via reflection
- Result: **impossible** — [SerializeReference] has native backing, the managed field already APPEARS null but Unity reads from native

### 7.6 link.xml preserve="all" + UxmlFactory<T> (with T = custom type)
- Attempt: typed factory that calls `new T()`
- Result: **corrupted ctor** corrupts contentContainer (even with preserve="all", the ctor IL body is broken)

### 7.7 link.xml preserve="all" + GetUninitializedObject + direct ctor
- Attempt: combine uninitialized object with manual ctor call
- Result: **same problem** with virtual dispatch — VisualElement base ctor dispatches to corrupted override

---

## 8. PostProcessLoginTree and Current State

### PostProcessLoginTree
With the factory creating plain `VisualElement` (instead of custom types), elements lose specific names and classes. Probe v16 includes `PostProcessLoginTree` which:
- Traverses the VisualElement tree after CloneTree
- Assigns hardcoded names based on the expected structure (from LoginBinding)
- Adds hardcoded CSS classes

### Current State (16/Sep/2026)

**Working:**
- Build WebGL compiles and runs in browser
- Camera with dark indigo clear color — visible background
- 13/13 custom element types registered as factories
- link.xml preserve="all" for custom types + UxmlSerializedData
- CloneTree succeeds with 0 errors
- 22 elements in the visual tree, 6 with real size
- 942 USS files loaded from all bundles
- PostProcessLoginTree applies names and classes
- Debug overlay with H toggle

**Main blocker: USS styles NOT applying**
- 942 USS files loaded
- But `background-color.a = 0.00` on all elements
- Styles defined in USS are not taking visual effect
- Elements have size (from inlineSheet) but no color/border/background

---

## 9. Key Files

| File | Purpose |
|------|---------|
| `C:\DofusWeb\Assets\WebGLProbe.cs` | Probe v16 — camera, factories, link.xml strategy |
| `C:\DofusWeb\Assets\link.xml` | preserve="all" for 13 custom types + 13 UxmlSerializedData |
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | Extract Win64 bundles + rebuild for WebGL |
| `C:\DofusWeb\Assets\Editor\WebGLBuildConfig.cs` | Build config (medium stripping, debug IL2CPP) |
| `PlayBrowser/WebClient/unity-build/` | WebGL build output |
| `PlayBrowser/WebClient/server.js` | Node.js server with WS proxy |

---

## 10. Next Steps

1. **Investigate why 942 USS files don't produce visual styles**
   - Are USS files empty/corrupted after bundle rebuild?
   - Verify actual content of USS inside WebGL bundles
2. **Check CSS selectors used by the game**
   - What classes/names do the USS files reference?
   - Do the names/classes assigned by PostProcessLoginTree match the selectors?
3. **If USS files are empty: re-extract/rebuild with correct USS serialization**
   - RebuildBundlesForWebGL saves USS as .asset (YAML serialized ScriptableObject)
   - Verify whether re-serialization preserves internal CSS rules
4. **Explore manual style application**
   - If USS doesn't work, apply inline styles via code as a temporary fallback
5. **Connect to emulator** — implement WebSocket bridge for TcpConnectionLayer

---

## Session Summary

| Metric | Value |
|--------|-------|
| Custom elements registered | 13/13 |
| "missing factory" errors | 0 (was 13+) |
| Elements in tree | 22 |
| Elements with size | 6 |
| USS files loaded | 942 |
| Visual styles applied | 0 (blocker) |
| Approaches tried and failed | 7 |
| Probe version | v16 |
