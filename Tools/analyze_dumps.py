import json
import os
import sys

def find_element(node, target_name):
    if node.get("name") == target_name:
        return node
    for c in node.get("children", []):
        res = find_element(c, target_name)
        if res:
            return res
    return None

def print_element_details(elem, indent=""):
    print(f"{indent}=== Element: {elem.get('name')} ({elem.get('type')}) ===")
    print(f"{indent}Classes: {elem.get('classes')}")
    layout = elem.get("layout", {})
    print(f"{indent}Layout:")
    for k, v in layout.items():
        if v is not None and v != 0 and v != "Auto":
            print(f"{indent}  {k}: {v}")
    
    borders = elem.get("borders", {})
    if any(v for v in borders.values() if v is not None and v != 0):
        print(f"{indent}Borders / Radii:")
        for k, v in borders.items():
            if v is not None and v != 0:
                print(f"{indent}  {k}: {v}")
                
    bg = elem.get("background", {})
    if any(v for v in bg.values() if v is not None):
        print(f"{indent}Background:")
        for k, v in bg.items():
            if v is not None:
                print(f"{indent}  {k}: {v}")
                
    typo = elem.get("typography", {})
    if any(v for v in typo.values() if v is not None):
        print(f"{indent}Typography:")
        for k, v in typo.items():
            if v is not None:
                print(f"{indent}  {k}: {v}")
                
    transform = elem.get("transform", {})
    if any(v for v in transform.values() if v is not None):
        print(f"{indent}Transform:")
        for k, v in transform.items():
            if v is not None:
                print(f"{indent}  {k}: {v}")
                
    ankama = elem.get("ankamaProps", {})
    if ankama:
        print(f"{indent}AnkamaProps: {ankama}")

def print_tree_shallow(elem, depth=0, max_depth=2):
    indent = "  " * depth
    name = elem.get("name") or "(unnamed)"
    etype = elem.get("type")
    layout = elem.get("layout", {})
    w, h = layout.get("width", 0), layout.get("height", 0)
    disp = layout.get("display")
    classes = elem.get("classes", [])
    print(f"{indent}- {name} ({etype}) [{', '.join(classes)}] | {w:.1f}x{h:.1f} | disp={disp}")
    if depth < max_depth:
        for c in elem.get("children", []):
            print_tree_shallow(c, depth + 1, max_depth)

def main():
    dump_dir = "LiveDofusDump/Dump_20261031_152338_2560x1080_Hotkey_F8_or_F10"
    if len(sys.argv) > 1 and not sys.argv[1].startswith("--"):
        dump_dir = sys.argv[1]
        
    json_path = os.path.join(dump_dir, "active_ui_documents.json")
    with open(json_path, "r", encoding="utf-8-sig") as f:
        data = json.load(f)
        
    root = data[0]["root"]
    
    target = "ServerSelection"
    if len(sys.argv) > 2:
        target = sys.argv[2]
        
    elem = find_element(root, target)
    if elem:
        print_element_details(elem)
        print("\n--- Children Hierarchy ---")
        print_tree_shallow(elem, depth=0, max_depth=3)
    else:
        print(f"Target '{target}' not found!")

if __name__ == "__main__":
    main()
