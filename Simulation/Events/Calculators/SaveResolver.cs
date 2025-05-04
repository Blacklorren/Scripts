using HandballManager.Core;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Utils;
using UnityEngine;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles detailed goalkeeper save resolution.
    /// </summary>
    public static class SaveResolver
    {
        public static ActionResult ResolveSaveAttempt(
            SimPlayer shooter,
            Vector3 shotDirection,
            float shotSpeed,
            float shotHeight,
            MatchState state)
        {
            if (shooter?.BaseData == null || state == null)
                return new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = shooter, Reason = "Goal scored" };

            int defendingTeamSimId = 1 - shooter.TeamSimId;
            SimPlayer gk = state.GetGoalkeeper(defendingTeamSimId);
            if (gk == null || gk.IsSuspended())
                return new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = shooter, Reason = "Goal scored" };

            Vector3 shooterPos3D = ActionCalculatorUtils.GetPosition3D(shooter);
            Vector2 goalCenter2D = (defendingTeamSimId == 0)
                ? new Vector2(ActionResolverConstants.PITCH_LENGTH, ActionResolverConstants.PITCH_CENTER_Y)
                : new Vector2(0f, ActionResolverConstants.PITCH_CENTER_Y);
            Vector3 goalCenter3D = new Vector3(goalCenter2D.x, shotHeight, goalCenter2D.y);
            float shotDistance = Vector3.Distance(shooterPos3D, goalCenter3D);
            float timeToImpact = shotSpeed > 0.1f ? shotDistance / shotSpeed : float.MaxValue;
            float agilityFactor = Mathf.Lerp(0.8f, 1.2f, (gk.BaseData?.Agility ?? 50f) / 100f);
            float reachDistance = gk.EffectiveSpeed * timeToImpact * agilityFactor;

            // Compute actual impact point using shotDirection
            Vector3 impactPoint = shooterPos3D + shotDirection.normalized * shotDistance;
            // Recalculate GK distance to impact
            float distanceToImpact = Vector3.Distance(ActionCalculatorUtils.GetPosition3D(gk), impactPoint);
            if (distanceToImpact > reachDistance + SimConstants.BALL_RADIUS + ActionResolverConstants.SAVE_REACH_BUFFER)
                return new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = shooter, Reason = "Goal scored" };

            float saveProb = ActionResolverConstants.BASE_SAVE_PROBABILITY;
            // Angle alignment: GK facing vs shot direction
            Vector3 gkPos3D = ActionCalculatorUtils.GetPosition3D(gk);
            Vector3 defaultFacing = (goalCenter3D - gkPos3D).normalized;
            Vector3 shotDirHorizontal = new Vector3(shotDirection.x, 0f, shotDirection.z).normalized;
            float facingAlignment = Mathf.Clamp01(Vector3.Dot(defaultFacing, shotDirHorizontal));

            // Gap detection: shot through uncovered area
            if (IsThroughGap(shotDirHorizontal, defaultFacing, shotHeight))
                return new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = shooter, Reason = "Shot through gap" };

            float angleMod = Mathf.Lerp(0.8f, 1.2f, facingAlignment);
            saveProb *= angleMod;

            // Distance-based modifier
            float saveDistanceMod = shotDistance <= 7f ? 0.85f
                : shotDistance >= 13f ? 1.15f
                : Mathf.Lerp(0.85f, 1.15f, (shotDistance - 7f) / 6f);
            saveProb *= saveDistanceMod;

            float reflex = gk.BaseData?.Reflexes ?? 50f;
            float oneOnOnes = gk.BaseData?.OneOnOnes ?? 50f;
            float handling = gk.BaseData?.Handling ?? 50f;
            float positioning = gk.BaseData?.PositioningGK ?? 50f;
            float penaltySaving = gk.BaseData?.PenaltySaving ?? 50f;
            if (state.CurrentPhase == GamePhase.HomePenalty || state.CurrentPhase == GamePhase.AwayPenalty)
            {
                float penaltySigmoid = Sigmoid((penaltySaving - 50f) / 20f);
                saveProb *= Mathf.Lerp(0.8f, 1.2f, penaltySigmoid);
            }
            else if (shotDistance <= 7f)
            {
                float closeWeight = 0.6f * (reflex / 100f) + 0.4f * (oneOnOnes / 100f);
                float closeSigmoid = Sigmoid((closeWeight - 0.5f) * 6f);
                saveProb *= Mathf.Lerp(0.7f, 1.3f, closeSigmoid);
            }
            else
            {
                float baseWeight = 0.5f * (reflex / 100f) + 0.5f * (positioning / 100f);
                float baseSigmoid = Sigmoid((baseWeight - 0.5f) * 6f);
                saveProb *= Mathf.Lerp(0.85f, 1.15f, baseSigmoid);
            }

            // Power and handling interaction
            float shooterPower = shooter.BaseData?.ShootingPower ?? 50f;
            float shooterPowerSigmoid = Sigmoid((shooterPower - 50f) / 20f);
            float powerPenalty = Mathf.Lerp(1.0f, 0.85f, shooterPowerSigmoid);
            float handlingSig = Sigmoid((handling - 50f) / 20f);
            float handlingMitigation = Mathf.Lerp(1.0f, 1.08f, handlingSig);
            saveProb *= powerPenalty * handlingMitigation;

            float shooterAcc = shooter.BaseData?.ShootingAccuracy ?? 50f;
            float shooterAccSigmoid = Sigmoid((shooterAcc - 50f) / 20f);
            saveProb *= Mathf.Lerp(1.05f, 0.85f, shooterAccSigmoid);

            saveProb = Mathf.Clamp(saveProb, 0.01f, 0.99f);
            if (state.RandomGenerator.NextDouble() < saveProb)
                return new ActionResult { Outcome = ActionResultOutcome.Saved, PrimaryPlayer = gk, SecondaryPlayer = shooter, Reason = "Shot saved by goalkeeper" };

            return new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = shooter, Reason = "Goal scored" };
        }

        private static float Sigmoid(float x)
        {
            return 1f / (1f + Mathf.Exp(-x));
        }

        // Detect holes in goalkeeper coverage
        private static bool IsThroughGap(Vector3 shotDirHorizontal, Vector3 defaultFacing, float shotHeight)
        {
            // Angular gaps (corners beyond coverage)
            const float coverageAngle = 50f;
            float angleDiff = Vector3.Angle(defaultFacing, shotDirHorizontal);
            if (angleDiff > coverageAngle) return true;

            // Vertical gaps (low under legs or high over reach)
            const float lowThreshold = 0.5f;
            const float highThreshold = 2.0f;
            if (shotHeight < lowThreshold || shotHeight > highThreshold) return true;

            return false;
        }
    }
}
