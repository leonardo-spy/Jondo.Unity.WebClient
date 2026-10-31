#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
scaffold_controller.py — Gerador Industrial de Controllers C# Desacoplados (Pilar 3)

Lê Decompiled/dump.cs, localiza a classe de UI da Ankama e sua Binding correspondente,
garante a existência do Generated<Binding>.cs e gera um Controller C# fortemente tipado,
com eventos, registro de callbacks, propriedades e métodos delegados.
"""

import os
import re
import sys
from typing import Dict, List, Tuple, Optional

# Reutiliza o gerador de bindings existente
from generate_bindings import (
    parse_binding_from_dump,
    generate_csharp_class,
    map_type,
    TYPE_MAPPING
)

DUMP_PATH = os.path.join("Decompiled", "dump.cs")
OUTPUT_DIR = os.path.join("UnityProject", "Assets", "Controllers")
BINDINGS_DIR = os.path.join("UnityProject", "Assets", "Generated", "UI")


def find_class_in_dump(dump_text: str, class_name: str) -> Optional[Dict]:
    """
    Localiza uma classe de UI no dump.cs e extrai:
    - namespace
    - classe base e tipo de binding (ex: gag<EndTurnWidgetBinding>)
    - lista de campos internos
    - lista de métodos públicos e privados
    """
    class_decl = re.compile(
        r"(?://\s*Namespace:\s*([^\r\n]+)\s+)?public\s+class\s+" + re.escape(class_name) + r"\s*:\s*([^\r\n]+)"
    )
    match = class_decl.search(dump_text)
    if not match:
        return None

    namespace = match.group(1).strip() if match.group(1) else ""
    inheritance = match.group(2).strip()

    binding_match = re.search(r"<([A-Za-z0-9_]+Binding)>", inheritance)
    binding_name = binding_match.group(1) if binding_match else None
    base_name = inheritance.split("<")[0].strip() if "<" in inheritance else inheritance.split()[0]

    # Encontra o corpo da classe delimitado por chaves balanceadas
    start_brace = dump_text.find("{", match.end())
    if start_brace == -1:
        return None

    depth = 0
    end_brace = -1
    for i in range(start_brace, len(dump_text)):
        ch = dump_text[i]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                end_brace = i
                break

    if end_brace == -1:
        end_brace = len(dump_text)

    body = dump_text[start_brace + 1:end_brace]

    # Extrai métodos
    method_pattern = re.compile(
        r"(?:public|private|protected)\s+(?:(?:static|sealed|override|async|virtual)\s+)*([A-Za-z0-9_<>,.\[\]]+)\s+([A-Za-z0-9_]+)\s*\(([^)]*)\)"
    )
    methods = []
    for line in body.splitlines():
        line = line.strip()
        m_meth = method_pattern.match(line)
        if m_meth:
            ret_type, m_name, params = m_meth.groups()
            if not m_name.startswith("<") and m_name not in (".ctor", ".cctor"):
                methods.append({
                    "name": m_name,
                    "return_type": ret_type,
                    "params": params.strip()
                })

    return {
        "class_name": class_name,
        "namespace": namespace,
        "base_name": base_name,
        "binding_name": binding_name,
        "methods": methods,
        "body": body
    }


def to_pascal_case(s: str) -> str:
    """Converte snake_case ou camelCase com prefixo _btn_ para PascalCase preservando maiúsculas internas."""
    s = re.sub(r"^(_?btn_|_?lbl_|_?ctr_|_?tg_|_?inp_)", "", s)
    parts = s.split("_")
    return "".join((p[0].upper() + p[1:]) for p in parts if p)


def generate_controller_csharp(
    class_name: str,
    binding_name: str,
    binding_fields: List[Tuple[str, str, str]],
    class_methods: List[Dict],
    uxml_name: Optional[str] = None
) -> str:
    """
    Gera o código C# do Controller desacoplado e fortemente tipado.
    """
    ctrl_name = class_name
    if ctrl_name.endswith("UI"):
        ctrl_name = ctrl_name[:-2] + "Controller"
    elif not ctrl_name.endswith("Controller"):
        ctrl_name += "Controller"

    events = []
    event_wires = []
    setters = []

    for raw_type, mapped_type, clean_name in binding_fields:
        pascal = to_pascal_case(clean_name)

        # Botões
        if clean_name.startswith("btn_") or "button" in clean_name.lower():
            evt_name = f"On{pascal}Clicked"
            events.append(f"        public event Action {evt_name};")
            event_wires.append(f"""            if (_binding.{clean_name} != null)
            {{
                _binding.{clean_name}.RegisterCallback<ClickEvent>(evt => {evt_name}?.Invoke());
            }}""")

        # Toggles
        elif clean_name.startswith("tg_") or "toggle" in clean_name.lower():
            evt_name = f"On{pascal}Toggled"
            events.append(f"        public event Action<bool> {evt_name};")
            event_wires.append(f"""            if (_binding.{clean_name} != null)
            {{
                _binding.{clean_name}.RegisterCallback<ClickEvent>(evt =>
                {{
                    // Toggle state dispatcher
                    {evt_name}?.Invoke(true);
                }});
            }}""")

        # Labels tipados
        elif clean_name.startswith("lbl_") or mapped_type == "Label":
            setter_name = f"Set{pascal}Text"
            setters.append(f"""        public void {setter_name}(string text)
        {{
            if (_binding.{clean_name} != null)
            {{
                _binding.{clean_name}.text = text ?? string.Empty;
            }}
        }}""")

    # Transcreve métodos chave do dump.cs
    custom_methods = []
    seen_method_names = set()

    for m in class_methods:
        m_name = m["name"]
        if m_name in seen_method_names:
            continue
        seen_method_names.add(m_name)

        if any(m_name.startswith(p) for p in ("Update", "Set", "Show", "Hide", "Toggle", "Reset", "Highlight", "OnTurn", "OnFight", "OnReady")):
            custom_methods.append(f"""        public void {m_name}()
        {{
            // Transcribed from {class_name}.{m_name} (dump.cs)
        }}""")

    events_code = "\n".join(events) if events else "        // No default button events detected"
    wires_code = "\n".join(event_wires) if event_wires else "            // No button event wires needed"
    setters_code = "\n\n".join(setters) if setters else ""
    methods_code = "\n\n".join(custom_methods) if custom_methods else ""

    uxml_comment = f"//   Default UXML: {uxml_name}" if uxml_name else ""

    code = f"""// <auto-generated-or-transcribed>
//   Generated by Tools/scaffold_controller.py
//   Source Class: {class_name} (dump.cs)
//   Binding: {binding_name} (Core.UILogic.Bindings)
{uxml_comment}
// </auto-generated-or-transcribed>

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Jondo.UI.Generated;

namespace Jondo.UI.Controllers
{{
    public class {ctrl_name}
    {{
        private readonly Generated{binding_name} _binding;
        private readonly Func<string, VisualTreeAsset> _uxmlLoader;
        private readonly Func<string, string, string> _i18n;

{events_code}

        public Generated{binding_name} Binding => _binding;
        public VisualElement Root => _binding.Root;

        public {ctrl_name}(
            VisualElement root,
            Func<string, VisualTreeAsset> uxmlLoader = null,
            Func<string, string, string> i18n = null)
        {{
            _binding = new Generated{binding_name}();
            _binding.Bind(root);
            _uxmlLoader = uxmlLoader;
            _i18n = i18n ?? ((k, d) => d);

            SetupEventListeners();
        }}

        private void SetupEventListeners()
        {{
{wires_code}
        }}

{setters_code}

{methods_code}
    }}
}}
"""
    return code


def scaffold_screen(screen_name: str, dump_path: str = DUMP_PATH, out_dir: str = OUTPUT_DIR) -> str:
    """
    Executa o pipeline completo para gerar o controller de uma tela:
    1. Localiza no dump.cs
    2. Garante o binding tipado gerado
    3. Gera e salva o Controller C#
    """
    if not os.path.exists(dump_path):
        raise FileNotFoundError(f"dump.cs não encontrado em {dump_path}")

    with open(dump_path, "r", encoding="utf-8", errors="ignore") as f:
        dump_text = f.read()

    info = find_class_in_dump(dump_text, screen_name)
    if not info:
        raise ValueError(f"Classe '{screen_name}' não encontrada no {dump_path}")

    binding_name = info["binding_name"]
    if not binding_name:
        candidate = screen_name + "Binding"
        if candidate in dump_text:
            binding_name = candidate
        elif screen_name.endswith("UI") and (screen_name[:-2] + "Binding") in dump_text:
            binding_name = screen_name[:-2] + "Binding"
        else:
            raise ValueError(f"Binding associada à classe '{screen_name}' não identificada.")

    print(f"[{screen_name}] Binding identificada: {binding_name}")

    # Garante a existência do Binding gerado
    binding_fields = parse_binding_from_dump(dump_text, binding_name)
    if not binding_fields:
        raise ValueError(f"Não foi possível extrair campos da binding '{binding_name}'")

    generated_binding_csharp = generate_csharp_class(binding_name, binding_fields)
    binding_file_path = os.path.join(BINDINGS_DIR, f"Generated{binding_name}.cs")
    os.makedirs(BINDINGS_DIR, exist_ok=True)
    with open(binding_file_path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(generated_binding_csharp)
    print(f"[{screen_name}] Binding gravada: {binding_file_path}")

    uxml_name = screen_name.lower().replace("ui", "")
    ctrl_code = generate_controller_csharp(
        screen_name,
        binding_name,
        binding_fields,
        info["methods"],
        uxml_name=f"{uxml_name}.uxml"
    )

    ctrl_name = screen_name
    if ctrl_name.endswith("UI"):
        ctrl_name = ctrl_name[:-2] + "Controller"
    elif not ctrl_name.endswith("Controller"):
        ctrl_name += "Controller"

    os.makedirs(out_dir, exist_ok=True)
    ctrl_file_path = os.path.join(out_dir, f"{ctrl_name}.cs")
    with open(ctrl_file_path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(ctrl_code)

    print(f"[{screen_name}] Controller gravado: {ctrl_file_path}")
    return ctrl_file_path


def main():
    if len(sys.argv) < 2:
        print("Uso: python scaffold_controller.py <NomeDaClasseUI>")
        print("Exemplo: python scaffold_controller.py EndTurnWidgetUI")
        sys.exit(1)

    screen = sys.argv[1]
    try:
        path = scaffold_screen(screen)
        print(f"[SUCESSO] Scaffold completo para {screen} -> {path}")
    except Exception as e:
        print(f"[ERRO] {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
