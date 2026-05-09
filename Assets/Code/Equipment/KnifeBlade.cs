using UnityEngine;
using UnityEngine.Serialization;
using ToJam26.Gameplay.Slicing;

namespace ToJam26.Gameplay.Equipment
{
    public class KnifeBlade : MonoBehaviour, IKnife
    {
        [Header("Knife Settings")]
        [SerializeField] private GameObject owner;
        [SerializeField] private float cuttingForce = 5f;
        [SerializeField] private Collider bladeCollider;
        [SerializeField] private bool startEnabled = false;

        [SerializeField] private GameObject hitParticlePrefab;
        [SerializeField] private GameObject cutParticlePrefab;

        [Header("Slicing Settings")]
        [SerializeField] private Vector3 sliceNormal = Vector3.up;
        [FormerlySerializedAs("debugMode")]
        [SerializeField] private bool enableDebugLogs = false;

        private bool slicingEnabled;
        private bool sliceConsumedThisWindow;

        public float CuttingForce => cuttingForce;
        public GameObject Owner => owner;
        public Collider BladeCollider => bladeCollider;

        private void OnEnable()
        {
            if (owner == null)
            {
                ToJam26.Gameplay.Player.PlayerController playerController =
                    GetComponentInParent<ToJam26.Gameplay.Player.PlayerController>();
                if (playerController != null)
                    owner = playerController.gameObject;
            }

            if (bladeCollider == null)
                bladeCollider = GetComponent<Collider>();

            if (bladeCollider != null)
                bladeCollider.isTrigger = true;

            SetSlicingEnabled(startEnabled);
        }

        private void OnDisable()
        {
            if (bladeCollider != null)
                bladeCollider.enabled = false;

            slicingEnabled = false;
            sliceConsumedThisWindow = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            TrySliceFromContact(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TrySliceFromContact(other);
        }

        public bool TrySlice(ISliceable target, Vector3 cutPoint, Vector3 cutNormal)
        {
            if (target == null)
                return false;
            
            Instantiate(hitParticlePrefab, cutPoint, Quaternion.identity);
            Instantiate(cutParticlePrefab, cutPoint, Quaternion.identity);

            string targetName = target is Component targetComponent ? targetComponent.name : target.ToString();
            Debug.Log($"[KnifeBlade] Hit {targetName} at {cutPoint} with normal {cutNormal}", this);

            Vector3 attackDirection = owner != null ? owner.transform.forward : transform.forward;
            target.OnSliced(cutPoint, cutNormal, cuttingForce, attackDirection);
            sliceConsumedThisWindow = true;

            if (enableDebugLogs)
                Debug.Log($"[KnifeBlade] Sliced {target} at {cutPoint} with normal {cutNormal}", this);

            return true;
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

            if (bladeCollider != null)
                bladeCollider.enabled = enabled;

            if (enableDebugLogs)
                Debug.Log($"[KnifeBlade] Slicing {(enabled ? "enabled" : "disabled")}", this);
        }

        private void TrySliceFromContact(Collider other)
        {
            if (!slicingEnabled || sliceConsumedThisWindow)
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
