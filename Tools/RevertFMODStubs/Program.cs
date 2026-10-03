using Mono.Cecil;
using Mono.Cecil.Cil;

// Reverts FMOD P/Invoke stubs created by FixPInvoke back to real P/Invokes.
// For WebGL: DllImport("__Internal") → static linking with libfmodstudio.a
// For Win64: DllImport("fmodstudio") → dynamic linking with fmodstudio.dll
//
// SOURCE: dump.cs shows 391 FMOD5_* extern methods in Ankama.AudioManagement.dll
// FixPInvoke replaced them with managed stubs `{ return default; }`
// This tool reverses that by making them P/Invoke again.
//
// Usage: dotnet run -- <plugins-dir> [--target webgl|win64]

if (args.Length < 1) { Console.WriteLine("Usage: RevertFMODStubs <plugins-dir> [--target webgl|win64]"); return 1; }

string dir = args[0];
string target = args.Length > 2 && args[1] == "--target" ? args[2] : "webgl";
string dllName = target == "webgl" ? "__Internal" : "fmodstudio";

Console.WriteLine($"Target: {target} → DllImport(\"{dllName}\")");

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(dir);

// Process Ankama.AudioManagement.dll (has FMOD types + P/Invokes)
string[] dllsToProcess = { "Ankama.AudioManagement.dll" };
int totalReverted = 0;

foreach (string dllFile in dllsToProcess)
{
    string dllPath = Path.Combine(dir, dllFile);
    if (!File.Exists(dllPath))
    {
        Console.WriteLine($"  {dllFile}: NOT FOUND");
        continue;
    }

    var bytes = File.ReadAllBytes(dllPath);
    using var asm = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes),
        new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });

    int reverted = 0;
    var module = asm.MainModule;

    foreach (var type in AllTypes(module))
    {
        var toRevert = new List<MethodDefinition>();

        foreach (var m in type.Methods)
        {
            // Check ALL FMOD P/Invokes: FMOD5_* (core) + FMOD_Studio_* (studio)
            bool isFmodMethod = m.Name.StartsWith("FMOD5_") || m.Name.StartsWith("FMOD_Studio_");
            if (!isFmodMethod) continue;

            if (m.IsPInvokeImpl)
            {
                string curModule = m.PInvokeInfo?.Module?.Name ?? "null";
                if (curModule != dllName)
                    toRevert.Add(m);
            }
            else if (m.HasBody)
            {
                // Was stubbed by FixPInvoke — restore as P/Invoke
                toRevert.Add(m);
            }
        }

        foreach (var method in toRevert)
        {
            var moduleRef = module.ModuleReferences.FirstOrDefault(r => r.Name == dllName);
            if (moduleRef == null)
            {
                moduleRef = new ModuleReference(dllName);
                module.ModuleReferences.Add(moduleRef);
            }

            if (method.IsPInvokeImpl && method.PInvokeInfo != null)
            {
                // Just change the module reference (fmodstudio → __Internal)
                method.PInvokeInfo = new PInvokeInfo(
                    method.PInvokeInfo.Attributes,
                    method.PInvokeInfo.EntryPoint ?? method.Name,
                    moduleRef);
                reverted++;
            }
            else
            {
                // Was stubbed — create P/Invoke replacement
                var newMethod = new MethodDefinition(method.Name,
                    method.Attributes | MethodAttributes.PInvokeImpl,
                    method.ReturnType);
                newMethod.ImplAttributes = MethodImplAttributes.PreserveSig;
                foreach (var p in method.Parameters)
                    newMethod.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, p.ParameterType));
                newMethod.PInvokeInfo = new PInvokeInfo(
                    PInvokeAttributes.CallConvCdecl | PInvokeAttributes.CharSetAnsi,
                    method.Name, moduleRef);
                int idx = type.Methods.IndexOf(method);
                type.Methods.Remove(method);
                type.Methods.Insert(idx, newMethod);
                reverted++;
            }
        }
    }

    if (reverted > 0)
    {
        string tmpPath = dllPath + ".tmp";
        asm.Write(tmpPath);
        File.Delete(dllPath);
        File.Move(tmpPath, dllPath);
        Console.WriteLine($"  {dllFile}: {reverted} P/Invokes restored → DllImport(\"{dllName}\")");
        totalReverted += reverted;
    }
    else
    {
        Console.WriteLine($"  {dllFile}: no FMOD5_ stubs found");
    }
}

Console.WriteLine($"\nTotal: {totalReverted} P/Invokes reverted");
return 0;

static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition m)
{
    foreach (var t in m.Types)
    {
        yield return t;
        foreach (var nt in t.NestedTypes)
            yield return nt;
    }
}
