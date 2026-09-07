using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AureateOrrery
{
    [ExecuteAlways]
    public sealed class CelestialSystem : MonoBehaviour
    {
        [Header("Composition — rebuild after changing geometry")]
        [Range(.5f,2)] public float masterScale = 1;
        [Range(3,8)] public int orbitalRings = 5;
        [Range(0,1)] public float goldenRatioInfluence = .65f;
        [Range(15,75)] public float ringTilt = 52;
        [Range(1,3)] public int geometryComplexity = 1;
        [Range(200,3000)] public int starCount = 1100;
        public int seed = 1729;
        [Header("Clock")]
        [Min(1)] public float loopSeconds = 12;
        [Range(0,2)] public float globalAnimationSpeed = 1;
        [Range(0,12)] public float ringSwingDegrees = 7;
        [Range(0,25)] public float precessionDegrees = 14;
        [Range(0,.8f)] public float trailLength = .38f;
        [Range(0,2)] public float trailStrength = 1;
        [Range(0,1)] public float alignmentGlow = .45f;
        public bool manualPhase;
        [Range(0,1)] public float normalizedPhase;
        [Header("Light")]
        [Range(0,5)] public float emissionStrength = 1.25f;
        public Shader metalShader;
        public Shader glowShader;
        readonly List<Object> owned = new();
        readonly List<Transform> rings = new();
        readonly List<Quaternion> ringBases = new();
        Transform generated, machine, lattice, core, harmonicMarker, lissajousMarker, epicycleMarker, epicycleRing;
        Material gold, inkGold, lightGold, blue, glow, coreMaterial, hazeMaterial, trailMaterial;
        CelestialTrails trails;
        Transform coreReticle, scattering;
        readonly List<Transform> sightBeads = new();
        readonly List<float> ringRadii = new();
        double elapsed;
        public float Phase { get; private set; }
        void OnEnable() { Rebuild(); }
        void OnDisable() { Clear(); }
        void OnValidate() { loopSeconds = Mathf.Max(1, loopSeconds); }
        T Own<T>(T value) where T : Object { value.hideFlags = HideFlags.HideAndDontSave; owned.Add(value); return value; }
        void Clear()
        {
            if (generated) generated.gameObject.SetActive(false);
            // Clear in reverse order so components disappear before their resources.
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i])
            {
                if (Application.isPlaying) Destroy(owned[i]); else DestroyImmediate(owned[i]);
            }
            owned.Clear(); rings.Clear(); ringBases.Clear(); sightBeads.Clear(); ringRadii.Clear(); trails=null; generated = null;
        }
        Transform Node(string label, Transform parent)
        {
            var g = Own(new GameObject(label)); g.transform.SetParent(parent, false); return g.transform;
        }
        Material Metal(string label, Color color, float emission)
        {
            var m = Own(new Material(metalShader) { name = label });
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", .78f); m.SetFloat("_Smoothness", .55f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); return m;
        }
        Transform Draw(string label, Mesh mesh, Material material, Transform parent)
        {
            Own(mesh); var n = Node(label, parent);
            n.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = n.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; return n;
        }
        Transform Orb(string label, float size, Material material, Transform parent)
        {
            var g = Own(GameObject.CreatePrimitive(PrimitiveType.Sphere)); g.name = label;
            g.transform.SetParent(parent, false); g.transform.localScale = Vector3.one * size;
            var collider = g.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off;
            return g.transform;
        }
        [ContextMenu("Rebuild instrument")]
        public void Rebuild()
        {
            Clear();
            if (!metalShader) metalShader = Shader.Find("Universal Render Pipeline/Lit");
            if (!glowShader) glowShader = Shader.Find("AureateOrrery/CelestialGlow");
            if (!metalShader || !glowShader) { Debug.LogError("Aureate Orrery shaders are missing.", this); return; }
            gold = Metal("Aged brass", new Color(.776f,.604f,.322f), .13f);
            inkGold = Metal("Recessed engraving", new Color(.32f,.23f,.11f), .18f);
            lightGold = Metal("Illuminated gold", new Color(.882f,.753f,.471f), .48f);
            blue = Metal("Celestial silver", new Color(.55f,.725f,.91f), emissionStrength);
            glow = Own(new Material(glowShader)); glow.SetColor("_Tint", Color.white);
            generated = Node("Generated instrument (transient)", transform);
            machine = Node("Armillary mechanism", generated);
            var dial = new CelestialGeometry();
            dial.Circle(2.66f,.027f); dial.Circle(2.58f,.01f); dial.Circle(2.39f,.012f); dial.Circle(2.33f,.005f);
            for (int i = 0; i < 180; i++)
            {
                float a = i * MathematicalOrbit.Tau / 180;
                dial.Segment(CelestialGeometry.Radial(a, i % 15 == 0 ? 2.40f : i % 5 == 0 ? 2.47f : 2.51f), CelestialGeometry.Radial(a,2.565f), i % 5 == 0 ? .006f : .003f);
            }
            // Twelve abstract observation stations; no historical script is claimed.
            for (int i = 0; i < 12; i++)
            {
                float a = i * MathematicalOrbit.Tau / 12;
                Vector3 p = CelestialGeometry.Radial(a,2.79f), tangent = CelestialGeometry.Radial(a + Mathf.PI/2,.035f), radial = CelestialGeometry.Radial(a,.065f);
                dial.Segment(p+radial,p+tangent,.007f); dial.Segment(p+tangent,p-radial,.007f);
                dial.Segment(p-radial,p-tangent,.007f); dial.Segment(p-tangent,p+radial,.007f);
            }
            var face = Draw("180-division ecliptic dial",dial.Build("Engraved dial"),gold,machine);
            face.localRotation = Quaternion.Euler(12,-8,0);
            for (int i = 0; i < orbitalRings; i++)
            {
                float u = i / (float)(orbitalRings - 1);
                float radius = Mathf.Lerp(1.22f,2.27f, Mathf.Lerp(u,Mathf.Pow(u,1/MathematicalOrbit.GoldenRatio),goldenRatioInfluence));
                var g = new CelestialGeometry(); g.Circle(radius,.016f); g.Circle(radius-.045f,.004f);
                for (int j = 0; j < 60; j++)
                {
                    float a = j * MathematicalOrbit.Tau / 60;
                    g.Segment(CelestialGeometry.Radial(a,radius),CelestialGeometry.Radial(a,radius-(j%5==0?.085f:.034f)),.003f);
                }
                var ring = Draw("Armillary ring " + (i+1), g.Build("Brass meridian"),i%2==0?gold:lightGold,machine);
                var q = Quaternion.Euler(ringTilt * (i%2==0?1:-1), i*37-60, i*29);
                rings.Add(ring); ringBases.Add(q);
                var bead=Orb("Travelling sight bead",.055f,blue,ring);
                sightBeads.Add(bead); ringRadii.Add(radius);
            }
            lattice = Draw("Hexagonal calculation plate",SacredGeometryGenerator.Create(geometryComplexity),inkGold,machine);
            lattice.localPosition = new Vector3(0,0,.45f);
            var harmonic = new CelestialGeometry(); var lissajous = new CelestialGeometry(); var epi = new CelestialGeometry();
            var hp = new Vector3[384]; var lp = new Vector3[384]; var ep = new Vector3[384];
            for (int i=0;i<384;i++) { float t=i*MathematicalOrbit.Tau/384; hp[i]=MathematicalOrbit.Harmonic(t,1.85f); lp[i]=MathematicalOrbit.Lissajous(t,1.55f); ep[i]=MathematicalOrbit.Epicycle(t); }
            harmonic.Tube(hp,.004f); lissajous.Tube(lp,.003f); epi.Tube(ep,.003f);
            Draw("1-2-3 harmonic observation curve",harmonic.Build("Harmonic"),lightGold,machine);
            Draw("2-3-5 celestial curve",lissajous.Build("Lissajous"),inkGold,machine);
            Draw("Epicycle record",epi.Build("Epicycle"),inkGold,machine);
            harmonicMarker=Orb("Harmonic star",.075f,blue,machine);
            lissajousMarker=Orb("Lissajous star",.05f,lightGold,machine);
            epicycleMarker=Orb("Epicycle pointer",.065f,blue,machine);
            var e=new CelestialGeometry(); e.Circle(.32f,.005f);
            epicycleRing=Draw("Moving deferent",e.Build("Epicycle ring"),lightGold,machine);
            coreMaterial=Metal("Luminous core",new Color(.72f,.84f,1),3.4f);
            core=Orb("Cold celestial core",.19f,coreMaterial,machine);
            hazeMaterial=Own(new Material(glowShader)); hazeMaterial.SetColor("_Tint",new Color(.065f,.10f,.17f,1));
            var haze=new Mesh { name="Soft core scattering" };
            haze.vertices=new[]{new Vector3(-1.1f,-1.1f,.12f),new Vector3(1.1f,-1.1f,.12f),new Vector3(1.1f,1.1f,.12f),new Vector3(-1.1f,1.1f,.12f)};
            haze.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
            haze.colors=new[]{Color.white,Color.white,Color.white,Color.white}; haze.triangles=new[]{0,1,2,0,2,3}; haze.RecalculateBounds();
            scattering=Draw("Faint central scattering",haze,hazeMaterial,machine);
            var halo=new CelestialGeometry(); halo.Circle(.28f,.008f); halo.Circle(.38f,.004f);
            coreReticle=Draw("Core reticle",halo.Build("Core rings"),lightGold,machine);
            trails=new CelestialTrails();
            trailMaterial=Own(new Material(glowShader)); trailMaterial.SetColor("_Tint",Color.white);
            Draw("Three fading orbital histories",trails.Mesh,trailMaterial,machine);
            // Sparse spherical reference points linked along short local chords.
            var net = new CelestialGeometry();
            for(int i=0;i<26;i++)
            {
                Vector3 p=MathematicalOrbit.SpherePoint(i,26,1.03f);
                Orb("Reference star "+i,.022f,blue,machine).localPosition=p;
                int nearest=-1; float distance=float.MaxValue;
                for(int j=i+1;j<26;j++) { float d=(p-MathematicalOrbit.SpherePoint(j,26,1.03f)).sqrMagnitude; if(d<distance){distance=d;nearest=j;} }
                if(nearest>=0 && distance<.75f) net.Segment(p,MathematicalOrbit.SpherePoint(nearest,26,1.03f),.0025f);
            }
            Draw("Spherical star catalogue",net.Build("Local celestial chords"),inkGold,machine);
            Draw("Distant fixed stars",StarFieldGenerator.Create(starCount,seed),glow,generated);
            Evaluate(0);
        }
        void Update()
        {
            if (!generated) return;
            if(Application.isPlaying) elapsed += Time.deltaTime * globalAnimationSpeed;
            Evaluate(manualPhase ? normalizedPhase : (float)((elapsed / loopSeconds) % 1));
        }
        public void Evaluate(float normalizedTime)
        {
            if(!generated) return;
            Phase = Mathf.Repeat(normalizedTime,1) * MathematicalOrbit.Tau;
            machine.localScale = Vector3.one * masterScale;
            // Quadrature separates tilt, yaw and roll instead of rocking every layer together.
            machine.localRotation = Quaternion.Euler(4*Mathf.Sin(Phase),7*Mathf.Sin(Phase+.7f),2*Mathf.Sin(Phase-.4f));
            for(int i=0;i<rings.Count;i++)
            {
                float offset=i*MathematicalOrbit.Tau/MathematicalOrbit.GoldenRatio;
                float direction=i%2==0?1:-1;
                rings[i].localRotation=Quaternion.Euler(
                    precessionDegrees*Mathf.Sin(Phase+offset),
                    precessionDegrees*.65f*Mathf.Cos(Phase+offset),
                    direction*ringSwingDegrees*Mathf.Sin(Phase+offset*.5f))*ringBases[i];
                sightBeads[i].localPosition=CelestialGeometry.Radial(direction*CelestialTrails.Travel(Phase)+offset,ringRadii[i]);
            }
            lattice.localRotation=Quaternion.Euler(5*Mathf.Sin(Phase+.8f),-7*Mathf.Sin(Phase),-9*Mathf.Sin(Phase-.5f));
            harmonicMarker.localPosition=CelestialTrails.Position(0,Phase);
            lissajousMarker.localPosition=CelestialTrails.Position(1,Phase);
            epicycleMarker.localPosition=CelestialTrails.Position(2,Phase);
            epicycleRing.localPosition=CelestialGeometry.Radial(CelestialTrails.Travel(Phase)+2.4f,1.12f);
            // A broad, smooth light crest marks the once-per-cycle transit near the seam.
            float alignment=Mathf.Pow(.5f+.5f*Mathf.Cos(Phase),6)*alignmentGlow;
            core.localScale=Vector3.one*(.19f+.009f*Mathf.Sin(Phase-.5f)+.012f*alignment);
            coreReticle.localRotation=Quaternion.Euler(18*Mathf.Sin(Phase),22*Mathf.Cos(Phase),0);
            scattering.localScale=Vector3.one*(1+.10f*Mathf.Sin(Phase-.5f)+.12f*alignment);
            hazeMaterial.SetColor("_Tint",new Color(.065f,.10f,.17f)*(1+alignment));
            coreMaterial.SetColor("_EmissionColor",new Color(.72f,.84f,1)*(3.4f+alignment*2));
            blue.SetColor("_EmissionColor",new Color(.55f,.725f,.91f)*emissionStrength*(1+.12f*Mathf.Sin(Phase)+alignment*.25f));
            trails.Evaluate(Phase,trailLength,trailStrength);
            trailMaterial.SetFloat("_Phase",Phase);
            glow.SetFloat("_Phase",Phase);
        }
    }
}
