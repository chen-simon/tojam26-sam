using UnityEngine;

namespace ToJam26.Gameplay.Interface
{
    public class HudCountdownAnimationRelay : MonoBehaviour
    {
        [SerializeField] private HudController owner;

        public void Initialize(HudController hudController)
        {
            owner = hudController;
        }

        public void NotifyCountdownFinished()
        {
            ResolveOwner();
            owner?.NotifyCountdownFinished();
        }

        private void ResolveOwner()
        {
            if (owner == null)
                owner = GetComponentInParent<HudController>();
        }
    }
}
