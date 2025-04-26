using UnityEngine;
using System;
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations related to determining foul severity.
    /// </summary>
    public class FoulCalculator
    {
        /// <summary>
        /// Determines the severity of a foul based on tackle context and match state
        /// </summary>
        /// <param name="tackler">Player committing the foul</param>
        /// <param name="target">Player being fouled</param>
        /// <param name="state">Current match state</param>
        /// <returns>Calculated foul severity level</returns>
        public FoulSeverity DetermineFoulSeverity(SimPlayer tackler, SimPlayer target, MatchState state)
        {
            if (tackler?.BaseData is null || target?.BaseData is null || state is null) 
                return FoulSeverity.FreeThrow;

            FoulSeverity severity = FoulSeverity.FreeThrow;
            float severityRoll = (float)state.RandomGenerator.NextDouble();
            float baseSeverityFactor = 0f;

            // --- Modificateurs secondaires (ajustés selon leur impact réel) ---
            // DecisionMaking : réduit sensiblement la sévérité (jusqu’à -5%)
            float decisionMod = Mathf.Lerp(1.05f, 1.0f, tackler.BaseData.DecisionMaking / 100f); // max -5%
            // Composure : réduit la sévérité (jusqu’à -3%)
            float composureMod = Mathf.Lerp(1.03f, 1.0f, tackler.BaseData.Composure / 100f); // max -3%
            // Resilience : réduit la sévérité (jusqu’à -2%)
            float resilienceMod = Mathf.Lerp(1.02f, 1.0f, tackler.BaseData.Resilience / 100f); // max -2%
            // Determination : peut augmenter la sévérité (jusqu’à +2%)
            float determinationMod = Mathf.Lerp(1.0f, 1.02f, tackler.BaseData.Determination / 100f); // max +2%
            // WorkRate : peut augmenter la sévérité (jusqu’à +1.5%)
            float workRateMod = Mathf.Lerp(1.0f, 1.015f, tackler.BaseData.WorkRate / 100f); // max +1.5%
            // Stamina : réduit la sévérité (jusqu’à -2%)
            float staminaMod = Mathf.Lerp(1.02f, 1.0f, tackler.BaseData.Stamina / 100f); // max -2%
            // Différence de gabarit (Strength) : jusqu’à +5% si écart ≥ +20
            float strengthDiff = tackler.BaseData.Strength - target.BaseData.Strength;
            float strengthMod = Mathf.Lerp(1.0f, 1.05f, Mathf.Clamp01((strengthDiff+20f)/100f)); // max +5%

            // Application des modificateurs secondaires (effet cumulatif)
            float secondaryMods = decisionMod * composureMod * resilienceMod * determinationMod * workRateMod * staminaMod * strengthMod;

            bool isFromBehind = ActionCalculatorUtils.IsTackleFromBehind(tackler, target); // Use Util
            if (isFromBehind) baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_FROM_BEHIND_BONUS;

            if (ActionResolverConstants.MAX_PLAYER_SPEED <= 0f)
                throw new InvalidOperationException("MAX_PLAYER_SPEED must be positive");

            float closingSpeed = ActionCalculatorUtils.CalculateClosingSpeed(tackler, target); // Use Util
            if (closingSpeed > ActionResolverConstants.MAX_PLAYER_SPEED * ActionResolverConstants.FOUL_SEVERITY_HIGH_SPEED_THRESHOLD_FACTOR)
                baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_HIGH_SPEED_BONUS;

            baseSeverityFactor += Mathf.Clamp((tackler.BaseData.Aggression - 50f) / 50f, -1f, 1f) * ActionResolverConstants.FOUL_SEVERITY_AGGRESSION_FACTOR;

            // Documentation attributs secondaires :
            // - DecisionMaking : réduit la sévérité si élevé
            // - Composure : réduit la sévérité sous pression
            // - Resilience : réduit la sévérité sur fautes répétées
            // - Determination : peut augmenter la sévérité sur engagement
            // - WorkRate : peut augmenter la sévérité si très engagé
            // - Stamina : réduit la sévérité si bonne gestion fatigue
            // - Strength (différence) : plus le tackleur est costaud vs cible, plus la faute est jugée sévère

            bool clearScoringChance = ActionCalculatorUtils.IsClearScoringChance(target, state); // Use Util
            if (clearScoringChance) {
                baseSeverityFactor += ActionResolverConstants.FOUL_SEVERITY_DOGSO_BONUS;

                bool reckless = (isFromBehind && tackler.BaseData.Aggression > ActionResolverConstants.FOUL_SEVERITY_RECKLESS_AGGRESSION_THRESHOLD) ||
                                closingSpeed > ActionResolverConstants.MAX_PLAYER_SPEED * ActionResolverConstants.FOUL_SEVERITY_RECKLESS_SPEED_THRESHOLD_FACTOR;
                float redCardChanceDOGSO = Mathf.Clamp(
                    ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_BASE_CHANCE
                    + baseSeverityFactor * ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_SEVERITY_SCALE
                    + (reckless ? ActionResolverConstants.FOUL_SEVERITY_DOGSO_RED_CARD_RECKLESS_BONUS : 0f),
                    0f, 1f
                );
                if (severityRoll < redCardChanceDOGSO) {
                    return FoulSeverity.RedCard;
                }
            }

            float twoMinuteThreshold = ActionResolverConstants.FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_BASE + baseSeverityFactor * ActionResolverConstants.FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_SEVERITY_SCALE;
            float redCardThreshold = ActionResolverConstants.FOUL_SEVERITY_RED_CARD_THRESHOLD_BASE + baseSeverityFactor * ActionResolverConstants.FOUL_SEVERITY_RED_CARD_THRESHOLD_SEVERITY_SCALE;

            // Application finale des modificateurs secondaires sur les seuils (effet subtil)
            twoMinuteThreshold *= secondaryMods;
            redCardThreshold *= secondaryMods;

            twoMinuteThreshold = Mathf.Clamp01(twoMinuteThreshold);
            redCardThreshold = Mathf.Clamp01(redCardThreshold);

            if (severityRoll < redCardThreshold) { severity = FoulSeverity.RedCard; }
            else if (severityRoll < twoMinuteThreshold) { severity = FoulSeverity.TwoMinuteSuspension; }

            // TODO: Add logic for OffensiveFoul based on movement/charge?

            return severity;
        }
    }
}