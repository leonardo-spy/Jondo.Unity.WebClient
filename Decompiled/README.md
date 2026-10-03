# Decompiled Files

These files are the **source of truth** for the entire WebGL client project. When the IL recovery from Cpp2IL is incomplete (Core.dll has only 5% IL), the decompiled pseudo-C from Ghidra fills the gap. Multiple tools read directly from these files.

## The Role of Each File

### dump.cs (65 MB) — Type Structures
**Source:** Il2CppDumper v6.7.48
**Content:** 1.98M lines — every class, struct, enum, field, method signature, and virtual address in the game.
**Used by:** `analyze_client.py` (parse types, fields, methods), `transcribe_methods.py` (find method VAs), `synthesize_screens.py` (binding structures), `parse_screens.py` (screen handlers)

### script.json (143 MB) — Address-to-Symbol Mapping
**Source:** Il2CppDumper v6.7.48
**Content:** Maps RVA addresses to C function names. Used to resolve method names when dump.cs VA doesn't match directly.
**Used by:** `transcribe_methods.py` (RVA alias resolution — IL2CPP deduplicates methods with identical bodies)

### decompiled_all.c (964 MB) — INCLUDED (Git LFS)
**Source:** Ghidra 12.1.3 + Il2CppDumper scripts (see regeneration steps below for new client versions)
**Content:** 308,000 pseudo-C functions decompiled from GameAssembly.dll. This is the **actual implementation code** — when dump.cs only shows method signatures, decompiled_all.c has the bodies.
**Used by:**
- `analyze_client.py --phase index` — builds byte-offset index (238,972 functions)
- `analyze_client.py --phase constructors` — extracts VE constructor operations
- `transcribe_methods.py` — reads method bodies by byte offset (548+ methods transcribed)
- `translate_screens.py` — extracts 1,905 UI operations from handler bodies
- `translate_boot.py` — traces the boot chain from Compositor.Compose

**WHY IT MATTERS:** The IL recovery from Cpp2IL/MergeIL only recovers ~5% of Core.dll method bodies. The remaining 95% are empty stubs. When we need to understand what a method DOES (not just its signature), we read the pseudo-C from decompiled_all.c. This is how we:
- Transcribed 548 screen handler methods
- Extracted 1,905 UI operations
- Discovered the OPS Obfuscator XOR decryption
- Traced the FMOD audio initialization chain
- Mapped the UIUtils functions (pca, pdb, pdd, etc.)
- Found the FNV-1a hash for I18n string keys

### stringliteral.json (3 MB) — String Literal Table
**Source:** Il2CppDumper v6.7.48
**Used by:** Reference only — most string lookups use the OPS decryption from global-metadata.dat

### DummyDll/ (143 files, 26 MB) — Stub DLLs
**Source:** Il2CppDumper v6.7.48
**Content:** Type skeleton DLLs with valid Unity GUIDs but no method bodies. These are the BASE that MergeIL copies Cpp2IL method bodies INTO.
**Used by:** `Tools/MergeIL` (target for IL merge), Unity Editor (GUID references for scene/asset binding)

## How to Generate from a New Client

### Prerequisites
| Tool | Version | Download |
|------|---------|----------|
| Il2CppDumper | v6.7.48 | https://github.com/Perfare/Il2CppDumper/releases |
| Cpp2IL | 2022.1.0-pre-release.21 | https://github.com/SamboyCoding/Cpp2IL/releases |
| Ghidra | 12.1.3 | https://github.com/NationalSecurityAgency/ghidra/releases |
| OpenJDK | 21 | Required by Ghidra |
| Python 3 | 3.12+ | For Ghidra scripts |

### Step 1: Il2CppDumper — Generate Stubs + Metadata

```bash
Il2CppDumper.exe GameAssembly.dll Dofus_Data/il2cpp_data/Metadata/global-metadata.dat OutputFolder
```

Produces: `dump.cs`, `script.json`, `stringliteral.json`, `il2cpp.h`, `DummyDll/`

Copy `dump.cs`, `script.json`, `stringliteral.json`, and `DummyDll/` into this `Decompiled/` directory.

### Step 2: Convert Header for Ghidra

```bash
python il2cpp_header_to_ghidra.py
```

Produces `il2cpp_ghidra.h` (Ghidra-compatible C header with type definitions).

### Step 3: Ghidra — Import and Analyze GameAssembly.dll

Reference: https://gist.github.com/BadMagic100/47096cbcf64ec0509cf75d48cfbdaea5

#### 3a. Import
1. Launch Ghidra → File → New Project
2. Open Code Browser (dragon icon)
3. File → Import File → select `GameAssembly.dll` (NOT DummyDll outputs)
4. **When prompted for auto-analysis, select NO** (do it after script import)

#### 3b. Parse C Header
1. File → Parse C Code
2. Select `VisualStudio22_64.prf` → Save Profile to New Name
3. Clear ALL entries from "Source Files", "Include Paths", "Parse Options"
4. Add `il2cpp_ghidra.h` to "Source Files to Parse"
5. Click "Parse to Program" → Continue → Use Open Archives

**Fixing parse errors:** The header may have syntax errors from Ghidra's parser. Common fixes in `il2cpp_ghidra.h`:
- `else` → `else_` (reserved keyword)
- `restrict` → `restrict_`
- `final` → `final_`
- `_asm` → `asmRef`
Comment out problematic struct bodies and rebuild them later in Ghidra's Structure Editor.

#### 3c. Run Il2CppDumper Script
1. Open Script Manager (green play button)
2. Add Il2CppDumper directory to Script Directories
3. Run `ghidra_with_struct.py` (or `ghidra_with_struct_py3.py` for Ghidra 12.1+)
4. When prompted, select the `script.json` from Step 1

**Python 3 compatibility:** For Ghidra 12.1+ with PyGhidra, add `#@runtime PyGhidra` at the top of the script. The project includes a Python 3 port that handles MSVC keyword sanitization.

#### 3d. Auto-Analyze
After the script completes, let Ghidra run its auto-analysis. **This takes 1-4 hours** depending on hardware. Expect ~308K functions.

**Memory warning:** Ghidra may crash at ~55% if JVM heap is too small. Increase with `-Xmx8g` in `ghidraRun.bat`.

#### 3e. Export All Decompiled Functions
Run `export_decompile.py` from the Script Manager to export all decompiled functions to a single file.

Result: `decompiled_all.c` (~964 MB for client 3.6.10.11)

### Step 4: Index the Decompiled Output

Place `decompiled_all.c` where the tools expect it (default: alongside `dump.cs` in this directory, or set `DECOMPILED_DIR` env var).

```bash
py Tools/ClientAnalyzer/analyze_client.py --phase index
```

Produces: `Generated/output/decompiled_index.json` (41 MB) — byte-offset index of all 238,972 functions. This index enables random access into the 964MB file without loading it entirely.

### Step 5: Cpp2IL — IL Recovery DLLs

```bash
Cpp2IL.exe --game-path "Client Directory" --use-processor callanalyzer --output-as dll_il_recovery --output-to Cpp2IL_Output
```

**DO NOT** use `--use-processor attributeinjector` — it crashes the Unity Linker with invalid custom attribute metadata.

Result: 143 DLLs with recovered IL method bodies (~89% methods recovered, but many bodies are empty or partial).

## File Placement for Tools

The tools expect these files at specific paths. Set environment variables or use the default structure:

```
Jondo.Unity.WebClient/
  Decompiled/
    dump.cs                    ← Il2CppDumper output
    script.json                ← Il2CppDumper output
    stringliteral.json         ← Il2CppDumper output
    decompiled_all.c           ← Ghidra export (generate yourself, 964MB)
    DummyDll/                  ← Il2CppDumper output (143 stub DLLs)
```

Or set environment variables:
```bash
export DECOMPILED_DIR="/path/to/Jondo.Unity.WebClient/Decompiled"
export DOFUS_CLIENT_DIR="/path/to/Cliente 3.6.10.11"
```

## UnityPy — Extracting Data Unity Can't Load

Some client data (FigmaThemeScriptable, AudioManagerLibrary, DofusUISettings) can't be loaded by Unity Editor due to MonoScript GUID mismatch between the original bundles and the MergeIL'd DLLs. **UnityPy** (Python package) reads the Win64 bundles directly, bypassing Unity's type system.

```bash
pip install UnityPy
```

Tools that use UnityPy:
- `Tools/UnityPyTools/scan_all_bundles.py` — discovers custom ScriptableObjects in all bundles
- `Tools/UnityPyTools/extract_audio_data.py` — extracts AudioManagerLibrary (30,833 events)
- `Tools/UnityPyTools/extract_fonts.py` — extracts .ttf fonts + SDF atlas data
- `Tools/ExtractThemeColors/extract_all_themes.py` — extracts 11 FigmaTheme color sets
