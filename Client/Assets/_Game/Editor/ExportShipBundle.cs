using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
public static class ExportShipBundle
{
    [MenuItem("Emberwake/Export selected ship prefab")]
    public static void Export(){
        var prefab=Selection.activeObject as GameObject;string assetPath=AssetDatabase.GetAssetPath(prefab);
        if(!prefab||!assetPath.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))throw new Exception("Select a saved ship prefab. Use a unit-scale root, bow pointing +Z, waterline Y=0.");
        string folder=EditorUtility.OpenFolderPanel("Export bundle for current build target","","");if(string.IsNullOrEmpty(folder))return;
        string name=prefab.name.ToLowerInvariant().Replace(" ","_")+".bundle";
        var manifest=BuildPipeline.BuildAssetBundles(folder,new[]{new AssetBundleBuild{assetBundleName=name,assetNames=new[]{assetPath},addressableNames=new[]{prefab.name}}},BuildAssetBundleOptions.ChunkBasedCompression,EditorUserBuildSettings.activeBuildTarget);
        if(!manifest)throw new Exception("Bundle export failed.");string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(folder,name)))).Replace("-","").ToLowerInvariant();
        File.WriteAllText(Path.Combine(folder,name+".json"),JsonUtility.ToJson(new BundleInfo{platform=EditorUserBuildSettings.activeBuildTarget.ToString(),bundleAsset=prefab.name,bundleSha256=hash,bundleUrl="https://YOUR-CDN/"+name},true));Debug.Log("Exported "+name+" with integrity manifest to "+folder);
    }
    [Serializable] class BundleInfo {public string platform,bundleAsset,bundleSha256,bundleUrl;}
}
