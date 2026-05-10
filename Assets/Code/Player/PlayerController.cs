using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ToJam26.Gameplay.Equipment;

namespace ToJam26.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        public static event System.Action AttackStarted;

        [Header("Controllers")]
        [SerializeField] private ScaleController scaleController;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator animator;
        [SerializeField] private Animator hitUIAnimator;
        [SerializeField] private KnifeBlade[] attackHitboxes;
        [SerializeField] private PlayerCameraController playerCameraController;
        [SerializeField] private float defaultKoWarningVolumeThreshold = 0.4f;

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
        private Animator sweatEffectAnimator;
        private readonly List<Animator> hitReactionEffectAnimators = new();
        private readonly List<Coroutine> hitReactionHideCoroutines = new();
        private bool sweatEffectVisible;
        private float cachedKoWarningVolumeThreshold = -1f;

        private static readonly int AnimForwardVel = Animator.StringToHash("forward_vel");
        private static readonly int AnimRightVel = Animator.StringToHash("right_vel");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimGotHit = Animator.StringToHash("Got Hit");
        private const string SweatEffectObjectName = "effect-sweat";
        private const string HitEffectLeftObjectName = "effect-gothit-left";
        private const string HitEffectRightObjectName = "effect-gothit-right";

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
            InitializeLocalEffects();
            DisableAttackHitbox();
        }

        private void OnDisable()
        {
            if (scaleController != null)
                scaleController.OnScaleChanged -= OnScaleChanged;

            if (attackAction != null)
                attackAction.performed -= HandleAttack;

            DisableAttackHitbox();
            StopAndHideLocalEffects();
        }

        private void HandleAttack(InputAction.CallbackContext context)
        {
            if (gameplayEnabled && AbleToPerformAttack())
            {
                DisableAttackHitbox();
                AttackStarted?.Invoke();
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

        private void InitializeLocalEffects()
        {
            ResolveLocalEffects();
            PrepareReusableEffect(sweatEffectAnimator, hideImmediately: true);

            for (int index = 0; index < hitReactionEffectAnimators.Count; index++)
            {
                PrepareReusableEffect(hitReactionEffectAnimators[index], hideImmediately: true);
            }

            UpdateSweatEffectState(forceRefresh: true);
        }

        private void ResolveLocalEffects()
        {
            if (sweatEffectAnimator == null)
                sweatEffectAnimator = FindChildAnimatorByName(SweatEffectObjectName);

            if (!HasHitReactionEffect(HitEffectLeftObjectName))
                TryAddHitReactionEffect(HitEffectLeftObjectName);

            if (!HasHitReactionEffect(HitEffectRightObjectName))
                TryAddHitReactionEffect(HitEffectRightObjectName);

            while (hitReactionHideCoroutines.Count < hitReactionEffectAnimators.Count)
                hitReactionHideCoroutines.Add(null);
        }

        private bool HasHitReactionEffect(string effectObjectName)
        {
            foreach (Animator effectAnimator in hitReactionEffectAnimators)
            {
                if (effectAnimator != null &&
                    string.Equals(effectAnimator.name, effectObjectName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void TryAddHitReactionEffect(string effectObjectName)
        {
            Animator effectAnimator = FindChildAnimatorByName(effectObjectName);
            if (effectAnimator != null)
                hitReactionEffectAnimators.Add(effectAnimator);
        }

        private Animator FindChildAnimatorByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (!string.Equals(descendant.name, objectName, System.StringComparison.Ordinal))
                    continue;

                return descendant.GetComponent<Animator>();
            }

            return null;
        }

        private void PrepareReusableEffect(Animator effectAnimator, bool hideImmediately)
        {
            if (effectAnimator == null)
                return;

            DestroyAfterSeconds destroyAfterSeconds = effectAnimator.GetComponent<DestroyAfterSeconds>();
            if (destroyAfterSeconds != null)
                destroyAfterSeconds.enabled = false;

            if (hideImmediately)
                effectAnimator.gameObject.SetActive(false);
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
            if (!scaleController.IsKnockedBack && inMovementState)
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
            UpdateSweatEffectState(forceRefresh: false);
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

        public void SetHitUIAnimator(Animator animator)
        {
            hitUIAnimator = animator;
        }

        public void TriggerHitUI()
        {
            if (hitUIAnimator != null)
                hitUIAnimator.SetTrigger(AnimGotHit);

            PlayHitReactionEffects();
        }

        public Transform GetCameraTransform() => cameraTransform;
        public Animator GetAnimator() => animator;
        public PlayerCameraController GetCameraController() => playerCameraController;

        private void PlayHitReactionEffects()
        {
            ResolveLocalEffects();

            for (int index = 0; index < hitReactionEffectAnimators.Count; index++)
            {
                Animator effectAnimator = hitReactionEffectAnimators[index];
                if (effectAnimator == null)
                    continue;

                if (hitReactionHideCoroutines[index] != null)
                    StopCoroutine(hitReactionHideCoroutines[index]);

                RestartEffectAnimator(effectAnimator);
                float playbackDuration = GetEffectPlaybackDuration(effectAnimator);
                hitReactionHideCoroutines[index] = StartCoroutine(HideEffectAfterDelay(index, effectAnimator.gameObject, playbackDuration));
            }
        }

        private IEnumerator HideEffectAfterDelay(int effectIndex, GameObject effectObject, float delaySeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, delaySeconds));

            if (effectObject != null)
                effectObject.SetActive(false);

            if (effectIndex >= 0 && effectIndex < hitReactionHideCoroutines.Count)
                hitReactionHideCoroutines[effectIndex] = null;
        }

        private void UpdateSweatEffectState(bool forceRefresh)
        {
            ResolveLocalEffects();

            if (sweatEffectAnimator == null)
                return;

            bool shouldShowSweat = scaleController != null &&
                                   !scaleController.IsEliminated &&
                                   scaleController.currentVolumeRatio < ResolveKoWarningVolumeThreshold();

            if (!forceRefresh && sweatEffectVisible == shouldShowSweat)
                return;

            sweatEffectVisible = shouldShowSweat;

            if (shouldShowSweat)
            {
                RestartEffectAnimator(sweatEffectAnimator);
                return;
            }

            sweatEffectAnimator.gameObject.SetActive(false);
        }

        private float ResolveKoWarningVolumeThreshold()
        {
            if (cachedKoWarningVolumeThreshold >= 0f)
                return cachedKoWarningVolumeThreshold;

            if (attackHitboxes != null)
            {
                foreach (KnifeBlade attackHitbox in attackHitboxes)
                {
                    if (attackHitbox == null)
                        continue;

                    cachedKoWarningVolumeThreshold = attackHitbox.KoVolumeThreshold;
                    return cachedKoWarningVolumeThreshold;
                }
            }

            cachedKoWarningVolumeThreshold = defaultKoWarningVolumeThreshold;
            return cachedKoWarningVolumeThreshold;
        }

        private void RestartEffectAnimator(Animator effectAnimator)
        {
            if (effectAnimator == null)
                return;

            GameObject effectObject = effectAnimator.gameObject;
            effectObject.SetActive(true);
            effectAnimator.speed = 1f;
            effectAnimator.Rebind();
            effectAnimator.Update(0f);

            string stateName = null;
            AnimationClip[] clips = effectAnimator.runtimeAnimatorController != null
                ? effectAnimator.runtimeAnimatorController.animationClips
                : null;

            if (clips != null && clips.Length > 0 && clips[0] != null)
                stateName = clips[0].name;

            if (!string.IsNullOrWhiteSpace(stateName))
                effectAnimator.Play(stateName, 0, 0f);
        }

        private float GetEffectPlaybackDuration(Animator effectAnimator)
        {
            if (effectAnimator == null || effectAnimator.runtimeAnimatorController == null)
                return 0.4f;

            AnimationClip[] clips = effectAnimator.runtimeAnimatorController.animationClips;
            float longestClipLength = 0f;
            if (clips != null)
            {
                foreach (AnimationClip clip in clips)
                {
                    if (clip != null)
                        longestClipLength = Mathf.Max(longestClipLength, clip.length);
                }
            }

            if (longestClipLength <= 0f)
                longestClipLength = 0.4f;

            return longestClipLength / Mathf.Max(0.01f, Mathf.Abs(effectAnimator.speed));
        }

        private void StopAndHideLocalEffects()
        {
            for (int index = 0; index < hitReactionHideCoroutines.Count; index++)
            {
                if (hitReactionHideCoroutines[index] != null)
                {
                    StopCoroutine(hitReactionHideCoroutines[index]);
                    hitReactionHideCoroutines[index] = null;
                }
            }

            foreach (Animator effectAnimator in hitReactionEffectAnimators)
            {
                if (effectAnimator != null)
                    effectAnimator.gameObject.SetActive(false);
            }

            if (sweatEffectAnimator != null)
                sweatEffectAnimator.gameObject.SetActive(false);

            sweatEffectVisible = false;
        }

    }
}
