# Jondo.Unity.WebClient

Run the Dofus 3 Unity client (IL2CPP, Windows 64-bit) in the browser via WebGL, connecting to the JondoEmu emulator via WebSocket.

## Current Status (v31, Client 3.6.10.11, Oct 2026)

**Working:**
- Full auth flow: Login -> ServerSelection -> CharacterSelection -> World entry
- FMOD audio: menu music + 103 UI sound events (click, hover, navigation)
- 114/114 Gen_ methods with lifecycle dispatch (ctor/OnEnable/Localize/Event)
- 20 asset bundles loaded via manifest discovery
- 11 theme variants with T-key cycling
- 0 errors through the entire flow (validated via Playwright)

**Not yet working:**
- Map rendering (lwt terrain data received but not rendered)
- Character rendering (kub characteristics received but not displayed)
- Game screens beyond auth (inventory, fight, chat)

## Key Data Sources

The project relies on three layers of extracted data. When one layer is incomplete, the next fills the gap:

| Source | Size | What it provides | When to use |
|--------|------|-----------------|-------------|
| **dump.cs** | 65 MB | Type structures: classes, fields, methods, VAs (1.98M lines) | Always — every tool reads it |
| **decompiled_all.c** | 964 MB | Pseudo-C function bodies from Ghidra (308K functions) | When IL recovery is incomplete (Core.dll = 5% IL) |
| **Cpp2IL DLLs** | ~200 MB | Recovered IL method bodies (~89% methods, many empty) | For the WebGL build (merged into stubs) |

**Why decompiled_all.c matters:** The IL recovery from Cpp2IL/MergeIL only recovers ~5% of Core.dll. The other 95% are empty stubs. When we need to know what a method actually DOES, we read the Ghidra pseudo-C. This is how 548 methods were transcribed, 1,905 UI operations extracted, the FMOD init chain traced, and OPS string decryption discovered. Five tools read directly from it. See `Decompiled/README.md` for full details and regeneration instructions.

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| Unity | 6000.3.16f1 + WebGL module | Build the WebGL client |
| .NET SDK | 10.0 | Build the .NET tools (MergeIL, FixPInvoke, etc.) |
| Python 3 | 3.12+ | Run extraction/analysis scripts |
| UnityPy | 1.25+ | `pip install UnityPy` — read bundles that Unity can't load (GUID mismatch) |
| Node.js | 18+ | WebGL dev server with WebSocket proxy |
| Il2CppDumper | v6.7.48 | Generate stubs + metadata from client |
| Cpp2IL | 2022.1.0-pre-release.21 | IL recovery from IL2CPP |
| Ghidra | 12.1.3 + JDK 21 | Decompile GameAssembly.dll to pseudo-C (decompiled_all.c) |
| AssetRipper | Latest | Extract Unity project from compiled client |

Python packages: `pip install UnityPy`

**Ghidra setup guide:** https://gist.github.com/BadMagic100/47096cbcf64ec0509cf75d48cfbdaea5

---

## Quick Start (from this repo's working state)

If you cloned this repo and have the Dofus 3.6.10.11 client installed:

```bash
# 1. Open UnityProject/ in Unity 6 (6000.3.16f1)

# 2. Point the server to your client's Content directory:
export DOFUS_ASSETS_DIR="/path/to/Cliente 3.6.10.11/Dofus_Data/StreamingAssets/Content"

# 3. Build WebGL (Unity menu: Build > Build WebGL, or batch mode):
"/path/to/Unity" -batchmode -nographics -projectPath UnityProject \
  -executeMethod WebGLBuildMenu.BuildWebGL -quit

# 4. Start services
bash Scripts/start-webgl.sh

# 5. Open http://localhost:3000
```

---

## Updating to a New Client Version

When Ankama releases a new client (e.g., 3.6.11.x), most of the work is regenerating data. The tools, scripts, and probe code are **reusable** — only the data they consume changes.

### What changes between client versions

| Component | Changes? | Action needed |
|-----------|----------|---------------|
| **Obfuscated type names** (glo, ecr, fys...) | YES — reshuffled each patch | Re-run dump analysis, update name references |
| **Opcodes** (kva, kvi, kqz...) | YES — reshuffled each patch | Use `protocolbuilder` to generate new Op.cs mapping |
| **Asset bundles** | MAYBE — content changes, structure usually stable | Re-extract with AssetRipper, rebuild for WebGL |
| **FMOD banks** | MAYBE — new audio events added/renamed | Re-extract AudioData.json |
| **I18n binaries** | YES — new strings added | Re-run parse_i18n.py |
| **DLL IL** | YES — new code, different recovery | Full MergeIL pipeline |
| **Tools (Python/C#)** | NO — reusable as-is | Just re-run with new input |
| **WebGLProbe.cs** | PARTIAL — hardcoded obfuscated names need updating | Search for old names, replace with new |
| **FMODProxy.cs** | NO — P/Invoke signatures are FMOD API, not game-specific | Reusable, check FMOD version |
| **GeneratedScreenMethods.cs** | YES — regenerated from new decompiled data | Re-run generate_probe_methods.py |
| **Node.js server** | NO — generic proxy | Reusable |
| **RebuildBundlesForWebGL.cs** | NO — reads bundle format, not game-specific names | Reusable |

### Update steps (abbreviated — only re-run what changed)

```bash
# 1. Extract new client
AssetRipper "path/to/New Client"

# 2. Regenerate decompiled data
Il2CppDumper.exe NewGameAssembly.dll ... OutputFolder
# Copy dump.cs, script.json, DummyDll/ to Decompiled/
# Run Ghidra on new GameAssembly.dll -> new decompiled_all.c

# 3. DLL pipeline (Steps 4-9 from Full Pipeline below)
Cpp2IL.exe --game-path "New Client" --use-processor callanalyzer ...
dotnet run --project Tools/MergeIL -- ...
dotnet run --project Tools/FixPInvoke -- ...
dotnet run --project Tools/FixComObject -- ...
dotnet run --project Tools/FixCctor -- ...
dotnet run --project Tools/RevertFMODStubs -- ...

# 4. Regenerate all analysis data
py Tools/ClientAnalyzer/analyze_client.py  # re-indexes everything
py Tools/ClientAnalyzer/transcribe_methods.py fys  # new obfuscated names
# ... repeat for all handler types

# 5. Regenerate Gen_ methods + runtime data
py Tools/ClientAnalyzer/translate_screens.py
py Tools/ClientAnalyzer/generate_probe_methods.py  # -> new GeneratedScreenMethods.cs
py Tools/ClientAnalyzer/synthesize_screens.py --deploy
py Tools/UnityPyTools/extract_audio_data.py  # -> new AudioData.json

# 6. Update WebGLProbe.cs obfuscated names
# The probe references names like "dxnw", "dxnx", "glo", "ecr", "fyh"
# These are extracted from dump.cs and change each patch.
# Search the new dump.cs for the same TYPES (by inheritance/interface)
# and replace the old names in WebGLProbe.cs.

# 7. Rebuild bundles + build WebGL
# Unity: Build > 1 - Extract + Rebuild for WebGL
# Unity: Build > Build WebGL
```

### Handling obfuscated name changes

The game uses three-letter obfuscated names that reshuffle on every patch. The emulator's `protocolbuilder` tool handles opcode remapping automatically. For the WebGL client, the key types to re-identify are:

| Purpose | Current name (3.6.10.11) | How to find in new version |
|---------|--------------------------|---------------------------|
| Service bus | `glo` | Implements `ezp` interface (14 abstract methods) |
| Coordinator | `ecr` | Constructor takes (ezp, fyh) |
| Debug/loading | `fyh` | Has `[ServiceInit]` method |
| Login handler | `fys` | Inherits `fzy<LoginBinding>` |
| Auth manager | `fpm` | Manages `Dictionary<AuthUis, screen>` |
| Audio service | `elv` | Has `AudioManagerLibrary` field + `PlaySound` methods |
| Connection | `ewz` | Has `ConnectAsync` + `TcpConnectionLayer` |

The `analyze_client.py` tool identifies these automatically by their structural patterns (inheritance, fields, interfaces), not by name.

---

## Full Pipeline: From Scratch (New Game, First Time)

This is the complete 13-step pipeline used to create the initial WebGL build. It took 12 sessions over 23 days to develop and refine. The steps below incorporate all lessons learned — warnings mark approaches that were tried and failed.

### Step 1: Extract Unity Project with AssetRipper

```bash
AssetRipper "path/to/Dofus Client"
```

Move the extracted project to a short path (e.g., `C:\DofusWeb`) to avoid Windows EPERM errors on long paths.

### Step 2: Generate Stubs with Il2CppDumper

```bash
Il2CppDumper.exe GameAssembly.dll Dofus_Data/il2cpp_data/Metadata/global-metadata.dat OutputFolder
```

Copy `dump.cs`, `script.json`, `stringliteral.json`, and `DummyDll/` to `Decompiled/`.

### Step 3: Decompile with Ghidra (decompiled_all.c)

This produces the 964MB pseudo-C file that 5 tools depend on. Without it, you cannot transcribe methods, extract UI operations, or trace the boot chain. See `Decompiled/README.md` for the full Ghidra setup.

Guide: https://gist.github.com/BadMagic100/47096cbcf64ec0509cf75d48cfbdaea5

```
1. Import GameAssembly.dll into Ghidra (NO auto-analysis on import)
2. Parse C Source: il2cpp_ghidra.h (fix keywords: else->else_, restrict->restrict_, final->final_, _asm->asmRef)
3. Run ghidra_with_struct.py (Python 3 port) with script.json
4. Let auto-analysis complete (~1-4 hours, increase JVM heap with -Xmx8g if crashes at 55%)
5. Export all functions with export_decompile.py
```

Result: `decompiled_all.c` (~964 MB). Place it in `Decompiled/`.

Then index it for random access:
```bash
py Tools/ClientAnalyzer/analyze_client.py --phase index
```

### Step 4: Generate IL Recovery DLLs with Cpp2IL

```bash
Cpp2IL.exe --game-path "Client Directory" --use-processor callanalyzer --output-as dll_il_recovery --output-to Cpp2IL_Output
```

> **WARNING:** Do NOT use `--use-processor attributeinjector` — it crashes the Unity Linker with invalid custom attribute metadata. Discovered in Session 1 (V01).

### Step 5: Merge IL (Tools/MergeIL)

Copies method bodies from Cpp2IL output into the Il2CppDumper stubs (which have valid GUIDs for Unity).

```bash
dotnet run --project Tools/MergeIL -- path/to/DummyDll path/to/Cpp2IL_Output path/to/UnityProject/Assets/Plugins
```

Result: ~158K/177K methods merged (89%). Engine DLLs that reference UnityEngine.CoreModule fail — expected.

> **NOTE:** The stubs from Il2CppDumper have the correct GUIDs that Unity's scene/asset system references. The Cpp2IL DLLs have real IL but broken GUIDs. MergeIL combines the best of both.

### Step 6: Fix P/Invokes (Tools/FixPInvoke)

Replaces Win32 P/Invoke methods (SendMessage, FMOD, etc.) with managed stubs (`return default`).

```bash
dotnet run --project Tools/FixPInvoke -- path/to/UnityProject/Assets/Plugins
```

> **IMPORTANT:** Mono.Cecil does NOT persist PInvokeImpl flag changes in ReadWrite mode. The tool must: read DLL to MemoryStream, remove old method, create NEW MethodDefinition without PInvokeImpl, insert at same index, write to .tmp file, then File.Move. Discovered in Session 1 (V01).

### Step 7: Fix COM Object References (Tools/FixComObject)

Auto-detects and fixes broken IL from Cpp2IL (DOTween generics, `__Il2CppFullySharedGenericType`).

```bash
dotnet run --project Tools/FixComObject -- path/to/UnityProject/Assets/Plugins/Core.dll
```

Uses poison-pattern auto-detection — no hardcoded type list needed. Also removes `__Generated.dll` and `Il2CppDummyDll.dll` from Plugins, and changes any type inheriting `System.__Il2CppComObject` to inherit `System.Object`.

### Step 8: Fix Static Constructors (Tools/FixCctor)

Replaces all `.cctor` methods with a single `ret` instruction to prevent crashes from broken static initialization.

```bash
dotnet run --project Tools/FixCctor -- path/to/UnityProject/Assets/Plugins
```

### Step 9: Revert FMOD Stubs (Tools/RevertFMODStubs)

Restores FMOD P/Invoke declarations that were stubbed by FixPInvoke in Step 6.

```bash
dotnet run --project Tools/RevertFMODStubs -- path/to/UnityProject/Assets/Plugins --target webgl
```

> **WARNING:** Must match BOTH prefixes: `FMOD5_*` (391 core API) AND `FMOD_Studio_*` (194 studio API). An early version only matched FMOD5_ and missed all 194 Studio methods — they stayed as `return default` stubs. Discovered in Session 9 (V13).

### Step 10: Generate link.xml

In Unity Editor: **Build > Generate link.xml**

Scans all Plugin DLLs for VisualElement subclasses and generates `preserve="all"` entries for custom types + their UxmlSerializedData inner classes.

> **WARNING:** Do NOT use `preserve="all"` on ALL assemblies — it causes `System.__Il2CppComObject::Finalize()` VTable duplicate in IL2CPP. Use `preserve="nothing"` for game assemblies + selective `preserve="all"` only for custom VisualElement types. Discovered in Session 1 (V01), refined in Session 2 (V03).

> **WHY preserve="all" on UxmlSerializedData:** When these inner classes are preserved, `m_SerializedData` paradoxically becomes null at runtime — forcing the factory path which we control. Without it, Unity uses the native CreateInstance path which produces broken elements. This was the key discovery of Session 2 (V03-V04).

### Step 11: Rebuild Asset Bundles for WebGL

In Unity Editor: **Build > 1 - Extract + Rebuild for WebGL**

This is a complex multi-pass operation (90KB script):
1. **Pass 1 (Two-pass load):** Load ALL Win64 bundles first, then extract assets
2. **Pass 1.25:** Extract MergedUSS from ThemeStyleSheets via SerializedObject scan
3. **Pass 2 (Remap):** Fix cross-bundle references (1,397 refs remapped)
4. **Pass 3 (CSS):** Reconstruct USS as CSS text (Unity 6 per-rule selectors)
5. **Pass 4 (Build):** Build bundles for WebGL target

> **WARNING:** USS files must be saved as `.uss` text (not `.uss.asset` YAML). Saving as .asset makes Unity treat them as ScriptableObjects — the CSS rules exist but `m_ComplexSelectors` is never compiled. Discovered in Session 3 (V05).

> **WARNING:** ThemeStyleSheet inherits from StyleSheet in Unity 6. Check `asset is ThemeStyleSheet` BEFORE `asset is StyleSheet` in the extraction loop, otherwise TSS files fall into the CSS reconstruction branch and fail. Discovered in Session 7 (V10).

### Step 12: Run Client Analysis Pipeline

```bash
# Full pipeline (all phases)
py Tools/ClientAnalyzer/analyze_client.py

# Transcribe screen handlers
py Tools/ClientAnalyzer/transcribe_methods.py fys
py Tools/ClientAnalyzer/transcribe_methods.py ServerSelection
py Tools/ClientAnalyzer/transcribe_methods.py CharacterSelection
py Tools/ClientAnalyzer/transcribe_methods.py CharacterCreation
py Tools/ClientAnalyzer/transcribe_methods.py CharacterHeader
py Tools/ClientAnalyzer/transcribe_methods.py fyv
py Tools/ClientAnalyzer/transcribe_methods.py fyr
py Tools/ClientAnalyzer/transcribe_methods.py fpm
py Tools/ClientAnalyzer/transcribe_methods.py elv  # audio service

# Generate runtime data
py Tools/ClientAnalyzer/translate_screens.py
py Tools/ClientAnalyzer/generate_probe_methods.py
py Tools/ClientAnalyzer/synthesize_screens.py --deploy
py Tools/ClientAnalyzer/translate_boot.py

# Parse I18n (FNV-1a hash: offset 0xcbf29ce484222325, prime 0x100000001b3, UTF-16 chars)
py Tools/I18nParser/parse_i18n.py en.bin --behavior Generated/output/ScreenBehavior.json -o UnityProject/Assets/Resources/I18n/en.json

# Extract audio + themes via UnityPy (bypasses MonoScript GUID mismatch)
py Tools/UnityPyTools/extract_audio_data.py
py Tools/ExtractThemeColors/extract_all_themes.py
```

> **NOTE:** The `transcribe_methods.py` tool reads from `decompiled_all.c` using byte offsets from `decompiled_index.json`. Without these files, transcription is impossible and you'll have empty Gen_ method bodies. The decompiled_all.c is the bridge between "knowing what types exist" (dump.cs) and "knowing what the code does."

> **NOTE:** UnityPy is needed because custom ScriptableObjects (FigmaThemeScriptable, AudioManagerLibrary, DofusUISettings) can't be loaded by Unity Editor — the MonoScript GUIDs in the bundles don't match the MergeIL'd DLLs. UnityPy reads the raw bundle data directly. Discovered in Session 5 (V08).

### Step 13: Build WebGL

```bash
"/path/to/Unity" -batchmode -nographics -projectPath UnityProject \
  -executeMethod WebGLBuildMenu.BuildWebGL -quit -logFile build.log
```

After build, patch `WebServer/unity-build/index.html` to expose the Unity instance:
```javascript
// Add inside .then((unityInstance) => { ... }):
window.unityInstance = unityInstance;
```

> **NOTE:** Unity regenerates index.html on every build. This patch must be re-applied each time. The `window.unityInstance` exposure is needed for Playwright testing via `SendMessage`.

---

## What NOT to Do (Lessons from 12 Sessions)

These approaches were tried during development and confirmed to fail. See `docs/webgl/JOURNEY.md` for the full story behind each one.

| Do NOT | Why it fails | Do this instead |
|--------|-------------|-----------------|
| `preserve="all"` on ALL assemblies | VTable duplicate (`__Il2CppComObject::Finalize`) | Selective preserve + `[Preserve]` attribute |
| `GetUninitializedObject` + ctor | Virtual dispatch corruption (contentContainer = null) | `UxmlFactory<VisualElement>` + `base.Create` |
| `UxmlFactory<T>` with T=custom type | Constructor body IL corrupted by Cpp2IL | Factory creates `VisualElement`, traits apply UXML attrs |
| `StyleSheet.m_ComplexSelectors` top-level | Does NOT exist in Unity 6 | Per-rule reconstruction from `m_Rules[i].m_ComplexSelectors` |
| USS brute-force load all files | Font MissingAssetReference spam every frame | Selective loading + font fix in CSS reconstruction |
| `FontAsset` from Win64 bundle via `LoadAsset` | Platform-specific serialization = null | Download .ttf + create SDF FontAsset at build time |
| `FigmaThemeScriptable` from Editor bundles | MonoScript GUID mismatch | UnityPy extraction (bypasses Unity type system) |
| Hardcode element names or CSS classes | Don't match actual USS selectors | OPS string decryption (28,517 strings) + dump.cs const fields |
| Heuristics by name ("contains Button") | Fails on obfuscated names | Real inheritance chain from dump.cs |
| Reflection + P/Invoke with enum return | IL2CPP WebGL can't marshal | FMODProxy.cs: direct `[DllImport]` with `int` return |
| FMOD stubs only matching `FMOD5_*` | Misses 194 `FMOD_Studio_*` methods | Match BOTH prefixes |
| Hardcode FMOD event paths | Wrong paths, version-specific | Extract from AudioManagerLibrary via UnityPy |
| Cpp2IL `--use-processor attributeinjector` | Crashes Unity Linker | Use `callanalyzer` only |

---

## Tool Reference

| Tool | Language | Purpose | Key Script |
|------|----------|---------|------------|
| ClientAnalyzer | Python | Universal extraction: dump.cs + decompiled_all.c + metadata | `analyze_client.py` |
| transcribe_methods | Python | Transcribe decompiled method bodies (548+ methods) | `transcribe_methods.py` |
| translate_screens | Python | Extract UI operations from decompiled (1,905 ops) | `translate_screens.py` |
| generate_probe_methods | Python | Transpile ScreenLogic to C# Gen_ methods (114 methods) | `generate_probe_methods.py` |
| synthesize_screens | Python | Merge all data into ScreenBehavior.json (7 screens, 312 elements) | `synthesize_screens.py` |
| translate_boot | Python | Extract boot chain to BootLogic.json (6 phases, 182 services) | `translate_boot.py` |
| I18nParser | Python | Parse binary localization (339K strings/lang, FNV-1a hash) | `parse_i18n.py` |
| UnityPyTools | Python | Bundle scanning, font/texture/audio extraction via UnityPy | `scan_all_bundles.py`, `extract_audio_data.py` |
| ExtractThemeColors | Python | Extract FigmaTheme colors (11 themes, 25 colors each) | `extract_all_themes.py` |
| MergeIL | .NET/Cecil | Copy IL from Cpp2IL into stubs (158K methods) | `Program.cs` |
| FixPInvoke | .NET/Cecil | Replace Win32 P/Invokes with managed stubs (779 total) | `Program.cs` |
| FixComObject | .NET/Cecil | Auto-fix broken IL via poison-pattern detection | `Program.cs` |
| FixCctor | .NET/Cecil | Empty broken static constructors (6,035 total) | `Program.cs` |
| RevertFMODStubs | .NET/Cecil | Restore 585 FMOD P/Invoke declarations | `Program.cs` |

## Architecture

### WebGLProbe.cs (~3,600 lines)

The main bootstrap that replaces the original `Compositor.Compose` async flow (broken in WebGL because Core.dll has only 5% IL recovery — the async state machines are empty stubs). It:

1. **Loads runtime data** — BootLogic.json, AudioData.json, DofusUISettings.json, ScreenBehavior.json, ClientData.json, I18n, ThemeColors (all extracted from client binaries)
2. **Registers services** (glo, ecr, fyh) via reflection — 3 of 182 services
3. **Loads asset bundles** via manifest discovery — 20 bundles auto-discovered
4. **Initializes FMOD** via FMODProxy — system created at boot, banks loaded on first user click
5. **Opens screens** (ConnectionBackground -> Login -> ServerSelection -> CharacterSelection)
6. **Handles protocol** — varint-prefixed protobuf via WebSocket, opcode parsing
7. **Dispatches Gen_ methods** — 114 methods with lifecycle awareness (Ctor/OnEnable/Localize/Event)

### FMODProxy.cs (258 lines)

Direct `[DllImport("__Internal")]` P/Invokes with `int` return type instead of `FMOD.RESULT` enum. This was the solution to the IL2CPP WebGL marshalling failure: reflection-based P/Invoke calls with enum return types fail because IL2CPP can't generate marshalling thunks dynamically. Direct compiled calls work because the thunks are generated at build time.

### GeneratedScreenMethods.cs (~1,324 lines)

114 methods auto-generated from decompiled screen handlers by `generate_probe_methods.py`. Four lifecycle dispatchers route all methods:
- `DispatchGen_Ctor` — constructor initialization (6 screens)
- `DispatchGen_OnEnable` — screen show (5 screens)
- `DispatchGen_Localize` — I18n text application (3 screens)
- `DispatchGen_Event` — state changes and interactions (all 114 methods routable by name)

### WebSocketBridge.cs + .jslib

Browser-side WebSocket + HTTP fetch bridge. The emulator's game server (port 5555) auto-detects WebSocket vs TCP connections via a 4-byte peek ("GET " = WebSocket handshake, otherwise raw TCP).

## Configuration

Tools use environment variables for paths. Set these before running:

```bash
export DOFUS_CLIENT_DIR="/path/to/Client"
export UNITY_PROJECT_DIR="/path/to/Jondo.Unity.WebClient/UnityProject"
export DECOMPILED_DIR="/path/to/Jondo.Unity.WebClient/Decompiled"
export DOFUS_ASSETS_DIR="/path/to/Client/Dofus_Data/StreamingAssets/Content"  # for server.js
```

If not set, tools default to relative paths based on the project structure.

## Testing

```bash
# Start all services (emulator on 5555/8888 + node server on 3000)
bash Scripts/start-webgl.sh

# Automated flow test via Playwright SendMessage:
window.unityInstance.SendMessage('__Probe', 'TestAction', 'enter')  // Play
# Wait 8s
window.unityInstance.SendMessage('__Probe', 'TestAction', 'enter')  // Server select
# Wait 8s
window.unityInstance.SendMessage('__Probe', 'TestAction', 'enter')  // Character -> World

# Keyboard shortcuts in browser:
# H = toggle debug overlay
# T = cycle themes (11 variants)
# Escape = toggle PreGameMainMenu
# Enter = confirm current screen action
# F = full diagnostic dump to console
```

## Documentation

- `docs/webgl/JOURNEY.md` — Complete map of 12 sessions, 102 subagent investigations, and what was tried/discarded
- `docs/webgl/V01-V14` — Session-by-session technical guides (translated to English)
- `docs/webgl/custom-elements-constructors.md` — Analysis of 13 custom VisualElement constructors
- `Decompiled/README.md` — How to generate decompiled files from a new client (Ghidra guide)

| Guide | Session | Key Topic |
|-------|---------|-----------|
| V01 | 1 | Complete IL2CPP -> WebGL pipeline |
| V02 | 1 | Asset bundles, UI Toolkit bootstrap |
| V03 | 2 | Custom element factories, m_SerializedData discovery |
| V04 | 2 | USS reference remap (1,397 cross-bundle refs) |
| V05 | 3 | Unity 6 CSS per-rule reconstruction, Lexend font |
| V06 | 4 | I18n binary parser, HAAPI login flow |
| V07 | 4 | Root cause: inlineSheet assets fileID:0 |
| V08 | 5 | UnityPy breakthrough, 13 specialized factories |
| V09 | 6 | OPS Obfuscator decryption (28,517 strings), data-driven factories |
| V10 | 7 | MergedUSS extraction, CSS var() fix, sprite variable tracing |
| V11 | 7 | Full auth protocol, 2-phase reconnect, dead code removal |
| V12 | 8 | Phased boot, server carousel, world entry (30+ opcodes) |
| V13 | 9 | 548/548 transcription, UniTask Cpp2IL, FMOD linking |
| V14 | 10 | FMODProxy (marshalling fix), 114/114 lifecycle dispatch, themes |
