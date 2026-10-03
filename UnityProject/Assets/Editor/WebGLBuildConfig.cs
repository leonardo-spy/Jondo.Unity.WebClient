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
