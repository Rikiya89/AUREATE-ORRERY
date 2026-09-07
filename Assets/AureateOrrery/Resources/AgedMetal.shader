Shader "AureateOrrery/AgedMetal"
{
    // ================================================================
    // マテリアル設定
    // Inspector から調整可能なパラメータ。
    // ================================================================
    Properties
    {
        // 金属表面の基本色。
        _BaseColor("Base color", Color) = (.5, .35, .18, 1)

        // 金属度。
        // 1 に近いほど金属らしい反射になる。
        _Metallic("Metallic", Range(0,1)) = .86

        // 表面の滑らかさ。
        // 高いほど反射がシャープになる。
        _Smoothness("Smoothness", Range(0,1)) = .46

        // 発光色。
        // 現在の Frag 内では直接使用していないが、
        // マテリアル互換性のため保持している。
        _EmissionColor("Emission", Color) = (0,0,0,1)

        // ベーステクスチャ。
        // 現在はプロシージャルノイズ中心の表現なので直接サンプリングしていない。
        _BaseMap("Base map", 2D) = "white" {}

        // Alpha Cutoff 用パラメータ。
        _Cutoff("Cutoff", Float) = .5

        // カリングモード。
        // 0 = Off
        // 1 = Front
        // 2 = Back
        _Cull("Cull", Float) = 2
    }

    SubShader
    {
        // ================================================================
        // Universal Render Pipeline 用設定。
        // 不透明オブジェクトとして描画する。
        // ================================================================
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            // URP の Forward Rendering 用 Pass。
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            // ================================================================
            // Shader Entry Points
            // ================================================================
            #pragma vertex Vert
            #pragma fragment Frag

            // ================================================================
            // URP ライティング / シャドウ Variant
            // メインライト、追加ライト、Shadow、Reflection Probe などに対応。
            // ================================================================
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS

            // URP の PBR ライティング処理を使用するためのライブラリ。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ================================================================
            // Material Parameters
            // マテリアルごとに GPU へ送られる定数バッファ。
            // ================================================================
            CBUFFER_START(UnityPerMaterial)

            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _BaseMap_ST;

            float _Metallic;
            float _Smoothness;
            float _Cutoff;
            float _Cull;

            CBUFFER_END

            // ================================================================
            // Vertex Input
            // Mesh から受け取る頂点情報。
            // ================================================================
            struct A
            {
                // Object Space の頂点座標。
                float4 p : POSITION;

                // Object Space の法線。
                float3 n : NORMAL;
            };

            // ================================================================
            // Vertex → Fragment 間で渡すデータ。
            // ================================================================
            struct V
            {
                // Clip Space 座標。
                float4 p : SV_POSITION;

                // World Space 座標。
                float3 ws : TEXCOORD0;

                // World Space 法線。
                float3 n : TEXCOORD1;

                // Object Space 座標。
                // プロシージャルノイズ生成に使用する。
                float3 os : TEXCOORD2;
            };

            // ================================================================
            // Vertex Shader
            // Object Space の頂点・法線を World / Clip Space へ変換する。
            // ================================================================
            V Vert(A a)
            {
                V o;

                // Object Space → World Space。
                o.ws = TransformObjectToWorld(a.p.xyz);

                // World Space → Clip Space。
                o.p = TransformWorldToHClip(o.ws);

                // 法線を Object Space → World Space に変換。
                o.n = TransformObjectToWorldNormal(a.n);

                // Object Space 座標も Fragment Shader へ渡す。
                // オブジェクトと一緒に固定されたノイズパターンを作るために使用。
                o.os = a.p.xyz;

                return o;
            }

            // ================================================================
            // 疑似乱数 Hash
            // 3D 座標から 0〜1 の疑似ランダム値を生成する。
            // ================================================================
            float hash(float3 p)
            {
                return frac(
                    sin(
                        dot(
                            p,
                            float3(127.1, 311.7, 74.7)
                        )
                    ) * 43758.5453
                );
            }

            // ================================================================
            // 3D Value Noise
            //
            // Object Space 上で滑らかなランダムパターンを生成する。
            // 金属表面の経年劣化、ムラ、細かな傷の表現に使用。
            // ================================================================
            float noise(float3 p)
            {
                // グリッドセル座標。
                float3 i = floor(p);

                // セル内部のローカル座標。
                float3 f = frac(p);

                // Smoothstep 相当の補間カーブ。
                // ノイズのセル境界を滑らかにつなぐ。
                f = f * f * (3 - 2 * f);

                // 8つのセル頂点に対する疑似乱数を
                // X → Y → Z の順番で線形補間する。
                return lerp(
                    lerp(
                        lerp(
                            hash(i),
                            hash(i + float3(1,0,0)),
                            f.x
                        ),
                        lerp(
                            hash(i + float3(0,1,0)),
                            hash(i + float3(1,1,0)),
                            f.x
                        ),
                        f.y
                    ),
                    lerp(
                        lerp(
                            hash(i + float3(0,0,1)),
                            hash(i + float3(1,0,1)),
                            f.x
                        ),
                        lerp(
                            hash(i + float3(0,1,1)),
                            hash(i + 1),
                            f.x
                        ),
                        f.y
                    ),
                    f.z
                );
            }

            // ================================================================
            // Fragment Shader
            //
            // 古びた真鍮 / 金属表面をプロシージャルに生成し、
            // URP の Physically Based Rendering に渡す。
            // ================================================================
            half4 Frag(V i) : SV_Target
            {
                // ------------------------------------------------------------
                // Patina / 経年変化
                //
                // 低周波ノイズを使用して、
                // 表面に酸化・変色したようなムラを生成する。
                // ------------------------------------------------------------
                float patina = smoothstep(
                    .44,
                    .82,
                    noise(i.os * 19)
                );

                // ------------------------------------------------------------
                // Micro Scratches / 微細な傷
                //
                // 高周波ノイズを縦方向に強く伸ばし、
                // 金属表面の細かい傷や研磨跡を表現する。
                //
                // fwidth を使ってサブピクセル領域では強度を落とし、
                // カメラ移動時のちらつきや shimmer を抑える。
                // ------------------------------------------------------------
                float resolution =
                    1 - saturate(
                        length(
                            fwidth(i.os * 280)
                        )
                    );

                float micro =
                    (
                        noise(
                            i.os * float3(80, 280, 80)
                        ) - .5
                    ) * resolution;

                // World Space 法線を正規化。
                float3 n = normalize(i.n);

                // ------------------------------------------------------------
                // Curvature Approximation / 曲率の簡易推定
                //
                // 隣接ピクセル間で法線がどれだけ変化しているかを
                // fwidth で取得し、エッジ・曲面部分を推定する。
                //
                // 曲率の高い場所では Smoothness を少し増加させ、
                // ハイライトを強調する。
                // ------------------------------------------------------------
                float curvature =
                    saturate(
                        length(
                            fwidth(n)
                        ) * 2
                    );

                // ============================================================
                // URP Surface Data
                // ============================================================
                SurfaceData s = (SurfaceData)0;

                // ------------------------------------------------------------
                // Base Color
                //
                // Patina が強い部分は暗くし、
                // micro noise で非常に微細な明暗差を追加する。
                // ------------------------------------------------------------
                s.albedo =
                    _BaseColor.rgb
                    * lerp(
                        1.02,
                        .76,
                        patina
                    )
                    * (
                        1 + micro * .035
                    );

                // ------------------------------------------------------------
                // Metallic
                //
                // 酸化・汚れた部分では Metallic を若干低下させる。
                // ------------------------------------------------------------
                s.metallic =
                    saturate(
                        _Metallic
                        - patina * .13
                    );

                // ------------------------------------------------------------
                // Smoothness
                //
                // Patina        → 表面を少し粗くする
                // Micro Scratch → 細かな反射差を作る
                // Curvature     → エッジ部分のハイライトを若干強める
                //
                // 最低・最高値を制限して極端な反射を防ぐ。
                // ------------------------------------------------------------
                s.smoothness =
                    clamp(
                        _Smoothness
                        - patina * .12
                        + micro * .065
                        + curvature * .035,
                        .22,
                        .64
                    );

                // Normal Map は使用していないため、
                // Tangent Space のデフォルト法線を使用。
                s.normalTS = float3(0,0,1);

                // Ambient Occlusion は最大値。
                s.occlusion = 1;

                // Opaque Material のため Alpha = 1。
                s.alpha = 1;

                // ============================================================
                // URP Input Data
                // PBR ライティングに必要な World Space 情報を設定する。
                // ============================================================
                InputData d = (InputData)0;

                // Fragment の World Space 座標。
                d.positionWS = i.ws;

                // World Space 法線。
                d.normalWS = n;

                // カメラ方向。
                d.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(i.ws);

                // Main Light Shadow 用座標。
                d.shadowCoord =
                    TransformWorldToShadowCoord(i.ws);

                // Spherical Harmonics から環境光を取得。
                d.bakedGI =
                    SampleSH(n);

                // Screen Space UV。
                d.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(i.p);

                // Shadow Mask。
                d.shadowMask =
                    half4(1,1,1,1);

                // ============================================================
                // URP Standard PBR Lighting
                //
                // Metallic / Smoothness / GI / Shadow / Reflection Probe
                // などを含む URP 標準 PBR 計算を実行する。
                // ============================================================
                return UniversalFragmentPBR(d, s);
            }

            ENDHLSL
        }

        // ================================================================
        // URP Lit Shader の既存 Pass を再利用。
        //
        // ShadowCaster:
        //   他オブジェクトへ影を落とすための Pass。
        //
        // DepthOnly:
        //   Depth Texture 用。
        //
        // DepthNormals:
        //   Depth + Normal Texture 用。
        //
        // MotionVectors:
        //   Motion Blur / Temporal Effect 用のモーションベクトルを生成する。
        // ================================================================
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/MotionVectors"
    }
}