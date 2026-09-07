using UnityEngine;
namespace AureateOrrery
{
    public static class SacredGeometryGenerator
    {
        public static Mesh Create(int complexity)
        {
            var g = new CelestialGeometry();
            g.Circle(.72f, .006f);
            for (int i = 0; i < 6; i++) g.Circle(.72f, .004f, CelestialGeometry.Radial(i * Mathf.PI / 3, .72f), 96);
            for (int k = 0; k < complexity; k++)
            {
                float r = 1.48f + k * .055f;
                for (int i = 0; i < 6; i++)
                    g.Segment(CelestialGeometry.Radial(i * Mathf.PI / 3, r), CelestialGeometry.Radial((i + 2) * Mathf.PI / 3, r), .004f);
            }
            return g.Build("Hexagonal observation lattice");
        }
    }
}
