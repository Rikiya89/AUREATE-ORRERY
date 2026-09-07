using UnityEngine;
namespace AureateOrrery
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        public CelestialSystem system;
        [Range(20,50)] public float fieldOfView = 32;
        [Range(.65f,.95f)] public float frameFill = .82f;
        [Range(0,.2f)] public float parallax = .055f;
        [Range(0,.03f)] public float breathingDolly = .008f;
        void LateUpdate() { Frame(); }
        public void Frame()
        {
            if(!system) return;
            Camera cameraComponent=GetComponent<Camera>(); cameraComponent.fieldOfView=fieldOfView;
            float radius=3.0f*system.masterScale;
            float limitingHalfAngle=Mathf.Atan(Mathf.Tan(fieldOfView*Mathf.Deg2Rad*.5f)*Mathf.Min(1,cameraComponent.aspect));
            float distance=radius/Mathf.Sin(limitingHalfAngle)/frameFill;
            Vector3 center=system.transform.position;
            transform.position=center+new Vector3(parallax*Mathf.Sin(system.Phase),parallax*.5f*Mathf.Sin(system.Phase+.8f),-distance*(1+breathingDolly*Mathf.Sin(system.Phase-.6f)));
            transform.LookAt(center);
        }
    }
}
