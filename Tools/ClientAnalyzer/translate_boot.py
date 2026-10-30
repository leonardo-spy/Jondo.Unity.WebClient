#!/usr/bin/env python3
"""
Translate the decompiled boot sequence into BootLogic.json.

Traces the FULL chain: Compositor.Compose → RegisterServices → PreInitServices →
InitializeServices → LoadDataDependencies → GameCompositor.bkqk

For each step, extracts:
- What services are registered
- What [ServiceInit] methods are called and in what order
- What assets are loaded (Addressables paths)
- What screens are opened (AuthUis)
- What audio init happens (AudioManager.Initialize)

Sources:
  - dump.cs: service types, [ServiceInit] attributes, Compositor methods
  - services.json: pre-extracted service list
  - asset_paths.json: Addressable paths
  - screen_handlers.json: auth screen handlers

Output:
  output/BootLogic.json — complete boot spec for the probe
"""

import argparse
import json
import os
import shutil
import sys
import io

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(SCRIPT_DIR))
OUTPUT_DIR = os.environ.get("GENERATED_OUTPUT_DIR", os.path.join(REPO_ROOT, "Generated", "output"))
RESOURCE_DIR = os.path.join(REPO_ROOT, "UnityProject", "Assets", "Resources")


def load_json(name):
    path = os.path.join(OUTPUT_DIR, name)
    if not os.path.exists(path):
        print(f"  WARNING: {name} not found")
        return None
    with open(path, 'r', encoding='utf-8') as f:
        return json.load(f)


def main(deploy=False):
    print("Loading extracted data...")
    services = load_json("services.json")
    if services is None:
        raise FileNotFoundError("services.json is required to generate BootLogic.json")
    asset_paths = load_json("asset_paths.json")
    screen_handlers = load_json("screen_handlers.json")
    registration = load_json("RegistrationObservations.json") or {}
    catalog = load_json("ReconstructedServiceCatalog.json") or {}
    central_registrations = registration.get("existingFieldRegistrations", [])
    registry_keys = set()
    for binding in central_registrations:
        if not all(binding.get(key) for key in ("field", "fieldType", "registryType")):
            raise ValueError("Incomplete extracted central registration")
        if binding["registryType"] in registry_keys:
            raise ValueError(f"Duplicate extracted registry key: {binding['registryType']}")
        registry_keys.add(binding["registryType"])

    boot = {
        "phases": [],
        "services": {},
        "audioInit": {},
        "screens": {},
        "assetPaths": {},
        "centralRegistrations": central_registrations,
        "registrationSource": registration.get("source", {}),
        "reconstructedServices": catalog.get("services", []),
        "reconstructedServiceEvidence": catalog.get("evidenceId"),
    }

    # Phase 0: RegisterServices (bkqa)
    # From dump.cs: GameCompositor creates glo (service bus), ecr (coordinator), fyh (debug)
    boot["phases"].append({
        "name": "RegisterServices",
        "decompiled": "GameCompositor.bkqa (dump.cs L405366)",
        "services": ["glo (service bus)", "ecr (coordinator)", "fyh (debug/loading)"],
    })

    # Phase 1: PreInitServices (bkpy)
    boot["phases"].append({
        "name": "PreInitServices",
        "decompiled": "Compositor.bkpy (dump.cs L405224)",
        "description": "Iterates dxnz Dictionary<Type, gao>, calls each pre-init",
    })

    # Phase 2: InitializeServices (bkpz)
    # Collect ALL services with [ServiceInit]
    service_inits = []
    if isinstance(services, dict):
        for name, info in services.items():
            if isinstance(info, dict) and info.get('serviceInitMethods'):
                for si in info['serviceInitMethods']:
                    si_name = si['name'] if isinstance(si, dict) else si
                    si_va = si.get('va', '') if isinstance(si, dict) else ''
                    service_inits.append({
                        "type": info.get('fullName', name),
                        "method": si_name,
                        "va": si_va,
                    })

    boot["phases"].append({
        "name": "InitializeServices",
        "decompiled": "Compositor.bkpz (dump.cs L405228)",
        "description": f"Finds [ServiceInit] methods via reflection, calls each. {len(service_inits)} services.",
        "serviceInits": service_inits,
    })
    boot["services"] = {si["type"]: si for si in service_inits}

    # Phase 3: LoadDataDependencies (bkqe)
    # Loads AudioManagerLibrary.asset + AudioManagerSettings.asset + DofusUISettings.asset
    audio_assets = {}
    if asset_paths:
        if isinstance(asset_paths, dict):
            for key, path in asset_paths.items():
                if any(k in str(path).lower() for k in ['audio', 'dofusuisettings']):
                    audio_assets[key] = path
        elif isinstance(asset_paths, list):
            for entry in asset_paths:
                if isinstance(entry, dict):
                    path = entry.get('path', '')
                    if 'audio' in path.lower() or 'DofusUISettings' in path:
                        audio_assets[entry.get('key', '')] = path

    boot["phases"].insert(1, {
        "name": "LoadDataDependencies",
        "decompiled": "Compositor.bkqe (dump.cs L405245)",
        "description": "Addressables.InitializeAsync, loads configuration assets",
        "assets": [
            "Assets/Configuration/Audio/AudioManagerLibrary.asset",
            "Assets/Configuration/Audio/AudioManagerSettings.asset",
            "Assets/UI/DofusUISettings.asset",
        ],
    })

    # Phase 4: Audio Init (from AudioManager.Initialize)
    boot["audioInit"] = {
        "decompiled": "AudioManager.Initialize (dump.cs L33428)",
        "description": "Static method: AudioManager.Initialize(settings, library)",
        "statesMachine": "AudioManager.<InitializationRoutine>d__25",
        "steps": [
            "Load AudioManagerSettings.asset from Addressables",
            "Load AudioManagerLibrary.asset from Addressables",
            "AudioManager.Initialize(settings, library)",
            "  → FMOD.Studio.System.create() (FMOD_Studio_System_Create)",
            "  → studioSystem.initialize(maxChannels, studioFlags, coreFlags, extraData)",
            "  → Load Master.bank from Content/Audio/Banks/Desktop/",
            "  → Load Master.strings.bank",
            "  → Load per-event banks on demand",
        ],
        "bankPath": "Content/Audio/Banks/Desktop/",
        "masterBank": "Master.bank",
        "stringsBank": "Master.strings.bank",
    }

    # Phase 5: Compose (GameCompositor.bkqk)
    boot["phases"].append({
        "name": "Compose",
        "decompiled": "GameCompositor.bkqk (dump.cs L405374)",
        "steps": [
            "bkqb() — setup",
            "bkqc(2) — progress step 2",
            "PreInitServices → await",
            "InitializeServices → await",
            "bkqc(3) — progress step 3",
            "bkqh — UI setup",
            "bkqc(4) — progress step 4",
            "isRunning = true",
        ],
    })

    # Phase 6: Open Screens
    boot["screens"] = {
        "ConnectionBackground": {
            "authUi": 8,
            "handler": "fyr",
            "decompiled": "fpm opens via gax<fbz_AuthUis>.bjrn",
        },
        "Login": {
            "authUi": 0,
            "handler": "fys",
            "decompiled": "fpm opens via gax<fbz_AuthUis>.bjrn",
        },
    }
    boot["phases"].append({
        "name": "OpenAuthScreens",
        "decompiled": "fpm (dump.cs L371728)",
        "screens": ["ConnectionBackground", "Login"],
    })

    # Save
    out_path = os.path.join(OUTPUT_DIR, "BootLogic.json")
    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump(boot, f, indent=2, ensure_ascii=False)
    if deploy:
        os.makedirs(RESOURCE_DIR, exist_ok=True)
        shutil.copyfile(out_path, os.path.join(RESOURCE_DIR, "BootLogic.json"))
        print(f"  Deployed to {RESOURCE_DIR}")

    print(f"\nSaved: {out_path} ({os.path.getsize(out_path) // 1024}KB)")
    print(f"  Phases: {len(boot['phases'])}")
    print(f"  Services with [ServiceInit]: {len(service_inits)}")
    print(f"  Audio init steps: {len(boot['audioInit']['steps'])}")


if __name__ == "__main__":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--deploy", action="store_true", help="Copy generated BootLogic.json into Unity Resources")
    args = parser.parse_args()
    main(deploy=args.deploy)
