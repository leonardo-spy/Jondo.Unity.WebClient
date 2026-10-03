#!/usr/bin/env python3
"""
Dofus 3 Screen Registry Parser

Parses dump.cs to extract:
1. All Binding structs (UI element names + types)
2. All screen handlers (fzy<T> subclasses)
3. UXML path constants from AssetsPaths
4. Auth screen system (AuthUis enum, screen managers)

Outputs JSON for the WebGL probe to use at runtime.

Usage:
  python parse_screens.py <dump.cs> [--output <json>]
  python parse_screens.py <dump.cs> --login     # only login-related
"""

import re
import json
import sys
import argparse
import io


def parse_bindings(dump_text):
    """Extract all Binding structs and their fields."""
    bindings = {}
    pattern = re.compile(
        r'^public struct (\w+Binding)\b.*?// TypeDefIndex: (\d+)\s*\n'
        r'\{(.*?)^\}',
        re.MULTILINE | re.DOTALL
    )

    for m in pattern.finditer(dump_text):
        name = m.group(1)
        type_idx = int(m.group(2))
        body = m.group(3)

        fields = []
        for fm in re.finditer(
            r'private\s+(\w+)\s+(_\w+)\s*;.*?//\s*(0x[0-9A-Fa-f]+)',
            body
        ):
            field_type = fm.group(1)
            field_name = fm.group(2).lstrip('_')
            offset = fm.group(3)
            fields.append({
                "name": field_name,
                "type": field_type,
                "offset": offset,
            })

        if fields:
            bindings[name] = {
                "typeIndex": type_idx,
                "fields": fields,
            }

    return bindings


def parse_screen_handlers(dump_text):
    """Extract fzy<T> subclasses (screen handlers)."""
    handlers = {}
    pattern = re.compile(
        r'^public (?:sealed )?class (\w+)\s*:\s*fzy<(\w+)>.*?// TypeDefIndex: (\d+)',
        re.MULTILINE
    )

    for m in pattern.finditer(dump_text):
        handler = m.group(1)
        binding = m.group(2)
        type_idx = int(m.group(3))
        handlers[handler] = {
            "binding": binding,
            "typeIndex": type_idx,
        }

    return handlers


def parse_asset_paths(dump_text):
    """Extract UXML/asset path constants from AssetsPaths class."""
    paths = {}
    # Find the AssetsPaths class
    ap_match = re.search(
        r'public static class AssetsPaths.*?\n\{(.*?)^\}',
        dump_text, re.MULTILINE | re.DOTALL
    )
    if not ap_match:
        return paths

    body = ap_match.group(1)
    for fm in re.finditer(
        r'public const string (\w+)\s*=\s*"([^"]+)"',
        body
    ):
        paths[fm.group(1)] = fm.group(2)

    return paths


def parse_auth_uis(dump_text):
    """Extract AuthUis enum values."""
    auth = {}
    au_match = re.search(
        r'public enum AuthUis.*?\n\{(.*?)^\}',
        dump_text, re.MULTILINE | re.DOTALL
    )
    if not au_match:
        # Try obfuscated name from V5 guide: fbz
        au_match = re.search(
            r'public enum (?:AuthUis|fbz).*?\n\{(.*?)^\}',
            dump_text, re.MULTILINE | re.DOTALL
        )
    if au_match:
        for fm in re.finditer(r'(\w+)\s*=\s*(\d+)', au_match.group(1)):
            auth[fm.group(1)] = int(fm.group(2))

    return auth


def parse_screen_manager(dump_text):
    """Extract auth screen manager (fpm) mapping AuthUis -> screen handler."""
    # fpm is gax<AuthUis, fzy<...>> - the screen manager
    managers = {}
    # Look for fpm class
    fpm_match = re.search(
        r'^public (?:sealed )?class fpm\b.*?\n\{(.*?)^\}',
        dump_text, re.MULTILINE | re.DOTALL
    )
    if fpm_match:
        managers["fpm"] = {
            "description": "Auth screen manager",
            "fields": [],
        }
        for fm in re.finditer(
            r'private\s+(\w+)\s+(\w+)\s*;',
            fpm_match.group(1)
        ):
            managers["fpm"]["fields"].append({
                "type": fm.group(1),
                "name": fm.group(2),
            })

    return managers


def build_handler_map():
    """Known mapping from V2/V5 guides."""
    return {
        "fyr": {
            "screen": "ConnectionBackground",
            "authUi": 8,
            "uxml": "Assets/UI/Auth/connectionBackground.uxml",
        },
        "fys": {
            "screen": "Login",
            "authUi": 0,
            "uxml": "Assets/UI/Auth/login.uxml",
        },
        "fyt": {
            "screen": "LoginMobile",
            "authUi": None,
            "uxml": "Assets/UI/Auth/login_mobile.uxml",
        },
        "fyv": {
            "screen": "PreGameMainMenu",
            "authUi": 4,
            "uxml": "Assets/UI/Auth/preGameMainMenu.uxml",
        },
        "CharacterSelection": {
            "screen": "CharacterSelection",
            "authUi": 2,
            "uxml": "Assets/UI/Auth/characterSelection/characterSelection.uxml",
        },
        "ServerSelection": {
            "screen": "ServerSelection",
            "authUi": 1,
            "uxml": "Assets/UI/Auth/characterSelection/characterSelection.uxml",
        },
    }


def main():
    parser = argparse.ArgumentParser(description="Dofus 3 Screen Registry Parser")
    parser.add_argument("dump", help="Path to dump.cs")
    parser.add_argument("--output", "-o", help="Output JSON path")
    parser.add_argument("--login", "-l", action="store_true",
                        help="Only login/auth related screens")
    parser.add_argument("--compact", "-c", action="store_true")
    args = parser.parse_args()

    sys.stderr.write(f"Reading {args.dump}...\n")
    with open(args.dump, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
    sys.stderr.write(f"  {len(text)} chars, parsing...\n")

    bindings = parse_bindings(text)
    sys.stderr.write(f"  Bindings: {len(bindings)}\n")

    handlers = parse_screen_handlers(text)
    sys.stderr.write(f"  Screen handlers: {len(handlers)}\n")

    paths = parse_asset_paths(text)
    sys.stderr.write(f"  Asset paths: {len(paths)}\n")

    auth_uis = parse_auth_uis(text)
    sys.stderr.write(f"  AuthUis: {len(auth_uis)}\n")

    handler_map = build_handler_map()

    if args.login:
        login_bindings = [
            "LoginBinding", "ConnectionBackgroundBinding",
            "PreGameMainMenuBinding", "ServerSelectionBinding",
            "CharacterSelectionBinding", "Login_mobileBinding",
        ]
        bindings = {k: v for k, v in bindings.items() if k in login_bindings}
        login_handlers = [
            "fyr", "fys", "fyt", "fyv",
            "CharacterSelection", "ServerSelection",
        ]
        handlers = {k: v for k, v in handlers.items() if k in login_handlers}

    result = {
        "bindings": bindings,
        "handlers": handlers,
        "handlerMap": handler_map,
        "assetPaths": paths,
        "authUis": auth_uis,
    }

    indent = None if args.compact else 2
    output = json.dumps(result, ensure_ascii=False, indent=indent)

    if args.output:
        with open(args.output, "w", encoding="utf-8") as f:
            f.write(output)
        sys.stderr.write(f"  Written to {args.output}\n")
    else:
        sys.stdout.buffer.write(output.encode("utf-8"))
        sys.stdout.buffer.write(b"\n")


if __name__ == "__main__":
    main()
