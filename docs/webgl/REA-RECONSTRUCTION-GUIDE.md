# REA Reconstruction Tools — How to Use for This Project

## Understanding (from schema analysis, session 11)

REA has 4 formal reconstruction tools. After studying their full schemas:

### 1. `build_reconstruction_obligation_ledger`
**What it does:** Creates a formal tracking ledger of reconstruction obligations (what needs to be rebuilt).
**Requirements:** Evidence bundle (artifacts + SHA256 digests + providers + records), reviewed obligations (with IDs, layers, dependencies), and a manifest (bindings with owners, fixtures, verifiers).
**Practical for us:** The input complexity is very high. Each obligation needs evidence IDs, artifact SHA256s, fixture tests, and verifier commands. NOT practical to construct manually for 119K methods in Core.dll.
**When to use:** For tracking subsystem-level obligations (auth=DONE, map=PARTIAL, fight=TODO, etc.) — not per-method tracking.

### 2. `import_managed_reconstruction`
**What it does:** Registers decompiled/transcribed methods against the original binary, anchored by metadata token + signature SHA256 + IL hash.
**Requirements:** An `inspect_managed_members` Evidence record (static_members), plus decompiler info and per-method reconstruction text.
**Practical for us:** Works for DLLs where `inspect_managed_members` succeeds (< 67MB metadata budget). That's 7 of 10 key Ankama DLLs. Does NOT work for Core.dll (119K methods exceed budget).
**When to use:** Register the 1,103 transcribed methods from smaller DLLs formally.

### 3. `verify_reconstruction`
**What it does:** Verifies behavioral or structural claims against Evidence.
**Requirements:** A specification with named claims, each linked to comparison Evidence IDs.
**Practical for us:** Useful for comparing WebGL vs original output. Would need `compare_functions` or `compare_process_captures` Evidence first.
**When to use:** After implementing a subsystem, verify it matches the original.

### 4. `evaluate_reconstruction_coverage`
**What it does:** Measures coverage against a named boundary (what surfaces/claims are covered).
**Requirements:** Complete coverage definition with surfaces, owners, claims, verifier contracts, and package proofs.
**When to use:** Periodic progress check — what percentage of the game is reconstructed.

## Practical REA Strategy for This Project

### Use REA for:
- **DLL inspection** (`inspect_managed_artifact/members/native_boundaries`) — proven to work on smaller DLLs
- **P/Invoke audit** — verified 585 FMOD P/Invokes correctly
- **Version comparison** (`compare_managed_members`) — when Ankama releases 3.6.11.x
- **Subsystem-level obligation tracking** — not per-method

### Use Python pipeline for:
- **Core.dll analysis** — dump.cs + decompiled_all.c (REA can't handle 119K methods)
- **Data extraction** — UnityPy for bundles, OPS for strings, CSS reconstruction
- **Runtime data** — JSON + PNG served via HTTP
- **Iteration** — Python runs in seconds, REA Ghidra times out on GameAssembly.dll

### The hybrid approach:
1. Python extracts ALL data offline (seconds)
2. REA verifies correctness on smaller DLLs
3. C# is a thin loader (downloads pre-computed JSON/PNG)
4. Obligation tracking at subsystem level (Python-side JSON, not REA ledger)

## Current Obligations (tracked informally — pragmatic approach)

| Subsystem | Status | Evidence |
|-----------|--------|----------|
| auth.login | DONE | Playwright screenshot, 0 errors |
| auth.serverSelection | DONE | 14 servers parsed, kvi received |
| auth.characterSelection | DONE | Character WebGLTest loaded |
| audio.fmod | DONE | REA verified 585 P/Invokes, 103 UI events |
| ui.factories | DONE | 226 types, data-driven from ClientData.json |
| ui.themes | DONE | 11 themes, T-key cycling |
| ui.css | DONE | 902 USS loaded, MergedUSS 4242 rules |
| map.dataLoading | DONE | 560 cells, 1373 elements, JSON extraction |
| map.spriteLoading | DONE | 180/182 PNGs, composed atlas |
| map.rendering | PARTIAL | Visible but quads (not polygons), tint correct |
| map.camera | PARTIAL | Ortho 500, but _currentScreen bug |
| map.cellGrid | TODO | Constants extracted (CellGrid.json) |
| character.rendering | TODO | kub received but not displayed |
| entity.displayables | TODO | DisplayableService traced (dump.cs L55231) |
| game.inventory | TODO | Not started |
| game.fight | TODO | Not started |
| game.chat | TODO | Not started |
| protocol.opcodes | PARTIAL | auth+world opcodes handled, 30+ game opcodes pending |
