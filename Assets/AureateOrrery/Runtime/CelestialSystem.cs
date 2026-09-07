using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AureateOrrery
{
    /// <summary>
    /// 天体儀全体の生成・配置・アニメーションを管理するメインシステム。
    ///
    /// 黄道ダイヤル、アーミラリリング、幾何学プレート、
    /// Harmonic / Lissajous / Epicycle 軌道、恒星、発光コアなどを
    /// プロシージャルに生成し、共通の周期 Phase を使って動かす。
    /// </summary>
    [ExecuteAlways]
    public sealed class CelestialSystem : MonoBehaviour
    {
        // ================================================================
        // 構成設定
        // ジオメトリ関連の値を変更した場合は Rebuild() が必要。
        // ================================================================

        [Header("Composition — rebuild after changing geometry")]

        // 天体儀全体のスケール。
        [Range(.5f, 2)] public float masterScale = 1;

        // 生成する軌道リングの数。
        [Range(3, 8)] public int orbitalRings = 5;

        // 軌道リング半径の配置に黄金比をどの程度影響させるか。
        [Range(0, 1)] public float goldenRatioInfluence = .65f;

        // 各アーミラリリングの基本傾斜角度。
        [Range(15, 75)] public float ringTilt = 52;

        // SacredGeometryGenerator で使用する幾何学の複雑度。
        [Range(1, 3)] public int geometryComplexity = 1;

        // 背景に生成する恒星数。
        [Range(200, 3000)] public int starCount = 1100;

        // 恒星配置に使用するランダムシード。
        public int seed = 1729;


        // ================================================================
        // 時間・アニメーション設定
        // ================================================================

        [Header("Clock")]

        // アニメーションが一周するまでの秒数。
        [Min(1)] public float loopSeconds = 12;

        // 全アニメーションの速度倍率。
        [Range(0, 2)] public float globalAnimationSpeed = 1;

        // 各リングが周期的に揺れる最大角度。
        [Range(0, 12)] public float ringSwingDegrees = 7;

        // リング歳差運動の最大角度。
        [Range(0, 25)] public float precessionDegrees = 14;

        // 軌道トレイルの長さ。
        [Range(0, .8f)] public float trailLength = .38f;

        // 軌道トレイルの発光強度。
        [Range(0, 2)] public float trailStrength = 1;

        // 一周期ごとの整列タイミングで発生する発光強度。
        [Range(0, 1)] public float alignmentGlow = .45f;

        // true の場合、自動時間ではなく normalizedPhase を使用する。
        public bool manualPhase;

        // 手動制御用の 0〜1 の正規化された時間。
        [Range(0, 1)] public float normalizedPhase;


        // ================================================================
        // ライティング・シェーダー設定
        // ================================================================

        [Header("Light")]

        // 青白い天体オブジェクトの基本発光強度。
        [Range(0, 5)] public float emissionStrength = 1.25f;

        [Header("Planet polish — rebuild after changing materials")]

        // 中央天体の粗さ。高いほどHighlightが広く穏やかになる。
        [Range(.25f, .9f)] public float centralPlanetRoughness = .62f;

        // 中央天体の大きな地表トーンと微細なNormal変化。
        [Range(0, 1)] public float centralPlanetSurfaceDetail = .42f;

        // シルエットだけに残す、弱い寒色Atmosphere Rim。
        [Range(0, .5f)] public float centralPlanetRimStrength = .12f;

        // 通常天体の暗部を完全に消さないための最小Emission。
        [Range(0, .15f)] public float secondaryPlanetEmission = .018f;

        // 金属パーツ用シェーダー。
        public Shader metalShader;

        // 恒星・霞・トレイルなどの発光用シェーダー。
        public Shader glowShader;


        // ================================================================
        // 内部オブジェクト管理
        // ================================================================

        // このクラスが動的生成した Unity Object を保持する。
        // Rebuild / Disable 時にまとめて破棄するために使用。
        readonly List<Object> owned = new();

        // 生成したアーミラリリング。
        readonly List<Transform> rings = new();

        // 各リングの初期回転値。
        readonly List<Quaternion> ringBases = new();


        // ================================================================
        // 天体儀を構成する主要 Transform
        // ================================================================

        Transform generated,
            machine,
            lattice,
            core,
            harmonicMarker,
            lissajousMarker,
            epicycleMarker,
            epicycleRing;


        // ================================================================
        // マテリアル
        // ================================================================

        Material gold,
            inkGold,
            lightGold,
            blue,
            glow,
            coreMaterial,
            warmPlanet,
            ivoryPlanet,
            charcoalPlanet,
            hazeMaterial,
            trailMaterial;


        // 軌道トレイル生成・更新クラス。
        CelestialTrails trails;

        // 中央コア周辺の補助ビジュアル。
        Transform coreReticle, scattering;

        // 各リング上を移動する観測ビーズ。
        readonly List<Transform> sightBeads = new();

        // sightBeads を配置するための各リング半径。
        readonly List<float> ringRadii = new();

        // 自動アニメーション用の経過時間。
        double elapsed;

        // 現在のアニメーション位相。
        // 0〜2π の範囲で保持される。
        public float Phase { get; private set; }


        // ================================================================
        // Unity ライフサイクル
        // ================================================================

        // コンポーネント有効化時に天体儀を生成する。
        void OnEnable()
        {
            Rebuild();
        }

        // コンポーネント無効化時に生成物を破棄する。
        void OnDisable()
        {
            Clear();
        }

        // Inspector 変更時に loopSeconds が 1 未満にならないよう制限。
        void OnValidate()
        {
            loopSeconds = Mathf.Max(1, loopSeconds);
        }


        // ================================================================
        // 動的生成オブジェクト管理
        // ================================================================

        /// <summary>
        /// 動的に生成した Unity Object を owned に登録する。
        /// HideAndDontSave を設定し、Scene / Asset として保存されないようにする。
        /// </summary>
        T Own<T>(T value) where T : Object
        {
            value.hideFlags = HideFlags.HideAndDontSave;
            owned.Add(value);
            return value;
        }


        /// <summary>
        /// 現在生成されている天体儀を全て破棄する。
        /// </summary>
        void Clear()
        {
            // ルートを最初に非表示にして、破棄途中の描画を防ぐ。
            if (generated)
                generated.gameObject.SetActive(false);

            // Clear in reverse order so components disappear before their resources.
            // コンポーネントが参照リソースより先に消えるよう、
            // 生成順とは逆順に破棄する。
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i])
                {
                    // Play Mode と Edit Mode で適切な削除方法を使い分ける。
                    if (Application.isPlaying)
                        Destroy(owned[i]);
                    else
                        DestroyImmediate(owned[i]);
                }

            // 内部参照をすべて初期化。
            owned.Clear();
            rings.Clear();
            ringBases.Clear();
            sightBeads.Clear();
            ringRadii.Clear();

            trails = null;
            generated = null;
        }


        // ================================================================
        // GameObject / Material / Mesh 生成ヘルパー
        // ================================================================

        /// <summary>
        /// 空の GameObject を生成して指定した親 Transform に配置する。
        /// </summary>
        Transform Node(string label, Transform parent)
        {
            var g = Own(new GameObject(label));
            g.transform.SetParent(parent, false);
            return g.transform;
        }


        /// <summary>
        /// 金属表現用マテリアルを生成する。
        /// BaseColor / Metallic / Smoothness / Emission を設定する。
        /// </summary>
        Material Metal(string label, Color color, float emission)
        {
            bool physicalMetal = label == "Aged brass" || label == "Recessed engraving" || label == "Illuminated gold";
            Shader aged = physicalMetal ? Resources.Load<Shader>("AgedMetal") : null;
            var m = Own(new Material(aged ? aged : metalShader) { name = label });

            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", .86f);
            m.SetFloat("_Smoothness", label == "Recessed engraving" ? .32f : .48f);

            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", physicalMetal ? Color.black : color * emission);

            return m;
        }


        /// <summary>
        /// 発光体ではなく、Scene Lightで立体を見せる天体Materialを生成する。
        /// 大きなTone Variationを優先し、遠景でちらつくMicro Detailは抑える。
        /// </summary>
        Material Planet(
            string label,
            Color color,
            float metallic,
            float roughness,
            float surfaceScale,
            float variation,
            float bump,
            Color rim,
            float rimStrength,
            float emission)
        {
            Shader shader =
                Resources.Load<Shader>("PlanetSurface");

            var material = Own(
                new Material(shader ? shader : metalShader)
                {
                    name = label
                }
            );

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", 1 - roughness);
            material.SetColor("_EmissionColor", color * emission);

            if (shader)
            {
                material.SetFloat("_SurfaceScale", surfaceScale);
                material.SetFloat("_SurfaceVariation", variation);
                material.SetFloat("_BumpStrength", bump);
                material.SetFloat("_RoughnessVariation", variation * .28f);
                material.SetColor("_RimColor", rim);
                material.SetFloat("_RimPower", 5.5f);
                material.SetFloat("_RimStrength", rimStrength);
            }

            return material;
        }


        /// <summary>
        /// MeshFilter と MeshRenderer を持つ描画オブジェクトを生成する。
        /// </summary>
        Transform Draw(
            string label,
            Mesh mesh,
            Material material,
            Transform parent)
        {
            // Mesh も Clear() 対象として登録する。
            Own(mesh);

            var n = Node(label, parent);

            n.gameObject
                .AddComponent<MeshFilter>()
                .sharedMesh = mesh;

            var r = n.gameObject.AddComponent<MeshRenderer>();

            r.sharedMaterial = material;

            // Opaque instrument parts cast and receive physical shadows.
            r.shadowCastingMode = material.renderQueue < 3000 ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;

            return n;
        }


        /// <summary>
        /// 天体・恒星・マーカーとして使用する球体を生成する。
        /// </summary>
        Transform Orb(
            string label,
            float size,
            Material material,
            Transform parent)
        {
            var g = Own(
                GameObject.CreatePrimitive(PrimitiveType.Sphere)
            );

            g.name = label;

            g.transform.SetParent(parent, false);
            g.transform.localScale = Vector3.one * size;

            // Primitive 生成時に付属する Collider は不要なので削除する。
            var collider = g.GetComponent<Collider>();

            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);

            var r = g.GetComponent<MeshRenderer>();

            r.sharedMaterial = material;
            r.shadowCastingMode = material.renderQueue < 3000 ? ShadowCastingMode.On : ShadowCastingMode.Off;

            return g.transform;
        }


        // ================================================================
        // 天体儀全体の再構築
        // ================================================================

        [ContextMenu("Rebuild instrument")]
        public void Rebuild()
        {
            // 古い生成物を全て破棄してから再構築する。
            Clear();


            // ============================================================
            // Shader の取得
            // ============================================================

            // Inspector で指定されていない場合は URP Lit を使用。
            if (!metalShader)
                metalShader = Shader.Find(
                    "Universal Render Pipeline/Lit"
                );

            // カスタム CelestialGlow シェーダーを取得。
            if (!glowShader)
                glowShader = Shader.Find(
                    "AureateOrrery/CelestialGlow"
                );

            // 必須 Shader が存在しない場合は生成を中止する。
            if (!metalShader || !glowShader)
            {
                Debug.LogError(
                    "Aureate Orrery shaders are missing.",
                    this
                );

                return;
            }


            // ============================================================
            // マテリアル生成
            // ============================================================

            // 古びた真鍮。
            gold = Metal(
                "Aged brass",
                new Color(.54f, .43f, .30f),
                .13f
            );

            // 彫刻・溝用の暗い真鍮。
            inkGold = Metal(
                "Recessed engraving",
                new Color(.32f, .23f, .11f),
                .18f
            );

            // 発光する明るい金色。
            lightGold = Metal(
                "Illuminated gold",
                new Color(.68f, .57f, .41f),
                .48f
            );

            // 青白い天体光。
            blue = Metal(
                "Celestial silver",
                new Color(.55f, .725f, .91f),
                emissionStrength
            );

            // Moving bodies use reflected light and restrained, desaturated colors.
            // Reference stars retain the intentionally luminous blue material above.
            warmPlanet = Planet(
                "Warm stone planet",
                new Color(.34f, .285f, .22f),
                .12f,
                .68f,
                3.4f,
                .25f,
                .055f,
                new Color(.20f, .28f, .40f),
                .025f,
                secondaryPlanetEmission
            );

            ivoryPlanet = Planet(
                "Muted ivory planet",
                new Color(.43f, .445f, .43f),
                .08f,
                .58f,
                4.1f,
                .20f,
                .045f,
                new Color(.24f, .32f, .46f),
                .035f,
                secondaryPlanetEmission
            );

            charcoalPlanet = Planet(
                "Charcoal planet",
                new Color(.15f, .17f, .19f),
                .18f,
                .72f,
                3.1f,
                .31f,
                .065f,
                new Color(.22f, .30f, .44f),
                .045f,
                secondaryPlanetEmission * .5f
            );

            // 恒星フィールド用発光マテリアル。
            glow = Own(new Material(glowShader));
            glow.SetColor("_Tint", Color.white);


            // ============================================================
            // 天体儀ルート生成
            // ============================================================

            // 動的生成物全体のルート。
            generated = Node(
                "Generated instrument (transient)",
                transform
            );

            // 天体儀本体。
            machine = Node(
                "Armillary mechanism",
                generated
            );


            // ============================================================
            // 黄道ダイヤル
            // ============================================================

            var dial = new CelestialGeometry();

            // 外周と内周の同心円。
            dial.Circle(2.66f, .027f);
            dial.Circle(2.58f, .01f);
            dial.Circle(2.39f, .012f);
            dial.Circle(2.33f, .005f);


            // 180 分割の目盛りを生成。
            for (int i = 0; i < 180; i++)
            {
                float a =
                    i * MathematicalOrbit.Tau / 180;

                dial.Segment(
                    CelestialGeometry.Radial(
                        a,
                        i % 15 == 0
                            ? 2.40f
                            : i % 5 == 0
                                ? 2.47f
                                : 2.51f
                    ),
                    CelestialGeometry.Radial(a, 2.565f),
                    i % 5 == 0 ? .006f : .003f
                );
            }


            // Twelve abstract observation stations; no historical script is claimed.
            // 12 個の抽象的な観測ステーションを配置。
            // 実在する歴史文字・古代文字を再現するものではない。
            for (int i = 0; i < 12; i++)
            {
                float a =
                    i * MathematicalOrbit.Tau / 12;

                Vector3 p =
                    CelestialGeometry.Radial(a, 2.79f);

                Vector3 tangent =
                    CelestialGeometry.Radial(
                        a + Mathf.PI / 2,
                        .035f
                    );

                Vector3 radial =
                    CelestialGeometry.Radial(a, .065f);

                // 菱形状の抽象記号を描画。
                dial.Segment(
                    p + radial,
                    p + tangent,
                    .007f
                );

                dial.Segment(
                    p + tangent,
                    p - radial,
                    .007f
                );

                dial.Segment(
                    p - radial,
                    p - tangent,
                    .007f
                );

                dial.Segment(
                    p - tangent,
                    p + radial,
                    .007f
                );
            }


            // 黄道ダイヤルを Mesh 化して配置。
            var face = Draw(
                "180-division ecliptic dial",
                dial.Build("Engraved dial"),
                gold,
                machine
            );

            // 正面から少し傾け、立体感を作る。
            face.localRotation =
                Quaternion.Euler(12, -8, 0);


            // ============================================================
            // アーミラリリング
            // ============================================================

            for (int i = 0; i < orbitalRings; i++)
            {
                // 0〜1 に正規化したリング番号。
                float u =
                    i / (float)(orbitalRings - 1);

                // 線形配置と黄金比ベースの非線形配置をブレンド。
                float radius = Mathf.Lerp(
                    1.22f,
                    2.27f,
                    Mathf.Lerp(
                        u,
                        Mathf.Pow(
                            u,
                            1 / MathematicalOrbit.GoldenRatio
                        ),
                        goldenRatioInfluence
                    )
                );


                var g = new CelestialGeometry();

                // リング本体。
                g.Circle(radius, .016f);

                // 内側の細い補助リング。
                g.Circle(radius - .045f, .004f);


                // 各リングに 60 分割の目盛りを生成。
                for (int j = 0; j < 60; j++)
                {
                    float a =
                        j * MathematicalOrbit.Tau / 60;

                    g.Segment(
                        CelestialGeometry.Radial(
                            a,
                            radius
                        ),
                        CelestialGeometry.Radial(
                            a,
                            radius - (
                                j % 5 == 0
                                    ? .085f
                                    : .034f
                            )
                        ),
                        .003f
                    );
                }


                // 偶数・奇数で異なる金属表現を使用。
                var ring = Draw(
                    "Armillary ring " + (i + 1),
                    g.Build("Brass meridian"),
                    i % 2 == 0
                        ? gold
                        : lightGold,
                    machine
                );


                // 各リングに異なる初期回転を設定。
                var q = Quaternion.Euler(
                    ringTilt * (
                        i % 2 == 0
                            ? 1
                            : -1
                    ),
                    i * 37 - 60,
                    i * 29
                );

                rings.Add(ring);
                ringBases.Add(q);


                // リング上を移動する観測用ビーズ。
                var bead = Orb(
                    "Travelling sight bead",
                    .055f,
                    i % 3 == 0
                        ? warmPlanet
                        : i % 3 == 1
                            ? ivoryPlanet
                            : charcoalPlanet,
                    ring
                );

                sightBeads.Add(bead);

                // アニメーション時に使用する半径を保存。
                ringRadii.Add(radius);
            }


            // ============================================================
            // Sacred Geometry 計算プレート
            // ============================================================

            lattice = Draw(
                "Hexagonal calculation plate",
                SacredGeometryGenerator.Create(
                    geometryComplexity
                ),
                inkGold,
                machine
            );

            // 中央プレートを少し前方に配置。
            lattice.localPosition =
                new Vector3(0, 0, .45f);


            // ============================================================
            // 数学的軌道カーブ
            // ============================================================

            var harmonic =
                new CelestialGeometry();

            var lissajous =
                new CelestialGeometry();

            var epi =
                new CelestialGeometry();


            // 軌道カーブを構成する頂点。
            var hp = new Vector3[384];
            var lp = new Vector3[384];
            var ep = new Vector3[384];


            // 0〜2π を 384 分割して各数学曲線を計算。
            for (int i = 0; i < 384; i++)
            {
                float t =
                    i * MathematicalOrbit.Tau / 384;

                // 1:2:3 Harmonic 軌道。
                hp[i] =
                    MathematicalOrbit.Harmonic(
                        t,
                        1.85f
                    );

                // 2:3:5 Lissajous 軌道。
                lp[i] =
                    MathematicalOrbit.Lissajous(
                        t,
                        1.55f
                    );

                // Epicycle 軌道。
                ep[i] =
                    MathematicalOrbit.Epicycle(t);
            }


            // 各曲線を細い Tube Mesh に変換。
            harmonic.Tube(hp, .004f);
            lissajous.Tube(lp, .003f);
            epi.Tube(ep, .003f);


            Draw(
                "1-2-3 harmonic observation curve",
                harmonic.Build("Harmonic"),
                lightGold,
                machine
            );

            Draw(
                "2-3-5 celestial curve",
                lissajous.Build("Lissajous"),
                inkGold,
                machine
            );

            Draw(
                "Epicycle record",
                epi.Build("Epicycle"),
                inkGold,
                machine
            );


            // ============================================================
            // 軌道上を移動するマーカー
            // ============================================================

            harmonicMarker = Orb(
                "Harmonic star",
                .075f,
                ivoryPlanet,
                machine
            );

            lissajousMarker = Orb(
                "Lissajous star",
                .05f,
                warmPlanet,
                machine
            );

            epicycleMarker = Orb(
                "Epicycle pointer",
                .065f,
                charcoalPlanet,
                machine
            );


            // ============================================================
            // Epicycle の移動リング
            // ============================================================

            var e =
                new CelestialGeometry();

            e.Circle(.32f, .005f);

            epicycleRing = Draw(
                "Moving deferent",
                e.Build("Epicycle ring"),
                lightGold,
                machine
            );


            // ============================================================
            // 中央の発光コア
            // ============================================================

            coreMaterial = Planet(
                "Ancient central planet",
                new Color(.31f, .345f, .38f),
                .20f,
                centralPlanetRoughness,
                2.75f,
                centralPlanetSurfaceDetail,
                centralPlanetSurfaceDetail * .18f,
                new Color(.28f, .43f, .68f),
                centralPlanetRimStrength,
                .028f
            );

            core = Orb(
                "Cold celestial core",
                .215f,
                coreMaterial,
                machine
            );


            // ============================================================
            // 中央コア周辺の霞
            // ============================================================

            hazeMaterial =
                Own(new Material(glowShader));

            hazeMaterial.SetColor(
                "_Tint",
                new Color(.065f, .10f, .17f, 1)
            );


            // カメラ方向に見せる簡易的な Quad Mesh。
            var haze = new Mesh
            {
                name = "Soft core scattering"
            };

            haze.vertices = new[]
            {
                new Vector3(-1.1f, -1.1f, .12f),
                new Vector3(1.1f, -1.1f, .12f),
                new Vector3(1.1f, 1.1f, .12f),
                new Vector3(-1.1f, 1.1f, .12f)
            };

            haze.uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            haze.colors = new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            };

            haze.triangles = new[]
            {
                0, 1, 2,
                0, 2, 3
            };

            haze.RecalculateBounds();


            scattering = Draw(
                "Faint central scattering",
                haze,
                hazeMaterial,
                machine
            );


            // ============================================================
            // コア周囲の照準リング
            // ============================================================

            var halo =
                new CelestialGeometry();

            halo.Circle(.28f, .008f);
            halo.Circle(.38f, .004f);

            coreReticle = Draw(
                "Core reticle",
                halo.Build("Core rings"),
                lightGold,
                machine
            );


            // ============================================================
            // 軌道トレイル
            // ============================================================

            trails =
                new CelestialTrails();

            trailMaterial =
                Own(new Material(glowShader));

            trailMaterial.SetColor(
                "_Tint",
                Color.white
            );

            Draw(
                "Three fading orbital histories",
                trails.Mesh,
                trailMaterial,
                machine
            );


            // ============================================================
            // 球状恒星カタログ
            // ============================================================

            // Sparse spherical reference points linked along short local chords.
            // 球面上に疎な参照点を配置し、
            // 距離が近い点同士を短い弦で接続する。
            var net =
                new CelestialGeometry();

            for (int i = 0; i < 26; i++)
            {
                // Fibonacci Sphere 系の分布で球面上の位置を取得。
                Vector3 p =
                    MathematicalOrbit.SpherePoint(
                        i,
                        26,
                        1.03f
                    );

                // 参照恒星を配置。
                Orb(
                    "Reference star " + i,
                    .022f,
                    blue,
                    machine
                ).localPosition = p;


                // 現在の点から最も近い次の点を探索する。
                int nearest = -1;
                float distance = float.MaxValue;

                for (int j = i + 1; j < 26; j++)
                {
                    float d =
                        (
                            p -
                            MathematicalOrbit.SpherePoint(
                                j,
                                26,
                                1.03f
                            )
                        ).sqrMagnitude;

                    if (d < distance)
                    {
                        distance = d;
                        nearest = j;
                    }
                }


                // 十分近い場合のみ線で接続する。
                if (
                    nearest >= 0 &&
                    distance < .75f
                )
                    net.Segment(
                        p,
                        MathematicalOrbit.SpherePoint(
                            nearest,
                            26,
                            1.03f
                        ),
                        .0025f
                    );
            }


            Draw(
                "Spherical star catalogue",
                net.Build(
                    "Local celestial chords"
                ),
                inkGold,
                machine
            );


            // ============================================================
            // 遠方の恒星フィールド
            // ============================================================

            Draw(
                "Distant fixed stars",
                StarFieldGenerator.Create(
                    starCount,
                    seed
                ),
                glow,
                generated
            );


            // 初期状態を Phase = 0 で評価。
            Evaluate(0);
        }


        // ================================================================
        // フレーム更新
        // ================================================================

        void Update()
        {
            // まだ生成されていない場合は何もしない。
            if (!generated)
                return;


            // Play Mode の場合のみ経過時間を進める。
            if (Application.isPlaying)
                elapsed +=
                    Time.deltaTime *
                    globalAnimationSpeed;


            // manualPhase が有効なら Inspector の値を使用。
            // 無効なら loopSeconds を基準に自動ループさせる。
            Evaluate(
                manualPhase
                    ? normalizedPhase
                    : (float)(
                        (elapsed / loopSeconds) % 1
                    )
            );
        }


        // ================================================================
        // アニメーション評価
        // ================================================================

        /// <summary>
        /// 正規化時間 0〜1 を使って天体儀全体の状態を更新する。
        ///
        /// 全てのモーションを同じ Phase から計算することで、
        /// 完全なループアニメーションを維持する。
        /// </summary>
        public void Evaluate(float normalizedTime)
        {
            if (!generated)
                return;


            // 0〜1 の時間を 0〜2π の角度へ変換。
            Phase =
                Mathf.Repeat(
                    normalizedTime,
                    1
                ) *
                MathematicalOrbit.Tau;


            // ============================================================
            // 天体儀全体のスケール
            // ============================================================

            machine.localScale =
                Vector3.one * masterScale;


            // Quadrature separates tilt, yaw and roll instead of rocking every layer together.
            // Tilt / Yaw / Roll に異なる位相を与え、
            // 全レイヤーが同じ方向へ単純に揺れることを防ぐ。
            machine.localRotation =
                Quaternion.Euler(
                    4 * Mathf.Sin(Phase),
                    7 * Mathf.Sin(
                        Phase + .7f
                    ),
                    2 * Mathf.Sin(
                        Phase - .4f
                    )
                );


            // ============================================================
            // 各アーミラリリング
            // ============================================================

            for (int i = 0; i < rings.Count; i++)
            {
                // 黄金比を使って各リングの位相をずらす。
                // 規則的すぎない有機的な同期パターンを作る。
                float offset =
                    i *
                    MathematicalOrbit.Tau /
                    MathematicalOrbit.GoldenRatio;


                // 偶数・奇数リングで回転方向を反転。
                float direction =
                    i % 2 == 0
                        ? 1
                        : -1;


                // 歳差運動 + 揺動をリング初期姿勢に加える。
                rings[i].localRotation =
                    Quaternion.Euler(
                        precessionDegrees *
                        Mathf.Sin(
                            Phase + offset
                        ),

                        precessionDegrees *
                        .65f *
                        Mathf.Cos(
                            Phase + offset
                        ),

                        direction *
                        ringSwingDegrees *
                        Mathf.Sin(
                            Phase + offset * .5f
                        )
                    ) *
                    ringBases[i];


                // リング上の観測ビーズを軌道に沿って移動。
                sightBeads[i].localPosition =
                    CelestialGeometry.Radial(
                        direction *
                        CelestialTrails.Travel(
                            Phase
                        ) +
                        offset,

                        ringRadii[i]
                    );
            }


            // ============================================================
            // Sacred Geometry プレート
            // ============================================================

            // 複数軸を異なる周期で回転させ、
            // 静的なプレートに緩やかな浮遊感を与える。
            lattice.localRotation =
                Quaternion.Euler(
                    5 * Mathf.Sin(
                        Phase + .8f
                    ),
                    -7 * Mathf.Sin(
                        Phase
                    ),
                    -9 * Mathf.Sin(
                        Phase - .5f
                    )
                );


            // ============================================================
            // 数学軌道マーカー
            // ============================================================

            // Harmonic 曲線上の現在位置。
            harmonicMarker.localPosition =
                CelestialTrails.Position(
                    0,
                    Phase
                );

            // Lissajous 曲線上の現在位置。
            lissajousMarker.localPosition =
                CelestialTrails.Position(
                    1,
                    Phase
                );

            // Epicycle 曲線上の現在位置。
            epicycleMarker.localPosition =
                CelestialTrails.Position(
                    2,
                    Phase
                );


            // Epicycle の基準リング自体も円軌道上を移動させる。
            epicycleRing.localPosition =
                CelestialGeometry.Radial(
                    CelestialTrails.Travel(
                        Phase
                    ) +
                    2.4f,

                    1.12f
                );


            // ============================================================
            // Alignment Glow
            // ============================================================

            // A broad, smooth light crest marks the once-per-cycle transit near the seam.
            // 一周期に一度だけ発生する滑らかな発光ピーク。
            //
            // cos(Phase) を 0〜1 に変換し、
            // 6 乗することで発光区間を中央付近へ集中させる。
            float alignment =
                Mathf.Pow(
                    .5f +
                    .5f *
                    Mathf.Cos(Phase),
                    6
                ) *
                alignmentGlow;


            // ============================================================
            // 中央コアの呼吸
            // ============================================================

            // 通常の微細な脈動と Alignment Glow を組み合わせる。
            core.localScale =
                Vector3.one *
                (
                    .215f +
                    .009f *
                    Mathf.Sin(
                        Phase - .5f
                    ) +
                    .012f *
                    alignment
                );


            // ============================================================
            // コア照準リング
            // ============================================================

            coreReticle.localRotation =
                Quaternion.Euler(
                    18 *
                    Mathf.Sin(Phase),

                    22 *
                    Mathf.Cos(Phase),

                    0
                );


            // ============================================================
            // 中央散乱光
            // ============================================================

            scattering.localScale =
                Vector3.one *
                (
                    1 +
                    .10f *
                    Mathf.Sin(
                        Phase - .5f
                    ) +
                    .12f *
                    alignment
                );


            // Alignment 時に霞の強度を上げる。
            hazeMaterial.SetColor(
                "_Tint",
                new Color(
                    .012f,
                    .018f,
                    .029f
                ) *
                (
                    1 +
                    alignment
                )
            );


            // ============================================================
            // 中央天体はLightで見せ、Emissionは暗部の最低限の情報だけを保持する。
            // ============================================================

            coreMaterial.SetColor(
                "_EmissionColor",
                new Color(
                    .31f,
                    .345f,
                    .38f
                ) *
                (
                    .028f +
                    alignment * .018f
                )
            );

            coreMaterial.SetFloat(
                "_Smoothness",
                1 - centralPlanetRoughness
            );

            if (coreMaterial.HasProperty("_SurfaceVariation"))
            {
                coreMaterial.SetFloat(
                    "_SurfaceVariation",
                    centralPlanetSurfaceDetail
                );

                coreMaterial.SetFloat(
                    "_BumpStrength",
                    centralPlanetSurfaceDetail * .18f
                );

                coreMaterial.SetFloat(
                    "_RimStrength",
                    centralPlanetRimStrength
                );
            }


            // ============================================================
            // 青白い恒星・マーカー発光
            // ============================================================

            blue.SetColor(
                "_EmissionColor",
                new Color(
                    .55f,
                    .725f,
                    .91f
                ) *
                emissionStrength *
                (
                    1 +
                    .12f *
                    Mathf.Sin(Phase) +
                    alignment * .25f
                )
            );


            // ============================================================
            // 軌道トレイル更新
            // ============================================================

            trails.Evaluate(
                Phase,
                trailLength,
                trailStrength
            );


            // Shader 側にも現在 Phase を送る。
            trailMaterial.SetFloat(
                "_Phase",
                Phase
            );

            glow.SetFloat(
                "_Phase",
                Phase
            );
        }
    }
}
