# V15: Dofus 3 WebGL — Session 11 (15/Oct/2026)

## Summary

Session focused on REA integration, formal pipeline audit, and map rendering investigation. REA (Reverse Engineer Anything) tool integrated for managed DLL inspection and P/Invoke verification. Complete map rendering pipeline traced through dump.cs + decompiled_all.c + UnityPy bundle analysis. 555 new methods transcribed (total now 1,103). Map bundle loader and protocol handlers implemented in WebGLProbe.cs (+293 lines, now 3,944). Probe v31 → v32.

---

## 1. REA Integration

### Assessment: Tested on Actual Project

REA was connected and tested against the real project binaries to evaluate what it provides beyond the existing pipeline.

| REA Capability | Tested On | Result |
|---|---|---|
| Ghidra interactive on GameAssembly.dll (115MB) | `open_binary` + `binary_overview` | **Timeout** after 5.5min — too large for interactive analysis |
| `inspect_managed_members` on Core.dll (7.5MB) | Merged Plugins/Core.dll | **Fails** — 119K methods exceed 67MB representation budget |
| `inspect_managed_members` on smaller DLLs | Ankama.AudioManagement.dll (210KB) | **Works** — 454 types, 2826 methods, CIL hashes, call edges |
| `inspect_managed_artifact` on all DLLs | 8 key Ankama DLLs | **Works** — PE/CLI metadata, classification, type/method counts |
| `inspect_managed_native_boundaries` | Ankama.AudioManagement.dll | **Works** — formal P/Invoke inventory with ImplMap details |
| `compare_managed_members` | Not yet tested | **Available** — for version diffing when 3.6.11.x releases |

**Conclusion:** REA augments the existing pipeline for smaller DLLs and formal auditing. It does NOT replace dump.cs (for Core.dll) or decompiled_all.c (for GameAssembly.dll) — both are too large for REA's interactive analysis.

### P/Invoke Boundary Audit (REA Evidence)

Formal verification of the FixPInvoke + RevertFMODStubs pipeline:

**Merged Ankama.AudioManagement.dll (Plugins/):**
- 585 P/Invoke imports, ALL targeting module `__Internal`
- 391 `FMOD5_*` (core API) — matches V13
- 194 `FMOD_Studio_*` (studio API) — matches V13
- 0 non-FMOD P/Invokes — no missed boundaries
- 264 runtime-provided (InternalCall) methods
- All 585 have `rva=0`, `body_interpretation=not-file-backed` — real P/Invoke declarations

**DummyDll Ankama.AudioManagement.dll (original stubs):**
- 0 P/Invoke imports (FixPInvoke removed them all)
- 0 ModuleRef entries
- 585 native implementations with `boundary_kind=pinvoke` (the flag remains, just no ImplMap)

**Verdict:** Pipeline confirmed correct. FixPInvoke correctly removed all 585 P/Invokes, RevertFMODStubs correctly restored all 585 as `[DllImport("__Internal")]`.

### DLL Inventory for Next Features

| DLL | Size | Types | Methods | REA Members? | Subsystem |
|-----|------|-------|---------|-------------|-----------|
| Core.dll | 7.5MB | 9,439 | 119,682 | NO (budget) | Main game logic |
| Ankama.Dofus.Protocol.Game.dll | 4.5MB | — | — | NO (budget) | Game protocol |
| Ankama.Dofus.Core.DataCenter.dll | 307KB | 597 | 1,723 | YES | Game data (items, spells, maps) |
| Ankama.AudioManagement.dll | 210KB | 454 | 2,826 | YES | FMOD audio |
| Ankama.Animator2D.dll | 100KB | 137 | 1,372 | YES | 2D animation |
| Ankama.Dofus.Protocol.Connection.dll | 61KB | 115 | 1,258 | YES | Connection protocol |
| Ankama.Dofus.Core.World.dll | 26KB | 51 | 273 | YES | World/map rendering types |
| Ankama.SpinConnection.dll | 26KB | 59 | 221 | YES | Network layer |
| Ankama.ScreenManager.dll | 26KB | 49 | 244 | YES | Screen management |
| Ankama.Dofus.Core.Characters.dll | 8KB | 24 | 47 | YES | Character data |

---

## 2. Map Asset Discovery

### Bundle Structure (discovered via filesystem + UnityPy)

Map assets are stored separately from the main UI bundles, in `Content/Map/`:

```
Content/Map/
├── Data/                              577 bundles (~463MB total)
│   ├── catalog_1.0.bin                Addressable catalog for map data
│   ├── catalog_1.0.hash
│   └── mapdata_assets_world_{worldId}.bundle    MapMetadata per world
│       Example: mapdata_assets_world_729.bundle (632KB, 17 maps)
│
├── Textures/
│   ├── 1x/                            29 atlas bundles (~1.4GB)
│   │   ├── catalog_1.0.bin
│   │   └── mapgfx_1x_{gfxRange}_assets_all.bundle
│   ├── 2x/                            Higher-resolution atlas
│   └── Effects/
│       └── mapeffects_assets_all.bundle
```

Served via Node.js: `/assets/Map/Data/{bundle}.bundle` (already configured in server.js line 36).

### WorldPoint mapId Encoding (decompiled from FromMapId @ 0x1807374E0)

The `jru` packet sends a `long mapId` that encodes world ID + grid coordinates:

```
worldId = (mapId >> 18) & 0xFFF       // bits 18-29 (12 bits, max 4096)
x_raw   = (mapId >> 9) & 0x1FF        // bits 9-17 (9 bits)
y_raw   = mapId & 0x1FF               // bits 0-8 (9 bits)
x = x_raw > 255 ? -(x_raw & 0xFF) : x_raw   // signed 9-bit
y = y_raw > 255 ? -(y_raw & 0xFF) : y_raw   // signed 9-bit
```

Example: `191104002` → worldId=729, x=2, y=2 → bundle `mapdata_assets_world_729.bundle` → asset `map_191104002.asset`

### MapMetadata in Bundles (verified via UnityPy)

Each `mapdata_assets_world_{worldId}.bundle` contains `MonoBehaviour` assets named `map_{mapId}.asset`. These are `MapMetadata` ScriptableObjects. The bundle `world_729` has 17 maps including the test character's `map_191104002`.

**Key finding:** MapMetadata in bundles contains DATA only (cells, elements, neighbors, material atlas rects). The `m_meshes`, `mapTextures`, `spriteDetails`, and `*RenderData` fields are all EMPTY (0 entries) — they are **generated at runtime** by `MapRenderer` from the element data + atlas textures loaded separately.

---

## 3. Map Rendering Data Model

### MapMetadata (REA Evidence + dump.cs L1946632)

```
MapMetadata : ScriptableObject
├── mapData: ClientMapData                    // all cell & element data
├── foregroundRenderData: RenderData          // EMPTY in bundle — runtime generated
├── backgroundRenderData: RenderData          // EMPTY in bundle — runtime generated
├── sortableRenderData: RenderData            // EMPTY in bundle — runtime generated
├── spriteDetails: Dictionary<int, SpriteDetails>  // EMPTY — runtime
├── mapTextures: List<Texture2D>              // EMPTY — runtime
├── m_meshes: List<Mesh>                      // EMPTY — runtime
└── allowMapEffects: bool
```

### ClientMapData (dump.cs L1816516)

```
ClientMapData
├── topNeighbourId / bottomNeighbourId / leftNeighbourId / rightNeighbourId
├── backgroundColor: AleColor
├── backgroundElements: List<ClientElementTransform>      (map 191104002: 1201 elements)
├── sortableElements: List<ClientSortableElementTransform> (map 191104002: 159 elements)
├── foregroundElements: List<ClientElementTransform>       (map 191104002: 13 elements)
├── animatedElements: List<ClientAnimatedElementTransform> (map 191104002: 1 element)
├── refractionElements / interactiveElements / boundingBoxes / particlesParameters
├── foregroundMaterialData / backgroundMaterialData / sortableMaterialData
├── cellsData: List<ClientCellData>                        (always 560 cells)
├── topArrowCellList / leftArrowCellList / bottomArrowCellList / rightArrowCellList
├── mapWindConfiguration / mapPostProcessConfiguration
└── mapWaveConfiguration / mapNoiseModifierConfiguration
```

### ClientElementTransform (extracted from UnityPy)

Each map element (sprite) in the 3 layers:

```json
{
  "gfxId": 900,                              // sprite ID in texture atlas
  "color": {"value": 4287269767},            // ARGB tint (0xFF98FB87)
  "transform": {                             // 2D affine matrix
    "m11": -1.0, "m12": 0.0,                // scale + rotation
    "m21": 0.0, "m22": 1.0,                 // scale + rotation
    "m31": 322.5, "m32": 541.75             // translation (pixels)
  },
  "materialIndex": 0,                        // which shader/material
  "displayBehaviour": 0
}
```

Sortable elements add: `cellId` (which cell they belong to), `innerCellRenderOrder` (z-order within cell).

### MaterialData (atlas UV mapping)

Each layer has a `MaterialData` with atlas rect mappings:

```
MaterialData
├── atlas1x: ElementAtlas (width=1044, height=996)
│   └── idsToRects: SerializedDictionary<int, Rect>
│       45 entries mapping gfxId → normalized UV rect {x, y, width, height}
├── atlas2x: ElementAtlas (higher res)
├── atlas4x: ElementAtlas (highest res)
└── shaderData: ShaderData[]
```

The renderer uses `gfxId → atlas.idsToRects[gfxId]` to get the UV rectangle, then applies the element's `transform` to position the sprite in world space.

### ClientCellData (dump.cs L1816398)

Per-cell gameplay data (map 191104002: 320/560 walkable, 487/560 with LoS):

```
ClientCellData
├── cellNumber: uint, speed: int, floor: int
├── mov: bool, los: bool                      // movement + line of sight
├── nonWalkableDuringFight: bool, nonWalkableDuringRP: bool
├── farmCell: bool, visible: bool, havenbagCell: bool
├── red: bool, blue: bool                     // fight team placement
├── arrow: int                                // map change direction
└── moveZone: int, linkedZone: int, mapChangeData: int
```

### FlashGeometryVertex (REA Ankama.Dofus.Core.World.dll)

The rendering uses a Flash-era vertex format — Dofus was originally a Flash game:

```
FlashGeometryVertex
├── pos: Vector3     // world position
├── color: Color     // vertex color (tint from element.color)
├── u: float         // texture U from atlas rect
└── v: float         // texture V from atlas rect
```

---

## 4. Network Protocol for Map

### `jru` — Map Load Command (dump.cs L680306)

Triggers a map change. Sent by the server when the character moves to a new map.

```
jru (protobuf)
├── field 1 (jru.jrt.jrs enum): map load reason (9 values: 0-8)
└── field 2 (long): mapId to load (WorldPoint encoding)
```

Handlers: `MapDisplayService.vev()`, `ejv.yvv()`, `eni.ctq()`, `fgv.bewl()`

### `lwt` — Map Terrain Data (dump.cs L828568)

Sent after `jru`, provides supplementary cell data for movement/LoS/items.

```
lwt (protobuf)
├── field 1 (RepeatedField<int>): cell IDs or floor mappings
├── field 2 (lwt.lws.lwr): map description
│   ├── field 1: RepeatedField<string>    // asset paths
│   ├── field 2: int
│   ├── field 3: RepeatedField<bool>      // flags
│   ├── field 4: bool
│   ├── field 5: RepeatedField<int>
│   └── field 6: long                      // map ID
├── field 3-5 (RepeatedField<int>)
└── field 6 (RepeatedField<llk>): CELL DATA ENTRIES
    └── llk (per cell)
        ├── field 1 (int): cell number
        ├── field 2 (RepeatedField<lng>): cell properties (polymorphic oneof, 11 sub-types)
        ├── field 3-4 (int)
        └── field 5 (RepeatedField<llj>): cell attributes
            ├── field 1 (int): attribute type
            └── field 2 (lbp enum): value (4 options: 0-3)
```

### Handler Flow (decompiled eqq.nmc @ 0x18926E0)

1. Extract `feay` (RepeatedField<llk> — cells) from lwt
2. Call `Inventory.InitializeFromCosmeticItems(cells, displayService, charService)`
3. Store 3 int arrays from lwt in backing fields (`_dqad`, `_dqae`, `_dqaf`)
4. Dispatch `dcv` event via service bus (`ezp.bdgr<dcv>()`)

**Key insight:** `lwt` provides supplementary cell data (movement, LoS, cosmetic items). Map RENDERING is triggered by `jru` → `MapDisplayService` → `MapRenderer`. `lwt` overlays gameplay data on the loaded map.

---

## 5. Rendering Pipeline (traced from 39 transcribed MapRenderer methods)

```
NETWORK                    SERVICE                       RENDERING
═══════                    ═══════                       ═════════
jru (mapId)          →     MapDisplayService.vev()  →    MapRenderer.qdf(mapId, dir)
                           ezm.ved(mapId)                    │
                                                        qdh(mapId) LoadMap async
                                                            │
                                                        qcy(metadata) HandleMapMetadataLoaded
                                                            ├── qdm() CreateMaterials
                                                            ├── qdn() BuildMesh
                                                            ├── qdo() BuildIndices
                                                            └── qcz()/qda() Wind/Wave

lwt (cells)          →     eqq.nmc()                     (updates ezl cell data)
                           ├── Store cell properties
                           └── Dispatch dcv event
```

### MapRenderer Key Methods (39 transcribed)

| Method | Purpose |
|--------|---------|
| qcu | Init with 21 service dependencies |
| qdf | Map load entry point (mapId, direction, force, callback) |
| qdh | LoadMap async (loads MapMetadata from addressables) |
| qcy | HandleMapMetadataLoaded (creates meshes, materials) |
| qdm | Create cached material from RenderData |
| qdn | Internal render (builds mesh geometry) |
| qdo | Build mesh indices from RenderData + MaterialSpan |
| qcv | CreatePostProcessVolume (async) |
| qcz, qda | GetWind/WaveTextures (async) |

### Key Services (obfuscated names, 3.6.10.11)

| Service | Interface | Purpose |
|---------|-----------|---------|
| MapDisplayService | ezm | Display control: ved(mapId), vdw(cellId→pos), vdx(pos→cellId) |
| eqq (273 methods) | — | Roleplay controller: nmc(lwt) + 272 game handlers |
| ejv (60 methods) | — | jru handler + map events: yvv(jru), joq, kew, je |
| eni (41 methods) | — | Map name tracking: ctq(jru), Dictionary<long,string> |
| ezl | — | Map data service: walkability, pathfinding, LoS |
| ezo | — | Map camera/view: transitions, zoom, reset |

---

## 6. Methods Transcribed This Session

| Type | Methods | Coverage | New? |
|------|---------|----------|------|
| eqq (roleplay controller) | 273/273 | 100% | NEW |
| ejv (jru handler) | 60/60 | 100% | NEW |
| eni (map name tracker) | 41/41 | 100% | NEW |
| MapRenderer | 39/39 | 100% | NEW |
| MapDisplayService | 38/38 | 100% | NEW |
| **TOTAL NEW** | **451** | | |
| **TOTAL ALL** | **1,103** | | (was 548 after V14) |

Previous types (unchanged): fys(49), fyv(26), fyr(16), fpm(161), ServerSelection(97), CharacterSelection(64), CharacterCreation(124), CharacterHeader(11), elv(104).

---

## 7. WebGLProbe.cs Changes (v31 → v32, +293 lines)

### New Structs
- `CellData` — cellNumber, mov, los, floor, speed, arrow
- `MapElement` — gfxId, color (ARGB uint), m11-m32 (2D affine transform), materialIndex

### New Static Decoders (from decompiled WorldPoint.FromMapId)
- `WorldIdFromMapId(long)` → world ID from bits 18-29
- `MapXFromMapId(long)` → signed x from bits 9-17
- `MapYFromMapId(long)` → signed y from bits 0-8

### `LoadMapData(mapId, worldId, root)` Coroutine
- Downloads `Content/Map/Data/mapdata_assets_world_{worldId}.bundle` via `UnityWebRequestAssetBundle`
- Finds `map_{mapId}` asset in bundle by name matching
- Loads as `ScriptableObject`
- Calls `ParseMapMetadata` via reflection
- Unloads previous map bundle before loading new one

### `ParseMapMetadata(ScriptableObject, mapId)` via Reflection
- Reads `mapData` field via reflection (bypasses broken IL — the type exists but constructor/methods are corrupted)
- Extracts 560 `cellsData` → `CellData[]` with mov/los/floor/speed/arrow
- Extracts bg/sort/fg elements → `MapElement[]` with gfxId/color/transform
- Reads neighbor mapIds (top/bottom/left/right)
- Logs: cell counts, walkable count, element counts per layer

### `ExtractMapElements(mapData, fieldName)` Helper
- Generic extractor for any element list field
- Reads gfxId, color.value (ARGB uint), transform (6 floats), materialIndex

### `PbVarFieldLong(byte[], int)` — New Protobuf Reader
- 64-bit varint reader for `long` fields (mapId in `jru` is `long`)

### `jru` Handler Expanded
- Decodes mapId → worldId + x + y via `WorldIdFromMapId`
- Triggers `LoadMapData` coroutine

### `lwt` Handler — New
- Parses field 6 (RepeatedField<llk> cell entries)
- For each cell: reads cellNumber (field 1) and attributes (field 5)
- Updates `_mapCells` array when available

---

## 8. Tools Updated

### transcribe_methods.py
- Paths updated: defaults to `Decompiled/` instead of `Il2CppDumper/Il2CppDumper V6.7.48/`
- Supports `DECOMPILED_DIR` env var for custom paths
- Supports `METADATA_PATH` env var for global-metadata.dat

### analyze_client.py
- Same path update: defaults to `Decompiled/` with `DECOMPILED_DIR` env var

---

## 9. What NOT to Repeat (V1-V15)

Everything from V1-V14 plus:
- Do NOT assume meshes/textures exist in MapMetadata bundles (V15) — they are 0 in the bundle, generated at runtime by MapRenderer
- Do NOT use `PbVarField` (int) for mapId parsing (V15) — mapId is `long`, use `PbVarFieldLong`
- Do NOT use short map IDs (MapInformationData.id) for bundle lookup (V15) — bundles use the full long mapId from WorldPoint encoding

---

## 10. Current State (v32, 15/Oct/2026)

```
[OK] Full auth flow: Login → ServerSelection → CharacterSelection → World
[OK] FMOD audio: menu music + 103 UI sounds
[OK] 114/114 Gen_ methods with lifecycle dispatch
[OK] 20 asset bundles loaded via manifest
[OK] 11 themes with T-key cycling
[OK] 0 errors through auth flow
[OK] P/Invoke pipeline verified via REA (585 FMOD P/Invokes correct)
[OK] Map asset discovery: 577 bundles, WorldPoint encoding decoded
[OK] jru handler: decodes mapId, loads map bundle
[OK] lwt handler: parses cell data entries
[OK] MapMetadata parser: reads cells + elements via reflection
[OK] 1,103 methods transcribed (was 548)

[PENDING] Atlas texture loading (mapgfx_1x_* bundles)
[PENDING] Visual map rendering (3 layers, element → mesh → draw)
[PENDING] Cell grid overlay (isometric, hover, click)
[PENDING] Character rendering (kub characteristics)
[PENDING] Game screens (inventory, fight, chat)
```

---

## 11. Key Files

| File | Description |
|------|-------------|
| `UnityProject/Assets/WebGLProbe.cs` | v32, 3944 lines — map loader, jru/lwt handlers, MapMetadata parser |
| `Tools/ClientAnalyzer/output/transcribed/eqq.json` | 273 methods — roleplay controller incl. lwt handler |
| `Tools/ClientAnalyzer/output/transcribed/MapRenderer.json` | 39 methods — Unity map rendering |
| `Tools/ClientAnalyzer/output/transcribed/MapDisplayService.json` | 38 methods — map display service |
| `Tools/ClientAnalyzer/output/transcribed/ejv.json` | 60 methods — jru map load handler |
| `Tools/ClientAnalyzer/output/transcribed/eni.json` | 41 methods — map name tracking |
| `Tools/ClientAnalyzer/transcribe_methods.py` | Updated: Decompiled/ paths + env vars |
| `Tools/ClientAnalyzer/analyze_client.py` | Updated: Decompiled/ paths + env vars |

---

## 12. Full Rendering Pipeline Analysis (from agent analysis of 39 MapRenderer methods + dump.cs)

### Complete Data Flow (original MapRenderer)
```
qdf(mapId, direction, force, callback) — validate + prepare transition
  → qdg(mapId) — async wrapper (UniTask.Create + Forget)
    → qdh(mapId) — LoadMap async (Addressables.LoadAssetAsync<MapMetadata>("map_{mapId}"))
      → qcy(metadata, mapId, gameObject) — HandleMapMetadataLoaded
        ├── td.qcb(metadata) — CreateMaterialsForMap (material factory)
        │   └── td.qca() — PopulateRenderData per layer
        ├── MapMetadata.bnzl() — PrepareElementsAndSprites (loads sprites, builds SpriteDetails)
        │   └── bnzn() — PrepareSpriteDetails: gfxId → SpriteDetails{sprite, vertices, triangles, uvs, ppu}
        ├── hde methods — build mesh from elements + SpriteDetails (Flash polygon vertices)
        ├── qcz/qda — load wind/wave textures
        └── qdk — enable shader features (15 flags)
  → LateUpdate (0.15s throttle)
    → qdo — rebuild mesh for displayable changes
```

### Key Rendering Types
| Type | Purpose |
|------|---------|
| td | Material factory — creates Materials from atlas data |
| tk | CachedMaterial — Material + texture binding cache |
| hde (static) | Mesh builder — builds mesh from elements + SpriteDetails |
| hdp (static) | Render queue ordering — base 3000, per-layer offsets |
| oh (static) | Cell grid — 560 cells, 14 cols, 20 rows, half-width 623.5 |
| zd (interface) | Displayable — entity on map (position, sort order, visibility) |
| su (interface) | SortUnit — depth-sorted entity (MapPoint, cell) |

### SpriteDetails (Flash-era polygon vertices — NOT quads)
```
SpriteDetails { sprite, triangles: List<ushort>, vertices: List<Vector2>, uvs: List<Vector2>, ppu: float }
```
Built by MapMetadata.bnzn(). Each sprite has custom polygon outline with triangulation.

## 13. Map Mesh Builder (WebGLProbe v32, +504 lines, now 4196)

### Atlas Loading Pipeline
1. Collect unique gfxIds from bg/sort/fg elements (182 for test map)
2. Determine bundle ranges: `mapgfx_1x_{range}_assets_all.bundle`
3. Load bundles via `UnityWebRequestAssetBundle`
4. Extract sprites: `assets/builtassets/textures/gfx/1x/{gfxId}.png`

### Mesh Building (per layer)
1. Pack sprites into atlas via `Texture2D.PackTextures(4096)`
2. Per element: apply affine transform (m11-m32) to quad vertices
3. UV from packed atlas rect, Color32 from ARGB tint
4. 3 submeshes: bg(3000), sort(3001), fg(3002)

### MakeReadable()
Non-readable textures → RenderTexture blit → ReadPixels (same technique as RebuildBundlesForWebGL)

## 14. Map Data Loading — WORKING (tested in browser)

### Platform Mismatch Discovery
Win64 map bundles (`mapdata_assets_world_{worldId}.bundle`) cannot be loaded in WebGL — `"can't be loaded because it was not built with the right version or build target."` Same issue as V02 for UI bundles.

### Solution: Pre-extract as JSON via UnityPy
New tool: `Tools/UnityPyTools/extract_map_data.py`
- Extracts MapMetadata from Win64 bundles as compact JSON
- Output: `Content/Map/Json/{mapId}.json` (served via `/assets/Map/Json/`)
- Test map 191104002: 132KB JSON, 560 cells, 1201+159+13 elements

### WebGLProbe: JSON map loader (replaces bundle loader)
- `LoadMapData` downloads `{mapId}.json` via `UnityWebRequest.Get`
- `ParseMapJson` parses cells + elements from JSON (custom parser, no JsonUtility)
- Triggered by `lwt` packet (not `jru` — emulator sends lwt directly on world entry)

### Browser Test Results (confirmed)
```
lwt: 153 cell entries (4068b)
lwt → map 191104002 (world=729 x=2 y=2)
[Map] Loading JSON: 191104002.json ...
[Map] JSON loaded: 132814 chars
[Map] 560 cells (320 walkable)
[Map] Elements: bg=1201 sort=159 fg=13
[Map] Map 191104002 parsed successfully
[Map] 182 unique gfxIds needed
[Map] 12 texture bundles needed
[Map] Total sprites: 0/182           ← BLOCKED: texture bundles also Win64
```

### Remaining blocker: Sprite textures
The `mapgfx_1x_*` texture bundles are also Win64. Need to extract sprites as PNG files (same approach as map JSON).

## 15. Additional Fixes This Session

- **ShowScreen**: sets `_currentScreen` even when UXML not found in bundles (was causing TestAction to re-trigger Play)
- **TestAction ServerSelection**: auto-selects first server when `_pendingServerId` is 0
- **TestAction CharacterSelection**: logs character name on selection
- **lwt handler**: extracts mapId from field 2.6, with fallback to character's known mapId
- **PbVarFieldLong**: 64-bit varint reader for long mapId fields
- **server.js**: ASSETS_DIR now defaults to `../Cliente 3.6.10.11/...` with env var override
- **server.js**: WEBGL_BUILD_DIR changed to `__dirname` (serves from WebServer/ root)
- **index.html**: `window.unityInstance` exposure patched after each build

## 16. Sprite Extraction + Map Mesh — WORKING

### extract_map_sprites.py (NEW)
- Extracts Texture2D from Win64 bundles as PNG files
- Output: `Content/Map/Sprites/1x/{gfxId}.png`
- Map 191104002: 180/182 sprites (2 missing: gfxIds 118928, 118929)

### WebGLProbe: PNG sprite loader + mesh builder
- Downloads 180 PNGs via `UnityWebRequestTexture.GetTexture` (~5s for all)
- Packs into atlas per layer via `Texture2D.PackTextures(4096)`
- Builds mesh: 5,484 vertices, 2,742 triangles, 3 submeshes

### Browser Test Results
```
[Map] Sprites loaded: 180/182 (2 failed)
[Map] bg: 1201 elements, 45 sprites, 2402 triangles
[Map] sort: 159 elements, 127 sprites, 314 triangles
[Map] fg: 13 elements, 11 sprites, 26 triangles
[Map] Mesh built: 5484 vertices, 3 submeshes, 3 materials
```

### Map not visible yet
The mesh is created but not visible — the UI Toolkit layer renders on top, and the camera/scale may need adjustment. The infrastructure works end-to-end.

## 17. MAP RENDERING VISIBLE — Session 11 Final Result

After fixing camera (ortho=500, pos=(21.5,0,-10)), cullingMask (-1 = all layers), and hiding UI on world entry (`_uiRoot.style.display = None` when kva received), the **isometric map is now visible in the browser**.

Screenshot `v32-map-ui-hidden.png` shows: terrain with stone paving, trees, buildings, market stalls, decorative elements — a recognizable Dofus 3 town map rendered in WebGL.

**Remaining visual issues:**
- Yellow/warm tint on elements (color tinting may have wrong byte order or alpha)
- Quad geometry instead of Flash polygon vertices (sprite edges not tight)
- Runtime atlas packing instead of pre-computed UV rects
- No cell grid overlay, no character sprites

## 18. Process Feedback — RESTRUCTURE NEEDED

The edit→build→test cycle (~15min per iteration) is too slow. Leona's directive: use REA systematically, research how recompilation projects work, automate the pipeline.

**Key insight:** Move rendering logic to Python extraction (fast iteration) instead of C# (slow builds).

**New approach for next session:**
1. Python extracts EVERYTHING: atlas PNGs, vertex buffers, UV rects, camera config
2. C# side becomes a thin data loader — minimal changes, rare rebuilds
3. Use REA formally: obligation ledger, evidence-based verification
4. Batch changes, test in Editor when possible, automate deploy pipeline

## 19. Next Steps (Priority — Next Session)

1. **Restructure extraction pipeline** — move atlas composition + vertex data to Python
2. **Fix color tinting** — check ARGB byte order in element color parsing
3. **Use pre-computed atlas rects** from MapMetadata (already in JSON)
4. **SpriteDetails polygon extraction** — new Python tool for Flash-era vertices
5. **Automate build+deploy+test** — single script for the full cycle
6. **Cell grid + character rendering** — after visual fidelity is correct
