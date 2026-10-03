using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length < 1)
{
    Console.WriteLine("Usage: FixCctor <PluginsDir> [--dry-run]");
    Console.WriteLine("  Fixes broken .cctor methods in UniTask.dll and Core.dll");
    return 1;
}

var pluginsDir = args[0];
var dryRun = args.Contains("--dry-run");
int totalFixed = 0;

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(pluginsDir);

// Fix ALL DLLs — broken .cctors cascade across assemblies
foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll"))
    FixDll(dll);

Console.WriteLine($"\nTotal fixed: {totalFixed}");
return 0;

void FixDll(string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"[SKIP] {Path.GetFileName(path)} — not found");
        return;
    }

    Console.WriteLine($"[{Path.GetFileName(path)}]");

    var bytes = File.ReadAllBytes(path);
    var ms = new MemoryStream(bytes);
    var readerParams = new ReaderParameters
    {
        ReadingMode = ReadingMode.Deferred,
        AssemblyResolver = resolver,
        ReadSymbols = false,
    };

    using var dll = AssemblyDefinition.ReadAssembly(ms, readerParams);
    int count = 0;

    foreach (var type in dll.MainModule.Types)
        count += FixCctorsRecursive(type);

    if (count > 0 && !dryRun)
    {
        using var outMs = new MemoryStream();
        dll.Write(outMs);
        dll.Dispose();
        ms.Dispose();
        File.WriteAllBytes(path, outMs.ToArray());
        Console.WriteLine($"  → {count} .cctor(s) fixed, saved");
    }
    else if (count > 0)
    {
        Console.WriteLine($"  → {count} .cctor(s) would be fixed (dry-run)");
    }
    else
    {
        Console.WriteLine($"  → no broken .cctors found");
    }

    totalFixed += count;
}

int FixCctorsRecursive(TypeDefinition type)
{
    int count = 0;

    foreach (var method in type.Methods)
    {
        if (!method.IsConstructor || !method.IsStatic || !method.HasBody)
            continue;

        var body = method.Body;
        if (body.Instructions.Count <= 1)
            continue;

        body.Instructions.Clear();
        body.ExceptionHandlers.Clear();
        body.Variables.Clear();
        var il = body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ret));
        body.MaxStackSize = 0;

        Console.WriteLine($"  fixed: {type.FullName}..cctor ({body.Instructions.Count} → 1 ret)");
        count++;
    }

    foreach (var nested in type.NestedTypes)
        count += FixCctorsRecursive(nested);

    return count;
}
