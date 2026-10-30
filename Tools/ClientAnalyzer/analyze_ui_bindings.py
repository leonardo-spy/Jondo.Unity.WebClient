import os
import re
import json

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(SCRIPT_DIR))
DUMP_CS = os.path.join(REPO_ROOT, "Decompiled", "dump.cs")

def main():
    bindings = {}
    current_ns = ""
    current_cls = ""
    current_type_idx = ""

    with open(DUMP_CS, "r", encoding="utf-8") as f:
        for line in f:
            if line.startswith("// Namespace: "):
                current_ns = line.replace("// Namespace: ", "").strip()
            elif "class " in line or "struct " in line or "interface " in line:
                m_cls = re.search(r'(?:public|private|internal|protected)?\s*(?:sealed|abstract|static)?\s*(?:class|struct|interface)\s+([\w<>_]+)', line)
                if m_cls:
                    current_cls = m_cls.group(1)
                m_type = re.search(r'TypeDefIndex:\s*(\d+)', line)
                if m_type:
                    current_type_idx = m_type.group(1)
            elif "// RVA: " in line:
                if current_ns.startswith("Core.UILogic.Bindings") or current_ns.startswith("Core.UI.Bindings"):
                    key = f"{current_ns}.{current_cls}"
                    if key not in bindings:
                        bindings[key] = {
                            "namespace": current_ns,
                            "class": current_cls,
                            "typeDef": current_type_idx,
                            "methods": []
                        }
                    m_name = line.strip()
                    bindings[key]["methods"].append(m_name)

    print(f"Total UI_BINDINGS classes: {len(bindings)}")
    method_count = sum(len(v["methods"]) for v in bindings.values())
    print(f"Total UI_BINDINGS methods: {method_count}")
    
    # Print sample top classes with highest method count
    sorted_cls = sorted(bindings.values(), key=lambda x: -len(x["methods"]))
    print("\nTop 20 UI_BINDINGS classes:")
    for c in sorted_cls[:20]:
        print(f"  {c['namespace']}.{c['class']} (TypeDefIndex: {c['typeDef']}) - {len(c['methods'])} methods")

    # Let's inspect some notable ones: ItemSlotBinding, SpellSlotBinding, ActionBarBinding, etc.
    notable = [c for c in bindings.values() if any(k in c['class'] for k in ["Item", "Spell", "ActionBar", "Chat", "Entity", "Server", "Character", "Slot", "Button", "Card"])]
    print(f"\nNotable domain binding classes ({len(notable)}):")
    for c in notable[:25]:
        print(f"  {c['class']} ({c['typeDef']}) in {c['namespace']}")

if __name__ == "__main__":
    main()

