#!/usr/bin/env python3
"""
Generate C# probe methods from ScreenLogic.json.

Instead of a runtime interpreter, this transpiles the decompiled ops into
C# methods that the probe includes directly. Each screen handler method
becomes a C# method that performs the same operations in the same order.

This is more faithful to the original than a runtime interpreter because:
- The call chain is preserved (OnEnable calls Localize, then CreateTabFilter, etc.)
- Conditional logic from the decompiled flow is maintained
- The generated code can be reviewed and verified

Output:
  output/GeneratedScreenMethods.cs — partial class extending WebGLProbe
"""

import json
import os
import sys
import io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(SCRIPT_DIR, "output")

SCREEN_MAP = {
    "fys": "Login",
    "ServerSelection": "ServerSelection",
    "CharacterSelection": "CharacterSelection",
    "CharacterCreation": "CharacterCreation",
    "fyv": "PreGameMainMenu",
    "CharacterHeader": "CharacterHeader",
    "fyr": "ConnectionBackground",
    "BannerMenu": "BannerMenu",
    "Core.UILogic.Chat.ChatView": "ChatView",
    "Core.UILogic.Inventory.Uis.PlayerInventory.PlayerInventoryUi": "PlayerInventoryUi",
    "Core.UILogic.MainMenu.CharacterInformation": "CharacterInformation",
    "Core.UILogic.GameUICore.UIActionBar": "ActionBar",
    "Core.UILogic.NpcDialog.NpcDialog": "NpcDialog",
    "EquipmentStorageUI": "EquipmentStorageUI",
    "Core.UILogic.Inventory.Uis.PlayerInventory.EquipmentStorageUI": "EquipmentStorageUI",
    "SpellBaseUi": "SpellBaseUi",
    "Core.UILogic.Grimoire.SpellBaseUi": "SpellBaseUi",
}

# Methods that should generate code (the important UI methods)
GENERATE_METHODS = {
    "fys": ["OnEnable", "Localize", "mx", "ctor"],
    "ServerSelection": ["OnEnable", "Localize", "CreateTabFilter", "DisplayFilter",
                         "DisplayServer", "BindServerLine", "InitServer"],
    "CharacterSelection": ["OnEnable", "Localize", "FillCharacters", "BindItem",
                            "DisplayCharacters", "Init", "UpdateCreateButton",
                            "OnSelectionChanged", "UpdateSelectedCharacter"],
    "CharacterCreation": ["OnEnable", "LocalizeTexts", "InitializeUI", "InitializeBinding",
                           "Reset", "SetUpAll", "DisableAll"],
    "fyv": ["ctor"],
    "CharacterHeader": ["OnEnable", "ctor", "ShowHeader", "PlayerDisplay", "OnServersList"],
}


def generate_method(screen_type, method_name, method_data, screen_name):
    """Generate a C# method from extracted ops."""
    ops = method_data.get("operations", [])
    if not ops:
        return None

    lines = []
    cs_method_name = f"Gen_{screen_name}_{method_name}".replace("<", "_").replace(">", "_").replace("|", "_")
    lines.append(f"    // Generated from decompiled {screen_type}.{method_name}")
    lines.append(f"    // {method_data.get('totalLines', 0)} lines → {len(ops)} ops")
    lines.append(f"    void {cs_method_name}(VisualElement root)")
    lines.append("    {")

    prev_field = None
    for op in ops:
        op_type = op.get("op", "")
        field = op.get("field", "?")

        if op_type == "BIND_GET" and field != "?":
            prev_field = field
            # Don't emit code for just accessing — the next op will use it

        elif op_type == "I18N_SET" and field != "?":
            i18n = op.get("i18n", "")
            action = op.get("action", "set_text")
            to_upper = op.get("toUpper", False)
            if i18n.startswith("ui."):
                upper_code = ".ToUpper()" if to_upper else ""
                if action == "set_placeholder":
                    lines.append(f'        {{ var el = root.Q("{field}"); if (el is TextField tf) tf.label = I18n("{i18n}"){upper_code}; }}')
                elif action == "set_title":
                    lines.append(f'        {{ var el = root.Q("{field}"); if (el is Label lbl) lbl.text = I18n("{i18n}"){upper_code}; }}')
                else:
                    lines.append(f'        SetElementText(root, "{field}", I18n("{i18n}"){upper_code});')
            prev_field = None

        elif op_type == "SET_TEXT" and field != "?":
            lines.append(f'        // SET_TEXT on {field} (dynamic text from data)')
            prev_field = None

        elif op_type == "DISPLAY" and field != "?":
            val = op.get("value", "?")
            via = op.get("via", "")
            if val == "Flex":
                lines.append(f'        {{ var el = root.Q("{field}"); if (el != null) el.style.display = DisplayStyle.Flex; }}')
            elif val == "None":
                lines.append(f'        {{ var el = root.Q("{field}"); if (el != null) el.style.display = DisplayStyle.None; }}')
            else:
                lines.append(f'        // DISPLAY {field} = ? (via {via}, depends on runtime state — default Flex for DOTween fallback)')
                lines.append(f'        {{ var el = root.Q("{field}"); if (el != null) el.style.display = DisplayStyle.Flex; }}')
            prev_field = None

        elif op_type == "VISIBLE" and field != "?":
            val = op.get("value", "true")
            vis = "Visibility.Visible" if val == "true" else "Visibility.Hidden"
            lines.append(f'        {{ var el = root.Q("{field}"); if (el != null) el.style.visibility = {vis}; }}')
            prev_field = None

        elif op_type == "ENABLE" and field != "?":
            val = op.get("value", "true")
            lines.append(f'        {{ var el = root.Q("{field}"); if (el != null) el.SetEnabled({val}); }}')
            prev_field = None

        elif op_type == "EVENT" and field != "?":
            evt_type = op.get("type", "click")
            via = op.get("via", "")
            if evt_type in ("click", "clicked"):
                lines.append(f'        {{ var el = root.Q("{field}"); if (el != null) el.RegisterCallback<ClickEvent>(evt => Log("  [Gen] Click: {field}")); }}')
            else:
                lines.append(f'        // EVENT: {field} {evt_type} ({via})')
            prev_field = None

        elif op_type == "CALL":
            called = op.get("method", "")
            called_cs = f"Gen_{screen_name}_{called}"
            lines.append(f'        // CALL {screen_type}.{called}')
            prev_field = None

        elif op_type == "CLASS_ADD" and field != "?":
            cls = op.get("class", "?")
            if cls != "?":
                lines.append(f'        {{ var el = root.Q("{field}"); if (el != null) el.AddToClassList("{cls}"); }}')
            prev_field = None

        elif op_type == "CLASS_REMOVE" and field != "?":
            cls = op.get("class", "?")
            lines.append(f'        {{ var el = root.Q("{field}"); el?.RemoveFromClassList("{cls}"); }}')
            prev_field = None

    lines.append("    }")
    lines.append("")
    return "\n".join(lines)


def generate_helper():
    """Generate the SetElementText helper."""
    return '''    void SetElementText(VisualElement root, string elementName, string text)
    {
        var el = root.Q(elementName);
        if (el == null) return;
        if (el is Label lbl) lbl.text = text;
        else if (el is Button btn) btn.text = text;
        else
        {
            var inner = el.Q("playButton_label");
            if (inner is Label il) il.text = text;
        }
    }
'''


def main():
    with open(os.path.join(OUTPUT_DIR, "ScreenLogic.json")) as f:
        logic = json.load(f)

    all_methods = []
    all_methods.append("// Auto-generated from decompiled_all.c via generate_probe_methods.py")
    all_methods.append("// DO NOT EDIT — regenerate with: py Tools/ClientAnalyzer/generate_probe_methods.py")
    all_methods.append("using UnityEngine;")
    all_methods.append("using UnityEngine.UIElements;")
    all_methods.append("")
    all_methods.append("public partial class WebGLProbe")
    all_methods.append("{")
    all_methods.append(generate_helper())

    total_generated = 0
    total_ops_covered = 0

    for screen_type in SCREEN_MAP:
        screen_name = SCREEN_MAP.get(screen_type, screen_type)
        screen_data = logic.get(screen_type, {})
        methods = screen_data.get("methods", {})

        generated_for_screen = 0
        for method_name, method_data in methods.items():
            # Skip trivial methods with very few actionable ops
            actionable = sum(1 for o in method_data.get("operations", [])
                           if o["op"] in ("I18N_SET", "DISPLAY", "VISIBLE", "ENABLE",
                                          "EVENT", "CLASS_ADD", "CLASS_REMOVE", "SET_TEXT"))
            if actionable == 0:
                continue

            code = generate_method(screen_type, method_name, method_data, screen_name)
            if code:
                all_methods.append(code)
                total_generated += 1
                total_ops_covered += method_data.get("opsCount", 0)
                generated_for_screen += 1

        if generated_for_screen > 0:
            print(f"  {screen_name}: {generated_for_screen} methods generated")

    # Generate ApplyGeneratedMethods dispatcher
    # Only calls INIT methods (OnEnable, Localize, ctor, Init, Reset) on screen open.
    # State-change methods (mx, bjdo, etc.) are available but called by probe when needed.
    INIT_METHODS = {"OnEnable", "Localize", "LocalizeTexts", "ctor", "Init",
                    "InitializeUI", "InitializeBinding", "Reset", "SetUpAll",
                    "DisplayCharacter", "DisplayCharacters", "CreateTabFilter",
                    "ShowHeader", "PlayerDisplay", "OnServersList", "Toggle", "BindUiElements", "SetUp", "SetModuleSprites", "AddTooltips", "AddMouseClick", "SubscribeInputs", "SubscribeClicked", "SetToDefaultWidthAndHeight"}
    # Methods that set launcher-dependent states (btn_play disabled until launcher connects)
    # Skip for WebGL: no Ankama launcher → button should start enabled
    SKIP_WEBGL = {
        ("Login", "OnEnable"),  # fys.OnEnable: btn_play=false, dd_host=false (waits for launcher)
    }

    dispatcher = []
    dispatcher.append("    void ApplyGeneratedMethods(string screenName, VisualElement root)")
    dispatcher.append("    {")
    dispatcher.append("        switch (screenName)")
    dispatcher.append("        {")

    for screen_type in SCREEN_MAP:
        screen_name = SCREEN_MAP[screen_type]
        screen_data = logic.get(screen_type, {})
        methods = screen_data.get("methods", {})

        gen_names = []
        for method_name, method_data in methods.items():
            # Only include INIT methods in the dispatcher
            if method_name not in INIT_METHODS:
                continue
            # Skip launcher-dependent methods in WebGL
            if (screen_name, method_name) in SKIP_WEBGL:
                continue
            actionable = sum(1 for o in method_data.get("operations", [])
                           if o["op"] in ("I18N_SET", "DISPLAY", "VISIBLE", "ENABLE",
                                          "EVENT", "CLASS_ADD", "CLASS_REMOVE", "SET_TEXT"))
            if actionable > 0:
                cs_name = f"Gen_{screen_name}_{method_name}"
                cs_name = cs_name.replace("<", "_").replace(">", "_").replace("|", "_")
                gen_names.append(cs_name)

        if gen_names:
            dispatcher.append(f'            case "{screen_name}":')
            for name in gen_names:
                dispatcher.append(f'                {name}(root);')
            dispatcher.append(f'                break;')

    dispatcher.append('        }')
    dispatcher.append('    }')
    dispatcher.append('')

    all_methods.append("\n".join(dispatcher))

    # Lifecycle-aware dispatchers
    ctor_disp = ["    void DispatchGen_Ctor(string screen, VisualElement root)", "    {", "        switch (screen)", "        {"]
    for screen_type, screen_name in SCREEN_MAP.items():
        methods = logic.get(screen_type, {}).get("methods", {})
        calls = []
        for m in ["ctor", "InitializeUI", "InitializeBinding"]:
            if m in methods:
                cs_name = f"Gen_{screen_name}_{m}".replace("<", "_").replace(">", "_").replace("|", "_")
                if any(f"void {cs_name}(" in m_entry for m_entry in all_methods):
                    calls.append(cs_name)
        if calls:
            ctor_disp.append(f'            case "{screen_name}":')
            for c in calls:
                ctor_disp.append(f'                {c}(root);')
            ctor_disp.append('                break;')
    ctor_disp.extend(["        }", "    }", ""])
    all_methods.append("\n".join(ctor_disp))

    enable_disp = ["    void DispatchGen_OnEnable(string screen, VisualElement root)", "    {", "        switch (screen)", "        {"]
    for screen_type, screen_name in SCREEN_MAP.items():
        methods = logic.get(screen_type, {}).get("methods", {})
        calls = []
        for m in ["OnEnable", "EnableEffect", "SpawnEffect", "InitServer", "Init", "DisplayCharacters", "DisplayCharacter", "Reset", "SetUpAll"]:
            if m in methods:
                cs_name = f"Gen_{screen_name}_{m}".replace("<", "_").replace(">", "_").replace("|", "_")
                if any(f"void {cs_name}(" in m_entry for m_entry in all_methods):
                    calls.append(cs_name)
        if calls:
            enable_disp.append(f'            case "{screen_name}":')
            for c in calls:
                enable_disp.append(f'                {c}(root);')
            enable_disp.append('                break;')
    enable_disp.extend(["        }", "    }", ""])
    all_methods.append("\n".join(enable_disp))

    loc_disp = ["    void DispatchGen_Localize(string screen, VisualElement root)", "    {", "        switch (screen)", "        {"]
    for screen_type, screen_name in SCREEN_MAP.items():
        methods = logic.get(screen_type, {}).get("methods", {})
        calls = []
        for m in ["Localize", "LocalizeTexts"]:
            if m in methods:
                cs_name = f"Gen_{screen_name}_{m}".replace("<", "_").replace(">", "_").replace("|", "_")
                if any(f"void {cs_name}(" in m_entry for m_entry in all_methods):
                    calls.append(cs_name)
        if calls:
            loc_disp.append(f'            case "{screen_name}":')
            for c in calls:
                loc_disp.append(f'                {c}(root);')
            loc_disp.append('                break;')
    loc_disp.extend(["        }", "    }", ""])
    all_methods.append("\n".join(loc_disp))

    evt_disp = ["    void DispatchGen_Event(string screen, string evt, VisualElement root)", "    {", "        switch (screen)", "        {"]
    for screen_type, screen_name in SCREEN_MAP.items():
        methods = logic.get(screen_type, {}).get("methods", {})
        cases = []
        for m in methods:
            if m in ("ctor", "Localize", "LocalizeTexts", "OnEnable"):
                continue
            cs_name = f"Gen_{screen_name}_{m}".replace("<", "_").replace(">", "_").replace("|", "_")
            if any(f"void {cs_name}(" in m_entry for m_entry in all_methods):
                cases.append((m, cs_name))
        if cases:
            evt_disp.append(f'            case "{screen_name}":')
            evt_disp.append("                switch (evt)")
            evt_disp.append("                {")
            for orig_name, c in cases:
                evt_disp.append(f'                    case "{orig_name}": {c}(root); break;')
            evt_disp.append("                }")
            evt_disp.append('                break;')
    evt_disp.extend(["        }", "    }", ""])
    all_methods.append("\n".join(evt_disp))

    all_methods.append("}")

    out_path = os.path.join(OUTPUT_DIR, "GeneratedScreenMethods.cs")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(all_methods))

    print(f"\nGenerated: {total_generated} methods covering {total_ops_covered} ops")
    print(f"Saved: {out_path}")
    import shutil
    unity_cs_path = os.path.join(SCRIPT_DIR, "../../UnityProject/Assets/GeneratedScreenMethods.cs")
    unity_cs_path = os.path.normpath(unity_cs_path)
    shutil.copyfile(out_path, unity_cs_path)
    print(f"Copied to: {unity_cs_path}")


if __name__ == "__main__":
    main()
