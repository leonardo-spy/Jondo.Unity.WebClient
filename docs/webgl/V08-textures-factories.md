# V08: Dofus 3 WebGL — Textures, USS Resolution, Specialized Factories

## Summary

Session 5 (Sep 19-20, 2026). Focused on inline texture extraction, USS texture resolution, specialized factories, and visual pipeline automation. We started from V7 (root cause: inlineSheet assets fileID:0) and arrived at:
1. **1525 USS images** resolve (were 0)
2. **105 USS textures** auto-extracted to `Assets/UI/USSTextures/`
3. **TSS imports** fixed (two-pass bundle loading)
4. **13 specialized factories** with child hierarchy from decompiled constructors
5. **WebGLClickable** manipulator replaces ExtendedClickable (red box removed)
6. **Per-element USS loading** (LoadComponentUSS replicates AddStyleSheetPath)
7. **Automatic CSS type→class transformation** for custom elements

---

## 1. Inline Texture Extraction (SaveAsAsset fix)

### Problem (V7)
`Object.Instantiate()` copies textures as sub-objects of the `.uxml.asset`. In the Editor they work. In the WebGL bundle, sub-objects without their own GUID are NOT included → `Invalid image specified`.

### Fix
In `SaveAsAsset`, after creating the inlineSheet sub-asset:
1. Reads `assets[]` from original via `RF<Object[]>(refObj, "assets")`
2. For each texture/sprite with `width > 1`:
   - Extracts as PNG via `SaveTexture()` to `Assets/UI/InlineTextures/{vtaName}_{idx}_{name}.png`
   - For sprites: sets `TextureImporterType.Sprite` + `SpriteImportMode.Single`
3. Replaces references in subCopy via `SerializedObject`

**Result**: Dofus logo + gradient visible on login. All inline textures from all UXMLs auto-extracted.

---

## 2. USS Texture Auto-Extraction

### Problem
CSS reconstruction generated `url("btn_green_normal")` (just the name) for textures not found in the project. Unity does not resolve names without a path.

### Fix: ResolveTexturePath
Unified helper in `AssetRefToCSS` and `ScalableImageToCSS`:
1. Tries `AssetDatabase.GetAssetPath(asset)`
2. Tries `refMap[instanceID]`
3. Tries `FindAssets("t:Texture2D {name}")`
4. **Auto-extract**: saves PNG to `Assets/UI/USSTextures/{name}.png`, imports, uses the path

### Fix: `/` prefix in url()
USS url() without `/` is relative to the file. `url("Assets/...")` → Unity resolves relative to the .uss. Fix: `url("/Assets/...")` → absolute path in the project.

**Result**: 1525 USS images resolve (were 0). 105 textures auto-extracted.

---

## 3. Two-Pass Bundle Loading

### Problem
The extraction loop loaded and extracted in the same cycle. When `tss_assets_all` was processed, the USS bundles might not be loaded yet → cross-bundle refs null.

### Fix
Separate into two loops:
```
FIRST: Load ALL bundles (AssetBundle.LoadFromFile)
THEN:  Extract assets from all bundles
```

**Result**: TSS imports=3, null=0. All cross-bundle refs resolve.

---

## 4. 13 Specialized Factories

### Constructor Analysis
Complete investigation of all 13 constructors via `dump.cs`. Each type:
- Creates a specific child hierarchy
- Adds specific USS classes
- Uses ExtendedClickable (PlayButton, DofusButtonCustom, DropdownButton)

### Implementation
Each factory:
1. Calls `SetupDVE()`/`SetupDBE()` (inheritance chain)
2. Adds real USS class (`.dofus-button`, `.combobox`, `.window`)
3. Creates children with verified USS classes
4. Loads component USS via `LoadComponentUSS`
5. Adds `WebGLClickable` manipulator (types with ExtendedClickable)

### USS Class Mapping (verified in .uss files)
| Factory | USS Class | Component USS |
|---------|-----------|---------------|
| F_DofusButtonCustom | `.dofus-button` | Button.uss |
| F_WindowFigma | `.window` | Window.uss |
| F_TextInput | `.dofus-text-field` | InputField.uss |
| F_Dropdown | `.combobox` | Combobox.uss |
| F_PlayButton | `.playButton` + `.loginButton` | (none) |

### CSS Type→Class Transformation
In `ComplexSelectorToCSS`, type selectors for the 13 custom types are transformed into class selectors:
```
PlayButton { ... }  →  .PlayButton { ... }
```
Factories add the corresponding class → USS matches automatically.

---

## 5. WebGLClickable Manipulator

### Problem
ExtendedClickable (original C#) intercepts pointer events and prevents the default UIToolkit hover. Corrupted IL → does not work.

### Fix
`WebGLClickable` extends `Clickable`:
- Registers `PointerOverEvent`/`PointerOutEvent` with `TrickleDown`
- Adds/removes classes `.hovered`/`.pressed`
- Override inline on `FocusInEvent` → `borderColor = Color.clear`

Applied in factories: F_DofusButtonCustom, F_PlayButton, F_DropdownButton.

**Result**: Red box removed.

---

## 6. WebGLOverrides.uss

Minimal USS that compensates for absent C# behavior (ExtendedClickable). Contains ONLY hover/focus/active overrides for types with ExtendedClickable. NO hardcoded colors, NO layouts, NO tint colors.

---

## 7. Theme Colors — Limitation Found

`FigmaThemeScriptable` does not deserialize from bundles. The SO type dump shows only Unity built-in types (VisualTreeAsset, StyleSheet, ThemeStyleSheet, PanelSettings). Custom ScriptableObjects do not appear because the MonoScript GUIDs do not match between the original bundles and the MergeIL DLLs.

**Alternative approach needed**: Binary parse of the `data_assets_all` bundle, or extract color constants from `dump.cs`/`decompiled_all.c`.

---

## 8. The 7 USS Systems of Dofus 3

| # | System | Status |
|---|---------|--------|
| 1 | PanelSettings.themeStyleSheet | [done] TSS applied |
| 2 | TSS imports | [done] 3 imports, 0 null |
| 3 | VisualElementAsset.stylesheetPaths | [warn] Strings, needs verification |
| 4 | VisualElementAsset.stylesheets | [done] Remap fixes refs |
| 5 | VisualTreeAsset aggregation | Depends on 3+4 |
| 6 | DofusUISettings TSS matrix | [done] Correct TSS loaded |
| 7 | Figma dynamic colors | [fail] MonoScript GUID mismatch |

---

## 9. Current Visual State

```
[done] Dofus logo (inline texture)
[done] Vertical gradient (inline texture)
[done] Krosmoz background
[done] bigPlayButtonWhite on loginButton_container
[done] bg_small_border_light_cropped on dropdown
[done] 1525 USS images resolve
[done] Dropdown has height (combobox USS works)
[done] No red box on hover (WebGLClickable)
[done] 19 component USS cached (per-element loading)
[done] TSS imports valid

[warn] PlayButton white (real texture, missing effect layers)
[warn] Theme colors hardcoded defaults (FigmaTheme does not deserialize)
[warn] Font atlas 1x1 (stripped in build)
```

---

## 10. Key Files

| File | Description |
|---------|-----------|
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | v5+ — two-pass, inline tex, USS tex, `/` prefix, TSS fix |
| `C:\DofusWeb\Assets\WebGLProbe.cs` | 13 factories, WebGLClickable, LoadComponentUSS |
| `C:\DofusWeb\Assets\Resources\WebGLOverrides.uss` | Minimal hover overrides (ExtendedClickable substitute) |
| `PlayBrowser/CUSTOM_ELEMENTS_CONSTRUCTORS.md` | 13 constructors + USS class mapping |
| `Assets/UI/InlineTextures/` | PNGs extracted from inlineSheets |
| `Assets/UI/USSTextures/` | 105 PNGs extracted from USS StyleSheets |

---

## 11. UnityPy — External Parser for Locked Data

### Problem: MonoScript GUID Mismatch
Core.dll in the project has GUID `9a6d44e21d664134aae20ec69eb008b2`, bundles expect `6c8e168389b232d7cc78427b51f93153`. Unity refuses to load ANY custom ScriptableObject from the bundles.

### Solution: UnityPy 1.25.3
Python parser that reads Unity bundles directly, without depending on the type system.

### Tools created:
| Script | Function | Result |
|---|---|---|
| `Tools/ExtractThemeColors/extract_figma_colors.py` | Extracts FigmaThemeScriptable from data_assets_all | 25 colors -> ThemeColors.json |
| `Tools/UnityPyTools/scan_all_bundles.py` | Scans ALL bundles | DofusUISettings + 2509 custom SOs + 21 fonts |
| `Tools/UnityPyTools/extract_fonts.py` | Extracts .ttf + SDF atlas | 10 .ttf + 5 atlas PNG + 7 FontAsset metadata |

### Extracted data:
- **25 theme colors** (Standard theme) -> `Assets/Resources/ThemeColors.json`
- **DofusUISettings** with 33 TSS GUIDs -> `Assets/Resources/DofusUISettings.json`
- **10 .ttf fonts** (Lexend family, NotoSans, Roboto, etc.)
- **5 SDF atlas** (1024x1024 and 512x512)
- **7 FontAsset metadata** with glyph tables

### 5 Implemented opportunities:

**#1 FigmaTheme Colors** [done]
- 11 themes extracted: Standard/Unity, Bonta, Brakmar, GoldAndSteel, Tribute, Belladone, Unicorn, EmeraldMine, Sufokia, Pandala, Wabbit
- 25 colors per theme + 10 universal colors (black/white)
- ThemeColors.json with 35 entries → Probe loads automatically

**#2 DofusUISettings** [done]
- Found in `gameassets_assets_all` bundle
- 33 TSS GUIDs extracted (11 themes x 3 font sizes)
- DofusUISettings.json generated in Resources

**#3 Original Font Atlas** [done]
- 10 .ttf fonts extracted (Lexend family, NotoSans, Roboto, LiberationSans, etc.)
- 5 SDF atlas PNG (1024x1024, 512x512)
- 7 FontAsset metadata with glyph tables
- User runs `Build > 2. Create FontAsset from Lexend` to regenerate SDF

**#4 Pipeline Validation** [done]
- `validate_webgl_bundles.py`: compares Win64 vs WebGL (23 vs 20 bundles, 3 missing)
- Identifies `gameassets_assets_all` as most affected (212 → 5 objects)
- Reports in JSON for tracking

**#5 CSS Validation** [done]
- `validate_css.py`: 1146 original StyleSheets, 229 reconstructed
- 21649 original rules, 3546 reconstructed blocks
- Identifies MergedUSS (4255 rules) and default (383 rules) in TSS as main gap
- Report in JSON

### Complete UnityPy tools:
| Script | Function |
|---|---|
| `Tools/ExtractThemeColors/extract_all_themes.py` | 11 themes + universals → ThemeColors.json |
| `Tools/UnityPyTools/scan_all_bundles.py` | Scans 23 bundles → DofusUISettings + SOs |
| `Tools/UnityPyTools/extract_fonts.py` | 10 .ttf + 5 atlas + 7 metadata |
| `Tools/UnityPyTools/validate_css.py` | Compares original CSS vs reconstructed |
| `Tools/UnityPyTools/validate_webgl_bundles.py` | Compares Win64 vs WebGL bundles |

---

## 12. Next Steps

1. **Use ThemeColors.json** in Probe — automatic real colors
2. **Use original .ttf** — create FontAsset SDF with complete glyphs
3. **Use DofusUISettings** — select correct TSS per theme
4. **Additional screens** — ServerSelection, CharacterCreation etc.
5. **WebSocket login** — test PLAY -> HAAPI -> protobuf with JondoEmu
6. **Extract ALL 11 themes** — not just Standard
