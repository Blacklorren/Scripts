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

                float staminaAttributeMod = 1f - (STAMINA_ATTRIBUTE_DRAIN_MOD * (player.BaseData.Stamina / 100f));
                staminaDrain *= staminaAttributeMod;
            }

            float staminaRecovery = 0f;
            if (!isMovingSignificantly)
            {
                float naturalFitnessMod = 1f + (NATURAL_FITNESS_RECOVERY_MOD * ((player.BaseData.NaturalFitness - 50f) / 50f));
                staminaRecovery = STAMINA_RECOVERY_RATE * naturalFitnessMod * deltaTime;
            }

            player.Stamina = Mathf.Clamp01(player.Stamina - staminaDrain + staminaRecovery);
            player.UpdateEffectiveSpeed(); // Always update, let the method handle thresholds
            
            // Remove the conditional speed reset here
        }
    }
}
