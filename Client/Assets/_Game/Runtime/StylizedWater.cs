using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Emberwake.Client
{
    // Presentation only: water never changes authoritative positions or navigation.
    public sealed class StylizedWater : MonoBehaviour
    {
        public const float Level = -.18f;
        public const int MaxRipples = 192;
        public Material SeaMaterial { get; private set; }
        readonly List<Vector3> shores = new List<Vector3>();
        readonly Ripple[] ripples = new Ripple[MaxRipples];
        readonly Dictionary<int, Vector3> lastWake = new Dictionary<int, Vector3>();
        readonly Dictionary<int, float> wakeTime = new Dictionary<int, float>();
        Mesh seaMesh, bedMesh, foamMesh;
        Material foamMaterial;
        readonly Vector3[] foamVertices = new Vector3[MaxRipples * 4];
        readonly Vector2[] foamUV = new Vector2[MaxRipples * 4];
        readonly Color[] foamColors = new Color[MaxRipples * 4];
        int cursor;
        struct Ripple { public Vector3 position,direction; public float born, life, size, strength; public bool wake; }

        public void AddShore(Vector3 position, float radius) { shores.Add(new Vector3(position.x, position.z, radius)); }
        public void Initialize(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresDepthTexture = true;
            // Opt-in refraction costs an opaque color copy. Default uses depth only.
            data.requiresColorTexture = false;
            var template=Resources.Load<Material>("Water/Sea");
            SeaMaterial = template ? new Material(template) : new Material(Resources.Load<Shader>("Water/StylizedSea"));
            foamMaterial = new Material(Resources.Load<Shader>("Water/Foam"));
            seaMesh = Grid(false); bedMesh = Grid(true);
            CreateRenderer("Faceted teal water", seaMesh, SeaMaterial);
            CreateRenderer("Submerged lagoon shelf", bedMesh, ToyFactory.Mat(new Color(.12f,.36f,.31f)));
            foamMesh = new Mesh { name = "Pooled wake and impact rings" }; foamMesh.MarkDynamic();
            int[] triangles = new int[MaxRipples * 6];
            for(int i=0;i<MaxRipples;i++)
            {
                int v=i*4,t=i*6; foamUV[v]=new Vector2(0,0);foamUV[v+1]=new Vector2(1,0);foamUV[v+2]=new Vector2(0,1);foamUV[v+3]=new Vector2(1,1);
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            foamMesh.vertices=foamVertices;foamMesh.uv=foamUV;foamMesh.colors=foamColors;foamMesh.triangles=triangles;
            foamMesh.bounds=new Bounds(Vector3.zero,new Vector3(140,10,140));
            CreateRenderer("Ship wakes and splashes",foamMesh,foamMaterial);
        }
        void CreateRenderer(string label, Mesh mesh, Material material)
        {
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
        }
        Mesh Grid(bool bed)
        {
            const int n=112;const float extent=70;
            var v=new Vector3[(n+1)*(n+1)];var t=new int[n*n*6];
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)
            {
                float px=(x/(float)n-.5f)*extent,pz=(z/(float)n-.5f)*extent;
                float y=Level;
                if(bed)
                {
                    y=-5.5f+.4f*Mathf.PerlinNoise(px*.08f+30,pz*.08f+30);
                    foreach(var s in shores)
                    {
                        float d=Vector2.Distance(new Vector2(px,pz),new Vector2(s.x,s.y));
                        float shelf=-.60f-Mathf.Max(0,d-s.z*.45f)*1.65f;
                        y=Mathf.Max(y,shelf);
                    }
                }
                v[z*(n+1)+x]=new Vector3(px,y,pz);
            }
            int k=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {int a=z*(n+1)+x,b=a+1,c=a+n+1,d=c+1;t[k++]=a;t[k++]=c;t[k++]=b;t[k++]=b;t[k++]=c;t[k++]=d;}
            var mesh=new Mesh{name=bed?"Lagoon bathymetry":"Low swell grid"};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(Vector3.up);mesh.bounds=bounds;return mesh;
        }
        public void TrackShip(int id,Vector3 at,float width)
        {
            if(lastWake.TryGetValue(id,out var previous))
            {
                float distance=Vector3.Distance(at,previous);
                if(distance>5){lastWake[id]=at;wakeTime[id]=Time.time;return;}
                if(distance>.42f && Time.time-wakeTime[id]>.22f)
                {var forward=(at-previous).normalized;Emit(at-forward*.65f,width*.8f,1.7f,.65f,forward);lastWake[id]=at;wakeTime[id]=Time.time;}
            }
            else {lastWake[id]=at;wakeTime[id]=Time.time;Emit(at,width*.65f,.9f,.55f);}
        }
        public void ForgetShip(int id){lastWake.Remove(id);wakeTime.Remove(id);}
        public void Splash(Vector3 at,float size=1){Emit(at,size,1.2f,.85f);}
        public void ClearEffects(){System.Array.Clear(ripples,0,ripples.Length);lastWake.Clear();wakeTime.Clear();}
        void Emit(Vector3 at,float size,float life,float strength,Vector3 direction=default)
        {ripples[cursor]=new Ripple{position=at,born=Time.time,life=life,size=size,strength=strength,direction=direction,wake=direction.sqrMagnitude>.1f};cursor=(cursor+1)%MaxRipples;}
        void LateUpdate()
        {
            if(!foamMesh)return;
            for(int i=0;i<MaxRipples;i++)
            {
                var r=ripples[i];float age=(Time.time-r.born)/Mathf.Max(.01f,r.life);bool active=r.life>0&&age<1;
                float size=active?r.size*(1+age*2):0;var p=r.position;p.y=Level+.14f;
                Vector3 forward=r.wake?r.direction:Vector3.forward;Vector3 right=new Vector3(forward.z,0,-forward.x);
                int j=i*4;foamVertices[j]=p-right*size-forward*size;foamVertices[j+1]=p+right*size-forward*size;foamVertices[j+2]=p-right*size+forward*size;foamVertices[j+3]=p+right*size+forward*size;
                var c=new Color(r.wake?1:0,1,1,active?r.strength*(1-age)*(1-age):0);for(int q=0;q<4;q++)foamColors[j+q]=c;
            }
            foamMesh.vertices=foamVertices;foamMesh.colors=foamColors;
        }
        public void SetRefraction(Camera camera,bool enabled)
        {camera.GetUniversalAdditionalCameraData().requiresColorTexture=enabled;if(enabled)SeaMaterial.EnableKeyword("_SEA_REFRACTION");else SeaMaterial.DisableKeyword("_SEA_REFRACTION");}
        void OnDestroy(){Destroy(SeaMaterial);Destroy(foamMaterial);Destroy(seaMesh);Destroy(bedMesh);Destroy(foamMesh);}
    }
}
