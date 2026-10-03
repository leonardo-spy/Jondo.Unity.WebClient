# V14: Dofus 3 WebGL — Session 10 (02-03/Oct/2026)

## Summary

Session focused on FMOD audio, boot fidelity, lifecycle dispatch, and project reorganization. Probe v29 -> v31. 10 builds. 0 errors in final validated flow.

## 1. FMOD Audio — FIXED

### Root Cause
`MethodInfo.Invoke()` on a P/Invoke with `FMOD.RESULT` enum return type fails on IL2CPP WebGL: "Cannot marshal type 'FMOD.RESULT'". Direct compiled `[DllImport]` calls with `int` return type generate correct marshalling thunks at build time.

### Solution: FMODProxy.cs (NEW, 258 lines)
- Direct `[DllImport("__Internal")]` P/Invokes with `int` return type
- 11 functions: Create, Initialize, Release, Update, LoadBankMemory, GetEvent, CreateInstance, Start, Stop, SetVolume, SetPaused
- Header version `0x00020314` (FMOD 2.03.14, matching libfmodstudio.a)
- `PlayEvent(path)`, `PlayMusic(path)`, `PauseMusic()`, `ResumeMusic()`
- `#if UNITY_WEBGL && !UNITY_EDITOR` guards with editor stubs

### Audio Data Extraction
New tool: `Tools/UnityPyTools/extract_audio_data.py`
- Extracts AudioManagerLibrary from bundles via UnityPy
- 30,833 events, 7,090 banks, 103 UI sound events
- Output: `Resources/AudioData.json` with event paths and bank mappings

### Banks and Events (from AudioManagerLibrary)
- Master.bank (138MB) + Master.strings.bank (1.7MB) + UI.bank (8.5MB)
- Menu music: `event:/Musiques/Special/Musique_Menu`
- UI sounds: `event:/Ui/RollOver`, `event:/Ui/Bouton_Générique`, `event:/Ui/Menu_Jouer`, etc.
- Bank loading deferred to first user interaction (WebAudio policy)

### Tab Defocus Fix
`OnApplicationFocus(bool)` calls `FMODProxy.PauseMusic()` / `ResumeMusic()` via `FMOD_Studio_EventInstance_SetPaused`.

## 2. Lifecycle Gen_ Dispatch — 114/114 Methods Wired

### Problem
`ApplyGeneratedMethods` called all Gen_ methods in a flat pass at screen load time. Only 24/114 had call sites. No lifecycle distinction (ctor vs OnEnable vs state change).

### Solution
Replaced with 4 dispatchers in `GeneratedScreenMethods.cs`:
- `DispatchGen_Ctor(screen, root)` — 6 screens
- `DispatchGen_OnEnable(screen, root)` — 5 screens
- `DispatchGen_Localize(screen, root)` — 3 screens
- `DispatchGen_Event(screen, event, root)` — all 114 methods routable by name

### Call Sites Wired
- `ShowScreen()` + `ShowLoginOverlay()`: Ctor + Localize + OnEnable
- `SetLoginState()`: Event("Login", "mx") + disablers
- `HandleServerMessage()`: AuthAccepted -> DisplayServer/DisplayFilter, kvi -> OnCharactersListUpdated
- `SelectServerCard()`: OnSelectionChanged

## 3. Data-Driven Boot

### Manifest Bundle Discovery
Replaced 17 hardcoded bundle names with `AssetBundleManifest.GetAllAssetBundles()`. Loads the "WebGL" manifest bundle first, discovers all 20 bundles automatically.

### BootLogic.json
Loaded at boot. Parses phase count (6) + service count (182) for data-driven logging. Bank paths (bankPath, masterBank, stringsBank) read from JSON instead of hardcoded.

### DofusUISettings.json
33 TSS GUIDs parsed (11 themes x 3 font sizes). Enables theme switching.

### AudioData.json
103 UI events + bank mappings loaded at boot. Drives FMOD event playback.

## 4. Hardcode Eliminated

| Before | After |
|--------|-------|
| 17 hardcoded bundle names | Manifest-based discovery (20 bundles) |
| FMOD event paths hardcoded | AudioData.json (30,833 events from AudioManagerLibrary) |
| PostProcessLoginTree (80 lines) | REMOVED — Gen_PreGameMainMenu_ctor with I18n |
| DiagnoseUniTask (harmless NullRef) | REMOVED — 0 errors at boot |
| Login "admin/admin" hardcoded | Reads from TextInput UXML fields |
| Theme Standard only | 11 themes from ThemeColors_*.json |

## 5. UniTask — Diagnostic Removed

The `DiagnoseUniTask()` call was proactively triggering `RunClassConstructor` on UniTask's type handle, which threw a harmless NullReferenceException every boot. The Cpp2IL UniTask.dll (1.4MB, 96% IL) works fine — the diagnostic was unnecessary. Removed in v31.

## 6. Project Reorganization

Entire WebGL client organized as `Jondo.Unity.WebClient/` subproject:
- UnityProject/ (11,876 files from C:\DofusWeb\)
- Tools/ (310 files, 12 tool dirs)
- Decompiled/ (145 files: dump.cs 65MB + script.json 143MB + DummyDll)
- Generated/ (30 files, pipeline output)
- WebServer/ (Node.js server)
- docs/webgl/ (14 translated guides)
- Git LFS for large files

## 7. Files Changed

| File | Lines | Change |
|------|-------|--------|
| FMODProxy.cs | 258 | NEW — direct P/Invoke + event playback + pause/resume |
| GeneratedScreenMethods.cs | 1,324 | 4 lifecycle dispatchers (all 114 routable) |
| WebGLProbe.cs | ~3,600 | v31: themes, DofusUISettings, OnApplicationFocus, BootLogic data-driven |
| AudioData.json | 8 KB | NEW — extracted from AudioManagerLibrary |
| BootLogic.json | 42 KB | NEW in Resources |
| extract_audio_data.py | tool | NEW — UnityPy AudioManagerLibrary extraction |

## 8. Extraction Usage (Session 10)

| Resource | Used? | Notes |
|----------|-------|-------|
| ClientData.json | YES | Factories, USS, asset paths |
| RuntimeData.json | YES | Screen-UXML map, server types |
| ScreenBehavior.json | YES | 7 screens, I18n, states |
| BootLogic.json | YES | 6 phases, 182 services, bank paths |
| AudioData.json | YES | 103 UI events, banks, menu music |
| DofusUISettings.json | YES | 33 TSS GUIDs (11 themes x 3 sizes) |
| ThemeColors + 10 variants | YES | T key cycles themes |
| ServerNames.json | YES | 14 server names |
| I18n (4 langs) | YES | 147 string keys |
| Gen_ methods | 114/114 | Lifecycle dispatch |
| FigmaStyles.json | NO | Reference only (183 style defs) |
| ScreenRegistry.json | NO | Superseded by RuntimeData |

Runtime data usage: **12/21 files (57%)**

## 9. Next Steps

1. Map rendering (lwt terrain data)
2. Game screens (inventory, fight, chat)
3. elv audio service Gen_ methods (105 transcribed, 0 generated)
4. fpm auth manager Gen_ methods (161 transcribed, 0 generated)
5. Core.dll IL recovery improvement (currently 5%)
