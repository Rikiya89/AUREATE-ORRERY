Shader "AureateOrrery/PlanetSurface"
{
 Properties
 {
  _BaseColor("Base color", Color)=(.34,.36,.38,1)
  _Metallic("Metallic", Range(0,1))=.15
  _Smoothness("Smoothness", Range(0,1))=.38
  _SurfaceScale("Large surface scale", Range(.5,12))=3
  _SurfaceVariation("Tonal variation", Range(0,1))=.35
  _BumpStrength("Procedural bump", Range(0,.25))=.06
  _RoughnessVariation("Roughness variation", Range(0,.4))=.1
  _EmissionColor("Shadow floor", Color)=(.006,.007,.009,1)
  _RimColor("Atmospheric rim", Color)=(.2,.35,.6,1)
  _RimPower("Rim width", Range(2,12))=5.5
  _RimStrength("Rim strength", Range(0,.5))=.1
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Name "PlanetForward"
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
   float4 _BaseColor;
   float4 _EmissionColor;
   float4 _RimColor;
   float _Metallic;
   float _Smoothness;
   float _SurfaceScale;
   float _SurfaceVariation;
   float _BumpStrength;
   float _RoughnessVariation;
   float _RimPower;
   float _RimStrength;
   CBUFFER_END

   struct Attributes
   {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
   };

   struct Varyings
   {
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 positionOS : TEXCOORD2;
   };

   Varyings Vert(Attributes input)
   {
    Varyings output;
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.positionOS = input.positionOS.xyz;
    return output;
   }

   float Hash(float3 p)
   {
    return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
   }

   float Noise(float3 p)
   {
    float3 cell = floor(p);
    float3 f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(
     lerp(lerp(Hash(cell), Hash(cell + float3(1,0,0)), f.x), lerp(Hash(cell + float3(0,1,0)), Hash(cell + float3(1,1,0)), f.x), f.y),
     lerp(lerp(Hash(cell + float3(0,0,1)), Hash(cell + float3(1,0,1)), f.x), lerp(Hash(cell + float3(0,1,1)), Hash(cell + 1), f.x), f.y),
     f.z
    );
   }

   float SurfaceNoise(float3 p)
   {
    float broad = Noise(p);
    float continents = Noise(p * 2.07 + 13.4);
    float erosion = Noise(p * 4.13 - 7.2);
    return broad * .58 + continents * .29 + erosion * .13;
   }

   half4 Frag(Varyings input) : SV_Target
   {
    float3 p = input.positionOS * _SurfaceScale;
    float tone = SurfaceNoise(p);

    // A finite-difference gradient creates restrained bump without textures or added geometry.
    float epsilon = .035;
    float3 gradientOS = float3(
     SurfaceNoise(p + float3(epsilon,0,0)) - tone,
     SurfaceNoise(p + float3(0,epsilon,0)) - tone,
     SurfaceNoise(p + float3(0,0,epsilon)) - tone
    ) / epsilon;

    float3 geometricNormal = normalize(input.normalWS);
    float3 gradientWS = TransformObjectToWorldDir(gradientOS);
    gradientWS -= geometricNormal * dot(gradientWS, geometricNormal);
    float3 normalWS = normalize(geometricNormal - gradientWS * _BumpStrength);
    float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    float broadTone = lerp(.70, 1.14, smoothstep(.12, .88, tone));
    float fineTone = Noise(p * 7.1 + 4.7) - .5;

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = _BaseColor.rgb * lerp(1, broadTone, _SurfaceVariation) * (1 + fineTone * _SurfaceVariation * .06);
    surface.metallic = _Metallic;
    surface.smoothness = saturate(_Smoothness + (tone - .5) * _RoughnessVariation);
    surface.normalTS = float3(0, 0, 1);
    surface.occlusion = lerp(1, .82, saturate((.48 - tone) * _SurfaceVariation));
    surface.alpha = 1;

    // The atmosphere is silhouette-only and biased toward one side to avoid a uniform halo.
    float fresnel = pow(1 - saturate(dot(normalWS, viewWS)), _RimPower);
    float directionalRim = lerp(.18, 1, saturate(dot(normalWS, normalize(float3(-.45, .55, -.7))) * .5 + .5));
    surface.emission = _EmissionColor.rgb + _RimColor.rgb * fresnel * directionalRim * _RimStrength;

    InputData lighting = (InputData)0;
    lighting.positionWS = input.positionWS;
    lighting.normalWS = normalWS;
    lighting.viewDirectionWS = viewWS;
    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    lighting.bakedGI = SampleSH(normalWS);
    lighting.vertexLighting = VertexLighting(input.positionWS, normalWS);
    lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    lighting.shadowMask = half4(1,1,1,1);

    return UniversalFragmentPBR(lighting, surface);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
  UsePass "Universal Render Pipeline/Lit/MotionVectors"
 }
}
