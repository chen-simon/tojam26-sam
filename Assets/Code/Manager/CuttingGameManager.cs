using UnityEngine;
using ToJam26.Gameplay.Slicing;
using ToJam26.Gameplay.Player;
using ToJam26.Gameplay.Equipment;
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
        /// Orchestrates: mesh slicing -> scale recalculation -> knockback application
        /// </summary>
        private void HandlePlayerSliced(ScaleController targetPlayer, Vector3 cutPoint, Vector3 cutNormal, float cuttingForce)
        {
            if (debugMode)
                Debug.Log($"[CuttingGameManager] {targetPlayer.gameObject.name} sliced at {cutPoint} with force {cuttingForce}");

            // Step 1: Perform the actual mesh slicing
            if (meshSlicer != null)
            {
                meshSlicer.Cut(targetPlayer.gameObject, cutPoint, cutNormal);
            }

            // Step 2: Wait for mesh to be generated, then recalculate scale
            // Note: MeshSlicer generates mesh immediately in this implementation
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
