using UnityEngine;
using UnityEngine.InputSystem;

namespace ToJam26.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] private ScaleController scaleController;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator animator;

        [Header("Input Settings")]
        [SerializeField] private float inputDeadzone = 0.1f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float speedDamping = 10f;

        private CharacterController characterController;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction attackAction;
        private Vector3 movementInput;
        private float verticalVelocity;
        private float smoothedSpeed;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");

        private void OnEnable()
        {
            if (scaleController == null)
                scaleController = GetComponent<ScaleController>();

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();

            if (playerInput != null)
            {
                moveAction = playerInput.actions["Move"];
                attackAction = playerInput.actions["Attack"];
                attackAction.performed += HandleAttack;
            }

            if (scaleController != null)
                scaleController.OnScaleChanged += OnScaleChanged;
        }

        private void OnDisable()
        {
            if (scaleController != null)
                scaleController.OnScaleChanged -= OnScaleChanged;

            if (attackAction != null)
                attackAction.performed -= HandleAttack;
        }

        private void HandleAttack(InputAction.CallbackContext context)
        {
            if (AbleToPerformAttack())
                animator.SetTrigger(AnimAttack);
        }

        private bool AbleToPerformAttack()
        {
            return animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Movement");
        }

        private void Update()
        {
            ReadInput();
            ApplyMovement();
        }

        private void ReadInput()
        {
            Vector2 raw = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            float magnitude = raw.magnitude;
            if (magnitude < inputDeadzone)
            {
                movementInput = Vector3.zero;
                return;
            }

            float analogScale = Mathf.InverseLerp(inputDeadzone, 1f, magnitude);
            Vector2 dir2D = raw / magnitude;

            Transform cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
            Vector3 dir;
            if (cam != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
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
            if (characterController == null || scaleController == null)
                return;

            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            else
                verticalVelocity += Physics.gravity.y * Time.deltaTime;

            Vector3 planarVelocity = scaleController.IsKnockedBack
                ? scaleController.GetKnockbackVelocity()
                : movementInput * scaleController.GetCurrentMovementSpeed();

            Vector3 velocity = planarVelocity;
            velocity.y = verticalVelocity;

            characterController.Move(velocity * Time.deltaTime);

            if (animator != null)
            {
                smoothedSpeed = Mathf.Lerp(smoothedSpeed, characterController.velocity.magnitude, speedDamping * Time.deltaTime);
                animator.SetFloat(AnimSpeed, smoothedSpeed);
            }

            if (!scaleController.IsKnockedBack && movementInput.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movementInput);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private void OnScaleChanged(float newScale, float newMass)
        {
        }

        public float GetCurrentMovementSpeed()
        {
            return scaleController != null ? scaleController.GetCurrentMovementSpeed() : 0f;
        }

        public float GetCurrentScaleRatio()
        {
            return scaleController != null ? scaleController.ScaleRatio : 1f;
        }
    }
}
