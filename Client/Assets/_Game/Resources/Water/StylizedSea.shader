Shader "Emberwake/Stylized Sea"
{
 Properties
 {
  _DeepColor("Deep teal",Color)=(0.035,0.29,0.34,1)
  _ShallowColor("Lagoon turquoise",Color)=(0.12,0.68,0.64,1)
  _FoamColor("Ivory foam",Color)=(0.82,0.98,0.89,1)
  _DepthRange("Depth gradient metres",Range(0.5,12))=3.2
  _FoamWidth("Contact foam metres",Range(0.05,2))=0.65
  _WaveHeight("Wave height",Range(0,0.095))=0.085
  _WaveSpeed("Wave speed",Range(0,2))=0.65
  _FacetStrength("Facet shading",Range(0,1))=0.28
  _CausticStrength("Shallow caustics",Range(0,0.5))=0.10
  [Toggle(_SEA_REFRACTION)] _Refraction("Optional opaque texture refraction",Float)=0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
  Pass
  {
   Name "StylizedSea"
   Tags { "LightMode"="UniversalForward" }
   ZWrite Off Cull Back
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.5
   #pragma multi_compile_local_fragment _ _SEA_REFRACTION
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #if defined(_SEA_REFRACTION)
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   #endif
   CBUFFER_START(UnityPerMaterial)
   half4 _DeepColor,_ShallowColor,_FoamColor;
   float _DepthRange,_FoamWidth,_WaveHeight,_WaveSpeed,_FacetStrength,_CausticStrength,_Refraction;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; };
   struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
   float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
   float Noise(float2 p) { float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y); }
   Varyings Vert(Attributes input)
   {
    Varyings o; o.world=TransformObjectToWorld(input.positionOS.xyz);
    float2 p=o.world.xz;float t=_Time.y*_WaveSpeed;
    o.world.y+=_WaveHeight*(sin(dot(p,float2(.65,.32))+t)+.45*sin(dot(p,float2(-.28,.83))-t*1.3));
    o.positionCS=TransformWorldToHClip(o.world);return o;
   }
   half4 Frag(Varyings i):SV_Target
   {
    float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
    float raw=SampleSceneDepth(uv);
    #if !UNITY_REVERSED_Z
    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
    #endif
    float3 bottom=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
    float depth=max(0,i.world.y-bottom.y);
    float2 p=i.world.xz;float t=_Time.y*_WaveSpeed;
    float n=Noise(p*.9+float2(t*.12,-t*.09));
    float shallow=exp2(-depth/max(.1,_DepthRange)*2.5);
    shallow=lerp(shallow,floor(shallow*6)/6,.7);
    half3 color=lerp(_DeepColor.rgb,_ShallowColor.rgb,shallow);
    float3 normal=normalize(cross(ddy(i.world),ddx(i.world)));if(normal.y<0)normal=-normal;
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
    float facet=floor(saturate(dot(normal,normalize(float3(-.5,1,-.4))))*7)/7;
    float cells=Hash(floor(p*.65)+step(frac(p.x*.65),frac(p.y*.65))*.37);
    color*=1+_FacetStrength*(facet-.75)+.10*(cells-.5)+.05*(n-.5);
    color*=lerp(.76,1,sun.shadowAttenuation);
    float caustic=pow(saturate(1-abs(sin(p.x*2.1+n*2+t*.4)*sin(p.y*2.4-n*2-t*.3))),18);
    color+=_CausticStrength*caustic*shallow*smoothstep(.15,1,depth)*_ShallowColor.rgb;
    // Pulsing broken bands follow real opaque geometry, including orthographic cameras.
    float band=1-smoothstep(_FoamWidth*.3,_FoamWidth,depth+(n-.5)*.35);
    float surf=pow(saturate(.5+.5*sin(depth*16-t*2+n*4)),5);
    float foam=saturate(band*(.35+.65*surf));
    color=lerp(color,_FoamColor.rgb,foam*.88);
    float3 viewDir=GetWorldSpaceNormalizeViewDir(i.world);
    float fresnel=pow(1-saturate(dot(normal,viewDir)),4);
    color+=fresnel*.15*half3(.32,.63,.68);
    float glint=pow(saturate(dot(normal,normalize(sun.direction+viewDir))),96);
    color+=sun.color*glint*.12;
    #if defined(_SEA_REFRACTION)
    float2 refractUV=saturate(uv+normal.xz*.006*saturate(depth));
    float rd=SampleSceneDepth(refractUV);
    #if !UNITY_REVERSED_Z
    rd=lerp(UNITY_NEAR_CLIP_VALUE,1,rd);
    #endif
    float3 rp=ComputeWorldSpacePosition(refractUV,rd,UNITY_MATRIX_I_VP);
    refractUV=rp.y>i.world.y?uv:refractUV;
    color=lerp(color,SampleSceneColor(refractUV)*_ShallowColor.rgb,shallow*.18*(1-foam));
    #endif
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
