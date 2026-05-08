using UnityEngine;

namespace ToJam26.Gameplay.Utility
{
    /// <summary>
    /// Utility for calculating mesh volume and scale changes based on mesh modifications.
    /// </summary>
    public static class MeshVolumeCalculator
    {
        /// <summary>
        /// Calculates the volume of a mesh.
        /// Uses the divergence theorem: V = (1/6) * sum of (vertex · triangle normal) for each triangle.
        /// </summary>
        /// <param name="mesh">The mesh to calculate volume for</param>
        /// <returns>The volume of the mesh</returns>
        public static float CalculateVolume(Mesh mesh)
        {
            if (mesh == null)
                return 0f;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            float volume = 0f;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];

                // Calculate the signed volume contribution of this triangle
                // Using the scalar triple product: (v0 × v1) · v2
                volume += Vector3.Dot(Vector3.Cross(v0, v1), v2);
            }

            // Divide by 6 to get actual volume (from divergence theorem)
            volume = Mathf.Abs(volume) / 6f;

            return volume;
        }

        /// <summary>
        /// Calculates the scale factor based on the ratio of current volume to original volume.
        /// Assumes uniform scaling: scale = cbrt(volumeRatio)
        /// </summary>
        /// <param name="currentMesh">The current (possibly sliced) mesh</param>
        /// <param name="originalVolume">The original volume before any slicing</param>
        /// <returns>The new uniform scale factor</returns>
        public static float CalculateScaleFromVolume(Mesh currentMesh, float originalVolume)
        {
            if (originalVolume <= 0f)
                return 1f;

            float currentVolume = CalculateVolume(currentMesh);
            float volumeRatio = currentVolume / originalVolume;

            // For uniform scaling: new_scale = original_scale * cbrt(volume_ratio)
            // If original_scale is 1, then new_scale = cbrt(volume_ratio)
            float newScale = Mathf.Pow(volumeRatio, 1f / 3f);

            return newScale;
        }

        /// <summary>
        /// Calculates the scale factor based on mass ratio.
        /// Useful when you have mass values instead of volumes.
        /// </summary>
        /// <param name="currentMass">The current mass after cuts</param>
        /// <param name="originalMass">The original mass before cuts</param>
        /// <returns>The new uniform scale factor</returns>
        public static float CalculateScaleFromMass(float currentMass, float originalMass)
        {
            if (originalMass <= 0f)
                return 1f;

            float massRatio = currentMass / originalMass;
            // Since volume is proportional to mass, scale = cbrt(mass_ratio)
            return Mathf.Pow(massRatio, 1f / 3f);
        }

        /// <summary>
        /// Calculates the mass based on volume and density.
        /// </summary>
        /// <param name="volume">The volume of the object</param>
        /// <param name="density">The density of the material</param>
        /// <returns>The mass</returns>
        public static float CalculateMass(float volume, float density)
        {
            return volume * density;
        }
    }
}
