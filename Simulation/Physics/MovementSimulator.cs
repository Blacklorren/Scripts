using UnityEngine;
using System;
using HandballManager.Simulation.Utils;
using System.Linq;
using HandballManager.Core;
using HandballManager.Simulation.Events;
using HandballManager.Data;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.Events.Interfaces;
using System.Collections.Generic;

namespace HandballManager.Simulation.Physics
{
    public partial class MovementSimulator : IMovementSimulator
    {
        private readonly ITacticPositioner _tacticPositioner;
        // --- Spatial Partitioning Fields ---
        private SpatialGrid _spatialGrid;
        private float _spatialGridCellSize = -1f;
        private float _spatialGridWidth = -1f;
        private float _spatialGridHeight = -1f;

        private readonly IGeometryProvider _geometry;
        private readonly IMatchEventHandler _eventHandler;

        public MovementSimulator(IGeometryProvider geometry, IMatchEventHandler eventHandler, ITacticPositioner tacticPositioner)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _tacticPositioner = tacticPositioner ?? throw new ArgumentNullException(nameof(tacticPositioner));
        }

        // Movement and Physics Constants
        private const float PLAYER_ACCELERATION_BASE = 15.0f;  // Base acceleration m/s^2
        private const float PLAYER_DECELERATION_BASE = 20.0f;  // Base deceleration m/s^2
        private const float PLAYER_NEAR_STOP_VELOCITY_THRESHOLD = 0.5f;  // Speed below which accel limit is always used
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f;  // Minimum squared distance for movement checks
        private const float PLAYER_MAX_SPEED_OVERSHOOT_FACTOR = 1.01f;  // Allowed overshoot before clamping

        // Attribute Modifiers
        private const float PLAYER_AGILITY_MOD_MIN = 0.8f;  // Effect of 0 Agility on accel/decel
        private const float PLAYER_AGILITY_MOD_MAX = 1.2f;  // Effect of 100 Agility on accel/decel

        // Boundary and Spacing Constants
        private const float SIDELINE_BUFFER = 0.5f;  // Buffer from sidelines for player and ball positions

        // Collision Constants
        private const float PLAYER_COLLISION_RADIUS = 0.4f;
        private const float PLAYER_COLLISION_DIAMETER = PLAYER_COLLISION_RADIUS * 2f;
        private const float PLAYER_COLLISION_DIAMETER_SQ = PLAYER_COLLISION_DIAMETER * PLAYER_COLLISION_DIAMETER;
        private const float COLLISION_RESPONSE_FACTOR = 0.5f;  // How strongly players push apart
        private const float COLLISION_MIN_DIST_SQ_CHECK = 0.0001f;  // Lower bound for collision distance check

        // Team Spacing Constants
        private const float MIN_SPACING_DISTANCE = 2.0f;  // How close teammates can get before spacing push
        private const float MIN_SPACING_DISTANCE_SQ = MIN_SPACING_DISTANCE * MIN_SPACING_DISTANCE;
        private const float SPACING_PUSH_FACTOR = 0.4f;
        private const float SPACING_PROXIMITY_POWER = 2.0f;  // Power for spacing push magnitude (higher = stronger when very close)

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
        
        /// <summary>
        /// Main update entry point called by MatchSimulator. Updates ball and player movement, handles collisions.
        /// </summary>
        public void UpdateMovement(MatchState state, float deltaTime)
        {
            // Safety check for essential state
            if (state == null || state.Ball == null) {
                Debug.LogError("[MovementSimulator] UpdateMovement called with null state or ball.");
                return;
            }

            UpdateBallMovement(state, deltaTime);
            UpdatePlayersMovement(state, deltaTime);
            HandleCollisionsAndBoundaries(state); // Handles player-player, spacing, and boundary clamping
        }

        /// <summary>
        /// Updates the ball's position and velocity based on physics simulation.
        /// Implements the IMovementSimulator interface method.
        /// </summary>
        /// <param name="state">The current match state containing the ball data.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when state is null.</exception>
        public void UpdateBallPhysics(MatchState state, float timeStep)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state), "Match state cannot be null when updating ball physics.");
            }

            if (state.Ball == null)
            {
                Debug.LogError("[MovementSimulator] UpdateBallPhysics called with null ball in state.");
                return;
            }

            // Delegate to the existing ball movement logic
            UpdateBallMovement(state, timeStep);
        }

        /// <summary>
        /// Updates the ball's 3D position and velocity based on physics.
        /// Applies gravity, air resistance, Magnus effect, and handles ground interactions.
        /// </summary>
        private void UpdateBallMovement(MatchState state, float deltaTime)
        {
            SimBall ball = state.Ball;

            if (ball.Holder != null)
            {
                // Ball stays attached to the holder on the ground plane
                Vector2 playerPos2D = ball.Holder.Position;
                Vector2 offsetDir2D = Vector2.right * (ball.Holder.TeamSimId == 0 ? 1f : -1f);
                if (ball.Holder.Velocity.sqrMagnitude > SimConstants.VELOCITY_NEAR_ZERO_SQ) {
                    offsetDir2D = ball.Holder.Velocity.normalized;
                }
                Vector2 ballPos2D = playerPos2D + offsetDir2D * SimConstants.BALL_OFFSET_FROM_HOLDER;
                ball.Position = new Vector3(ballPos2D.x, SimConstants.BALL_DEFAULT_HEIGHT, ballPos2D.y); // y → z mapping for 3D space
                ball.Velocity = Vector3.zero;
                ball.AngularVelocity = Vector3.zero;
            }
            else if (ball.IsInFlight)
            {
                // Apply physics simulation for ball in flight
                SimulateBallFlight(ball, deltaTime);
            }
            else if (ball.IsRolling)
            {
                // Handle rolling physics
                SimulateBallRolling(ball, deltaTime);
            }
        }

        private void SimulateBallFlight(SimBall ball, float deltaTime)
        {
            // --- Apply Forces (Air Resistance, Magnus, Gravity) ---
            Vector3 force = Vector3.zero;
            float speed = ball.Velocity.magnitude;

            // 1. Gravity
            force += SimConstants.GRAVITY * SimConstants.BALL_MASS;

            if (speed > SimConstants.FLOAT_EPSILON)
            {
                // 2. Air Resistance (Drag)
                float dragMagnitude = 0.5f * SimConstants.AIR_DENSITY * speed * speed * 
                                     SimConstants.DRAG_COEFFICIENT * SimConstants.BALL_CROSS_SECTIONAL_AREA;
                force += -ball.Velocity.normalized * dragMagnitude;

                // 3. Magnus Effect
                Vector3 magnusForce = SimConstants.MAGNUS_COEFFICIENT_SIMPLE * 
                                     Vector3.Cross(ball.AngularVelocity, ball.Velocity);
                force += magnusForce;
            }

            // Update physics
            ball.AngularVelocity *= Mathf.Pow(SimConstants.SPIN_DECAY_FACTOR, deltaTime);
            Vector3 acceleration = force / SimConstants.BALL_MASS;
            ball.Velocity += acceleration * deltaTime;
            ball.Position += ball.Velocity * deltaTime;

            // Ground collision check
            if (ball.Position.y <= SimConstants.BALL_RADIUS)
            {
                HandleBallGroundCollision(ball);
            }
        }

        private void HandleBallGroundCollision(SimBall ball)
        {
            ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);
            Vector3 incomingVelocity = ball.Velocity;
            float vDotN = Vector3.Dot(incomingVelocity, Vector3.up);

            if (vDotN < 0)
            {
                Vector3 reflectedVelocity = incomingVelocity - 2 * vDotN * Vector3.up;
                reflectedVelocity *= SimConstants.COEFFICIENT_OF_RESTITUTION;

                var horizontalVelocity = new Vector3(reflectedVelocity.x, 0, reflectedVelocity.z);
                horizontalVelocity *= (1f - SimConstants.FRICTION_COEFFICIENT_SLIDING);

                ball.Velocity = new Vector3(horizontalVelocity.x, reflectedVelocity.y, horizontalVelocity.z);

                if (Mathf.Abs(ball.Velocity.y) < SimConstants.ROLLING_TRANSITION_VEL_Y_THRESHOLD)
                {
                    float horizontalSpeed = new Vector2(ball.Velocity.x, ball.Velocity.z).magnitude;
                    if (horizontalSpeed > SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                    {
                        ball.StartRolling(); // Start rolling
                        ball.Velocity = new(ball.Velocity.x, 0, ball.Velocity.z);
                    }
                    else
                    {
                        ball.Stop();
                    }
                }
            }
        }

        private void SimulateBallRolling(SimBall ball, float deltaTime)
        {
            Vector3 horizontalVelocity = new(ball.Velocity.x, 0, ball.Velocity.z);
            float horizontalSpeed = horizontalVelocity.magnitude;

            if (horizontalSpeed > SimConstants.FLOAT_EPSILON)
            {
                float frictionDeceleration = SimConstants.FRICTION_COEFFICIENT_ROLLING * SimConstants.EARTH_GRAVITY;
                float speedReduction = frictionDeceleration * deltaTime;
                float newSpeed = Mathf.Max(0, horizontalSpeed - speedReduction);

                if (newSpeed < SimConstants.ROLLING_TRANSITION_VEL_XZ_THRESHOLD)
                {
                    ball.Stop();
                }
                else
                {
                    ball.Velocity = horizontalVelocity.normalized * newSpeed;
                    ball.Position += ball.Velocity * deltaTime;
                    ball.Position = new Vector3(ball.Position.x, SimConstants.BALL_RADIUS, ball.Position.z);
                }
            }
            else
            {
                ball.Stop();
            }
        }

        /// <summary>
        /// Handles logic when a player exceeds the allowed number of steps (step violation).
        /// This will log the violation, notify the match state, and trigger a turnover or foul as appropriate.
        /// </summary>
        /// <param name="player">The player who committed the step violation.</param>
        private void TriggerStepViolation(PlayerData player)
        {
            if (player == null) return;
            
            // Log the violation
            Debug.Log($"[MovementSimulator] Step violation detected for player {player.FullName} (ID: {player.PlayerID})");

            // Optionally, mark the player as having violated (could be a flag or stat)
            player.LastStepViolationTime = Time.time;

            // Notify match state (if accessible)
            // Example: Add a MatchEvent for the violation
            var state = player.CurrentMatchState;
            if (state != null && state.MatchEvents != null)
            {
                state.MatchEvents.Add(new MatchEvent(state.MatchTimeSeconds, $"Step violation by {player.FullName}", player.CurrentTeamID ?? -1, player.PlayerID));
            }

            // Trigger a turnover (if logic is available)
            // Example: Call a method on the match event handler
            if (state != null && _eventHandler != null)
            {
                _eventHandler.HandleActionResult(new ActionResult {
                    Outcome = ActionResultOutcome.Turnover,
                    PrimaryPlayer = state.GetPlayerById(player.PlayerID),
                    Reason = "Step Violation"
                }, state);
            }

            // Optionally, reset the player's step count or take other corrective action
            player.ResetSteps();
        }

        /// <summary>
        /// Updates players' movement based on their current actions and targets.
        /// </summary>
        // Store previous IsDribbling state for each player
        static readonly Dictionary<int, bool> prevDribblingState = new();

        private void UpdatePlayersMovement(MatchState state, float deltaTime)
        {
            if(state.PlayersOnCourt == null) return;

            const float STEP_THRESHOLD = 0.25f; // meters, adjust as needed
            foreach (var player in state.PlayersOnCourt)
            {
                if (player == null || player.SuspensionTimer > 0) continue;

                Vector2 previousPosition = player.Position;
                Vector2 targetVelocity = CalculateActionTargetVelocity(player, out bool allowSprint, out bool applyArrivalSlowdown);
                ApplyAcceleration(player, targetVelocity, allowSprint, applyArrivalSlowdown, deltaTime);
                player.Position += player.Velocity * deltaTime;

                // --- Fatigue System: Update fatigue based on movement ---
                // --- Fatigue System: Update fatigue based on match progression ---
                float matchProgress = Mathf.Clamp01(state.MatchTimeSeconds / Mathf.Max(1f, state.MatchDurationSeconds));
                float accumulationMultiplier = Mathf.Lerp(1.0f, 2.0f, matchProgress); // up to 2x at end
                float recoveryMultiplier = Mathf.Lerp(1.0f, 0.5f, matchProgress); // down to 0.5x at end

                Debug.Log($"[FatigueMult] Time: {state.MatchTimeSeconds:F1}/{state.MatchDurationSeconds:F1} ({matchProgress:P1}) | Accum: {accumulationMultiplier:F2}, Recov: {recoveryMultiplier:F2}");
                player.UpdateFatigue(deltaTime, player.HasBall);

                // --- Goal Area Violation Enforcement (Handball 6m Zone Rules) ---
                bool inGoalArea = IsInGoalArea(player.Position, player.TeamSimId);
                bool inGoalAreaBuffer = IsInGoalAreaBuffer(player.Position, player.TeamSimId);
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
                            TriggerTurnover(player, state, "Attacker entered 6m zone");
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
                bool isJumping = player.CurrentAction == PlayerAction.Jumping;
                if (isAttacking && wasJumping && !isJumping && player.JumpOriginatedOutsideGoalArea && inGoalArea && player.HasBall)
                {
                    // Attacker landed in 6m zone with ball after jump: turnover
                    TriggerTurnover(player, state, "Attacker landed in 6m zone with ball after jump");
                }

                ApplyStaminaEffects(player, deltaTime);

                // Step tracking logic
                if (player.BaseData is HandballManager.Data.PlayerData pdata)
                {
                    // --- Jumping Mechanics Update ---
                    // Track jump state transitions for zone logic
                    // (wasJumping, isJumping, isLanding already computed above)

                    // Detect jump start
                    if (!wasJumping && isJumping)
                    {
                        player.JumpOrigin = player.Position;
                        player.JumpOriginatedOutsideGoalArea = !IsInGoalArea(player.Position, player.TeamSimId);
                    }
                    // Detect landing or jump end
                    else if (wasJumping && !isJumping)
                    {
                        player.JumpOrigin = null;
                        player.JumpOriginatedOutsideGoalArea = false;
                    }

                    pdata.UpdateJump(deltaTime);

                    // Get previous dribbling state
                    int pid = pdata.PlayerID;
                    prevDribblingState.TryGetValue(pid, out bool wasDribbling);
                    if (!wasDribbling && pdata.IsDribbling)
                    {
                        pdata.ResetSteps();
                    }

                    // If player just caught the ball or picked up dribble, reset steps (optional: depends on rules)
                    // if (wasDribbling && !pdata.IsDribbling && pdata.HasBall) { pdata.ResetSteps(); }

                    // Step counting
                    if (pdata.HasBall && !pdata.IsDribbling)
                    {
                        float distance = Vector2.Distance(player.Position, previousPosition);
                        if (distance > STEP_THRESHOLD)
                        {
                            pdata.IncrementStep();
                            if (pdata.ExceededStepLimit())
                            {
                                TriggerStepViolation(pdata);
                            }
                        }
                    }

                    // Update previous dribbling state
                    prevDribblingState[pid] = pdata.IsDribbling;
                }
            }
        }

        private Vector2 CalculateActionTargetVelocity(SimPlayer player, out bool allowSprint, out bool applyArrivalSlowdown)
        {
            Vector2 targetVelocity = Vector2.zero;
            allowSprint = false;
            applyArrivalSlowdown = true;

            if (player?.BaseData == null) return targetVelocity;

            switch (player.CurrentAction)
            {
                case PlayerAction.MovingToPosition:
                case PlayerAction.MovingWithBall:
                case PlayerAction.ChasingBall:
                case PlayerAction.MarkingPlayer:
                case PlayerAction.ReceivingPass:
                case PlayerAction.AttemptingIntercept:
                case PlayerAction.AttemptingBlock:
                case PlayerAction.GoalkeeperPositioning:
                    Vector2 direction = (player.TargetPosition - player.Position);
                    if (direction.sqrMagnitude > MIN_DISTANCE_CHECK_SQ)
                    {
                        targetVelocity = direction.normalized * player.EffectiveSpeed;
                    }
                    allowSprint = player.AssignedTacticalRole != PlayerPosition.Goalkeeper && player.CurrentAction != PlayerAction.AttemptingBlock;
                    break;

                case PlayerAction.PreparingPass:
                case PlayerAction.PreparingShot:
                case PlayerAction.AttemptingTackle:
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
        /// Determines the severity of a foul (FreeThrow or PenaltyThrow) based on the context of the foul.
        /// - PenaltyThrow if the attacker is pushed in the air with significant speed, or if there is head contact.
        /// - FreeThrow otherwise.
        /// </summary>
        /// <param name="attacker">The player being fouled</param>
        /// <param name="defender">The player committing the foul</param>
        /// <param name="wasPush">True if the foul was a push (for air check)</param>
        /// <returns>FoulSeverity.FreeThrow or FoulSeverity.PenaltyThrow</returns>
        private FoulSeverity DetermineFoulSeverity(SimPlayer attacker, SimPlayer defender, bool wasPush)
        {
            // Threshold for "dangerous" speed in the air (tweak as needed)
            const float airborneSpeedThreshold = 2.5f; // meters/second

            bool isAirborne = attacker.CurrentAction == PlayerAction.Jumping;
            bool isPushedInAir = isAirborne && wasPush;
            bool isFastInAir = isAirborne && attacker.Velocity.magnitude > airborneSpeedThreshold;
            // TODO: Implement head contact detection
            // bool isHeadContact = ...;
            // if (isHeadContact) return FoulSeverity.PenaltyThrow;

            if (isPushedInAir && isFastInAir)
                return FoulSeverity.PenaltyThrow;

            return FoulSeverity.FreeThrow;
        }

        private void ApplyAcceleration(SimPlayer player, Vector2 targetVelocity, bool allowSprint, bool applyArrivalSlowdown, float deltaTime)
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

            float agilityFactor = Mathf.Lerp(PLAYER_AGILITY_MOD_MIN, PLAYER_AGILITY_MOD_MAX, (player.BaseData?.Agility ?? 50f) / 100f);
            float maxAccel = PLAYER_ACCELERATION_BASE * agilityFactor;
            float maxDecel = PLAYER_DECELERATION_BASE * agilityFactor;

            bool isAccelerating = Vector2.Dot(requiredAcceleration, currentVelocity.normalized) > -0.1f || currentSpeed < PLAYER_NEAR_STOP_VELOCITY_THRESHOLD;
            float maxAccelerationMagnitude = isAccelerating ? maxAccel : maxDecel;

            Vector2 appliedAcceleration = Vector2.ClampMagnitude(requiredAcceleration, maxAccelerationMagnitude);
            player.Velocity += appliedAcceleration * deltaTime;

            // Clamp final velocity to prevent excessive speed
            float maxAllowedSpeed = player.EffectiveSpeed * PLAYER_MAX_SPEED_OVERSHOOT_FACTOR;
            if (player.Velocity.sqrMagnitude > maxAllowedSpeed * maxAllowedSpeed)
            {
                player.Velocity = player.Velocity.normalized * maxAllowedSpeed;
            }
        }

        private void ApplyStaminaEffects(SimPlayer player, float deltaTime)
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

        /// <summary>
        /// /// Checks for and resolves collisions between players and between players and the ball.
        /// Implements the IMovementSimulator interface method.
        /// </summary>
        /// <param name="state">The current match state containing players and ball data.</param>
        /// <exception cref="ArgumentNullException">Thrown when state is null.</exception>
        public void ResolveCollisions(MatchState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state), "Match state cannot be null when resolving collisions.");
            }

            // Delegate to the existing collision handling logic with a minimal time step
            // This allows reuse of the existing collision logic without duplicating code
            HandleCollisionsAndBoundaries(state);
        }

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

        /// <summary>
        /// Handles collisions and boundaries with proper single implementation
        /// </summary>
        private void HandleCollisionsAndBoundaries(MatchState state)
        {
            if (state?.PlayersOnCourt == null) return;

            // --- Reset BallProtectionBonus for all players at the start of the step ---
            // BallProtectionBonus property removed; shielding handled via PlayerData.GetShieldingEffectiveness()

            var players = state.PlayersOnCourt.ToList(); // Convertir IEnumerable en List pour permettre l'indexation

            // --- Spatial Partitioning for Player-Player Collisions and Team Spacing ---
            float pitchLength = (_geometry?.PitchLength ?? SimConstants.DEFAULT_PITCH_LENGTH);
            float pitchWidth = (_geometry?.PitchWidth ?? SimConstants.DEFAULT_PITCH_WIDTH);
            float cellSize = SimConstants.PLAYER_COLLISION_RADIUS * 2.5f;
            if (_spatialGrid == null || _spatialGridCellSize != cellSize || _spatialGridWidth != pitchLength || _spatialGridHeight != pitchWidth)
            {
                _spatialGrid = new SpatialGrid(pitchLength, pitchWidth, cellSize);
                _spatialGridCellSize = cellSize;
                _spatialGridWidth = pitchLength;
                _spatialGridHeight = pitchWidth;
            }
            _spatialGrid.Clear();
            foreach (var p in players)
                if (p != null) _spatialGrid.Insert(p);

            HashSet<(int, int)> checkedPairs = new();
            for (int i = 0; i < players.Count; i++)
            {
                var player1 = players[i];
                if (player1 == null) continue;
                var nearbyPlayers = _spatialGrid.GetNearbySimPlayers(player1.Position, SimConstants.PLAYER_COLLISION_RADIUS * 2f);
                foreach (var player2 in nearbyPlayers)
                {
                    if (player2 == null || player1 == player2) continue;
                    int id1 = player1.GetPlayerId();
                    int id2 = player2.GetPlayerId();
                    if (id1 > id2) continue; // Avoid duplicate checks
                    var pair = (id1, id2);
                    if (checkedPairs.Contains(pair)) continue;
                    checkedPairs.Add(pair);

                    Vector2 separation = player1.Position - player2.Position;
                    float distanceSq = separation.sqrMagnitude;

                    // Handle collision
                    if (distanceSq < SimConstants.PLAYER_COLLISION_DIAMETER_SQ && distanceSq > COLLISION_MIN_DIST_SQ_CHECK)
                    {
                        float distance = Mathf.Sqrt(distanceSq);
                        Vector2 separationDir = separation / distance;

                        float overlap = SimConstants.PLAYER_COLLISION_DIAMETER - distance;
                        Vector2 responseVector = COLLISION_RESPONSE_FACTOR * overlap * separationDir;

                        // --- Shielding Mechanics ---
                        bool player1Shielding = player1.CurrentAction == PlayerAction.ShieldingBall && player1.HasBall;
                        bool player2Shielding = player2.CurrentAction == PlayerAction.ShieldingBall && player2.HasBall;
                        float shieldFactor1 = 0f, shieldFactor2 = 0f;
                        if (player1Shielding && player1.BaseData != null)
                            shieldFactor1 = player1.BaseData.GetShieldingEffectiveness();
                        if (player2Shielding && player2.BaseData != null)
                            shieldFactor2 = player2.BaseData.GetShieldingEffectiveness();

                        // Reduce displacement effect for shielding player
                        Vector2 response1 = responseVector;
                        Vector2 response2 = -responseVector;
                        if (player1Shielding)
                            response1 *= (1f - shieldFactor1);
                        if (player2Shielding)
                            response2 *= (1f - shieldFactor2);
                        player1.Position += response1;
                        player2.Position += response2;

                        // Add velocity response (conservation of momentum)
                        Vector2 relativeVelocity = player1.Velocity - player2.Velocity;
                        float velAlongNormal = Vector2.Dot(relativeVelocity, separationDir);
                        
                        if (velAlongNormal > 0) // Only if moving towards each other
                        {
                            // In collision response
                            float p1Mass = 1.0f + ((player1.BaseData?.Strength ?? 50f) / 200f);
                            float p2Mass = 1.0f + ((player2.BaseData?.Strength ?? 50f) / 200f);
                            float impulse = velAlongNormal / (1 / p1Mass + 1 / p2Mass);
                            Vector2 impulseVector = separationDir * impulse;
                            // Reduce impulse for shielding
                            if (player1Shielding)
                                impulseVector *= (1f - shieldFactor1);
                            if (player2Shielding)
                                impulseVector *= (1f - shieldFactor2);
                            player1.Velocity -= impulseVector / p1Mass;
                            player2.Velocity += impulseVector / p2Mass;
                        }
                    }

                    // Handle team spacing
                    if (player1.TeamSimId == player2.TeamSimId && distanceSq < MIN_SPACING_DISTANCE_SQ)
                    {
                        float spacingStrength = 1f - (distanceSq / MIN_SPACING_DISTANCE_SQ);
                        spacingStrength = Mathf.Pow(spacingStrength, SPACING_PROXIMITY_POWER);
                        Vector2 spacingForce = separation.normalized * spacingStrength * SPACING_PUSH_FACTOR;

                        player1.Position += spacingForce;
                        player2.Position -= spacingForce;
                    }
                }
            }

            // Boundary clamping for players
            foreach (var player in players)
            {
                if (player == null) continue;
                // In boundary clamping
                float maxX = (_geometry?.PitchLength ?? SimConstants.DEFAULT_PITCH_LENGTH) - SIDELINE_BUFFER;
                float maxY = (_geometry?.PitchWidth ?? SimConstants.DEFAULT_PITCH_WIDTH) - SIDELINE_BUFFER;
                player.Position = new Vector2(
                    Mathf.Clamp(player.Position.x, SIDELINE_BUFFER, maxX),
                    Mathf.Clamp(player.Position.y, SIDELINE_BUFFER, maxY)
                );
            }

            // Ball boundary clamping
            if (state.Ball != null)
            {
                SimBall ball = state.Ball;
                float ballMaxX = (_geometry?.PitchLength ?? SimConstants.DEFAULT_PITCH_LENGTH) - SIDELINE_BUFFER;
                float ballMaxZ = (_geometry?.PitchWidth ?? SimConstants.DEFAULT_PITCH_WIDTH) - SIDELINE_BUFFER;
                ball.Position = new Vector3(
                    Mathf.Clamp(ball.Position.x, SIDELINE_BUFFER, ballMaxX),
                    ball.Position.y,
                    Mathf.Clamp(ball.Position.z, SIDELINE_BUFFER, ballMaxZ)
                );
            }
        }

        /// <summary>
        /// Interface implementation with single method for enforcing boundaries
        /// </summary>
        public void EnforceBoundaries(MatchState state)
        {
            if (state == null) return;
            HandleCollisionsAndBoundaries(state);
        }

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

        // Special situation positioning is now handled by TacticPositioner.
        // Removed legacy helpers from MovementSimulator.

        // --- Goal Area Violation Helpers (Handball 6m Zone) ---
        // Only the goalkeeper is allowed in their own 6m zone
        private bool ShouldAvoidGoalArea(SimPlayer player, MatchState state)
        {
            return !IsGoalkeeper(player);
        }

        // Returns true if this player is the goalkeeper
        private bool IsGoalkeeper(SimPlayer player)
        {
            return player.AssignedTacticalRole == PlayerPosition.Goalkeeper;
        }

        // Triggers a turnover for the attacking team
        private void TriggerTurnover(SimPlayer player, MatchState state, string reason)
        {
            if (_eventHandler != null && state != null)
            {
                _eventHandler.HandleActionResult(new ActionResult
                {
                    Outcome = ActionResultOutcome.Turnover,
                    PrimaryPlayer = player,
                    Reason = reason
                }, state);
            }
        }

        // Testable: Returns true if position is inside the goal area for a team
        private bool IsInGoalArea(Vector2 position, int teamId)
        {
            Vector2 goalCenter = _geometry.GetGoalCenter(teamId == 0 ? 1 : 0); // Opponent's goal area
            float radius = _geometry.GoalAreaRadius;
            return Vector2.Distance(position, goalCenter) < radius;
        }

        // Returns true if position is within the avoidance buffer outside the goal area
        private bool IsInGoalAreaBuffer(Vector2 position, int teamId)
        {
            Vector2 goalCenter = _geometry.GetGoalCenter(teamId == 0 ? 1 : 0);
            float buffer = _geometry.GoalAreaRadius + 0.7f; // 0.7m buffer (adjustable)
            return Vector2.Distance(position, goalCenter) < buffer && !IsInGoalArea(position, teamId);
        }

        // Returns true if the player is allowed to enter the zone in current context
        // Only allowed if the player is in the air and the jump originated from outside the zone
        private bool IsAllowedZoneEntryContext(SimPlayer player, MatchState state, bool isAttacking, bool isDefending)
        {
            return player.CurrentAction == PlayerAction.Jumping && player.JumpOriginatedOutsideGoalArea;
        }

        // Testable: Redirects player velocity tangentially around the goal area
        private void RedirectAroundGoalArea(SimPlayer player, int teamId)
        {
            Vector2 goalCenter = _geometry.GetGoalCenter(teamId == 0 ? 1 : 0);
            Vector2 fromGoal = (player.Position - goalCenter).normalized;
            Vector2 tangent = new Vector2(-fromGoal.y, fromGoal.x);
            if (Vector2.Dot(tangent, player.Velocity) < 0)
                tangent = -tangent;
            player.Velocity = tangent * player.Velocity.magnitude;
            player.Velocity += fromGoal * player.Velocity.magnitude * 0.2f;
        }

    }

 }