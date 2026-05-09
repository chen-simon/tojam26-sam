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
        private Material[] originalMaterials;
        private Vector3 currentKnockbackVelocity = Vector3.zero;
        private bool isKnockedBack;
        private bool isKnockbackPaused;
        private bool isEliminated;
        private bool isInitialized;

        public delegate void OnScaleChangedDelegate(float newScale, float newMass);
        public OnScaleChangedDelegate OnScaleChanged;

        public delegate void OnSlicedDelegate(Vector3 cutPoint, Vector3 cutNormal, float damage, Vector3 attackDirection);
        public OnSlicedDelegate OnPlayerSliced;

        public delegate void OnPlayerEliminatedDelegate(ScaleController player);
        public OnPlayerEliminatedDelegate OnEliminated;

        public float CurrentMass => currentMass;
        public float CurrentScale => currentScale;
        public float MassRatio => currentMass / Mathf.Max(originalMass, 0.0001f);
        public float ScaleRatio => currentScale;
        public float MovementSpeedMultiplier => speedScaleCurve.Evaluate(MassRatio);
        public float KnockbackForceMultiplier => knockbackScaleCurve.Evaluate(MassRatio);
        public bool IsKnockedBack => isKnockedBack && !isKnockbackPaused;
        public bool IsEliminated => isEliminated;
        public MeshFilter SliceMeshFilter => meshFilter;
        public GameObject SliceTargetObject => meshFilter != null ? meshFilter.gameObject : gameObject;

        private void OnEnable()
        {
            if (!isInitialized)
                Initialize();
        }

        public void Initialize()
        {
            if (isInitialized)
                return;

            if (meshFilter == null)
                meshFilter = GetComponentInChildren<MeshFilter>();

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                originalMesh = meshFilter.sharedMesh;
                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                    originalMaterials = meshRenderer.sharedMaterials;

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
            isEliminated = false;
            isInitialized = true;
            OnScaleChanged?.Invoke(currentScale, currentMass);
        }

        public void PauseKnockback() => isKnockbackPaused = true;
        public void ResumeKnockback() => isKnockbackPaused = false;

        private void Update()
        {
            if (!isKnockedBack || isKnockbackPaused)
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

        public float GetCurrentVolume()
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return 0f;

            return MeshVolumeCalculator.CalculateVolume(meshFilter.sharedMesh);
        }

        public float GetCurrentMovementSpeed()
        {
            return baseMovementSpeed * MovementSpeedMultiplier;
        }

        public float GetKnockbackMultiplier()
        {
            return KnockbackForceMultiplier;
        }

        public void Eliminate()
        {
            OnPlayerEliminated();
        }

        private void OnPlayerEliminated()
        {
            if (isEliminated)
                return;

            isEliminated = true;
            isKnockedBack = false;
            isKnockbackPaused = false;
            currentKnockbackVelocity = Vector3.zero;
            Debug.Log($"[ScaleController] Player {gameObject.name} has been eliminated!", this);
            OnEliminated?.Invoke(this);
            gameObject.SetActive(false);
        }

        public void ResetToOriginal()
        {
            if (!isInitialized)
                Initialize();

            RestoreOriginalMesh();
            RestoreOriginalMaterials();

            currentScale = 1f;
            currentMass = originalMass;
            isKnockedBack = false;
            isKnockbackPaused = false;
            currentKnockbackVelocity = Vector3.zero;
            isEliminated = false;

            OnScaleChanged?.Invoke(currentScale, currentMass);
        }

        private void RestoreOriginalMesh()
        {
            if (meshFilter == null || originalMesh == null)
                return;

            Mesh currentMesh = meshFilter.sharedMesh;
            if (currentMesh != null && currentMesh != originalMesh)
                Destroy(currentMesh);

            meshFilter.mesh = Instantiate(originalMesh);

            MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = true;
        }

        private void RestoreOriginalMaterials()
        {
            if (meshFilter == null || originalMaterials == null || originalMaterials.Length == 0)
                return;

            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.materials = originalMaterials;
        }
    }
}
