using System.Collections.Generic;
using UnityEngine;
using Emberwake.Core;
namespace Emberwake.Client
{
    public sealed class PlacementOverlay : MonoBehaviour
    {
        float nextMap;string mapCard;bool mapAffordable;int mapTeam; Mesh mesh;Material material;LineRenderer cursor;
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> indices=new List<int>();
        public static bool Affordable(BattleSnapshot s,CardDef c) => s.supply+.0001f>=c.cost && (c.role=="ORDER" || System.Linq.Enumerable.Count(s.units,u=>!u.structure&&u.hp>0)+c.count<=96);
        void Awake(){mesh=new Mesh{name="Deployment map"};mesh.MarkDynamic();gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;material=new Material(Shader.Find("Emberwake/Feedback"));gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;cursor=new GameObject("Drop radius").AddComponent<LineRenderer>();cursor.transform.SetParent(transform);cursor.sharedMaterial=material;cursor.useWorldSpace=true;cursor.loop=true;cursor.positionCount=64;cursor.widthMultiplier=.08f;}
        public bool Show(BattleSnapshot s,CardDef card,Vector3 at,bool inArena)
        {
            bool refresh=!gameObject.activeSelf;gameObject.SetActive(true);bool affordable=Affordable(s,card);if(refresh||Time.unscaledTime>=nextMap||mapCard!=card.id||mapAffordable!=affordable||mapTeam!=s.team){nextMap=Time.unscaledTime+.1f;mapCard=card.id;mapAffordable=affordable;mapTeam=s.team;vertices.Clear();colors.Clear();indices.Clear();
            for(float x=-11;x<11;x+=.5f)for(float z=-17;z<17;z+=.5f){bool legal=affordable&&MatchSimulation.LegalPlacement(s.team,card,x+.25f,z+.25f,s.units);var color=legal?new Color(.12f,1,.48f,.18f):new Color(1,.12f,.12f,.24f);int n=vertices.Count;vertices.Add(new Vector3(x,.12f,z));vertices.Add(new Vector3(x+.5f,.12f,z));vertices.Add(new Vector3(x+.5f,.12f,z+.5f));vertices.Add(new Vector3(x,.12f,z+.5f));for(int j=0;j<4;j++)colors.Add(color);indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);}
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();}
            bool valid=inArena&&affordable&&MatchSimulation.LegalPlacement(s.team,card,at.x,at.z,s.units);var tint=valid?new Color(.25f,1,.55f,1):new Color(1,.2f,.12f,1);cursor.startColor=cursor.endColor=tint;float radius=card.role=="ORDER"?card.range:.8f;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;cursor.SetPosition(i,at+new Vector3(Mathf.Cos(a)*radius,.18f,Mathf.Sin(a)*radius));}return valid;
        }
        public void Hide(){gameObject.SetActive(false);}
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}

