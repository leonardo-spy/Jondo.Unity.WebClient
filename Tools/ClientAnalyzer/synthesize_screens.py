#!/usr/bin/env python3
"""
Synthesize ScreenBehavior.json from all extracted client data.

Combines bindings, screen handlers, I18n keys, transcribed methods,
and RuntimeData into a single data-driven spec for the WebGL probe.

Usage:
    py Tools/ClientAnalyzer/synthesize_screens.py
    py Tools/ClientAnalyzer/synthesize_screens.py --deploy   # also copy to DofusWeb Resources

Sources read:
    output/bindings.json            — 717 UI binding structs (element names + types)
    output/screen_handlers.json     — 258 screen handlers (handler → binding, methods)
    output/obfuscated_strings.json  — 28,517 OPS strings (I18n keys, element names)
    output/transcribed/*.json       — Decompiled method logic (display toggles, I18n, events)
    output/enums.json               — State machine values (AuthUis, fyq, dsp serverType)
    output/asset_paths.json         — 385 asset path constants
    output/css_selectors_all.json   — 1,910 CSS selectors

Output:
    output/ScreenBehavior.json      — Complete screen spec for the WebGL probe
"""

import json
import os
import re
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(SCRIPT_DIR, "output")
DEPLOY_DIR = r"C:\DofusWeb\Assets\Resources"

AUTH_SCREENS = {
    "ConnectionBackground": {
        "handler": "fyr",
        "authUi": 8,
        "bindingKey": "Core.UI.Bindings.ConnectionBackgroundBinding",
    },
    "Login": {
        "handler": "fys",
        "authUi": 0,
        "bindingKey": "Core.UILogic.Bindings.LoginBinding",
    },
    "ServerSelection": {
        "handler": "ServerSelection",
        "authUi": 1,
        "bindingKey": "Core.UILogic.Bindings.ServerSelectionBinding",
    },
    "CharacterSelection": {
        "handler": "CharacterSelection",
        "authUi": 2,
        "bindingKey": "Core.UILogic.Bindings.CharacterSelectionBinding",
    },
    "CharacterCreation": {
        "handler": "CharacterCreation",
        "authUi": 3,
        "bindingKey": "Core.UILogic.Bindings.CharacterCreationBinding",
    },
    "PreGameMainMenu": {
        "handler": "fyv",
        "authUi": 4,
        "bindingKey": "Core.UI.Bindings.PreGameMainMenuBinding",
    },
    "CharacterHeader": {
        "handler": "CharacterHeader",
        "authUi": 7,
        "bindingKey": "Core.UILogic.Bindings.CharacterHeaderBinding",
    },
}

TEMPLATE_BINDINGS = {
    "ServerCard": "Core.UILogic.Bindings.ServerCardBinding",
    "ServerLine": "Core.UILogic.Bindings.ServerLineBinding",
    "ServerFilter": "Core.UILogic.Bindings.ServerFilterBinding",
    "CharacterLine": "Core.UILogic.Bindings.CharacterLineBinding",
}

I18N_PREFIXES = {
    "Login": ["ui.common.play", "ui.common.options", "ui.connection."],
    "ServerSelection": ["ui.server.", "ui.common.select", "ui.common.play"],
    "CharacterSelection": ["ui.charsel.", "ui.common.play", "ui.common.confirm"],
    "CharacterCreation": ["ui.charcrea.", "ui.common.cancel", "ui.common.play"],
    "PreGameMainMenu": ["ui.common.mainMenu", "ui.common.options", "ui.common.quit",
                         "ui.common.cancel", "ui.common.yes", "ui.common.no",
                         "ui.common.confirm", "ui.header.", "ui.information.", "ui.link."],
    "CharacterHeader": ["ui.header."],
}

SCREEN_UXML = {
    "ConnectionBackground": "Assets/UI/Auth/connectionBackground.uxml",
    "Login": "Assets/UI/Auth/login.uxml",
    "ServerSelection": "Assets/UI/Auth/ServerSelection/ServerSelection.uxml",
    "CharacterSelection": "Assets/UI/Auth/CharacterSelection/characterSelection.uxml",
    "CharacterCreation": "Assets/UI/Auth/CharacterCreation/characterCreation.uxml",
    "PreGameMainMenu": "Assets/UI/Auth/preGameMainMenu.uxml",
    "CharacterHeader": "Assets/UI/Auth/CharacterHeader/characterHeader.uxml",
}

# From decompiled GetTabWithFilter switch (dump.cs L262920, dsp enum):
# dsp enum values map to tab binding fields via switch cases in decompiled_all.c
SERVER_TABS = [
    {"element": "tab_pioneer", "dsp": 3, "gameType": 5, "i18n": "ui.server.pioneerServers"},
    {"element": "tab_pioneer_mono", "dsp": 2, "gameType": 5, "i18n": "ui.server.pioneerMonoServers", "mono": True},
    {"element": "tab_classic", "dsp": 1, "gameType": 1, "i18n": "ui.server.classicalServers"},
    {"element": "tab_classic_mono", "dsp": 0, "gameType": 1, "i18n": "ui.server.classicalMonoServers", "mono": True},
    {"element": "tab_epic", "dsp": 5, "gameType": 2, "i18n": "ui.server.epicServers"},
    {"element": "tab_tournament", "dsp": 4, "gameType": 3, "i18n": "ui.server.temporaryServer"},
    {"element": "tab_temporis", "dsp": 6, "gameType": 4, "i18n": "ui.server.temporaryServer"},
]


def load_json(name):
    path = os.path.join(OUTPUT_DIR, name)
    if not os.path.exists(path):
        print(f"  WARNING: {name} not found")
        return None
    with open(path, 'r', encoding='utf-8') as f:
        return json.load(f)


def extract_binding_elements(bindings, binding_key):
    if not bindings or binding_key not in bindings:
        return []
    binding = bindings[binding_key]
    elements = binding.get("elements", [])
    result = []
    for el in elements:
        name = el.get("name", "")
        if name == "_____root_____":
            continue
        clean_name = name.lstrip("_")
        result.append({
            "name": clean_name,
            "type": el.get("type", "VisualElement"),
        })
    return result


def find_i18n_keys(ops_strings, prefixes):
    keys = {}
    for ops_key, value in ops_strings.items():
        if not isinstance(value, str) or not value.startswith("ui."):
            continue
        for prefix in prefixes:
            if value.startswith(prefix) or value == prefix:
                keys[value] = ops_key
                break
    return keys


def match_i18n_to_elements(elements, i18n_keys):
    i18n_map = {}
    for el in elements:
        name = el["name"]
        el_type = el["type"]
        if el_type not in ("DofusLabel", "DofusButtonCustom", "PlayButton",
                            "DofusTab", "TextInput", "TextInputToValidate"):
            continue
        for key in i18n_keys:
            key_suffix = key.rsplit(".", 1)[-1] if "." in key else key
            name_lower = name.lower().replace("_", "")
            key_lower = key_suffix.lower().replace("_", "")
            if key_lower in name_lower or name_lower in key_lower:
                i18n_map[name] = key
                break
    return i18n_map


def extract_transcribed_i18n(transcribed, handler_name):
    path = os.path.join(OUTPUT_DIR, "transcribed", f"{handler_name}.json")
    if not os.path.exists(path):
        return {}
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    i18n_refs = {}
    for method in data.get("methods", []):
        var_map = method.get("analysis", {}).get("varMap", {})
        for var, value in var_map.items():
            if isinstance(value, str) and value.startswith("ui."):
                i18n_refs[value] = method.get("method", "?")
    return i18n_refs


def extract_display_states(transcribed, handler_name):
    path = os.path.join(OUTPUT_DIR, "transcribed", f"{handler_name}.json")
    if not os.path.exists(path):
        return {}
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    states = {}
    for method in data.get("methods", []):
        ops = method.get("analysis", {}).get("ops", [])
        for op in ops:
            if op.get("type") == "SET_STYLE":
                method_name = method.get("method", "?")
                if method_name not in states:
                    states[method_name] = []
                states[method_name].append(op)
    return states


def build_flow():
    return {
        "Login.onPlay": {"action": "haapi_auth", "nextState": "connecting"},
        "Login.onAuthAccepted": {"action": "show", "screen": "ServerSelection"},
        "Login.onError": {"action": "state", "state": "idle"},
        "ServerSelection.onValidate": {"action": "server_select", "nextState": "reconnect"},
        "ServerSelection.onKvi": {"action": "show", "screen": "CharacterSelection"},
        "CharacterSelection.onValidate": {"action": "character_select"},
        "CharacterSelection.onKva": {"action": "show", "screen": "World"},
        "CharacterCreation.onCreate": {"action": "create_character"},
        "CharacterCreation.onBack": {"action": "show", "screen": "ServerSelection"},
    }


def build_login_states():
    return {
        "idle": {
            "ctr_login": "Flex",
            "ctr_error": "None",
            "ctr_message": "None",
            "iconLoading": "None",
            "btn_play.enabled": True,
        },
        "connecting": {
            "ctr_login": "None",
            "iconLoading": "Flex",
            "btn_play.enabled": False,
        },
        "error": {
            "ctr_error": "Flex",
            "ctr_message": "Flex",
        },
    }


def merge_screen_logic(screen_logic_data, handler_name):
    """Extract I18n, calls, events, enables from ScreenLogic.json for a handler."""
    logic = screen_logic_data.get(handler_name, {})
    methods = logic.get("methods", {})

    # Merge ALL I18N_SET operations across all methods
    i18n_map = {}
    for mname, mdata in methods.items():
        for op in mdata.get("operations", []):
            if op["op"] == "I18N_SET" and op.get("i18n", "").startswith("ui."):
                field = op.get("field", "?")
                if field != "?":
                    i18n_map[field] = {
                        "key": op["i18n"],
                        "action": op.get("action", "set_text"),
                        "toUpper": op.get("toUpper", False),
                        "source": mname,
                    }

    # OnEnable call chain
    on_enable = methods.get("OnEnable", {})
    call_chain = [op["method"] for op in on_enable.get("operations", []) if op["op"] == "CALL"]
    enable_binds = [op["field"] for op in on_enable.get("operations", []) if op["op"] == "BIND_GET"]

    # ALL events across all methods
    events = []
    for mname, mdata in methods.items():
        for op in mdata.get("operations", []):
            if op["op"] == "EVENT" and op.get("field", "?") != "?":
                events.append({
                    "field": op["field"],
                    "type": op.get("type", "click"),
                    "source": mname,
                })

    # ALL enable/disable operations
    enables = []
    for mname, mdata in methods.items():
        for op in mdata.get("operations", []):
            if op["op"] == "ENABLE" and op.get("field", "?") != "?":
                enables.append({
                    "field": op["field"],
                    "value": op.get("value", "true"),
                    "source": mname,
                })

    # UIUTILS calls (display helpers)
    uiutils = []
    for mname, mdata in methods.items():
        for op in mdata.get("operations", []):
            if op["op"] == "UIUTILS":
                uiutils.append({
                    "function": op.get("function", "?"),
                    "field": op.get("field", "?"),
                    "source": mname,
                })

    # VISIBLE toggles
    visibles = []
    for mname, mdata in methods.items():
        for op in mdata.get("operations", []):
            if op["op"] == "VISIBLE":
                visibles.append({
                    "field": op.get("field", "?"),
                    "value": op.get("value", "true"),
                    "source": mname,
                })

    return {
        "i18n": i18n_map,
        "callChain": call_chain,
        "enableBinds": enable_binds,
        "events": events,
        "enables": enables,
        "uiutils": uiutils,
        "visibles": visibles,
        "methodCount": len(methods),
        "totalOps": sum(m.get("opsCount", 0) for m in methods.values()),
    }


def synthesize():
    print("Loading extracted data...")
    bindings = load_json("bindings.json")
    screen_handlers = load_json("screen_handlers.json")
    ops_strings = load_json("obfuscated_strings.json")
    enums = load_json("enums.json")
    asset_paths = load_json("asset_paths.json")
    css_selectors = load_json("css_selectors_all.json")

    if not bindings:
        print("ERROR: bindings.json required")
        return None

    # Load ScreenLogic.json (auto-extracted from decompiled_all.c)
    screen_logic = load_json("ScreenLogic.json") or {}
    print(f"  ScreenLogic: {len(screen_logic)} screen types")

    result = {"screens": {}, "templates": {}, "flow": build_flow(), "i18nKeys": {}}
    all_i18n = {}

    for screen_name, config in AUTH_SCREENS.items():
        print(f"\n  Synthesizing: {screen_name}")
        handler = config["handler"]
        binding_key = config["bindingKey"]

        elements = extract_binding_elements(bindings, binding_key)
        print(f"    Elements: {len(elements)}")

        # Merge I18n from ScreenLogic (automatically extracted from decompiled_all.c)
        logic = merge_screen_logic(screen_logic, handler)
        logic_i18n = {}
        for field, info in logic["i18n"].items():
            logic_i18n[field] = info["key"]
        print(f"    I18n from decompiled: {len(logic_i18n)} (auto)")

        # Also collect all available I18n keys from OPS strings
        prefixes = I18N_PREFIXES.get(screen_name, [])
        available_keys = find_i18n_keys(ops_strings or {}, prefixes) if prefixes else {}

        screen = {
            "handler": handler,
            "authUi": config["authUi"],
            "uxml": SCREEN_UXML.get(screen_name, ""),
            "binding": binding_key.rsplit(".", 1)[-1],
            "elements": elements,
            "i18n": logic_i18n,  # ALL from decompiled, zero hardcoded
            "i18nDetails": {f: info for f, info in logic["i18n"].items()},
            "onEnable": {
                "callChain": logic["callChain"],
                "binds": logic["enableBinds"],
            },
            "events": logic["events"],
            "enables": logic["enables"],
            "uiutils": logic["uiutils"],
            "visibles": logic["visibles"],
            "i18nAvailable": sorted(available_keys.keys()),
            "decompiled": {
                "methods": logic["methodCount"],
                "ops": logic["totalOps"],
            },
        }

        if screen_name == "Login":
            screen["states"] = build_login_states()

        if screen_name == "ServerSelection":
            screen["tabs"] = SERVER_TABS
            screen["cardTemplate"] = "ServerCard"
            # From decompiled OnEnable: pdb/pdd DOTween animations → fallback display:Flex
            # ctr_carouselMode shown (default mode), ctr_listMode hidden
            # ctr_serverSelection, ctr_header, ctr_tabSection shown by pdb animation
            screen["initDisplay"] = {
                "ctr_carouselMode": "Flex",
                "ctr_listMode": "None",
                "ctr_serverSelection": "Flex",
                "ctr_header": "Flex",
                "ctr_tabSection": "Flex",
                "ctr_force": "None",
            }

        result["screens"][screen_name] = screen
        all_i18n.update(available_keys)
        all_i18n.update({v: "decompiled" for v in logic_i18n.values()})

    for tmpl_name, binding_key in TEMPLATE_BINDINGS.items():
        elements = extract_binding_elements(bindings, binding_key)
        if elements:
            result["templates"][tmpl_name] = {
                "binding": binding_key.rsplit(".", 1)[-1],
                "elements": elements,
            }
            print(f"\n  Template: {tmpl_name} ({len(elements)} elements)")

    result["i18nKeys"] = {k: v for k, v in sorted(all_i18n.items())}

    print(f"\n--- Summary ---")
    print(f"Screens: {len(result['screens'])}")
    print(f"Templates: {len(result['templates'])}")
    print(f"Flow rules: {len(result['flow'])}")
    print(f"I18n keys: {len(result['i18nKeys'])}")
    total_elements = sum(len(s["elements"]) for s in result["screens"].values())
    total_elements += sum(len(t["elements"]) for t in result["templates"].values())
    print(f"Total elements: {total_elements}")

    return result


def main():
    result = synthesize()
    if not result:
        sys.exit(1)

    out_path = os.path.join(OUTPUT_DIR, "ScreenBehavior.json")
    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump(result, f, indent=2, ensure_ascii=False)
    print(f"\nSaved: {out_path} ({os.path.getsize(out_path) // 1024}KB)")

    if "--deploy" in sys.argv:
        deploy_path = os.path.join(DEPLOY_DIR, "ScreenBehavior.json")
        if os.path.isdir(DEPLOY_DIR):
            with open(deploy_path, 'w', encoding='utf-8') as f:
                json.dump(result, f, ensure_ascii=False)
            print(f"Deployed: {deploy_path}")
        else:
            print(f"WARNING: Deploy dir not found: {DEPLOY_DIR}")


if __name__ == "__main__":
    main()
