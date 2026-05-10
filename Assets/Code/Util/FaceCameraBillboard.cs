using UnityEngine;

namespace ToJam26.Gameplay.Utility
{
    public class FaceCameraBillboard : MonoBehaviour
    {
        [SerializeField] private Transform targetCamera;

        public void SetTargetCamera(Transform cameraTransform)
        {
            targetCamera = cameraTransform;
        }

        private void LateUpdate()
        {
            Transform currentCamera = targetCamera != null
                ? targetCamera
                : Camera.main != null
                    ? Camera.main.transform
                    : null;

            if (currentCamera == null)
                return;

            transform.rotation = Quaternion.LookRotation(currentCamera.forward, currentCamera.up);
        }
    }
}
