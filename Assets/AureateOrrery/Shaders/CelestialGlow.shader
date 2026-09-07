Shader "AureateOrrery/CelestialGlow"
{
    Properties { [HDR] _Tint("Tint", Color) = (1,1,1,1) _Phase("Loop phase", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Phase;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.color=v.color; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float r = length(i.uv * 2 - 1);
                float glow = exp(-r*r*7) * smoothstep(1, .65, r);
                float flicker = .85 + .15 * sin(_Phase + i.color.a * 6.2831853);
                return half4(i.color.rgb * _Tint.rgb * glow * flicker, 1);
            }
            ENDHLSL
        }
    }
}
