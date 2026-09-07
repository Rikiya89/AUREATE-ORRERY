using UnityEngine;
namespace AureateOrrery
{
    // Analytic history: seeking, pausing and looping never leave stale particles.
    // Three trails share one dynamic mesh and one material, with reused buffers.
    public sealed class CelestialTrails
    {
        const int Samples = 64;
        readonly Vector3[] vertices = new Vector3[Samples * 3 * 4];
        public Mesh Mesh { get; }
        public CelestialTrails()
        {
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[Samples * 3 * 6];
            for(int orbit=0;orbit<3;orbit++)
                for(int i=0;i<Samples;i++)
                {
                    int quad=orbit*Samples+i, v=quad*4, t=quad*6;
                    float fade=Mathf.Pow(1-i/(float)Samples,1.7f);
                    Color color=(orbit==1 ? new Color(1,.72f,.32f) : new Color(.48f,.73f,1))*fade*1.8f;
                    color.a=.25f;
                    for(int j=0;j<4;j++) colors[v+j]=color;
                    uv[v]=new(0,0); uv[v+1]=new(1,0); uv[v+2]=new(1,1); uv[v+3]=new(0,1);
                    triangles[t]=v; triangles[t+1]=v+1; triangles[t+2]=v+2;
                    triangles[t+3]=v; triangles[t+4]=v+2; triangles[t+5]=v+3;
                }
            Mesh=new Mesh { name="Analytic celestial trails" };
            Mesh.MarkDynamic(); Mesh.vertices=vertices; Mesh.uv=uv; Mesh.colors=colors; Mesh.triangles=triangles;
            Mesh.bounds=new Bounds(Vector3.zero,Vector3.one*5);
        }
        public static float Travel(float phase) => phase-.24f*Mathf.Sin(phase);
        public static Vector3 Position(int orbit,float phase)
        {
            float t=Travel(phase);
            return orbit==0 ? MathematicalOrbit.Harmonic(t+.4f,1.85f)
                : orbit==1 ? MathematicalOrbit.Lissajous(-t+1.5f,1.55f)
                : MathematicalOrbit.Epicycle(t+2.4f);
        }
        public void Evaluate(float phase,float length,float strength)
        {
            for(int orbit=0;orbit<3;orbit++)
                for(int i=0;i<Samples;i++)
                {
                    float u=i/(float)Samples;
                    Vector3 p=Position(orbit,phase-u*length);
                    float radius=Mathf.Lerp(.030f,.006f,u)*strength;
                    int v=(orbit*Samples+i)*4;
                    vertices[v]=p+new Vector3(-radius,-radius,0); vertices[v+1]=p+new Vector3(radius,-radius,0);
                    vertices[v+2]=p+new Vector3(radius,radius,0); vertices[v+3]=p+new Vector3(-radius,radius,0);
                }
            Mesh.SetVertices(vertices);
        }
    }
}
