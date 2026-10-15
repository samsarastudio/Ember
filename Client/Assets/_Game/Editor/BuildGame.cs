using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Emberwake.Client;
public static class BuildGame
{
    public static void Setup(){
        PrepareFusionBattle.Setup();
        Directory.CreateDirectory("Assets/_Game/Settings");Directory.CreateDirectory("Assets/_Game/Scenes");
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Game/Settings/MobileURP.asset");
        if(!pipeline){var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/_Game/Settings/Renderer.asset");pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.msaaSampleCount=4;pipeline.shadowDistance=80;AssetDatabase.CreateAsset(pipeline,"Assets/_Game/Settings/MobileURP.asset");}
        var rendererData=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Game/Settings/Renderer.asset");rendererData.copyDepthMode=CopyDepthMode.AfterOpaques;EditorUtility.SetDirty(rendererData);GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;
        pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=false;EditorUtility.SetDirty(pipeline);pipeline.mainLightShadowmapResolution=2048;pipeline.shadowCascadeCount=2;PlayerSettings.colorSpace=ColorSpace.Linear;
        int jobs=int.TryParse(System.Environment.GetEnvironmentVariable("EMBERWAKE_BUILD_JOBS"),out int requestedJobs)?Mathf.Clamp(requestedJobs,1,8):2;
        PlayerSettings.SetAdditionalIl2CppArgs($"--jobs={jobs} --bee-jobs={jobs}");
        PlayerSettings.companyName="Emberwake Studio";PlayerSettings.productName="Emberwake Fleet Duel";PlayerSettings.bundleVersion="0.1.0";PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.emberwake.fleetduel");
        PlayerSettings.defaultScreenWidth=540;PlayerSettings.defaultScreenHeight=960;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
        var serialized=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var input=serialized.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;serialized.ApplyModifiedPropertiesWithoutUndo();}
        var atlas=AssetImporter.GetAtPath("Assets/_Game/Resources/Art/card-atlas.png") as TextureImporter;if(atlas){atlas.textureType=TextureImporterType.Default;atlas.npotScale=TextureImporterNPOTScale.None;atlas.maxTextureSize=2048;atlas.textureCompression=TextureImporterCompression.Compressed;atlas.SaveAndReimport();}
        if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Resources/ShipMaterial.mat"))AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")),"Assets/_Game/Resources/ShipMaterial.mat");
        if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Resources/Water/Sea.mat"))AssetDatabase.CreateAsset(new Material(Shader.Find("Emberwake/Stylized Sea")),"Assets/_Game/Resources/Water/Sea.mat");
        if(!AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Game/Resources/NavalGrade.asset")){var grade=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(grade,"Assets/_Game/Resources/NavalGrade.asset");var tone=grade.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);AssetDatabase.AddObjectToAsset(tone,grade);var adjustment=grade.Add<ColorAdjustments>();adjustment.saturation.Override(-8);AssetDatabase.AddObjectToAsset(adjustment,grade);EditorUtility.SetDirty(grade);}
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Resources/Art/app-icon.png");if(icon)PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown,new[]{icon},IconKind.Any);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Emberwake",typeof(EmberwakeApp));EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/Boot.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/_Game/Scenes/Boot.unity",true)};AssetDatabase.SaveAssets();
    }
    public static void Windows(){Setup();Build("../Builds/Windows/Emberwake.exe",BuildTarget.StandaloneWindows64);}
    public static void Android(){Setup();EditorUserBuildSettings.buildAppBundle=false;Build("../Builds/Android/Emberwake-preview.apk",BuildTarget.Android);}
    static void ReleaseSettings()
    {
        PlayerSettings.bundleVersion="0.2.0";
        int number=int.TryParse(System.Environment.GetEnvironmentVariable("EMBERWAKE_BUILD_NUMBER"),out var n)?System.Math.Max(2,n):2;
        PlayerSettings.Android.bundleVersionCode=number;
        PlayerSettings.iOS.buildNumber=number.ToString();
        PlayerSettings.insecureHttpOption=InsecureHttpOption.NotAllowed;
    }
    public static void WindowsRelease(){Setup();ReleaseSettings();Build("../Builds/Release/Windows/Emberwake.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);}
    public static void AndroidRelease(){Setup();ReleaseSettings();EditorUserBuildSettings.buildAppBundle=false;Build("../Builds/Release/Android/Emberwake.apk",BuildTarget.Android,BuildOptions.None);}
    public static void IOS()
    {
        Setup();ReleaseSettings();
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS,"com.emberwake.fleetduel");
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS,ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.targetOSVersionString="15.0";
        PlayerSettings.iOS.sdkVersion=iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.appleEnableAutomaticSigning=false;
        Build("../Builds/iOS",BuildTarget.iOS,BuildOptions.None);
    }
    public static void Both(){Android();Windows();}
    static void Build(string path,BuildTarget target,BuildOptions options=BuildOptions.Development){Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/_Game/Scenes/Boot.unity"},locationPathName=path,target=target,options=options});if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Build failed: "+report.summary.result);}
}


