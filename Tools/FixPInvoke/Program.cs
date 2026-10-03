using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length < 1) { Console.WriteLine("Usage: FixPInvoke <dir>"); return 1; }

string dir = args[0];
int totalFixed = 0;

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(dir);
resolver.AddSearchDirectory(@"C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Data\Managed\UnityEngine");
resolver.AddSearchDirectory(@"C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Data\PlaybackEngines\WebGLSupport\Managed");

foreach (string dllPath in Directory.GetFiles(dir, "*.dll"))
{
    string name = Path.GetFileName(dllPath);

    try
    {
        var bytes = File.ReadAllBytes(dllPath);
        using var asm = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes),
            new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });

        var toFix = new List<(TypeDefinition type, MethodDefinition method)>();

        foreach (var type in AllTypes(asm.MainModule))
            foreach (var m in type.Methods)
                if (m.IsPInvokeImpl || m.IsInternalCall ||
                    (m.Attributes & MethodAttributes.PInvokeImpl) != 0)
                    toFix.Add((type, m));

        if (toFix.Count == 0) continue;

        foreach (var (type, oldMethod) in toFix)
        {
            var newMethod = new MethodDefinition(oldMethod.Name,
                (oldMethod.Attributes & ~MethodAttributes.PInvokeImpl),
                oldMethod.ReturnType);

            newMethod.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;

            foreach (var p in oldMethod.Parameters)
                newMethod.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, p.ParameterType));

            foreach (var gp in oldMethod.GenericParameters)
            {
                var ngp = new GenericParameter(gp.Name, newMethod);
                newMethod.GenericParameters.Add(ngp);
            }

            newMethod.Body = new MethodBody(newMethod);
            var il = newMethod.Body.GetILProcessor();

            if (newMethod.ReturnType.FullName == "System.Void")
            {
                il.Append(il.Create(OpCodes.Ret));
            }
            else if (newMethod.ReturnType.IsValueType)
            {
                var v = new VariableDefinition(newMethod.ReturnType);
                newMethod.Body.Variables.Add(v);
                newMethod.Body.InitLocals = true;
                il.Append(il.Create(OpCodes.Ldloca_S, v));
                il.Append(il.Create(OpCodes.Initobj, newMethod.ReturnType));
                il.Append(il.Create(OpCodes.Ldloc_0));
                il.Append(il.Create(OpCodes.Ret));
            }
            else
            {
                il.Append(il.Create(OpCodes.Ldnull));
                il.Append(il.Create(OpCodes.Ret));
            }

            int idx = type.Methods.IndexOf(oldMethod);
            type.Methods.Remove(oldMethod);
            type.Methods.Insert(idx, newMethod);
        }

        string tmpPath = dllPath + ".tmp";
        asm.Write(tmpPath);
        File.Delete(dllPath);
        File.Move(tmpPath, dllPath);
        Console.WriteLine($"{name}: replaced {toFix.Count} P/Invokes");
        totalFixed += toFix.Count;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{name}: ERROR {ex.Message}");
    }
}

Console.WriteLine($"\nDone. Replaced {totalFixed} total.");
return 0;

static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
{
    foreach (var type in module.Types)
    {
        yield return type;
        foreach (var n in type.NestedTypes) { yield return n; foreach (var nn in n.NestedTypes) yield return nn; }
    }
}
