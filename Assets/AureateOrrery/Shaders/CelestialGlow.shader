Shader "AureateOrrery/CelestialGlow"
{
    // ================================================================
    // Inspector公開パラメータ
    // ================================================================

    Properties
    {
        // HDR対応の発光カラー。
        // CelestialSystem側から恒星・Trail・霞などの色を制御する。
        [HDR]
        _Tint("Tint", Color) = (1,1,1,1)

        // 0〜2πで進行するループ位相。
        // 発光の微細なFlickerアニメーションに使用する。
        _Phase("Loop phase", Float) = 0
    }


    SubShader
    {
        // ============================================================
        // URP / Transparent設定
        // ============================================================

        Tags
        {
            // Universal Render Pipeline専用Shader。
            "RenderPipeline" = "UniversalPipeline"

            // Transparent Queueで描画する。
            "Queue" = "Transparent"

            // 半透明オブジェクトとして扱う。
            "RenderType" = "Transparent"
        }


        Pass
        {
            // ========================================================
            // 加算合成
            // ========================================================

            // Source + Destination の加算ブレンド。
            //
            // 黒背景に対して光を重ねることで、
            // 星・Trail・Glowのような発光表現を作る。
            Blend One One


            // ========================================================
            // Depth設定
            // ========================================================

            // 深度バッファへの書き込みを無効化。
            // Glow同士が重なった際の不自然な遮蔽を防ぐ。
            ZWrite Off


            // ========================================================
            // Culling
            // ========================================================

            // QuadやTrailを両面から描画する。
            Cull Off


            // ========================================================
            // HLSL開始
            // ========================================================

            HLSLPROGRAM


            // Vertex Shader。
            #pragma vertex Vert

            // Fragment Shader。
            #pragma fragment Frag


            // ========================================================
            // URP Core Library
            // ========================================================

            // TransformObjectToHClipなど、
            // URP標準の座標変換関数を使用する。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            // ========================================================
            // Vertex入力
            // ========================================================

            struct Attributes
            {
                // Object Space上の頂点座標。
                float4 positionOS : POSITION;

                // Glow形状を計算するためのUV。
                float2 uv : TEXCOORD0;

                // Mesh側から渡される頂点カラー。
                //
                // RGB:
                // 星やTrailの基本色・明るさ。
                //
                // A:
                // Flickerの個体差を作るPhase Offsetとしても使用。
                float4 color : COLOR;
            };


            // ========================================================
            // Vertex → Fragment
            // ========================================================

            struct Varyings
            {
                // Clip Space上の頂点座標。
                float4 positionCS : SV_POSITION;

                // Fragment Shaderへ渡すUV。
                float2 uv : TEXCOORD0;

                // Fragment Shaderへ渡す頂点カラー。
                float4 color : COLOR;
            };


            // ========================================================
            // Material Parameters
            // ========================================================

            // SRP Batcher互換のMaterial定数バッファ。
            CBUFFER_START(UnityPerMaterial)

            // 全体Tintカラー。
            float4 _Tint;

            // アニメーションループ位相。
            float _Phase;

            CBUFFER_END


            // ========================================================
            // Vertex Shader
            // ========================================================

            Varyings Vert(Attributes v)
            {
                Varyings o;


                // Object SpaceからClip Spaceへ頂点を変換する。
                o.positionCS =
                    TransformObjectToHClip(
                        v.positionOS.xyz
                    );


                // UVをそのままFragment Shaderへ渡す。
                o.uv =
                    v.uv;


                // Mesh頂点カラーもそのまま渡す。
                o.color =
                    v.color;


                return o;
            }


            // ========================================================
            // Fragment Shader
            // ========================================================

            half4 Frag(Varyings i) : SV_Target
            {
                // ====================================================
                // UV中心からの距離
                // ====================================================

                // UV:
                // 0〜1
                //
                // uv * 2 - 1:
                // -1〜1
                //
                // Quad中心が (0,0) になるよう変換し、
                // 中心からの距離を求める。
                float r =
                    length(
                        i.uv * 2 - 1
                    );


                // ====================================================
                // Radial Glow
                // ====================================================

                // 中心を最大輝度とし、
                // 外側へ行くほど指数関数的に暗くする。
                //
                // exp(-r² * 7)
                //
                // によって柔らかいGaussian系の光分布を作る。
                //
                // smoothstepによって外周をさらに抑え、
                // Quadの四角い輪郭が見えないようにする。
                float glow =
                    exp(
                        -r * r * 7
                    )
                    *
                    smoothstep(
                        1,
                        .65,
                        r
                    );


                // ====================================================
                // Flicker
                // ====================================================

                // 星やTrailへ非常に弱い明滅を追加する。
                //
                // 基本輝度:
                // 0.85
                //
                // 変動幅:
                // ±0.15
                //
                // _Phase:
                // 全体のループ時間。
                //
                // color.a * 2π:
                // 各頂点カラーAlphaをPhase Offsetとして使用し、
                // 全ての星が同時に点滅することを防ぐ。
                float flicker =
                    .85
                    +
                    .15
                    *
                    sin(
                        _Phase
                        +
                        i.color.a
                        * 6.2831853
                    );


                // ====================================================
                // 最終発光カラー
                // ====================================================

                // Meshの頂点カラー
                // × Material Tint
                // × Radial Glow
                // × Flicker
                //
                // Blend One Oneなので、
                // RGB値がそのまま背景へ加算される。
                return half4(
                    i.color.rgb
                    *
                    _Tint.rgb
                    *
                    glow
                    *
                    flicker,

                    1
                );
            }


            ENDHLSL
        }
    }
}