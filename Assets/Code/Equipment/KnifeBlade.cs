using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using ToJam26.Gameplay.Slicing;
using ToJam26.Gameplay.Player;
using ToJam26.Gameplay.Manager;
using ToJam26.Gameplay.Utility;

namespace ToJam26.Gameplay.Equipment
{
    public class KnifeBlade : MonoBehaviour, IKnife
    {
        public static event System.Action<bool> HitResolved;
        public static event System.Action BladeClashed;

        [Header("Knife Settings")]
        [SerializeField] private GameObject owner;
        [SerializeField] private float cuttingForce = 5f;
        [SerializeField] private Collider bladeCollider;
        [SerializeField] private bool startEnabled = false;

        [SerializeField] private GameObject hitParticlePrefab;
        [SerializeField] private GameObject hitStarParticlePrefab;
        [SerializeField] private GameObject cheeseParticlePrefab;
        [SerializeField] private GameObject cutParticlePrefab;
        [SerializeField] private GameObject koParticlePrefab;
        [FormerlySerializedAs("koScaleThreshold")]
        [SerializeField] private float koVolumeThreshold = 0.4f;

        [Header("Slicing Settings")]
        [SerializeField] private Vector3 sliceNormal = Vector3.up;
        [FormerlySerializedAs("debugMode")]
        [SerializeField] private bool enableDebugLogs = false;

        private bool slicingEnabled;
        private bool sliceConsumedThisWindow;
        private static readonly HashSet<ulong> ActiveBladeClashes = new();
        private static readonly HashSet<KnifeBlade> RegisteredBlades = new();

        public float CuttingForce => cuttingForce;
        public GameObject Owner => owner;
        public Collider BladeCollider => bladeCollider;
        public float KoVolumeThreshold => koVolumeThreshold;

        private void OnEnable()
        {
            RegisteredBlades.Add(this);

            if (owner == null)
            {
                PlayerController playerController = GetComponentInParent<PlayerController>();
                if (playerController != null)
                    owner = playerController.gameObject;
            }

            if (bladeCollider == null)
                bladeCollider = GetComponent<Collider>();

            if (bladeCollider != null)
            {
                bladeCollider.isTrigger = true;
                bladeCollider.enabled = true;
            }

            SetSlicingEnabled(startEnabled);
        }

        private void OnDisable()
        {
            RegisteredBlades.Remove(this);
            CleanupAllBladeClashesForThisBlade();

            if (bladeCollider != null)
                bladeCollider.enabled = false;

            slicingEnabled = false;
            sliceConsumedThisWindow = false;
        }

        private void FixedUpdate()
        {
            ProbeBladeClashes();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryNotifyBladeClash(other);
            TrySliceFromContact(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryNotifyBladeClash(other);
            TrySliceFromContact(other);
        }

        private void OnTriggerExit(Collider other)
        {
            CleanupBladeClash(other);
        }

        public bool TrySlice(ISliceable target, Vector3 cutPoint, Vector3 cutNormal)
        {
            if (target == null)
                return false;

            Transform effectCamera = ResolveEffectCameraTransform(target);
            ScaleController preSliceVictimScale = target as ScaleController;
            bool shouldPlayKoAudio = preSliceVictimScale != null &&
                                     preSliceVictimScale.currentVolumeRatio < koVolumeThreshold;
            float preSliceKnockbackMultiplier = preSliceVictimScale != null
                ? preSliceVictimScale.KnockbackForceMultiplier
                : 1f;

            SpawnEffectPrefab(cheeseParticlePrefab, cutPoint, effectCamera);
            SpawnEffectPrefab(hitParticlePrefab, cutPoint, effectCamera);
            SpawnEffectPrefab(hitStarParticlePrefab, cutPoint, effectCamera);
            SpawnEffectPrefab(cutParticlePrefab, cutPoint, effectCamera);

            string targetName = target is Component targetComponent ? targetComponent.name : target.ToString();
            Debug.Log($"[KnifeBlade] Hit {targetName} at {cutPoint} with normal {cutNormal}", this);

            if (preSliceVictimScale != null)
                TrySpawnKoParticle(preSliceVictimScale, cutPoint, effectCamera);

            Vector3 attackDirection = owner != null ? owner.transform.forward : transform.forward;
            target.OnSliced(cutPoint, cutNormal, cuttingForce, attackDirection);
            sliceConsumedThisWindow = true;
            HitResolved?.Invoke(shouldPlayKoAudio);

            if (target is ScaleController victimScale)
            {
                PlayerController victimController = victimScale.GetComponent<PlayerController>();
                Animator victimAnimator = victimController != null ? victimController.GetAnimator() : null;
                if (victimController != null)
                    victimController.TriggerHitUI();
                if (HitstopManager.Instance != null)
                {
                    PlayerController ownerController = owner != null ? owner.GetComponent<PlayerController>() : null;
                    PlayerCameraController attackerCamera = ownerController != null ? ownerController.GetCameraController() : null;
                    HitstopManager.Instance.TriggerHitstop(victimAnimator, victimScale, preSliceKnockbackMultiplier, attackerCamera);
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[KnifeBlade] Sliced {target} at {cutPoint} with normal {cutNormal}", this);

            return true;
        }

        private void TrySpawnKoParticle(
            ScaleController victimScale,
            Vector3 cutPoint,
            Transform effectCamera)
        {
            if (victimScale == null)
            {
                Debug.LogWarning("[KnifeBlade] KO check skipped because victimScale is null.", this);
                return;
            }

            float victimCurrentScale = victimScale.currentScale;
            float victimCurrentVolumeRatio = victimScale.currentVolumeRatio;
            bool shouldPlayKo = victimCurrentVolumeRatio < koVolumeThreshold;

            Debug.Log(
                $"[KnifeBlade] KO check on {victimScale.name}: currentScale={victimCurrentScale:F3}, currentVolumeRatio={victimCurrentVolumeRatio:F3}, koVolumeThreshold={koVolumeThreshold:F3}, shouldPlayKo={shouldPlayKo}",
                this);

            if (!shouldPlayKo)
                return;

            if (koParticlePrefab == null)
            {
                Debug.LogWarning($"[KnifeBlade] KO should play on {victimScale.name}, but koParticlePrefab is null.", this);
                return;
            }

            SpawnEffectPrefab(koParticlePrefab, cutPoint, effectCamera);
        }

        private void SpawnEffectPrefab(GameObject effectPrefab, Vector3 position, Transform effectCamera)
        {
            if (effectPrefab == null)
                return;

            GameObject effectInstance = Instantiate(effectPrefab, position, Quaternion.identity);
            FaceCameraBillboard billboard = effectInstance.GetComponent<FaceCameraBillboard>();
            if (billboard == null)
                billboard = effectInstance.AddComponent<FaceCameraBillboard>();

            billboard.SetTargetCamera(effectCamera);
        }

        private Transform ResolveEffectCameraTransform(object target)
        {
            PlayerController ownerController = owner != null ? owner.GetComponent<PlayerController>() : null;
            Transform ownerCamera = ownerController != null ? ownerController.GetCameraTransform() : null;
            if (ownerCamera != null)
                return ownerCamera;

            if (target is Component targetComponent)
            {
                PlayerController targetController = targetComponent.GetComponent<PlayerController>();
                if (targetController == null)
                    targetController = targetComponent.GetComponentInParent<PlayerController>();

                Transform targetCamera = targetController != null ? targetController.GetCameraTransform() : null;
                if (targetCamera != null)
                    return targetCamera;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        private bool IsPartOfOwner(GameObject candidate)
        {
            if (owner == null || candidate == null)
                return false;

            return candidate == owner || candidate.transform.IsChildOf(owner.transform);
        }

        private bool IsPartOfBlade(GameObject candidate)
        {
            return candidate == gameObject || candidate.transform.IsChildOf(transform);
        }

        private Vector3 CalculateCutPoint(Collider target)
        {
            if (bladeCollider == null)
                return target.transform.position;

            Vector3 closestPointOnBlade = bladeCollider.ClosestPoint(target.transform.position);
            return target.ClosestPoint(closestPointOnBlade);
        }

        private Vector3 CalculateCutNormal()
        {
            Vector3 localNormal = sliceNormal.sqrMagnitude > 0.0001f ? sliceNormal.normalized : Vector3.up;
            return transform.TransformDirection(localNormal).normalized;
        }

        public void SetOwner(GameObject newOwner)
        {
            owner = newOwner;
        }

        public void EnableSlicing()
        {
            SetSlicingEnabled(true);
        }

        public void DisableSlicing()
        {
            SetSlicingEnabled(false);
        }

        public void SetSlicingEnabled(bool enabled)
        {
            slicingEnabled = enabled;
            sliceConsumedThisWindow = false;

            if (!enabled)
                CleanupAllBladeClashesForThisBlade();

            if (enableDebugLogs)
                Debug.Log($"[KnifeBlade] Slicing {(enabled ? "enabled" : "disabled")}", this);
        }

        private void TrySliceFromContact(Collider other)
        {
            if (!slicingEnabled || sliceConsumedThisWindow)
                return;

            if (IsBladeClashTarget(other))
                return;

            ISliceable sliceable = other.GetComponentInParent<ISliceable>();
            if (sliceable == null)
                return;

            if (sliceable is not Component targetComponent)
                return;

            if (IsPartOfOwner(targetComponent.gameObject) || IsPartOfBlade(other.gameObject))
                return;

            Vector3 cutPoint = CalculateCutPoint(other);
            Vector3 cutNormal = CalculateCutNormal();
            TrySlice(sliceable, cutPoint, cutNormal);
        }

        private void TryNotifyBladeClash(Collider other)
        {
            if (!slicingEnabled || !IsBladeClashTarget(other))
                return;

            KnifeBlade otherBlade = other.GetComponentInParent<KnifeBlade>();
            if (otherBlade == null)
                return;

            TryRegisterBladeClash(otherBlade);
        }

        private void ProbeBladeClashes()
        {
            if (!slicingEnabled || bladeCollider == null || !bladeCollider.enabled)
                return;

            HashSet<ulong> overlappingClashKeys = null;

            foreach (KnifeBlade otherBlade in RegisteredBlades)
            {
                if (!IsBladeClashCandidate(otherBlade))
                    continue;

                ulong clashKey = GetBladeClashKey(otherBlade);
                if (!AreBladeCollidersOverlapping(otherBlade))
                    continue;

                overlappingClashKeys ??= new HashSet<ulong>();
                overlappingClashKeys.Add(clashKey);

                TryRegisterBladeClash(otherBlade);
            }

            CleanupStaleBladeClashes(overlappingClashKeys);
        }

        private void CleanupBladeClash(Collider other)
        {
            if (other == null)
                return;

            KnifeBlade otherBlade = other.GetComponentInParent<KnifeBlade>();
            if (otherBlade == null)
                return;

            ActiveBladeClashes.Remove(GetBladeClashKey(otherBlade));
        }

        private void CleanupAllBladeClashesForThisBlade()
        {
            int thisId = GetInstanceID();
            List<ulong> keysToRemove = null;
            foreach (ulong clashKey in ActiveBladeClashes)
            {
                int lowId = (int)(clashKey >> 32);
                int highId = (int)(clashKey & 0xffffffff);
                if (lowId != thisId && highId != thisId)
                    continue;

                keysToRemove ??= new List<ulong>();
                keysToRemove.Add(clashKey);
            }

            if (keysToRemove == null)
                return;

            foreach (ulong clashKey in keysToRemove)
                ActiveBladeClashes.Remove(clashKey);
        }

        private bool IsBladeClashTarget(Collider other)
        {
            if (other == null)
                return false;

            KnifeBlade otherBlade = other.GetComponentInParent<KnifeBlade>();
            return IsBladeClashCandidate(otherBlade);
        }

        private bool IsBladeClashCandidate(KnifeBlade otherBlade)
        {
            if (otherBlade == null || otherBlade == this)
                return false;

            if (!slicingEnabled || !otherBlade.slicingEnabled)
                return false;

            if (bladeCollider == null || !bladeCollider.enabled)
                return false;

            if (otherBlade.bladeCollider == null || !otherBlade.bladeCollider.enabled)
                return false;

            if (IsPartOfOwner(otherBlade.gameObject) || otherBlade.IsPartOfOwner(gameObject))
                return false;

            return true;
        }

        private bool AreBladeCollidersOverlapping(KnifeBlade otherBlade)
        {
            return Physics.ComputePenetration(
                bladeCollider,
                bladeCollider.transform.position,
                bladeCollider.transform.rotation,
                otherBlade.bladeCollider,
                otherBlade.bladeCollider.transform.position,
                otherBlade.bladeCollider.transform.rotation,
                out _,
                out _);
        }

        private void TryRegisterBladeClash(KnifeBlade otherBlade)
        {
            if (otherBlade == null || !IsPrimaryBladeForClash(otherBlade))
                return;

            ulong clashKey = GetBladeClashKey(otherBlade);
            if (!ActiveBladeClashes.Add(clashKey))
                return;

            if (enableDebugLogs)
                Debug.Log($"[KnifeBlade] Blade clash detected between {name} and {otherBlade.name}", this);

            BladeClashed?.Invoke();
        }

        private bool IsPrimaryBladeForClash(KnifeBlade otherBlade)
        {
            if (otherBlade == null)
                return false;

            return GetInstanceID() < otherBlade.GetInstanceID();
        }

        private void CleanupStaleBladeClashes(HashSet<ulong> overlappingClashKeys)
        {
            int thisId = GetInstanceID();
            List<ulong> keysToRemove = null;
            foreach (ulong clashKey in ActiveBladeClashes)
            {
                int lowId = (int)(clashKey >> 32);
                int highId = (int)(clashKey & 0xffffffff);
                if (lowId != thisId && highId != thisId)
                    continue;

                if (overlappingClashKeys != null && overlappingClashKeys.Contains(clashKey))
                    continue;

                keysToRemove ??= new List<ulong>();
                keysToRemove.Add(clashKey);
            }

            if (keysToRemove == null)
                return;

            foreach (ulong clashKey in keysToRemove)
                ActiveBladeClashes.Remove(clashKey);
        }

        private ulong GetBladeClashKey(KnifeBlade otherBlade)
        {
            int thisId = GetInstanceID();
            int otherId = otherBlade != null ? otherBlade.GetInstanceID() : 0;
            uint lowId = (uint)Mathf.Min(thisId, otherId);
            uint highId = (uint)Mathf.Max(thisId, otherId);
            return ((ulong)lowId << 32) | highId;
        }

        private void OnDrawGizmosSelected()
        {
            Collider gizmoCollider = bladeCollider != null ? bladeCollider : GetComponent<Collider>();

            if (gizmoCollider != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(gizmoCollider.bounds.center, gizmoCollider.bounds.size);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, CalculateCutNormal() * 2f);
        }
    }
}
