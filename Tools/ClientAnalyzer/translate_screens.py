#!/usr/bin/env python3
"""
Translate decompiled screen handler methods into a complete ScreenLogic.json.

Follows the FULL call chain from boot (Compositor.Compose) through each screen handler,
extracting ALL UI operations from ALL methods — not just Localize.

Operations extracted per method:
  - BIND_GET: binding field access (get_lbl_title, get_btn_play, etc.)
  - I18N_SET: Localized(key) → set_text on a binding field
  - DISPLAY: style.display toggle (Flex/None)
  - EVENT: RegisterCallback / clicked handler
  - ENABLE: SetEnabled(true/false)
  - CLASS_ADD: AddToClassList
  - CLASS_REMOVE: RemoveFromClassList
  - UPPER: String.ToUpper before set_text
  - CALL: calls to other handler methods (call chain)

Sources:
  - decompiled_all.c: Ghidra pseudo-C bodies (read via byte offset)
  - dump.cs: method signatures with VAs
  - obfuscated_strings.json: OPS string resolution
  - bindings.json: binding field names → element names
  - screen_handlers.json: handler → binding mapping

Output:
  output/ScreenLogic.json — complete UI operation spec per screen per method
"""

import json
import os
import re
import sys
import io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from transcribe_methods import (
    find_c_function, find_type_methods, read_function_body,
    load_obfuscated_strings, DUMP_CS
)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(SCRIPT_DIR, "output")

SCREEN_TYPES = [
    "fyr",                # ConnectionBackground
    "fys",                # Login
    "ServerSelection",    # Server selection
    "CharacterSelection", # Character selection
    "CharacterCreation",  # Character creation
    "fyv",                # PreGameMainMenu
    "CharacterHeader",    # Character header bar
    "fpm",                # Auth screen manager
]

KEY_METHODS = [
    "OnEnable", "OnDisable", "Localize", "LocalizeTexts",
    "ctor", "Init", "InitServer", "InitServersList",
    "InitializeUI", "InitializeBinding", "InitializeHooks",
    "DisplayServer", "DisplayFilter", "DisplayCharacter", "DisplayCharacters",
    "FillCharacters", "CreateTabFilter", "FilterTab",
    "UpdateServerCards", "CreateDataProvider", "UpdateDisplayProvider",
    "BindItem", "BindServerLine", "BindSearchResult",
    "MakeServerLine", "MakeSearchResult", "MakeItem",
    "OnValidateButton", "ValidateServerChoice", "ValidateCharacterChoice",
    "OnSelectionChanged", "OnFilterSelected", "OnModeChange",
    "SetSelection", "OnPressEnter", "OnCreateCharacterClicked",
    "OnServerChoice", "OnServerSelection",
    "SpawnEffect", "HideEffect", "EnableEffect",
    "Disable", "DisableAsync", "Unlock",
    "Scroll", "RefreshCarouselCards",
    "OnCharactersListUpdated", "OnCharacterCanBeCreated", "UpdateCreateButton",
    "mn", "mx", "bn", "gdj", "bjdo", "bjdg", "bjdh", "gnz",  # fys methods
    "ShowHeader", "PlayerDisplay", "Toggle",  # CharacterHeader
]


def extract_method_ops(type_name, method_name, full_namespace, va, ops_strings):
    """Extract ALL UI operations from a single method body.

    Tracks context across lines: binding field access on line N may be consumed
    by a set_text/display toggle/event wire on line N+5..N+30.
    """
    entry = find_c_function(type_name, method_name, full_namespace, va)
    if not entry:
        return None

    body = read_function_body(entry)
    if not body:
        return None

    lines = body.split('\n')
    operations = []

    # Context tracking across lines
    last_binding_field = None     # most recent Binding__get_xxx
    last_binding_line = -100      # line where it was set
    last_ops_key = None           # most recent __a__xxx
    last_ops_resolved = None
    pending_upper = False

    # Track ALL OPS string assignments (variable → resolved string)
    var_ops = {}  # pSVarX → resolved string

    # C function namespace prefix for self-calls
    c_prefix = (full_namespace or type_name).replace('.', '_')

    for i, line in enumerate(lines):
        s = line.strip()
        if not s or s.startswith('//'):
            continue

        # --- OPS string variable tracking ---
        ops_match = re.search(r'__a__(\w+)', s)
        if ops_match:
            key = ops_match.group(1)
            resolved = ops_strings.get(key, f'OPS:{key}')
            last_ops_key = key
            last_ops_resolved = resolved
            # Track variable assignment: pSVarX = __a__key
            var_assign = re.match(r'(\w+)\s*=', s)
            if var_assign:
                var_ops[var_assign.group(1)] = resolved

        # --- BIND_GET: binding field access ---
        bind_match = re.search(r'Binding__get_(\w+)', s)
        if bind_match:
            last_binding_field = bind_match.group(1)
            last_binding_line = i
            pending_upper = False
            operations.append({
                'op': 'BIND_GET',
                'field': last_binding_field,
                'line': i,
            })
            continue

        # Context window: binding field is relevant for ~30 lines after access
        if i - last_binding_line > 40:
            last_binding_field = None

        # --- ToUpper ---
        if 'ToUpper' in s:
            pending_upper = True

        # --- I18N: Localized call ---
        if 'Localized' in s and '__Localized' in s:
            operations.append({
                'op': 'I18N_RESOLVE',
                'field': last_binding_field or '?',
                'i18n': last_ops_resolved or '?',
                'line': i,
            })

        # --- SET_TEXT: set_text / set_Text via vtable or direct ---
        if 'set_text' in s or 'set_Text' in s:
            operations.append({
                'op': 'I18N_SET' if last_ops_resolved else 'SET_TEXT',
                'field': last_binding_field or '?',
                'i18n': last_ops_resolved or '',
                'action': 'set_text',
                'toUpper': pending_upper,
                'line': i,
            })
            last_ops_resolved = None
            pending_upper = False

        # --- SET_PLACEHOLDER ---
        if 'set_placeholderText' in s:
            operations.append({
                'op': 'I18N_SET',
                'field': last_binding_field or '?',
                'i18n': last_ops_resolved or '',
                'action': 'set_placeholder',
                'line': i,
            })
            last_ops_resolved = None

        # --- SET_TITLE ---
        if 'set_title' in s:
            operations.append({
                'op': 'I18N_SET',
                'field': last_binding_field or '?',
                'i18n': last_ops_resolved or '',
                'action': 'set_title',
                'line': i,
            })
            last_ops_resolved = None

        # --- DISPLAY: direct set_display or UIUtils display helpers ---
        if 'set_display' in s:
            val = 'Flex' if 'Flex' in s or ',1,' in s else 'None' if 'None' in s or ',0,' in s else '?'
            operations.append({
                'op': 'DISPLAY',
                'field': last_binding_field or '?',
                'value': val,
                'line': i,
            })

        # UIUtils helpers — decompiled meanings:
        # pca(el, bool): display toggle — true=Flex, false=None (b^1 maps to DisplayStyle)
        # pcc(el): set pickingMode = Ignore
        # pcd(el): set pickingMode = Position
        # ozz(el, bool): toggle picking mode
        # pbg(el): cleanup event registrations
        # pad(el, bool): store custom data on element
        # paw(el, callback): register GeometryChanged callback
        # pai(el): clear inline styles
        # pap(el): reset picking mode
        uiutils_match = re.search(r'UIUtils__(\w+)', s)
        if uiutils_match:
            util_name = uiutils_match.group(1)
            if util_name == 'pca':
                # pca(element, bool) → DISPLAY: true=Flex, false=None
                # Args often on NEXT lines due to Ghidra wrapping
                context = s
                for lookahead in range(1, 5):
                    if i + lookahead < len(lines):
                        context += ' ' + lines[i + lookahead].strip()
                val = '?'
                if 'true' in context.lower() and 'false' not in context.lower():
                    val = 'Flex'
                elif 'false' in context.lower():
                    val = 'None'
                operations.append({
                    'op': 'DISPLAY',
                    'field': last_binding_field or '?',
                    'value': val,
                    'via': 'pca',
                    'line': i,
                })
            elif util_name in ('pcc', 'pcd', 'ozz'):
                operations.append({
                    'op': 'PICKING',
                    'function': util_name,
                    'field': last_binding_field or '?',
                    'line': i,
                })
            elif util_name == 'pbg':
                operations.append({
                    'op': 'CLEANUP',
                    'field': last_binding_field or '?',
                    'line': i,
                })
            elif util_name in ('paw', 'pas', 'pat', 'pau', 'pav', 'pay', 'paz', 'pba'):
                operations.append({
                    'op': 'EVENT',
                    'field': last_binding_field or '?',
                    'type': 'callback',
                    'via': util_name,
                    'line': i,
                })
            elif util_name in ('pdb', 'pdc', 'pdd'):
                # DOTween animation functions → fallback to display:Flex in WebGL
                operations.append({
                    'op': 'DISPLAY',
                    'field': last_binding_field or '?',
                    'value': 'Flex',
                    'via': f'DOTween_{util_name}',
                    'line': i,
                })
            else:
                operations.append({
                    'op': 'UIUTILS',
                    'function': util_name,
                    'field': last_binding_field or '?',
                    'line': i,
                })

        # --- SET_VISIBLE ---
        if 'set_visible' in s:
            val = 'true' if 'true' in s.lower() or ',1' in s else 'false'
            operations.append({
                'op': 'VISIBLE',
                'field': last_binding_field or '?',
                'value': val,
                'line': i,
            })

        # --- ENABLE ---
        if 'SetEnabled' in s:
            val = 'true' if 'true' in s.lower() or ',1' in s else 'false'
            operations.append({
                'op': 'ENABLE',
                'field': last_binding_field or '?',
                'value': val,
                'line': i,
            })

        # --- EVENT: callbacks ---
        if 'add_clicked' in s:
            operations.append({
                'op': 'EVENT',
                'field': last_binding_field or '?',
                'type': 'clicked',
                'line': i,
            })
        elif 'RegisterCallback' in s:
            cb_type = '?'
            if 'ClickEvent' in s: cb_type = 'click'
            elif 'ChangeEvent' in s: cb_type = 'change'
            elif 'NavigationSubmitEvent' in s: cb_type = 'submit'
            elif 'KeyDown' in s: cb_type = 'keydown'
            operations.append({
                'op': 'EVENT',
                'field': last_binding_field or '?',
                'type': cb_type,
                'line': i,
            })
        elif 'UnregisterCallback' in s:
            operations.append({
                'op': 'EVENT_REMOVE',
                'field': last_binding_field or '?',
                'line': i,
            })

        # --- CSS CLASS ---
        if 'AddToClassList' in s:
            cls_key = re.search(r'__a__(\w+)', s)
            cls_name = ops_strings.get(cls_key.group(1), '?') if cls_key else '?'
            operations.append({
                'op': 'CLASS_ADD',
                'field': last_binding_field or '?',
                'class': cls_name,
                'line': i,
            })
        elif 'RemoveFromClassList' in s:
            cls_key = re.search(r'__a__(\w+)', s)
            cls_name = ops_strings.get(cls_key.group(1), '?') if cls_key else '?'
            operations.append({
                'op': 'CLASS_REMOVE',
                'field': last_binding_field or '?',
                'class': cls_name,
                'line': i,
            })

        # --- SET_VALUE / SET_CHECKED ---
        if 'set_value' in s and 'Binding' not in s:
            operations.append({
                'op': 'SET_VALUE',
                'field': last_binding_field or '?',
                'line': i,
            })
        if 'SetChecked' in s or 'set_isChecked' in s:
            operations.append({
                'op': 'SET_CHECKED',
                'field': last_binding_field or '?',
                'line': i,
            })

        # --- SELF CALLS (call graph) ---
        # Match: TypeName__MethodName( or full namespace prefix
        self_call = re.search(rf'{re.escape(type_name)}__(\w+)\s*[\(\n]', s)
        if not self_call and c_prefix != type_name:
            self_call = re.search(rf'{re.escape(c_prefix)}__(\w+)\s*[\(\n]', s)
        if self_call:
            called = self_call.group(1)
            if not called.startswith('get_') and not called.startswith('set_') and called != '_cctor':
                operations.append({
                    'op': 'CALL',
                    'method': called,
                    'line': i,
                })

        # --- CHILD TYPE CALLS (ServerLine, DofusTab, etc.) ---
        for child_type in ['ServerLine', 'ServerCard', 'DofusTab', 'CheckboxCustom',
                           'DofusButtonCustom', 'PlayButton', 'TextInput', 'DropdownButton']:
            if f'{child_type}__' in s:
                child_method = re.search(rf'{child_type}__(\w+)', s)
                if child_method:
                    operations.append({
                        'op': 'CHILD_CALL',
                        'childType': child_type,
                        'method': child_method.group(1),
                        'line': i,
                    })
                break

    return {
        'totalLines': len(lines),
        'opsCount': len(operations),
        'operations': operations,
    }


def translate_screen(type_name, ops_strings):
    """Translate ALL key methods of a screen handler."""
    methods, full_namespace = find_type_methods(type_name)
    if not methods:
        print(f"  {type_name}: no methods found")
        return None

    result = {
        'type': type_name,
        'namespace': full_namespace,
        'methods': {},
    }

    skipped_trivial = 0
    for method in methods:
        name = method['name']
        # Skip trivial: async state machines, property getters, lambdas
        if name in ('SetStateMachine', '_cctor') or name.startswith('b__'):
            skipped_trivial += 1
            continue

        va = method.get('va', 0)
        ops = extract_method_ops(type_name, name, full_namespace, va, ops_strings)
        if ops and ops['opsCount'] > 0:
            result['methods'][name] = ops
            print(f"    {name}: {ops['opsCount']} ops ({ops['totalLines']}L)")
    if skipped_trivial:
        print(f"    ({skipped_trivial} trivial methods skipped)")

    return result


def main():
    print("Loading OPS strings...")
    ops_strings = load_obfuscated_strings()
    print(f"  {len(ops_strings)} strings loaded")

    screen_logic = {}

    for type_name in SCREEN_TYPES:
        print(f"\nTranslating: {type_name}")
        result = translate_screen(type_name, ops_strings)
        if result:
            method_count = len(result['methods'])
            total_ops = sum(m['opsCount'] for m in result['methods'].values())
            screen_logic[type_name] = result
            print(f"  => {method_count} methods, {total_ops} total ops")

    out_path = os.path.join(OUTPUT_DIR, "ScreenLogic.json")
    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump(screen_logic, f, indent=2, ensure_ascii=False)
    print(f"\nSaved: {out_path} ({os.path.getsize(out_path) // 1024}KB)")

    # Summary
    print("\n=== Summary ===")
    total_methods = sum(len(s['methods']) for s in screen_logic.values())
    total_ops = sum(sum(m['opsCount'] for m in s['methods'].values()) for s in screen_logic.values())
    i18n_ops = sum(
        sum(1 for m in s['methods'].values() for o in m['operations'] if o['op'] == 'I18N_SET')
        for s in screen_logic.values()
    )
    display_ops = sum(
        sum(1 for m in s['methods'].values() for o in m['operations'] if o['op'] == 'DISPLAY')
        for s in screen_logic.values()
    )
    event_ops = sum(
        sum(1 for m in s['methods'].values() for o in m['operations'] if o['op'] == 'EVENT')
        for s in screen_logic.values()
    )
    print(f"Screens: {len(screen_logic)}")
    print(f"Methods: {total_methods}")
    print(f"Total ops: {total_ops}")
    print(f"  I18N_SET: {i18n_ops}")
    print(f"  DISPLAY: {display_ops}")
    print(f"  EVENT: {event_ops}")


if __name__ == "__main__":
    main()
