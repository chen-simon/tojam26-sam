using UnityEngine;
using UnityEngine.InputSystem;
using ToJam26.Gameplay.Player;

namespace ToJam26.Gameplay.Input
{
    /// <summary>
    /// Example player controller showing how to interact with ScaleController.
    /// Handles movement and uses scale-dependent movement speed.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] private ScaleController scaleController;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator animator;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1f;

        [Header("Input Settings")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private float inputDeadzone = 0.1f;
        [SerializeField] private float rotationSpeed = 720f;

        // Components
        private CharacterController characterController;
        private Vector3 movementInput;
        private float verticalVelocity;

        private void OnEnable()
        {
            if (scaleController == null)
                scaleController = GetComponent<ScaleController>();

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (animator == null)
                animator = GetComponent<Animator>();

            if (scaleController != null)
                scaleController.OnScaleChanged += OnScaleChanged;

            moveAction?.action.Enable();
        }

        private void OnDisable()
        {
            if (scaleController != null)
                scaleController.OnScaleChanged -= OnScaleChanged;

            moveAction?.action.Disable();
        }

        private void Update()
        {
            ReadInput();
            ApplyMovement();
        }

        private void ReadInput()
        {
            Vector2 raw = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

            float magnitude = raw.magnitude;
            if (magnitude < inputDeadzone)
            {
                movementInput = Vector3.zero;
                return;
            }

            float analogScale = Mathf.InverseLerp(inputDeadzone, 1f, magnitude);
            Vector2 dir2D = raw / magnitude;

            Transform cam = cameraTransform ?? Camera.main?.transform;
            Vector3 dir;
            if (cam != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                Vector3 right   = Vector3.ProjectOnPlane(cam.right,   Vector3.up).normalized;
                dir = (forward * dir2D.y + right * dir2D.x).normalized;
            }
            else
            {
                dir = new Vector3(dir2D.x, 0f, dir2D.y).normalized;
            }

            movementInput = dir * analogScale;
        }

        private void ApplyMovement()
        {
            if (characterController == null || scaleController == null || scaleController.IsKnockedBack)
                return;

            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            else
                verticalVelocity += Physics.gravity.y * Time.deltaTime;

            float currentSpeed = scaleController.GetCurrentMovementSpeed();
            Vector3 velocity = movementInput * currentSpeed;
            velocity.y = verticalVelocity;

            characterController.Move(velocity * Time.deltaTime);

            animator.SetFloat("Speed", characterController.velocity.magnitude);

            if (movementInput.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movementInput);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Called when the scale changes (after being sliced).
        /// Can be used to trigger visual effects, sounds, etc.
        /// </summary>
        private void OnScaleChanged(float newScale, float newMass)
        {
            // Example: Could adjust character model size here
            // transform.localScale = Vector3.one * newScale;

            // Or trigger visual feedback
            // PlayHitEffect();
        }

        /// <summary>
        /// Gets the current movement speed (useful for animations or UI).
        /// </summary>
        public float GetCurrentMovementSpeed()
        {
            return scaleController != null ? scaleController.GetCurrentMovementSpeed() : 0f;
        }

        /// <summary>
        /// Gets the current scale ratio (useful for UI or visual effects).
        /// </summary>
        public float GetCurrentScaleRatio()
        {
            return scaleController != null ? scaleController.ScaleRatio : 1f;
        }
    }
}
