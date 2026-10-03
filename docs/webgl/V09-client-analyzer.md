# V09: Dofus 3 WebGL — ClientAnalyzer, OPS Decryption, Data-Driven Factories

## Session Summary

Session 6 (Sep 21-22, 2026). Focused on eliminating ALL hardcode from the probe and replacing it with automatic extraction from client binaries. We started from V8 (13 hardcoded factories, invented names, default colors) and arrived at:
1. **ClientAnalyzer** (`Tools/ClientAnalyzer/analyze_client.py`) — universal extraction tool
2. **28,517 OPS-decrypted strings** from `global-metadata.dat` via XOR
3. **1,910 CSS selectors** extracted from compiled StyleSheets in Win64 bundles via UnityPy
4. **Zero hardcode** — everything comes from the original binaries
5. **226 VE types** in auto-generated link.xml
6. **4 data-driven factories** replace the 13 hardcoded ones

---

## 1. The Principle: NEVER Hardcode

Leona corrected 7+ times during sessions 5-6 each time something was invented instead of extracted. The corrections included:
- Texture lookup by invented name → universal extraction tool
- Hardcoded theme colors → ThemeColors.json via UnityPy
- Invented element names → decrypted strings from global-metadata.dat
- Name-based heuristics ("contains Button") → real inheritance from dump.cs
- Factories with manual hierarchies → automatically extracted data
- Invented USS classes ("dofus-button") → const string fields from dump.cs

This principle guided ALL of session 6 and led to creating the complete extraction pipeline.

---

## 2. ClientAnalyzer: Universal Extraction Pipeline

### Tool: `Tools/ClientAnalyzer/analyze_client.py`

Parses ALL data from the IL2CPP client automatically:

```bash
py Tools/ClientAnalyzer/analyze_client.py           # full pipeline
py Tools/ClientAnalyzer/analyze_client.py --phase dump        # dump.cs only
py Tools/ClientAnalyzer/analyze_client.py --phase index       # index decompiled_all.c
py Tools/ClientAnalyzer/analyze_client.py --phase constructors # extract constructors
py Tools/ClientAnalyzer/analyze_client.py --phase synthesize   # generate ClientData.json
```

### Data sources and what is extracted from each

| Source | Size | What it extracts | Count |
|---|---|---|---|
| `dump.cs` | 65 MB, 1.98M lines | Type hierarchy, fields, consts, methods with VA | 39,754 types |
| `dump.cs` | | VisualElement types with inheritance, USS consts, child fields | 314 VE types |
| `dump.cs` | | Asset paths (const string) | 385 paths |
| `dump.cs` | | Services with [ServiceInit] | 182 services |
| `dump.cs` | | Screen handlers (fzy<T>) with bindings | 258 handlers |
| `dump.cs` | | UI binding structs (name → type) | 717 bindings |
| `dump.cs` | | Enums with values | 2,081 enums |
| `decompiled_all.c` | 964 MB | Function index (byte offsets) | 261,745 functions |
| `decompiled_all.c` | | Constructor operation sequences | 269 constructors |
| `decompiled_all.c` | | Function calls, field assigns, AddToClassList, manipulators | 1,579 ops total |
| `global-metadata.dat` | 38.5 MB | OPS Obfuscator decrypted strings | 28,517 strings |
| Reconstructed `.uss` | 229 files | BEM USS patterns (.window__background, etc.) | 41 BEM roots |
| Win64 bundles | via UnityPy | Compiled CSS selectors (.class #id) | 1,910 selectors |

### Output

| File | Size | Content |
|---|---|---|
| `output/visual_elements.json` | 295 KB | 314 VE types with inheritance, fields, USS consts |
| `output/constructors.json` | 235 KB | 269 constructors with ops, field order, string literals |
| `output/obfuscated_strings.json` | ~1.5 MB | 28,517 decrypted strings (element names, USS classes, i18n keys) |
| `output/asset_paths.json` | 40 KB | 385 asset paths |
| `output/services.json` | 234 KB | 182 services with [ServiceInit] methods |
| `output/screen_handlers.json` | 254 KB | 258 screen handlers with binding types |
| `output/bindings.json` | 1.0 MB | 717 UI binding structs |
| `output/enums.json` | 771 KB | 2,081 enums |
| `output/decompiled_index.json` | 41 MB | 261,745 functions indexed (cached 24h) |
| `output/bem_map.json` | | 41 BEM roots from USS files |
| `output/css_selectors_all.json` | | 1,910 CSS selectors from Win64 bundles |
| `output/uss_overrides.json` | | 25 USS overrides with background-image |
| `output/ClientData_compact.json` | 65 KB | Runtime data for WebGLProbe |

---

## 3. OPS Obfuscator Decryption (Key Discovery)

### Problem
The game uses OPS Obfuscator to encrypt string literals. Element names (`playButton_container`), USS classes, i18n keys — everything is hidden in functions `<PrivateImplementationDetails>{GUID}.__a__xxx`.

### Discovery
There are 5 encrypted string tables. The main one (GUID `0E33E920`) has 28,519 entries in 803,027 bytes.

### Mechanism
1. The encrypted bytes are in `global-metadata.dat` at offset `0x13D51B0`
2. XOR formula: `decoded[i] = encrypted[i] ^ (i & 0xFF) ^ 0xAA`
3. Each wrapper function (`__a__ykl`, `__a__ysu`, etc.) calls `__a__a_(index, offset, length)` to decode a string from the byte pool
4. The byte pool decodes to pure UTF-8 without invalid characters

### Verification
```python
# Verified against known strings:
# __a__ysu → (0x4148, 0x53821, 10) → "playButton"  ✓
# __a__ysv → (0x4149, 0x5382b, 20) → "playButton_container"  ✓
# __a__ykl → (0x406f, 0x52a0b, 6)  → "button"  ✓
```

### Result
28,517/28,517 strings decrypted in 0.7 seconds. Zero failures. Includes:
- UI element names (playButton_container, button_container, divider_diamondContainer, etc.)
- USS classes (playButton, button, tab, checkbox, etc.)
- i18n keys (ui.common.play, ui.connection.updaterRequired, etc.)
- Asset paths
- Build hashes

### What is NOT in these strings
The strings `loginButton` and `loginButton_container` do NOT exist in the 28,517 OPS strings. This was verified and confirmed that these strings come from the UXML/USS system (Editor), not from C# code.

---

## 4. Constructor Analysis from decompiled_all.c

### How it works
1. The `decompiled_index.json` maps 261,745 functions to byte offsets in the 964 MB file
2. For each VE constructor, the analyzer reads the body and extracts a sequence of operations
3. The operations are: VE (new VisualElement), LBL (new Label), ICO (new DofusIcon), NAM (set_name), CLS (AddToClassList), ADD (Hierarchy_Add), F:field (field assign), CLK (ExtendedClickable), MAN (AddManipulator), CUR (DofusCursor), SPR (sprite load), STY (style set)

### Example: PlayButton
```
CLS                          ← AddToClassList("playButton") [string ysu]
VE + NAM                     ← new VE, set_name("playButton_container") [string ysv]
VE + NAM + F:m_background    ← new VE, name="playButton_background", assign to field
SPR + SPR                    ← load 2 sprites for background
VE + NAM + F:m_border        ← border child
SPR + SPR
... (9 more children)
LBL + NAM + F:m_label        ← DofusLabel child
ADD                          ← Hierarchy_Add (wrapper → root)
CLK + MAN                    ← ExtendedClickable + AddManipulator
CUR                          ← DofusCursor handler
```

Total: 51 operations, 10 VEs, 1 Label, 1 AddToClassList, 9 sprite loads, 11 set_name.

### Wrapper Detection
The VE+NAM pattern before the first FIELD + ADD at the end indicates the constructor creates a wrapper container. For PlayButton, the wrapper is `playButton_container` and all children go inside it.

### FUN_ Address Resolution
The `FUN_XXXX` calls in decompiled_all.c are internal IL2CPP runtime functions (lazy type init, null check, exception). All game-relevant calls (AddToClassList, set_name, VisualElement__Add, ExtendedClickable___ctor) use full names — no additional resolution needed.

---

## 5. CSS Selectors from Win64 Bundles (via UnityPy)

### Problem
The .uss files reconstructed by RebuildBundlesForWebGL contain selectors from the compiled StyleSheets of Win64 bundles. But we had not verified whether the MergedUSS of the TSS also has relevant selectors.

### Discovery
By reading ALL compiled StyleSheets from ALL bundles via UnityPy, we found **1,910 CSS selectors** of type `.class #id`. For PlayButton:

**MergedUSS (tss_assets_all)** — 11 rules:
```
.playButton #playButton_container
.playButton #playButton_background
.playButton #playButton_border
.playButton #playButton_shadowEffect
.playButton #playButton_shadowEffectHover
.playButton #playButton_shadowEffectPressed
.playButton #playButton_lightEffect
.playButton #playButton_lightEffectHover
.playButton #playButton_lightEffectPressed
.playButton #playButton_gems
.playButton #playButton_label
```

**login.uss (uielements_assets_all)** — 1 rule:
```
.loginButton #loginButton_container { background-image: bigPlayButtonWhite }
```

### Significance
The MergedUSS of the TSS (loaded as `LargeFont_Standard.tss`) has rules for ALL PlayButton children with names `playButton_*` — exactly the names the constructor uses (verified via OPS strings). The factory only needs to create children with these names and the TSS applies the styles.

The `login.uss` with `loginButton_container` is a UXML-specific override that the m_SerializedData would define — but it is null in the original bundle (confirmed via UnityPy raw bytes scan).

---

## 6. The loginButton vs playButton Question

### Complete investigation

1. **`loginButton` in 28,517 OPS strings**: ZERO occurrences. Does not exist in C# code.
2. **`loginButton` in raw bytes of login.uxml (Win64 bundle)**: ZERO. Not in the VisualTreeAsset.
3. **`loginButton` in SerializedFile of the bundle**: FOUND at offset 4,825,320 — in the managed references section ([SerializeReference]).
4. **`loginButton` in compiled login.uss**: FOUND as selector `.loginButton #loginButton_container` (type=3 class, type=6 id).

### Conclusion
`loginButton` is an attribute of the `m_SerializedData` of the VisualElementAsset for PlayButton in the context of `login.uxml`. The `m_SerializedData` is in the managed references section of the SerializedFile — not inside the UXML object. AssetRipper could not extract this data (rid: -2 = null reference in exported YAML).

In the original game:
1. CloneTree processes the PlayButton → `m_SerializedData` **is not null** (it is in the SerializedFile)
2. `UxmlSerializedData.Instantiate()` is called → creates the PlayButton with override attributes
3. The attributes include class `loginButton` and container name `loginButton_container`
4. The `login.uss` matches `.loginButton #loginButton_container` → applies bigPlayButtonWhite

In our WebGL build:
1. `preserve="all"` in link.xml → `m_SerializedData` becomes null (the UxmlSerializedData type exists but the reference data does not deserialize)
2. Factory path is used → creates PlayButton with DEFAULT names from constructor (`playButton_*`)
3. The MergedUSS of the TSS matches `.playButton #playButton_*` → applies BASE styles
4. The `login.uss` does NOT match (needs `.loginButton` which does not exist)

### Why we CANNOT use loginButton
The string `loginButton` comes from the `m_SerializedData` which is in the managed references section of the binary SerializedFile. Extracting this requires parsing the Unity SerializedFile binary format — possible but complex. For now, the BASE visual from MergedUSS (with `playButton_*`) should work. The `loginButton_container` with `bigPlayButtonWhite` is an ADDITIONAL visual override that is not the main style.

### Why we went down this path
1. First we tried name-based heuristics ("contains Button") → Leona corrected: do not hardcode
2. We tried using field names from dump.cs (`m_background` → `background`) → real names are `playButton_background` (with prefix)
3. We tried placeholder `_container` → does not match any USS
4. We decrypted 28,517 OPS strings → got the REAL names (`playButton_container`)
5. We extracted 1,910 CSS selectors from bundles → confirmed MergedUSS matches `playButton_*`
6. We verified in the binary SerializedFile → `loginButton` is in managed references, not in code

---

## 7. Data-Driven Factories

### Before (13 hardcoded factories)
```csharp
class F_PlayButton : UxmlFactory<Button> {
    // 30 lines of manual hierarchy with invented names
    var container = MakeVE("loginButton_container"); // WRONG — invented name
    container.Add(MakeVE("shadowEffect"));           // WRONG — name without prefix
    ...
}
```

### After (4 data-driven factories)
```csharp
class DDFactory_VE : UxmlFactory<VisualElement> {
    internal TreeNode[] _tree; // children from ClientData.json
    public override VisualElement Create(...) {
        var el = base.Create(bag, cc);
        // USS classes from dump.cs const fields
        if (_uss != null) foreach (var u in _uss) el.AddToClassList(u);
        // Children with REAL names from global-metadata.dat
        if (_tree != null) foreach (var ch in _tree) el.Add(CreateChild(ch));
        ...
    }
}
```

### TreeNode structure (from ClientData.json)
```json
{
  "n": "playButton_container",  // name decrypted from global-metadata.dat
  "t": "VE",                    // field type from dump.cs
  "cls": ["window__background"], // BEM class from reconstructed .uss
  "ch": [                       // nested children (from wrapper pattern in decompiled_all.c)
    {"n": "playButton_background", "t": "VE"},
    {"n": "playButton_border", "t": "VE"},
    ...
  ]
}
```

---

## 8. Other Tools Created/Updated

### GenerateLinkXml.cs (`Build > Generate link.xml`)
- Reads ALL plugin DLLs via `Assembly.LoadFrom()`
- Walks the type hierarchy by name (`InheritsFrom` by fullname, not IsAssignableFrom — necessary because the load context is different)
- Generates link.xml with 226 VE types + 128 UxmlSerializedData
- Discovery: plugin DLLs are NOT in `AppDomain.CurrentDomain.GetAssemblies()` in the Editor — they need explicit `Assembly.LoadFrom()`

### RebuildBundlesForWebGL.cs (CustomTypeNames auto-scan)
- `CustomTypeNames` changed from a hardcoded HashSet of 13 names to lazy-init assembly scan
- Discovers ALL VE types automatically for the CSS type→class transformation

### Expanded UnityPy tools
- `css_selectors_all.json` — 1,910 CSS selectors from ALL Win64 bundles
- `uss_overrides.json` — 25 parent classes with background-image rules

---

## 9. Problems Resolved

| Problem | Solution | Data source |
|---|---|---|
| 13 hardcoded factories | 4 DDFactory data-driven | dump.cs + decompiled_all.c + global-metadata.dat |
| Invented element names | OPS-decrypted strings | global-metadata.dat offset 0x13D51B0 |
| Invented USS classes | const string fields | dump.cs regex parser |
| Name-based heuristics ("contains Button") | Real type inheritance | dump.cs chain walker |
| Hardcoded theme colors (19 defaults) | Removed — only ThemeColors.json | UnityPy extract_all_themes.py |
| Hardcoded PlayButton tint | Removed — USS applies | MergedUSS in TSS |
| Manual link.xml (13 types) | Auto-generated (226 types) | GenerateLinkXml.cs + Assembly.LoadFrom() |
| Hardcoded CustomTypeNames (13) | Auto-scan assemblies | RebuildBundlesForWebGL lazy property |
| "missing factory" for 6 types | Auto-scan registers 209 | AppDomain assembly scan |
| Dropdown red box | Base type VE (not Button) | dump.cs inheritance (not clickable upgrade) |
| AutoButtonFactory MissingMethodException | Parameterless constructor added | Unity Editor UXMLEditorFactories requirement |

---

## 10. Known Issues (for next session)

### PlayButton visual
- Children `playButton_container`, `playButton_background`, etc. are created with correct names
- The MergedUSS of the TSS HAS rules `.playButton #playButton_*` that should apply styles
- The `loginButton_container` with `bigPlayButtonWhite` is a UXML-specific override in the managed references of the SerializedFile — we cannot extract it yet
- **Possible fix**: parse the managed references section of the binary SerializedFile via UnityPy/Python to extract ALL m_SerializedData attribute overrides

### ServerSelection
- Loads with 270 elements and 9 theme colors
- `ArgumentOutOfRangeException` — some element tries to access an invalid index
- Needs investigation in decompiled_all.c for the ServerSelection handler

### Font
- `Font not found for path: DefaultFont` — in ServerSelection
- `ApplyFontRecursive` in ShowScreen should resolve it
- The CSS `resource("DefaultFont")` or `url("/Assets/Resources/DefaultFont.asset")` needs a bundle rebuild

### m_SerializedData extraction
- The managed reference data of the SerializedFile is in the Win64 bundle at known offsets
- `loginButton` found at offset 4,825,320 of SerializedFile `CAB-5f99c5e0...`
- Format: int32 length + UTF8 bytes + null padding to 4-byte alignment
- A Python/UnityPy tool that reads this section would resolve ALL attribute overrides from ALL UXMLs at once

---

## 11. Decisions Made and Why

### Why not hardcode element names?
The constructor names (`playButton_container`) and the UXML override names (`loginButton_container`) are DIFFERENT. Without automatic extraction, we would use wrong names that do not match any USS.

### Why decrypt global-metadata.dat?
The real element names are hidden by OPS Obfuscator in `<PrivateImplementationDetails>` functions. Without decrypting, we only had field names (`m_background` → "background") which do not include the type prefix (`playButton_background`).

### Why extract CSS selectors via UnityPy?
RebuildBundlesForWebGL already reconstructed .uss as text, but we had not verified the MergedUSS inside the TSS. The UnityPy extraction showed that the MergedUSS has 11 specific rules for `.playButton #playButton_*` — confirming the factory only needs to create children with these names.

### Why not parse m_SerializedData now?
The m_SerializedData is in the managed references section of the binary SerializedFile. Parsing this format requires understanding the [SerializeReference] structure of Unity 6 — possible but complex. The BASE visual from MergedUSS with `playButton_*` should work without the `loginButton` override.

### Why remove the 13 specialized factories?
Each factory had manual hierarchies with invented names that did not match the real names. The names from `login.uss` (`loginButton_container`) were different from the constructor names (`playButton_container`). The only source of truth is the client binaries.

### Why DDFactory_VE instead of DDFactory_Btn for Dropdown?
dump.cs shows that Dropdown inherits from DofusBindableElement → DofusVisualElement → VisualElement. It does NOT inherit from Button. The ExtendedClickable field exists (the constructor adds a manipulator), but the BASE TYPE is VisualElement. Using Button caused `:focus` styling (red box).

---

## 12. Key Files

| File | Description |
|---|---|
| `Tools/ClientAnalyzer/analyze_client.py` | Complete extraction pipeline (dump.cs + decompiled_all.c + global-metadata.dat) |
| `Tools/ClientAnalyzer/output/obfuscated_strings.json` | 28,517 OPS-decrypted strings |
| `Tools/ClientAnalyzer/output/css_selectors_all.json` | 1,910 CSS selectors from Win64 bundles |
| `Tools/ClientAnalyzer/output/ClientData_compact.json` | 65KB runtime data with real names |
| `C:\DofusWeb\Assets\Resources\ClientData.json` | Copy for Unity Resources |
| `C:\DofusWeb\Assets\WebGLProbe.cs` | 4 DDFactory data-driven, zero hardcode |
| `C:\DofusWeb\Assets\Editor\GenerateLinkXml.cs` | Auto link.xml (226 types) via Assembly.LoadFrom() |
| `C:\DofusWeb\Assets\Editor\RebuildBundlesForWebGL.cs` | CustomTypeNames auto-scan |
| `PlayBrowser/GUIA_WEBGL_DOFUS3_V1-V8.md` | Previous session guides |

---

## 13. Next Steps (Priority)

1. **Rebuild and test** — verify if the MergedUSS of the TSS applies styles to `playButton_*` children
2. **Parse managed references** — extract m_SerializedData from ALL UXMLs in Win64 bundles to get attribute overrides (loginButton, etc.)
3. **Fix ServerSelection** — investigate the ArgumentOutOfRangeException in decompiled_all.c
4. **Fix font path** — rebuild bundles with the `url()` fix in CSS
5. **Screen flow** — extract transitions from decompiled_all.c automatically
6. **CharacterSelection** — next screen after ServerSelection
