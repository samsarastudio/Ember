using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using Emberwake.Core;
namespace Emberwake.Client
{
    public sealed class FeedbackShowcase : MonoBehaviour
    {
        string output;EmberwakeApp app;const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        object Get(string name)=>typeof(EmberwakeApp).GetField(name,Flags).GetValue(app);
        void Set(string name,object value)=>typeof(EmberwakeApp).GetField(name,Flags).SetValue(app,value);
        void Call(string name,params object[] args)=>typeof(EmberwakeApp).GetMethod(name,Flags).Invoke(app,args);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(!Debug.isDebugBuild&&!Application.isEditor)return;foreach(var arg in Environment.GetCommandLineArgs())if(arg.StartsWith("--feedback-capture="))new GameObject("Feedback audit").AddComponent<FeedbackShowcase>().output=arg.Substring(19);}
        IEnumerator Start(){Directory.CreateDirectory(output);app=FindFirstObjectByType<EmberwakeApp>();float deadline=Time.time+30;while((string)Get("screen")!="home"&&(string)Get("screen")!="tutorial"){if(Time.time>deadline)throw new Exception("Feedback audit startup timeout");yield return null;}
            app.enabled=false;Call("StartPractice");var sim=(MatchSimulation)Get("simulation");var arena=(ArenaView)Get("arena");
            sim.Hands[0][0]="cutter";Set("snap",sim.Snapshot(0));Call("ApplySnapshot");yield return null;Canvas.ForceUpdateCanvases();
            var hand=(GameObject)Get("handRoot");var drag=hand.transform.GetChild(0).GetComponent<CardDrag>();var data=new PointerEventData(EventSystem.current){position=arena.Camera.WorldToScreenPoint(new Vector3(7,0,-5))};drag.OnBeginDrag(data);yield return null;Call("CaptureFrame",Path.Combine(output,"placement-valid.png"));
            data.position=arena.Camera.WorldToScreenPoint(new Vector3(7,0,5));drag.OnDrag(data);yield return null;Call("CaptureFrame",Path.Combine(output,"placement-invalid.png"));float supply=sim.Supply[0];drag.OnEndDrag(data);if(sim.Supply[0]!=supply)throw new Exception("Invalid drag spent supply");
            data.position=arena.Camera.WorldToScreenPoint(new Vector3(7,0,-5));drag.OnBeginDrag(data);drag.OnEndDrag(data);if(sim.Supply[0]!=supply-3)throw new Exception("Valid drag did not deploy");
            sim.Supply[0]=10;sim.Hands[0][0]="grapeshot";Set("snap",sim.Snapshot(0));Call("ApplySnapshot");yield return null;
            hand=(GameObject)Get("handRoot");drag=hand.transform.GetChild(0).GetComponent<CardDrag>();data.position=arena.Camera.WorldToScreenPoint(new Vector3(7,0,8));drag.OnBeginDrag(data);yield return null;Call("CaptureFrame",Path.Combine(output,"grapeshot-target.png"));drag.OnEndDrag(data);Set("snap",sim.Snapshot(0));Call("ApplySnapshot");yield return new WaitForSeconds(.1f);Call("CaptureFrame",Path.Combine(output,"grapeshot-flight.png"));yield return new WaitForSeconds(.3f);Call("CaptureFrame",Path.Combine(output,"grapeshot-impact.png"));
            Debug.Log("FEEDBACK AUDIT PASSED: invalid drag spends nothing, valid drag deploys, five visual captures.");Application.Quit();
        }
    }
}
