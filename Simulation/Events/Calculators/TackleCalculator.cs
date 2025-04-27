using UnityEngine;
using System;
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations and resolution related to tackling actions.
    /// </summary>
    public class TackleCalculator
    {
        private readonly FoulCalculator _foulCalculator; // Dependency

        public TackleCalculator(FoulCalculator foulCalculator)
        {
            _foulCalculator = foulCalculator ?? throw new ArgumentNullException(nameof(foulCalculator));
        }

        /// <summary>
        /// Calculates and resolves the outcome of a tackle attempt (Success, Foul, Failure).
        /// </summary>
        public ActionResult ResolveTackleAttempt(SimPlayer tackler, MatchState state)
        {
             SimPlayer target = tackler?.TargetPlayer; // Get target from tackler state

             // Re-verify target validity and range
             if (tackler == null || target == null || state == null || 
    target != state.Ball.Holder ||  // Only allow tackles on current ball holder
    Vector2.Distance(tackler.Position, target.Position) > ActionResolverConstants.TACKLE_RADIUS * 1.1f)
             {
                  if(tackler != null) { // Reset tackler if possible
                      tackler.CurrentAction = PlayerAction.Idle;
                      tackler.ActionTimer = 0f;
                  }
                  return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = tackler, Reason = "Tackle Target Invalid/Out of Range on Release" };
             }

             var (successChance, foulChance) = CalculateTackleProbabilities(tackler, target, state);

             float totalProb = successChance + foulChance;
             if (totalProb > 1.0f) { successChance /= totalProb; foulChance /= totalProb; }
             // float failureChance = Mathf.Max(0f, 1.0f - successChance - foulChance); // Not explicitly needed for roll check

             double roll = state.RandomGenerator.NextDouble();
             Vector2 impactPos = (tackler.Position != Vector2.zero && target.Position != Vector2.zero) 
    ? Vector2.Lerp(tackler.Position, target.Position, 0.5f)
    : (tackler.Position != Vector2.zero ? tackler.Position : target.Position);

             // Reset actions for both players *after* calculations
              tackler.CurrentAction = PlayerAction.Idle;
              tackler.ActionTimer = 0f;
              if (target.CurrentAction != PlayerAction.Suspended) {
                   target.CurrentAction = PlayerAction.Idle;
                   target.ActionTimer = 0f;
              }

             if (roll < successChance) {
                 // SUCCESS
                 bool targetHadBall = target.HasBall; // Must check BEFORE MakeLoose
                 if (targetHadBall) {
                     state.Ball.MakeLoose(
    ActionCalculatorUtils.GetPosition3D(target) + 
    new Vector3((float)state.RandomGenerator.NextDouble() - 0.5f, 0, (float)state.RandomGenerator.NextDouble() - 0.5f) * 0.2f,
    new Vector3(
        (float)(state.RandomGenerator.NextDouble() * 2 - 1),
        0,
        (float)(state.RandomGenerator.NextDouble() * 2 - 1)
    ).normalized * (float)state.RandomGenerator.NextDouble() * 3f,
    tackler.TeamSimId, 
    tackler
);
                 } else if (state.Ball.Holder == target) {
                    state.Ball.SetPossession(null); // Safety cleanup
                }
                 return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = tackler, SecondaryPlayer = target, ImpactPosition = impactPos, Reason = targetHadBall ? "Tackle Won Ball" : "Tackle Successful (No Ball)" };
             }
             else if (roll < successChance + foulChance) {
                 // FOUL
                 FoulSeverity severity = _foulCalculator.DetermineFoulSeverity(tackler, target, state); // Use FoulCalculator

                 if (target.HasBall) target.HasBall = false;
                 if (state.Ball.Holder == target) state.Ball.SetPossession(null);
                 state.Ball.Stop();
                 state.Ball.Position = ActionCalculatorUtils.GetPosition3D(target); // Ball dead at target pos

                 return new ActionResult { Outcome = ActionResultOutcome.FoulCommitted, PrimaryPlayer = tackler, SecondaryPlayer = target, ImpactPosition = impactPos, FoulSeverity = severity };
             }
             else {
                 // FAILURE (EVADED)
                 return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = tackler, SecondaryPlayer = target, Reason = "Tackle Evaded", ImpactPosition = impactPos };
             }
        }

        /// <summary>
        /// Calculates the probabilities of success and foul for a potential tackle attempt.
        /// </summary>
        public (float successChance, float foulChance) CalculateTackleProbabilities(SimPlayer tackler, SimPlayer target, MatchState state)
        {
            if (tackler?.BaseData == null || target?.BaseData == null || state == null) return (0f, 0f);

            float successChance = ActionResolverConstants.BASE_TACKLE_SUCCESS;
            float foulChance = ActionResolverConstants.BASE_TACKLE_FOUL_CHANCE;

            // Attributes principaux
            float tacklerSkill = (tackler.BaseData.Tackling * ActionResolverConstants.TACKLE_SKILL_WEIGHT_TACKLING +
                                  tackler.BaseData.Strength * ActionResolverConstants.TACKLE_SKILL_WEIGHT_STRENGTH +
                                  tackler.BaseData.Anticipation * ActionResolverConstants.TACKLE_SKILL_WEIGHT_ANTICIPATION);
            float targetSkill = (target.BaseData.Dribbling * ActionResolverConstants.TARGET_SKILL_WEIGHT_DRIBBLING +
                                 target.BaseData.Agility * ActionResolverConstants.TARGET_SKILL_WEIGHT_AGILITY +
                                 target.BaseData.Strength * ActionResolverConstants.TARGET_SKILL_WEIGHT_STRENGTH +
                                 target.BaseData.Composure * ActionResolverConstants.TARGET_SKILL_WEIGHT_COMPOSURE);

            // --- Stamina & WorkRate du tackleur : réduisent la pénalité de fatigue sur la réussite du tacle ---
            // Non-linear: stamina effect is gentle at high stamina, harsher at low
            float staminaMod = Mathf.Lerp(0.7f, 1.0f, Mathf.Pow(tackler.Stamina, 1.5f));
            // Direct strength factor: stronger tackler vs. weaker target increases chance
            float strengthRatio = (tackler.BaseData.Strength + 1f) / (target.BaseData.Strength + 1f);
            successChance *= Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(strengthRatio / 2f));
            float workRateMod = Mathf.Lerp(0.97f, 1.0f, tackler.BaseData.WorkRate / 100f); // max +3%
            // --- Determination du tackleur : bonus subtil sur la réussite ---
            float determinationMod = Mathf.Lerp(0.97f, 1.0f, tackler.BaseData.Determination / 100f); // max +3%
            // --- Positioning du tackleur : réduit le risque de faute et augmente légèrement la réussite ---
            float positioningMod = Mathf.Lerp(0.97f, 1.0f, tackler.BaseData.Positioning / 100f); // max +3%
            // --- DecisionMaking du tackleur : réduit le risque de faute ---
            float decisionMod = Mathf.Lerp(1.0f, 0.97f, tackler.BaseData.DecisionMaking / 100f); // max -3% faute
            // --- Resilience du tackleur : réduit la pénalité de fatigue sur la réussite ---
            float resilienceMod = Mathf.Lerp(0.96f, 1.0f, tackler.BaseData.Resilience / 100f); // max +4%

            // Non-linear: Use sigmoid for skill ratio to avoid extreme swings
            float ratio = tacklerSkill / Mathf.Max(ActionResolverConstants.MIN_TACKLE_TARGET_SKILL_DENOMINATOR, targetSkill);
            float skillSigmoid = Sigmoid((ratio - 1f) * 3f); // Centered at 1, steepness tuned
            successChance *= Mathf.Lerp(1.0f - ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_SUCCESS_SKILL_RANGE_MOD,
                                       1.0f + ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_SUCCESS_SKILL_RANGE_MOD,
                                       skillSigmoid);

            // Non-linear: Use sigmoid for foul skill ratio
            float foulSkillRatio = targetSkill / Mathf.Max(ActionResolverConstants.MIN_TACKLE_TARGET_SKILL_DENOMINATOR, tacklerSkill);
            float foulSkillSigmoid = Sigmoid((foulSkillRatio - 1f) * 3f);
            foulChance *= Mathf.Lerp(1.0f - ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_FOUL_SKILL_RANGE_MOD * 0.5f,
                                    1.0f + ActionResolverConstants.TACKLE_ATTRIBUTE_SCALING * ActionResolverConstants.TACKLE_FOUL_SKILL_RANGE_MOD,
                                    foulSkillSigmoid);

            // Application des modificateurs subtils
            successChance *= staminaMod * workRateMod * determinationMod * positioningMod * resilienceMod;
            foulChance *= decisionMod * (2f - positioningMod); // positioningMod < 1 donc réduit la faute

            // --- Mental Attribute Integration ---
            // Composure: add bonus to success (up to 2%)
            successChance *= 1.0f + (tackler.BaseData.Composure / 350f);
            // Concentration: add random penalty to success, reduced by high concentration
            float concentrationNoise = UnityEngine.Random.Range(0f, 0.01f) * (1.0f - (tackler.BaseData.Concentration / 100f));
            successChance -= concentrationNoise;
            // Decision Making: if very low, add slight penalty to success
            if (tackler.BaseData.DecisionMaking < 40)
                successChance *= 0.98f;

            // Situationals
            // Non-linear: Use power curve for aggression's effect on foul chance
            float aggressionNonLinear = Mathf.Pow(tackler.BaseData.Aggression / 100f, 1.3f);
            foulChance *= Mathf.Lerp(ActionResolverConstants.TACKLE_AGGRESSION_FOUL_FACTOR_MIN, ActionResolverConstants.TACKLE_AGGRESSION_FOUL_FACTOR_MAX, aggressionNonLinear);
            if (ActionCalculatorUtils.IsTackleFromBehind(tackler, target)) foulChance *= ActionResolverConstants.TACKLE_FROM_BEHIND_FOUL_MOD; // Use Util
            float closingSpeed = ActionCalculatorUtils.CalculateClosingSpeed(tackler, target); // Use Util
            float highSpeedThreshold = ActionResolverConstants.MAX_PLAYER_SPEED * ActionResolverConstants.TACKLE_HIGH_SPEED_THRESHOLD_FACTOR;
            if (closingSpeed > highSpeedThreshold) {
                foulChance *= Mathf.Lerp(1.0f, ActionResolverConstants.TACKLE_HIGH_SPEED_FOUL_MOD, Mathf.Clamp01((closingSpeed - highSpeedThreshold) / (ActionResolverConstants.MAX_PLAYER_SPEED * (1.0f - ActionResolverConstants.TACKLE_HIGH_SPEED_THRESHOLD_FACTOR))));
            }
            if (ActionCalculatorUtils.IsClearScoringChance(target, state)) foulChance *= ActionResolverConstants.TACKLE_CLEAR_CHANCE_FOUL_MOD; // Use Util

            successChance = Mathf.Clamp01(successChance);
            foulChance = Mathf.Clamp01(foulChance);

            return (successChance, foulChance);
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