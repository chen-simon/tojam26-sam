using UnityEngine;
using ToJam26.Gameplay.Slicing;
using com.marufhow.meshslicer.core;

namespace ToJam26.Gameplay.Equipment
{
    /// <summary>
    /// Handles knife blade detection and slicing logic.
    /// Attach this to a GameObject with a trigger collider on the knife blade.
    /// </summary>
    public class KnifeBlade : MonoBehaviour, IKnife
    {
        [Header("Knife Settings")]
        [SerializeField] private GameObject owner; // The player wielding this knife
        [SerializeField] private float cuttingForce = 5f;
        [SerializeField] private Collider bladeCollider;

        [Header("Slicing Settings")]
        [SerializeField] private MHCutter meshSlicer; // Reference to meshslicer component
        [SerializeField] private float sliceDelay = 0.1f; // Cooldown between slices on same object
        [SerializeField] private bool debugMode = false;

        // Track sliced targets to prevent multiple cuts in quick succession
        private System.Collections.Generic.Dictionary<ISliceable, float> lastSliceTime = new();

        public float CuttingForce => cuttingForce;
        public GameObject Owner => owner;
        public Collider BladeCollider => bladeCollider;

        private void OnEnable()
        {
            if (bladeCollider == null)
                bladeCollider = GetComponent<Collider>();

            if (bladeCollider != null)
                bladeCollider.isTrigger = true;

            if (meshSlicer == null)
                meshSlicer = GetComponentInParent<MHCutter>();
        }

        private void OnTriggerStay(Collider other)
        {
            // Check if the collided object has ISliceable
            if (other.TryGetComponent<ISliceable>(out var sliceable))
            {
                // Don't slice the owner
                if (other.gameObject == owner)
                    return;

                // Check if enough time has passed since last slice
                if (IsSliceable(sliceable))
                {
                    // Calculate cut point and normal
                    Vector3 cutPoint = CalculateCutPoint(other);
                    Vector3 cutNormal = CalculateCutNormal();

                    // Perform the slice
                    TrySlice(sliceable, cutPoint, cutNormal);
                }
            }
        }

        public bool TrySlice(ISliceable target, Vector3 cutPoint, Vector3 cutNormal)
        {
            if (target == null)
                return false;

            // Call OnSliced on the target
            target.OnSliced(cutPoint, cutNormal, cuttingForce);

            // Update last slice time
            lastSliceTime[target] = Time.time;

            if (debugMode)
            {
                Debug.Log($"[KnifeBlade] Sliced {target}", (Object)target);
            }

            return true;
        }

        /// <summary>
        /// Checks if the target can be sliced (cooldown check).
        /// </summary>
        private bool IsSliceable(ISliceable target)
        {
            if (!lastSliceTime.TryGetValue(target, out float lastTime))
                return true;

            return Time.time - lastTime >= sliceDelay;
        }

        /// <summary>
        /// Calculates where on the target object the cut occurs.
        /// Uses the closest point on the blade collider to the target collider.
        /// </summary>
        private Vector3 CalculateCutPoint(Collider target)
        {
            if (bladeCollider == null)
                return target.transform.position;

            // Get the closest point on the blade to the target
            Vector3 closestPointOnBlade = bladeCollider.ClosestPoint(target.transform.position);
            
            // Get the closest point on the target to the blade
            Vector3 cutPoint = target.ClosestPoint(closestPointOnBlade);

            return cutPoint;
        }

        /// <summary>
        /// Calculates the cutting plane normal (direction of the knife).
        /// This is typically the forward direction of the knife/blade.
        /// </summary>
        private Vector3 CalculateCutNormal()
        {
            // Use the blade's forward direction
            return transform.forward;
        }

        /// <summary>
        /// Sets the owner of this knife.
        /// </summary>
        public void SetOwner(GameObject newOwner)
        {
            owner = newOwner;
        }

        /// <summary>
        /// Clears the slice cooldown for all targets.
        /// Useful for resetting after certain game events.
        /// </summary>
        public void ClearSliceCooldowns()
        {
            lastSliceTime.Clear();
        }

        #region Debug Utilities

        private void OnDrawGizmosSelected()
        {
            if (!debugMode)
                return;

            // Draw the blade collider bounds
            if (bladeCollider != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(bladeCollider.bounds.center, bladeCollider.bounds.size);
            }

            // Draw the forward direction
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }

        #endregion
    }
}
