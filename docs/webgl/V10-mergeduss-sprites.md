# V10: Dofus 3 WebGL — MergedUSS, CSS var(), Sprites (Session 7, 22/Sep/2026)

## Summary

Session focused on extracting the MergedUSS (4255 CSS rules) and fixing CSS reconstruction. We started from V9 (MergedUSS missing, PlayButton [280x0]) and arrived at:
1. **MergedUSS extracted** via SerializedObject scan of ThemeStyleSheets
2. **CSS var()** fixed: Function handle points to strings[], next Float contains StyleValueFunction enum
3. **PlayButton VISIBLE** with 9 sprites loaded from bundles by addressable path
4. **Variable tracing** in decompiled_all.c distinguishes SPR with OPS string vs SPR with field data
5. **Zero errors** (zero Assertion failed, zero ArgumentOutOfRange, zero 404)

---

## 1. MergedUSS Extraction

### Problem
The MergedUSS (4255 rules) was a sub-asset of ThemeStyleSheets in the Win64 tss_assets_all bundle. It had no container path — `GetAllAssetNames()` did not return it. `LoadAllAssets<StyleSheet>()` also did not — because `ThemeStyleSheet` inherits from `StyleSheet` in Unity 6.

### Solution: SerializedObject scan
For each ThemeStyleSheet loaded from the bundle:
1. `new SerializedObject(tss)` iterates ALL properties
2. Finds ObjectReferences to StyleSheet (that are not ThemeStyleSheet)
3. Checks if it has m_Rules with rules
4. Reconstructs as CSS text (ManualReconstructCSS) and saves as .uss
5. Adds to the bundle build

### ThemeStyleSheet routing
`ThemeStyleSheet` inherits from `StyleSheet` in Unity 6. Without the check, TSS files fell into the `asset is StyleSheet` branch — CSS reconstruction (which failed — TSS has no m_Rules). Fix: `asset is ThemeStyleSheet` check BEFORE `asset is StyleSheet` — route to `SaveAsAsset`.

### Result
- 15 TSS import StyleSheets extracted (MergedUSS + 3 font sizes + 11 themes)
- tss_assets_all: 1271KB (was 4KB)
- 48 assets in the bundle (was 33)

---

## 2. CSS var() Reconstruction Fix

### Problem
The reconstructed MergedUSS had 2630 `transparent(1 var(--xxx))` instead of `var(--xxx)`. Caused `Assertion failed` every frame.

### Root Cause (verified via debug on real MergedUSS)
```
Function handle (vType=10):
  strings[valueIndex] = "transparent"  <- initial/fallback value, NOT the funcId
  floats[valueIndex]  = 0             <- does NOT contain the StyleValueFunction enum

Next Float handle (vType=2):
  floats[valueIndex]  = 1.0           <- StyleValueFunction.Var IS HERE
```

Initial research said `floats[]` of the Function handle contains the funcId — WRONG. Confirmed via real debug logging.

### Fix
```csharp
case 10: // Function
    // Peek at NEXT Float handle for the StyleValueFunction enum
    int funcId = 0;
    if (i + 1 < values.Length) {
        var peekH = values.GetValue(i + 1);
        if (peekType == 2) // Float
            funcId = (int)floats[peekIdx];
    }
    switch (funcId) {
        case 1: sb.Append("var("); skipArgCount = true; break;
        case 2: sb.Append("env("); skipArgCount = true; break;
        // ...
    }
```

### StyleValueFunction enum (dump.cs line 1131756)
Unknown=0, Var=1, Env=2, LinearGradient=3, filters=4-13, MaterialProperty=14

---

## 3. Sprite Loading

### Problem
PlayButton had dimensions (280x132 from MergedUSS) but `bgImg=none` — sprites are loaded by the C# constructor, not by CSS.

### Solution: Variable tracing in decompiled_all.c
In decompiled_all.c, Ghidra places `__a__` assignments at the top of the function and uses the variables 100+ lines later:
```c
pSVar8 = __a__ysx;  // line 134 — "defaultBackgoundButton"
// ... 20 lines later ...
AddressableEntry___ctor(&stack, &type, pSVar8, ...);  // line 153 — uses pSVar8
```

Vs PortraitRanking (without OPS string):
```c
pSVar22 = System_Enum__ToString(...);  // runtime value, not OPS string
AddressableEntry___ctor(stack, &type, pSVar22, ...);  // uses computed value
```

### Implementation
1. `analyze_client.py`: maps `pSVarX = __a__KEY` — checks if `pSVarX` appears in the 10-line window after `AddressableEntry___ctor` — `spriteOpsKeys`
2. `build_child_tree`: SPR only consumes string if the OPS key is in `spriteOpsKeys`
3. `ClientData_compact.json`: `spr` field in each TreeNode child
4. `WebGLProbe.cs`: `ApplySprite` with cache by addressable path, disambiguation by type

### Result
- PlayButton: 9/9 sprites correct, 0 false positives
- PortraitRanking: 0 sprites (correct — uses Enum.ToString, not OPS strings)
- 2753 textures in the sprite cache
- 12 BG-IMG on the login screen (was 3)

---

## 4. Other Fixes

### JSON parser bounds
Brace-matching in ParseClientData did not ignore braces inside quoted strings — truncated substring — `ArgumentOutOfRangeException` in ParseNode. Fix: tracking `inStr` + bounds checks.

### data_assets_all removed
Never existed in the WebGL build (V8: MonoScript GUID mismatch). Data via UnityPy (ThemeColors.json, DofusUISettings.json).

### PlayButton text z-order
`Button.text` creates an internal TextElement that stays behind absolute children. Fix: set text on `#playButton_label` (DofusLabel child, created last = visual top).

---

## 5. Current State

```
VISUAL:
  OK  PlayButton VISIBLE with 9 sprites (background, border, shadow, light, gems)
  OK  MergedUSS 4242 rules working
  OK  CSS var() correct (zero Assertion failed)
  OK  Dofus logo + gradient + Krosmoz background
  OK  Beige font from var(--white_white100)
  OK  902/943 USS loaded
  OK  Full login flow: HAAPI -> WS -> auth -> ServerSelection

  !!  Play text behind sprites (fix: use playButton_label instead of Button.text)
  !!  Dropdown dd_host [234x0] (MergedUSS expects dropdown_dropdown, factory creates dropdown)
  !!  ServerSelection incomplete
  !!  Boot sequence skips steps (no loading, no intro)

ERRORS:
  OK  Zero Assertion failed
  OK  Zero ArgumentOutOfRangeException
  OK  Zero 404
  !!  Missing scripts (Quantum Console, EventSystem — pre-existing)
```

---

## 6. Next Steps

1. **Dropdown names** — investigate in decompiled_all.c whether Dropdown constructor adds a prefix to children names
2. **Boot flow fidelity** — reproduce the 7 progress steps (bkqc 0-6) with ConnectionBackground and loading spinner
3. **ServerSelection** — parse protobuf response and populate the server list
4. **Screen transitions** — derive from decompiled_all.c (fpm auth screen manager)

---

## 7. Key Files

| File | Description |
|------|-------------|
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | ThemeStyleSheet routing, TSS import extraction, CSS Function fix |
| `C:\DofusWeb\Assets\WebGLProbe.cs` | Sprite loading, JSON parser fix, text z-order fix |
| `Tools/ClientAnalyzer/analyze_client.py` | SPR variable tracing, spriteOpsKeys |
| `C:\DofusWeb\Assets\UI\OrphanUSS\MergedUSS.uss` | 4255 rules reconstructed with correct var() |
| `C:\DofusWeb\Assets\Resources\ClientData.json` | Factory data with spr field |
