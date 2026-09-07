using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AureateOrrery
{
    // Builds actual narrow tubes rather than screen-space debug lines.
    public sealed class CelestialGeometry
    {
        readonly List<Vector3> vertices = new();
        readonly List<int> indices = new();
        public void Tube(Vector3[] points, float width, bool closed = true, int sides = 5)
        {
            int start = vertices.Count;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 tangent = (points[(i + 1) % points.Length] - points[(i - 1 + points.Length) % points.Length]).normalized;
                if (!closed && i == 0) tangent = (points[1] - points[0]).normalized;
                if (!closed && i == points.Length - 1) tangent = (points[i] - points[i - 1]).normalized;
                Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.z) < .9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides;
                    vertices.Add(points[i] + width * (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)));
                }
            }
            int segments = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < segments; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + i * sides + s, b = start + i * sides + (s + 1) % sides;
                    int c = start + ((i + 1) % points.Length) * sides + s;
                    int d = start + ((i + 1) % points.Length) * sides + (s + 1) % sides;
                    indices.Add(a); indices.Add(b); indices.Add(c);
                    indices.Add(b); indices.Add(d); indices.Add(c);
                }
        }
        public void Circle(float radius, float width, Vector3 center = default, int segments = 192)
        {
            var p = new Vector3[segments];
            for (int i = 0; i < segments; i++) p[i] = center + Radial(i * Mathf.PI * 2 / segments, radius);
            Tube(p, width);
        }
        public void Segment(Vector3 a, Vector3 b, float width) => Tube(new[] { a, b }, width, false, 4);
        public static Vector3 Radial(float angle, float radius) => new(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0);
        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
    public static class MathematicalOrbit
    {
        public const float Tau = Mathf.PI * 2;
        public static readonly float GoldenRatio = (1 + Mathf.Sqrt(5)) / 2;
        public static Vector3 Harmonic(float t, float radius) => new(radius * Mathf.Cos(t), radius * .72f * Mathf.Sin(2 * t), radius * .23f * Mathf.Sin(3 * t));
        public static Vector3 Lissajous(float t, float radius) => new(radius * Mathf.Sin(2 * t + Mathf.PI / 2), radius * .8f * Mathf.Sin(3 * t), radius * .2f * Mathf.Sin(5 * t));
        public static Vector3 Epicycle(float t) => CelestialGeometry.Radial(t, 1.12f) + CelestialGeometry.Radial(-3 * t, .32f) + CelestialGeometry.Radial(5 * t, .10f);
        public static Vector3 SpherePoint(int i, int count, float radius)
        {
            float y = 1 - 2 * (i + .5f) / count;
            float theta = i * Mathf.PI * (3 - Mathf.Sqrt(5));
            float r = Mathf.Sqrt(1 - y * y);
            return radius * new Vector3(r * Mathf.Cos(theta), y, r * Mathf.Sin(theta));
        }
    }
}
