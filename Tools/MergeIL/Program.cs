using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length < 3)
{
    Console.WriteLine("Usage: MergeIL <stubs-dir> <cpp2il-dir> <output-dir>");
    return 1;
}

string stubsDir = args[0];
string cpp2ilDir = args[1];
string outputDir = args[2];

Directory.CreateDirectory(outputDir);

int totalMerged = 0;
int totalMethods = 0;
int totalFailed = 0;

foreach (string stubPath in Directory.GetFiles(stubsDir, "*.dll"))
{
    string name = Path.GetFileName(stubPath);
    string cpp2ilPath = Path.Combine(cpp2ilDir, name);
    string outputPath = Path.Combine(outputDir, name);

    if (!File.Exists(cpp2ilPath))
    {
        File.Copy(stubPath, outputPath, true);
        continue;
    }

    Console.Write($"{name}: ");

    try
    {
        int merged = 0;
        int methods = 0;

        var stubParams = new ReaderParameters { ReadWrite = false, ReadSymbols = false };
        var cpp2ilParams = new ReaderParameters { ReadWrite = false, ReadSymbols = false };

        using var stubAsm = AssemblyDefinition.ReadAssembly(stubPath, stubParams);
        using var cpp2ilAsm = AssemblyDefinition.ReadAssembly(cpp2ilPath, cpp2ilParams);

        var cpp2ilMethods = new Dictionary<string, MethodDefinition>();
        foreach (var type in AllTypes(cpp2ilAsm.MainModule))
        {
            foreach (var method in type.Methods)
            {
                if (method.HasBody && method.Body.Instructions.Count > 0)
                {
                    string key = MethodKey(method);
                    cpp2ilMethods.TryAdd(key, method);
                }
            }
        }

        foreach (var type in AllTypes(stubAsm.MainModule))
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                methods++;

                string key = MethodKey(method);
                if (!cpp2ilMethods.TryGetValue(key, out var source)) continue;

                try
                {
                    CopyBody(method, source, stubAsm.MainModule);
                    merged++;
                }
                catch
                {
                    totalFailed++;
                }
            }
        }

        stubAsm.Write(outputPath);
        totalMethods += methods;
        totalMerged += merged;
        Console.WriteLine($"{merged}/{methods} methods merged");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED: {ex.Message}");
        File.Copy(stubPath, outputPath, true);
    }
}

Console.WriteLine();
Console.WriteLine($"Done. {totalMerged}/{totalMethods} methods merged, {totalFailed} failed.");
return 0;

static string MethodKey(MethodDefinition m)
{
    var parts = new List<string> { m.DeclaringType.FullName, m.Name };
    foreach (var p in m.Parameters)
        parts.Add(p.ParameterType.FullName);
    return string.Join("|", parts);
}

static void CopyBody(MethodDefinition target, MethodDefinition source, ModuleDefinition targetModule)
{
    target.Body = new MethodBody(target);
    var il = target.Body.GetILProcessor();

    foreach (var v in source.Body.Variables)
    {
        var vType = targetModule.ImportReference(v.VariableType);
        target.Body.Variables.Add(new VariableDefinition(vType));
    }

    target.Body.InitLocals = source.Body.InitLocals;
    target.Body.MaxStackSize = source.Body.MaxStackSize;

    var instrMap = new Dictionary<Instruction, Instruction>();

    foreach (var instr in source.Body.Instructions)
    {
        var newInstr = CloneInstruction(instr, targetModule);
        instrMap[instr] = newInstr;
        il.Append(newInstr);
    }

    foreach (var instr in target.Body.Instructions)
    {
        if (instr.Operand is Instruction targetInstr && instrMap.ContainsValue(targetInstr) == false)
        {
            var orig = instrMap.FirstOrDefault(kv => kv.Value == instr).Key;
            if (orig?.Operand is Instruction origTarget && instrMap.TryGetValue(origTarget, out var mapped))
                instr.Operand = mapped;
        }
    }

    foreach (var instr in source.Body.Instructions)
    {
        if (instr.Operand is Instruction brTarget && instrMap.TryGetValue(brTarget, out var newTarget))
            instrMap[instr].Operand = newTarget;
        else if (instr.Operand is Instruction[] targets)
        {
            var newTargets = new Instruction[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                newTargets[i] = instrMap.TryGetValue(targets[i], out var t) ? t : targets[i];
            instrMap[instr].Operand = newTargets;
        }
    }

    foreach (var handler in source.Body.ExceptionHandlers)
    {
        var h = new ExceptionHandler(handler.HandlerType);
        if (handler.CatchType != null)
            h.CatchType = targetModule.ImportReference(handler.CatchType);
        if (handler.TryStart != null && instrMap.TryGetValue(handler.TryStart, out var ts)) h.TryStart = ts;
        if (handler.TryEnd != null && instrMap.TryGetValue(handler.TryEnd, out var te)) h.TryEnd = te;
        if (handler.HandlerStart != null && instrMap.TryGetValue(handler.HandlerStart, out var hs)) h.HandlerStart = hs;
        if (handler.HandlerEnd != null && instrMap.TryGetValue(handler.HandlerEnd, out var he)) h.HandlerEnd = he;
        if (handler.FilterStart != null && instrMap.TryGetValue(handler.FilterStart, out var fs)) h.FilterStart = fs;
        target.Body.ExceptionHandlers.Add(h);
    }
}

static Instruction CloneInstruction(Instruction instr, ModuleDefinition module)
{
    if (instr.Operand == null)
        return Instruction.Create(instr.OpCode);

    return instr.Operand switch
    {
        TypeReference t => Instruction.Create(instr.OpCode, module.ImportReference(t)),
        MethodReference m => Instruction.Create(instr.OpCode, module.ImportReference(m)),
        FieldReference f => Instruction.Create(instr.OpCode, module.ImportReference(f)),
        string s => Instruction.Create(instr.OpCode, s),
        int i => Instruction.Create(instr.OpCode, i),
        long l => Instruction.Create(instr.OpCode, l),
        float f => Instruction.Create(instr.OpCode, f),
        double d => Instruction.Create(instr.OpCode, d),
        sbyte b => Instruction.Create(instr.OpCode, b),
        byte b => Instruction.Create(instr.OpCode, b),
        Instruction _ => Instruction.Create(instr.OpCode, instr), // fixed later
        Instruction[] _ => Instruction.Create(instr.OpCode, Array.Empty<Instruction>()), // fixed later
        VariableDefinition v => Instruction.Create(instr.OpCode, v),
        ParameterDefinition p => Instruction.Create(instr.OpCode, p),
        CallSite cs => Instruction.Create(instr.OpCode, cs),
        _ => Instruction.Create(instr.OpCode),
    };
}

static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
{
    foreach (var type in module.Types)
    {
        yield return type;
        foreach (var nested in AllNested(type))
            yield return nested;
    }
}

static IEnumerable<TypeDefinition> AllNested(TypeDefinition type)
{
    foreach (var nested in type.NestedTypes)
    {
        yield return nested;
        foreach (var sub in AllNested(nested))
            yield return sub;
    }
}
