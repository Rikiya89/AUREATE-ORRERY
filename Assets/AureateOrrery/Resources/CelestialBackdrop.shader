Shader "AureateOrrery/CelestialBackdrop"
{
    Properties
    {
        _Intensity("Nebula brightness", Range(0, 2)) = 1
        _Stars("Star brightness", Range(0, 2)) = .65
        _Aspect("Aspect", Float) = 1
        _Seed("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Intensity, _Stars, _Aspect, _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1,0)), u.x),
                            lerp(Hash(i + float2(0,1)), Hash(i + 1), u.x), u.y);
            }
            float Cloud(float2 p)
            {
                float v = 0, a = .5;
                for (int i = 0; i < 4; i++)
                {
                    v += Noise(p) * a;
                    p = float2(p.x * 1.6 - p.y * 1.2, p.x * 1.2 + p.y * 1.6) + 5.7;
                    a *= .5;
                }
                return v;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - .5) * float2(_Aspect, 1);
                float2 offset = float2(_Seed * .013, _Seed * .007);
                float cloud = Cloud(p * 4 + offset);
                // Asymmetric diagonal mist; keep the central mechanism quiet.
                float band = exp(-pow((p.x + p.y * .42 - .13 + (cloud - .5) * .35) * 3.8, 2));
                float quietCenter = lerp(.28, 1, smoothstep(.12, .52, length(p)));
                float mist = band * smoothstep(.25, .78, cloud) * quietCenter;
                float3 color = float3(.008, .012, .025) +
                    lerp(float3(.028, .065, .095), float3(.075, .035, .063), i.uv.y) * mist * 2.2;
                color *= _Intensity;
                float2 grid = p * 115 + offset;
                float2 cell = floor(grid);
                float h = Hash(cell);
                float2 starCenter = .2 + .6 * float2(Hash(cell + 17), Hash(cell + 39));
                float d = length(frac(grid) - starCenter);
                float radius = lerp(.04, .10, Hash(cell + 71));
                float aa = max(length(fwidth(grid)), .015);
                float star = (1 - smoothstep(radius, radius + aa, d)) * step(.992, h);
                color += lerp(float3(.38,.53,.7), float3(.72,.60,.40), Hash(cell + 9))
                    * star * _Stars * quietCenter;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
