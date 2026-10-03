using Mono.Cecil;
using Mono.Cecil.Cil;

string dir = args[0];
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(dir);
resolver.AddSearchDirectory(@"C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Data\Managed\UnityEngine");
resolver.AddSearchDirectory(@"C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Data\PlaybackEngines\WebGLSupport\Managed");

int total = 0;

foreach (string dllPath in Directory.GetFiles(dir, "*.dll"))
{
    string name = Path.GetFileName(dllPath);
    try
    {
        var bytes = File.ReadAllBytes(dllPath);
        using var asm = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes),
            new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });

        var preserveAttrType = asm.MainModule.ImportReference(
            typeof(System.Attribute)).Resolve();

        TypeReference? preserveRef = null;
        foreach (var asmRef in asm.MainModule.AssemblyReferences)
        {
            try
            {
                var resolved = resolver.Resolve(asmRef);
                foreach (var t in resolved.MainModule.Types)
                {
                    if (t.FullName == "UnityEngine.Scripting.PreserveAttribute")
                    {
                        preserveRef = asm.MainModule.ImportReference(t);
                        break;
                    }
                }
                if (preserveRef != null) break;
            }
            catch { }
        }

        if (preserveRef == null)
        {
            // Try loading from UnityEngine.CoreModule directly
            try
            {
                var unityAsm = AssemblyDefinition.ReadAssembly(
                    @"C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Data\PlaybackEngines\WebGLSupport\Managed\UnityEngine.CoreModule.dll",
                    new ReaderParameters { ReadSymbols = false });
                foreach (var t in unityAsm.MainModule.Types)
                {
                    if (t.FullName == "UnityEngine.Scripting.PreserveAttribute")
                    {
                        preserveRef = asm.MainModule.ImportReference(t);
                        break;
                    }
                }
            }
            catch { }
        }

        if (preserveRef == null) continue;

        var preserveCtor = new MethodReference(".ctor", asm.MainModule.TypeSystem.Void, preserveRef);
        preserveCtor.HasThis = true;
        preserveCtor = asm.MainModule.ImportReference(preserveCtor);

        bool changed = false;
        foreach (var type in AllTypes(asm.MainModule))
        {
            if (!DerivesFromMonoBehaviour(type)) continue;

            bool hasPreserve = false;
            foreach (var attr in type.CustomAttributes)
            {
                if (attr.AttributeType.FullName == "UnityEngine.Scripting.PreserveAttribute")
                {
                    hasPreserve = true;
                    break;
                }
            }

            if (!hasPreserve)
            {
                type.CustomAttributes.Add(new CustomAttribute(preserveCtor));
                Console.WriteLine($"  {name}: [Preserve] {type.FullName}");
                changed = true;
                total++;
            }
        }

        if (changed)
        {
            string tmp = dllPath + ".tmp";
            asm.Write(tmp);
            asm.Dispose();
            File.Delete(dllPath);
            File.Move(tmp, dllPath);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {name}: ERROR {ex.Message}");
    }
}
Console.WriteLine($"\nDone. Added [Preserve] to {total} MonoBehaviour types.");

static bool DerivesFromMonoBehaviour(TypeDefinition type)
{
    var current = type.BaseType;
    int depth = 0;
    while (current != null && depth < 20)
    {
        if (current.FullName == "UnityEngine.MonoBehaviour") return true;
        if (current.FullName == "UnityEngine.ScriptableObject") return true;
        try { current = current.Resolve()?.BaseType; }
        catch { break; }
        depth++;
    }
    return false;
}

static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition m)
{
    foreach (var t in m.Types) foreach (var x in Deep(t)) yield return x;
}
static IEnumerable<TypeDefinition> Deep(TypeDefinition t)
{
    yield return t;
    foreach (var n in t.NestedTypes) foreach (var x in Deep(n)) yield return x;
}
