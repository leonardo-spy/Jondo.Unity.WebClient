#!/usr/bin/env python3
"""
Dofus 3 Localization Binary Parser

Format (discovered via reverse engineering):
  [3 bytes]  Magic/version header (always 02 66 72)
  [4 bytes]  LE uint32: dict1_count (int-keyed entries)
  [8*N bytes] Dict1: (int32 key, uint32 string_offset) pairs
  [4 bytes]  LE uint32: dict2_count (hash64-keyed entries)
  [12*M bytes] Dict2: (uint64 hash_key, uint32 string_offset) triples
  [rest]     String data: varint-length-prefixed UTF-8 strings

Usage:
  python parse_i18n.py <path_to_bin> [--output <json_path>] [--filter <term>]
  python parse_i18n.py --all <i18n_dir> [--output-dir <dir>]
  python parse_i18n.py --login <path_to_bin>
"""

import struct
import json
import sys
import os
import argparse


def read_varint(data, pos):
    val = 0
    shift = 0
    while pos < len(data):
        b = data[pos]
        val |= (b & 0x7F) << shift
        shift += 7
        pos += 1
        if not (b & 0x80):
            break
    return val, pos


def read_string(data, offset):
    if offset >= len(data):
        return None
    length, pos = read_varint(data, offset)
    if length < 0 or pos + length > len(data):
        return None
    return data[pos:pos + length].decode("utf-8", errors="replace")


def parse_bin(path):
    with open(path, "rb") as f:
        data = f.read()

    if len(data) < 11:
        raise ValueError(f"File too small: {len(data)} bytes")

    dict1_count = struct.unpack_from("<I", data, 3)[0]
    dict1_start = 7
    dict1_end = dict1_start + dict1_count * 8

    if dict1_end + 4 > len(data):
        raise ValueError(f"Dict1 extends past EOF: count={dict1_count}")

    dict2_count = struct.unpack_from("<I", data, dict1_end)[0]
    dict2_start = dict1_end + 4
    dict2_end = dict2_start + dict2_count * 12

    dict1 = {}
    for i in range(dict1_count):
        key, offset = struct.unpack_from("<iI", data, dict1_start + i * 8)
        s = read_string(data, offset)
        if s is not None:
            dict1[key] = s

    dict2 = {}
    for i in range(dict2_count):
        h64 = struct.unpack_from("<Q", data, dict2_start + i * 12)[0]
        offset = struct.unpack_from("<I", data, dict2_start + i * 12 + 8)[0]
        s = read_string(data, offset)
        if s is not None:
            dict2[f"0x{h64:016X}"] = s

    return dict1, dict2


def fnv1a_64_utf16(s):
    """FNV-1a 64-bit hash over UTF-16 characters — matches Core.Localization.Utils.HashHelper.Fnv1A."""
    h = 0xcbf29ce484222325
    for ch in s:
        h = ((h ^ ord(ch)) * 0x100000001b3) & 0xFFFFFFFFFFFFFFFF
    return h


LOGIN_KEYS = {
    60264: "Play",
    1094405: "Play",
    1094627: "Options",
    1094651: "Log in",
    1095661: "Login",
    1094084: "Loading",
    1094100: "Close",
    1093664: "Server",
    1092959: "Select",
    1093466: "Create",
    1092166: "Confirm",
    1094018: "Cancel",
    1092679: "Character",
    1093710: "Name",
    1093893: "Log out",
    1093778: "Ready",
    1093990: "News",
    1094221: "Accept",
    1093399: "Refuse",
}


def main():
    parser = argparse.ArgumentParser(description="Dofus 3 I18n binary parser")
    parser.add_argument("path", nargs="?", help="Path to .bin file")
    parser.add_argument("--output", "-o", help="Output JSON path")
    parser.add_argument("--filter", "-f", help="Filter strings containing term")
    parser.add_argument("--all", "-a", help="Parse all .bin files in directory")
    parser.add_argument("--output-dir", "-d", help="Output directory for --all")
    parser.add_argument("--login", "-l", action="store_true",
                        help="Output only login/UI related strings")
    parser.add_argument("--compact", "-c", action="store_true",
                        help="Compact JSON (no indentation)")
    parser.add_argument("--stats", "-s", action="store_true",
                        help="Print statistics only")
    parser.add_argument("--keys", "-k", help="Resolve string keys (comma-separated or @file)")
    parser.add_argument("--behavior", "-b", help="Read ScreenBehavior.json and resolve all I18n keys")
    args = parser.parse_args()

    if args.all:
        parse_all(args.all, args.output_dir, args.compact)
        return

    if not args.path:
        parser.print_help()
        return

    print(f"Parsing {args.path}...", file=sys.stderr)
    dict1, dict2 = parse_bin(args.path)
    print(f"  Dict1: {len(dict1)} entries, Dict2: {len(dict2)} entries",
          file=sys.stderr)

    if args.stats:
        lens = [len(v) for v in dict1.values()]
        print(f"  String lengths: min={min(lens)}, max={max(lens)}, "
              f"avg={sum(lens)/len(lens):.0f}")
        short = sum(1 for v in dict1.values() if len(v) < 30)
        print(f"  Short strings (<30 chars): {short}")
        return

    if args.keys or args.behavior:
        string_keys = []
        if args.behavior:
            with open(args.behavior, 'r', encoding='utf-8') as bf:
                behavior = json.load(bf)
            for screen in behavior.get("screens", {}).values():
                for key in screen.get("i18n", {}).values():
                    if key not in string_keys:
                        string_keys.append(key)
                for key in screen.get("i18nAvailable", []):
                    if key not in string_keys:
                        string_keys.append(key)
            print(f"  ScreenBehavior: {len(string_keys)} unique I18n keys", file=sys.stderr)
        elif args.keys.startswith("@"):
            with open(args.keys[1:], 'r') as kf:
                string_keys = [line.strip() for line in kf if line.strip()]
        else:
            string_keys = [k.strip() for k in args.keys.split(",")]

        result = {}
        for key, label in LOGIN_KEYS.items():
            if key in dict1:
                result[str(key)] = dict1[key]
        resolved = 0
        for sk in string_keys:
            h = fnv1a_64_utf16(sk)
            hex_key = f"0x{h:016X}"
            if hex_key in dict2:
                result[sk] = dict2[hex_key]
                resolved += 1
            else:
                result[sk] = sk.rsplit(".", 1)[-1] if "." in sk else sk
        print(f"  Resolved: {resolved}/{len(string_keys)} string keys", file=sys.stderr)
        indent = None if args.compact else 2
        output = json.dumps(result, ensure_ascii=False, indent=indent)
    elif args.login:
        result = {}
        for key, label in LOGIN_KEYS.items():
            if key in dict1:
                result[str(key)] = dict1[key]
        indent = None if args.compact else 2
        output = json.dumps(result, ensure_ascii=False, indent=indent)
    elif args.filter:
        term = args.filter.lower()
        filtered = {
            str(k): v for k, v in dict1.items()
            if term in v.lower() and len(v) < 200
        }
        indent = None if args.compact else 2
        output = json.dumps(filtered, ensure_ascii=False, indent=indent)
        print(f"  Filtered: {len(filtered)} matches", file=sys.stderr)
    else:
        result = {
            "dict1": {str(k): v for k, v in dict1.items()},
            "dict2": dict2,
        }
        indent = None if args.compact else 2
        output = json.dumps(result, ensure_ascii=False, indent=indent)

    if args.output:
        with open(args.output, "w", encoding="utf-8") as f:
            f.write(output)
        print(f"  Written to {args.output}", file=sys.stderr)
    else:
        sys.stdout.buffer.write(output.encode("utf-8"))
        sys.stdout.buffer.write(b"\n")


def parse_all(i18n_dir, output_dir, compact):
    if output_dir and not os.path.exists(output_dir):
        os.makedirs(output_dir)

    for fname in sorted(os.listdir(i18n_dir)):
        if not fname.endswith(".bin"):
            continue
        lang = fname[:-4]
        path = os.path.join(i18n_dir, fname)
        print(f"Parsing {lang}...", file=sys.stderr)
        dict1, dict2 = parse_bin(path)
        print(f"  {lang}: {len(dict1)} + {len(dict2)} entries", file=sys.stderr)

        result = {
            "dict1": {str(k): v for k, v in dict1.items()},
            "dict2": dict2,
        }
        indent = None if compact else 2
        out_path = os.path.join(output_dir or ".", f"{lang}.json")
        with open(out_path, "w", encoding="utf-8") as f:
            json.dump(result, f, ensure_ascii=False, indent=indent)
        size_mb = os.path.getsize(out_path) / 1024 / 1024
        print(f"  Written: {out_path} ({size_mb:.1f}MB)", file=sys.stderr)


if __name__ == "__main__":
    main()
