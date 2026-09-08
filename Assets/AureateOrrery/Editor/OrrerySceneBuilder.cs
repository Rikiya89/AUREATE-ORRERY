using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AureateOrrery.Editor
{
    /// <summary>
    /// Aureate Orrery用のUnity Editor拡張。
    ///
    /// 以下の処理をEditorメニューから実行する。
    ///
    /// ・シーンの自動生成
    /// ・カメラ / ライト / Volume設定
    /// ・完全ループの検証
    /// ・縦長プレビュー画像のレンダリング
    /// ・96フレームのモーションスタディ出力
    /// </summary>
    public static class OrrerySceneBuilder
    {
        // ================================================================
        // 生成するUnity Sceneの保存先
        // ================================================================

        const string ScenePath =
            "Assets/AureateOrrery/AureateOrrery.unity";


        // ================================================================
        // Scene生成
        // ================================================================

        /// <summary>
        /// Aureate Orrery用の新しいSceneを自動生成する。
        ///
        /// Menu:
        /// Art > Aureate Orrery > Create Scene
        /// </summary>
        [MenuItem("Art/Aureate Orrery/Create Scene")]
        public static void CreateScene()
        {
            // ============================================================
            // 現在のSceneに未保存変更がある場合の確認
            // ============================================================

            // Batch Modeではダイアログを表示せず、
            // 通常Editorでは現在のSceneを保存するかユーザーへ確認する。
            if (
                !Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
            )
                return;


            // ============================================================
            // 既存生成Sceneの上書き確認
            // ============================================================

            // AureateOrrery.unityがすでに存在する場合、
            // 通常Editorでは上書き確認ダイアログを表示する。
            //
            // Batch Modeでは確認なしで処理を続行する。
            if (
                File.Exists(ScenePath) &&
                !Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "Replace generated scene?",
                    "This replaces only Assets/AureateOrrery/AureateOrrery.unity.",
                    "Replace",
                    "Cancel"
                )
            )
                return;


            // ============================================================
            // 空Scene生成
            // ============================================================

            // 既存Sceneを閉じ、
            // 完全に空のSceneをSingle Modeで作成する。
            var scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single
                );


            // ============================================================
            // Aureate Orrery本体
            // ============================================================

            // CelestialSystemを持つルートGameObjectを生成。
            //
            // AddComponent時にCelestialSystem.OnEnable()が実行され、
            // Rebuild()によって天体儀が自動生成される。
            var system =
                new GameObject("AUREATE ORRERY")
                    .AddComponent<CelestialSystem>();


            // ============================================================
            // Portrait Camera
            // ============================================================

            // 縦長作品用のメインカメラを生成。
            var camera =
                new GameObject("Portrait Camera")
                    .AddComponent<Camera>();

            // Camera.mainから取得できるようMainCameraタグを設定。
            camera.tag =
                "MainCamera";


            // ============================================================
            // Camera基本設定
            // ============================================================

            // 背景は非常に暗い青黒色。
            camera.clearFlags =
                CameraClearFlags.SolidColor;

            camera.backgroundColor =
                new Color(
                    .008f,
                    .008f,
                    .02f
                );


            // Near / Far Clipを設定。
            camera.nearClipPlane =
                .1f;

            camera.farClipPlane =
                120;


            // Bloomや高輝度Emissionを維持するためHDRを有効化。
            camera.allowHDR =
                true;


            // ============================================================
            // URP Camera追加設定
            // ============================================================

            var data =
                camera.GetUniversalAdditionalCameraData();


            // VolumeベースのPost Processingを有効にする。
            data.renderPostProcessing =
                true;


            // SMAAを使用してエッジのジャギーを抑える。
            data.antialiasing =
                AntialiasingMode
                    .SubpixelMorphologicalAntiAliasing;


            // ============================================================
            // Audio Listener
            // ============================================================

            // UnityのMain Cameraとして通常必要になる
            // AudioListenerを追加する。
            camera.gameObject
                .AddComponent<AudioListener>();


            // ============================================================
            // CameraController
            // ============================================================

            // 天体儀全体がPortrait構図へ収まるよう、
            // CameraControllerを追加して自動フレーミングする。
            var framing =
                camera.gameObject
                    .AddComponent<CameraController>();

            framing.system =
                system;

            framing.Frame();


            // ============================================================
            // Directional Lights
            // ============================================================

            // 暖色系のメインライト。
            Light(
                "Warm key",
                new Color(
                    1,
                    .88f,
                    .72f
                ),
                2.6f,
                new Vector3(
                    35,
                    -35,
                    0
                )
            );


            // 青白いリムライト。
            // 真鍮と冷たい天体光の色差を作る。
            Light(
                "Silver rim",
                new Color(
                    .55f,
                    .72f,
                    1
                ),
                .45f,
                new Vector3(
                    -25,
                    145,
                    0
                )
            );


            // ============================================================
            // 中央Point Light
            // ============================================================

            // 中央天体の外側から当てる、局所的なWarm Hero Light。
            // 球の内部へ置くと表面を照らせないため、Camera側かつ斜め上へ配置する。
            var point =
                new GameObject("Core illumination")
                    .AddComponent<Light>();

            point.type =
                LightType.Point;

            point.color =
                new Color(
                    .95f,
                    .72f,
                    .48f
                );

            point.intensity =
                3.2f;

            point.range =
                1.35f;

            point.transform.position =
                new Vector3(
                    -.42f,
                    .34f,
                    -.52f
                );


            // ============================================================
            // Ambient Light
            // ============================================================

            // Skyboxではなく均一なFlat Ambientを使用。
            RenderSettings.ambientMode =
                AmbientMode.Flat;

            // 完全な黒潰れを避ける程度の弱い環境光。
            RenderSettings.ambientLight =
                new Color(
                    .018f,
                    .022f,
                    .03f
                );


            // ============================================================
            // Global Volume
            // ============================================================

            // Bloom / Tonemapping / Vignette用のGlobal Volume。
            var volume =
                new GameObject(
                    "Restrained bloom + tonal response"
                )
                .AddComponent<Volume>();

            volume.isGlobal =
                true;


            // Volume Profile保存先。
            string profilePath =
                "Assets/AureateOrrery/OrreryVolume.asset";


            // 既存Profileが存在する場合は再利用する。
            var profile =
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                    profilePath
                );


            // ============================================================
            // Volume Profile初回生成
            // ============================================================

            if (!profile)
            {
                // 新しいVolumeProfileを生成。
                profile =
                    ScriptableObject
                        .CreateInstance<VolumeProfile>();

                AssetDatabase.CreateAsset(
                    profile,
                    profilePath
                );


                // ========================================================
                // Bloom
                // ========================================================

                // 強すぎないBloom。
                // 発光部分のみを柔らかく広げる。
                var bloom =
                    profile.Add<Bloom>();

                bloom.threshold.Override(
                    1.1f
                );

                bloom.intensity.Override(
                    .32f
                );

                bloom.scatter.Override(
                    .58f
                );


                // ========================================================
                // Tonemapping
                // ========================================================

                // HDRのハイライトを自然に圧縮するためACESを使用。
                var tone =
                    profile.Add<Tonemapping>();

                tone.mode.Override(
                    TonemappingMode.ACES
                );


                // ========================================================
                // Vignette
                // ========================================================

                // 画面端をわずかに暗くし、
                // 視線を中央の天体儀へ誘導する。
                var vignette =
                    profile.Add<Vignette>();

                vignette.intensity.Override(
                    .16f
                );

                vignette.smoothness.Override(
                    .6f
                );


                // VolumeComponentもProfile Assetの
                // Sub Assetとして登録する。
                foreach (
                    var component in profile.components
                )
                    AssetDatabase.AddObjectToAsset(
                        component,
                        profile
                    );
            }


            // Global VolumeへProfileを設定。
            volume.sharedProfile =
                profile;


            // ============================================================
            // Scene保存
            // ============================================================

            EditorSceneManager.SaveScene(
                scene,
                ScenePath
            );

            AssetDatabase.SaveAssets();


            // Hierarchy上でCelestialSystemを選択状態にする。
            Selection.activeGameObject =
                system.gameObject;


            // 自動処理用にも検出しやすい固定ログ。
            Debug.Log(
                "AUREATE_ORRERY_SCENE_CREATED " +
                ScenePath
            );
        }


        // ================================================================
        // Directional Light生成ヘルパー
        // ================================================================

        /// <summary>
        /// Directional Lightを生成する。
        /// </summary>
        static void Light(
            string label,
            Color color,
            float intensity,
            Vector3 rotation
        )
        {
            // Directional Lightを持つGameObjectを作成。
            var light =
                new GameObject(label)
                    .AddComponent<Light>();

            light.type =
                LightType.Directional;

            light.color =
                color;

            light.intensity =
                intensity;


            // この作品ではリアルタイムシャドウを使用しない。
            light.shadows =
                label == "Warm key" ? LightShadows.Soft : LightShadows.None;


            // Euler角でライト方向を設定。
            light.transform.rotation =
                Quaternion.Euler(
                    rotation
                );
        }


        // ================================================================
        // Loop Validation
        // ================================================================

        /// <summary>
        /// 天体儀アニメーションが完全にループするか検証する。
        ///
        /// 以下を検証:
        ///
        /// ・Transformの開始 / 終了状態
        /// ・軌道位置の連続性
        /// ・軌道速度の連続性
        /// ・Trail Meshの開始 / 終了状態
        /// ・49個の時間サンプルでNaN / Infinityがないこと
        /// ・全Mesh頂点がFiniteであること
        /// ・Shaderにコンパイルエラーがないこと
        /// </summary>
        [MenuItem("Art/Aureate Orrery/Validate Loop")]
        public static void ValidateLoop()
        {
            // ============================================================
            // CelestialSystem取得
            // ============================================================

            var system =
                UnityEngine.Object
                    .FindFirstObjectByType<CelestialSystem>();


            // Sceneが開かれていない、
            // またはCelestialSystemが存在しない場合はエラー。
            if (!system)
                throw new InvalidOperationException(
                    "Open the Aureate Orrery scene first."
                );


            // ============================================================
            // Phase 0のTransform状態を保存
            // ============================================================

            system.Evaluate(0);


            // CelestialSystem配下の全Transformを取得。
            var transforms =
                system.GetComponentsInChildren<Transform>();


            // Phase 0の状態保存用配列。
            var positions =
                new Vector3[
                    transforms.Length
                ];

            var rotations =
                new Quaternion[
                    transforms.Length
                ];

            var scales =
                new Vector3[
                    transforms.Length
                ];


            // 全TransformのLocal状態を記録。
            for (
                int i = 0;
                i < transforms.Length;
                i++
            )
            {
                positions[i] =
                    transforms[i].localPosition;

                rotations[i] =
                    transforms[i].localRotation;

                scales[i] =
                    transforms[i].localScale;
            }


            // ============================================================
            // Phase 1との比較
            // ============================================================

            // normalizedTime = 1は一周後なので、
            // Phase 0と完全に一致する必要がある。
            system.Evaluate(1);


            for (
                int i = 0;
                i < transforms.Length;
                i++
            )
                if (
                    Vector3.Distance(
                        positions[i],
                        transforms[i].localPosition
                    ) > 1e-5f ||

                    Quaternion.Angle(
                        rotations[i],
                        transforms[i].localRotation
                    ) > 1e-3f ||

                    Vector3.Distance(
                        scales[i],
                        transforms[i].localScale
                    ) > 1e-5f
                )
                    throw new Exception(
                        "Loop mismatch: " +
                        transforms[i].name
                    );


            // ============================================================
            // 軌道シームの位置・速度検証
            // ============================================================

            // 数値微分用の微小時間。
            const float epsilon =
                .0001f;


            // Harmonic / Lissajous / Epicycleの
            // 3軌道すべてを検証する。
            for (
                int orbit = 0;
                orbit < 3;
                orbit++
            )
            {
                // ループ開始位置。
                var a =
                    CelestialTrails.Position(
                        orbit,
                        0
                    );


                // 2π後のループ終了位置。
                var b =
                    CelestialTrails.Position(
                        orbit,
                        MathematicalOrbit.Tau
                    );


                // 開始地点付近の速度を前進差分で計算。
                var startVelocity =
                    (
                        CelestialTrails.Position(
                            orbit,
                            epsilon
                        ) -
                        a
                    ) /
                    epsilon;


                // 終了地点付近の速度を後退差分で計算。
                var endVelocity =
                    (
                        b -
                        CelestialTrails.Position(
                            orbit,
                            MathematicalOrbit.Tau -
                            epsilon
                        )
                    ) /
                    epsilon;


                // 開始位置と終了位置、
                // および速度方向が連続しているか確認する。
                if (
                    Vector3.Distance(
                        a,
                        b
                    ) > 1e-4f ||

                    Vector3.Distance(
                        startVelocity,
                        endVelocity
                    ) > .05f
                )
                    throw new Exception(
                        "Orbital seam continuity failed"
                    );


                // Phaseを変化させてもマーカーが
                // ほとんど動いていない場合は異常と判断する。
                if (
                    Vector3.Distance(
                        a,
                        CelestialTrails.Position(
                            orbit,
                            1
                        )
                    ) < .1f
                )
                    throw new Exception(
                        "Orbital marker did not travel"
                    );
            }


            // ============================================================
            // Trail Mesh取得
            // ============================================================

            // 名前から解析的Trail Meshを持つMeshFilterを検索する。
            var trailFilter =
                Array.Find(
                    system.GetComponentsInChildren<MeshFilter>(),
                    f =>
                        f.sharedMesh.name ==
                        "Analytic celestial trails"
                );


            if (!trailFilter)
                throw new Exception(
                    "Trail mesh missing"
                );


            // ============================================================
            // Trail Seam検証
            // ============================================================

            // Phase 0のTrail頂点。
            system.Evaluate(0);

            var trailStart =
                trailFilter.sharedMesh.vertices;


            // Phase 1のTrail頂点。
            system.Evaluate(1);

            var trailEnd =
                trailFilter.sharedMesh.vertices;


            // 全頂点が開始時と終了時で一致するか確認する。
            for (
                int v = 0;
                v < trailStart.Length;
                v++
            )
                if (
                    Vector3.Distance(
                        trailStart[v],
                        trailEnd[v]
                    ) > 1e-5f
                )
                    throw new Exception(
                        "Trail seam mismatch"
                    );


            // ============================================================
            // アニメーション全体のFiniteチェック
            // ============================================================

            // 0〜1のループを48分割し、
            // 両端を含む49地点で検証する。
            for (
                int sample = 0;
                sample <= 48;
                sample++
            )
            {
                system.Evaluate(
                    sample / 48f
                );


                // Transform位置にNaN / Infinityが存在しないか確認。
                foreach (
                    var t in transforms
                )
                    if (
                        !float.IsFinite(
                            t.position.x
                        ) ||
                        !float.IsFinite(
                            t.position.y
                        ) ||
                        !float.IsFinite(
                            t.position.z
                        )
                    )
                        throw new Exception(
                            "Invalid animated transform"
                        );
            }


            // ============================================================
            // Mesh Vertex検証
            // ============================================================

            // 初期Phaseへ戻す。
            system.Evaluate(0);


            // CelestialSystem配下の全Mesh頂点を検証する。
            foreach (
                var filter in
                system.GetComponentsInChildren<MeshFilter>()
            )
                foreach (
                    var vertex in
                    filter.sharedMesh.vertices
                )
                    if (
                        !float.IsFinite(
                            vertex.x
                        ) ||
                        !float.IsFinite(
                            vertex.y
                        ) ||
                        !float.IsFinite(
                            vertex.z
                        )
                    )
                        throw new Exception(
                            "Non-finite mesh vertex"
                        );


            // ============================================================
            // Shader検証
            // ============================================================

            // 使用している主要Shaderが存在し、
            // コンパイルエラーを持っていないか確認する。
            foreach (
                string shaderName in
                new[]
                {
                    "AureateOrrery/AgedMetal",
                    "AureateOrrery/PlanetSurface",
                    "AureateOrrery/CelestialBackdrop",
                    "AureateOrrery/PhotographicDust",
                    "AureateOrrery/CelestialGlow",
                    "Universal Render Pipeline/Lit"
                }
            )
            {
                var shader =
                    Shader.Find(
                        shaderName
                    );


                if (
                    !shader ||
                    ShaderUtil.ShaderHasError(
                        shader
                    )
                )
                    throw new Exception(
                        "Shader error: " +
                        shaderName
                    );
            }


            // 全検証成功。
            Debug.Log(
                "AUREATE_ORRERY_VALIDATED: endpoint transforms, orbital seam velocities, trail seam, 49 animation samples, mesh vertices, shader import"
            );
        }


        // ================================================================
        // Portrait Preview Render
        // ================================================================

        /// <summary>
        /// 1080 × 1920の縦長静止画をレンダリングし、
        /// PNGとしてArtworkPreviewsへ保存する。
        /// </summary>
        [MenuItem("Art/Aureate Orrery/Render Portrait Preview")]
        public static void RenderPreview()
        {
            // レンダリング前に完全ループやShader状態を検証する。
            ValidateLoop();


            // ============================================================
            // Camera取得
            // ============================================================

            var camera =
                Camera.main;


            if (!camera)
                throw new Exception(
                    "Scene camera missing"
                );


            // ============================================================
            // CelestialSystem / CameraController取得
            // ============================================================

            var system =
                UnityEngine.Object
                    .FindFirstObjectByType<CelestialSystem>();

            var framing =
                camera.GetComponent<CameraController>();


            // ============================================================
            // 元Camera状態を保存
            // ============================================================

            var previous =
                camera.targetTexture;

            float oldAspect =
                camera.aspect;


            // ============================================================
            // 1080 × 1920 HDR RenderTexture
            // ============================================================

            // ARGBHalfを使ってHDRレンダリングを保持する。
            var target =
                new RenderTexture(
                    1080,
                    1920,
                    24,
                    RenderTextureFormat.ARGBHalf
                );

            target.Create();


            // PNG保存用Texture。
            var image =
                new Texture2D(
                    1080,
                    1920,
                    TextureFormat.RGB24,
                    false
                );


            // 現在のActive RenderTextureを保存。
            var active =
                RenderTexture.active;


            try
            {
                // ========================================================
                // Portrait Render設定
                // ========================================================

                camera.aspect =
                    1080f / 1920;

                camera.targetTexture =
                    target;


                // ループ開始地点を静止画として使用。
                system.Evaluate(0);


                // Portrait比率に合わせて再フレーミング。
                framing.Frame();


                // ========================================================
                // URP Render
                // ========================================================

                RenderPipeline.SubmitRenderRequest(
                    camera,
                    new UniversalRenderPipeline
                        .SingleCameraRequest
                    {
                        destination =
                            target
                    }
                );


                // ========================================================
                // GPU → CPU Texture転送
                // ========================================================

                RenderTexture.active =
                    target;

                image.ReadPixels(
                    new Rect(
                        0,
                        0,
                        1080,
                        1920
                    ),
                    0,
                    0
                );

                image.Apply();


                // ========================================================
                // PNG保存
                // ========================================================

                Directory.CreateDirectory(
                    "ArtworkPreviews"
                );

                File.WriteAllBytes(
                    "ArtworkPreviews/AureateOrrery.png",
                    image.EncodeToPNG()
                );


                Debug.Log(
                    "AUREATE_ORRERY_PREVIEW_RENDERED ArtworkPreviews/AureateOrrery.png"
                );
            }
            finally
            {
                // ========================================================
                // Camera / Render状態を必ず元に戻す
                // ========================================================

                RenderTexture.active =
                    active;

                camera.targetTexture =
                    previous;

                camera.aspect =
                    oldAspect;


                // 元のAspect比でフレーミングし直す。
                framing.Frame();


                // RenderTextureのGPUリソースを解放。
                target.Release();


                // Editor用一時オブジェクトなので即時破棄。
                UnityEngine.Object
                    .DestroyImmediate(
                        target
                    );

                UnityEngine.Object
                    .DestroyImmediate(
                        image
                    );
            }
        }


        // ================================================================
        // Motion Study Render
        // ================================================================

        /// <summary>
        /// 完全ループを96分割し、
        /// 432 × 768 PNGシーケンスとして出力する。
        ///
        /// Motion確認や外部動画エンコード用の
        /// 軽量プレビューとして使用できる。
        /// </summary>
        [MenuItem("Art/Aureate Orrery/Render Motion Study")]
        public static void RenderMotionStudy()
        {
            // 出力前にループの完全性を検証。
            ValidateLoop();


            // ============================================================
            // 必要オブジェクト取得
            // ============================================================

            var camera =
                Camera.main;

            var system =
                UnityEngine.Object
                    .FindFirstObjectByType<CelestialSystem>();

            var framing =
                camera.GetComponent<CameraController>();


            // ============================================================
            // 現在状態を保存
            // ============================================================

            var previous =
                camera.targetTexture;

            var active =
                RenderTexture.active;


            float oldAspect =
                camera.aspect;


            // 現在のPhaseを0〜1へ戻して保存。
            float oldPhase =
                system.Phase /
                MathematicalOrbit.Tau;


            // ============================================================
            // Motion Study用RenderTexture
            // ============================================================

            // 9:16を維持した軽量解像度。
            var target =
                new RenderTexture(
                    432,
                    768,
                    24,
                    RenderTextureFormat.ARGBHalf
                );

            target.Create();


            var image =
                new Texture2D(
                    432,
                    768,
                    TextureFormat.RGB24,
                    false
                );


            try
            {
                // ========================================================
                // 出力フォルダ生成
                // ========================================================

                Directory.CreateDirectory(
                    "ArtworkPreviews/Motion"
                );


                // Portrait Aspect。
                camera.aspect =
                    432f / 768;

                camera.targetTexture =
                    target;


                // ========================================================
                // 96フレーム完全ループ
                // ========================================================

                // frame = 0〜95
                //
                // normalizedTime:
                // 0 / 96 ～ 95 / 96
                //
                // 1.0地点は出力しないため、
                // 動画として連結した際に最初のフレームが重複しない。
                for (
                    int frame = 0;
                    frame < 96;
                    frame++
                )
                {
                    // 現在フレームのPhaseを設定。
                    system.Evaluate(
                        frame / 96f
                    );


                    // 現在状態に合わせてカメラを再調整。
                    framing.Frame();


                    // ====================================================
                    // URP Render
                    // ====================================================

                    RenderPipeline.SubmitRenderRequest(
                        camera,
                        new UniversalRenderPipeline
                            .SingleCameraRequest
                        {
                            destination =
                                target
                        }
                    );


                    // ====================================================
                    // RenderTexture → Texture2D
                    // ====================================================

                    RenderTexture.active =
                        target;

                    image.ReadPixels(
                        new Rect(
                            0,
                            0,
                            432,
                            768
                        ),
                        0,
                        0
                    );

                    image.Apply();


                    // ====================================================
                    // PNG Sequence保存
                    // ====================================================

                    // 000.png ～ 095.png
                    File.WriteAllBytes(
                        $"ArtworkPreviews/Motion/{frame:D3}.png",
                        image.EncodeToPNG()
                    );
                }


                Debug.Log(
                    "AUREATE_ORRERY_MOTION_RENDERED: 96 frames, full loop"
                );
            }
            finally
            {
                // ========================================================
                // Render前の状態へ復元
                // ========================================================

                RenderTexture.active =
                    active;

                camera.targetTexture =
                    previous;

                camera.aspect =
                    oldAspect;


                // 処理前のPhaseへ戻す。
                system.Evaluate(
                    oldPhase
                );


                // 元のカメラ比率・Phaseで再フレーミング。
                framing.Frame();


                // ========================================================
                // 一時リソース解放
                // ========================================================

                target.Release();

                UnityEngine.Object
                    .DestroyImmediate(
                        target
                    );

                UnityEngine.Object
                    .DestroyImmediate(
                        image
                    );
            }
        }


        // ================================================================
        // Build + Validate
        // ================================================================

        /// <summary>
        /// Scene生成後、そのままLoop Validationを実行する。
        ///
        /// CI / Batch処理などから1メソッドで
        /// Scene生成と検証を行うためのエントリーポイント。
        /// </summary>
        public static void BuildAndValidate()
        {
            CreateScene();
            ValidateLoop();
        }
    }
}
