using UnityEngine;
using UnityEngine.InputSystem;
using ToJam26.Gameplay.Equipment;

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
        [SerializeField] private KnifeBlade[] attackHitboxes;
        [SerializeField] private PlayerCameraController playerCameraController;

        [Header("Input Settings")]
        [SerializeField] private float inputDeadzone = 0.1f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float speedDamping = 10f;
        [SerializeField] private float rotationVelContribution = 0.01f;

        [Header("Attack Animation")]
        [SerializeField] private string locomotionStateName = "Movement";
        [SerializeField] private string attackStateName = "Attack";
        [SerializeField] private string attackClipName = "attack";

        private CharacterController characterController;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction attackAction;
        private Vector3 movementInput;
        private float verticalVelocity;
        private float smoothedForwardVel;
        private float smoothedRightVel;
        private float previousYRotation;
        private bool attackHitboxEnabled;
        private bool wasInAttackState;
        private bool gameplayEnabled = true;

        private static readonly int AnimForwardVel = Animator.StringToHash("forward_vel");
        private static readonly int AnimRightVel = Animator.StringToHash("right_vel");
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
                if (playerInput.actions != null)
                {
                    moveAction = playerInput.actions.FindAction("Move", throwIfNotFound: false);
                    attackAction = playerInput.actions.FindAction("Attack", throwIfNotFound: false);

                    if (attackAction != null)
                        attackAction.performed += HandleAttack;
                    else
                        Debug.LogWarning("[PlayerController] PlayerInput is missing an 'Attack' action.", this);

                    if (moveAction == null)
                        Debug.LogWarning("[PlayerController] PlayerInput is missing a 'Move' action.", this);
                }
                else
                {
                    Debug.LogWarning("[PlayerController] PlayerInput has no assigned actions asset.", this);
                }
            }

            if (scaleController != null)
                scaleController.OnScaleChanged += OnScaleChanged;

            ConfigureAttackHitboxes();
            EnsureAnimationEventRelay();
            DisableAttackHitbox();
        }

        private void OnDisable()
        {
            if (scaleController != null)
                scaleController.OnScaleChanged -= OnScaleChanged;

            if (attackAction != null)
                attackAction.performed -= HandleAttack;

            DisableAttackHitbox();
        }

        private void HandleAttack(InputAction.CallbackContext context)
        {
            if (gameplayEnabled && AbleToPerformAttack())
            {
                DisableAttackHitbox();
                animator.SetTrigger(AnimAttack);
            }
        }

        private bool AbleToPerformAttack()
        {
            return animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(locomotionStateName);
        }

        private void Update()
        {
            if (!gameplayEnabled)
            {
                movementInput = Vector3.zero;
                UpdateAttackStateSafety();

                return;
            }

            ReadInput();
            UpdateAttackStateSafety();
            ApplyMovement();
        }

        private void UpdateAttackStateSafety()
        {
            if (animator == null)
                return;

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            string clipName = GetCurrentClipDebugName();
            bool isAttackState = MatchesAttackState(currentState, clipName);

            if (wasInAttackState && !isAttackState && attackHitboxEnabled)
                SetAttackHitboxesEnabled(false);

            wasInAttackState = isAttackState;
        }

        private void EnsureAnimationEventRelay()
        {
            if (animator == null)
                return;

            PlayerAnimationEventRelay relay = animator.GetComponent<PlayerAnimationEventRelay>();
            if (relay == null)
                relay = animator.gameObject.AddComponent<PlayerAnimationEventRelay>();

            relay.Initialize(this);
        }

        private void ConfigureAttackHitboxes()
        {
            if (attackHitboxes == null)
                return;

            foreach (KnifeBlade hitbox in attackHitboxes)
            {
                if (hitbox != null)
                    hitbox.SetOwner(gameObject);
            }
        }

        private string GetCurrentClipDebugName()
        {
            AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfos == null || clipInfos.Length == 0 || clipInfos[0].clip == null)
                return "<none>";

            return clipInfos[0].clip.name;
        }

        private bool MatchesAttackState(AnimatorStateInfo stateInfo, string clipName)
        {
            return stateInfo.IsName(attackStateName) || MatchesClipName(clipName);
        }

        private bool MatchesClipName(string clipName)
        {
            return !string.IsNullOrWhiteSpace(attackClipName) &&
                   string.Equals(clipName, attackClipName, System.StringComparison.OrdinalIgnoreCase);
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

            bool inMovementState = animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(locomotionStateName);
            Vector3 planarVelocity = scaleController.IsKnockedBack
                ? scaleController.GetKnockbackVelocity()
                : inMovementState ? movementInput * scaleController.GetCurrentMovementSpeed() : Vector3.zero;

            Vector3 velocity = planarVelocity;
            velocity.y = verticalVelocity;

            characterController.Move(velocity * Time.deltaTime);

            if (animator != null)
            {
                Vector3 flatVel = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
                float rawForward = Vector3.Dot(flatVel, transform.forward);
                float angularVel = Mathf.DeltaAngle(previousYRotation, transform.eulerAngles.y) / Time.deltaTime;
                previousYRotation = transform.eulerAngles.y;
                float rawRight = Vector3.Dot(flatVel, transform.right) + angularVel * rotationVelContribution;
                smoothedForwardVel = Mathf.Lerp(smoothedForwardVel, rawForward, speedDamping * Time.deltaTime);
                smoothedRightVel = Mathf.Lerp(smoothedRightVel, rawRight, speedDamping * Time.deltaTime);
                animator.SetFloat(AnimForwardVel, smoothedForwardVel);
                animator.SetFloat(AnimRightVel, smoothedRightVel);
            }

            bool isFreeLook = playerCameraController == null || playerCameraController.IsFreeLook;
            if (!scaleController.IsKnockedBack)
            {
                Vector3 facingDir = Vector3.zero;
                if (!isFreeLook && cameraTransform != null)
                    facingDir = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                else if (movementInput.sqrMagnitude > 0f)
                    facingDir = movementInput;

                if (facingDir.sqrMagnitude > 0f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(facingDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
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

        public void EnableAttackHitbox()
        {
            SetAttackHitboxesEnabled(true);
        }

        public void DisableAttackHitbox()
        {
            SetAttackHitboxesEnabled(false);
        }

        public void SetGameplayEnabled(bool enabled)
        {
            gameplayEnabled = enabled;

            if (enabled)
                return;

            movementInput = Vector3.zero;
            verticalVelocity = 0f;
            DisableAttackHitbox();

            if (animator != null)
            {
                // animator.SetFloat("forward_vel", 0f);
                // animator.SetFloat("right_vel", 0f);
            }
        }

        private void SetAttackHitboxesEnabled(bool enabled)
        {
            attackHitboxEnabled = enabled;

            if (attackHitboxes == null)
                return;

            foreach (KnifeBlade hitbox in attackHitboxes)
            {
                if (hitbox != null)
                    hitbox.SetSlicingEnabled(enabled);
            }
        }

        public void SetAnimatorSpeed(float speed)
        {
            if (animator != null)
                animator.speed = speed;
        }

        public Transform GetCameraTransform() => cameraTransform;
        public Animator GetAnimator() => animator;

    }
}
