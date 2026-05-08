using UnityEngine;
using ToJam26.Gameplay.Utility;
using ToJam26.Gameplay.Slicing;

namespace ToJam26.Gameplay.Player
{
    /// <summary>
    /// Controls the player's scale, mass, and gameplay mechanics related to being sliced.
    /// Handles mass-dependent movement speed and knockback force calculations.
    /// </summary>
    public class ScaleController : MonoBehaviour, ISliceable
    {
        [Header("Scale Settings")]
        [SerializeField] private float originalMass = 10f;
        [SerializeField] private float density = 1f; // Mass per unit volume
        [SerializeField] private float minScale = 0.3f; // Minimum scale before player is eliminated

        [Header("Movement Settings")]
        [SerializeField] private float baseMovementSpeed = 5f;
        [SerializeField] private AnimationCurve speedScaleCurve = AnimationCurve.Linear(0, 0.5f, 1, 1);

        [Header("Knockback Settings")]
        [SerializeField] private float baseKnockbackForce = 10f;
        [SerializeField] private AnimationCurve knockbackScaleCurve = AnimationCurve.Linear(0, 2f, 1, 0.5f);
        [SerializeField] private float knockbackDuration = 0.2f;

        [Header("Mesh Slicing")]
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private bool useVolumeCalculation = true; // If false, uses simple area-based scale

        // State
        private float currentMass;
        private float currentScale = 1f;
        private float originalVolume;
        private Rigidbody rigidBody;
        private Mesh originalMesh;

        // Knockback state
        private float knockbackTimer = 0f;
        private bool isKnockedBack = false;

        // Callbacks for other systems
        public delegate void OnScaleChangedDelegate(float newScale, float newMass);
        public OnScaleChangedDelegate OnScaleChanged;

        public delegate void OnSlicedDelegate(Vector3 cutPoint, Vector3 cutNormal, float damage);
        public OnSlicedDelegate OnPlayerSliced;

        // Public properties
        public float CurrentMass => currentMass;
        public float CurrentScale => currentScale;
        public float MassRatio => currentMass / originalMass;
        public float ScaleRatio => currentScale;
        public float MovementSpeedMultiplier => speedScaleCurve.Evaluate(MassRatio);
        public float KnockbackForceMultiplier => knockbackScaleCurve.Evaluate(MassRatio);
        public bool IsKnockedBack => isKnockedBack;

        private void OnEnable()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (rigidBody == null)
                rigidBody = GetComponent<Rigidbody>();

            // Store the original mesh and volume
            if (meshFilter != null && meshFilter.mesh != null)
            {
                originalMesh = meshFilter.mesh;
                originalVolume = MeshVolumeCalculator.CalculateVolume(originalMesh);

                if (originalVolume <= 0f)
                {
                    Debug.LogWarning($"[ScaleController] Mesh volume is 0 for {gameObject.name}. Using default mass.", this);
                    originalVolume = 1f;
                }
            }
            else
            {
                Debug.LogError($"[ScaleController] No MeshFilter found on {gameObject.name}", this);
                originalVolume = 1f;
            }

            currentMass = originalMass;
            currentScale = 1f;

            // Notify listeners of initial state
            OnScaleChanged?.Invoke(currentScale, currentMass);
        }

        private void Update()
        {
            if (isKnockedBack)
            {
                knockbackTimer -= Time.deltaTime;
                if (knockbackTimer <= 0f)
                {
                    isKnockedBack = false;
                }
            }
        }

        /// <summary>
        /// Implementation of ISliceable.OnSliced - called when this player is sliced.
        /// </summary>
        public void OnSliced(Vector3 cutPoint, Vector3 cutNormal, float cuttingForce)
        {
            // Notify listeners that we've been sliced
            OnPlayerSliced?.Invoke(cutPoint, cutNormal, cuttingForce);

            // Check if there's actual mesh slicing to do
            // This would typically be handled by the meshslicer library
            // The scale update would happen after the mesh is actually sliced
        }

        /// <summary>
        /// Apply knockback to this player. Should be called after mesh slicing.
        /// </summary>
        /// <param name="knockbackDirection">Direction of the knockback force</param>
        /// <param name="knockbackMagnitude">Optional custom knockback magnitude. If 0, uses calculated value.</param>
        public void ApplyKnockback(Vector3 knockbackDirection, float knockbackMagnitude = 0f)
        {
            if (rigidBody == null || rigidBody.isKinematic)
                return;

            if (knockbackMagnitude <= 0f)
            {
                knockbackMagnitude = baseKnockbackForce * KnockbackForceMultiplier;
            }

            Vector3 knockbackForce = knockbackDirection.normalized * knockbackMagnitude;
            rigidBody.velocity = Vector3.zero; // Reset velocity
            rigidBody.AddForce(knockbackForce, ForceMode.Impulse);

            // Set knockback state
            isKnockedBack = true;
            knockbackTimer = knockbackDuration;
        }

        /// <summary>
        /// Updates the player's scale and mass based on current mesh.
        /// Should be called after the mesh has been modified by the slicer.
        /// </summary>
        public void RecalculateScale()
        {
            if (meshFilter == null || meshFilter.mesh == null)
                return;

            if (useVolumeCalculation)
            {
                currentScale = MeshVolumeCalculator.CalculateScaleFromVolume(
                    meshFilter.mesh,
                    originalVolume
                );
            }
            else
            {
                // Simple scale based on mesh bounds
                float currentBoundsSize = meshFilter.mesh.bounds.size.magnitude;
                float originalBoundsSize = originalMesh.bounds.size.magnitude;
                currentScale = originalBoundsSize > 0 ? currentBoundsSize / originalBoundsSize : 1f;
            }

            currentScale = Mathf.Max(currentScale, minScale);
            currentMass = originalMass * MassRatio; // Scale mass accordingly

            // Invoke callback
            OnScaleChanged?.Invoke(currentScale, currentMass);

            // Check if player has been sliced too much
            if (currentScale <= minScale)
            {
                OnPlayerEliminated();
            }
        }

        /// <summary>
        /// Directly set the scale (useful for testing or special cases).
        /// </summary>
        public void SetScale(float newScale)
        {
            currentScale = Mathf.Max(newScale, minScale);
            currentMass = originalMass * (currentScale * currentScale * currentScale); // Volume scales as scale^3

            OnScaleChanged?.Invoke(currentScale, currentMass);

            if (currentScale <= minScale)
            {
                OnPlayerEliminated();
            }
        }

        /// <summary>
        /// Gets the current movement speed based on mass and base speed.
        /// </summary>
        public float GetCurrentMovementSpeed()
        {
            return baseMovementSpeed * MovementSpeedMultiplier;
        }

        /// <summary>
        /// Gets the current knockback force multiplier.
        /// Smaller players get knocked back harder.
        /// </summary>
        public float GetKnockbackMultiplier()
        {
            return KnockbackForceMultiplier;
        }

        /// <summary>
        /// Called when the player is eliminated (too small to continue).
        /// Override or subscribe to OnScaleChanged to handle this.
        /// </summary>
        private void OnPlayerEliminated()
        {
            Debug.Log($"[ScaleController] Player {gameObject.name} has been eliminated!", this);
            // Trigger game over logic - you may want to call a GameManager or similar
            gameObject.SetActive(false); // Temporarily disable - implement proper game over logic
        }

        /// <summary>
        /// Resets the player to original scale and mass.
        /// </summary>
        public void ResetToOriginal()
        {
            currentScale = 1f;
            currentMass = originalMass;
            OnScaleChanged?.Invoke(currentScale, currentMass);
            isKnockedBack = false;
            knockbackTimer = 0f;
        }

        #region Debug Utilities

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            // Draw current scale as a sphere
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, currentScale);

            // Draw mass info as text (requires custom gizmo label rendering)
            Debug.Log($"Scale: {currentScale:F2}, Mass: {currentMass:F2}, KnockbackMult: {KnockbackForceMultiplier:F2}");
        }

        #endregion
    }
}
