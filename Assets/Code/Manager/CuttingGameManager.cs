using UnityEngine;
using ToJam26.Gameplay.Slicing;
using ToJam26.Gameplay.Player;
using ToJam26.Gameplay.Equipment;
using ToJam26.Gameplay.Utility;
using com.marufhow.meshslicer.core;

namespace ToJam26.Gameplay.Manager
{
    /// <summary>
    /// Orchestrates the cutting, slicing, and scale changes in the arena game.
    /// Bridges the KnifeBlade (cutting detection) with ScaleController (gameplay logic).
    /// </summary>
    public class CuttingGameManager : MonoBehaviour
    {
        [Header("Mesh Slicer")]
        [SerializeField] private MHCutter meshSlicer; // The meshslicer component that does actual mesh cutting

        [Header("Knockback Settings")]
        [SerializeField] private AnimationCurve knockbackDirectionCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private bool debugMode = false;

        private void Start()
        {
            // Find all players with ScaleController
            ScaleController[] playerScaleControllers = FindObjectsByType<ScaleController>(FindObjectsSortMode.None);

            foreach (var scaleController in playerScaleControllers)
            {
                // Subscribe to slicing events
                scaleController.OnPlayerSliced += (cutPoint, cutNormal, force) => 
                    HandlePlayerSliced(scaleController, cutPoint, cutNormal, force);
            }
        }

        /// <summary>
        /// Called when a player is sliced.
        /// Orchestrates: mesh slicing -> keep larger part -> scale recalculation -> knockback application
        /// </summary>
        private void HandlePlayerSliced(ScaleController targetPlayer, Vector3 cutPoint, Vector3 cutNormal, float cuttingForce)
        {
            if (debugMode)
                Debug.Log($"[CuttingGameManager] {targetPlayer.gameObject.name} sliced at {cutPoint} with force {cuttingForce}");

            // Step 1: Perform the actual mesh slicing
            if (meshSlicer != null)
            {
                meshSlicer.Cut(targetPlayer.gameObject, cutPoint, cutNormal);
                // Step 2: Find and compare the two pieces, keep larger one
                KeepLargerPiece(targetPlayer, cutNormal, cuttingForce);
            }

            // Step 3: Recalculate scale and apply knockback
            PerformPostSliceOperations(targetPlayer, cutPoint, cutNormal, cuttingForce);
        }

        /// <summary>
        /// Performs operations after mesh slicing is complete.
        /// </summary>
        private void PerformPostSliceOperations(ScaleController targetPlayer, Vector3 cutPoint, Vector3 cutNormal, float cuttingForce)
        {
            // Recalculate the player's scale based on new mesh
            targetPlayer.RecalculateScale();

            // Apply knockback
            Vector3 knockbackDirection = cutNormal;
            float knockbackMagnitude = cuttingForce * targetPlayer.GetKnockbackMultiplier();

            targetPlayer.ApplyKnockback(knockbackDirection, knockbackMagnitude);

            if (debugMode)
            {
                Debug.Log($"[CuttingGameManager] Applied knockback: {knockbackDirection} * {knockbackMagnitude}");
                Debug.Log($"[CuttingGameManager] New scale: {targetPlayer.CurrentScale:F2}, New mass: {targetPlayer.CurrentMass:F2}");
            }
        }

        /// <summary>
        /// Keeps the larger volume piece as the player body and turns the smaller one into debris.
        /// </summary>
        private void KeepLargerPiece(ScaleController targetPlayer, Vector3 cutNormal, float cuttingForce)
        {
            GameObject originalPiece = targetPlayer.gameObject;
            MeshFilter originalMeshFilter = originalPiece.GetComponent<MeshFilter>();

            if (originalMeshFilter == null || originalMeshFilter.mesh == null)
                return;

            GameObject slicedPiece = FindSlicedPiece(originalPiece);

            if (slicedPiece == null)
                return;

            MeshFilter slicedMeshFilter = slicedPiece.GetComponent<MeshFilter>();
            if (slicedMeshFilter == null || slicedMeshFilter.mesh == null)
                return;

            // Calculate volumes
            float originalVolume = MeshVolumeCalculator.CalculateVolume(originalMeshFilter.mesh);
            float slicedVolume = MeshVolumeCalculator.CalculateVolume(slicedMeshFilter.mesh);

            if (debugMode)
                Debug.Log($"[CuttingGameManager] Original volume: {originalVolume:F2}, Sliced volume: {slicedVolume:F2}");

            if (slicedVolume > originalVolume)
            {
                if (debugMode)
                    Debug.Log($"[CuttingGameManager] Keeping sliced piece (larger)");

                MeshRenderer originalRenderer = originalPiece.GetComponent<MeshRenderer>();
                GameObject detachedPiece = CreateDetachedPiece(
                    originalPiece,
                    originalMeshFilter.mesh,
                    originalRenderer != null ? originalRenderer.materials : null);

                PrepareDetachedPiece(detachedPiece, originalPiece, cutNormal, cuttingForce);

                targetPlayer.transform.SetPositionAndRotation(slicedPiece.transform.position, slicedPiece.transform.rotation);
                targetPlayer.transform.localScale = slicedPiece.transform.localScale;

                Mesh slicedMesh = slicedMeshFilter.mesh;
                originalMeshFilter.mesh = slicedMesh;

                MeshCollider originalCollider = originalPiece.GetComponent<MeshCollider>();
                if (originalCollider != null)
                    originalCollider.sharedMesh = slicedMesh;

                MeshRenderer slicedRenderer = slicedPiece.GetComponent<MeshRenderer>();
                if (originalRenderer != null && slicedRenderer != null)
                    originalRenderer.materials = slicedRenderer.materials;

                UnityEngine.Object.Destroy(slicedPiece);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[CuttingGameManager] Keeping original piece (larger)");

                PrepareDetachedPiece(slicedPiece, originalPiece, cutNormal, cuttingForce);
            }
        }

        private GameObject FindSlicedPiece(GameObject originalPiece)
        {
            if (meshSlicer != null && meshSlicer.LastSlicedObject != null && meshSlicer.LastSlicedObject != originalPiece)
                return meshSlicer.LastSlicedObject;

            Transform parentTransform = originalPiece.transform.parent;
            if (parentTransform == null)
                return null;

            for (int i = 0; i < parentTransform.childCount; i++)
            {
                Transform child = parentTransform.GetChild(i);
                if (child.gameObject != originalPiece && child.gameObject.name.StartsWith("Sliced"))
                    return child.gameObject;
            }

            return null;
        }

        private static GameObject CreateDetachedPiece(GameObject sourcePiece, Mesh sourceMesh, Material[] sourceMaterials)
        {
            if (sourcePiece == null || sourceMesh == null)
                return null;

            GameObject detachedPiece = new GameObject($"Detached {sourcePiece.name}");
            detachedPiece.layer = sourcePiece.layer;

            Transform detachedTransform = detachedPiece.transform;
            detachedTransform.SetParent(sourcePiece.transform.parent, false);
            detachedTransform.position = sourcePiece.transform.position;
            detachedTransform.rotation = sourcePiece.transform.rotation;
            detachedTransform.localScale = sourcePiece.transform.localScale;

            MeshFilter meshFilter = detachedPiece.AddComponent<MeshFilter>();
            meshFilter.mesh = UnityEngine.Object.Instantiate(sourceMesh);

            MeshRenderer meshRenderer = detachedPiece.AddComponent<MeshRenderer>();
            if (sourceMaterials != null && sourceMaterials.Length > 0)
                meshRenderer.materials = sourceMaterials;

            MeshCollider meshCollider = detachedPiece.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.mesh;
            meshCollider.convex = true;

            return detachedPiece;
        }

        private static void PrepareDetachedPiece(GameObject detachedPiece, GameObject sourcePiece, Vector3 cutNormal, float cuttingForce)
        {
            if (detachedPiece == null)
                return;

            if (detachedPiece.TryGetComponent<MeshCollider>(out MeshCollider meshCollider))
                meshCollider.convex = true;

            Rigidbody detachedBody = detachedPiece.GetComponent<Rigidbody>();
            if (detachedBody == null)
                detachedBody = detachedPiece.AddComponent<Rigidbody>();

            Rigidbody sourceBody = sourcePiece != null ? sourcePiece.GetComponent<Rigidbody>() : null;

            detachedBody.isKinematic = false;
            detachedBody.useGravity = true;
            detachedBody.constraints = RigidbodyConstraints.None;

            if (sourceBody != null)
            {
                detachedBody.mass = Mathf.Max(0.05f, sourceBody.mass * 0.35f);
                detachedBody.interpolation = sourceBody.interpolation;
                detachedBody.collisionDetectionMode = sourceBody.collisionDetectionMode;
                detachedBody.linearVelocity = sourceBody.linearVelocity;
                detachedBody.angularVelocity = sourceBody.angularVelocity;
            }
            else if (detachedBody.mass <= 0f)
            {
                detachedBody.mass = 1f;
            }

            Vector3 pushDirection = cutNormal.sqrMagnitude > 0f ? cutNormal.normalized : Vector3.up;
            Vector3 impulse = pushDirection * Mathf.Max(0.5f, cuttingForce * 0.25f) + Vector3.up * 0.25f;
            detachedBody.AddForce(impulse, ForceMode.Impulse);
        }

        /// <summary>
        /// Example method showing how to integrate with player controller.
        /// This should be called from your PlayerController to get movement parameters.
        /// </summary>
        public float GetPlayerMovementSpeed(ScaleController player)
        {
            return player.GetCurrentMovementSpeed();
        }

        /// <summary>
        /// Gets the knockback multiplier for a player (smaller = harder knockback).
        /// </summary>
        public float GetKnockbackMultiplier(ScaleController player)
        {
            return player.GetKnockbackMultiplier();
        }

        /// <summary>
        /// Example reset method for testing.
        /// </summary>
        public void ResetAllPlayers()
        {
            ScaleController[] playerScaleControllers = FindObjectsByType<ScaleController>(FindObjectsSortMode.None);
            foreach (var player in playerScaleControllers)
            {
                player.ResetToOriginal();
            }
        }
    }
}
