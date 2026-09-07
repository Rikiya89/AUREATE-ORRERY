Shader "AureateOrrery/PhotographicDust"
{
    // ================================================================
    // Photographic Dust Shader
    //
    // 半透明の微粒子を描画するためのシェーダー。
    // 加算発光ではなく通常の Alpha Blend を使い、
    // 写真的な「空気中の埃」のような見え方を狙う。
    //
    // Scene Depth を参照し、
    // オブジェクト表面との交差部分をフェードさせることで、
    // パーティクルがジオメトリを突き抜けて見えるのを抑える。
    // ================================================================
    SubShader
    {
        // ============================================================
        // URP / Transparent 設定
        // ============================================================
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            // ========================================================
            // 通常の Alpha Blend。
            //
            // SrcAlpha:
            //   粒子自身の Alpha を使う。
            //
            // OneMinusSrcAlpha:
            //   背景との自然な合成を行う。
            //
            // Additive Blend ではないため、
            // 星や発光粒子のように白飛びしにくい。
            // ========================================================
            Blend SrcAlpha OneMinusSrcAlpha

            // 半透明粒子なので Depth Buffer へは書き込まない。
            ZWrite Off

            // Particle Quad の両面を描画する。
            Cull Off

            HLSLPROGRAM

            // ========================================================
            // Shader Entry Points
            // ========================================================
            #pragma vertex Vert
            #pragma fragment Frag

            // URP の Lighting 情報を使用する。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Camera Depth Texture を取得するためのライブラリ。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ========================================================
            // Vertex Input
            // ========================================================
            struct A
            {
                // Object Space の頂点座標。
                float4 p : POSITION;

                // Particle Quad の UV。
                float2 uv : TEXCOORD0;

                // Particle System から渡される頂点カラー。
                // RGB = 粒子色
                // A   = 粒子の透明度
                float4 c : COLOR;
            };

            // ========================================================
            // Vertex → Fragment 間で渡すデータ
            // ========================================================
            struct V
            {
                // Clip Space 座標。
                float4 p : SV_POSITION;

                // Particle UV。
                float2 uv : TEXCOORD0;

                // Particle Color。
                float4 c : COLOR;

                // Camera から見た粒子の Eye Depth。
                float depth : TEXCOORD1;

                // World Space 座標。
                float3 ws : TEXCOORD2;
            };

            // ========================================================
            // Vertex Shader
            // ========================================================
            V Vert(A a)
            {
                V o;

                // Object Space → World Space。
                o.ws =
                    TransformObjectToWorld(a.p.xyz);

                // World Space → Clip Space。
                o.p =
                    TransformWorldToHClip(o.ws);

                // ----------------------------------------------------
                // View Space の Z から Eye Depth を取得。
                //
                // Unity の View Space ではカメラ前方が負の Z なので、
                // 正の距離として扱うためにマイナスを付ける。
                // ----------------------------------------------------
                o.depth =
                    -TransformWorldToView(o.ws).z;

                // UV と頂点カラーをそのまま Fragment Shader へ渡す。
                o.uv = a.uv;
                o.c = a.c;

                return o;
            }

            // ========================================================
            // Fragment Shader
            //
            // 柔らかい円形粒子を生成し、
            // Scene Depth と比較して交差部分をフェードさせる。
            // ========================================================
            half4 Frag(V i) : SV_Target
            {
                // ----------------------------------------------------
                // UV を 0〜1 から -1〜1 に変換。
                //
                // Particle Quad の中心が (0,0) になる。
                // ----------------------------------------------------
                float2 xy =
                    i.uv * 2 - 1;

                // Particle 中心からの距離。
                float r =
                    length(xy);

                // ----------------------------------------------------
                // Dust Shape
                //
                // exp() を使った Gaussian に近い減衰で、
                // 中心が少し濃く、外側が柔らかく消える粒子を作る。
                //
                // smoothstep を追加することで、
                // Quad の端を完全に透明化する。
                // ----------------------------------------------------
                float shape =
                    exp(-r * r * 5)
                    * smoothstep(
                        1,
                        .6,
                        r
                    );

                // ----------------------------------------------------
                // Scene Depth
                //
                // 現在の Fragment と同じ Screen UV 位置にある
                // Scene Depth Texture を取得する。
                // ----------------------------------------------------
                float scene =
                    LinearEyeDepth(
                        SampleSceneDepth(
                            GetNormalizedScreenSpaceUV(i.p)
                        ),
                        _ZBufferParams
                    );

                // ----------------------------------------------------
                // Soft Particle Fade
                //
                // Scene Depth と粒子 Depth の差を比較する。
                //
                // 粒子が物体表面へ近づくほど Alpha を下げ、
                // ジオメトリとの不自然な交差を軽減する。
                //
                // * 8 はフェード距離の強さ。
                // 大きいほど短い距離でフェードする。
                // ----------------------------------------------------
                float fade =
                    saturate(
                        (scene - i.depth) * 8
                    );

                // URP の Main Directional Light を取得。
                Light key =
                    GetMainLight();

                // ----------------------------------------------------
                // Dust Illumination
                //
                // 非常に弱い Single Scattering の簡易近似。
                //
                // .035:
                //   完全な黒にならないための最低環境光。
                //
                // key.color * .12:
                //   Main Light の色を弱く反映する。
                //
                // Fog や Additive Star のような強い発光は使わず、
                // 写真的な微粒子として見えるようにする。
                // ----------------------------------------------------
                float3 illumination =
                    .035
                    + key.color * .12;

                // ----------------------------------------------------
                // Final Output
                //
                // RGB:
                //   Particle Color × 弱いライティング
                //
                // Alpha:
                //   Particle Alpha
                //   × 円形 Shape
                //   × Scene Depth Fade
                // ----------------------------------------------------
                return half4(
                    i.c.rgb * illumination,
                    i.c.a * shape * fade
                );
            }

            ENDHLSL
        }
    }
}