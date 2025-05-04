using HandballManager.Core;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Utils;
using UnityEngine;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations and resolution related to passing actions.
    /// Provides methods to determine pass accuracy and resolve the pass attempt itself.
    /// </summary>
    public class PassCalculator
    {
        // --- Constants Moved ---
        // It's generally better practice to define constants at the class level
        // or ideally, within a dedicated constants class (like ActionResolverConstants).
        // Assuming these might belong in ActionResolverConstants based on naming conventions.
        // If they are truly specific ONLY to this accurate pass calculation,
        // static readonly fields here would be appropriate. For this example,
        // let's assume they should be in ActionResolverConstants.
        // Example if they were moved to ActionResolverConstants:
        // private const float PASS_SPEED_LOW_FACTOR = ActionResolverConstants.PASS_ACCURATE_SPEED_LOW_FACTOR;
        // private const float PASS_SPEED_HIGH_FACTOR = ActionResolverConstants.PASS_ACCURATE_SPEED_HIGH_FACTOR;
        // For now, let's define them here as static readonly if they aren't elsewhere.
        private static readonly float PASS_SPEED_LOW_FACTOR = 0.8f; // Example value
        private static readonly float PASS_SPEED_HIGH_FACTOR = 1.2f; // Example value


        /// <summary>
        /// Resolves a pass attempt at the moment of release.
        /// Determines if the release is accurate or inaccurate and updates the ball state.
        /// </summary>
        /// <param name="passer">The player attempting the pass.</param>
        /// <param name="target">The intended recipient of the pass.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>An ActionResult indicating the outcome (Success, Turnover, or Failure).</returns>
        public ActionResult ResolvePassAttempt(SimPlayer passer, SimPlayer target, MatchState state)
        {
            // --- Input Validation ---
            // Check for null references early. Also check BaseData which is used extensively.
            if (passer?.BaseData == null || target?.BaseData == null || state?.Ball == null || state.RandomGenerator == null)
            {
                // Provide a more specific reason if possible
                string reason = "Invalid input: ";
                if (passer?.BaseData == null) reason += "Passer or Passer.BaseData is null. ";
                if (target?.BaseData == null) reason += "Target or Target.BaseData is null. ";
                if (state == null) reason += "MatchState is null. ";
                else if (state.Ball == null) reason += "MatchState.Ball is null. ";
                else if (state.RandomGenerator == null) reason += "MatchState.RandomGenerator is null.";

                return new ActionResult
                {
                    Outcome = ActionResultOutcome.Failure,
                    PrimaryPlayer = passer, // Passer might be null, but pass it anyway for context if available
                    SecondaryPlayer = target,
                    Reason = reason.Trim()
                };
            }

            // --- Pre-calculate common values ---
            Vector3 passerPos3D = ActionCalculatorUtils.GetPosition3D(passer);
            Vector3 targetPos3D = ActionCalculatorUtils.GetPosition3D(target);
            Vector3 directionToTarget3D = targetPos3D - passerPos3D;
            float distanceToTarget = directionToTarget3D.magnitude; // Keep distance if needed later, otherwise use sqrMagnitude for checks

            // Handle edge case where passer and target are at the exact same position
            if (directionToTarget3D.sqrMagnitude < SimConstants.FLOAT_EPSILON)
            {
                // Cannot determine a direction. Could fail, or use a default. Let's fail clearly.
                Debug.LogWarning($"Passer {passer.BaseData.FullName} and target {target.BaseData.FullName} are at the same position {passerPos3D}. Failing pass.");
                return new ActionResult
                {
                    Outcome = ActionResultOutcome.Failure,
                    PrimaryPlayer = passer,
                    SecondaryPlayer = target,
                    Reason = "Passer and target at same position"
                };
                // Or alternatively, use a default direction if failing isn't desired:
                // directionToTarget3D = Vector3.forward; // Use a default direction
                // distanceToTarget = 0f; // Although distance is technically 0
            }

            Vector3 normalizedDirection = directionToTarget3D.normalized; // Normalize *once* after the check

            // --- Pre-pass Interception Integration ---
            // Check for possible pre-pass interceptions
            var interceptionCalculator = new InterceptionCalculator(); // Or get from dependency injection
            var possibleInterceptors = state.GetPotentialInterceptors(passer, normalizedDirection, distanceToTarget);

            foreach (var defender in possibleInterceptors)
            {
                // Calculate pre-pass interception chance
                float interceptionChance = interceptionCalculator.CalculatePrePassInterceptionChance(defender, passer, target, state);

                // Check if interception happens
                if (state.RandomGenerator.NextDouble() < interceptionChance)
                {
                    // Defender reads the pass and intercepts immediately
                    state.Ball.MakeLoose(passerPos3D, Vector3.zero, defender.TeamSimId, defender);

                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[PrePassInterception] Defender {defender.BaseData?.FullName} intercepted pass from {passer.BaseData?.FullName} to {target.BaseData?.FullName}.");
                    #endif
                    // TODO: Visual feedback cue for interception (e.g., highlight defender, show animation)

                    return new ActionResult
                    {
                        Outcome = ActionResultOutcome.Turnover,
                        PrimaryPlayer = passer,
                        SecondaryPlayer = defender,
                        Reason = "Pass Read And Intercepted",
                        ImpactPosition = CoordinateUtils.To2DGround(passerPos3D)
                    };
                }
            }

            // --- Calculate Accuracy ---
            float accuracyChance = CalculatePassAccuracy(passer, target, state, distanceToTarget); // Pass pre-calculated distance

            // --- Determine Outcome (Accurate vs Inaccurate) ---
            if (state.RandomGenerator.NextDouble() < accuracyChance)
            {
                // --- ACCURATE RELEASE ---
                // Calculate base speed based on passer's skill
                // Make pass speed scale with distance (min: PASS_BASE_SPEED, max: 18 m/s at 9m+)
                float minSpeed = ActionResolverConstants.PASS_BASE_SPEED;
                float maxSpeed = 18f; // Absolute max pass speed for long passes
                float maxDistance = ActionResolverConstants.LONG_SHOT_DISTANCE; // Use same threshold as long shots for max pass speed
                float t = Mathf.Clamp01(distanceToTarget / maxDistance);
                float baseSpeed = Mathf.Lerp(minSpeed, maxSpeed, t) *
                    Mathf.Lerp(PASS_SPEED_LOW_FACTOR, PASS_SPEED_HIGH_FACTOR, passer.BaseData.Passing / 100f);

                // Calculate vertical launch angle variation
                float launchAngleVariance = ActionResolverConstants.PASS_LAUNCH_ANGLE_VARIANCE_DEG;
                float launchAngle = ActionResolverConstants.PASS_BASE_LAUNCH_ANGLE_DEG +
                                    (float)(state.RandomGenerator.NextDouble() * 2.0 - 1.0) * launchAngleVariance; // More concise random range [-Var, +Var]

                // Calculate rotation axis for vertical launch angle (perpendicular to direction and up)
                Vector3 verticalRotationAxis = Vector3.Cross(normalizedDirection, Vector3.up);
                Quaternion verticalRotation = Quaternion.identity; // Default to no rotation

                // Ensure axis is valid before creating rotation (prevents issues if direction is purely vertical)
                if (verticalRotationAxis.sqrMagnitude > SimConstants.FLOAT_EPSILON)
                {
                    verticalRotation = Quaternion.AngleAxis(launchAngle, verticalRotationAxis.normalized);
                }
                else
                {
                    // Direction is likely straight up or down, vertical rotation axis is zero.
                    // Log warning or handle as needed. Applying no extra vertical rotation is safe.
                    Debug.LogWarning($"Pass direction {normalizedDirection} is parallel to Vector3.up. Skipping vertical launch angle rotation.");
                }

                // Apply vertical launch angle rotation
                Vector3 velocityAfterVertical = verticalRotation * normalizedDirection * baseSpeed;

                // Calculate slight horizontal inaccuracy for "accurate" passes (more accurate = less offset)
                float accurateOffsetAngleDeg = UnityEngine.Random.Range(
                                                -ActionResolverConstants.PASS_ACCURATE_ANGLE_OFFSET_RANGE,
                                                 ActionResolverConstants.PASS_ACCURATE_ANGLE_OFFSET_RANGE)
                                               * (1f - accuracyChance); // Less deviation for higher accuracy chance

                Quaternion horizontalRotation = Quaternion.AngleAxis(accurateOffsetAngleDeg, Vector3.up);

                // Apply horizontal offset rotation
                Vector3 finalInitialVelocity = horizontalRotation * velocityAfterVertical;

                // Release the ball towards the target
                state.Ball.ReleaseAsPass(passer, target, finalInitialVelocity);

                passer.HasBall = false;
                state.Ball.SetHolder(null);

                return new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = passer, SecondaryPlayer = target, Reason = "Pass Released Accurately" };
            }
            else
            {
                // --- INACCURATE RELEASE ---
                // Calculate significant horizontal angle offset (more inaccurate = more offset)
                float inaccurateAngleOffsetMax = ActionResolverConstants.PASS_INACCURATE_ANGLE_OFFSET_MAX;
                float inaccurateAngleOffsetMin = ActionResolverConstants.PASS_INACCURATE_ANGLE_OFFSET_MIN;
                // Ensure min is not greater than max if constants are configurable
                float baseAngleOffset = UnityEngine.Random.Range(inaccurateAngleOffsetMin, inaccurateAngleOffsetMax);
                float angleOffset = baseAngleOffset * (1.0f - accuracyChance); // Scale offset by inaccuracy factor
                angleOffset *= (state.RandomGenerator.Next(0, 2) == 0 ? 1f : -1f); // Randomize direction (left/right)

                Quaternion horizontalRotation = Quaternion.AngleAxis(angleOffset, Vector3.up);

                // Determine the direction the inaccurate pass goes
                Vector3 missDirection = horizontalRotation * normalizedDirection;

                // Calculate speed for the inaccurate pass (usually slower or more variable)
                float missSpeed = ActionResolverConstants.PASS_BASE_SPEED *
                                  UnityEngine.Random.Range(ActionResolverConstants.PASS_INACCURATE_SPEED_MIN_FACTOR,
                                                           ActionResolverConstants.PASS_INACCURATE_SPEED_MAX_FACTOR);

                // Calculate release position with a small offset from the passer
                Vector3 releasePosition = passerPos3D + missDirection * SimConstants.BALL_RELEASE_OFFSET;

                // Make the ball loose (turnover)
                state.Ball.MakeLoose(releasePosition, missDirection * missSpeed, passer.TeamSimId, passer);

                return new ActionResult
                {
                    Outcome = ActionResultOutcome.Turnover,
                    PrimaryPlayer = passer,
                    SecondaryPlayer = target, // Target is still relevant contextually
                    Reason = "Pass Inaccurate",
                    ImpactPosition = CoordinateUtils.To2DGround(state.Ball.Position) // Use helper for ground position
                };
            }
        }

        /// <summary>
        /// Calculates the accuracy chance (0.0 to 1.0) for a pass attempt based on various factors.
        /// </summary>
        /// <param name="passer">The player attempting the pass.</param>
        /// <param name="target">The intended recipient of the pass.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="distance">Optional pre-calculated distance between passer and target.</param>
        /// <returns>A float representing the probability of the pass being accurate (0.0 to 1.0).</returns>
        public float CalculatePassAccuracy(SimPlayer passer, SimPlayer target, MatchState state, float? distance = null)
        {
            // Basic validation
            if (passer?.BaseData == null || target == null || state == null)
            {
                Debug.LogError("CalculatePassAccuracy called with invalid parameters.");
                return 0f;
            }

            float baseAcc = ActionResolverConstants.BASE_PASS_ACCURACY;
            float avgSkill = (passer.BaseData.Passing + passer.BaseData.DecisionMaking + passer.BaseData.Technique) / 3f;
            float attrScore = Sigmoid((avgSkill - 50f) / 15f);

            float distVal = distance ?? Vector2.Distance(passer.Position, target.Position);
            float distFactor = Mathf.Exp(-distVal / ActionResolverConstants.PITCH_LENGTH);

            float pressure = ActionCalculatorUtils.CalculatePressureOnPlayer(passer, state);
            float pressNorm = Mathf.Clamp01(pressure / ActionResolverConstants.MAX_PRESSURE_DIST);
            float pressFactor = 1f - pressNorm * pressNorm;

            // --- Fatigue Penalty (Stamina) ---
            float stamina = passer.Stamina;
            float fatigueThreshold = 0.5f; // Penalty starts below 50% stamina
            float maxFatiguePenalty = 0.3f; // Max 30% reduction in accuracy due to fatigue
            float fatiguePenaltyFactor = 1.0f; // Default: no penalty
            if (stamina < fatigueThreshold)
            {
                // Non-linear scaling: penalty increases quadratically as stamina drops below threshold
                fatiguePenaltyFactor = 1.0f - (Mathf.Pow((fatigueThreshold - stamina) / fatigueThreshold, 2.0f) * maxFatiguePenalty);
            }

            float noiseSigma = 0.02f;
            float noise = (float)(state.RandomGenerator.NextDouble() * noiseSigma - noiseSigma / 2f);

            // Apply fatigue penalty factor
            float accuracyChance = baseAcc * attrScore * distFactor * pressFactor * fatiguePenaltyFactor + noise;
            return Mathf.Clamp01(accuracyChance);
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
// --- END OF REVISED FILE PassCalculator.cs ---