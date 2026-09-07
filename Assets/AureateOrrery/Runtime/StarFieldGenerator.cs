using System.Collections.Generic;
using UnityEngine;
namespace AureateOrrery
{
    public static class StarFieldGenerator
    {
        // One mesh, one draw; no particle GameObjects or CPU simulation.
        public static Mesh Create(int count, int seed)
        {
            var random = new System.Random(seed);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                Vector3 p = MathematicalOrbit.SpherePoint(i, count, 24 + (float)random.NextDouble() * 15);
                float size = Mathf.Lerp(.012f, .055f, Mathf.Pow((float)random.NextDouble(), 5));
                Vector3 normal = p.normalized;
                Vector3 right = Vector3.Cross(normal, Vector3.up).normalized * size;
                Vector3 up = Vector3.Cross(right.normalized, normal) * size;
                int n = vertices.Count;
                vertices.Add(p - right - up); vertices.Add(p + right - up); vertices.Add(p + right + up); vertices.Add(p - right + up);
                uv.Add(new(0,0)); uv.Add(new(1,0)); uv.Add(new(1,1)); uv.Add(new(0,1));
                Color c = Color.Lerp(new Color(.4f,.54f,.75f), new Color(1,.84f,.6f), (float)random.NextDouble());
                c *= Mathf.Lerp(.3f, 1.3f, (float)random.NextDouble()); c.a = (float)random.NextDouble();
                for (int j = 0; j < 4; j++) colors.Add(c);
                triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2); triangles.Add(n); triangles.Add(n+2); triangles.Add(n+3);
            }
            var m = new Mesh { name = "Golden-angle fixed stars" };
            m.SetVertices(vertices); m.SetUVs(0,uv); m.SetColors(colors); m.SetTriangles(triangles,0); m.RecalculateBounds(); return m;
        }
    }
}
