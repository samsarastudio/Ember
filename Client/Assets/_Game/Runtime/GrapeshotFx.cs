using UnityEngine;
using Emberwake.Core;
namespace Emberwake.Client
{
    public sealed class GrapeshotFx : MonoBehaviour
    {
        Transform[] pellets=new Transform[9];LineRenderer[] trails=new LineRenderer[9];LineRenderer ring;Material material;Vector3 target,origin;float started,radius;bool hit;StylizedWater water;
        public void Init(ZoneState cue,float serverTime,StylizedWater sea)
        {
            water=sea;target=new Vector3(cue.x,.3f,cue.z);origin=new Vector3(0,1.8f,cue.team==0?-15:15);radius=cue.radius;started=Time.time-Mathf.Max(0,serverTime-(cue.expires-.95f));material=new Material(Shader.Find("Emberwake/Feedback"));
            for(int i=0;i<pellets.Length;i++){var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(ball.GetComponent<Collider>());ball.transform.SetParent(transform);ball.transform.localScale=Vector3.one*.23f;ball.GetComponent<Renderer>().sharedMaterial=ToyFactory.Mat(new Color(1,.65f,.18f),true);pellets[i]=ball.transform;trails[i]=Line("Grapeshot streak",2,.09f);trails[i].startColor=new Color(1,.65f,.15f,0);trails[i].endColor=new Color(1,.9f,.5f,1);}
            ring=Line("Grapeshot impact",64,.12f);ring.loop=true;ring.enabled=false;Update();
        }
        LineRenderer Line(string title,int count,float width){var line=new GameObject(title).AddComponent<LineRenderer>();line.transform.SetParent(transform);line.sharedMaterial=material;line.positionCount=count;line.widthMultiplier=width;line.useWorldSpace=true;return line;}
        Vector3 End(int i){float a=i*2.39996f;float r=i==0?0:radius*.75f*Mathf.Sqrt(i/8f);return target+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);}
        Vector3 Flight(int i,float t){return Vector3.Lerp(origin,End(i),t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*4);}
        void Update(){if(!material)return;float age=Time.time-started;float t=Mathf.Clamp01(age/.35f);for(int i=0;i<9;i++){pellets[i].gameObject.SetActive(age<.35f);trails[i].enabled=age<.35f;pellets[i].position=Flight(i,t);trails[i].SetPosition(0,Flight(i,Mathf.Max(0,t-.13f)));trails[i].SetPosition(1,pellets[i].position);}
            if(age>=.35f){if(!hit){hit=true;for(int i=0;i<9;i++)water.Splash(End(i),.55f);}ring.enabled=true;float fade=Mathf.Clamp01((age-.35f)/.6f);ring.startColor=ring.endColor=new Color(1,.8f,.3f,1-fade);for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,target+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*Mathf.Lerp(.3f,1,fade));}}
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
