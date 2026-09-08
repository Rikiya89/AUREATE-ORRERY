using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AureateOrrery
{
    /// <summary>
    /// カメラに一時的な写真表現を追加するセットアップクラス。
    ///
    /// 以下の要素を動的に生成する。
    ///
    /// ・Bloom / ACES / Color Grading
    /// ・White Balance
    /// ・Vignette
    /// ・Chromatic Aberration
    /// ・Lens Distortion
    /// ・Film Grain
    /// ・Depth of Field
    /// ・Motion Blur
    /// ・ローカルReflection Probe
    /// ・前景 / 空間中のDust Particles
    ///
    /// 保存済みSceneやVolume Profileは直接変更せず、
    /// CameraControllerが所有する一時リソースとして動作する。
    /// </summary>

    // Owned by CameraController; transient resources never replace the saved scene or profile.
    // CameraControllerによって所有される一時的な写真表現セットアップ。
    // 保存済みSceneやVolume Profileを上書きしない。
    internal sealed class PhotographicSetup : IDisposable
    {
        // ================================================================
        // 基本参照
        // ================================================================

        // 写真表現を適用するカメラ。
        readonly Camera camera;

        // 撮影対象となる天体儀システム。
        readonly CelestialSystem system;

        // このセットアップが生成する一時オブジェクトのルート。
        readonly GameObject root;


        // ================================================================
        // Post Processing
        // ================================================================

        // Runtimeで生成する一時Volume Profile。
        readonly VolumeProfile profile;

        // 毎フレームFocus Distanceを更新するDepth of Field。
        readonly DepthOfField focus;
        readonly ReflectionProbe probe;
        readonly ColorAdjustments grading;
        readonly Material backdropMaterial;
        readonly Mesh backdropMesh;
        readonly Transform backdrop;


        // ================================================================
        // Dust
        // ================================================================

        // 54個のDustを1つにまとめた動的Mesh。
        readonly Mesh dust;

        // Dust専用Material。
        readonly Material dustMaterial;


        // ================================================================
        // Reflection
        // ================================================================

        // スタジオSoftbox風の環境反射を格納するCubemap。
        readonly Cubemap reflection;


        // ================================================================
        // Dust用再利用バッファ
        // ================================================================

        // 各Dust粒子の基準World Position。
        readonly Vector3[] origins =
            new Vector3[54];

        // 54 Quad × 4頂点。
        readonly Vector3[] vertices =
            new Vector3[216];

        // Dust QuadのVertex Color。
        readonly Color[] colors =
            new Color[216];

        // 各Dust粒子のサイズ。
        readonly float[] sizes =
            new float[54];


        // ================================================================
        // Constructor
        // ================================================================

        /// <summary>
        /// カメラとCelestialSystemへ写真表現を追加する。
        ///
        /// 生成されるすべての主要リソースはHideAndDontSaveに設定され、
        /// SceneやProject Assetとして保存されない。
        /// </summary>
        public PhotographicSetup(
            Camera camera,
            CelestialSystem system
        )
        {
            this.camera =
                camera;

            this.system =
                system;


            // ============================================================
            // 一時ルートオブジェクト
            // ============================================================

            root =
                new GameObject(
                    "Photographic presentation (transient)"
                )
                {
                    hideFlags =
                        HideFlags.HideAndDontSave
                };


            // Camera配下へ配置することで、
            // DustなどをCamera基準で扱いやすくする。
            root.transform.SetParent(
                camera.transform,
                false
            );


            // ============================================================
            // 一時Volume Profile
            // ============================================================

            profile =
                ScriptableObject
                    .CreateInstance<VolumeProfile>();

            profile.hideFlags =
                HideFlags.HideAndDontSave;


            // Global Volumeを生成。
            var volume =
                root.AddComponent<Volume>();

            volume.isGlobal =
                true;


            // Scene側の通常Volumeより優先して適用。
            volume.priority =
                20;

            volume.sharedProfile =
                profile;


            // ============================================================
            // Bloom
            // ============================================================

            // Glowを極端に広げず、
            // 金属や天体の強いHighlightだけを穏やかに発光させる。
            var bloom =
                profile.Add<Bloom>(true);

            bloom.threshold.value =
                2.4f;

            bloom.intensity.value =
                .075f;

            bloom.scatter.value =
                .28f;

            bloom.clamp.value =
                5;

            // 高品質Bloom Filterを使用。
            bloom.highQualityFiltering.value =
                true;


            // ============================================================
            // Tonemapping
            // ============================================================

            // HDR Highlightを映画的に圧縮するACESを使用。
            profile.Add<Tonemapping>(true)
                .mode.value =
                TonemappingMode.ACES;


            // ============================================================
            // Color Adjustments
            // ============================================================

            grading =
                profile.Add<ColorAdjustments>(true);


            // Lift fine brass detail at mobile playback sizes while retaining ACES highlights.
            grading.postExposure.value =
                .25f;

            grading.contrast.value =
                4;


            // 彩度を少し落として、
            // 派手なCG感を抑えた写真表現へ寄せる。
            grading.saturation.value =
                -9;


            // ============================================================
            // White Balance
            // ============================================================

            var white =
                profile.Add<WhiteBalance>(true);


            // わずかに冷たい方向へ色温度を調整。
            white.temperature.value =
                -3;

            white.tint.value =
                1;


            // ============================================================
            // Vignette
            // ============================================================

            var vignette =
                profile.Add<Vignette>(true);


            // 周辺を軽く暗くし、
            // 中央の天体儀へ視線を集中させる。
            vignette.intensity.value =
                .13f;

            vignette.smoothness.value =
                .65f;


            // ============================================================
            // Chromatic Aberration
            // ============================================================

            // ごく弱い色収差を加える。
            // 強くしすぎずレンズ由来の質感だけを追加する。
            profile
                .Add<ChromaticAberration>(true)
                .intensity.value =
                .008f;


            // ============================================================
            // Lens Distortion
            // ============================================================

            // 非常に弱い樽型方向のLens Distortion。
            profile
                .Add<LensDistortion>(true)
                .intensity.value =
                -.012f;


            // ============================================================
            // Film Grain
            // ============================================================

            var grain =
                profile.Add<FilmGrain>(true);

            grain.type.value =
                FilmGrainLookup.Thin1;


            // 強すぎない細粒Film Grain。
            grain.intensity.value =
                .025f;

            grain.response.value =
                .85f;


            // ============================================================
            // Depth of Field
            // ============================================================

            focus =
                profile.Add<DepthOfField>(true);


            // 写真的なBokeh DOFを使用。
            focus.mode.value =
                DepthOfFieldMode.Bokeh;


            // F2.8相当。
            focus.aperture.value =
                2.8f;


            // 7枚羽根の絞り形状。
            focus.bladeCount.value =
                7;


            // ============================================================
            // Motion Blur
            // ============================================================

            var motion =
                profile.Add<MotionBlur>(true);


            // CameraとObject双方の動きをBlurへ反映。
            motion.mode.value =
                MotionBlurMode.CameraAndObjects;

            motion.quality.value =
                MotionBlurQuality.Medium;


            // 動きを残しつつ、
            // ディテールが失われない程度に抑える。
            motion.intensity.value =
                .06f;

            motion.clamp.value =
                .008f;


            // ============================================================
            // Camera設定
            // ============================================================

            // Emission / Bloom表現のためHDRを有効化。
            camera.allowHDR =
                true;


            // 通常よりさらに暗い黒背景。
            camera.backgroundColor =
                new Color(
                    .003f,
                    .0035f,
                    .0045f
                );


            // URP Post Processingを有効化。
            camera
                .GetUniversalAdditionalCameraData()
                .renderPostProcessing =
                true;


            // Depth of Fieldなどに必要なDepth Textureを有効化。
            camera
                .GetUniversalAdditionalCameraData()
                .requiresDepthTexture =
                true;


            // ============================================================
            // Studio Reflection Cubemap
            // ============================================================

            // A restrained studio environment gives rough metal broad reflections in a black scene.
            // 黒背景の中でも粗い金属面に幅広いReflectionが出るよう、
            // 控えめなスタジオ照明環境をCubemapとして生成する。

            // Local probe only: no global skybox mutation or per-frame cubemap capture.
            // Global Skyboxは変更せず、
            // ローカルReflection Probeだけへ適用する。
            //
            // 毎フレームCubemapをCaptureする処理も行わない。
            reflection =
                new Cubemap(
                    64,
                    TextureFormat.RGBAHalf,
                    true
                )
                {
                    hideFlags =
                        HideFlags.HideAndDontSave
                };


            reflection.name =
                "Dim photographic softboxes";


            // Cubemapの6面をCPU上で生成。
            for (
                int face = 0;
                face < 6;
                face++
            )
            {
                // 64 × 64ピクセル。
                var pixels =
                    new Color[64 * 64];


                for (
                    int y = 0;
                    y < 64;
                    y++
                )
                {
                    for (
                        int x = 0;
                        x < 64;
                        x++
                    )
                    {
                        // Cubemap面内座標を
                        // おおよそ -1〜1へ正規化。
                        float u =
                            (x + .5f) / 32 - 1;

                        float v =
                            (y + .5f) / 32 - 1;


                        // =================================================
                        // Cubemap Face → Direction Vector
                        // =================================================

                        // 各Cubemap面に応じて、
                        // ピクセル位置を3D方向ベクトルへ変換する。
                        Vector3 direction =
                            face switch
                            {
                                0 => new Vector3(
                                    1,
                                    -v,
                                    -u
                                ),

                                1 => new Vector3(
                                    -1,
                                    -v,
                                    u
                                ),

                                2 => new Vector3(
                                    u,
                                    1,
                                    v
                                ),

                                3 => new Vector3(
                                    u,
                                    -1,
                                    -v
                                ),

                                4 => new Vector3(
                                    u,
                                    -v,
                                    1
                                ),

                                _ => new Vector3(
                                    -u,
                                    -v,
                                    -1
                                )
                            };


                        direction.Normalize();


                        // =================================================
                        // Warm Softbox
                        // =================================================

                        // 暖色Softbox方向とのDot Productから
                        // 強い指向性Highlightを作る。
                        float warm =
                            Mathf.Pow(
                                Mathf.Max(
                                    0,
                                    Vector3.Dot(
                                        direction,
                                        new Vector3(
                                            -.5f,
                                            .65f,
                                            -.6f
                                        ).normalized
                                    )
                                ),
                                10
                            );


                        // =================================================
                        // Cold Softbox
                        // =================================================

                        // 反対側には冷色系のSoftboxを配置。
                        float cold =
                            Mathf.Pow(
                                Mathf.Max(
                                    0,
                                    Vector3.Dot(
                                        direction,
                                        new Vector3(
                                            .6f,
                                            .2f,
                                            .7f
                                        ).normalized
                                    )
                                ),
                                16
                            );


                        // =================================================
                        // Cubemap Color
                        // =================================================

                        // 暗い環境色
                        // +
                        // 暖色Softbox
                        // +
                        // 冷色Softbox
                        //
                        // の組み合わせで簡易スタジオ環境を生成。
                        pixels[y * 64 + x] =
                            new Color(
                                .020f,
                                .024f,
                                .030f
                            )
                            +
                            new Color(
                                .48f,
                                .4f,
                                .30f
                            )
                            * warm
                            +
                            new Color(
                                .10f,
                                .14f,
                                .21f
                            )
                            * cold;
                    }
                }


                // 現在のCubemap面へPixelデータを設定。
                reflection.SetPixels(
                    pixels,
                    (CubemapFace)face
                );
            }


            // Mipmapも生成してCubemapを確定。
            // makeNoLongerReadable=true。
            reflection.Apply(
                true,
                true
            );


            // ============================================================
            // Local Reflection Probe
            // ============================================================

            probe =
                root.AddComponent<ReflectionProbe>();


            // Realtime Captureではなく、
            // 生成したCubemapを直接使用するCustom Mode。
            probe.mode =
                ReflectionProbeMode.Custom;


            probe.customBakedTexture =
                reflection;


            // 天体儀全体を十分覆うサイズ。
            probe.size =
                Vector3.one
                * 150
                * system.masterScale;


            probe.intensity =
                1;


            // 単純な広域Reflectionなので
            // Box Projectionは使用しない。
            probe.boxProjection =
                false;


            Shader backdropShader = Resources.Load<Shader>("CelestialBackdrop");
            if (backdropShader)
            {
                backdropMaterial = new Material(backdropShader) { hideFlags = HideFlags.HideAndDontSave };
                backdropMesh = new Mesh { name = "Celestial backdrop", hideFlags = HideFlags.HideAndDontSave };
                backdropMesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
                backdropMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                backdropMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                backdropMesh.RecalculateBounds();
                var backgroundObject = new GameObject("Celestial background (transient)") { hideFlags = HideFlags.HideAndDontSave };
                backdrop = backgroundObject.transform;
                backdrop.SetParent(root.transform, false);
                backgroundObject.AddComponent<MeshFilter>().sharedMesh = backdropMesh;
                var backdropRenderer = backgroundObject.AddComponent<MeshRenderer>();
                backdropRenderer.sharedMaterial = backdropMaterial;
                backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
                backdropRenderer.receiveShadows = false;
            }

            // ============================================================
            // Dust Mesh
            // ============================================================

            dust =
                new Mesh
                {
                    name =
                        "Sparse photographic dust",

                    hideFlags =
                        HideFlags.HideAndDontSave
                };


            // 毎フレーム頂点を更新するためDynamic Meshとして設定。
            dust.MarkDynamic();


            // ============================================================
            // Dust Material
            // ============================================================

            // Resources/PhotographicDust.shader を読み込む。
            dustMaterial =
                new Material(
                    Resources.Load<Shader>(
                        "PhotographicDust"
                    )
                )
                {
                    hideFlags =
                        HideFlags.HideAndDontSave
                };


            // ============================================================
            // Dust GameObject
            // ============================================================

            var go =
                new GameObject(
                    "Foreground and suspended dust"
                )
                {
                    hideFlags =
                        HideFlags.HideAndDontSave
                };


            // root自体がCameraの子なので、
            // DustもCamera基準の構造になる。
            go.transform.SetParent(
                root.transform,
                false
            );


            // Dust Meshを設定。
            go
                .AddComponent<MeshFilter>()
                .sharedMesh =
                dust;


            var renderer =
                go.AddComponent<MeshRenderer>();

            renderer.sharedMaterial =
                dustMaterial;


            // Dustは影を落とさない。
            renderer.shadowCastingMode =
                ShadowCastingMode.Off;


            // ============================================================
            // Dust Random
            // ============================================================

            // CelestialSystemのseedから派生した固定Seed。
            //
            // +391することでStar Fieldとは
            // 異なるランダムパターンを生成する。
            var random =
                new System.Random(
                    system.seed + 391
                );


            // 54 Quad × 4頂点。
            var uv =
                new Vector2[216];


            // 54 Quad × 6 Triangle Index。
            var indices =
                new int[324];


            // ============================================================
            // 54個のDustを生成
            // ============================================================

            for (
                int i = 0;
                i < 54;
                i++
            )
            {
                // 0〜1のfloat Randomを取得する
                // ローカルヘルパー関数。
                float R() =>
                    (float)random.NextDouble();


                // ========================================================
                // Dust Depth
                // ========================================================

                // 最初の6個:
                // Camera直前に配置するForeground Dust。
                //
                // 残り48個:
                // 天体儀付近から奥側へ配置するSuspended Dust。
                float depth =
                    i < 6
                        ? Mathf.Lerp(
                            .7f,
                            1.8f,
                            R()
                        )
                        : Vector3.Distance(
                            camera.transform.position,
                            system.transform.position
                        )
                        +
                        Mathf.Lerp(
                            -3,
                            5,
                            R()
                        )
                        * system.masterScale;


                // ========================================================
                // Camera Frustum Height
                // ========================================================

                // 現在のDepth地点で画面に映る
                // Frustumの半分の高さを計算。
                float halfHeight =
                    Mathf.Tan(
                        camera.fieldOfView
                        * Mathf.Deg2Rad
                        * .5f
                    )
                    * depth;


                // ========================================================
                // Dust Origin
                // ========================================================

                // Camera Frustumより少し広い1.3倍範囲へ
                // Dustをランダム配置する。
                origins[i] =
                    camera.transform.TransformPoint(
                        new Vector3(
                            (R() * 2 - 1)
                            * halfHeight
                            * camera.aspect
                            * 1.3f,

                            (R() * 2 - 1)
                            * halfHeight
                            * 1.3f,

                            depth
                        )
                    );


                // ========================================================
                // Dust Size
                // ========================================================

                // Foreground Dustは大きく、
                // Suspended Dustは小さくする。
                sizes[i] =
                    i < 6
                        ? Mathf.Lerp(
                            .025f,
                            .06f,
                            R()
                        )
                        : Mathf.Lerp(
                            .003f,
                            .015f,
                            R()
                        )
                        * system.masterScale;


                // ========================================================
                // Dust Color
                // ========================================================

                // 少し暖色寄りの灰色。
                //
                // Foreground Dustは極端に薄くして、
                // レンズ前のボケに近い表現へする。
                Color color =
                    new Color(
                        .64f,
                        .59f,
                        .48f,
                        i < 6
                            ? .018f
                            : Mathf.Lerp(
                                .012f,
                                .06f,
                                R()
                            )
                    );


                // 現在Quadの頂点 / Triangle開始Index。
                int n =
                    i * 4;

                int t =
                    i * 6;


                // ========================================================
                // UV
                // ========================================================

                uv[n] =
                    new Vector2(
                        0,
                        0
                    );

                uv[n + 1] =
                    new Vector2(
                        1,
                        0
                    );

                uv[n + 2] =
                    new Vector2(
                        1,
                        1
                    );

                uv[n + 3] =
                    new Vector2(
                        0,
                        1
                    );


                // Quadの4頂点へ同じColorを設定。
                for (
                    int j = 0;
                    j < 4;
                    j++
                )
                {
                    colors[n + j] =
                        color;
                }


                // ========================================================
                // Triangle
                // ========================================================

                // Triangle 1
                indices[t] =
                    n;

                indices[t + 1] =
                    n + 1;

                indices[t + 2] =
                    n + 2;


                // Triangle 2
                indices[t + 3] =
                    n;

                indices[t + 4] =
                    n + 2;

                indices[t + 5] =
                    n + 3;
            }


            // ============================================================
            // Dust Mesh初期化
            // ============================================================

            dust.vertices =
                vertices;

            dust.uv =
                uv;

            dust.colors =
                colors;

            dust.triangles =
                indices;
        }


        // ================================================================
        // Update
        // ================================================================

        /// <summary>
        /// カメラFocusとDust Animationを更新する。
        ///
        /// CameraController側から毎フレーム呼び出される想定。
        /// </summary>
        public void Update()
        {
            if (backdrop)
            {
                float depth = camera.farClipPlane * .95f;
                float halfHeight = depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                backdrop.localPosition = new Vector3(0, 0, depth);
                backdrop.localScale = new Vector3(halfHeight * camera.aspect * 1.01f, halfHeight * 1.01f, 1);
                backdropMaterial.SetFloat("_Aspect", camera.aspect);
                backdropMaterial.SetFloat("_Intensity", system.nebulaBrightness);
                backdropMaterial.SetFloat("_Stars", system.backgroundStarBrightness);
                backdropMaterial.SetFloat("_Seed", system.seed);
            }
            probe.intensity = system.reflectionStrength;
            grading.postExposure.value = system.exposure;
            // ============================================================
            // Depth of Field Focus
            // ============================================================

            // Cameraから天体儀中心までのベクトルを
            // Camera Forwardへ射影し、
            // 実際のHero Planeまでの距離をFocus Distanceとして設定。
            focus.focusDistance.value =
                Vector3.Dot(
                    system.transform.position
                    - camera.transform.position,

                    camera.transform.forward
                );


            // URP optical distances use scene metres; focus tracks the actual hero plane.
            // URPの光学距離はScene内のMeter単位を使用する。
            // Focusは実際のHero Plane位置へ追従させる。

            // CameraのFocal LengthとDOFのFocal Lengthを同期。
            focus.focalLength.value =
                camera.focalLength;


            // 現在のLoop Phase。
            float q =
                system.Phase;


            // ============================================================
            // Dust Animation
            // ============================================================

            for (
                int i = 0;
                i < 54;
                i++
            )
            {
                // ========================================================
                // Perlin Noise Motion X
                // ========================================================

                // Phaseを入力へ使い、
                // Loop中に緩やかに変化するNoiseを作る。
                float a =
                    Mathf.PerlinNoise(
                        i * .73f
                        +
                        Mathf.Cos(q) * .18f,

                        Mathf.Sin(q) * .18f
                        +
                        3
                    )
                    -
                    .5f;


                // ========================================================
                // Perlin Noise Motion Y
                // ========================================================

                // Xとは異なるFrequency / Offsetを使用し、
                // 単純な直線移動を避ける。
                float b =
                    Mathf.PerlinNoise(
                        i * .53f
                        +
                        9
                        +
                        Mathf.Sin(q) * .18f,

                        Mathf.Cos(q) * .18f
                        +
                        7
                    )
                    -
                    .5f;


                // ========================================================
                // Dust Position
                // ========================================================

                // World Spaceに保存されているOriginへ
                // Noise Offsetを加える。
                //
                // その後Camera Local Spaceへ変換して、
                // Cameraに対して正面を向くQuadとして構築する。
                Vector3 p =
                    camera.transform.InverseTransformPoint(
                        origins[i]
                        +
                        new Vector3(
                            a,
                            b,
                            0
                        )
                        *
                        (
                            i < 6
                                ? .12f
                                : .18f
                        )
                        *
                        system.masterScale
                    );


                // Dustサイズ。
                float s =
                    sizes[i];


                // 現在Quadの最初の頂点Index。
                int n =
                    i * 4;


                // ========================================================
                // Camera-facing Quad
                // ========================================================

                // Camera Local Space上のXY平面へQuadを構築。
                //
                // Yサイズを0.8倍して、
                // 完全な正円ではなく少し横長のDust形状にする。
                vertices[n] =
                    p
                    +
                    new Vector3(
                        -s,
                        -s * .8f,
                        0
                    );

                vertices[n + 1] =
                    p
                    +
                    new Vector3(
                        s,
                        -s * .8f,
                        0
                    );

                vertices[n + 2] =
                    p
                    +
                    new Vector3(
                        s,
                        s * .8f,
                        0
                    );

                vertices[n + 3] =
                    p
                    +
                    new Vector3(
                        -s,
                        s * .8f,
                        0
                    );
            }


            // ============================================================
            // Dynamic Mesh更新
            // ============================================================

            // Vertex Positionのみ更新。
            dust.vertices =
                vertices;


            // DustがFrustum Cullingで不正に消えないよう
            // Boundsを再計算する。
            dust.RecalculateBounds();
        }


        // ================================================================
        // Resource Release Helper
        // ================================================================

        /// <summary>
        /// Play Mode / Edit Modeに応じて
        /// 適切なDestroy方法を使用してObjectを破棄する。
        /// </summary>
        static void Release(
            UnityEngine.Object value
        )
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(
                    value
                );
            else
                UnityEngine.Object.DestroyImmediate(
                    value
                );
        }


        // ================================================================
        // IDisposable
        // ================================================================

        /// <summary>
        /// PhotographicSetupが生成した
        /// 一時リソースをすべて解放する。
        /// </summary>
        public void Dispose()
        {
            // Root GameObject。
            Release(
                root
            );


            Release(backdropMaterial);
            Release(backdropMesh);

            // Dust Mesh。
            Release(
                dust
            );


            // Dust Material。
            Release(
                dustMaterial
            );


            // Reflection Cubemap。
            Release(
                reflection
            );


            // ============================================================
            // Volume Components
            // ============================================================

            // Runtime生成した各Post Processing Componentを破棄。
            foreach (
                var component in profile.components
            )
            {
                Release(
                    component
                );
            }


            // 最後にVolumeProfile本体を破棄。
            Release(
                profile
            );
        }
    }
}
