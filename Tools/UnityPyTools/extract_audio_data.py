"""
Extract audio event data from Dofus 3 client bundles via UnityPy.
Reads AudioManagerLibrary to get event ID -> FMOD GUID mapping + bank references.
Also reads Master.strings.bank to discover all event paths.

Output: AudioData.json -> C:\DofusWeb\Assets\Resources\AudioData.json
"""
import sys, json, os, struct, re

import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.16f1"

BUNDLE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11",
    "Dofus_Data", "StreamingAssets", "aa", "StandaloneWindows64")
BANK_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Cliente 3.6.10.11",
    "Dofus_Data", "StreamingAssets", "Content", "Audio", "Banks", "Desktop")
OUT_DIR = r"C:\DofusWeb\Assets\Resources"

# --- Step 1: Extract event paths from Master.strings.bank ---
def extract_fmod_events(bank_dir):
    """Read the Master.bank to enumerate event paths via binary scanning."""
    events = []
    banks_info = []

    master = os.path.join(bank_dir, "Master.bank")
    strings = os.path.join(bank_dir, "Master.strings.bank")

    if not os.path.exists(master):
        print(f"  Master.bank not found at {master}")
        return events, banks_info

    # Scan strings bank for readable path-like strings
    for bp in [strings, master]:
        if not os.path.exists(bp):
            continue
        data = open(bp, 'rb').read()
        for m in re.finditer(rb'event:/[\x20-\x7e]{3,200}', data):
            path = m.group().decode('ascii').rstrip('\x00')
            if path not in [e['path'] for e in events]:
                events.append({"path": path, "source": os.path.basename(bp)})

    # List all bank files
    for f in sorted(os.listdir(bank_dir)):
        if f.endswith('.bank') and f != 'Master.bank' and f != 'Master.strings.bank':
            size = os.path.getsize(os.path.join(bank_dir, f))
            banks_info.append({"file": f, "size": size})

    print(f"  Events from bank scan: {len(events)}")
    print(f"  Additional bank files: {len(banks_info)}")
    return events, banks_info


# --- Step 2: Extract AudioManagerLibrary from bundles ---
def extract_audio_manager(bundle_dir):
    """Find AudioManagerLibrary/Settings in bundles via UnityPy typetree."""
    import glob
    bundles = glob.glob(os.path.join(bundle_dir, "*.bundle"))
    if not bundles:
        bundles = [f for f in glob.glob(os.path.join(bundle_dir, "*"))
                   if not f.endswith(('.manifest', '.meta'))]

    audio_data = {"library": None, "settings": None, "events": [], "banks": []}

    for bp in bundles:
        name = os.path.basename(bp)
        if "monoscripts" in name or "unitybuiltinassets" in name:
            continue
        try:
            env = UnityPy.load(bp)
        except:
            continue

        for obj in env.objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                tree = obj.read_typetree()
                if not tree:
                    continue
            except:
                continue

            obj_name = tree.get("m_Name", "")

            # AudioManagerLibrary
            if "m_sounds" in tree or "m_events" in tree:
                print(f"  Found potential AudioManagerLibrary in {name}: {obj_name}")
                audio_data["library"] = {"bundle": name, "name": obj_name}

                # Extract event entries
                for field_name in ["m_sounds", "m_events", "m_eventReferences"]:
                    items = tree.get(field_name, [])
                    if not items:
                        continue
                    print(f"    {field_name}: {len(items)} entries")
                    for item in items[:500]:
                        entry = {}
                        if isinstance(item, dict):
                            entry["id"] = item.get("m_eventId", item.get("eventId", item.get("id", "")))
                            entry["guid"] = item.get("m_guid", item.get("guid", ""))
                            entry["bankRef"] = item.get("m_bankRef", item.get("bank", ""))
                        audio_data["events"].append(entry)

                # Extract bank references
                for field_name in ["m_banks", "m_bankList"]:
                    items = tree.get(field_name, [])
                    if items:
                        print(f"    {field_name}: {len(items)} entries")
                        for item in items[:200]:
                            if isinstance(item, dict):
                                audio_data["banks"].append({
                                    "name": item.get("m_name", item.get("name", "")),
                                    "file": item.get("m_file", item.get("file", "")),
                                    "guid": item.get("m_guid", item.get("guid", ""))
                                })

            # AudioManagerSettings
            if obj_name == "AudioManagerSettings" or "m_masterBank" in tree:
                print(f"  Found AudioManagerSettings in {name}: {obj_name}")
                audio_data["settings"] = {
                    "bundle": name, "name": obj_name,
                    "masterBank": tree.get("m_masterBank", ""),
                    "banksPath": tree.get("m_banksPath", tree.get("m_bankPath", "")),
                }

            # Also check for any MonoBehaviour with FMOD-related fields
            keys = set(tree.keys()) if isinstance(tree, dict) else set()
            fmod_keys = keys & {"m_studioSystem", "m_bankPath", "m_masterBank",
                                "m_eventId", "m_audioEvents", "m_audioLibrary"}
            if fmod_keys:
                print(f"  FMOD-related SO in {name}: {obj_name} keys={fmod_keys}")

    return audio_data


# --- Main ---
print("=== Audio Data Extraction ===")

print("\n[1] Scanning FMOD banks...")
events, bank_files = extract_fmod_events(BANK_DIR)

print(f"\n[2] Scanning bundles for AudioManager...")
audio_mgr = extract_audio_manager(BUNDLE_DIR)

# Merge
result = {
    "bankPath": "Content/Audio/Banks/Desktop/",
    "masterBank": "Master.bank",
    "stringsBank": "Master.strings.bank",
    "eventsFromBank": events,
    "bankFiles": bank_files[:20],
    "bankFilesTotal": len(bank_files),
    "audioManager": audio_mgr
}

# Save
os.makedirs(OUT_DIR, exist_ok=True)
out_path = os.path.join(OUT_DIR, "AudioData.json")
with open(out_path, 'w', encoding='utf-8') as f:
    json.dump(result, f, indent=2, ensure_ascii=False)
print(f"\n[3] Saved -> {out_path}")
print(f"    Events: {len(events)}, Bank files: {len(bank_files)}")
print(f"    AudioManager events: {len(audio_mgr.get('events', []))}")
print(f"    AudioManager banks: {len(audio_mgr.get('banks', []))}")
