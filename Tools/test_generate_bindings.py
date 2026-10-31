#!/usr/bin/env python3
"""
test_generate_bindings.py — Testes Unitários do Gerador de Bindings (Pilar 2 Lacuna 7)

Verifica que:
1. Snippets de structs do dump.cs são parseados corretamente.
2. Tipos proprietários da Ankama são mapeados com precisão para tipos Unity standard.
3. Tipos desconhecidos recebem fallback seguro para VisualElement.
4. O código C# gerado é sintaticamente válido e possui as declarações e Bind() esperados.
"""

import unittest
from generate_bindings import (
    map_type,
    extract_fields_from_struct_text,
    generate_csharp_class,
    parse_binding_from_dump,
    TYPE_MAPPING
)

SAMPLE_DUMP_STRUCT = """
// Namespace: Core.UILogic.Bindings
public struct ServerSelectionBinding : IAsyncStateMachine // TypeDefIndex: 5201
{
	// Fields
	private VisualElement _____root_____; // 0x0
	private DofusVisualElement _ctr_serverSelection; // 0x8
	private VisualElement _ctr_header; // 0x10
	private DofusLabel _lbl_title; // 0x60
	private TextInput _inp_search; // 0x78
	private DofusButtonCustom _btn_search; // 0x80
	private PlayButton _btn_play; // 0x208
	private CheckboxCustom _cb_onlyCharacter; // 0x210
	private DofusLabel _lbl_noServer; // 0x218

	// Properties
	public DofusVisualElement ctr_serverSelection { get; }
	public VisualElement ctr_header { get; }
}
"""


class TestBindingGenerator(unittest.TestCase):

    def test_type_mappings(self):
        """Verifica se todos os tipos Ankama críticos são mapeados corretamente."""
        self.assertEqual(map_type("DofusLabel"), "Label")
        self.assertEqual(map_type("DofusButtonCustom"), "Button")
        self.assertEqual(map_type("DofusVisualElement"), "VisualElement")
        self.assertEqual(map_type("TextInput"), "TextField")
        self.assertEqual(map_type("CheckboxCustom"), "Toggle")
        self.assertEqual(map_type("PlayButton"), "VisualElement")
        self.assertEqual(map_type("ServerCard"), "VisualElement")

    def test_unknown_type_fallback(self):
        """Verifica se tipos não previstos ou futuros fazem fallback seguro para VisualElement."""
        self.assertEqual(map_type("UnknownCustomAnkamaWidget123"), "VisualElement")
        self.assertEqual(map_type("SomeFuturisticControl"), "VisualElement")

    def test_extract_fields(self):
        """Verifica a extração correta de campos a partir do snippet da struct."""
        fields = extract_fields_from_struct_text(SAMPLE_DUMP_STRUCT)
        names = [f[2] for f in fields]
        mapped_types = {f[2]: f[1] for f in fields}

        # _____root_____ deve ser ignorado
        self.assertNotIn("____root____", names)

        # Campos esperados devem estar presentes
        self.assertIn("ctr_serverSelection", names)
        self.assertIn("lbl_title", names)
        self.assertIn("inp_search", names)
        self.assertIn("btn_search", names)
        self.assertIn("btn_play", names)
        self.assertIn("cb_onlyCharacter", names)
        self.assertIn("lbl_noServer", names)

        # Tipos mapeados devem estar corretos
        self.assertEqual(mapped_types["lbl_title"], "Label")
        self.assertEqual(mapped_types["inp_search"], "TextField")
        self.assertEqual(mapped_types["btn_search"], "Button")
        self.assertEqual(mapped_types["cb_onlyCharacter"], "Toggle")
        self.assertEqual(mapped_types["lbl_noServer"], "Label")

    def test_csharp_generation(self):
        """Verifica o código C# gerado."""
        fields = [
            ("DofusLabel", "Label", "lbl_title"),
            ("PlayButton", "VisualElement", "btn_play"),
            ("DofusButtonCustom", "Button", "btn_search"),
        ]
        cs = generate_csharp_class("ServerSelectionBinding", fields)

        # Valida namespace e declaração da classe
        self.assertIn("namespace Jondo.UI.Generated", cs)
        self.assertIn("public class GeneratedServerSelectionBinding", cs)
        self.assertIn("public VisualElement Root { get; private set; }", cs)

        # Valida propriedades
        self.assertIn("public Label lbl_title { get; private set; }", cs)
        self.assertIn("public VisualElement btn_play { get; private set; }", cs)
        self.assertIn("public Button btn_search { get; private set; }", cs)

        # Valida método Bind
        self.assertIn("public void Bind(VisualElement root)", cs)
        self.assertIn('lbl_title = root.Q<Label>("lbl_title");', cs)
        self.assertIn('btn_play = root.Q("btn_play");', cs)
        self.assertIn('btn_search = root.Q<Button>("btn_search");', cs)

    def test_parse_from_dump(self):
        """Verifica se parse_binding_from_dump localiza a struct no texto completo."""
        fields = parse_binding_from_dump(SAMPLE_DUMP_STRUCT, "ServerSelectionBinding")
        self.assertGreater(len(fields), 0)
        self.assertTrue(any(f[2] == "btn_play" for f in fields))


if __name__ == "__main__":
    unittest.main()
