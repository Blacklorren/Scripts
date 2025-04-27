using UnityEngine;
using HandballManager.Core;
using HandballManager.Data;
using HandballManager.Simulation.Events;
using System.Collections.Generic;
using HandballManager.Simulation.Engines;
using System;

namespace HandballManager.Simulation.Physics
{
    /// <summary>
    /// Handles stamina management for players.
    /// </summary>
    public class StaminaManager
    {
        // Stamina Constants
        private const float STAMINA_DRAIN_BASE = MatchSimulator.BASE_STAMINA_DRAIN_PER_SECOND;
        private const float STAMINA_SPRINT_MULTIPLIER = MatchSimulator.SPRINT_STAMINA_MULTIPLIER;
        private const float STAMINA_RECOVERY_RATE = 0.003f;
        private const float NATURAL_FITNESS_RECOVERY_MOD = 0.2f; // +/- 20% effect on recovery rate based on 0-100 NF (0 = 0.8x, 100 = 1.2x)
        private const float STAMINA_ATTRIBUTE_DRAIN_MOD = 0.3f; // +/- 30% effect on drain rate based on 0-100 Stamina (0=1.3x, 100=0.7x)
        private const float SPRINT_MIN_EFFORT_THRESHOLD = 0.85f; // % of BASE max speed considered sprinting
        private const float SIGNIFICANT_MOVEMENT_EFFORT_THRESHOLD = 0.2f; // % of BASE max speed considered 'moving' for stamina drain

        /// <summary>
        /// Updates player stamina based on their current activity level.
        /// </summary>
        /// <param name="state">The current match state containing player data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when state is null.</exception>
        public void UpdateStamina(MatchState state, float timeStep)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state), "Match state cannot be null when updating stamina.");
            }

            if (state.PlayersOnCourt == null) return;

            foreach (var player in state.PlayersOnCourt)
            {
                if (player == null) continue;
                ApplyStaminaEffects(player, timeStep);
            }
        }

        public void ApplyStaminaEffects(SimPlayer player, float deltaTime)
        {
            if (player == null || player.BaseData == null) return;

            float currentEffort = player.EffectiveSpeed > 0.01f 
                ? player.Velocity.magnitude / player.EffectiveSpeed
                : 0f;
            bool isMovingSignificantly = currentEffort > SIGNIFICANT_MOVEMENT_EFFORT_THRESHOLD;
            bool isSprinting = currentEffort > SPRINT_MIN_EFFORT_THRESHOLD;

            float staminaDrain = 0f;
            if (isMovingSignificantly)
            {
                staminaDrain = STAMINA_DRAIN_BASE * deltaTime;
                if (isSprinting) staminaDrain *= STAMINA_SPRINT_MULTIPLIER;

                // Non-linear: Use power curve for stamina's effect on drain (high stamina = much less drain)
                float staminaCurve = PowerCurve(player.BaseData.Stamina / 100f, 1.5f);
                float staminaAttributeMod = 1f - (STAMINA_ATTRIBUTE_DRAIN_MOD * staminaCurve);
                staminaDrain *= staminaAttributeMod;
            }

            float staminaRecovery = 0f;
            if (!isMovingSignificantly)
            {
                // Non-linear: Use sigmoid for natural fitness's effect on recovery (mid-range fitness most impactful)
                float naturalFitnessCurve = Sigmoid((player.BaseData.NaturalFitness - 50f) / 20f);
                float naturalFitnessMod = 0.8f + 0.4f * naturalFitnessCurve; // Range 0.8 to 1.2
                staminaRecovery = STAMINA_RECOVERY_RATE * naturalFitnessMod * deltaTime;
            }

            player.Stamina = Mathf.Clamp01(player.Stamina - staminaDrain + staminaRecovery);
            player.UpdateEffectiveSpeed(); // Always update, let the method handle thresholds
            
            // Remove the conditional speed reset here
        }
        // --- Non-linear Utility Functions ---
        /// <summary>
        /// Sigmoid function: returns value between 0 and 1. Use for S-curve scaling.
        /// </summary>
        private static float Sigmoid(float x)
        {
            return 1f / (1f + Mathf.Exp(-x));
        }

        /// <summary>
        /// Power curve: raises input (0..1) to the given power. Use for gentle/harsh curve.
        /// </summary>
        private static float PowerCurve(float t, float power)
        {
            return Mathf.Pow(Mathf.Clamp01(t), power);
        }
    }
}
