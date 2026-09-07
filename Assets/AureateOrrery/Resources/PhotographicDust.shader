Shader "AureateOrrery/PhotographicDust"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   struct A { float4 p:POSITION; float2 uv:TEXCOORD0; float4 c:COLOR; };
   struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float4 c:COLOR; float depth:TEXCOORD1; float3 ws:TEXCOORD2; };
   V Vert(A a) { V o; o.ws=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.ws);o.depth=-TransformWorldToView(o.ws).z;o.uv=a.uv;o.c=a.c;return o; }
   half4 Frag(V i):SV_Target {
    float2 xy=i.uv*2-1;
    float r=length(xy);
    float shape=exp(-r*r*5)*smoothstep(1,.6,r);
    float scene=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.p)),_ZBufferParams);
    float fade=saturate((scene-i.depth)*8);
    Light key=GetMainLight();
    // Very low single-scattering approximation, no fog or additive star points.
    float3 illumination=.035+key.color*.12;
    return half4(i.c.rgb*illumination,i.c.a*shape*fade);
   }
   ENDHLSL
  }
 }
}
