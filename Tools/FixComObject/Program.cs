using Mono.Cecil;

// Fix __Il2CppComObject inheritance in Cpp2IL DLLs for WebGL builds.
// SOURCE: V1 guide — IL2CPP generates __Il2CppComObject internally, causing
//   "Failed to resolve base type System.__Il2CppComObject" in the Unity Linker.
// FIX: Change base type from __Il2CppComObject to System.Object.
//   Also remove __Generated.dll reference and the __Il2CppComObject type def if present.
//
// Usage: dotnet run -- <dll-path-or-dir>

if (args.Length < 1) { Console.WriteLine("Usage: FixComObject <dll-path-or-dir>"); return 1; }

string target = args[0];
var files = File.Exists(target) ? new[] { target }
    : Directory.GetFiles(target, "*.dll");

int totalFixed = 0;

foreach (string dllPath in files)
{
    string name = Path.GetFileName(dllPath);
    try
    {
        var bytes = File.ReadAllBytes(dllPath);
        var ms = new MemoryStream(bytes);
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath)!);
        // Unity managed assemblies needed for resolving references
        resolver.AddSearchDirectory(@"C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Data\Managed\UnityEngine");
        resolver.AddSearchDirectory(@"C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Data\PlaybackEngines\WebGLSupport\Managed");
        resolver.AddSearchDirectory(@"C:\DofusWeb\Assets\Plugins");

        using var asm = AssemblyDefinition.ReadAssembly(ms,
            new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate });

        var module = asm.MainModule;
        int fixed_ = 0;

        // 1. Fix types inheriting from __Il2CppComObject → System.Object
        var objectRef = module.TypeSystem.Object;
        foreach (var type in AllTypes(module))
        {
            if (type.BaseType != null && type.BaseType.FullName == "System.__Il2CppComObject")
            {
                Console.WriteLine($"  {name}: {type.FullName} base: __Il2CppComObject → System.Object");
                type.BaseType = objectRef;
                fixed_++;
            }
        }

        // 2. Remove __Il2CppComObject type definitions (if defined in this assembly)
        var toRemove = new List<TypeDefinition>();
        foreach (var type in module.Types)
        {
            if (type.FullName == "System.__Il2CppComObject" ||
                type.FullName == "System.__Il2CppComDelegate")
            {
                toRemove.Add(type);
            }
        }
        foreach (var t in toRemove)
        {
            Console.WriteLine($"  {name}: Removed type definition {t.FullName}");
            module.Types.Remove(t);
            fixed_++;
        }

        // 3. Remove __Generated assembly reference
        var genRef = module.AssemblyReferences
            .FirstOrDefault(r => r.Name == "__Generated");
        if (genRef != null)
        {
            module.AssemblyReferences.Remove(genRef);
            Console.WriteLine($"  {name}: Removed __Generated assembly reference");
            fixed_++;
        }

        // 4. Fix methods with corrupted IL that crash the linker
        // These are typically DOTween/animation methods that Cpp2IL couldn't recover
        // AUTO-DETECT and fix methods with unresolvable references.
        // Root causes found via investigation of decompiled_all.c:
        //   - DG.Tweening (DOTween) generic types → linker can't resolve
        //   - __Il2CppFullySharedGenericType → IL2CPP placeholder, doesn't exist in managed
        //   - __Generated assembly references
        var poisonPatterns = new[] { "DG.Tweening", "__Il2CppFullySharedGeneric", "__Generated" };
        int brokenFixed = 0;
        foreach (var type in AllTypes(module))
        {
            foreach (var method in type.Methods.ToList())
            {
                if (!method.HasBody || method.Body.Instructions.Count <= 1) continue;
                bool isBroken = false;
                try
                {
                    foreach (var inst in method.Body.Instructions)
                    {
                        string operandStr = null;
                        try
                        {
                            if (inst.Operand is MemberReference mr) operandStr = mr.FullName;
                            else if (inst.Operand is TypeReference tr) operandStr = tr.FullName;
                        }
                        catch { isBroken = true; break; }

                        if (operandStr != null && poisonPatterns.Any(p => operandStr.Contains(p)))
                        { isBroken = true; break; }
                    }
                    if (!isBroken)
                    {
                        foreach (var v in method.Body.Variables)
                        {
                            try
                            {
                                var vtn = v.VariableType.FullName;
                                if (poisonPatterns.Any(p => vtn.Contains(p)))
                                { isBroken = true; break; }
                            }
                            catch { isBroken = true; break; }
                        }
                    }
                }
                catch { isBroken = true; }

                if (isBroken)
                {
                    int idx = type.Methods.IndexOf(method);
                    type.Methods.Remove(method);
                    var replacement = new MethodDefinition(method.Name,
                        method.Attributes & ~MethodAttributes.PInvokeImpl,
                        method.ReturnType);
                    replacement.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;
                    foreach (var p in method.Parameters)
                        replacement.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, p.ParameterType));
                    foreach (var gp in method.GenericParameters)
                        replacement.GenericParameters.Add(new GenericParameter(gp.Name, replacement));
                    EmptyMethodBody(replacement);
                    type.Methods.Insert(idx, replacement);
                    if (brokenFixed < 20)
                        Console.WriteLine($"    → {type.FullName}::{method.Name}");
                    brokenFixed++;
                }
            }
        }
        if (brokenFixed > 0)
        {
            Console.WriteLine($"  {name}: Auto-fixed {brokenFixed} methods with poison references (DOTween/__Il2CppFullySharedGeneric)");
            fixed_ += brokenFixed;
        }

        // Methods known to have corrupted IL that crashes the Unity linker.
        // Found iteratively by building and checking ILLink error IL1005.
        // These are methods where Cpp2IL recovery produced invalid IL.
        // AUTO-DETECT broken IL by scanning for unresolvable references:
        // 1. DG.Tweening (DOTween) — generic types crash the linker
        // 2. __Il2CppFullySharedGenericType — IL2CPP placeholder, not in managed assemblies
        // 3. Known broken types from iterative builds
        // ROOT CAUSE: Cpp2IL generates IL with references to these types but the
        // WebGL linker/IL2CPP can't resolve them in the managed assembly context.
        var brokenTypesList = new List<string>();
        // Individual methods (Type::Method format)
        var brokenMethodsList = new List<string>();

        string typesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "broken_types.txt");
        if (File.Exists(typesFile))
            foreach (var line in File.ReadAllLines(typesFile))
                if (!string.IsNullOrWhiteSpace(line) && !brokenTypesList.Contains(line.Trim()))
                    brokenTypesList.Add(line.Trim());

        // Empty ALL methods in broken types
        foreach (var type in AllTypes(module))
        {
            bool typeMatch = brokenTypesList.Any(b =>
                type.FullName == b || type.FullName.Replace("/", ".") == b.Replace("/", "."));
            if (!typeMatch) continue;

            int methodsFixed = 0;
            foreach (var method in type.Methods.ToList())
            {
                if (!method.HasBody) continue;
                int idx = type.Methods.IndexOf(method);
                type.Methods.Remove(method);
                var replacement = new MethodDefinition(method.Name,
                    method.Attributes & ~MethodAttributes.PInvokeImpl,
                    method.ReturnType);
                replacement.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;
                foreach (var p in method.Parameters)
                    replacement.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, p.ParameterType));
                foreach (var gp in method.GenericParameters)
                    replacement.GenericParameters.Add(new GenericParameter(gp.Name, replacement));
                EmptyMethodBody(replacement);
                type.Methods.Insert(idx, replacement);
                methodsFixed++;
            }
            if (methodsFixed > 0)
            {
                Console.WriteLine($"  {name}: Emptied {methodsFixed} methods in broken type {type.FullName}");
                fixed_ += methodsFixed;
            }
        }

        string[] brokenMethods = brokenMethodsList.ToArray();
        foreach (var type in AllTypes(module))
        {
            foreach (var method in type.Methods.ToList())
            {
                string methodKey = $"{type.FullName}::{method.Name}";
                if (brokenMethods.Any(b => methodKey == b) && method.HasBody)
                {
                    // Remove and re-add with clean body (Cecil caches the original)
                    int idx = type.Methods.IndexOf(method);
                    type.Methods.Remove(method);

                    var replacement = new MethodDefinition(method.Name,
                        method.Attributes & ~MethodAttributes.PInvokeImpl,
                        method.ReturnType);
                    replacement.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;
                    foreach (var p in method.Parameters)
                        replacement.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, p.ParameterType));
                    EmptyMethodBody(replacement);
                    type.Methods.Insert(idx, replacement);

                    Console.WriteLine($"  {name}: Replaced broken method {methodKey}");
                    fixed_++;
                }
            }
        }

        string[] brokenTypes = { };
        var typesToRemove = new List<TypeDefinition>();
        foreach (var type in AllTypes(module))
        {
            if (brokenTypes.Any(b => type.FullName == b))
            {
                typesToRemove.Add(type);
            }
        }
        foreach (var t in typesToRemove)
        {
            if (t.DeclaringType != null)
                t.DeclaringType.NestedTypes.Remove(t);
            else
                module.Types.Remove(t);
            Console.WriteLine($"  {name}: Removed broken type {t.FullName}");
            fixed_++;
        }
        foreach (var type in AllTypes(module))
        {
            foreach (var method in type.Methods)
            {
                string fullName = $"{type.FullName}::{method.Name}";
                if (brokenMethods.Any(b => fullName == b || fullName.Replace("/", ".") == b))
                {
                    // Empty the body — these are animation methods that don't work in WebGL
                    // Create a fresh minimal body to replace corrupted IL
                    var newBody = new Mono.Cecil.Cil.MethodBody(method);
                    newBody.InitLocals = false;
                    newBody.MaxStackSize = 8;
                    var processor = newBody.GetILProcessor();
                    if (method.ReturnType.FullName == "System.Void")
                    {
                        processor.Append(processor.Create(Mono.Cecil.Cil.OpCodes.Nop));
                        processor.Append(processor.Create(Mono.Cecil.Cil.OpCodes.Ret));
                    }
                    else
                    {
                        processor.Append(processor.Create(Mono.Cecil.Cil.OpCodes.Ldnull));
                        processor.Append(processor.Create(Mono.Cecil.Cil.OpCodes.Ret));
                    }
                    method.Body = newBody;
                    // Clear any exception handlers and local variables
                    method.Body.ExceptionHandlers.Clear();
                    method.Body.Variables.Clear();
                    Console.WriteLine($"  {name}: Fixed broken IL in {fullName}");
                    fixed_++;
                }
            }
        }

        if (fixed_ > 0)
        {
            string tmpPath = dllPath + ".fixed";
            asm.Write(tmpPath, new WriterParameters { WriteSymbols = false });
            asm.Dispose();
            ms.Dispose();
            File.Delete(dllPath);
            File.Move(tmpPath, dllPath);

            // VERIFY: re-read and check if the fixes persisted
            var verifyBytes = File.ReadAllBytes(dllPath);
            using var verifyAsm = AssemblyDefinition.ReadAssembly(new MemoryStream(verifyBytes),
                new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate });
            int stillBroken = 0;
            foreach (var vtype in AllTypes(verifyAsm.MainModule))
            {
                bool isFixedType = brokenTypesList.Any(b => vtype.FullName == b || vtype.FullName.Replace("/", ".") == b.Replace("/", "."));
                if (!isFixedType) continue;
                foreach (var vm in vtype.Methods)
                {
                    if (vm.HasBody && vm.Body.Instructions.Count > 2) stillBroken++;
                }
            }
            Console.WriteLine($"  {name}: {fixed_} fixes applied → saved ({new FileInfo(dllPath).Length / 1024 / 1024}MB), verify: {stillBroken} still have bodies");
            totalFixed += fixed_;
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"  {name}: ERROR — {e.Message}");
    }
}

Console.WriteLine($"\nTotal: {totalFixed} fixes across all DLLs");
return 0;

static void EmptyMethodBody(MethodDefinition method)
{
    var newBody = new Mono.Cecil.Cil.MethodBody(method);
    var il = newBody.GetILProcessor();
    if (method.ReturnType.FullName == "System.Void")
    {
        il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ret));
    }
    else if (method.ReturnType.IsValueType)
    {
        var v = new Mono.Cecil.Cil.VariableDefinition(method.ReturnType);
        newBody.Variables.Add(v);
        newBody.InitLocals = true;
        il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ldloca_S, v));
        il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Initobj, method.ReturnType));
        il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ldloc_0));
        il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ret));
    }
    else
    {
        il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ldnull));
        il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ret));
    }
    method.Body = newBody;
}

static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition m)
{
    foreach (var t in m.Types)
    {
        yield return t;
        foreach (var nt in AllNested(t))
            yield return nt;
    }
}

static IEnumerable<TypeDefinition> AllNested(TypeDefinition parent)
{
    foreach (var nt in parent.NestedTypes)
    {
        yield return nt;
        foreach (var nnt in AllNested(nt))
            yield return nnt;
    }
}
