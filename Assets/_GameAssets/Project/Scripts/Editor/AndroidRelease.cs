using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Google Play identity and the Android player build: an app bundle (.aab) signed with the upload key, for Play App
/// Signing. The key lives outside the repo in ~/.android-signing/walkapp/upload.properties (keystore path,
/// storePassword, keyAlias, keyPassword); back that folder up, because every update must be signed with it.
/// Batch mode: Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod AndroidRelease.BuildFromCommandLine
/// Then upload to internal testing: python Tools/play_release.py
/// </summary>
public static class AndroidRelease
{
    public const string PackageName = "se.ogun.walk";
    /// <summary>Name on the phone. Set only during the build: the Editor's save location follows the product name.</summary>
    public const string DisplayName = "Convoy Walk";
    public const string Version = "0.1.0";
    /// <summary>Play needs a higher code on every upload.</summary>
    public const int VersionCode = 1;
    public const string OutputPath = "Builds/Android/Walkapp.aab";

    private static string SigningFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android-signing", "walkapp", "upload.properties");

    [MenuItem("Tools/Walkapp/Release/Apply Android Release Settings")]
    public static void ApplySettings()
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = true;
        AssetDatabase.SaveAssets();
        Debug.Log($"Walkapp: Android release settings applied ({PackageName} {Version} ({VersionCode})).");
    }

    [MenuItem("Tools/Walkapp/Release/Build Android App Bundle")]
    public static void BuildAppBundle()
    {
        var report = Build();
        if (report != null && report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(OutputPath);
    }

    public static void BuildFromCommandLine()
    {
        var report = Build();
        EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static BuildReport Build()
    {
        var signing = ReadSigning();
        if (signing == null) return null;

        ApplySettings();

        string projectName = PlayerSettings.productName;
        PlayerSettings.productName = DisplayName;

        // The keystore path is machine specific and the passwords are secret, so they are set only for this build.
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = signing["keystore"];
        PlayerSettings.Android.keystorePass = signing["storePassword"];
        PlayerSettings.Android.keyaliasName = signing["keyAlias"];
        PlayerSettings.Android.keyaliasPass = signing["keyPassword"];
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"Walkapp: Android build {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024f * 1024f):F1} MB, output {Path.GetFullPath(OutputPath)}");
            return report;
        }
        finally
        {
            PlayerSettings.productName = projectName;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keyaliasPass = "";
            AssetDatabase.SaveAssets();
        }
    }

    private static Dictionary<string, string> ReadSigning()
    {
        if (!File.Exists(SigningFile))
        {
            Debug.LogError($"Walkapp: no upload key settings at {SigningFile}; the bundle can't be signed.");
            return null;
        }
        var values = File.ReadAllLines(SigningFile)
            .Select(line => line.Split(new[] { '=' }, 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
        foreach (var key in new[] { "keystore", "storePassword", "keyAlias", "keyPassword" })
        {
            if (values.ContainsKey(key)) continue;
            Debug.LogError($"Walkapp: {SigningFile} has no {key}.");
            return null;
        }
        return values;
    }
}
