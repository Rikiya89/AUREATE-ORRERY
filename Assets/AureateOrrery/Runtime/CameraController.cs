using UnityEngine;

namespace AureateOrrery
{
    // エディタ上でも実行し、Camera コンポーネントを必須にする
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        // 制御対象の天体系
        public CelestialSystem system;

        [Range(50, 85)] public float focalLength = 75;

        // 天体系を画面内にどの程度収めるか
        [Range(.65f, .95f)]
        public float frameFill = .82f;

        // カメラの微細な視差移動量
        [Range(0, .2f)]
        public float parallax = .018f;

        // カメラを前後にゆっくり動かす量
        [Range(0, .03f)]
        public float breathingDolly = .002f;

        PhotographicSetup photographicSetup;
        float dustAspect, dustScale;

        void OnDisable()
        {
            photographicSetup?.Dispose();
            photographicSetup = null;
        }

        // 毎フレーム最後にカメラ位置を更新する
        void LateUpdate()
        {
            Frame();
        }

        public void Frame()
        {
            // system が設定されていない場合は処理しない
            if (!system)
                return;

            // Camera コンポーネントを取得して視野角を設定
            Camera cameraComponent = GetComponent<Camera>();
            cameraComponent.usePhysicalProperties = true;
            // Preserve the existing vertical framing with a 75 mm full-frame lens.
            cameraComponent.sensorSize = new Vector2(36, 24);
            cameraComponent.gateFit = Camera.GateFitMode.Vertical;
            cameraComponent.focalLength = focalLength;
            cameraComponent.aperture = 2.8f;
            cameraComponent.iso = 100;
            cameraComponent.shutterSpeed = 1f / 48;
            float opticalFieldOfView = Camera.FocalLengthToFieldOfView(focalLength, 24);

            // 天体系全体のおおよその半径を取得
            float radius = 3.0f * system.masterScale;

            // FOV と画面比率から、表示範囲を制限する角度を計算
            float limitingHalfAngle = Mathf.Atan(
                Mathf.Tan(
                    opticalFieldOfView
                    * Mathf.Deg2Rad
                    * .5f
                )
                * Mathf.Min(
                    1,
                    cameraComponent.aspect
                )
            );

            // 天体系が画面内に収まるカメラ距離を計算
            float distance =
                radius
                / Mathf.Sin(limitingHalfAngle)
                / frameFill;

            // 天体系の中心位置
            Vector3 center = system.transform.position;

            // パララックスと呼吸するような前後移動を加えて
            // カメラ位置を決定する
            transform.position =
                center
                + new Vector3(
                    parallax
                        * Mathf.Sin(system.Phase),

                    parallax
                        * .5f
                        * Mathf.Sin(system.Phase + .8f),

                    -distance
                        * (
                            1
                            + breathingDolly
                            * Mathf.Sin(system.Phase - .6f)
                        )
                );

            // 常に天体系の中心を向く
            transform.LookAt(center);
            if (!Resources.Load<Shader>("PhotographicDust")) return;
            if (photographicSetup != null &&
                (!Mathf.Approximately(dustAspect,cameraComponent.aspect) || !Mathf.Approximately(dustScale,system.masterScale)))
            {
                photographicSetup.Dispose();
                photographicSetup = null;
            }
            if (photographicSetup == null)
            {
                photographicSetup = new PhotographicSetup(cameraComponent, system);
                dustAspect = cameraComponent.aspect;
                dustScale = system.masterScale;
            }
            photographicSetup.Update();
        }
    }
}