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

        [Header("Input Settings")]
        [SerializeField] private float inputDeadzone = 0.1f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float speedDamping = 10f;

        [Header("Attack Window")]
        [SerializeField] private string locomotionStateName = "Movement";
        [SerializeField] private string attackStateName = "Attack";
        [SerializeField] private string attackClipName = "attack";
        [SerializeField, Range(0f, 1f)] private float attackHitboxStartNormalizedTime = 0.2f;
        [SerializeField, Range(0f, 1f)] private float attackHitboxEndNormalizedTime = 0.6f;
        [SerializeField] private bool debugAttackWindow = false;

        private CharacterController characterController;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction attackAction;
        private Vector3 movementInput;
        private float verticalVelocity;
        private float smoothedSpeed;
        private bool attackHitboxEnabled;
        private bool wasInAttackState;
        private int lastLoggedStateHash = int.MinValue;
        private string lastLoggedClipName = string.Empty;
        private bool lastLoggedInTransition;

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
            if (AbleToPerformAttack())
            {
                DisableAttackHitbox();
                animator.SetTrigger(AnimAttack);

                if (debugAttackWindow)
                {
                    Debug.Log(
                        $"[PlayerController] Attack trigger sent. Waiting for state '{attackStateName}'.",
                        this);
                }
            }
            else if (debugAttackWindow && animator != null)
            {
                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
                string clipName = GetCurrentClipDebugName();
                Debug.Log(
                    $"[PlayerController] Attack input ignored. Current state is '{GetCurrentStateDebugName(currentState, clipName)}'. Expected locomotion state '{locomotionStateName}'.",
                    this);
            }
        }

        private bool AbleToPerformAttack()
        {
            return animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(locomotionStateName);
        }

        private void Update()
        {
            ReadInput();
            UpdateAttackHitboxWindow();
            ApplyMovement();
        }

        private void UpdateAttackHitboxWindow()
        {
            if (animator == null)
                return;

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            string clipName = GetCurrentClipDebugName();
            LogAnimatorDebugState(currentState, clipName);
            bool isAttackState = MatchesAttackState(currentState, clipName);
            float normalizedTime = currentState.normalizedTime % 1f;

            if (debugAttackWindow && isAttackState && !wasInAttackState)
            {
                Debug.Log(
                    $"[PlayerController] Entered attack window source '{GetCurrentStateDebugName(currentState, clipName)}'. Window={attackHitboxStartNormalizedTime:F2}-{attackHitboxEndNormalizedTime:F2}.",
                    this);
            }

            if (!isAttackState)
            {
                if (attackHitboxEnabled)
                    SetAttackHitboxesEnabled(false);

                if (debugAttackWindow && wasInAttackState)
                {
                    Debug.Log(
                        $"[PlayerController] Left attack state. Current state is '{GetCurrentStateDebugName(currentState, clipName)}'.",
                        this);
                }

                wasInAttackState = false;
                return;
            }

            bool shouldEnableHitbox =
                normalizedTime >= attackHitboxStartNormalizedTime &&
                normalizedTime <= attackHitboxEndNormalizedTime;

            if (shouldEnableHitbox != attackHitboxEnabled)
            {
                SetAttackHitboxesEnabled(shouldEnableHitbox);

                if (debugAttackWindow)
                {
                    Debug.Log(
                        $"[PlayerController] Attack state '{GetCurrentStateDebugName(currentState, clipName)}' time={normalizedTime:F2}, hitbox={(shouldEnableHitbox ? "ON" : "OFF")}.",
                        this);
                }
            }

            wasInAttackState = true;
        }

        private void LogAnimatorDebugState(AnimatorStateInfo currentState, string clipName)
        {
            if (!debugAttackWindow)
                return;

            bool inTransition = animator.IsInTransition(0);
            if (currentState.shortNameHash == lastLoggedStateHash &&
                clipName == lastLoggedClipName &&
                inTransition == lastLoggedInTransition)
            {
                return;
            }

            lastLoggedStateHash = currentState.shortNameHash;
            lastLoggedClipName = clipName;
            lastLoggedInTransition = inTransition;

            Debug.Log(
                $"[PlayerController] Animator state update: clip='{clipName}', shortHash={currentState.shortNameHash}, fullHash={currentState.fullPathHash}, normalizedTime={(currentState.normalizedTime % 1f):F2}, inTransition={inTransition}, matchesLocomotion={currentState.IsName(locomotionStateName)}, matchesAttackState={currentState.IsName(attackStateName)}, matchesAttackClip={MatchesClipName(clipName)}.",
                this);
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

        public void EnableAttackHitbox()
        {
            SetAttackHitboxesEnabled(true);
        }

        public void DisableAttackHitbox()
        {
            SetAttackHitboxesEnabled(false);
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

        private string GetCurrentStateDebugName(AnimatorStateInfo stateInfo, string clipName)
        {
            return stateInfo.IsName(attackStateName) ? attackStateName :
                stateInfo.IsName(locomotionStateName) ? locomotionStateName :
                MatchesClipName(clipName) ? $"clip:{clipName}" :
                $"hash:{stateInfo.shortNameHash}";
        }
    }
}
