using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace AureateOrrery.Editor
{
    public static class OrrerySceneBuilder
    {
        const string ScenePath="Assets/AureateOrrery/AureateOrrery.unity";
        [MenuItem("Art/Aureate Orrery/Create Scene")]
        public static void CreateScene()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(File.Exists(ScenePath) && !Application.isBatchMode && !EditorUtility.DisplayDialog("Replace generated scene?", "This replaces only Assets/AureateOrrery/AureateOrrery.unity.", "Replace", "Cancel")) return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var system=new GameObject("AUREATE ORRERY").AddComponent<CelestialSystem>();
            var camera=new GameObject("Portrait Camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.008f,.008f,.02f);
            camera.nearClipPlane=.1f; camera.farClipPlane=120; camera.allowHDR=true;
            var data=camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.gameObject.AddComponent<AudioListener>();
            var framing=camera.gameObject.AddComponent<CameraController>(); framing.system=system; framing.Frame();
            Light("Warm key",new Color(1,.79f,.49f),2.2f,new Vector3(35,-35,0));
            Light("Silver rim",new Color(.55f,.72f,1),1.5f,new Vector3(-25,145,0));
            var point=new GameObject("Core illumination").AddComponent<Light>(); point.type=LightType.Point;
            point.color=new Color(.6f,.77f,1); point.intensity=2; point.range=4;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.06f,.065f,.085f);
            var volume=new GameObject("Restrained bloom + tonal response").AddComponent<Volume>(); volume.isGlobal=true;
            string profilePath="Assets/AureateOrrery/OrreryVolume.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,profilePath);
                var bloom=profile.Add<Bloom>(); bloom.threshold.Override(1.1f); bloom.intensity.Override(.32f); bloom.scatter.Override(.58f);
                var tone=profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
                var vignette=profile.Add<Vignette>(); vignette.intensity.Override(.16f); vignette.smoothness.Override(.6f);
                foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
            }
            volume.sharedProfile=profile;
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
            Selection.activeGameObject=system.gameObject;
            Debug.Log("AUREATE_ORRERY_SCENE_CREATED " + ScenePath);
        }
        static void Light(string label,Color color,float intensity,Vector3 rotation)
        {
            var light=new GameObject(label).AddComponent<Light>(); light.type=LightType.Directional;
            light.color=color; light.intensity=intensity; light.shadows=LightShadows.None; light.transform.rotation=Quaternion.Euler(rotation);
        }
        [MenuItem("Art/Aureate Orrery/Validate Loop")]
        public static void ValidateLoop()
        {
            var system=UnityEngine.Object.FindFirstObjectByType<CelestialSystem>();
            if(!system) throw new InvalidOperationException("Open the Aureate Orrery scene first.");
            system.Evaluate(0); var transforms=system.GetComponentsInChildren<Transform>();
            var positions=new Vector3[transforms.Length]; var rotations=new Quaternion[transforms.Length]; var scales=new Vector3[transforms.Length];
            for(int i=0;i<transforms.Length;i++){positions[i]=transforms[i].localPosition;rotations[i]=transforms[i].localRotation;scales[i]=transforms[i].localScale;}
            system.Evaluate(1);
            for(int i=0;i<transforms.Length;i++)
                if(Vector3.Distance(positions[i],transforms[i].localPosition)>1e-5f || Quaternion.Angle(rotations[i],transforms[i].localRotation)>1e-3f || Vector3.Distance(scales[i],transforms[i].localScale)>1e-5f) throw new Exception("Loop mismatch: "+transforms[i].name);
            const float epsilon=.0001f;
            for(int orbit=0;orbit<3;orbit++)
            {
                var a=CelestialTrails.Position(orbit,0);
                var b=CelestialTrails.Position(orbit,MathematicalOrbit.Tau);
                var startVelocity=(CelestialTrails.Position(orbit,epsilon)-a)/epsilon;
                var endVelocity=(b-CelestialTrails.Position(orbit,MathematicalOrbit.Tau-epsilon))/epsilon;
                if(Vector3.Distance(a,b)>1e-4f || Vector3.Distance(startVelocity,endVelocity)>.05f)
                    throw new Exception("Orbital seam continuity failed");
                if(Vector3.Distance(a,CelestialTrails.Position(orbit,1))<.1f)
                    throw new Exception("Orbital marker did not travel");
            }
            var trailFilter=Array.Find(system.GetComponentsInChildren<MeshFilter>(),f=>f.sharedMesh.name=="Analytic celestial trails");
            if(!trailFilter) throw new Exception("Trail mesh missing");
            system.Evaluate(0); var trailStart=trailFilter.sharedMesh.vertices;
            system.Evaluate(1); var trailEnd=trailFilter.sharedMesh.vertices;
            for(int v=0;v<trailStart.Length;v++) if(Vector3.Distance(trailStart[v],trailEnd[v])>1e-5f) throw new Exception("Trail seam mismatch");
            for(int sample=0;sample<=48;sample++)
            {
                system.Evaluate(sample/48f);
                foreach(var t in transforms)
                    if(!float.IsFinite(t.position.x)||!float.IsFinite(t.position.y)||!float.IsFinite(t.position.z)) throw new Exception("Invalid animated transform");
            }
            system.Evaluate(0);
            foreach(var filter in system.GetComponentsInChildren<MeshFilter>())
                foreach(var vertex in filter.sharedMesh.vertices)
                    if(!float.IsFinite(vertex.x)||!float.IsFinite(vertex.y)||!float.IsFinite(vertex.z)) throw new Exception("Non-finite mesh vertex");
            foreach(string shaderName in new[]{"AureateOrrery/CelestialGlow","Universal Render Pipeline/Lit"})
            {
                var shader=Shader.Find(shaderName); if(!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader error: "+shaderName);
            }
            Debug.Log("AUREATE_ORRERY_VALIDATED: endpoint transforms, orbital seam velocities, trail seam, 49 animation samples, mesh vertices, shader import");
        }
        [MenuItem("Art/Aureate Orrery/Render Portrait Preview")]
        public static void RenderPreview()
        {
            ValidateLoop();
            var camera=Camera.main;
            if(!camera) throw new Exception("Scene camera missing");
            var system=UnityEngine.Object.FindFirstObjectByType<CelestialSystem>();
            var framing=camera.GetComponent<CameraController>();
            var previous=camera.targetTexture;
            float oldAspect=camera.aspect;
            var target=new RenderTexture(1080,1920,24,RenderTextureFormat.ARGBHalf);
            target.Create();
            var image=new Texture2D(1080,1920,TextureFormat.RGB24,false);
            var active=RenderTexture.active;
            try
            {
                camera.aspect=1080f/1920; camera.targetTexture=target;
                system.Evaluate(0); framing.Frame();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1080,1920),0,0); image.Apply();
                Directory.CreateDirectory("ArtworkPreviews");
                File.WriteAllBytes("ArtworkPreviews/AureateOrrery.png",image.EncodeToPNG());
                Debug.Log("AUREATE_ORRERY_PREVIEW_RENDERED ArtworkPreviews/AureateOrrery.png");
            }
            finally
            {
                RenderTexture.active=active; camera.targetTexture=previous; camera.aspect=oldAspect;
                framing.Frame(); target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }
        [MenuItem("Art/Aureate Orrery/Render Motion Study")]
        public static void RenderMotionStudy()
        {
            ValidateLoop();
            var camera=Camera.main;
            var system=UnityEngine.Object.FindFirstObjectByType<CelestialSystem>();
            var framing=camera.GetComponent<CameraController>();
            var previous=camera.targetTexture; var active=RenderTexture.active;
            float oldAspect=camera.aspect, oldPhase=system.Phase/MathematicalOrbit.Tau;
            var target=new RenderTexture(432,768,24,RenderTextureFormat.ARGBHalf);
            target.Create(); var image=new Texture2D(432,768,TextureFormat.RGB24,false);
            try
            {
                Directory.CreateDirectory("ArtworkPreviews/Motion");
                camera.aspect=432f/768; camera.targetTexture=target;
                for(int frame=0;frame<96;frame++)
                {
                    system.Evaluate(frame/96f); framing.Frame();
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                    RenderTexture.active=target; image.ReadPixels(new Rect(0,0,432,768),0,0); image.Apply();
                    File.WriteAllBytes($"ArtworkPreviews/Motion/{frame:D3}.png",image.EncodeToPNG());
                }
                Debug.Log("AUREATE_ORRERY_MOTION_RENDERED: 96 frames, full loop");
            }
            finally
            {
                RenderTexture.active=active; camera.targetTexture=previous; camera.aspect=oldAspect;
                system.Evaluate(oldPhase); framing.Frame(); target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }
        public static void BuildAndValidate() { CreateScene(); ValidateLoop(); }
    }
}
