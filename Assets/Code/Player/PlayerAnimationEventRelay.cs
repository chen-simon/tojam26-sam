using UnityEngine;

namespace ToJam26.Gameplay.Player
{
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private PlayerController owner;

        public void Initialize(PlayerController controller)
        {
            owner = controller;
        }

        public void EnableAttackHitbox()
        {
            ResolveOwner();
            owner?.EnableAttackHitbox();
        }

        public void DisableAttackHitbox()
        {
            ResolveOwner();
            owner?.DisableAttackHitbox();
        }

        private void ResolveOwner()
        {
            if (owner == null)
                owner = GetComponentInParent<PlayerController>();
        }
    }
}
