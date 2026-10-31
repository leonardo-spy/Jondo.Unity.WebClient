#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
patch_controllers.py — Enriquece os Controllers desacoplados com métodos operacionais
específicos necessários para a integração 100% autônoma.
"""

import os
import re

def patch_chat_view():
    path = os.path.join("UnityProject", "Assets", "Controllers", "ChatViewController.cs")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read().replace("\r\n", "\n")

    if "public event Action<string> OnSendMessage;" not in content:
        content = content.replace(
            "public event Action OnNewsClicked;",
            "public event Action OnNewsClicked;\n        public event Action<string> OnSendMessage;"
        )

    input_listener = """            var tf = _binding.ti_chatInput as TextField ?? _binding.ti_chatInput?.Q<TextField>();
            if (tf != null)
            {
                tf.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        string t = tf.value?.Trim();
                        if (!string.IsNullOrEmpty(t))
                        {
                            OnSendMessage?.Invoke(t);
                            tf.value = string.Empty;
                        }
                    }
                });
            }"""

    if "OnSendMessage?.Invoke(t);" not in content:
        pattern = re.compile(r"(_binding\.btn_news\.RegisterCallback<ClickEvent>[^;]+;\s*\})", re.MULTILINE)
        content = pattern.sub(r"\1\n" + input_listener, content)

    append_method = """        public void AppendMessage(string speaker, string message, int channel, string timeStr = "")
        {
            var listChat = _binding.listChat ?? _binding.Root?.Q("listChat");
            if (listChat != null)
            {
                var msgLabel = new Label();
                string color = channel == 0 ? "#ffffff" : channel == 1 ? "#ffff00" : channel == 2 ? "#ff8800" : "#aaccff";
                msgLabel.text = $"<color={color}>[{speaker}] {message}</color>";
                msgLabel.style.fontSize = 12;
                msgLabel.style.whiteSpace = WhiteSpace.Normal;
                listChat.Add(msgLabel);
                if (listChat is ScrollView sv)
                {
                    sv.scrollOffset = new Vector2(0, sv.contentContainer.layout.height);
                }
            }
        }
"""

    if "public void AppendMessage" not in content:
        content = content.replace(
            "public void SetUp()",
            append_method + "\n        public void SetUp()"
        )

    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(content.replace("\n", "\r\n"))
    print("ChatViewController patched successfully.")


def patch_character_info():
    path = os.path.join("UnityProject", "Assets", "Controllers", "CharacterInformationController.cs")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read().replace("\r\n", "\n")

    update_stats = """        public void UpdateStats(int currentHp, int maxHp, int currentAp, int currentMp, int shield = 0, int invocations = 1)
        {
            SetPdvText($"{currentHp} / {maxHp}");
            SetPdvBottomText($"{currentHp}");
            SetPaText($"{currentAp}");
            SetPmText($"{currentMp}");
            SetShieldText(shield > 0 ? $"{shield}" : string.Empty);
            SetIpText($"{invocations}");
        }
"""

    if "public void UpdateStats" not in content:
        content = content.replace(
            "public void SetHeaderStateCustom()",
            update_stats + "\n        public void SetHeaderStateCustom()"
        )

    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(content.replace("\n", "\r\n"))
    print("CharacterInformationController patched successfully.")


def patch_action_bar():
    path = os.path.join("UnityProject", "Assets", "Controllers", "UIActionBarController.cs")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read().replace("\r\n", "\n")

    update_spells = """        public void UpdateSpellSlots(IEnumerable<int> spellIds)
        {
            var grid = _binding.gd_spellitemotes ?? _binding.Root?.Q("gd_spellitemotes");
            if (grid == null) return;

            int slotIdx = 0;
            foreach (var spId in spellIds)
            {
                var slot = grid.Q($"slot_{slotIdx}") ?? (grid.childCount > slotIdx ? grid[slotIdx] : null);
                if (slot != null)
                {
                    var lbl = slot.Q<Label>() ?? new Label();
                    lbl.text = $"S{spId}";
                    lbl.style.fontSize = 10;
                    lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                    if (!slot.Contains(lbl)) slot.Add(lbl);
                }
                slotIdx++;
                if (slotIdx >= 10) break;
            }
        }
"""

    if "public void UpdateSpellSlots" not in content:
        content = content.replace(
            "public void UpdateGrid()",
            update_spells + "\n        public void UpdateGrid()"
        )

    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(content.replace("\n", "\r\n"))
    print("UIActionBarController patched successfully.")


def patch_end_turn():
    path = os.path.join("UnityProject", "Assets", "Controllers", "EndTurnWidgetController.cs")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read().replace("\r\n", "\n")

    helpers = """        public void UpdateTimer(float secondsLeft)
        {
            SetSecondsText(Mathf.CeilToInt(Mathf.Max(0f, secondsLeft)).ToString());
        }

        public void UpdateRound(int round)
        {
            SetTurnCountText($"Tour {round}");
        }
"""

    if "public void UpdateTimer" not in content:
        content = content.replace(
            "public void ToggleSpectatorForbidden()",
            helpers + "\n        public void ToggleSpectatorForbidden()"
        )

    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(content.replace("\n", "\r\n"))
    print("EndTurnWidgetController patched successfully.")


def patch_timeline():
    path = os.path.join("UnityProject", "Assets", "Controllers", "UITimelineController.cs")
    with open(path, "r", encoding="utf-8") as f:
        content = f.read().replace("\r\n", "\n")

    helpers = """        public void UpdateTurn(int round, int order)
        {
            SetCurrentTurnText($"{round}");
        }

        public void ClearFighters()
        {
            _binding.sv_fighters?.Clear();
        }

        public VisualElement AddFighterSlot(long entityId, string name, int currentHp, int maxHp, int order, bool isCurrentTurn)
        {
            if (_binding.sv_fighters == null) return null;
            VisualElement fighterRoot = null;
            if (_uxmlLoader != null)
            {
                var vta = _uxmlLoader("timelinefighter") ?? _uxmlLoader("Assets/UI/Fight/timeline/timelineFighter.uxml");
                if (vta != null)
                {
                    fighterRoot = vta.CloneTree();
                }
            }

            if (fighterRoot == null)
            {
                fighterRoot = new VisualElement();
                fighterRoot.style.width = 48;
                fighterRoot.style.height = 48;
                fighterRoot.style.marginRight = 4;
                fighterRoot.style.backgroundColor = isCurrentTurn ? new Color(0.95f, 0.77f, 0.25f, 0.4f) : new Color(0.1f, 0.12f, 0.16f, 0.8f);
                var lbl = new Label(name);
                lbl.style.fontSize = 9;
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                fighterRoot.Add(lbl);
            }
            else
            {
                var fighterCtrl = new TimelineFighterController(fighterRoot, _uxmlLoader, _i18n);
                fighterCtrl.SetOrderText($"{order + 1}");
            }

            _binding.sv_fighters.Add(fighterRoot);
            return fighterRoot;
        }
"""

    if "public void UpdateTurn(" not in content:
        content = content.replace(
            "public void UpdateTurnCount()",
            helpers + "\n        public void UpdateTurnCount()"
        )

    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(content.replace("\n", "\r\n"))
    print("UITimelineController patched successfully.")


if __name__ == "__main__":
    patch_chat_view()
    patch_character_info()
    patch_action_bar()
    patch_end_turn()
    patch_timeline()
