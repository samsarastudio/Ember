using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Emberwake.Core;
namespace Emberwake.Client
{
    public sealed class ArenaView : MonoBehaviour
    {
        public GameObject Root; public Camera Camera;public bool Battle;FleetArt art;Transform actors;int team;public StylizedWater Water;readonly HashSet<int> movingShips=new HashSet<int>();
        readonly Dictionary<int,GameObject> units=new Dictionary<int,GameObject>();readonly Dictionary<int,Vector3> positions=new Dictionary<int,Vector3>();readonly Dictionary<int,GameObject> shots=new Dictionary<int,GameObject>();readonly Dictionary<int,GameObject> zones=new Dictionary<int,GameObject>();
        readonly Dictionary<int,Vector3> shotFrom=new Dictionary<int,Vector3>(),shotTo=new Dictionary<int,Vector3>();
        float shotUpdated,shotInterval=1f/30f;int lastEvent;
        public void Init(FleetArt library){art=library;Root=new GameObject("Ember Shoals Arena");actors=new GameObject("Fleet").transform;actors.SetParent(Root.transform);var cam=new GameObject("Arena camera",typeof(Camera));Camera=cam.GetComponent<Camera>();Camera.orthographic=true;Camera.orthographicSize=24;Camera.backgroundColor=new Color(.025f,.28f,.31f);Camera.clearFlags=CameraClearFlags.SolidColor;Camera.nearClipPlane=.1f;Camera.farClipPlane=150;SetCamera(0);Camera.allowHDR=true;
            var light=new GameObject("Warm afternoon sun",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.88f,.7f);light.intensity=1.2f;light.shadows=LightShadows.Soft;light.shadowStrength=.65f;light.transform.rotation=Quaternion.Euler(55,-35,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.53f,.68f,.73f);
            Camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;var volume=new GameObject("Warm naval grade",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=Resources.Load<VolumeProfile>("NavalGrade");
            Water=Root.AddComponent<StylizedWater>();
            var random=new System.Random(18);for(int i=0;i<42;i++){float x=i<21?-13.5f:13.5f,z=-23+i%21*2.2f;Rock(new Vector3(x+(float)random.NextDouble()*2,0,z),1.8f+(float)random.NextDouble()*1.5f);}
            for(int i=-4;i<=4;i++)Rock(new Vector3(i*1.1f,0,0),1.05f);for(int i=0;i<2;i++){Rock(new Vector3(i==0?-11:11,0,0),1.7f);}
            foreach(float z in new[]{-10f,10f}){Water.AddShore(new Vector3(-7,0,z),1.8f);Water.AddShore(new Vector3(7,0,z),1.8f);}Water.AddShore(new Vector3(0,0,-15),2.3f);Water.AddShore(new Vector3(0,0,15),2.3f);Water.Initialize(Camera);
        }
        void Rock(Vector3 at,float size){Water.AddShore(at,size*.65f);var r=ToyFactory.Part(Root.transform,"Basalt",PrimitiveType.Cube,at,new Vector3(size,size*.7f,size*.9f),new Color(.12f,.17f,.18f));r.transform.rotation=Quaternion.Euler(5,at.z*23,8);var fissure=ToyFactory.Part(r.transform,"Ember fissure",PrimitiveType.Cube,new Vector3(.05f,.51f,0),new Vector3(.06f,.04f,.8f),new Color(1,.34f,.035f));fissure.GetComponent<Renderer>().sharedMaterial=ToyFactory.Mat(new Color(1,.3f,.03f),true);}
        void SetCamera(int t){team=t;Camera.transform.position=new Vector3(0,40,t==0?-26:26);Camera.transform.LookAt(new Vector3(0,0,t==0?-1:1));}
        public void DeploymentFeedback(float x,float z){var cue=ToyFactory.Part(actors,"Pending deployment",PrimitiveType.Cylinder,new Vector3(x,.18f,z),new Vector3(1.4f,.035f,1.4f),new Color(1,.78f,.22f));cue.AddComponent<CombatCue>().Init(.55f);}
        public void Reset(){foreach(Transform c in actors)Destroy(c.gameObject);units.Clear();positions.Clear();shots.Clear();shotFrom.Clear();shotTo.Clear();zones.Clear();movingShips.Clear();lastEvent=0;shotUpdated=Time.time;Water.ClearEffects();}
        void ShowEvents(BattleSnapshot s)
        {
            foreach(var e in s.events??System.Array.Empty<CombatEvent>())
            {
                if(e.id<=lastEvent)continue;
                lastEvent=e.id;
                if(s.tick-e.tick>30)continue;
                var from=new Vector3(e.x,.9f,e.z);var to=new Vector3(e.toX,.9f,e.toZ);
                if(e.kind==2){Water.Splash(to,1);continue;}
                if(e.kind==5)continue;
                if(e.kind==3)
                {
                    var beam=ToyFactory.Part(actors,"Repair pulse",PrimitiveType.Cube,(from+to)*.5f,new Vector3(.12f,.12f,Vector3.Distance(from,to)),new Color(.2f,1,.65f));
                    if((to-from).sqrMagnitude>.001f)beam.transform.rotation=Quaternion.LookRotation(to-from);
                    beam.AddComponent<CombatCue>().Init(.3f);
                }
                else
                {
                    var flash=ToyFactory.Part(actors,e.kind==4?"Ram impact":"Cannon flash",PrimitiveType.Sphere,e.kind==4?to:from,Vector3.one*(e.kind==4?.8f:.5f),new Color(1,.75f,.2f));
                    flash.AddComponent<CombatCue>().Init(.18f);
                    if(e.kind==4)Water.Splash(to,1.4f);
                }
            }
        }
        public void Apply(BattleSnapshot s,string cosmetic){ShowEvents(s);shotInterval=Mathf.Clamp(Time.time-shotUpdated,1f/60f,.15f);shotUpdated=Time.time;if(team!=s.team)SetCamera(s.team);var alive=new HashSet<int>();foreach(var u in s.units){if(u.hp<=0)continue;alive.Add(u.id);if(!units.TryGetValue(u.id,out var obj)){if(u.structure)obj=ToyFactory.Base(u.hq,u.team==s.team?0:1);else if(art.Models.TryGetValue(u.card,out var prefab))obj=Instantiate(prefab);else obj=ToyFactory.Ship(u.kind,u.team==s.team?0:1,cosmetic);obj.transform.SetParent(actors);obj.transform.position=new Vector3(u.x,0,u.z);obj.transform.rotation=Quaternion.Euler(0,u.team==0?0:180,0);var bar=ToyFactory.Part(obj.transform,"Health",PrimitiveType.Cube,new Vector3(0,3,0),new Vector3(1.6f,.09f,.16f),u.team==s.team?new Color(.2f,1,.7f):new Color(1,.3f,.2f));units[u.id]=obj;if(!u.structure && u.kind!="battery")movingShips.Add(u.id);}positions[u.id]=new Vector3(u.x,0,u.z);var health=obj.transform.Find("Health");if(health)health.localScale=new Vector3(1.6f*u.hp/u.maxHp,.09f,.16f);}
            foreach(var id in new List<int>(units.Keys))if(!alive.Contains(id)){Water.Splash(units[id].transform.position,1.2f);Water.ForgetShip(id);movingShips.Remove(id);Destroy(units[id]);units.Remove(id);positions.Remove(id);}var activeShots=new HashSet<int>();foreach(var shot in s.shots){activeShots.Add(shot.id);if(!shots.TryGetValue(shot.id,out var obj)){obj=ToyFactory.Part(actors,"Cannonball",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.25f,new Color(1,.65f,.15f));shots[shot.id]=obj;obj.transform.position=new Vector3(shot.x,.9f,shot.z);}shotFrom[shot.id]=obj.transform.position;shotTo[shot.id]=new Vector3(shot.x,.9f,shot.z);}foreach(var id in new List<int>(shots.Keys))if(!activeShots.Contains(id)){if(s.events==null)Water.Splash(shots[id].transform.position,.65f);Destroy(shots[id]);shots.Remove(id);shotFrom.Remove(id);shotTo.Remove(id);}var activeZones=new HashSet<int>();foreach(var z in s.zones){activeZones.Add(z.id);if(!zones.TryGetValue(z.id,out var obj)){if(z.kind=="grapeshot"){obj=new GameObject("Grapeshot volley");obj.transform.SetParent(actors);obj.AddComponent<GrapeshotFx>().Init(z,s.time,Water);}else obj=ToyFactory.Part(actors,z.kind,PrimitiveType.Cylinder,Vector3.zero,new Vector3(z.radius*2,.02f,z.radius*2),z.kind=="smoke"?new Color(.6f,.67f,.7f):new Color(.4f,.9f,.78f));zones[z.id]=obj;}obj.transform.position=new Vector3(z.x,.06f,z.z);}foreach(var id in new List<int>(zones.Keys))if(!activeZones.Contains(id)){Destroy(zones[id]);zones.Remove(id);}}
        void LateUpdate(){foreach(var shot in shots)shot.Value.transform.position=Vector3.Lerp(shotFrom[shot.Key],shotTo[shot.Key],Mathf.Clamp01((Time.time-shotUpdated)/shotInterval));foreach(var pair in units){var obj=pair.Value;if(movingShips.Contains(pair.Key))Water.TrackShip(pair.Key,obj.transform.position,.85f);var target=positions[pair.Key];var delta=target-obj.transform.position;if(delta.sqrMagnitude>.001f){obj.transform.position=Vector3.Lerp(obj.transform.position,target,1-Mathf.Exp(-18*Time.deltaTime));if(delta.sqrMagnitude>.003f)obj.transform.rotation=Quaternion.Slerp(obj.transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*8);}}}
    }
}



