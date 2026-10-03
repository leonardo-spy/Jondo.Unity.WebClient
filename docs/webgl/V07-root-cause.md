# V07: Dofus 3 WebGL — Root Cause Analysis — Why the UI Does Not Render Visually

## Discoveries from This Session (Sep 18-19, 2026)

### ROOT CAUSE: broken inlineSheet assets

The login.uxml.asset has a sub-asset `inlineSheet` (StyleSheet) that contains ALL inline styles for the elements. This inlineSheet references textures via `assets[]`:

```yaml
assets:
  - {fileID: 0}    # ← BROKEN — should be the logo texture
  - {fileID: 0}    # ← BROKEN — should be the gradient texture
```

The original textures were in other Win64 bundles (cross-bundle refs). The remap pass of RebuildBundlesForWebGL did NOT resolve these references because:
1. The assets already existed on disk (skipped on extract)
2. The remap processes `extractedPairs` but inlineSheet is a sub-asset, not a main asset
3. `Remap: 0 remapped, 0 failed` — nothing was processed

### Why bg.a=0.00 on all elements

```
DEBUG #btn_play: [280x81] bg.a=0.00 bgImg=none
DEBUG #ctr_gradient: [500x600] bg.a=0.00 bgImg=none
DEBUG #tx_dofusLogo: [498x336] bg.a=0.00 bgImg=none
```

- Sizes (`280x81`, `498x336`) come from the inlineSheet → WORK (they are numeric values, not refs)
- Backgrounds (`bg.a=0.00`, `bgImg=none`) are texture references → BROKEN (fileID: 0)
- The inlineSheet has `m_ValueType: 6` (AssetReference) pointing to `assets[0]` and `assets[1]` which are `{fileID: 0}`

### USS: works but has no visible effect on login

```
USS: 202/928 loaded (659 skipped no-selectors)
USS images: 0 total, 0 null
USS on root: 202 sheets
```

The 202 USS load but:
1. Login UXML does not reference external USS (`m_StylesheetPaths: []`, `m_Stylesheets: []`)
2. The login visual comes from the inlineSheet, not from external USS
3. The elements do not have CSS classes that USS targets (only `backgroundColor_black_black50` on 2 elements)
4. The USS that define `.dofus-button` etc. do not match because the factories create plain types

### TSS: loads but was choosing the wrong one

```
TSS LoadAllAssets<ThemeStyleSheet>: 33
TSS applied: SmallFont_Standard.tss   ← WRONG, should be LargeFont_Standard
```

Bug: the loop picked the first TSS because the override condition did not work. Fix applied: exact search for `LargeFont_Standard`.

### Font: 1x1 atlas in build

```
Font: Resources DefaultFont applied (atlas=1x1)
```

The FontAsset loads but the atlas texture (sub-asset) is 1x1 in the WebGL build — the glyphs were not included. Text renders via fallback (LegacyRuntime).

### Theme Colors: not found in bundles

```
[ThemeColors] No theme colors found in any bundle.
```

FigmaThemeScriptable is not in the Win64 bundles as a loadable asset. The v2 extractor searches for field `m_figmaColorsData` but does not find it. It is probably in bundle `data_assets_all` which does not exist as a separate .bundle, or the type does not deserialize correctly in cross-platform bundles.

---

## WHAT WORKS

| Feature | Status |
|---|---|
| Login flow (HAAPI → WS → protobuf) | [done] Complete |
| 13/13 factories | [done] |
| USS loading (202 sheets) | [done] |
| InlineSheet dimensions | [done] (sizes apply) |
| CSS classes from UXML | [done] (backgroundColor_*) |
| I18n (Play text) | [done] |
| TSS loading (33 themes) | [done] (fix selection) |
| Krosmoz background (manual) | [done] |

## WHAT DOES NOT WORK

| Feature | Root Cause |
|---|---|
| Background images (logo, gradient, buttons) | inlineSheet assets = fileID:0 (broken cross-bundle ref) |
| USS visual matching on login | Login UXML does not use external USS, uses inlineSheet |
| USS images in general | Textures reconstructed as url() text do not include dependencies |
| Theme colors (backgroundColor_*) | FigmaThemeScriptable not found in bundles |
| Font atlas | Sub-asset stripped in build (1x1) |

## REQUIRED FIX: Remap inlineSheet assets

RebuildBundlesForWebGL needs to:
1. **During extract**: when saving a VisualTreeAsset as .asset, the inlineSheet is copied as a sub-asset (already does this)
2. **During remap**: ALSO process the inlineSheet sub-asset — iterate its `assets[]` and replace fileID:0 with the correct texture from refMap
3. **Force re-extract**: even if the .asset already exists on disk, re-extract if it is a VisualTreeAsset (to ensure the inlineSheet is updated)

### How to find the original textures

In the original Win64 bundle, the inlineSheet references textures by instanceID. The refMap maps instanceID → savePath. During remap, for each entry in the inlineSheet's `assets[]`:
1. Read the original SerializedObject (from the in-memory bundle)
2. For each ObjectReference in the inlineSheet, look up in refMap
3. Replace in the saved asset

### Alternative: extract textures by name from UXML

The original login.uxml (in the Win64 bundle) knows which textures it uses. They can be extracted by name:
- `tx_dofusLogo` → probably references a "dofusLogo" texture or similar
- `ctr_gradient` → probably references a gradient texture
- `btn_play` → inherits visual from the PlayButton type which references button textures

## Key Files

| File | Description |
|---|---|
| `C:\DofusWeb\Assets\WebGLProbe.cs` | Probe v27 with detailed debug |
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | v4 — NEEDS fix in remap for inlineSheet |
| `C:\DofusWeb\Assets\UI\Auth\login.uxml.asset` | Login UXML — inlineSheet with broken assets |
| `Tools/ScreenRegistry/parse_screens.py` | 763 bindings, 198 handlers extracted |
| `Tools/I18nParser/parse_i18n.py` | Localization parser working |
