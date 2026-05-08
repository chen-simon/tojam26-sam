using UnityEngine;

namespace ToJam26.Gameplay.Slicing
{
    /// <summary>
    /// Interface for objects that can be sliced by a knife.
    /// </summary>
    public interface ISliceable
    {
        /// <summary>
        /// Called when the object is sliced.
        /// </summary>
        /// <param name="cutPoint">The point in world space where the cut occurs</param>
        /// <param name="cutNormal">The normal vector of the cutting plane (direction of the knife)</param>
        /// <param name="cuttingForce">The force/magnitude of the cut (for damage or effect calculation)</param>
        void OnSliced(Vector3 cutPoint, Vector3 cutNormal, float cuttingForce);
    }
}
