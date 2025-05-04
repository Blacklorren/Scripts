using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Physics;
using UnityEngine;
using HandballManager.Core;
using HandballManager.Simulation.AI.Evaluation; // Added for IGameStateEvaluator dependency

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles calculations and resolution related to shooting actions.
    /// </summary>
    public class ShotCalculator
    {
        private readonly BlockCalculator _blockCalculator;
        private readonly JumpSimulator _jumpSimulator;

        public ShotCalculator(BlockCalculator blockCalculator, JumpSimulator jumpSimulator)
        {
            _blockCalculator = blockCalculator ?? throw new System.ArgumentNullException(nameof(blockCalculator));
            _jumpSimulator = jumpSimulator ?? throw new System.ArgumentNullException(nameof(jumpSimulator));
        }

        public ShotCalculator(BlockCalculator blockCalculator)
        {
            _blockCalculator = blockCalculator ?? throw new System.ArgumentNullException(nameof(blockCalculator));
            // Warning: JumpSimulator might be null here if this constructor is used.
            // Consider ensuring JumpSimulator is always provided or handle null case.
            _jumpSimulator = null; // Explicitly null if not provided
        }
        /// <summary>
        /// Resolves the release of a shot. Sets the ball in flight with calculated inaccuracy and spin.
        /// </summary>
        public ActionResult ResolveShotAttempt(SimPlayer shooter, MatchState state, HandballManager.Simulation.AI.Evaluation.IGameStateEvaluator evaluator)
        {
            // --- Penalty Shot Resolution --- 
            if (state.CurrentPhase == GamePhase.HomePenalty || state.CurrentPhase == GamePhase.AwayPenalty)
            {
                return ResolvePenaltyShot(shooter, state);
            }

            // --- Normal Shot Resolution (Existing Logic) --- 
            // Conditional jump logic
            if (shooter?.BaseData != null && state != null && _jumpSimulator != null && JumpDecisionUtils.ShouldJumpForShot(shooter, state, evaluator))
            {
                // Calculate vertical velocity based on Jumping attribute
                float jumpingValue = Mathf.Clamp(shooter.BaseData.Jumping, 0f, 100f);
                // Non-linear: Use sigmoid for jumping effect (mid-range jumpers benefit most)
                float jumpingFactor = Sigmoid((jumpingValue - 50f) / 20f);
                float verticalVelocity = Mathf.Lerp(
                    SimConstants.MIN_JUMP_VERTICAL_VELOCITY,
                    SimConstants.MAX_JUMP_VERTICAL_VELOCITY,
                    jumpingFactor
                );
                // Use the shot direction's X/Z for horizontal, verticalVelocity for Y
                Vector2 jumpVelocity = new Vector2(0f, verticalVelocity); // If you want to add horizontal, use actualDirection3D.x/z
                _jumpSimulator.StartJump(shooter, jumpVelocity);
            }

            if (shooter?.BaseData is null || state is null)
                return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = shooter, Reason = "Null input in ResolveShotAttempt" };

            Vector2 targetGoalCenter2D = (shooter.TeamSimId == 0)
                ? new Vector2(ActionResolverConstants.PITCH_LENGTH, ActionResolverConstants.PITCH_CENTER_Y)
                : new Vector2(0f, ActionResolverConstants.PITCH_CENTER_Y);

            // --- Attributs utilisés dans le calcul du tir ---
            // Non-linear: Use sigmoid for shooting accuracy (S-curve)
            float accuracyRaw = Mathf.Clamp(shooter.BaseData.ShootingAccuracy, 1f, 100f);
            float accuracyFactor = Sigmoid((accuracyRaw - 50f) / 20f); // S-curve, steepness tuned by divisor
            float pressure = ActionCalculatorUtils.CalculatePressureOnPlayer(shooter, state); // Pression défensive
            // --- Mental Attribute Integration ---
            // Composure: reduce pressure effect more strongly if high (tuned scaling)
            // Non-linear: Use power curve for pressure effect
            float pressureNonLinear = Mathf.Pow(pressure, 1.3f); // Amplifies moderate/high pressure
            float pressureEffectiveness = pressureNonLinear * (1.0f - (shooter.BaseData.Composure / 120f));

            // Stamina (fatigue = 1 - Stamina) : impacte négativement la précision et la puissance
            // Non-linear: stamina effect is gentle at high stamina, harsher at low
            float staminaEffect = Mathf.Lerp(0.7f, 1.0f, Mathf.Pow(shooter.Stamina, 1.5f));
            // Détermination et WorkRate réduisent l'effet de la fatigue
            float determinationMod = Mathf.Lerp(0.95f, 1.0f, shooter.BaseData.Determination / 100f);
            float workRateMod = Mathf.Lerp(0.95f, 1.0f, shooter.BaseData.WorkRate / 100f);
            staminaEffect *= determinationMod * workRateMod;

            // Force : réduit l'impact de la pression physique
            float strengthEffect = Mathf.Lerp(1.1f, 1.0f, shooter.BaseData.Strength / 100f); // Plus fort = moins d'impact négatif
            // Agilité : réduit la perte de précision sur tir en mouvement
            float agilityEffect = Mathf.Lerp(0.95f, 1.0f, shooter.BaseData.Agility / 100f);
            // Positioning : améliore l'angle de tir (réduit la déviation)
            float positioningMod = Mathf.Lerp(1.0f, 0.95f, shooter.BaseData.Positioning / 100f);

            float baseAcc = ActionResolverConstants.SHOT_ACCURACY_BASE;
            float shotSkill = (shooter.BaseData.ShootingAccuracy + shooter.BaseData.Technique) / 2f;
            float attrScore = Sigmoid((shotSkill - 50f) / 20f);
            float distVal = Vector3.Distance(ActionCalculatorUtils.GetPosition3D(shooter), new Vector3(targetGoalCenter2D.x, 1.2f, targetGoalCenter2D.y));
            float distFactor = Mathf.Exp(-distVal / ActionResolverConstants.PITCH_LENGTH);
            float defenderPressure = ActionCalculatorUtils.CalculatePressureOnPlayer(shooter, state);
            float pressFactor = 1f - Mathf.Clamp01(defenderPressure / ActionResolverConstants.MAX_PRESSURE_DIST);
            float fatigueFactor = (float)System.Math.Tanh(shooter.Stamina / 100f);
            float noiseSigma = 0.02f;
            float noise = (float)(state.RandomGenerator.NextDouble() * noiseSigma - noiseSigma / 2f);
            float accuracyChance = baseAcc * attrScore * distFactor * pressFactor * fatigueFactor + noise;

            // --- Fatigue Penalty (Stamina) ---
            float stamina = shooter.Stamina;
            float fatigueThreshold = 0.5f; // Penalty starts below 50% stamina
            float maxFatigueAccuracyPenalty = 0.4f; // Max 40% increase in angle deviation
            float maxFatigueSpeedPenalty = 0.2f; // Max 20% reduction in speed
            float fatiguePenaltyFactor = 0f;
            if (stamina < fatigueThreshold)
            {
                // Non-linear scaling: penalty increases quadratically as stamina drops below threshold
                fatiguePenaltyFactor = Mathf.Pow((fatigueThreshold - stamina) / fatigueThreshold, 2.0f);
            }

            // Apply fatigue penalty to accuracy (increase deviation)
            float fatigueAccuracyPenalty = fatiguePenaltyFactor * maxFatigueAccuracyPenalty;
            float maxAngleDeviation = ActionResolverConstants.SHOT_MAX_ANGLE_OFFSET_DEGREES * (1f - Mathf.Clamp01(accuracyChance)) * (1f + fatigueAccuracyPenalty);
            float horizontalAngleOffset = (float)state.RandomGenerator.NextDouble() * 2f * maxAngleDeviation - maxAngleDeviation;

            Vector3 shooterPos3D = ActionCalculatorUtils.GetPosition3D(shooter); // Utilise la position 3D réelle
            Vector3 targetGoalCenter3D = new Vector3(targetGoalCenter2D.x, 1.2f, targetGoalCenter2D.y);

            // Apply fatigue penalty to speed (decrease speed)
            float fatigueSpeedPenalty = fatiguePenaltyFactor * maxFatigueSpeedPenalty;
            float speed = ActionResolverConstants.SHOT_BASE_SPEED * Mathf.Lerp(0.8f, 1.2f, shooter.BaseData.ShootingPower / 100f) * staminaEffect * (1f - fatigueSpeedPenalty);
            Vector3 idealDirection3D = (targetGoalCenter3D - shooterPos3D).normalized;
            // --- Fin intégration attributs physiques/mentaux ---

            // Documentation attributs :
            // - ShootingAccuracy : modifie la précision de base
            // - Composure : réduit l'effet négatif de la pression
            // - Stamina : réduit la puissance et la précision si faible
            // - Determination & WorkRate : réduisent l'effet de la fatigue
            // - Strength : réduit l'impact physique de la pression
            // - Agility : réduit la perte de précision sur tir en mouvement
            // - Positioning : réduit la déviation de l'angle de tir

            if(idealDirection3D.sqrMagnitude < SimConstants.FLOAT_EPSILON * SimConstants.FLOAT_EPSILON) idealDirection3D = Vector3.forward;

            Quaternion horizontalRotation = Quaternion.AngleAxis(horizontalAngleOffset, Vector3.up);
            Vector3 actualDirection3D = horizontalRotation * idealDirection3D;

            float launchAngle = ActionResolverConstants.SHOT_BASE_LAUNCH_ANGLE_DEG + UnityEngine.Random.Range(-ActionResolverConstants.SHOT_LAUNCH_ANGLE_VARIANCE_DEG, ActionResolverConstants.SHOT_LAUNCH_ANGLE_VARIANCE_DEG);
            Vector3 horizontalAxis = Vector3.Cross(actualDirection3D, Vector3.up);
             // Ensure axis is valid before creating rotation
             Quaternion launchRotation = (horizontalAxis.sqrMagnitude > SimConstants.FLOAT_EPSILON)
                                    ? Quaternion.AngleAxis(launchAngle, horizontalAxis.normalized)
                                    : Quaternion.identity;
            actualDirection3D = launchRotation * actualDirection3D;

            Vector3 spinAxis = CalculateShotSpinAxis(shooter, horizontalAxis, actualDirection3D, state);
            float spinMagnitude = CalculateShotSpinMagnitude(shooter, state);
            Vector3 angularVelocity = spinAxis * spinMagnitude;

            state.Ball.SetLastShooter(shooter); // Record who took the shot
            state.Ball.ResetPassContext(); // Clear any previous pass info

            // Update shooter state: no longer has the ball
            shooter.HasBall = false; 
            state.Ball.SetHolder(null); // Ball is now in flight, not held

            state.Ball.ReleaseAsShot(shooter, actualDirection3D * speed, angularVelocity);

            // --- Block Logic Integration ---
            var shotContext = new ShotContext {
                ShotOrigin = shooterPos3D,
                ShotDirection = actualDirection3D,
                ShotSpeed = speed,
                ShotHeight = shooter.BaseData.Height,
                ShotAngle = horizontalAngleOffset,
                // ShotDeception : pourrait aussi être influencé par Technique ou Agility
                ShotDeception = Mathf.Lerp(shooter.BaseData.Blocking, shooter.BaseData.Technique, shooter.BaseData.Agility / 100f),
                ReleaseTime = Time.time // Or use a simulation time if available
            }; // Technique et Agility influencent la tromperie du tir

            var blockResult = _blockCalculator.TryBlockShot(shooter, state, shotContext);
            if (blockResult.Blocked || blockResult.Partial) {
                // Enhanced block outcome logic
                ActionResultOutcome outcome;
                string reason;
                SimPlayer possessionPlayer = null;
                switch (blockResult.OutcomeType) {
                    case BlockCalculator.BlockOutcomeType.CaughtByBlocker:
                        outcome = ActionResultOutcome.BlockedAndCaught;
                        possessionPlayer = blockResult.PossessionPlayer;
                        reason = "Shot blocked and caught by defender.";
                        break;
                    case BlockCalculator.BlockOutcomeType.ToTeammate:
                        outcome = ActionResultOutcome.BlockedToTeammate;
                        possessionPlayer = blockResult.PossessionPlayer;
                        reason = "Shot blocked to defender's teammate.";
                        break;
                    case BlockCalculator.BlockOutcomeType.OutOfBounds:
                        outcome = ActionResultOutcome.BlockedOutOfBounds;
                        possessionPlayer = null;
                        reason = "Shot blocked out of bounds.";
                        break;
                    case BlockCalculator.BlockOutcomeType.DeflectedLoose:
                        outcome = ActionResultOutcome.Deflected;
                        possessionPlayer = null;
                        reason = "Shot deflected and loose.";
                        break;
                    default:
                        outcome = blockResult.Blocked ? ActionResultOutcome.Blocked : ActionResultOutcome.Intercepted;
                        possessionPlayer = blockResult.PossessionPlayer;
                        reason = blockResult.Reason ?? "Shot blocked.";
                        break;
                }
                return new ActionResult {
                    Outcome = outcome,
                    PrimaryPlayer = shooter,
                    SecondaryPlayer = blockResult.Blocker,
                    PossessionPlayer = possessionPlayer,
                    Reason = reason
                };
            }
            // Delegate GK save resolution to SaveResolver
            return SaveResolver.ResolveSaveAttempt(shooter, actualDirection3D, speed, shooter.BaseData.Height, state);
        }

        // --- New Method for Penalty Resolution ---
        private ActionResult ResolvePenaltyShot(SimPlayer shooter, MatchState state)
        {
            if (shooter == null || state == null || shooter.BaseData == null)
            {
                return new ActionResult { Outcome = ActionResultOutcome.Failure, PrimaryPlayer = shooter, Reason = "Invalid input for penalty resolution." };
            }

            SimPlayer goalkeeper = state.GetGoalkeeper(1 - shooter.TeamSimId);
            if (goalkeeper == null || goalkeeper.BaseData == null)
            {
                // No GK? Automatic goal (shouldn't happen in penalty phase setup)
                Debug.LogWarning("[Penalty] Goalkeeper not found during penalty resolution.");
                // TODO: Decide how to handle ball state for automatic goal
                // For now, return a success placeholder
                return new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = shooter, Reason = "Penalty Goal (No Goalkeeper)" };
            }

            // 1. Get Shooter's Aim and GK's Dive Target (from their PlannedAction data)
            //    We assume PlayerAIController set these correctly.
            Vector2 shotTarget2D = shooter.TargetPosition; // The point on the goal line the shooter aimed at.
            Vector2 diveTarget2D = goalkeeper.TargetPosition; // The point on the goal line the GK committed to.

            // 2. Define Goal Zones (Simplified: Left, Center, Right)
            float goalCenterY = ActionResolverConstants.PITCH_CENTER_Y;
            float zoneWidth = ActionResolverConstants.GOAL_WIDTH / 3f;
            float leftZoneBoundary = goalCenterY - zoneWidth / 2f;
            float rightZoneBoundary = goalCenterY + zoneWidth / 2f;

            GoalZone shotZone = GetGoalZone(shotTarget2D.y, leftZoneBoundary, rightZoneBoundary);
            GoalZone diveZone = GetGoalZone(diveTarget2D.y, leftZoneBoundary, rightZoneBoundary);

            // 3. Calculate Base Save Probability (GK Dive vs Shot Placement)
            float baseSaveProbability = 0.1f; // Base chance even if GK guesses wrong
            if (shotZone == diveZone)
            {
                baseSaveProbability = 0.75f; // High chance if GK dives correctly
            }
            else if ((shotZone == GoalZone.Left && diveZone == GoalZone.Center) ||
                     (shotZone == GoalZone.Right && diveZone == GoalZone.Center) ||
                     (shotZone == GoalZone.Center && diveZone != GoalZone.Center))
            {
                baseSaveProbability = 0.35f; // Medium chance if GK dives center or shooter aims center
            }

            // 4. Factor in Player Attributes
            // Shooter Attributes (Accuracy, Composure influence *chance to hit target zone accurately*)
            float shooterAccuracyFactor = Mathf.Clamp01(shooter.BaseData.ShootingAccuracy / 100f);
            float shooterComposureFactor = Mathf.Clamp01(shooter.BaseData.Composure / 100f);
            // Combine them (e.g., weighted average or multiplicative)
            float shooterPerformance = Mathf.Lerp(0.5f, 1.0f, (shooterAccuracyFactor + shooterComposureFactor) / 2f);

            // GK Attributes (PenaltySaving, Reflexes influence *save chance within the chosen dive zone*)
            float gkSavingFactor = Mathf.Clamp01(goalkeeper.BaseData.PenaltySaving / 100f);
            float gkReflexFactor = Mathf.Clamp01(goalkeeper.BaseData.Reflexes / 100f);
            // Combine them
            float gkPerformance = Mathf.Lerp(0.5f, 1.0f, (gkSavingFactor + gkReflexFactor) / 2f);

            // Adjust save probability based on attributes
            // If GK guessed right, GK performance increases save chance significantly
            // If GK guessed wrong, GK performance has less impact
            // Shooter performance slightly reduces save chance (better shot is harder to save)
            float finalSaveProbability = baseSaveProbability;
            if (shotZone == diveZone)
            {
                 finalSaveProbability = Mathf.Lerp(finalSaveProbability, 1.0f, gkPerformance * 0.5f); // Stronger GK influence
                 finalSaveProbability *= (1.0f - shooterPerformance * 0.2f); // Shooter reduces chance slightly
            }
            else
            {
                 finalSaveProbability = Mathf.Lerp(0f, finalSaveProbability, gkPerformance * 0.3f); // Weaker GK influence
                 finalSaveProbability *= (1.0f - shooterPerformance * 0.1f);
            }

             finalSaveProbability = Mathf.Clamp01(finalSaveProbability);

            // 5. Determine Outcome
            bool saved = state.RandomGenerator.NextDouble() < finalSaveProbability;

            // TODO: Add logic for hitting post/missing wide based on accuracy?
            // For now, simplify: if not saved, it's a goal.

            if (saved)
            {
                Debug.Log($"[Penalty Result] SAVE! Shooter: {shooter.GetPlayerId()}, GK: {goalkeeper.GetPlayerId()}, ShotZone: {shotZone}, DiveZone: {diveZone}, Prob: {finalSaveProbability:P2}");
                // TODO: Set ball state correctly for a save (e.g., GK possession or rebound)
                shooter.HasBall = false; // Shooter no longer has the ball
                state.Ball.SetPossession(goalkeeper); // Give possession to goalkeeper
                state.Ball.Velocity = Vector3.zero;
                state.Ball.Position = goalkeeper.Position + Vector2.right * (goalkeeper.TeamSimId == 0 ? -0.5f : 0.5f); // Place near GK
                return new ActionResult
                {
                    Outcome = ActionResultOutcome.Saved,
                    PrimaryPlayer = shooter,
                    SecondaryPlayer = goalkeeper,
                    PossessionPlayer = goalkeeper,
                    Reason = "Penalty saved by goalkeeper."
                };
            }
            else
            {
                Debug.Log($"[Penalty Result] GOAL! Shooter: {shooter.GetPlayerId()}, GK: {goalkeeper.GetPlayerId()}, ShotZone: {shotZone}, DiveZone: {diveZone}, Prob: {finalSaveProbability:P2}");
                // TODO: Set ball state for a goal
                shooter.HasBall = false; // Shooter no longer has the ball
                state.Ball.SetPossession(null); // No possession after goal
                // ActionResolver/EventHandler usually handles goal event triggering phase change
                return new ActionResult
                {
                    Outcome = ActionResultOutcome.Goal,
                    PrimaryPlayer = shooter,
                    SecondaryPlayer = goalkeeper,
                    Reason = "Penalty scored."
                };
            }
        }

        private enum GoalZone { Left, Center, Right }

        private GoalZone GetGoalZone(float yPos, float leftBoundary, float rightBoundary)
        {
            if (yPos < leftBoundary) return GoalZone.Left;
            if (yPos > rightBoundary) return GoalZone.Right;
            return GoalZone.Center;
        }

        // --- End of Penalty Resolution Method ---


        private const float DOMINANT_HAND_PROBABILITY = 0.7f;
        private const float MIN_SPIN_VARIANCE = 0.8f;
        private const float MAX_SPIN_VARIANCE = 1.2f;

        private Vector3 CalculateShotSpinAxis(SimPlayer shooter, Vector3 horizontalAxis, Vector3 shotDirection, MatchState state)
        {
             if (shooter?.BaseData == null) return horizontalAxis; // Default if no data

             Vector3 defaultSpinAxis = horizontalAxis;
             float techniqueInfluence = shooter.BaseData.Technique / 100f;
             bool isRightDominant = state.RandomGenerator.NextDouble() < DOMINANT_HAND_PROBABILITY;
             float sideSpinFactor = techniqueInfluence * (isRightDominant ? 1f : -1f) * UnityEngine.Random.Range(0.5f, 1.0f);

             Vector3 spinAxis = Vector3.Lerp(
                 defaultSpinAxis,
                 new Vector3(0, sideSpinFactor, 0), // Simple side spin component
                 techniqueInfluence * ActionResolverConstants.SHOT_TECHNIQUE_SPIN_FACTOR
             );

              // Normalize only if the vector is not near zero
             if (spinAxis.sqrMagnitude > SimConstants.FLOAT_EPSILON) {
                 return spinAxis.normalized;
             } else {
                 return horizontalAxis; // Fallback to horizontal axis
             }
        }


        private float CalculateShotSpinMagnitude(SimPlayer shooter, MatchState state)
        {
             if (shooter?.BaseData == null) return 0f;

             float techniqueInfluence = shooter.BaseData.Technique / 100f;
             float powerInfluence = shooter.BaseData.ShootingPower / 100f;

             // Non-linear: Use power curve for technique and sigmoid for power
            float techniqueNonLinear = Mathf.Pow(techniqueInfluence, 1.2f);
            float powerNonLinear = Sigmoid((shooter.BaseData.ShootingPower - 50f) / 20f);
            float spinMagnitude = ActionResolverConstants.SHOT_MAX_SPIN_MAGNITUDE *
                                 Mathf.Lerp(0.3f, 1.0f, techniqueNonLinear) *
                                 Mathf.Lerp(0.7f, 1.1f, powerNonLinear) *
                                 ActionResolverConstants.SHOT_TYPE_SPIN_FACTOR;

             spinMagnitude *= (float)state.RandomGenerator.NextDouble() * (MAX_SPIN_VARIANCE - MIN_SPIN_VARIANCE) + MIN_SPIN_VARIANCE;
             return spinMagnitude;
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