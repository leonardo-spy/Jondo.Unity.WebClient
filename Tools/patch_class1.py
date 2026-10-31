import sys

path = r"..\JondoFix\Class1.cs"
with open(path, "r", encoding="utf-8") as f:
    text = f.read()

target = "public override void OnUpdate()\r\n        {\r\n            if (UseLocalRedirect && !itemMappingsLoadedFromClient)"
if target not in text:
    target = "public override void OnUpdate()\n        {\n            if (UseLocalRedirect && !itemMappingsLoadedFromClient)"

replacement = """public override void OnUpdate()
        {
            try
            {
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8) || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F10))
                {
                    JondoDumper.DumpAll("Hotkey_F8_or_F10");
                }

                string triggerFile = System.IO.Path.Combine(JondoDumper.DumpDirectory, "dump.trigger");
                if (System.IO.File.Exists(triggerFile))
                {
                    try { System.IO.File.Delete(triggerFile); } catch { }
                    JondoDumper.DumpAll("FileTrigger");
                }
            }
            catch (Exception)
            {
                // Early frames input safety
            }

            if (UseLocalRedirect && !itemMappingsLoadedFromClient)"""

if target in text:
    text = text.replace(target, replacement, 1)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    print("Successfully patched Class1.cs")
else:
    print("Target not found in Class1.cs")

