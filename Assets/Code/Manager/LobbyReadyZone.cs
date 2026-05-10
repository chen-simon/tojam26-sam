using System.Collections.Generic;
using UnityEngine;
using ToJam26.Gameplay.Player;

namespace ToJam26.Gameplay.Manager
{
    [RequireComponent(typeof(Collider))]
    public class LobbyReadyZone : MonoBehaviour
    {
        [SerializeField] private Renderer zoneRenderer;
        [SerializeField] private Material readyMaterial;
        [SerializeField] private Material unreadyMaterial;

        private readonly Dictionary<ScaleController, int> occupants = new();
        private Collider zoneCollider;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (occupants.Count == 0)
                return;

            bool changed = false;
            List<ScaleController> outside = null;
            foreach (KeyValuePair<ScaleController, int> occupant in occupants)
            {
                if (occupant.Key == null || !occupant.Key.gameObject.activeInHierarchy)
                {
                    outside ??= new List<ScaleController>();
                    outside.Add(occupant.Key);
                    changed = true;
                    continue;
                }

                Vector3 pos = occupant.Key.transform.position;
                Vector3 closest = zoneCollider.ClosestPoint(pos);
                if ((closest - pos).sqrMagnitude > 0.01f)
                {
                    outside ??= new List<ScaleController>();
                    outside.Add(occupant.Key);
                    changed = true;
                }
            }

            if (outside != null)
            {
                foreach (ScaleController p in outside)
                    occupants.Remove(p);
            }

            if (changed)
                UpdateColor();
        }

        public int OccupantCount
        {
            get
            {
                CleanupOccupants();
                int count = 0;
                foreach (KeyValuePair<ScaleController, int> occupant in occupants)
                {
                    if (occupant.Value > 0)
                        count++;
                }

                return count;
            }
        }

        public ScaleController OccupyingPlayer
        {
            get
            {
                CleanupOccupants();
                if (OccupantCount != 1)
                    return null;

                foreach (KeyValuePair<ScaleController, int> occupant in occupants)
                {
                    if (occupant.Key != null && occupant.Value > 0)
                        return occupant.Key;
                }

                return null;
            }
        }

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            ScaleController player = other.GetComponentInParent<ScaleController>();
            if (player == null)
                return;

            occupants.TryGetValue(player, out int contactCount);
            occupants[player] = contactCount + 1;
            UpdateColor();
        }

        private void OnTriggerExit(Collider other)
        {
            ScaleController player = other.GetComponentInParent<ScaleController>();
            if (player == null || !occupants.TryGetValue(player, out int contactCount))
                return;

            if (contactCount <= 1)
            {
                occupants.Remove(player);
                UpdateColor();
                return;
            }

            occupants[player] = contactCount - 1;
            UpdateColor();
        }

        private void UpdateColor()
        {
            if (zoneRenderer == null)
                return;
            zoneRenderer.material = OccupantCount > 0 ? readyMaterial : unreadyMaterial;
        }

        public void ClearOccupants()
        {
            occupants.Clear();
        }

        private void CleanupOccupants()
        {
            if (occupants.Count == 0)
                return;

            List<ScaleController> stalePlayers = null;
            foreach (KeyValuePair<ScaleController, int> occupant in occupants)
            {
                if (occupant.Key != null && occupant.Key.gameObject.activeInHierarchy && occupant.Value > 0)
                    continue;

                stalePlayers ??= new List<ScaleController>();
                stalePlayers.Add(occupant.Key);
            }

            if (stalePlayers == null)
                return;

            foreach (ScaleController stalePlayer in stalePlayers)
                occupants.Remove(stalePlayer);
        }
    }
}
