using System;
using System.IO;
using Emberwake.Client;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildFusionProbe
{
    public static void Windows()
    {
        const string scenePath = "Assets/_Game/Scenes/FusionProbe.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Fusion connectivity probe", typeof(FusionConnectionProbe));
        EditorSceneManager.SaveScene(scene, scenePath);
        Directory.CreateDirectory("../Builds/FusionProbe");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = "../Builds/FusionProbe/FusionProbe.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Fusion probe build failed: " + report.summary.result);
    }
}
