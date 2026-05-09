using UnityEngine;
using ToJam26.Gameplay.Player;
using ToJam26.Gameplay.Utility;
using ToJam26.Gameplay.Slicing.External;

namespace ToJam26.Gameplay.Manager
{
    public class CuttingGameManager : MonoBehaviour
    {
        [Header("Slice Settings")]
        [SerializeField] private bool solidSlices = true;
        [SerializeField] private bool reverseWireTriangles = false;
        [SerializeField] private bool shareVertices = false;
        [SerializeField] private bool smoothVertices = false;
        [SerializeField] private Material insideSliceMaterial;

        [Header("Debris Settings")]
        [SerializeField] private float debrisMassRatio = 0.35f;
        [SerializeField] private float minimumDebrisMass = 0.05f;
        [SerializeField] private float debrisImpulseFactor = 0.25f;
        [SerializeField] private float minimumDebrisImpulse = 0.5f;
        [SerializeField] private float upwardDebrisImpulse = 0.25f;
        [SerializeField] private float debrisLaunchSpeed = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private void Start()
        {
            ScaleController[] playerScaleControllers = FindObjectsByType<ScaleController>(FindObjectsSortMode.None);
            foreach (ScaleController scaleController in playerScaleControllers)
            {
                scaleController.OnPlayerSliced += (cutPoint, cutNormal, force) =>
                    TrySlicePlayer(scaleController, cutPoint, cutNormal, force);
            }
        }

        public bool TrySlicePlayer(ScaleController targetPlayer, Vector3 cutPoint, Vector3 cutNormal, float cuttingForce)
        {
            if (targetPlayer == null)
                return false;

            GameObject sliceTarget = targetPlayer.SliceTargetObject;
            MeshFilter sourceMeshFilter = targetPlayer.SliceMeshFilter;
            if (sliceTarget == null || sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            {
                if (debugMode)
                    Debug.LogWarning($"[CuttingGameManager] {targetPlayer.name} has no valid slice target.", targetPlayer);
                return false;
            }

            if (!sourceMeshFilter.sharedMesh.isReadable)
            {
                if (debugMode)
                    Debug.LogWarning($"[CuttingGameManager] Mesh '{sourceMeshFilter.sharedMesh.name}' is not readable.", sliceTarget);
                return false;
            }

            EnsureSliceSettings(sliceTarget);

            Plane slicePlane = new Plane(
                sliceTarget.transform.InverseTransformDirection(-cutNormal),
                sliceTarget.transform.InverseTransformPoint(cutPoint));

            GameObject[] sliceResults;
            try
            {
                sliceResults = Slicer.Slice(slicePlane, sliceTarget, insideSliceMaterial);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CuttingGameManager] Slice failed on {sliceTarget.name}: {ex.Message}", sliceTarget);
                return false;
            }

            if (sliceResults == null || sliceResults.Length != 2)
                return false;

            MeshFilter positiveMeshFilter = sliceResults[0].GetComponent<MeshFilter>();
            MeshFilter negativeMeshFilter = sliceResults[1].GetComponent<MeshFilter>();
            if (positiveMeshFilter == null || negativeMeshFilter == null ||
                positiveMeshFilter.sharedMesh == null || negativeMeshFilter.sharedMesh == null)
            {
                DestroyTemporarySlices(sliceResults);
                return false;
            }

            float positiveVolume = MeshVolumeCalculator.CalculateVolume(positiveMeshFilter.sharedMesh);
            float negativeVolume = MeshVolumeCalculator.CalculateVolume(negativeMeshFilter.sharedMesh);

            if (debugMode)
                Debug.Log($"[CuttingGameManager] {sliceTarget.name} volumes: positive={positiveVolume:F3}, negative={negativeVolume:F3}", sliceTarget);

            GameObject keptSlice = positiveVolume >= negativeVolume ? sliceResults[0] : sliceResults[1];
            GameObject debrisSlice = keptSlice == sliceResults[0] ? sliceResults[1] : sliceResults[0];

            ApplySliceToSource(sliceTarget, keptSlice);
            PrepareDetachedPiece(debrisSlice, sliceTarget, cutNormal, cuttingForce);
            Destroy(keptSlice);

            targetPlayer.RecalculateScale();
            ApplyPlayerKnockback(targetPlayer, cutPoint, cutNormal, cuttingForce);
            return true;
        }

        private void ApplySliceToSource(GameObject sourceObject, GameObject keptSlice)
        {
            MeshFilter sourceMeshFilter = sourceObject.GetComponent<MeshFilter>();
            MeshFilter keptMeshFilter = keptSlice.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = sourceObject.GetComponent<MeshRenderer>();
            MeshRenderer keptRenderer = keptSlice.GetComponent<MeshRenderer>();

            sourceMeshFilter.mesh = Instantiate(keptMeshFilter.sharedMesh);

            MeshCollider sourceCollider = sourceObject.GetComponent<MeshCollider>();
            if (sourceCollider == null)
                sourceCollider = sourceObject.AddComponent<MeshCollider>();
            sourceCollider.sharedMesh = sourceMeshFilter.sharedMesh;
            sourceCollider.convex = true;

            if (sourceRenderer != null && keptRenderer != null)
                sourceRenderer.materials = keptRenderer.materials;
        }

        private void PrepareDetachedPiece(GameObject detachedPiece, GameObject sourcePiece, Vector3 cutNormal, float cuttingForce)
        {
            if (detachedPiece == null)
                return;

            detachedPiece.name = $"Detached {sourcePiece.name}";
            detachedPiece.transform.SetParent(null, true);

            MeshCollider meshCollider = detachedPiece.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = detachedPiece.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = detachedPiece.GetComponent<MeshFilter>().sharedMesh;
            meshCollider.convex = true;

            Rigidbody detachedBody = detachedPiece.GetComponent<Rigidbody>();
            if (detachedBody == null)
                detachedBody = detachedPiece.AddComponent<Rigidbody>();

            detachedBody.isKinematic = false;
            detachedBody.useGravity = true;
            detachedBody.constraints = RigidbodyConstraints.None;
            detachedBody.mass = Mathf.Max(minimumDebrisMass, debrisMassRatio);

            Vector3 pushDirection = cutNormal.sqrMagnitude > 0f ? cutNormal.normalized : Vector3.up;
            detachedBody.linearVelocity = pushDirection * debrisLaunchSpeed;
            Vector3 impulse = pushDirection * Mathf.Max(minimumDebrisImpulse, cuttingForce * debrisImpulseFactor)
                              + Vector3.up * upwardDebrisImpulse;
            detachedBody.AddForce(impulse, ForceMode.Impulse);
        }

        private void ApplyPlayerKnockback(ScaleController targetPlayer, Vector3 cutPoint, Vector3 cutNormal, float cuttingForce)
        {
            Vector3 knockbackDirection = Vector3.ProjectOnPlane(targetPlayer.transform.position - cutPoint, Vector3.up);
            if (knockbackDirection.sqrMagnitude < 0.0001f)
                knockbackDirection = Vector3.ProjectOnPlane(-cutNormal, Vector3.up);
            if (knockbackDirection.sqrMagnitude < 0.0001f)
                knockbackDirection = -targetPlayer.transform.forward;

            float knockbackMagnitude = cuttingForce * targetPlayer.GetKnockbackMultiplier();
            targetPlayer.ApplyKnockback(knockbackDirection, knockbackMagnitude);

            if (debugMode)
            {
                Debug.Log($"[CuttingGameManager] Applied knockback: {knockbackDirection} * {knockbackMagnitude}", targetPlayer);
                Debug.Log($"[CuttingGameManager] New scale: {targetPlayer.CurrentScale:F2}, New mass: {targetPlayer.CurrentMass:F2}", targetPlayer);
            }
        }

        private void EnsureSliceSettings(GameObject sliceTarget)
        {
            Sliceable sliceable = sliceTarget.GetComponent<Sliceable>();
            if (sliceable == null)
                sliceable = sliceTarget.AddComponent<Sliceable>();

            sliceable.IsSolid = solidSlices;
            sliceable.ReverseWireTriangles = reverseWireTriangles;
            sliceable.ShareVertices = shareVertices;
            sliceable.SmoothVertices = smoothVertices;
            sliceable.UseGravity = true;
        }

        private static void DestroyTemporarySlices(GameObject[] sliceResults)
        {
            if (sliceResults == null)
                return;

            foreach (GameObject slice in sliceResults)
            {
                if (slice != null)
                    Destroy(slice);
            }
        }

        public float GetPlayerMovementSpeed(ScaleController player)
        {
            return player.GetCurrentMovementSpeed();
        }

        public float GetKnockbackMultiplier(ScaleController player)
        {
            return player.GetKnockbackMultiplier();
        }

        public void ResetAllPlayers()
        {
            ScaleController[] playerScaleControllers = FindObjectsByType<ScaleController>(FindObjectsSortMode.None);
            foreach (ScaleController player in playerScaleControllers)
                player.ResetToOriginal();
        }
    }
}
