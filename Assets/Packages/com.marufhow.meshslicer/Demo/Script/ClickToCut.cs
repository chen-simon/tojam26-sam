using com.marufhow.meshslicer.core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace com.marufhow.meshslicer.demo
{
    public class ClickToCut : MonoBehaviour
    {
        [Header("Click to cut target vertically. Press SHIFT to cut horizontally")] [SerializeField]
        private MHCutter _mhCutter;

        private void Update()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider.gameObject.CompareTag("Ground")) return;

                    // Check if the Shift key is held down
                    Vector3 cutDirection = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed
                        ? Vector3.up
                        : Vector3.right;

                    _mhCutter.Cut(hit.collider.gameObject, hit.point, cutDirection);
                }
            }
        }



    }
}


 