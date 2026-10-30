using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class WebGLBuildConfig : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.WebGL,
            ManagedStrippingLevel.Medium);

        PlayerSettings.stripEngineCode = false;

        PlayerSettings.SetIl2CppCompilerConfiguration(
            NamedBuildTarget.WebGL,
            Il2CppCompilerConfiguration.Debug);

        PlayerSettings.SetIl2CppCodeGeneration(
            NamedBuildTarget.WebGL,
            Il2CppCodeGeneration.OptimizeSize);

        PlayerSettings.SetAdditionalIl2CppArgs(
            "--entry-assembly-name=Core"
        );

        Debug.Log(
            "[WebGLBuildConfig] WebGL: Medium stripping, no engine strip, Debug IL2CPP, entry assembly = Core"
        );
    }
}

public static class WebGLBuildMenu
{
    [System.Serializable]
    sealed class ExpectedBootLogic
    {
        public ExpectedBootPhase[] phases;
    }

    [System.Serializable]
    sealed class ExpectedBootPhase
    {
        public string name;
        public ExpectedBootService[] serviceInits;
    }

    [System.Serializable]
    sealed class ExpectedBootService
    {
        public string type;
        public string method;
    }

    [MenuItem("Build/Validate Boot Logic")]
    public static void ValidateBootLogic()
    {
        var resource = Resources.Load<TextAsset>("BootLogic");
        if (resource == null) throw new System.InvalidOperationException("BootLogic resource is missing");
        var expected = JsonUtility.FromJson<ExpectedBootLogic>(resource.text);
        if (expected?.phases == null || expected.phases.Length == 0)
            throw new System.InvalidOperationException("BootLogic has no phases");
        int serviceCount = 0;
        foreach (var phase in expected.phases)
        {
            if (phase == null || string.IsNullOrEmpty(phase.name))
                throw new System.InvalidOperationException("BootLogic contains an unnamed phase");
            if (phase.serviceInits == null) continue;
            foreach (var service in phase.serviceInits)
            {
                if (service == null || string.IsNullOrEmpty(service.type) || string.IsNullOrEmpty(service.method))
                    throw new System.InvalidOperationException("BootLogic contains an incomplete initializer");
                serviceCount++;
            }
        }
        int load = System.Array.FindIndex(expected.phases, phase => phase.name == "LoadDataDependencies");
        int preInit = System.Array.FindIndex(expected.phases, phase => phase.name == "PreInitServices");
        int init = System.Array.FindIndex(expected.phases, phase => phase.name == "InitializeServices");
        if (load < 0 || preInit <= load || init <= preInit)
            throw new System.InvalidOperationException("BootLogic must load dependencies before pre-init and init");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var loader = typeof(WebGLProbe).GetMethod("LoadBootLogic", flags);
        if (loader == null) throw new System.InvalidOperationException("Probe boot loader was not found");
        loader.Invoke(null, null);
        int loadedPhases = (int)typeof(WebGLProbe).GetField("_bootPhaseCount", flags).GetValue(null);
        int loadedServices = (int)typeof(WebGLProbe).GetField("_bootServiceCount", flags).GetValue(null);
        if (loadedPhases != expected.phases.Length || loadedServices != serviceCount)
            throw new System.InvalidOperationException($"Probe boot counts differ: {loadedPhases}/{loadedServices}");
        ValidateCentralRegistrations();
        ValidateReconstructedServices();
        ValidateComposedMapPayloads();
        ValidateCellGridAndPicking();
        Debug.Log($"[BootValidation] PASS phases={loadedPhases} candidateInitializers={loadedServices}; runtime service coverage is not verified");
    }

    static void ValidateReconstructedServices()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var services = (System.Array)typeof(WebGLProbe).GetField("_reconstructedServices", flags).GetValue(null);
        if (services == null || services.Length != 239)
            throw new System.InvalidOperationException($"Reconstructed service count differs: {services?.Length ?? 0} expected 239");
        int instantiated = 0;
        int fields = 0;
        foreach (var s in services)
        {
            var stype = (string)s.GetType().GetField("sourceType").GetValue(s);
            if (stype == "instantiated") instantiated++;
            else if (stype == "field") fields++;
        }
        if (instantiated != 236 || fields != 3)
            throw new System.InvalidOperationException($"Reconstructed service source type mismatch: {instantiated} instantiated, {fields} fields");
        Debug.Log($"[ServiceCatalogValidation] PASS total={services.Length} instantiated={instantiated} fields={fields}; service catalog verified");
    }

    static void ValidateCentralRegistrations()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var bindings = (System.Array)typeof(WebGLProbe).GetField("_centralRegistrations", flags).GetValue(null);
        if (bindings == null || bindings.Length == 0) throw new System.InvalidOperationException("No extracted central bindings");
        System.Reflection.Assembly.LoadFrom(System.IO.Path.Combine(Application.dataPath, "Plugins", "Core.dll"));
        var findType = typeof(WebGLProbe).GetMethod("FindType", flags);
        var compositorType = (System.Type)findType.Invoke(null, new object[] { "Core.Engine.Compositor.GameCompositor" });
        if (compositorType == null) throw new System.InvalidOperationException("GameCompositor type was not found");
        var validate = typeof(WebGLProbe).GetMethod("ValidateCentralRegistration", flags);
        foreach (var binding in bindings) validate.Invoke(null, new object[] { binding, compositorType });
        var register = typeof(WebGLProbe).GetMethod("RegisterInDict", flags);
        var dictionary = new System.Collections.Generic.Dictionary<System.Type, object>();
        using (var original = new System.IO.MemoryStream())
        using (var replacement = new System.IO.MemoryStream())
        {
            register.Invoke(null, new object[] { dictionary, typeof(System.IDisposable), original, "fixture" });
            bool rejected = false;
            try { register.Invoke(null, new object[] { dictionary, typeof(System.IDisposable), replacement, "duplicate-fixture" }); }
            catch (System.Reflection.TargetInvocationException exception)
            {
                if (exception.InnerException is System.InvalidOperationException) rejected = true;
                else throw;
            }
            if (!rejected || !object.ReferenceEquals(original, dictionary[typeof(System.IDisposable)]))
                throw new System.InvalidOperationException("Duplicate registration overwrote the original instance");
        }
        Debug.Log($"[CentralRegistrationValidation] PASS bindings={bindings.Length}; original field/interface types compatible; duplicate overwrite rejected");
    }

    static void ValidateComposedMapPayloads()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var parser = typeof(WebGLProbe).GetMethod("ParseComposedMapData", flags);
        if (parser == null) throw new System.InvalidOperationException("Composed map parser was not found");
        var valid = "{\"schemaVersion\":2,\"mapId\":1099511627776,\"background\":{\"positions\":[0,0,1,0,0,1],\"uvs\":[0,0,1,0,0,1],\"colors\":[255,255,255,255,255,255,255,255,255,255,255,255],\"indices\":[0,1,2]}}";
        var data = parser.Invoke(null, new object[] { valid });
        long mapId = (long)data.GetType().GetField("mapId").GetValue(data);
        if (mapId != 1099511627776L) throw new System.InvalidOperationException("Map ID lost 64-bit precision");
        foreach (var invalid in new[] { valid.Replace("[0,1,2]", "[0,1,3]"), valid.Replace("[0,0,1,0,0,1]", "[0]") })
        {
            bool rejected = false;
            try { parser.Invoke(null, new object[] { invalid }); }
            catch (System.Reflection.TargetInvocationException exception)
            {
                if (exception.InnerException is System.InvalidOperationException) rejected = true;
                else throw;
            }
            if (!rejected) throw new System.InvalidOperationException("Malformed map buffers were accepted");
        }
        var client = System.Environment.GetEnvironmentVariable("DOFUS_CLIENT_DIR");
        if (string.IsNullOrEmpty(client))
            client = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).Parent.FullName, "Cliente 3.6.10.11");
        var directory = System.IO.Path.Combine(client, "Dofus_Data", "StreamingAssets", "Content", "Map", "Composed");
        int maps = 0;
        foreach (var path in System.IO.Directory.GetFiles(directory, "*_render.json"))
        {
            parser.Invoke(null, new object[] { System.IO.File.ReadAllText(path) });
            maps++;
        }
        if (maps == 0) throw new System.InvalidOperationException("No composed map payload was exercised");
        Debug.Log($"[MapPayloadValidation] PASS maps={maps}; malformed buffers rejected; map IDs preserve 64-bit precision; visual fidelity is not verified");
    }

    static void ValidateCellGridAndPicking()
    {
        var resource = Resources.Load<TextAsset>("CellGrid560");
        if (resource == null) throw new System.InvalidOperationException("CellGrid560 resource is missing");

        WebGLProbe.IsometricGrid.EnsureTable();
        for (int cellId = 0; cellId < 560; cellId++)
        {
            var coords = WebGLProbe.IsometricGrid.CellIdToCoords(cellId);
            int recovered = WebGLProbe.IsometricGrid.CoordsToCellId(coords.x, coords.y);
            if (recovered != cellId)
                throw new System.InvalidOperationException($"Isometric round-trip failed for cell {cellId}: ({coords.x}, {coords.y}) -> {recovered}");

            var worldPos = WebGLProbe.IsometricGrid.CellIdToWorldPos(cellId);
            int picked = WebGLProbe.IsometricGrid.WorldPosToCellId(worldPos);
            if (picked != cellId)
                throw new System.InvalidOperationException($"Isometric picking failed for cell {cellId}: world ({worldPos.x}, {worldPos.y}) -> {picked}");
        }

        // Test pathfinding with mock cells
        var mockCells = new WebGLProbe.CellData[560];
        for (int i = 0; i < 560; i++)
        {
            mockCells[i] = new WebGLProbe.CellData { cellNumber = (uint)i, mov = true };
        }
        // Place obstacle at cell 159 (11, 0)
        int obs = WebGLProbe.IsometricGrid.CoordsToCellId(11, 0);
        mockCells[obs].mov = false;

        int start = WebGLProbe.IsometricGrid.CoordsToCellId(10, 0);
        int target = WebGLProbe.IsometricGrid.CoordsToCellId(12, 0);
        var path = WebGLProbe.IsometricGrid.FindPath(start, target, mockCells);
        if (path == null || path.Count < 2 || path[0] != start || path[path.Count - 1] != target || path.Contains(obs))
            throw new System.InvalidOperationException("A* pathfinding failed obstacle test");

        Debug.Log($"[CellGridValidation] PASS total=560 roundTrip=560 picking=560 pathfindingSteps={path.Count}");
    }

    [MenuItem("Build/Build WebGL")]
    public static void BuildWebGL()
    {
        var scenes = new[] { "Assets/Scenes/DofusUnity.unity" };
        // Discover JondoEmu repo via environment or known sibling
        var jondoRoot = System.Environment.GetEnvironmentVariable("JONDO_ROOT");
        if (string.IsNullOrEmpty(jondoRoot))
            jondoRoot = System.IO.Path.Combine(System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.MyDocuments), "Projetos", "JondoEmu");
        var output = System.IO.Path.Combine(jondoRoot, "PlayBrowser", "WebClient", "unity-build");
        UnityEngine.Debug.Log($"[WebGLBuild] Building to: {output}");
        var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.WebGL, BuildOptions.None);
        UnityEngine.Debug.Log($"[WebGLBuild] Result: {report.summary.result} ({report.summary.totalTime})");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            UnityEngine.Debug.LogError($"[WebGLBuild] FAILED: {report.summary.totalErrors} errors");
    }

    [MenuItem("Build/Apply WebGL Settings")]
    public static void ApplySettings()
    {
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.WebGL,
            ManagedStrippingLevel.Medium);

        PlayerSettings.stripEngineCode = false;

        PlayerSettings.SetIl2CppCompilerConfiguration(
            NamedBuildTarget.WebGL,
            Il2CppCompilerConfiguration.Debug);

        PlayerSettings.SetAdditionalIl2CppArgs(
            "--entry-assembly-name=Core"
        );

        Debug.Log(
            "[WebGLBuildConfig] Settings applied: entry assembly = Core"
        );
    }
}
