using Fusion;
using Fusion.Editor;
using Emberwake.Client;
using UnityEditor;
using UnityEngine;

public static class PrepareFusionBattle
{
    public static void Setup()
    {
        const string path = "Assets/_Game/Resources/FusionBattleWorld.prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
        {
            var obj = new GameObject("FusionBattleWorld", typeof(NetworkObject), typeof(FusionBattleWorld));
            PrefabUtility.SaveAsPrefabAsset(obj, path);
            Object.DestroyImmediate(obj);
        }
        AssetDatabase.SetLabels(AssetDatabase.LoadAssetAtPath<GameObject>(path), new[] { "FusionPrefab" });
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        NetworkProjectConfigUtilities.RebuildPrefabTable();
    }
}
