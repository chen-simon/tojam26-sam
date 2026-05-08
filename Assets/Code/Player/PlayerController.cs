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

        [Header("Input Settings")]
        [SerializeField] private float inputDeadzone = 0.1f;

        // Components
        private Rigidbody rigidBody;
        private Vector3 movementInput;

        private void OnEnable()
        {
            if (scaleController == null)
                scaleController = GetComponent<ScaleController>();

            if (rigidBody == null)
                rigidBody = GetComponent<Rigidbody>();

            // Subscribe to scale changes to handle effects
            if (scaleController != null)
                scaleController.OnScaleChanged += OnScaleChanged;
        }

        private void OnDisable()
        {
            if (scaleController != null)
                scaleController.OnScaleChanged -= OnScaleChanged;
        }

        private void Update()
        {
            // Read input
            ReadInput();
        }

        private void FixedUpdate()
        {
            // Apply movement, accounting for current scale/mass
            ApplyMovement();
        }

        private void ReadInput()
        {
            // Example: WASD movement
            movementInput = Vector3.zero;

            if (Keyboard.current.wKey.isPressed)
                movementInput.z += 1f;
            if (Keyboard.current.sKey.isPressed)
                movementInput.z -= 1f;
            if (Keyboard.current.aKey.isPressed)
                movementInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed)
                movementInput.x += 1f;

            // Apply deadzone
            if (movementInput.magnitude < inputDeadzone)
                movementInput = Vector3.zero;
            else
                movementInput = movementInput.normalized;
        }

        private void ApplyMovement()
        {
            if (rigidBody == null || scaleController == null || scaleController.IsKnockedBack)
                return;

            // Get scale-dependent movement speed
            float currentSpeed = scaleController.GetCurrentMovementSpeed();

            // Apply movement
            Vector3 velocity = movementInput * currentSpeed;
            
            // Preserve vertical velocity if using gravity
            velocity.y = rigidBody.linearVelocity.y;

            rigidBody.linearVelocity = velocity;
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
