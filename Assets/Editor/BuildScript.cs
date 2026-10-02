using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Codemagic (CI) build giris noktalari. Unity'yi batch modda calistirip platform cikti uretir.
/// codemagic.yaml icinden: -executeMethod BuildScript.BuildIos / BuildAndroid ile cagrilir.
/// Editor assembly'de olmali (Assets/Editor/) cunku UnityEditor API kullaniyor.
/// </summary>
public static class BuildScript
{
    /// <summary>Build Settings'te ISARETLI (enabled) sahnelerin yollari — oyunun sahne sirasi burada.</summary>
    private static string[] EnabledScenes()
    {
        return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
    }

    private static void FailIfNotSucceeded(BuildReport report)
    {
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Build FAILED: {report.summary.result} ({report.summary.totalErrors} hata)");
            EditorApplication.Exit(1);
        }
        Debug.Log($"Build OK: {report.summary.outputPath}");
    }

    /// <summary>iOS: Unity bir Xcode projesi uretir (./ios). Codemagic sonra Xcode ile derler + imzalar + yukler.</summary>
    [MenuItem("Build/Build iOS (CI)")]
    public static void BuildIos()
    {
        var opts = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = "ios",          // Xcode projesi bu klasore uretilir
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None
        };
        FailIfNotSucceeded(BuildPipeline.BuildPlayer(opts));
    }

    /// <summary>Android AAB (opsiyonel; lokal build zaten var, CI icin alternatif).</summary>
    [MenuItem("Build/Build Android (CI)")]
    public static void BuildAndroid()
    {
        EditorUserBuildSettings.buildAppBundle = true;   // .aab (APK degil)
        var opts = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = "android/meowvivors.aab",
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };
        FailIfNotSucceeded(BuildPipeline.BuildPlayer(opts));
    }
}
