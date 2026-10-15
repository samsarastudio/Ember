using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using Emberwake.Core;

namespace Emberwake.Client
{
    public sealed class FleetArt : MonoBehaviour
    {
        public readonly Dictionary<string,Sprite> Cards=new Dictionary<string,Sprite>();
        public readonly Dictionary<string,GameObject> Models=new Dictionary<string,GameObject>();
        readonly List<AssetBundle> bundles=new List<AssetBundle>(); public Sprite[] Atlas;Coroutine loading;
        public void Reload(Catalog catalog){if(loading!=null)StopCoroutine(loading);loading=StartCoroutine(LoadContent(catalog));}
        public void Init(){var texture=Resources.Load<Texture2D>("Art/card-atlas");Atlas=new Sprite[12];for(int i=0;i<12;i++){int row=i/4;float y=row==0?724:row==1?393:0;float h=row==0?362:row==1?331:393;Atlas[i]=Sprite.Create(texture,new Rect((i%4*362+8)*texture.width/1448f,(y+8)*texture.height/1086f,346*texture.width/1448f,(h-16)*texture.height/1086f),new Vector2(.5f,.5f));}}
        public Sprite Card(CardDef c)=>Cards.TryGetValue(c.id,out var image)?image:Atlas[Mathf.Clamp(c.artIndex,0,11)];
        public IEnumerator LoadContent(Catalog catalog)
        {
            foreach(var loaded in bundles)loaded.Unload(false);bundles.Clear();Models.Clear();Cards.Clear();
            foreach(var c in catalog.cards){
                string platform=Application.platform==RuntimePlatform.Android?"Android":Application.platform==RuntimePlatform.IPhonePlayer?"iOS":"Windows";
                var platformBundle=c.modelBundles==null?null:Array.Find(c.modelBundles,b=>b.platform==platform);
                string bundleUrl=platformBundle?.url??c.bundleUrl,bundleSha256=platformBundle?.sha256??c.bundleSha256,bundleAsset=platformBundle?.asset??c.bundleAsset;
                if(!string.IsNullOrEmpty(c.artUrl))using(var r=UnityWebRequestTexture.GetTexture(c.artUrl)){r.timeout=20;yield return r.SendWebRequest();if(r.result==UnityWebRequest.Result.Success){var t=DownloadHandlerTexture.GetContent(r);Cards[c.id]=Sprite.Create(t,new Rect(0,0,t.width,t.height),Vector2.one*.5f);}}
                if(string.IsNullOrEmpty(bundleUrl))continue;
                string path=Path.Combine(Application.persistentDataPath,bundleSha256.ToLowerInvariant()+".bundle");
                if(!File.Exists(path))using(var r=UnityWebRequest.Get(bundleUrl)){r.timeout=60;yield return r.SendWebRequest();if(r.result!=UnityWebRequest.Result.Success)continue;var bytes=r.downloadHandler.data;if(bytes.Length>64*1024*1024)continue;using(var hash=SHA256.Create()){if(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!=bundleSha256.ToLowerInvariant())continue;}File.WriteAllBytes(path,bytes);}
                byte[] cached=File.ReadAllBytes(path);using(var hash=SHA256.Create()){if(BitConverter.ToString(hash.ComputeHash(cached)).Replace("-","").ToLowerInvariant()!=bundleSha256.ToLowerInvariant()){File.Delete(path);continue;}}
                var request=AssetBundle.LoadFromFileAsync(path);yield return request;if(request.assetBundle==null)continue;bundles.Add(request.assetBundle);var asset=request.assetBundle.LoadAssetAsync<GameObject>(bundleAsset);yield return asset;if(asset.asset is GameObject go)Models[c.id]=go;
            }
        }
        void OnDestroy(){foreach(var b in bundles)b.Unload(false);}
    }
    public static class ToyFactory
    {
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        public static Material Mat(Color color,bool emission=false){string k=color.ToString()+emission;if(materials.TryGetValue(k,out var m))return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.15f);if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);}materials[k]=m;return m;}
        public static GameObject Part(Transform p,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color){var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=Mat(color);UnityEngine.Object.Destroy(g.GetComponent<Collider>());return g;}
        public static GameObject Ship(string kind,int team,string cosmetic="default"){
            var root=new GameObject(kind);var t=root.transform;Color wood=new Color(.25f,.105f,.055f),deck=new Color(.6f,.32f,.13f),sail=team==1?new Color(1,.38f,.25f):new Color(.98f,.89f,.65f);
            if(team==0){if(cosmetic=="midnight")sail=new Color(.22f,.24f,.46f);if(cosmetic=="coral")sail=new Color(1,.42f,.4f);if(cosmetic=="copper")sail=new Color(.77f,.43f,.2f);}
            float length=kind=="tank"?2.5f:kind=="swarm"?1.1f:1.8f;float width=kind=="tank"?1.3f:.85f;
            Part(t,"Keel",PrimitiveType.Cube,new Vector3(0,.22f,0),new Vector3(width,.38f,length),wood);
            var bow=Part(t,"Bow",PrimitiveType.Cube,new Vector3(0,.24f,length*.42f),new Vector3(width*.7f,.36f,width*.7f),wood);bow.transform.localRotation=Quaternion.Euler(0,45,0);
            Part(t,"Deck",PrimitiveType.Cube,new Vector3(0,.44f,0),new Vector3(width*.88f,.14f,length*.82f),deck);
            Part(t,"Mast",PrimitiveType.Cylinder,new Vector3(0,1.12f,0),new Vector3(.1f,.7f,.1f),wood);
            Part(t,"Sail",PrimitiveType.Cube,new Vector3(0,1.5f,.03f),new Vector3(width*1.2f,.8f,.08f),sail);
            var gun=Part(t,"Cannon",PrimitiveType.Cylinder,new Vector3(.3f,.7f,.5f),new Vector3(.25f,.38f,.25f),new Color(.14f,.2f,.22f));gun.transform.localRotation=Quaternion.Euler(65,0,0);
            if(kind=="tank")Part(t,"Armor",PrimitiveType.Cube,new Vector3(0,.65f,-.5f),new Vector3(1.32f,.6f,.7f),new Color(.23f,.31f,.34f));
            if(kind=="ram")Part(t,"Bronze ram",PrimitiveType.Cube,new Vector3(0,.37f,1.35f),new Vector3(.32f,.25f,.9f),new Color(.92f,.63f,.2f));
            if(kind=="mortar"||kind=="bomb")Part(t,"Mortar",PrimitiveType.Cylinder,new Vector3(0,.7f,.5f),new Vector3(.65f,.3f,.65f),Color.gray);
            if(kind=="repair"){Part(t,"Repair cross",PrimitiveType.Cube,new Vector3(0,1.5f,-.03f),new Vector3(.5f,.13f,.1f),new Color(.1f,.65f,.5f));Part(t,"Repair cross",PrimitiveType.Cube,new Vector3(0,1.5f,-.03f),new Vector3(.13f,.5f,.1f),new Color(.1f,.65f,.5f));}
            return root;
        }
        public static GameObject Base(bool hq,int team){var g=new GameObject(hq?"Flagship harbor":"Cannon outpost");Color trim=team==0?new Color(.15f,.66f,.72f):new Color(.95f,.3f,.2f);Part(g.transform,"Island",PrimitiveType.Cylinder,Vector3.zero,new Vector3(hq?4:3,.4f,hq?4:3),new Color(.19f,.23f,.24f));Part(g.transform,"Fort",PrimitiveType.Cube,new Vector3(0,1,0),new Vector3(hq?2.7f:1.8f,1.7f,hq?2.3f:1.6f),new Color(.55f,.4f,.24f));Part(g.transform,"Roof",PrimitiveType.Cube,new Vector3(0,2,0),new Vector3(hq?3:2.1f,.35f,hq?2.6f:1.9f),trim);var cannon=Part(g.transform,"Cannon",PrimitiveType.Cylinder,new Vector3(0,2.4f,.5f),new Vector3(.55f,.8f,.55f),new Color(.14f,.18f,.2f));cannon.transform.localRotation=Quaternion.Euler(70,0,0);return g;}
    }
}
