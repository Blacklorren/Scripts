using UnityEngine;
using System;
using HandballManager.Core;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.Events.Interfaces;

namespace HandballManager.Simulation.Physics
{
    /// <summary>
    /// Handles player acceleration, deceleration, inertia, and turn logic.
    /// </summary>
    public class PlayerPhysicsEngine
    {

        // Movement and Physics Constants
        private const float PLAYER_ACCELERATION_BASE = 15.0f;  // Base acceleration m/s^2
        private const float PLAYER_DECELERATION_BASE = 20.0f;  // Base deceleration m/s^2
        private const float PLAYER_NEAR_STOP_VELOCITY_THRESHOLD = 0.5f;  // Speed below which accel limit is always used
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f;  // Minimum squared distance for movement checks
        private const float PLAYER_MAX_SPEED_OVERSHOOT_FACTOR = 1.01f;  // Allowed overshoot before clamping
        private const float JUMP_RECOVERY_DURATION = 0.3f; // Duration (in seconds) after landing before full control is restored

        // Attribute Modifiers
        private const float PLAYER_AGILITY_MOD_MIN = 0.8f;  // Effect of 0 Agility on accel/decel
        private const float PLAYER_AGILITY_MOD_MAX = 1.2f;  // Effect of 100 Agility on accel/decel
       
        // Stamina Constants
        private const float STAMINA_DRAIN_BASE = MatchSimulator.BASE_STAMINA_DRAIN_PER_SECOND;
        private const float STAMINA_SPRINT_MULTIPLIER = MatchSimulator.SPRINT_STAMINA_MULTIPLIER;
        private const float STAMINA_RECOVERY_RATE = 0.003f;
        private const float NATURAL_FITNESS_RECOVERY_MOD = 0.2f; // +/- 20% effect on recovery rate based on 0-100 NF (0 = 0.8x, 100 = 1.2x)
        private const float STAMINA_ATTRIBUTE_DRAIN_MOD = 0.3f; // +/- 30% effect on drain rate based on 0-100 Stamina (0=1.3x, 100=0.7x)
        private const float SPRINT_MIN_EFFORT_THRESHOLD = 0.85f; // % of BASE max speed considered sprinting
        private const float SIGNIFICANT_MOVEMENT_EFFORT_THRESHOLD = 0.2f; // % of BASE max speed considered 'moving' for stamina drain

        // Sprinting / Arrival Constants
        private const float SPRINT_MIN_DISTANCE = 3.0f;
        private const float SPRINT_MIN_STAMINA = 0.3f;
        private const float SPRINT_TARGET_SPEED_FACTOR = 0.6f; // Must be trying to move faster than this % of effective speed to sprint
        private const float NON_SPRINT_SPEED_CAP_FACTOR = 0.85f; // % cap on effective speed when not sprinting
        private const float ARRIVAL_SLOWDOWN_RADIUS = 1.5f;
        private const float ARRIVAL_SLOWDOWN_MIN_DIST = 0.05f; // Min distance for slowdown logic to apply

        private readonly IGeometryProvider _geometry;
        private readonly IMatchEventHandler _eventHandler;
        private readonly StaminaManager _staminaManager;
        public delegate void TurnoverHandler(SimPlayer player, MatchState state, string reason);
        private readonly TurnoverHandler _turnoverHandler;
        private readonly ITacticPositioner _tacticPositioner;

        // Delegate for notifying attacking intent
        private Action _attackingIntentHandler;

        /// <summary>
        /// Handles player acceleration, deceleration, and inertia.
        /// </summary>
        public PlayerPhysicsEngine(TurnoverHandler turnoverHandler, IGeometryProvider geometry = null, IMatchEventHandler eventHandler = null, StaminaManager staminaManager = null, ITacticPositioner tacticPositioner = null, Action attackingIntentHandler = null)
        {
            _turnoverHandler = turnoverHandler;
            _attackingIntentHandler = attackingIntentHandler;
            _geometry = geometry;
            _eventHandler = eventHandler;
            _staminaManager = staminaManager;
            _tacticPositioner = tacticPositioner;
        }

        /// <summary>
        /// Returns true if position is inside the goal area for a team.
        /// </summary>
        private static bool IsInGoalArea(Vector2 position, int teamId, IGeometryProvider geometry)
        {
            Vector2 goalCenter = geometry.GetGoalCenter(teamId == 0 ? 1 : 0);
            float radius = geometry.GoalAreaRadius;
            return Vector2.Distance(position, goalCenter) < radius;
        }

        /// <summary>
        /// Returns true if position is within the avoidance buffer outside the goal area
        /// </summary>
        private static bool IsInGoalAreaBuffer(Vector2 position, int teamId, IGeometryProvider geometry)
        {
            Vector2 goalCenter = geometry.GetGoalCenter(teamId == 0 ? 1 : 0);
            float buffer = geometry.GoalAreaRadius + 0.7f; // 0.7m buffer (adjustable)
            return Vector2.Distance(position, goalCenter) < buffer && !IsInGoalArea(position, teamId, geometry);
        }

        /// <summary>
        /// Returns true if this player is the goalkeeper.
        /// </summary>
        private static bool IsGoalkeeper(SimPlayer player)
        {
            return player.AssignedTacticalRole == PlayerPosition.Goalkeeper;
        }

        /// <summary>
        /// Returns true if the player is allowed to enter the zone in current context.
        /// Only allowed if the player is in the air and the jump originated from outside the zone.
        /// </summary>
        private static bool IsAllowedZoneEntryContext(SimPlayer player, MatchState state, bool isAttacking, bool isDefending)
        {
            return player.CurrentAction == PlayerAction.JumpingForShot && player.JumpOriginatedOutsideGoalArea;
        }

        /// <summary>
        /// Redirects player velocity tangentially around the goal area.
        /// </summary>
        private void RedirectAroundGoalArea(SimPlayer player, int teamId)
        {
            Vector2 goalCenter = _geometry.GetGoalCenter(teamId == 0 ? 1 : 0);
            Vector2 fromGoal = (player.Position - goalCenter).normalized;
            Vector2 tangent = new Vector2(-fromGoal.y, fromGoal.x);
            if (Vector2.Dot(tangent, player.Velocity) < 0)
                tangent = -tangent;
            player.Velocity = tangent * player.Velocity.magnitude;
            player.Velocity += new Vector3(fromGoal.x, 0f, fromGoal.y) * player.Velocity.magnitude * 0.2f;
        }

        public void ApplyAcceleration(SimPlayer player, Vector2 targetVelocity, bool allowSprint, bool applyArrivalSlowdown, float deltaTime)
        {
            if (player?.BaseData == null) return;

            Vector2 currentVelocity = player.Velocity;
            float currentSpeed = currentVelocity.magnitude;
            Vector2 directionToTarget = (player.TargetPosition - player.Position);
            float distanceToTarget = directionToTarget.magnitude;

            bool isSprinting = allowSprint &&
                              targetVelocity.sqrMagnitude > (player.EffectiveSpeed * SPRINT_TARGET_SPEED_FACTOR) * (player.EffectiveSpeed * SPRINT_TARGET_SPEED_FACTOR) &&
                              distanceToTarget > SPRINT_MIN_DISTANCE &&
                              player.Stamina > SPRINT_MIN_STAMINA;

            float finalTargetSpeed = targetVelocity.magnitude;

            if (isSprinting)
            {
                finalTargetSpeed = player.EffectiveSpeed;
            }
            else
            {
                finalTargetSpeed = Mathf.Min(finalTargetSpeed, player.EffectiveSpeed * NON_SPRINT_SPEED_CAP_FACTOR);
            }

            if (applyArrivalSlowdown && distanceToTarget < ARRIVAL_SLOWDOWN_RADIUS && distanceToTarget > ARRIVAL_SLOWDOWN_MIN_DIST)
            {
                finalTargetSpeed *= Mathf.Sqrt(Mathf.Clamp01(distanceToTarget / ARRIVAL_SLOWDOWN_RADIUS));
            }

            Vector2 finalTargetVelocity = (finalTargetSpeed > 0.01f) ? targetVelocity.normalized * finalTargetSpeed : Vector2.zero;
            Vector2 requiredAcceleration = (finalTargetVelocity - currentVelocity) / deltaTime;

            // Agility affects acceleration/deceleration
            float agilityFactor = Mathf.Lerp(PLAYER_AGILITY_MOD_MIN, PLAYER_AGILITY_MOD_MAX, (player.BaseData?.Agility ?? 50f) / 100f); // Modified by BaseData.Agility
            // Strength affects resistance to deceleration (higher = less slowdown from collisions)
            float strengthFactor = Mathf.Lerp(0.9f, 1.1f, (player.BaseData?.Strength ?? 50f) / 100f); // Modified by BaseData.Strength
            float maxAccel = PLAYER_ACCELERATION_BASE * agilityFactor * strengthFactor;
            float maxDecel = PLAYER_DECELERATION_BASE * agilityFactor / strengthFactor; // Stronger = less deceleration

            // Speed directly affects effective speed
            // Stamina penalty: effective speed drops up to 30% at zero stamina
            float baseSpeed = Mathf.Lerp(4.0f, 7.5f, (player.BaseData?.Speed ?? 50f) / 100f);
            player.EffectiveSpeed = baseSpeed * Mathf.Lerp(0.7f, 1.0f, player.Stamina);

            // WorkRate increases willingness to sprint (lowers sprint threshold) and increases stamina drain
            float workRateFactor = Mathf.Lerp(1.0f, 1.2f, (player.BaseData?.WorkRate ?? 50f) / 100f); // Modified by BaseData.WorkRate

            // Stamina affects stamina drain (higher stamina = less drain)
            float staminaDrainMod = Mathf.Lerp(1.3f, 0.7f, (player.BaseData?.Stamina ?? 50f) / 100f); // Modified by BaseData.Stamina

            // NaturalFitness affects stamina recovery rate
            float naturalFitnessRecoveryMod = Mathf.Lerp(0.8f, 1.2f, (player.BaseData?.NaturalFitness ?? 50f) / 100f); // Modified by BaseData.NaturalFitness

            // Resilience affects fatigue resistance (less fatigue from physical actions)
            float resilienceFatigueMod = Mathf.Lerp(1.2f, 0.8f, (player.BaseData?.Resilience ?? 50f) / 100f); // Modified by BaseData.Resilience

            // Jumping affects jump height (used in jump logic elsewhere)
            // See jump mechanics for usage of BaseData.Jumping


            bool isAccelerating = Vector2.Dot(requiredAcceleration, currentVelocity.normalized) > -0.1f || currentSpeed < PLAYER_NEAR_STOP_VELOCITY_THRESHOLD;
            float maxAccelerationMagnitude = isAccelerating ? maxAccel : maxDecel;

            Vector2 appliedAcceleration = Vector2.ClampMagnitude(requiredAcceleration, maxAccelerationMagnitude);
            if (player.IsStumbling)
                appliedAcceleration *= SimConstants.STUMBLE_ACCELERATION_FACTOR;
            player.Velocity += new Vector3(appliedAcceleration.x, 0, appliedAcceleration.y) * deltaTime;

            // Clamp final velocity to prevent excessive speed
            float maxAllowedSpeed = player.EffectiveSpeed * PLAYER_MAX_SPEED_OVERSHOOT_FACTOR;
            if (player.Velocity.sqrMagnitude > maxAllowedSpeed * maxAllowedSpeed)
            {
                player.Velocity = player.Velocity.normalized * maxAllowedSpeed;
            }
            if (player.IsStumbling)
                player.Velocity *= SimConstants.STUMBLE_SPEED_FACTOR;
        }

        /// <summary>
        /// Calculates the target velocity for a player's current action.
        /// </summary>
        public Vector2 CalculateActionTargetVelocity(SimPlayer player, out bool allowSprint, out bool applyArrivalSlowdown)
        {
            Vector2 targetVelocity = Vector2.zero;
            allowSprint = false;
            applyArrivalSlowdown = true;

            if (player?.BaseData == null) return targetVelocity;

            switch (player.CurrentAction)
            {
                case PlayerAction.MovingToPosition:
                case PlayerAction.MovingWithBall:
                case PlayerAction.DefendingPlayer:
                case PlayerAction.ReceivingPass:
                case PlayerAction.Intercepting:
                case PlayerAction.Blocking:
                // case PlayerAction.GoalkeeperPositioning: // Removed: not present in PlayerAction enum
                    Vector2 direction = (player.TargetPosition - player.Position);
                    if (direction.sqrMagnitude > MIN_DISTANCE_CHECK_SQ)
                    {
                        targetVelocity = direction.normalized * player.EffectiveSpeed;
                    }
                    allowSprint = player.AssignedTacticalRole != PlayerPosition.Goalkeeper && player.CurrentAction != PlayerAction.Blocking;
                    break;

                case PlayerAction.PreparingPass:
                case PlayerAction.PreparingShot:
                case PlayerAction.Tackling:
                    targetVelocity = Vector2.zero;
                    applyArrivalSlowdown = false;
                    break;

                default:
                    targetVelocity = Vector2.zero;
                    applyArrivalSlowdown = false;
                    break;
            }
            return targetVelocity;
        }

        /// <summary>
        /// Updates movement for all players on the court.
        /// </summary>
        public void UpdatePlayersMovement(MatchState state, float deltaTime)
        {
            // --- Attacking intent detection ---
            bool attackingIntentDetected = false;
            Vector2 opponentGoal = _geometry.GetGoalCenter(state.PossessionTeamId == 0 ? 1 : 0);
            foreach (var player in state.PlayersOnCourt)
            {
                if (player == null || player.SuspensionTimer > 0) continue;
                // Only consider attacking players
                if (player.TeamSimId == state.PossessionTeamId)
                {
                    // Player must be moving significantly
                    if (player.Velocity.magnitude > 1.0f) // 1 m/s threshold for 'clear' movement
                    {
                        Vector2 toGoal = (opponentGoal - player.Position).normalized;
                        float dot = Vector2.Dot(player.Velocity.normalized, toGoal);
                        if (dot > 0.7f) // Moving mostly towards goal (within ~45 degrees)
                        {
                            attackingIntentDetected = true;
                            break; // Only need one per frame
                        }
                    }
                }
            }
            
            // Notify the PassivePlayManager if attacking intent is detected (via delegate)
            if (attackingIntentDetected && _attackingIntentHandler != null)
            {
                _attackingIntentHandler.Invoke();
            }

            if(state.PlayersOnCourt == null) return;

            const float STEP_THRESHOLD = 0.25f; // meters, adjust as needed
            foreach (var player in state.PlayersOnCourt)
            {
                if (player == null || player.SuspensionTimer > 0) continue;

                player.UpdateJumpRecovery(deltaTime);
                player.UpdateStumble(deltaTime);

                // Get tactical target position
                Vector2 tacticalTargetPosition = _tacticPositioner.GetPlayerTargetPosition(state, player);
                player.TargetPosition = tacticalTargetPosition; // Set the target for physics calculations (like arrival slowdown)

                Vector2 previousPosition = player.Position;
                // Calculate desired velocity based on current action/state (might override tactical positioning temporarily)
                Vector2 targetVelocity = CalculateActionTargetVelocity(player, out bool allowSprint, out bool applyArrivalSlowdown);
                ApplyAcceleration(player, targetVelocity, allowSprint, applyArrivalSlowdown, deltaTime);
                player.Position += new Vector2(player.Velocity.x, player.Velocity.y) * deltaTime;

                // --- Stamina System: Update stamina based on movement ---
                // --- Stamina System: Update stamina based on match progression ---
                float matchProgress = Mathf.Clamp01(state.MatchTimeSeconds / Mathf.Max(1f, state.MatchDurationSeconds));
                float accumulationMultiplier = Mathf.Lerp(1.0f, 2.0f, matchProgress); // up to 2x at end
                float recoveryMultiplier = Mathf.Lerp(1.0f, 0.5f, matchProgress); // down to 0.5x at end

                Debug.Log($"[StaminaMult] Time: {state.MatchTimeSeconds:F1}/{state.MatchDurationSeconds:F1} ({matchProgress:P1}) | Accum: {accumulationMultiplier:F2}, Recov: {recoveryMultiplier:F2}");
                // Update player stamina - method only takes deltaTime parameter
                player.UpdateStamina(deltaTime);

                // --- Goal Area Violation Enforcement (Handball 6m Zone Rules) ---
                bool inGoalArea = IsInGoalArea(player.Position, player.TeamSimId, _geometry);
                bool inGoalAreaBuffer = IsInGoalAreaBuffer(player.Position, player.TeamSimId, _geometry);
                bool isGoalkeeper = IsGoalkeeper(player);
                int defendingTeamId = (player.Position.x < (_geometry.PitchLength / 2f)) ? 0 : 1;
                bool isAttacking = player.TeamSimId != defendingTeamId;
                bool isDefending = !isAttacking;

                // Proactive avoidance: steer clear of the 6m zone (with buffer)
                if (!isGoalkeeper && inGoalAreaBuffer && !IsAllowedZoneEntryContext(player, state, isAttacking, isDefending))
                {
                    RedirectAroundGoalArea(player, player.TeamSimId);
                }

                // Allow entry in special contexts, but trigger violation if player ends up in zone after context ends
                if (inGoalArea && !isGoalkeeper)
                {
                    if (isAttacking)
                    {
                        // If not in shot/jump context, turnover
                        if (!IsAllowedZoneEntryContext(player, state, true, false))
                        {
                            _turnoverHandler?.Invoke(player, state, "Attacker entered 6m zone");
                        }
                    }
                    else // defending
                    {
                        // If not 'pushed' by collision, penalty
                        if (!IsAllowedZoneEntryContext(player, state, false, true))
                        {
                            // Defender entered 6m zone: always penalty throw per updated rules
                            if (_eventHandler != null && state != null)
                            {
                                _eventHandler.HandleActionResult(new ActionResult
                                {
                                    Outcome = ActionResultOutcome.FoulCommitted,
                                    PrimaryPlayer = player,
                                    FoulSeverity = FoulSeverity.PenaltyThrow,
                                    Reason = "Defender entered 6m zone"
                                }, state);
                            }
                        }
                    }
                }

                // --- Aerial Play Violation: Attacker lands in 6m zone with ball ---
                // Detect landing event
                bool wasJumping = player.JumpOrigin != null;
                bool isJumping = player.CurrentAction == PlayerAction.JumpingForShot;
                if (isAttacking && wasJumping && !isJumping && player.JumpOriginatedOutsideGoalArea && inGoalArea && player.HasBall)
                {
                    // Attacker landed in 6m zone with ball after jump: turnover
                    _turnoverHandler?.Invoke(player, state, "Attacker landed in 6m zone with ball after jump");
                }

                _staminaManager?.ApplyStaminaEffects(player, deltaTime);

                // Step and Dribble tracking logic
                if (player.BaseData is HandballManager.Data.PlayerData pdata)
                {
                    // --- Double Dribble Detection ---
                    // If player attempts to start dribbling again after already dribbling since possession
                    if (player.HasBall && player.IsDribbling) // You may need to replace WantsToDribble with the actual intent flag
                    {
                        if (player.IsDribbling)
                        {
                            // Already dribbling, continue
                        }
                        else if (player.HasDribbledSincePossession) // Use SimPlayer instance
                        {
                            // Double dribble detected
                            _turnoverHandler?.Invoke(player, state, "Double Dribble");
                        }
                        else
                        {
                            player.StartDribble(); // Call on SimPlayer, not PlayerData
                        }
                    }
                    // --- Step Counting ---
                    if (player.HasBall && !player.IsDribbling)
                    {
                        player.TryIncrementStep(player.Position); // Use SimPlayer instance
                        if (player.ExceededStepLimit()) // Use SimPlayer instance
                        {
                            Debug.Log($"[Violation] Steps violation by Player ID: {player.BaseData.PlayerID}");
                            _turnoverHandler?.Invoke(player, state, "Step Violation");
                        }
                    }
                    // Possession state is already handled by SimPlayer.HasBall
                    // No need to update PlayerData separately as these methods don't exist there
                    // The SimPlayer object already tracks possession state

                    // --- Jumping Mechanics Update ---
                    // Track jump state transitions for zone logic
                    // (wasJumping, isJumping, isLanding already computed above)

                    // Detect jump start
                    if (!wasJumping && isJumping)
                    {
                        player.JumpOrigin = player.Position;
                        player.JumpOriginatedOutsideGoalArea = !IsInGoalArea(player.Position, player.TeamSimId, _geometry);
                    }
                    // Detect landing or jump end
                    else if (wasJumping && !isJumping)
                    {
                        player.JumpOrigin = null;
                        player.JumpOriginatedOutsideGoalArea = false;
                        // Player just landed from a jump
                        player.StartJumpRecovery(JUMP_RECOVERY_DURATION);
                        // (Other landing logic can be added here)
                    }

                    // Use JumpSimulator for jump arc updates
                }
            }
        }

        /// <summary>
        /// Handles special movement situations (e.g., set pieces).
        /// </summary>
        public void HandleSpecialMovement(MatchState state, GameSituationType situationType)
        {
            if (state == null) return;

            // Handle different special movement situations via TacticPositioner
            // Note: Ensure _tacticPositioner is constructed/injected appropriately
            switch (situationType)
            {
                case GameSituationType.FreeThrow:
                    _tacticPositioner.PositionForFreeThrow(state);
                    break;

                case GameSituationType.Penalty:
                    _tacticPositioner.PositionForPenalty(state);
                    break;

                case GameSituationType.KickOff:
                    _tacticPositioner.PositionForKickOff(state);
                    break;

                case GameSituationType.ThrowIn:
                    _tacticPositioner.PositionForThrowIn(state);
                    break;

                case GameSituationType.GoalThrow:
                    _tacticPositioner.PositionForGoalThrow(state);
                    break;

                    // Add other cases as needed
            }
        }
    }
}
