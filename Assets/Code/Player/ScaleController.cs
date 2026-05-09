using UnityEngine;
using UnityEngine.Serialization;
using ToJam26.Gameplay.Utility;
using ToJam26.Gameplay.Slicing;

namespace ToJam26.Gameplay.Player
{
    public class ScaleController : MonoBehaviour, ISliceable
    {
        [Header("Scale Settings")]
        [SerializeField] private float originalMass = 10f;
        [SerializeField] private float minScale = 0.3f;

        [Header("Movement Settings")]
        [SerializeField] private float baseMovementSpeed = 5f;
        [SerializeField] private AnimationCurve speedScaleCurve = AnimationCurve.Linear(0, 0.5f, 1, 1);

        [Header("Knockback Settings")]
        [SerializeField] private float baseKnockbackForce = 10f;
        [SerializeField] private AnimationCurve knockbackScaleCurve = AnimationCurve.Linear(0, 2f, 1, 0.5f);
        [FormerlySerializedAs("knockbackDuration")]
        [SerializeField] private float knockbackDeceleration = 25f;

        [Header("Mesh Slicing")]
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private bool useVolumeCalculation = true;

        private float currentMass;
        private float currentScale = 1f;
        private float originalVolume;
        private Mesh originalMesh;
        private Vector3 currentKnockbackVelocity = Vector3.zero;
        private bool isKnockedBack;

        public delegate void OnScaleChangedDelegate(float newScale, float newMass);
        public OnScaleChangedDelegate OnScaleChanged;

        public delegate void OnSlicedDelegate(Vector3 cutPoint, Vector3 cutNormal, float damage, Vector3 attackDirection);
        public OnSlicedDelegate OnPlayerSliced;

        public float CurrentMass => currentMass;
        public float CurrentScale => currentScale;
        public float MassRatio => currentMass / Mathf.Max(originalMass, 0.0001f);
        public float ScaleRatio => currentScale;
        public float MovementSpeedMultiplier => speedScaleCurve.Evaluate(MassRatio);
        public float KnockbackForceMultiplier => knockbackScaleCurve.Evaluate(MassRatio);
        public bool IsKnockedBack => isKnockedBack;
        public MeshFilter SliceMeshFilter => meshFilter;
        public GameObject SliceTargetObject => meshFilter != null ? meshFilter.gameObject : gameObject;

        private void OnEnable()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (meshFilter == null)
                meshFilter = GetComponentInChildren<MeshFilter>();

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                originalMesh = meshFilter.sharedMesh;
                originalVolume = MeshVolumeCalculator.CalculateVolume(originalMesh);
                if (originalVolume <= 0f)
                    originalVolume = 1f;
            }
            else
            {
                Debug.LogError($"[ScaleController] No MeshFilter found on {gameObject.name}", this);
                originalVolume = 1f;
            }

            currentMass = originalMass;
            currentScale = 1f;
            OnScaleChanged?.Invoke(currentScale, currentMass);
        }

        private void Update()
        {
            if (!isKnockedBack)
                return;

            float decelerationPerSecond = Mathf.Max(0f, knockbackDeceleration);
            currentKnockbackVelocity = Vector3.MoveTowards(
                currentKnockbackVelocity,
                Vector3.zero,
                decelerationPerSecond * Time.deltaTime);

            if (currentKnockbackVelocity.sqrMagnitude <= 0.0001f)
            {
                isKnockedBack = false;
                currentKnockbackVelocity = Vector3.zero;
            }
        }

        public void OnSliced(Vector3 cutPoint, Vector3 cutNormal, float cuttingForce, Vector3 attackDirection)
        {
            OnPlayerSliced?.Invoke(cutPoint, cutNormal, cuttingForce, attackDirection);
        }

        public void ApplyKnockback(Vector3 knockbackDirection, float knockbackMagnitude = 0f)
        {
            if (knockbackMagnitude <= 0f)
                knockbackMagnitude = baseKnockbackForce * KnockbackForceMultiplier;

            Vector3 planarDirection = Vector3.ProjectOnPlane(knockbackDirection, Vector3.up);
            if (planarDirection.sqrMagnitude < 0.0001f)
                planarDirection = -transform.forward;

            Vector3 knockbackVelocity = planarDirection.normalized * knockbackMagnitude;
            currentKnockbackVelocity = knockbackVelocity;
            isKnockedBack = true;
        }

        public Vector3 GetKnockbackVelocity()
        {
            return currentKnockbackVelocity;
        }

        public void RecalculateScale()
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return;

            if (useVolumeCalculation)
            {
                currentScale = MeshVolumeCalculator.CalculateScaleFromVolume(meshFilter.sharedMesh, originalVolume);
            }
            else
            {
                float currentBoundsSize = meshFilter.sharedMesh.bounds.size.magnitude;
                float originalBoundsSize = originalMesh != null ? originalMesh.bounds.size.magnitude : currentBoundsSize;
                currentScale = originalBoundsSize > 0f ? currentBoundsSize / originalBoundsSize : 1f;
            }

            currentScale = Mathf.Max(currentScale, minScale);
            currentMass = originalMass * (currentScale * currentScale * currentScale);
            OnScaleChanged?.Invoke(currentScale, currentMass);

            if (currentScale <= minScale)
                OnPlayerEliminated();
        }

        public void SetScale(float newScale)
        {
            currentScale = Mathf.Max(newScale, minScale);
            currentMass = originalMass * (currentScale * currentScale * currentScale);
            OnScaleChanged?.Invoke(currentScale, currentMass);

            if (currentScale <= minScale)
                OnPlayerEliminated();
        }

        public float GetCurrentMovementSpeed()
        {
            return baseMovementSpeed * MovementSpeedMultiplier;
        }

        public float GetKnockbackMultiplier()
        {
            return KnockbackForceMultiplier;
        }

        private void OnPlayerEliminated()
        {
            Debug.Log($"[ScaleController] Player {gameObject.name} has been eliminated!", this);
            gameObject.SetActive(false);
        }

        public void ResetToOriginal()
        {
            currentScale = 1f;
            currentMass = originalMass;
            OnScaleChanged?.Invoke(currentScale, currentMass);
            isKnockedBack = false;
            currentKnockbackVelocity = Vector3.zero;
        }
    }
}
