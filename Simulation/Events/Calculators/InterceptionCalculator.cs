using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Utils;
using HandballManager.Core;
using UnityEngine;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations related to interception chances.
    /// </summary>
    public class InterceptionCalculator
    {
        private const float MIN_PASS_DISTANCE = 1.0f;
        /// <summary>
        /// Calculates the probability of a defender intercepting a specific pass in flight.
        /// </summary>
        public float CalculateInterceptionChance(SimPlayer defender, SimBall ball, MatchState state)
        {
            /// <summary>
            /// Early validation: defender must be valid, ball must be a valid pass in flight, and defender cannot be on the same team as the passer.
            /// Returns 0 if interception is not possible.
            /// </summary>
            if (defender?.BaseData is null 
                || ball is not { IsInFlight: true, IntendedTarget: not null, Passer: not null } 
                || defender?.TeamSimId == ball.Passer?.TeamSimId)
                return 0f;

            // Prevent goalkeepers from intercepting passes outside their area (or modify as needed)
            if (defender.AssignedTacticalRole == PlayerPosition.Goalkeeper)
                return 0f;

            float baseChance = ActionResolverConstants.INTERCEPTION_BASE_CHANCE;

            // Skill
            float defenderSkill = (defender.BaseData.Anticipation * ActionResolverConstants.INTERCEPTION_SKILL_WEIGHT_ANTICIPATION +
                                   defender.BaseData.Agility * ActionResolverConstants.INTERCEPTION_SKILL_WEIGHT_AGILITY +
                                   defender.BaseData.Positioning * ActionResolverConstants.INTERCEPTION_SKILL_WEIGHT_POSITIONING);
            float skillMod = Mathf.Lerp(ActionResolverConstants.INTERCEPTION_SKILL_MIN_MOD, ActionResolverConstants.INTERCEPTION_SKILL_MAX_MOD, defenderSkill / 100f);

            // --- Modificateurs secondaires (subtils) ---
            // WorkRate : bonus subtil pour les efforts répétés
            float workRateMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.WorkRate / 100f); // max +3%
            // Stamina : réduit la pénalité de fatigue
            float staminaMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.Stamina / 100f); // max +3%
            // Determination : bonus subtil sur l'interception
            float determinationMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.Determination / 100f); // max +3%
            // Resilience : réduit l'impact de la fatigue
            float resilienceMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.Resilience / 100f); // max +3%
            // DecisionMaking : bonus subtil sur la réussite
            float decisionMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.DecisionMaking / 100f); // max +3%

            // Position
            float distToLine = SimulationUtils.CalculateDistanceToLine(defender.Position, CoordinateUtils.To2DGround(ball.PassOrigin), ball.IntendedTarget.Position); // Use helpers
            float lineProximityFactor = Mathf.Clamp01(1.0f - (distToLine / ActionResolverConstants.INTERCEPTION_RADIUS));
            lineProximityFactor *= lineProximityFactor;

            Vector2 ballPos2D = CoordinateUtils.To2DGround(ball.Position); // Use helper
            float distToBall = Vector2.Distance(defender.Position, ballPos2D);
            float ballProximityFactor = Mathf.Clamp01(1.0f - (distToBall / (ActionResolverConstants.INTERCEPTION_RADIUS * ActionResolverConstants.INTERCEPTION_RADIUS_EXTENDED_FACTOR)));

            // Pass Properties
            Vector2 passOrigin2D = CoordinateUtils.To2DGround(ball.PassOrigin);
            Vector2 targetPos2D = ball.IntendedTarget.Position;
            float passDistTotal = Vector2.Distance(passOrigin2D, targetPos2D);
            if (passDistTotal < MIN_PASS_DISTANCE) passDistTotal = MIN_PASS_DISTANCE;
            float passDistTravelled = Vector2.Distance(CoordinateUtils.To2DGround(ball.PassOrigin), ballPos2D);
            float passProgress = Mathf.Clamp01(passDistTravelled / passDistTotal);
            float passProgressFactor = ActionResolverConstants.INTERCEPTION_PASS_PROGRESS_BASE_FACTOR + (ActionResolverConstants.INTERCEPTION_PASS_PROGRESS_MIDPOINT_BONUS * Mathf.Sin(passProgress * Mathf.PI));

            // Ball Speed
            float ballSpeedFactor = Mathf.Clamp(1.0f - (ball.Velocity.magnitude / (ActionResolverConstants.PASS_BASE_SPEED * 1.5f)), // Use constant
                                                1.0f - ActionResolverConstants.INTERCEPTION_PASS_SPEED_MAX_PENALTY, 1.0f);

            // Combine
            float finalChance = baseChance
                              * Mathf.Lerp(1.0f, skillMod, ActionResolverConstants.INTERCEPTION_ATTRIBUTE_WEIGHT)
                              * Mathf.Lerp(1.0f, lineProximityFactor * ballProximityFactor, ActionResolverConstants.INTERCEPTION_POSITION_WEIGHT)
                              * passProgressFactor
                              * ballSpeedFactor
                              * workRateMod * staminaMod * determinationMod * resilienceMod * decisionMod;

            // Documentation attributs secondaires :
            // - WorkRate : bonus subtil sur interception répétée
            // - Stamina : réduit pénalité fatigue
            // - Determination : bonus subtil
            // - Resilience : réduit impact fatigue
            // - DecisionMaking : bonus subtil

            // Movement Direction
            if (defender.Velocity.sqrMagnitude > 1f) {
                Vector2 defenderToBallDir = (ballPos2D - defender.Position).normalized;
                float closingFactor = Vector2.Dot(defender.Velocity.normalized, defenderToBallDir);
                finalChance *= Mathf.Lerp(ActionResolverConstants.INTERCEPTION_CLOSING_FACTOR_MIN_SCALE, ActionResolverConstants.INTERCEPTION_CLOSING_FACTOR_MAX_SCALE, (closingFactor + 1f) / 2f);
            }

            // Awareness factor
            float awareness = CalculatePlayerAwareness(defender, ball);
            finalChance *= Mathf.Lerp(ActionResolverConstants.AWARENESS_MIN_FACTOR, ActionResolverConstants.AWARENESS_MAX_FACTOR, awareness);

            // --- Stamina Penalty (fatigue = 1 - Stamina) ---
            if (ActionResolverConstants.INTERCEPTION_FATIGUE_MAX_EFFECT > 0)
            {
                float fatigueEffect = Mathf.Lerp(ActionResolverConstants.INTERCEPTION_FATIGUE_MIN_EFFECT,
                    ActionResolverConstants.INTERCEPTION_FATIGUE_MAX_EFFECT,
                    1f - defender.Stamina);
                finalChance *= (1f - fatigueEffect);
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Stamina] Defender {defender.BaseData?.FullName} stamina: {defender.Stamina:F2}, effect: {fatigueEffect:F2}");
                #endif
            }

            return Mathf.Clamp01(finalChance);
        }

        /// <summary>
        /// Calculates how aware a player is of the ball based on their looking direction.
        /// Returns a value from 0.0 (facing away) to 1.0 (looking directly at the ball).
        /// </summary>
        /// <param name="player">The player whose awareness is being evaluated.</param>
        /// <param name="ball">The ball object.</param>
        /// <returns>Awareness score between 0.0 and 1.0.</returns>
        public float CalculatePlayerAwareness(SimPlayer player, SimBall ball)
        {
            if (player == null || ball == null)
            {
                Debug.LogWarning("CalculatePlayerAwareness called with null player or ball.");
                return 0f;
            }
            if (player.LookDirection.sqrMagnitude < 0.01f)
            {
                Debug.Log($"Player {player.GetPlayerId()} has no look direction set.");
                return 0f;
            }
            Vector2 playerPos2D = player.Position;
            Vector2 ballPos2D = CoordinateUtils.To2DGround(ball.Position);
            Vector2 toBall = (ballPos2D - playerPos2D).normalized;
            Vector2 lookDir = player.LookDirection.normalized;
            float dot = Vector2.Dot(lookDir, toBall);
            // Scale from -1..1 to 0..1
            float awareness = Mathf.Clamp01((dot + 1f) * 0.5f);
            return awareness;
        }
        /// <summary>
        /// Calculates the probability of a defender intercepting a pass before it's released.
        /// </summary>
        /// <param name="defender">The defending player.</param>
        /// <param name="passer">The player attempting to pass.</param>
        /// <param name="target">The intended target of the pass.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>A float representing the probability (0.0 to 1.0) of intercepting the pass.</returns>
        public float CalculatePrePassInterceptionChance(SimPlayer defender, SimPlayer passer, SimPlayer target, MatchState state)
        {
            // Basic validation
            if (defender?.BaseData == null || passer?.BaseData == null || target == null || state == null)
                return 0f;

            // Cannot intercept passes from teammates
            if (defender.TeamSimId == passer.TeamSimId)
                return 0f;

            // Base chance is lower than regular interception
            float baseChance = ActionResolverConstants.PRE_PASS_INTERCEPTION_BASE_CHANCE;

            // Skill factor - weights anticipation more heavily for reading passes
            float defenderSkill = (defender.BaseData.Anticipation * ActionResolverConstants.PRE_PASS_SKILL_WEIGHT_ANTICIPATION +
                                  defender.BaseData.Agility * ActionResolverConstants.PRE_PASS_SKILL_WEIGHT_AGILITY +
                                  defender.BaseData.Positioning * ActionResolverConstants.PRE_PASS_SKILL_WEIGHT_POSITIONING);
            float skillMod = Mathf.Lerp(ActionResolverConstants.PRE_PASS_SKILL_MIN_MOD, 
                                       ActionResolverConstants.PRE_PASS_SKILL_MAX_MOD, 
                                       defenderSkill / 100f);

            // --- Modificateurs secondaires (subtils) ---
            float workRateMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.WorkRate / 100f); // max +3%
            float staminaMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.Stamina / 100f); // max +3%
            float determinationMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.Determination / 100f); // max +3%
            float resilienceMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.Resilience / 100f); // max +3%
            float decisionMod = Mathf.Lerp(0.97f, 1.0f, defender.BaseData.DecisionMaking / 100f); // max +3%

            // Position factor - how close defender is to pass line
            Vector2 passStartPos = passer.Position;
            Vector2 passEndPos = target.Position;
            float distToLine = SimulationUtils.CalculateDistanceToLine(defender.Position, passStartPos, passEndPos);
            float lineProximityFactor = Mathf.Clamp01(1.0f - (distToLine / ActionResolverConstants.PRE_PASS_INTERCEPTION_RADIUS));

            // Distance to passer factor - closer is better for reading the pass
            float distToPasser = Vector2.Distance(defender.Position, passer.Position);
            float passerProximityFactor = Mathf.Clamp01(1.0f - (distToPasser / ActionResolverConstants.PRE_PASS_PROXIMITY_RADIUS));

            // Is defender looking at the passer?
            Vector2 defenderToPasserDir = (passer.Position - defender.Position).normalized;
            float awarenessModifier = Vector2.Dot(defender.LookDirection, defenderToPasserDir);
            awarenessModifier = Mathf.Clamp01((awarenessModifier + 1f) / 2f); // Scale from [-1,1] to [0,1]

            // Calculate final chance
            float finalChance = baseChance
                              * skillMod
                              * lineProximityFactor
                              * passerProximityFactor
                              * awarenessModifier
                              * workRateMod * staminaMod * determinationMod * resilienceMod * decisionMod;

            // Documentation attributs secondaires :
            // - WorkRate : bonus subtil sur interception répétée
            // - Stamina : réduit pénalité fatigue
            // - Determination : bonus subtil
            // - Resilience : réduit impact fatigue
            // - DecisionMaking : bonus subtil

            finalChance = Mathf.Clamp01(finalChance);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PrePassInterception] Defender: {defender.BaseData?.FullName}, Passer: {passer.BaseData?.FullName}, Target: {target.BaseData?.FullName}, Chance: {finalChance:F3}, SkillMod: {skillMod:F2}, Proximity: {lineProximityFactor:F2}, PasserDist: {passerProximityFactor:F2}, Awareness: {awarenessModifier:F2}");
#endif
            return finalChance;
        }
    }
}