Shader "Emberwake/Water Foam"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   V Vert(A i){V o;o.pos=TransformObjectToHClip(i.vertex.xyz);o.uv=i.uv;o.color=i.color;return o;}
   half4 Frag(V i):SV_Target
   {
    float2 p=i.uv*2-1;float radius=length(p);
    float ring=1-smoothstep(.045,.13,abs(radius-.72));
    float angle=atan2(p.y,p.x);
    float broken=smoothstep(-.4,.6,sin(angle*9+sin(angle*5)*2+_Time.y*1.8));
    float wake=1-smoothstep(.035,.16,abs(abs(p.x)-(.16+.42*(1-i.uv.y))+.045*sin(p.y*19+_Time.y*3)));
    wake*=smoothstep(0,.2,i.uv.y)*(1-smoothstep(.65,1,i.uv.y));
    float flecks=smoothstep(-.5,.6,sin(p.y*32+sin(p.x*18)+_Time.y*3));
    float alpha=lerp(ring*lerp(.12,1,broken),wake*lerp(.25,1,flecks),i.color.r);
    return half4(.78,.98,.90,alpha*i.color.a);
   }
   ENDHLSL
  }
 }
}
