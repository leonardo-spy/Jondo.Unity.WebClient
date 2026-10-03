// Quick type checker - run with: dotnet script CheckTypes.cs <pluginsDir>
using Mono.Cecil;
var dir = args.Length > 0 ? args[0] : @"C:\DofusWeb\Assets\Plugins";
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(dir);
var rp = new ReaderParameters { ReadingMode = ReadingMode.Deferred, AssemblyResolver = resolver, ReadSymbols = false };

var corePath = System.IO.Path.Combine(dir, "Core.dll");
var bytes = System.IO.File.ReadAllBytes(corePath);
using var dll = AssemblyDefinition.ReadAssembly(new System.IO.MemoryStream(bytes), rp);

// Search for glo
var glo = dll.MainModule.Types.FirstOrDefault(t => t.Name == "glo");
if (glo != null) Console.WriteLine($"FOUND glo: {glo.FullName} Public={glo.IsPublic}");
else Console.WriteLine("glo NOT FOUND in Core.dll types");

// Search for ecr, fyh, ezp
foreach (var name in new[] { "ecr", "fyh", "ezp", "glo" }) {
    var t = dll.MainModule.Types.FirstOrDefault(x => x.Name == name);
    Console.WriteLine($"  {name}: {(t != null ? $"FOUND Public={t.IsPublic} Abstract={t.IsAbstract} Interface={t.IsInterface}" : "NOT FOUND")}");
}

// Show some short-name types
var shorts = dll.MainModule.Types.Where(t => t.Namespace == "" && t.Name.Length <= 3).OrderBy(t => t.Name).Take(50);
Console.WriteLine($"\nShort types (<=3 chars) in global ns ({shorts.Count()}): {string.Join(", ", shorts.Select(t => t.Name))}");
Console.WriteLine($"Total types in Core.dll: {dll.MainModule.Types.Count}");
