Shader "AureateOrrery/PlanetSurface"
{
    // ================================================================
    // Planet Surface Shader
    //
    // 惑星表面をプロシージャルノイズで生成する URP 用 PBR シェーダー。
    //
    // テクスチャを使用せず、
    // ・大陸のような大きな模様
    // ・侵食のような細かな模様
    // ・疑似的な凹凸
    // ・粗さの変化
    // ・大気リム
    // を Object Space ベースで生成する。
    // ================================================================

    Properties
    {
        // 惑星表面の基本色。
        _BaseColor("Base color", Color) = (.34, .36, .38, 1)

        // 金属度。
        // 岩石惑星では低めの値を使用する。
        _Metallic("Metallic", Range(0,1)) = .15

        // 表面の基本 Smoothness。
        // 高いほどハイライトがシャープになる。
        _Smoothness("Smoothness", Range(0,1)) = .38

        // プロシージャル表面パターン全体のスケール。
        // 値を大きくすると模様が細かくなる。
        _SurfaceScale("Large surface scale", Range(.5,12)) = 3

        // 表面の明暗変化の強さ。
        _SurfaceVariation("Tonal variation", Range(0,1)) = .35

        // プロシージャルノイズから作る疑似的な凹凸の強さ。
        _BumpStrength("Procedural bump", Range(0,.25)) = .06

        // ノイズに応じて Smoothness を変化させる強さ。
        _RoughnessVariation("Roughness variation", Range(0,.4)) = .1

        // 影側が完全な黒にならないための最低発光色。
        _EmissionColor("Shadow floor", Color) = (.006, .007, .009, 1)

        // 惑星輪郭付近に表示する大気リムの色。
        _RimColor("Atmospheric rim", Color) = (.2, .35, .6, 1)

        // Fresnel リムの幅。
        // 値を大きくすると輪郭付近だけに細く集中する。
        _RimPower("Rim width", Range(2,12)) = 5.5

        // 大気リムの発光強度。
        _RimStrength("Rim strength", Range(0,.5)) = .1
    }

    SubShader
    {
        // ================================================================
        // URP / Opaque Geometry 設定
        // ================================================================
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            // Forward Rendering 用のメイン Pass。
            Name "PlanetForward"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            // ============================================================
            // Shader Entry Points
            // ============================================================
            #pragma vertex Vert
            #pragma fragment Frag

            // ============================================================
            // URP Lighting / Shadow Variants
            //
            // Main Light
            // Additional Lights
            // Soft Shadows
            // Cluster Lighting
            // Reflection Probe
            // などに対応する。
            // ============================================================
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS

            // URP 標準ライティング機能を使用する。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ============================================================
            // Material Parameters
            // ============================================================
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

            // ============================================================
            // Vertex Input
            // ============================================================
            struct Attributes
            {
                // Object Space 頂点座標。
                float4 positionOS : POSITION;

                // Object Space 法線。
                float3 normalOS : NORMAL;
            };

            // ============================================================
            // Vertex → Fragment 間で渡すデータ
            // ============================================================
            struct Varyings
            {
                // Clip Space 座標。
                float4 positionCS : SV_POSITION;

                // World Space 座標。
                float3 positionWS : TEXCOORD0;

                // World Space 法線。
                float3 normalWS : TEXCOORD1;

                // Object Space 座標。
                // 表面ノイズ生成に使用する。
                float3 positionOS : TEXCOORD2;
            };

            // ============================================================
            // Vertex Shader
            // ============================================================
            Varyings Vert(Attributes input)
            {
                Varyings output;

                // URP 標準関数で Object Space から
                // World / View / Clip Space の座標を生成する。
                VertexPositionInputs position =
                    GetVertexPositionInputs(input.positionOS.xyz);

                // Clip Space 座標。
                output.positionCS =
                    position.positionCS;

                // World Space 座標。
                output.positionWS =
                    position.positionWS;

                // Object Space 法線を World Space へ変換。
                output.normalWS =
                    TransformObjectToWorldNormal(input.normalOS);

                // Object Space 座標を保持。
                // 惑星と一緒にノイズが固定されるため、
                // カメラ移動によって模様がずれない。
                output.positionOS =
                    input.positionOS.xyz;

                return output;
            }

            // ============================================================
            // Hash
            //
            // 3D 座標から 0〜1 の疑似ランダム値を生成する。
            // Value Noise の基礎として使用する。
            // ============================================================
            float Hash(float3 p)
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

            // ============================================================
            // 3D Value Noise
            //
            // 格子点ごとに Hash 値を作り、
            // 8 頂点を滑らかに補間して連続ノイズを生成する。
            // ============================================================
            float Noise(float3 p)
            {
                // 現在位置が所属するグリッドセル。
                float3 cell =
                    floor(p);

                // セル内部の 0〜1 座標。
                float3 f =
                    frac(p);

                // Smoothstep 相当の補間カーブ。
                // セル境界での急激な変化を抑える。
                f =
                    f * f * (3 - 2 * f);

                // 3D セルの8頂点を
                // X → Y → Z の順で補間する。
                return lerp(
                    lerp(
                        lerp(
                            Hash(cell),
                            Hash(cell + float3(1,0,0)),
                            f.x
                        ),
                        lerp(
                            Hash(cell + float3(0,1,0)),
                            Hash(cell + float3(1,1,0)),
                            f.x
                        ),
                        f.y
                    ),
                    lerp(
                        lerp(
                            Hash(cell + float3(0,0,1)),
                            Hash(cell + float3(1,0,1)),
                            f.x
                        ),
                        lerp(
                            Hash(cell + float3(0,1,1)),
                            Hash(cell + 1),
                            f.x
                        ),
                        f.y
                    ),
                    f.z
                );
            }

            // ============================================================
            // Planet Surface Noise
            //
            // 3つの異なる周波数の Noise を合成する。
            //
            // broad:
            //   惑星全体の大規模な地形。
            //
            // continents:
            //   大陸・地域単位の中規模な形状。
            //
            // erosion:
            //   侵食・岩肌のような細かなディテール。
            // ============================================================
            float SurfaceNoise(float3 p)
            {
                float broad =
                    Noise(p);

                float continents =
                    Noise(
                        p * 2.07
                        + 13.4
                    );

                float erosion =
                    Noise(
                        p * 4.13
                        - 7.2
                    );

                // 大きな地形を最も強くし、
                // 細かいノイズほど影響を小さくする。
                return
                    broad * .58
                    + continents * .29
                    + erosion * .13;
            }

            // ============================================================
            // Fragment Shader
            // ============================================================
            half4 Frag(Varyings input) : SV_Target
            {
                // --------------------------------------------------------
                // Surface Coordinate
                //
                // Object Space 座標へスケールを適用して
                // プロシージャル地形座標として使用する。
                // --------------------------------------------------------
                float3 p =
                    input.positionOS
                    * _SurfaceScale;

                // 地形全体の基準ノイズ。
                float tone =
                    SurfaceNoise(p);

                // --------------------------------------------------------
                // Finite Difference Gradient
                //
                // テクスチャや追加 Geometry を使わず、
                // Noise の微小な差分から疑似的な表面勾配を求める。
                //
                // この勾配を法線へ加えることで
                // Procedural Bump を作る。
                // --------------------------------------------------------
                float epsilon =
                    .035;

                float3 gradientOS =
                    float3(
                        SurfaceNoise(
                            p + float3(epsilon,0,0)
                        ) - tone,

                        SurfaceNoise(
                            p + float3(0,epsilon,0)
                        ) - tone,

                        SurfaceNoise(
                            p + float3(0,0,epsilon)
                        ) - tone
                    )
                    / epsilon;

                // 元の Mesh 法線。
                float3 geometricNormal =
                    normalize(
                        input.normalWS
                    );

                // Object Space の Gradient を World Space へ変換。
                float3 gradientWS =
                    TransformObjectToWorldDir(
                        gradientOS
                    );

                // --------------------------------------------------------
                // Gradient から法線方向成分を除去する。
                //
                // 表面の接線方向だけを残すことで、
                // 不自然な法線変形を抑える。
                // --------------------------------------------------------
                gradientWS -=
                    geometricNormal
                    * dot(
                        gradientWS,
                        geometricNormal
                    );

                // --------------------------------------------------------
                // Procedural Normal
                //
                // 元の Mesh 法線から Noise Gradient を引いて、
                // 擬似的な凹凸法線を生成する。
                // --------------------------------------------------------
                float3 normalWS =
                    normalize(
                        geometricNormal
                        - gradientWS
                        * _BumpStrength
                    );

                // Fragment から Camera 方向へのベクトル。
                float3 viewWS =
                    GetWorldSpaceNormalizeViewDir(
                        input.positionWS
                    );

                // --------------------------------------------------------
                // Broad Tone
                //
                // 大規模地形ノイズを
                // .70〜1.14 の明暗レンジへ変換する。
                // --------------------------------------------------------
                float broadTone =
                    lerp(
                        .70,
                        1.14,
                        smoothstep(
                            .12,
                            .88,
                            tone
                        )
                    );

                // --------------------------------------------------------
                // Fine Tone
                //
                // より高周波の Noise を追加し、
                // 表面へ細かな色ムラを加える。
                // --------------------------------------------------------
                float fineTone =
                    Noise(
                        p * 7.1
                        + 4.7
                    ) - .5;

                // ========================================================
                // URP Surface Data
                // ========================================================
                SurfaceData surface =
                    (SurfaceData)0;

                // --------------------------------------------------------
                // Albedo
                //
                // Base Color
                // × 大規模な明暗変化
                // × 細かな表面ムラ
                // --------------------------------------------------------
                surface.albedo =
                    _BaseColor.rgb

                    * lerp(
                        1,
                        broadTone,
                        _SurfaceVariation
                    )

                    * (
                        1
                        + fineTone
                        * _SurfaceVariation
                        * .06
                    );

                // 金属度。
                surface.metallic =
                    _Metallic;

                // --------------------------------------------------------
                // Smoothness
                //
                // tone に応じて表面の粗さを変化させる。
                //
                // 明るい / 高い領域と暗い / 低い領域で
                // 反射の質感を微妙に変える。
                // --------------------------------------------------------
                surface.smoothness =
                    saturate(
                        _Smoothness
                        + (tone - .5)
                        * _RoughnessVariation
                    );

                // Tangent Space Normal Map は使用しない。
                // World Space 側で既に Procedural Normal を作成している。
                surface.normalTS =
                    float3(0, 0, 1);

                // --------------------------------------------------------
                // Procedural Occlusion
                //
                // 暗い Noise 領域を少しだけ Occlusion させることで
                // 地形の奥行きを補強する。
                // --------------------------------------------------------
                surface.occlusion =
                    lerp(
                        1,
                        .82,
                        saturate(
                            (.48 - tone)
                            * _SurfaceVariation
                        )
                    );

                // Opaque Material。
                surface.alpha =
                    1;

                // ========================================================
                // Atmospheric Rim
                //
                // 惑星の輪郭部分だけに弱い大気光を加える。
                //
                // 均一な Halo ではなく、
                // Directional Rim を掛けて片側を強くすることで
                // 写真的なライティングに近づける。
                // ========================================================

                // --------------------------------------------------------
                // Fresnel
                //
                // Camera と法線が直交する輪郭部分ほど値が高くなる。
                // --------------------------------------------------------
                float fresnel =
                    pow(
                        1
                        - saturate(
                            dot(
                                normalWS,
                                viewWS
                            )
                        ),
                        _RimPower
                    );

                // --------------------------------------------------------
                // Directional Rim
                //
                // 固定方向ベクトルとの Dot Product を使い、
                // 惑星全周へ均等な Halo が出るのを防ぐ。
                // --------------------------------------------------------
                float directionalRim =
                    lerp(
                        .18,
                        1,
                        saturate(
                            dot(
                                normalWS,
                                normalize(
                                    float3(
                                        -.45,
                                        .55,
                                        -.7
                                    )
                                )
                            ) * .5 + .5
                        )
                    );

                // --------------------------------------------------------
                // Emission
                //
                // Shadow Floor:
                //   影側が完全な黒へ潰れるのを防ぐ。
                //
                // Atmospheric Rim:
                //   Fresnel × Directional Rim で輪郭だけを発光させる。
                // --------------------------------------------------------
                surface.emission =
                    _EmissionColor.rgb

                    + _RimColor.rgb
                    * fresnel
                    * directionalRim
                    * _RimStrength;

                // ========================================================
                // URP Lighting Input
                // ========================================================
                InputData lighting =
                    (InputData)0;

                // World Space 座標。
                lighting.positionWS =
                    input.positionWS;

                // Procedural Bump 適用後の World Space 法線。
                lighting.normalWS =
                    normalWS;

                // Camera 方向。
                lighting.viewDirectionWS =
                    viewWS;

                // Main Light Shadow 用座標。
                lighting.shadowCoord =
                    TransformWorldToShadowCoord(
                        input.positionWS
                    );

                // Spherical Harmonics による環境光。
                lighting.bakedGI =
                    SampleSH(
                        normalWS
                    );

                // Additional Vertex Lighting。
                lighting.vertexLighting =
                    VertexLighting(
                        input.positionWS,
                        normalWS
                    );

                // Screen Space UV。
                lighting.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(
                        input.positionCS
                    );

                // Shadow Mask。
                lighting.shadowMask =
                    half4(
                        1,
                        1,
                        1,
                        1
                    );

                // ========================================================
                // URP Standard PBR
                //
                // Base Color
                // Metallic
                // Smoothness
                // Normal
                // GI
                // Shadows
                // Reflection Probe
                // Additional Lights
                // Emission
                //
                // をまとめて計算する。
                // ========================================================
                return UniversalFragmentPBR(
                    lighting,
                    surface
                );
            }

            ENDHLSL
        }

        // ================================================================
        // URP Lit Shader の既存 Pass を再利用。
        //
        // ShadowCaster:
        //   他のオブジェクトへ影を落とす。
        //
        // DepthOnly:
        //   Camera Depth Texture 生成用。
        //
        // DepthNormals:
        //   Depth + Normal Texture 生成用。
        //
        // MotionVectors:
        //   Motion Blur や Temporal 系エフェクト用。
        // ================================================================
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/MotionVectors"
    }
}