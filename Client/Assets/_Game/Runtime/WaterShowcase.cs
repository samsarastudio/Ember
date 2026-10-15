using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Emberwake.Core;
namespace Emberwake.Client
{
    // Offline deterministic visual QA entry point, only included in development builds/editor.
    public sealed class WaterShowcase : MonoBehaviour
    {
        ArenaView arena;BattleSnapshot snapshot;string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(!Debug.isDebugBuild && !Application.isEditor)return;
            foreach(var arg in Environment.GetCommandLineArgs())if(arg.StartsWith("--water-capture="))
            {
                var app=FindFirstObjectByType<EmberwakeApp>();if(app)app.enabled=false;
                new GameObject("Water visual audit").AddComponent<WaterShowcase>().output=arg.Substring(16);
            }
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);Application.targetFrameRate=60;
            var art=gameObject.AddComponent<FleetArt>();art.Init();arena=gameObject.AddComponent<ArenaView>();arena.Init(art);
            var deck=new Profile().deck;var sim=new MatchSimulation(Catalog.Default(),deck,deck);
            snapshot=sim.Snapshot(0,"water-audit","Test fleet");
            var list=new System.Collections.Generic.List<UnitState>(snapshot.units);
            for(int i=0;i<8;i++)list.Add(new UnitState{id=100+i,team=i%2,card="cutter",kind=i==0?"tank":"cutter",hp=500,maxHp=500,x=(i%2==0?-1:1)*(5.5f+i%3),z=-8+i*2});
            snapshot.units=list.ToArray();arena.Apply(snapshot,"default");
            yield return new WaitForSeconds(2);Capture("water-arena.png",720,1280);
            arena.Camera.orthographicSize=11;arena.Camera.transform.position=new Vector3(4,18,-13);arena.Camera.transform.LookAt(new Vector3(3,0,-1));
            arena.Water.Splash(new Vector3(4,0,-3),1.6f);
            yield return new WaitForSeconds(.3f);Capture("water-detail.png",1000,1000);
            yield return new WaitForSeconds(1);Capture("water-motion.png",1000,1000);
            arena.Water.SetRefraction(arena.Camera,true);yield return new WaitForSeconds(.3f);Capture("water-refraction.png",1000,1000);
            Debug.Log("WATER AUDIT COMPLETE: four frames, depth-only and refraction, moving fleet and impact pool.");Application.Quit();
        }
        void Update(){if(snapshot==null)return;foreach(var u in snapshot.units)if(!u.structure)u.z+=Time.deltaTime*(u.team==0?1:-1)*1.3f;arena.Apply(snapshot,"default");}
        void Capture(string file,int width,int height)
        {
            var rt=new RenderTexture(width,height,24);rt.Create();
            RenderPipeline.SubmitRenderRequest(arena.Camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(output,file),texture.EncodeToPNG());RenderTexture.active=previous;Destroy(texture);rt.Release();Destroy(rt);
        }
    }
}
