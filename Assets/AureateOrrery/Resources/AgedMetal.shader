Shader "AureateOrrery/AgedMetal"
{
 Properties
 {
  _BaseColor("Base color", Color)=(.5,.35,.18,1)
  _Metallic("Metallic", Range(0,1))=.86
  _Smoothness("Smoothness", Range(0,1))=.46
  _EmissionColor("Emission", Color)=(0,0,0,1)
  _BaseMap("Base map", 2D)="white" {}
  _Cutoff("Cutoff", Float)=.5
  _Cull("Cull", Float)=2
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor, _EmissionColor, _BaseMap_ST;
   float _Metallic, _Smoothness, _Cutoff, _Cull;
   CBUFFER_END
   struct A { float4 p:POSITION; float3 n:NORMAL; };
   struct V { float4 p:SV_POSITION; float3 ws:TEXCOORD0; float3 n:TEXCOORD1; float3 os:TEXCOORD2; };
   V Vert(A a) { V o; o.ws=TransformObjectToWorld(a.p.xyz); o.p=TransformWorldToHClip(o.ws); o.n=TransformObjectToWorldNormal(a.n); o.os=a.p.xyz; return o; }
   float hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
   float noise(float3 p) {
    float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
   }
   half4 Frag(V i):SV_Target {
    float patina=smoothstep(.44,.82,noise(i.os*19));
    // Filter subpixel scratches rather than letting them shimmer in motion.
    float resolution=1-saturate(length(fwidth(i.os*280)));
    float micro=(noise(i.os*float3(80,280,80))-.5)*resolution;
    float3 n=normalize(i.n);
    float curvature=saturate(length(fwidth(n))*2);
    SurfaceData s=(SurfaceData)0;
    s.albedo=_BaseColor.rgb*lerp(1.02,.76,patina)*(1+micro*.035);
    s.metallic=saturate(_Metallic-patina*.13);
    s.smoothness=clamp(_Smoothness-patina*.12+micro*.065+curvature*.035,.22,.64);
    s.normalTS=float3(0,0,1); s.occlusion=1; s.alpha=1;
    InputData d=(InputData)0;
    d.positionWS=i.ws; d.normalWS=n; d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.ws);
    d.shadowCoord=TransformWorldToShadowCoord(i.ws); d.bakedGI=SampleSH(n);
    d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p); d.shadowMask=half4(1,1,1,1);
    return UniversalFragmentPBR(d,s);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
  UsePass "Universal Render Pipeline/Lit/MotionVectors"
 }
}
