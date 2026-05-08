using UnityEngine;

namespace ToJam26.Gameplay.Slicing
{
    /// <summary>
    /// Interface for knife/blade that can slice objects.
    /// </summary>
    public interface IKnife
    {
        /// <summary>
        /// Gets the current cutting force of the knife.
        /// </summary>
        float CuttingForce { get; }

        /// <summary>
        /// Gets the owner of this knife (the player wielding it).
        /// </summary>
        GameObject Owner { get; }

        /// <summary>
        /// Gets the knife blade's collider for cutting detection.
        /// </summary>
        Collider BladeCollider { get; }

        /// <summary>
        /// Called when the knife attempts to slice a target.
        /// Returns true if the slice was successful.
        /// </summary>
        bool TrySlice(ISliceable target, Vector3 cutPoint, Vector3 cutNormal);
    }
}
