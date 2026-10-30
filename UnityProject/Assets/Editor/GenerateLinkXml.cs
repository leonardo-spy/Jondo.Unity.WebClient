using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GenerateLinkXml
{
    const BindingFlags ALL = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    [MenuItem("Build/Generate link.xml (auto-scan VE types)")]
    static void Generate()
    {
        // Plugin DLLs aren't in AppDomain — load them with reflection-only context
        var pluginsDir = Path.GetFullPath("Assets/Plugins");
        if (!Directory.Exists(pluginsDir))
        {
            Debug.LogError($"[GenerateLinkXml] Plugins directory not found: {pluginsDir}");
            return;
        }

        var dlls = Directory.GetFiles(pluginsDir, "*.dll", SearchOption.TopDirectoryOnly);
        Debug.Log($"[GenerateLinkXml] Found {dlls.Length} DLLs in {pluginsDir}");

        // We need the VisualElement type name to match against — can't use typeof() since
        // the plugin DLLs reference a different UnityEngine.UIElements. Match by name instead.
        const string veFullName = "UnityEngine.UIElements.VisualElement";

        var veTypes = new Dictionary<string, List<(string fullName, bool hasUxmlSD)>>();
        int totalScanned = 0, totalFailed = 0;

        foreach (var dllPath in dlls)
        {
            var dllName = Path.GetFileNameWithoutExtension(dllPath);

            Assembly asm = null;
            try
            {
                asm = Assembly.LoadFrom(dllPath);
            }
            catch
            {
                totalFailed++;
                continue;
            }
            totalScanned++;

            Type[] types = null;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            catch
            {
                Debug.Log($"[GenerateLinkXml] {dllName}: GetTypes failed");
                continue;
            }
            if (types == null) continue;

            string asmName = asm.GetName().Name;
            int found = 0;

            foreach (var t in types)
            {
                if (t == null) continue;
                try
                {
                    if (t.IsGenericTypeDefinition) continue;

                    // Walk inheritance chain by name — can't use IsAssignableFrom across load contexts
                    if (!InheritsFrom(t, veFullName)) continue;

                    // Skip Unity-internal types that might leak through
                    if (t.Namespace != null &&
                        (t.Namespace.StartsWith("UnityEngine") || t.Namespace.StartsWith("UnityEditor") ||
                         t.Namespace.StartsWith("Unity.")))
                        continue;

                    bool hasSD = false;
                    try { hasSD = t.GetNestedType("UxmlSerializedData", ALL) != null; } catch { }

                    if (!veTypes.ContainsKey(asmName))
                        veTypes[asmName] = new List<(string, bool)>();
                    veTypes[asmName].Add((t.FullName.Replace('+', '/'), hasSD));
                    found++;
                }
                catch { }
            }

            if (found > 0)
                Debug.Log($"[GenerateLinkXml] {asmName}: {found} VE types");
        }

        Debug.Log($"[GenerateLinkXml] Scan complete: {totalScanned} DLLs loaded, {totalFailed} skipped, {veTypes.Values.Sum(v => v.Count)} VE types");

        // Build link.xml
        var sb = new StringBuilder();
        sb.AppendLine("<linker>");
        sb.AppendLine("  <!-- Unity Engine UIElements and TextCore types preserved for UXML deserialization from AssetBundles -->");
        sb.AppendLine("  <assembly fullname=\"UnityEngine.UIElementsModule\" preserve=\"all\"/>");
        sb.AppendLine("  <assembly fullname=\"UnityEngine.TextCoreFontEngineModule\" preserve=\"all\"/>");
        sb.AppendLine("  <assembly fullname=\"UnityEngine.TextCoreTextEngineModule\" preserve=\"all\"/>");
        sb.AppendLine("  <assembly fullname=\"UnityEngine.UIModule\" preserve=\"all\"/>");
        sb.AppendLine("  <assembly fullname=\"UnityEngine.IMGUIModule\" preserve=\"all\"/>");
        sb.AppendLine("  <assembly fullname=\"Assembly-CSharp\" preserve=\"all\"/>");

        // Core assembly: services + VE types
        sb.AppendLine("  <assembly fullname=\"Core\" preserve=\"nothing\">");
        sb.AppendLine("    <!-- Service types (obfuscated names) -->");
        foreach (var svc in new[] { "glo", "glo/glj", "glo/glk", "glo/gll", "glo/glm", "glo/gln", "ecr", "fyh", "ezp" })
            sb.AppendLine($"    <type fullname=\"{svc}\" preserve=\"all\"/>");

        if (veTypes.TryGetValue("Core", out var coreTypes))
        {
            sb.AppendLine($"    <!-- {coreTypes.Count} VisualElement types (auto-discovered) -->");
            foreach (var (fn, hasSD) in coreTypes.OrderBy(x => x.fullName))
            {
                sb.AppendLine($"    <type fullname=\"{fn}\" preserve=\"all\"/>");
                if (hasSD)
                    sb.AppendLine($"    <type fullname=\"{fn}/UxmlSerializedData\" preserve=\"all\"/>");
            }
        }
        else
        {
            Debug.LogWarning("[GenerateLinkXml] No VE types found in 'Core' assembly.");
            foreach (var kv in veTypes)
                Debug.Log($"  Found in: {kv.Key} ({kv.Value.Count} types)");
        }
        sb.AppendLine("  </assembly>");

        // Other assemblies with VE types
        foreach (var kv in veTypes.Where(x => x.Key != "Core").OrderBy(x => x.Key))
        {
            sb.AppendLine($"  <assembly fullname=\"{kv.Key}\" preserve=\"nothing\">");
            sb.AppendLine($"    <!-- {kv.Value.Count} VisualElement types -->");
            foreach (var (fn, hasSD) in kv.Value.OrderBy(x => x.fullName))
            {
                sb.AppendLine($"    <type fullname=\"{fn}\" preserve=\"all\"/>");
                if (hasSD)
                    sb.AppendLine($"    <type fullname=\"{fn}/UxmlSerializedData\" preserve=\"all\"/>");
            }
            sb.AppendLine("  </assembly>");
        }

        // Known assemblies without VE types
        var allDiscovered = new HashSet<string>(veTypes.Keys);
        foreach (var a in new[]
        {
            "Core.Localization", "AleCore",
            "Ankama.AddressableUtility.Runtime", "Ankama.Animator2D", "Ankama.AudioManagement",
            "Ankama.Dofus.Core.Characters", "Ankama.Dofus.Core.DataCenter", "Ankama.Dofus.Core.World",
            "Ankama.Dofus.Protocol.Connection", "Ankama.Dofus.Protocol.Game",
            "Ankama.LauncherConnection", "Ankama.ScreenManager", "Ankama.SpinConnection", "Ankama.Utilities",
            "Google.Protobuf",
            "DotNetty.Buffers", "DotNetty.Codecs", "DotNetty.Common", "DotNetty.Handlers", "DotNetty.Transport",
            "UniTask", "QFSW.QC", "QFSW.QC.Demo", "QFSW.QC.Extras",
            "XLuaAssembly", "ZaapClient",
            "Com.Ankama.HaapiAnkama", "Com.Ankama.HaapiDofus", "Com.Ankama.Shopi",
            "BouncyCastle.Crypto", "Newtonsoft.Json", "Logger",
            "ShapesRuntime", "Sentry", "Sentry.Unity", "mcs",
        })
        {
            if (!allDiscovered.Contains(a))
                sb.AppendLine($"  <assembly fullname=\"{a}\" preserve=\"nothing\"/>");
        }

        sb.AppendLine("</linker>");

        var path = "Assets/link.xml";
        File.WriteAllText(path, sb.ToString());
        AssetDatabase.Refresh();

        int totalTypes = veTypes.Values.Sum(v => v.Count);
        int totalSD = veTypes.Values.Sum(v => v.Count(x => x.hasUxmlSD));
        Debug.Log($"[GenerateLinkXml] Generated {path}: {totalTypes} VE types + {totalSD} UxmlSerializedData across {veTypes.Count} assemblies");
    }

    static bool InheritsFrom(Type t, string ancestorFullName)
    {
        for (var cur = t; cur != null; cur = cur.BaseType)
        {
            if (cur.FullName == ancestorFullName)
                return true;
        }
        return false;
    }
}
