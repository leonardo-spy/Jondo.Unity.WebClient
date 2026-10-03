# V01: Dofus 3 WebGL — IL2CPP Client to WebGL Browser Pipeline

## Goal

Transform the Dofus 3 client (Unity 6, IL2CPP, Windows 64-bit) into a WebGL build that runs in the browser, connecting to the JondoEmu emulator via WebSocket.

## Prerequisites

- **Unity 6** (6000.3.16f1) with WebGL module installed
- **AssetRipper** — extracts the Unity project from the compiled client
- **Il2CppDumper** v6.7.48 — generates stub DLLs with valid metadata + headers for Ghidra
- **Cpp2IL** (2022.1.0-pre-release.21) — reconstructs method bodies via IL recovery
- **Ghidra** 12.1.3 + JDK 21 — decompiles GameAssembly.dll to pseudo-C (reference)
- **Mono.Cecil** 0.11.6 (via NuGet) — DLL merge/fix tools
- **Python 3** — test HTTP server
- **Node.js** — web server with WebSocket proxy

## Full Pipeline (correct path)

### 1. Extract the Unity project

```bash
AssetRipper "Cliente 3.6.10.11"
```

Move the extracted project to a short path (e.g. `C:\DofusWeb`) to avoid EPERM due to long paths on Windows.

### 2. Generate stubs with Il2CppDumper

```bash
Il2CppDumper.exe GameAssembly.dll Dofus_Data/il2cpp_data/Metadata/global-metadata.dat OutputFolder
```

Result: `DummyDll/` with stubs (.dll), `il2cpp.h`, `script.json`, `dump.cs`, `stringliteral.json`.

### 3. Generate DLLs with IL Recovery via Cpp2IL

```bash
Cpp2IL.exe --game-path "Cliente 3.6.10.11" --use-processor callanalyzer --output-as dll_il_recovery --output-to Cpp2IL_Output
```

**DO NOT use** `attributeinjector` — causes crash in Unity Linker (invalid custom attributes metadata).

### 4. Merge: copy method bodies from Cpp2IL into the stubs

The Il2CppDumper stubs have valid metadata (correct GUIDs for Unity). Cpp2IL has real IL but broken metadata. The solution: use Mono.Cecil to copy method bodies from Cpp2IL into the stubs.

**Tool: `Tools/MergeIL/`** — for each method in each DLL:
- Opens the stub (has valid metadata)
- Opens the Cpp2IL output (has IL recovery)
- Copies the body from Cpp2IL into the stub (match by fullname + parameters)
- Saves the stub with real bodies

Result: 158k/177k methods merged (89%). The failures are DLLs that reference UnityEngine.CoreModule (engine DLLs that don't need merging).

### 5. Fix P/Invoke methods

The merged DLLs have P/Invoke methods (DllImport for Win32 APIs like SendMessage, FMOD, etc.) that don't exist in WebGL. The Unity Linker/IL2CPP crashes when processing them.

**Tool: `Tools/FixPInvoke/`** — for each PInvokeImpl method:
- Removes the old method from the type
- Creates a new managed method with the same signature
- Body: `return default;` (void -> ret, valuetype -> initobj+ldloc+ret, ref -> ldnull+ret)
- Result: 779 P/Invokes replaced

**IMPORTANT:** Mono.Cecil does not persist changes to the PInvokeImpl flag in ReadWrite mode. You need to:
1. Read the DLL into a MemoryStream
2. Remove the old MethodDefinition from the type
3. Create a new MethodDefinition (without PInvokeImpl)
4. Insert at the same index
5. Write to a .tmp file and then File.Move

### 6. Fix __Il2CppComObject

Il2CppDumper generates an assembly `__Generated.dll` with internal IL2CPP types (`System.__Il2CppComObject`, `__Il2CppFullySharedGenericType`). IL2CPP also generates these types internally -> duplicate key conflict.

Solution:
- Remove `__Generated.dll` from Plugins
- Remove `Il2CppDummyDll.dll` from Plugins (nothing references it)
- Any type that inherits from `System.__Il2CppComObject` -> change base type to `System.Object` via Mono.Cecil
- Remove assembly references to `__Generated` from all DLLs

### 7. Add [Preserve] attributes

With `preserve="nothing"` in link.xml (necessary to avoid VTable conflicts), the linker strips MonoBehaviours from the scene. Solution: add `[UnityEngine.Scripting.PreserveAttribute]` to all MonoBehaviour/ScriptableObject types in DLLs via Mono.Cecil.

Result: 654 types preserved.

### 8. Configure the Unity project

**`Assets/Editor/WebGLBuildConfig.cs`:**
```csharp
public void OnPreprocessBuild(BuildReport report)
{
    if (report.summary.platform != BuildTarget.WebGL) return;
    PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
    PlayerSettings.stripEngineCode = false;
    PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Debug);
    PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
    PlayerSettings.SetAdditionalIl2CppArgs("--entry-assembly-name=Core");
}
```

**`Assets/link.xml`:**
```xml
<linker>
  <assembly fullname="Core" preserve="nothing"/>
  <assembly fullname="Ankama.SpinConnection" preserve="nothing"/>
  <!-- ... all game assemblies with preserve="nothing" -->
</linker>
```

**DO NOT use `preserve="all"`** — causes `System.__Il2CppComObject::Finalize()` VTable duplicate in IL2CPP.

**Plugin .meta files:** All set to WebGL-only, Editor disabled, validateReferences=0.

**Scene:** Single scene `DofusUnity.unity` in Build Settings.

### 9. Place DLLs in Plugins

- Copy all merged+fixed DLLs to `Assets/Plugins/`
- **Backup the stubs** outside `Assets/` (e.g. `C:\DofusWeb\Plugins_stubs_backup\`) — if they remain inside Assets, Unity imports them as duplicate assemblies
- Keep the original `.meta` files from AssetRipper (they have the GUIDs that the scene references)

### 10. Build WebGL

Build via Unity Editor. Output in `PlayBrowser/WebClient/unity-build/`:
- `unity-build.wasm.gz` (~8.5MB)
- `unity-build.data.gz` (~5.5MB)
- `unity-build.framework.js.gz` (~91KB)
- `unity-build.loader.js` (~27KB)
- `index.html`

### 11. Serve with correct headers

A simple Python server does NOT work — it needs `Content-Encoding: gzip` in the headers. Use a custom `unity_server.py` or the Node.js server (`PlayBrowser/WebClient/server.js`).

### 12. Runtime: WebGLProbe injects GameCompositor

The scene has a GUID mismatch for the Compositor (missing script). `WebGLProbe.cs` uses `Type.GetType("Core.Engine.Compositor.GameCompositor, Core")` + `AddComponent` to inject it manually.

## Known Problems and Solutions

| Problem | Cause | Solution |
|---------|-------|----------|
| `preserve="all"` -> VTable duplicate | `__Il2CppComObject::Finalize()` conflict | Use `preserve="nothing"` + `[Preserve]` attribute |
| Missing scripts in scene | GUID mismatch after DLL merge | Inject via `AddComponent` in WebGLProbe |
| P/Invoke crash in Linker | Win32/FMOD DllImport | FixPInvoke tool replaces all |
| `__Generated` duplicate key | IL2CPP generates internally | Remove DLL, fix base types |
| `MovedFromExtractor` crash | URP DLL with broken metadata | Use DLL from AssetRipper (not Il2CppDumper) for URP |
| `ElementType 0x54` (Minimal stripping) | MarkEntireAssembly parses IL2CPP metadata | Use Medium stripping |
| FontAsset NullRef | Corrupted fonts from AssetRipper | Ignore (does not block build) |
| FMOD audio | Native plugin does not exist in WebGL | Exclude AudioManagement or stub P/Invokes |

## Decompilation with Ghidra (reference)

The `decompiled_all.c` (964MB, 308k functions) serves as reference to understand the original logic when IL recovery methods don't work correctly.

Setup:
1. Import `GameAssembly.dll` into Ghidra (no auto-analysis on import)
2. Parse C Source: `il2cpp_ghidra.h` (fix keywords: `else` -> `else_`, `restrict` -> `restrict_`, `final` -> `final_`, `_asm` -> `asmRef`)
3. Run `ghidra_with_struct.py` (py3 version of the gist) with `script.json`
4. Wait for auto-analysis to complete
5. Export everything with `export_decompile.py`

## Dofus 3 Boot Structure

```
Awake -> InitCompositor (async UniTaskVoid)
  +-- Logging setup (Ankama.Logging.Log)
  +-- Thread ID save (gal.dwrz)
  +-- GameCompositor.Compose (virtual, async UniTask<bool>)
        +-- Service container (glo) creation
        |   +-- glj = service glf/glr
        |   +-- glk = service glg/glx
        |   +-- gll = Dictionary<Type, gly>
        |   +-- glm = service IMessage/glu (protobuf)
        |   +-- Queue with capacity 15
        +-- fyh + ecr (coordinator) creation
        +-- Compositor.Compose (base, async UniTask<bool>)
              +-- LoadDataDependencies (await UniTask)
              +-- bkqb() + bkqc(2) -- progress
              +-- PreInitServices (await UniTask<bool>) -- iterates Dictionary<Type, gao>
              +-- InitializeServices (await UniTask)
              +-- bkqc(3) + bkqh + bkqc(4) -- loading
              +-- isRunning = true
              +-- bkqi + bkqc(5) -- finalization
              +-- return true
```

## Networking

- `Ankama.SpinConnection.Network.Layers.TcpConnectionLayer` — TCP (needs WebSocket replacement)
- Protocol: varint-prefixed protobuf with envelope `type.ankama.com/xxx`
- Emulator port 5555 already accepts WebSocket (handshake detected by peeking "GET ")
- Node.js proxy: `/game-ws` -> TCP 5555

## Current State

The GameCompositor was injected via probe and is executing. Black screen with no visible errors. The async bootstrap (UniTask) probably fails silently on:
- Network (TcpConnectionLayer doesn't work in WebGL)
- Addressables (needs to point to the HTTP server)
- Audio (FMOD doesn't exist)

Next step: add try/catch inside the Compositor's async methods to see where it fails, and then replace the incompatible subsystems with WebGL equivalents.
