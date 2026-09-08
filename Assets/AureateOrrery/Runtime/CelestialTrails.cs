using UnityEngine;

namespace AureateOrrery
{
    /// <summary>
    /// Harmonic / Lissajous / Epicycle の3種類の軌道に対して、
    /// フェード付きの軌跡（Trail）を生成・更新するクラス。
    ///
    /// ParticleSystem は使用せず、
    /// 数学的に現在位置から過去の位置を再計算することで
    /// シーク・ループ・一時停止時にも破綻しない軌跡を生成する。
    /// </summary>

    // Analytic history: seeking, pausing and looping never leave stale particles.
    // 数学的に過去位置を再計算する方式なので、
    // シーク・一時停止・ループを行っても古いパーティクルが残らない。

    // Three trails share one dynamic mesh and one material, with reused buffers.
    // 3本のトレイルは1つの動的Meshと1つのMaterialを共有し、
    // 頂点バッファも毎フレーム再利用する。
    public sealed class CelestialTrails
    {
        // ================================================================
        // トレイル設定
        // ================================================================

        // 1軌道あたりの履歴サンプル数。
        // 各サンプルは1枚のQuadとして描画される。
        const int Samples = 64;

        // 3軌道 × 64サンプル × Quad 4頂点。
        // 毎フレーム新しい配列を生成せず、このバッファを再利用する。
        readonly Vector3[] vertices =
            new Vector3[Samples * 3 * 4];


        // ================================================================
        // 動的トレイルMesh
        // ================================================================

        // 外部の CelestialSystem から描画に使用するMesh。
        public Mesh Mesh { get; }


        // ================================================================
        // コンストラクタ
        // ================================================================

        /// <summary>
        /// 3本の軌道トレイル用Meshを初期生成する。
        ///
        /// 頂点位置だけがアニメーション中に変化し、
        /// UV・Color・Triangle構造は最初に一度だけ生成される。
        /// </summary>
        public CelestialTrails()
        {
            // 各頂点のUV。
            var uv =
                new Vector2[vertices.Length];

            // 各頂点のカラー。
            var colors =
                new Color[vertices.Length];

            // Quad 1枚につきTriangle 2枚 = 6インデックス。
            //
            // Samples × 3軌道 × 6インデックス。
            var triangles =
                new int[Samples * 3 * 6];


            // ============================================================
            // 3種類の軌道トレイルを初期化
            // ============================================================

            for (int orbit = 0; orbit < 3; orbit++)
            {
                for (int i = 0; i < Samples; i++)
                {
                    // 現在処理中のQuad番号。
                    int quad =
                        orbit * Samples + i;

                    // Quadの最初の頂点インデックス。
                    int v =
                        quad * 4;

                    // Quadの最初のTriangleインデックス。
                    int t =
                        quad * 6;


                    // ====================================================
                    // トレイルのフェード
                    // ====================================================

                    // Trailの先頭を最も明るく、
                    // 後方へ行くほど非線形に減衰させる。
                    //
                    // pow(..., 1.7) によって、
                    // 線形フェードよりも自然な減衰カーブを作る。
                    float fade =
                        Mathf.Pow(
                            1 - i / (float)Samples,
                            1.7f
                        );


                    // ====================================================
                    // 軌道ごとのカラー
                    // ====================================================

                    // orbit == 1:
                    // Lissajous軌道を暖色系のゴールドにする。
                    //
                    // その他:
                    // Harmonic / Epicycle use subdued blue-grey, below the brass highlights.
                    //
                    // fade:
                    // Trail後方の色を暗くする。
                    //
                    // 1.8:
                    // HDR / Bloomを意識した輝度倍率。
                    Color color =
                        (
                            orbit == 1
                                ? new Color(
                                    1,
                                    .72f,
                                    .32f
                                )
                                : new Color(
                                    .30f,
                                    .40f,
                                    .48f
                                )
                        )
                        * fade
                        * 1.8f;


                    // Trail全体の透明度。
                    color.a = .25f;


                    // Quadの4頂点すべてに同じ色を設定。
                    for (int j = 0; j < 4; j++)
                    {
                        colors[v + j] = color;
                    }


                    // ====================================================
                    // Quad UV
                    // ====================================================

                    uv[v] =
                        new Vector2(0, 0);

                    uv[v + 1] =
                        new Vector2(1, 0);

                    uv[v + 2] =
                        new Vector2(1, 1);

                    uv[v + 3] =
                        new Vector2(0, 1);


                    // ====================================================
                    // Quad Triangle
                    // ====================================================

                    // Triangle 1
                    triangles[t] =
                        v;

                    triangles[t + 1] =
                        v + 1;

                    triangles[t + 2] =
                        v + 2;


                    // Triangle 2
                    triangles[t + 3] =
                        v;

                    triangles[t + 4] =
                        v + 2;

                    triangles[t + 5] =
                        v + 3;
                }
            }


            // ============================================================
            // Dynamic Mesh生成
            // ============================================================

            Mesh =
                new Mesh
                {
                    name =
                        "Analytic celestial trails"
                };


            // 毎フレーム頂点位置が変更されることをUnityへ通知。
            // 動的Meshとして内部最適化される。
            Mesh.MarkDynamic();


            // 初期Meshデータを設定。
            Mesh.vertices =
                vertices;

            Mesh.uv =
                uv;

            Mesh.colors =
                colors;

            Mesh.triangles =
                triangles;


            // ============================================================
            // Bounds
            // ============================================================

            // Trailが動いてもFrustum Cullingで消えないように、
            // 十分広い固定Boundsを設定する。
            Mesh.bounds =
                new Bounds(
                    Vector3.zero,
                    Vector3.one * 5
                );
        }


        // ================================================================
        // 軌道進行関数
        // ================================================================

        /// <summary>
        /// 単純な一定速度ではなく、
        /// sin波を加えて軌道上の進行速度を緩やかに変化させる。
        ///
        /// phase - 0.24 * sin(phase)
        ///
        /// これにより天体の動きに微細な加速・減速が発生する。
        /// </summary>
        public static float Travel(float phase)
        {
            return
                phase
                - .24f * Mathf.Sin(phase);
        }


        // ================================================================
        // 軌道位置計算
        // ================================================================

        /// <summary>
        /// 指定された軌道とPhaseから、
        /// その瞬間の3D座標を数学的に計算する。
        ///
        /// orbit 0 = Harmonic
        /// orbit 1 = Lissajous
        /// orbit 2 = Epicycle
        /// </summary>
        public static Vector3 Position(
            int orbit,
            float phase
        )
        {
            // 非線形な進行値へ変換。
            float t =
                Travel(phase);


            // ------------------------------------------------------------
            // orbit 0:
            // 1-2-3 Harmonic軌道
            // ------------------------------------------------------------

            if (orbit == 0)
            {
                return
                    MathematicalOrbit.Harmonic(
                        t + .4f,
                        1.85f
                    );
            }


            // ------------------------------------------------------------
            // orbit 1:
            // 2-3-5 Lissajous軌道
            //
            // -t にすることで他の軌道とは逆方向へ進ませ、
            // 全体の動きに対向流を作る。
            // ------------------------------------------------------------

            if (orbit == 1)
            {
                return
                    MathematicalOrbit.Lissajous(
                        -t + 1.5f,
                        1.55f
                    );
            }


            // ------------------------------------------------------------
            // orbit 2:
            // Epicycle軌道
            // ------------------------------------------------------------

            return
                MathematicalOrbit.Epicycle(
                    t + 2.4f
                );
        }


        // ================================================================
        // Trail更新
        // ================================================================

        /// <summary>
        /// 現在Phaseを基準として過去方向へ時間を遡り、
        /// 各軌道のTrail頂点を再計算する。
        ///
        /// ParticleSystemの履歴を保持するのではなく、
        ///
        /// currentPhase - historyOffset
        ///
        /// から毎フレーム軌道位置を解析的に求める。
        /// </summary>
        public void Evaluate(
            float phase,
            float length,
            float strength
        )
        {
            // ============================================================
            // 3種類の軌道を更新
            // ============================================================

            for (int orbit = 0; orbit < 3; orbit++)
            {
                for (int i = 0; i < Samples; i++)
                {
                    // 0 = Trail先頭
                    // 1に近づくほどTrail後方。
                    float u =
                        i / (float)Samples;


                    // ====================================================
                    // 過去の軌道位置を再計算
                    // ====================================================

                    // 現在Phaseから u * length 分だけ過去へ戻り、
                    // その時点の位置を数学的に求める。
                    //
                    // この方式により履歴データの蓄積が不要になる。
                    Vector3 p =
                        Position(
                            orbit,
                            phase - u * length
                        );


                    // ====================================================
                    // Trail幅
                    // ====================================================

                    // Trail先頭:
                    // 0.030
                    //
                    // Trail末端:
                    // 0.006
                    //
                    // 後方へ行くほど細くすることで、
                    // 彗星や光跡のような形状を作る。
                    float radius =
                        Mathf.Lerp(
                            .030f,
                            .006f,
                            u
                        )
                        * strength;


                    // 現在のQuadの最初の頂点インデックス。
                    int v =
                        (
                            orbit * Samples + i
                        )
                        * 4;


                    // ====================================================
                    // Trail Quad頂点
                    // ====================================================

                    // 軌道位置 p を中心として、
                    // XY平面上に正方形のQuadを生成する。
                    //
                    // Trail Shader側で発光させるための簡易Billboard形状。
                    vertices[v] =
                        p +
                        new Vector3(
                            -radius,
                            -radius,
                            0
                        );

                    vertices[v + 1] =
                        p +
                        new Vector3(
                            radius,
                            -radius,
                            0
                        );

                    vertices[v + 2] =
                        p +
                        new Vector3(
                            radius,
                            radius,
                            0
                        );

                    vertices[v + 3] =
                        p +
                        new Vector3(
                            -radius,
                            radius,
                            0
                        );
                }
            }


            // ============================================================
            // Dynamic Mesh更新
            // ============================================================

            // UV / Color / Triangleは変化しないため、
            // 毎フレーム更新するのは頂点位置のみ。
            //
            // 新規Meshや配列を生成しないためGC負荷も抑えられる。
            Mesh.SetVertices(vertices);
        }
    }
}
