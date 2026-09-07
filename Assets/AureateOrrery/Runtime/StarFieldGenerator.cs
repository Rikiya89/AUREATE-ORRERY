using System.Collections.Generic;
using UnityEngine;

namespace AureateOrrery
{
    /// <summary>
    /// 天体儀の周囲に配置する遠方の恒星フィールドを生成する静的クラス。
    ///
    /// 各恒星を個別のGameObjectとして生成せず、
    /// すべての星を1つのMeshへまとめることで描画負荷を抑える。
    /// </summary>
    public static class StarFieldGenerator
    {
        // One mesh, one draw; no particle GameObjects or CPU simulation.
        // すべての恒星を1つのMeshとして生成する。
        // ParticleSystemや大量のGameObjectは使用せず、
        // 1回の描画で星空全体を表示する構成。

        /// <summary>
        /// 指定された星数とシード値から、
        /// 球状に分布した恒星フィールドMeshを生成する。
        ///
        /// count:
        /// 生成する恒星数。
        ///
        /// seed:
        /// 恒星サイズ・色・明るさなどのランダム値を固定するシード。
        /// </summary>
        public static Mesh Create(int count, int seed)
        {
            // ============================================================
            // ランダム生成器
            // ============================================================

            // seedを指定することで、
            // Rebuildしても同じ恒星配置・色・サイズを再現できる。
            var random =
                new System.Random(seed);


            // ============================================================
            // Meshデータ用バッファ
            // ============================================================

            // 全恒星の頂点座標。
            var vertices =
                new List<Vector3>();

            // 各Quadに使用するUV。
            var uv =
                new List<Vector2>();

            // 各恒星の色・明るさ・透明度。
            var colors =
                new List<Color>();

            // Quadを構成するTriangleインデックス。
            var triangles =
                new List<int>();


            // ============================================================
            // 恒星生成
            // ============================================================

            for (int i = 0; i < count; i++)
            {
                // ========================================================
                // 球面上の恒星位置
                // ========================================================

                // SpherePointを使って球面上へ均等に近い形で配置する。
                //
                // 半径は24〜39程度の範囲でランダム化し、
                // 完全な球殻ではなく奥行きを持つ星空にする。
                float z = (float)random.NextDouble() * 2 - 1;
                float azimuth = (float)random.NextDouble() * Mathf.PI * 2;
                Vector3 direction = new Vector3(Mathf.Sqrt(1-z*z)*Mathf.Cos(azimuth), z,
                    Mathf.Sqrt(1-z*z)*Mathf.Sin(azimuth));
                // Seeded density clouds leave large quiet regions; the internal catalogue is unchanged.
                if (random.NextDouble() > Mathf.Lerp(.06f, .85f,
                    Mathf.Pow(Mathf.PerlinNoise(direction.x*2+7, direction.y*2+11), 2))) continue;
                Vector3 p = direction * (28 + (float)random.NextDouble()*18);

                // ========================================================
                // 恒星サイズ
                // ========================================================

                // 0.012〜0.055の範囲でサイズを決定する。
                //
                // random値を5乗することで、
                // 小さい星を圧倒的に多くし、
                // 大きな星は稀にしか現れない分布を作る。
                float size =
                    Mathf.Lerp(
                        .012f,
                        .055f,
                        Mathf.Pow(
                            (float)random.NextDouble(),
                            5
                        )
                    );


                // ========================================================
                // Billboard方向の計算
                // ========================================================

                // 恒星位置から球中心へ向かう法線方向。
                Vector3 normal =
                    p.normalized;


                // 法線に対して直交する右方向を計算。
                //
                // このベクトルを使って恒星Quadの横方向を作る。
                Vector3 right =
                    Vector3.Cross(
                        normal,
                        Vector3.up
                    ).normalized
                    * size;


                // rightとnormalの両方に直交する方向を計算。
                //
                // 恒星Quadの縦方向として使用する。
                Vector3 up =
                    Vector3.Cross(
                        right.normalized,
                        normal
                    )
                    * size;


                // 現在の頂点数を、
                // このQuadの開始インデックスとして保存する。
                int n =
                    vertices.Count;


                // ========================================================
                // 恒星Quadの4頂点
                // ========================================================

                // 星の位置pを中心として、
                // right / up方向へ広げたQuadを生成する。
                vertices.Add(
                    p - right - up
                );

                vertices.Add(
                    p + right - up
                );

                vertices.Add(
                    p + right + up
                );

                vertices.Add(
                    p - right + up
                );


                // ========================================================
                // UV
                // ========================================================

                // 1枚のQuad全体へ0〜1のUVを割り当てる。
                uv.Add(
                    new Vector2(0, 0)
                );

                uv.Add(
                    new Vector2(1, 0)
                );

                uv.Add(
                    new Vector2(1, 1)
                );

                uv.Add(
                    new Vector2(0, 1)
                );


                // ========================================================
                // 恒星カラー
                // ========================================================

                // 青白い恒星色と暖色系の恒星色をランダムに補間する。
                //
                // これにより完全な単色ではなく、
                // 温度差を感じる星空表現を作る。
                Color c =
                    Color.Lerp(
                        new Color(
                            .4f,
                            .54f,
                            .75f
                        ),
                        new Color(
                            1,
                            .84f,
                            .6f
                        ),
                        (float)random.NextDouble()
                    );


                // ========================================================
                // 恒星の明るさ
                // ========================================================

                // 色全体を0.3〜1.3倍して、
                // 星ごとの輝度差を作る。
                c *=
                    Mathf.Lerp(
                        .08f,
                        1.1f,
                        Mathf.Pow((float)random.NextDouble(), 4)
                    );


                // 星ごとの透明度もランダム化する。
                c.a =
                    (float)random.NextDouble();


                // Quadの4頂点すべてへ同じ色を設定する。
                for (int j = 0; j < 4; j++)
                {
                    colors.Add(c);
                }


                // ========================================================
                // Triangle
                // ========================================================

                // Quadを2枚のTriangleで構成する。
                //
                // Triangle 1
                triangles.Add(n);
                triangles.Add(n + 1);
                triangles.Add(n + 2);

                // Triangle 2
                triangles.Add(n);
                triangles.Add(n + 2);
                triangles.Add(n + 3);
            }


            // ============================================================
            // Mesh生成
            // ============================================================

            var m =
                new Mesh
                {
                    name =
                        "Golden-angle fixed stars"
                };


            // 全頂点をMeshへ設定。
            m.SetVertices(vertices);

            // UVチャンネル0へ恒星QuadのUVを設定。
            m.SetUVs(
                0,
                uv
            );

            // 星ごとの色・明るさ・透明度を設定。
            m.SetColors(colors);

            // TriangleインデックスをSubMesh 0へ設定。
            m.SetTriangles(
                triangles,
                0
            );


            // Mesh全体のBoundsを再計算する。
            // 遠距離に存在する恒星も正しく描画対象になる。
            m.RecalculateBounds();


            // 完成した恒星フィールドMeshを返す。
            return m;
        }
    }
}