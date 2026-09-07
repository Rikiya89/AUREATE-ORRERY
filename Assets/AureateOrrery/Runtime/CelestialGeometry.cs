using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AureateOrrery
{
    // 画面上のデバッグラインではなく、
    // 実際の細いチューブ形状のMeshを生成する
    public sealed class CelestialGeometry
    {
        // Mesh生成用の頂点リスト
        readonly List<Vector3> vertices = new();

        // Mesh生成用のインデックスリスト
        readonly List<int> indices = new();

        // 指定されたポイント列に沿ってチューブ形状を生成する
        public void Tube(
            Vector3[] points,
            float width,
            bool closed = true,
            int sides = 5
        )
        {
            // 現在の頂点数を開始位置として保存
            int start = vertices.Count;

            // 各ポイントごとにチューブ断面の頂点を生成
            for (int i = 0; i < points.Length; i++)
            {
                // 前後のポイントから接線方向を計算
                Vector3 tangent =
                    (
                        points[(i + 1) % points.Length]
                        - points[(i - 1 + points.Length) % points.Length]
                    ).normalized;

                // 開いたラインの場合、始点の接線を個別に計算
                if (!closed && i == 0)
                {
                    tangent =
                        (points[1] - points[0]).normalized;
                }

                // 開いたラインの場合、終点の接線を個別に計算
                if (!closed && i == points.Length - 1)
                {
                    tangent =
                        (points[i] - points[i - 1]).normalized;
                }

                // 接線に対して垂直な法線方向を計算
                Vector3 normal =
                    Vector3.Cross(
                        tangent,
                        Mathf.Abs(tangent.z) < .9f
                            ? Vector3.forward
                            : Vector3.up
                    ).normalized;

                // 接線と法線から従法線方向を計算
                Vector3 binormal =
                    Vector3.Cross(tangent, normal);

                // チューブ断面を構成する頂点を円状に配置
                for (int s = 0; s < sides; s++)
                {
                    float a =
                        s * Mathf.PI * 2 / sides;

                    vertices.Add(
                        points[i]
                        + width
                        * (
                            normal * Mathf.Cos(a)
                            + binormal * Mathf.Sin(a)
                        )
                    );
                }
            }

            // 閉じた形状か開いた形状かによって
            // 接続するセグメント数を決定
            int segments =
                closed
                    ? points.Length
                    : points.Length - 1;

            // 隣接する断面同士を三角形で接続
            for (int i = 0; i < segments; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    // 現在の断面の2頂点
                    int a =
                        start
                        + i * sides
                        + s;

                    int b =
                        start
                        + i * sides
                        + (s + 1) % sides;

                    // 次の断面の2頂点
                    int c =
                        start
                        + ((i + 1) % points.Length) * sides
                        + s;

                    int d =
                        start
                        + ((i + 1) % points.Length) * sides
                        + (s + 1) % sides;

                    // 1つ目の三角形
                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(c);

                    // 2つ目の三角形
                    indices.Add(b);
                    indices.Add(d);
                    indices.Add(c);
                }
            }
        }

        // XY平面上に円形のチューブを生成する
        public void Circle(
            float radius,
            float width,
            Vector3 center = default,
            int segments = 192
        )
        {
            // 円周上のポイント配列
            var p = new Vector3[segments];

            // 円周上に均等にポイントを配置
            for (int i = 0; i < segments; i++)
            {
                p[i] =
                    center
                    + Radial(
                        i * Mathf.PI * 2 / segments,
                        radius
                    );
            }

            // 円周ポイントからチューブを生成
            Tube(p, width);
        }

        // 2点を結ぶ直線状のチューブを生成する
        public void Segment(
            Vector3 a,
            Vector3 b,
            float width
        ) =>
            Tube(
                new[] { a, b },
                width,
                false,
                4
            );

        // 角度と半径からXY平面上の座標を取得する
        public static Vector3 Radial(
            float angle,
            float radius
        ) =>
            new(
                radius * Mathf.Cos(angle),
                radius * Mathf.Sin(angle),
                0
            );

        // 保存された頂点とインデックスからMeshを生成する
        public Mesh Build(string name)
        {
            // 32bit indexを使用して大きなMeshにも対応
            var mesh = new Mesh
            {
                name = name,
                indexFormat = IndexFormat.UInt32
            };

            // 頂点と三角形情報をMeshへ設定
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);

            // 法線とBoundsを再計算
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }

    // 天体軌道や数学的な配置に使用する
    // 数学関数をまとめたユーティリティクラス
    public static class MathematicalOrbit
    {
        // 2π
        public const float Tau =
            Mathf.PI * 2;

        // 黄金比 φ
        public static readonly float GoldenRatio =
            (1 + Mathf.Sqrt(5)) / 2;

        // 複数の正弦波を組み合わせた調和軌道
        public static Vector3 Harmonic(
            float t,
            float radius
        ) =>
            new(
                radius * Mathf.Cos(t),
                radius * .72f * Mathf.Sin(2 * t),
                radius * .23f * Mathf.Sin(3 * t)
            );

        // Lissajous曲線をベースにした3次元軌道
        public static Vector3 Lissajous(
            float t,
            float radius
        ) =>
            new(
                radius
                    * Mathf.Sin(
                        2 * t
                        + Mathf.PI / 2
                    ),

                radius
                    * .8f
                    * Mathf.Sin(3 * t),

                radius
                    * .2f
                    * Mathf.Sin(5 * t)
            );

        // 複数の円運動を合成した周転円軌道
        public static Vector3 Epicycle(float t) =>
            CelestialGeometry.Radial(
                t,
                1.12f
            )
            + CelestialGeometry.Radial(
                -3 * t,
                .32f
            )
            + CelestialGeometry.Radial(
                5 * t,
                .10f
            );

        // Fibonacci Sphere方式で球面上に
        // 均等に近いポイントを配置する
        public static Vector3 SpherePoint(
            int i,
            int count,
            float radius
        )
        {
            // -1〜1の範囲でY座標を配置
            float y =
                1
                - 2 * (i + .5f) / count;

            // 黄金角を利用して回転角度を決定
            float theta =
                i
                * Mathf.PI
                * (3 - Mathf.Sqrt(5));

            // 現在のY位置における水平半径
            float r =
                Mathf.Sqrt(
                    1 - y * y
                );

            // 球面上の座標を返す
            return radius
                * new Vector3(
                    r * Mathf.Cos(theta),
                    y,
                    r * Mathf.Sin(theta)
                );
        }
    }
}