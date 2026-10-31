using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Inspector
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2) return;
            string dllPath = args[0];
            string target = args[1];

            using var fs = File.OpenRead(dllPath);
            using var pe = new PEReader(fs);
            var mr = pe.GetMetadataReader();

            foreach (var th in mr.TypeDefinitions)
            {
                var t = mr.GetTypeDefinition(th);
                string ns = mr.GetString(t.Namespace);
                string name = mr.GetString(t.Name);
                string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

                if (name.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                    fullName.Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"=== Type: {fullName} ===");
                    Console.WriteLine("Properties:");
                    foreach (var ph in t.GetProperties())
                    {
                        var p = mr.GetPropertyDefinition(ph);
                        Console.WriteLine($"  {mr.GetString(p.Name)}");
                    }
                    Console.WriteLine("Fields:");
                    foreach (var fh in t.GetFields())
                    {
                        var f = mr.GetFieldDefinition(fh);
                        Console.WriteLine($"  {mr.GetString(f.Name)}");
                    }
                    return;
                }
            }
        }
    }
}
