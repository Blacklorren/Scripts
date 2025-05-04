using UnityEngine;
using System;
using HandballManager.Simulation.Utils;
using HandballManager.Core;
using HandballManager.Data;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.Events.Interfaces;

namespace HandballManager.Simulation.Physics
{
    public partial class MovementSimulator : IMovementSimulator
    {
        
        private readonly ITacticPositioner _tacticPositioner;
        private readonly IGeometryProvider _geometry;
        private readonly IMatchEventHandler _eventHandler;

        // Modularized sub-components
        private PlayerPhysicsEngine _playerPhysicsEngine;
        private readonly StaminaManager _staminaManager;
        private readonly CollisionResolver _collisionResolver;

        public void ResolveCollisions(MatchState state)
        {
            _collisionResolver.ResolveCollisions(state);
        }

        public void UpdateStamina(MatchState state, float deltaTime)
        {
            _staminaManager.UpdateStamina(state, deltaTime);
        }

        public void EnforceBoundaries(MatchState state)
        {
            _collisionResolver.EnforceBoundaries(state);
        }

        public void HandleSpecialMovement(MatchState state, GameSituationType situationType)
        {
            // TODO: Implement special movement logic for set pieces or specific game situations.
        }
        public MovementSimulator(IGeometryProvider geometry, IMatchEventHandler eventHandler, ITacticPositioner tacticPositioner,
            StaminaManager staminaManager,
            CollisionResolver collisionResolver,
            Action attackingIntentHandler = null)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _tacticPositioner = tacticPositioner ?? throw new ArgumentNullException(nameof(tacticPositioner));
            _playerPhysicsEngine = new PlayerPhysicsEngine(
                TriggerTurnover,
                _geometry,
                _eventHandler,
                _staminaManager,
                _tacticPositioner,
                attackingIntentHandler
            );
            _staminaManager = staminaManager ?? throw new ArgumentNullException(nameof(staminaManager));
            _collisionResolver = new CollisionResolver(_geometry);
        }
        
        /// <summary>
        /// Met à jour le PlayerPhysicsEngine avec un nouveau delegate pour la détection d'intention d'attaque.
        /// Cette méthode est utilisée par le système d'injection de dépendances pour connecter
        /// le PlayerPhysicsEngine au PassivePlayManager après l'initialisation.
        /// </summary>
        /// <param name="attackingIntentHandler">Le delegate à appeler lorsqu'une intention d'attaque est détectée</param>
        public void UpdatePlayerPhysicsEngine(Action attackingIntentHandler)
        {
            // Créer une nouvelle instance de PlayerPhysicsEngine avec le nouveau delegate
            _playerPhysicsEngine = new PlayerPhysicsEngine(
                TriggerTurnover,
                _geometry,
                _eventHandler,
                _staminaManager,
                _tacticPositioner,
                attackingIntentHandler
            );
        }

        
        // Collision Constants
        private const float PLAYER_COLLISION_RADIUS = 0.4f;
        private const float PLAYER_COLLISION_DIAMETER = PLAYER_COLLISION_RADIUS * 2f;
        private const float PLAYER_COLLISION_DIAMETER_SQ = PLAYER_COLLISION_DIAMETER * PLAYER_COLLISION_DIAMETER;
        private const float COLLISION_RESPONSE_FACTOR = 0.5f;  // How strongly players push apart
        private const float COLLISION_MIN_DIST_SQ_CHECK = 0.0001f;  // Lower bound for collision distance check
                      
                
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
            _playerPhysicsEngine.UpdatePlayersMovement(state, deltaTime);
            _staminaManager.UpdateStamina(state, deltaTime);
            _collisionResolver.ResolveCollisions(state);
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

            // Update physics using RK2 integration
            ball.AngularVelocity *= Mathf.Pow(SimConstants.SPIN_DECAY_FACTOR, deltaTime);
            Vector3 acceleration = force / SimConstants.BALL_MASS;
            Vector3 midVel = ball.Velocity + 0.5f * acceleration * deltaTime;
            ball.Position += midVel * deltaTime;
            ball.Velocity += acceleration * deltaTime;

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

                ball.Velocity = new(horizontalVelocity.x, reflectedVelocity.y, horizontalVelocity.z);

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
        /// <param name="state">The current match state.</param>
        private void TriggerStepViolation(PlayerData player, MatchState state)
        {
            if (player == null || state == null) return;
            
            // Log the violation
            Debug.Log($"[MovementSimulator] Step violation detected for player {player.FullName} (ID: {player.PlayerID})");

            // Optionally, mark the player as having violated (could be a flag or stat)
            player.LastStepViolationTime = Time.time;

            // Add a MatchEvent for the violation
            if (state.MatchEvents != null)
            {
                state.MatchEvents.Add(new MatchEvent(state.MatchTimeSeconds, $"Step violation by {player.FullName}", player.CurrentTeamID ?? -1, player.PlayerID));
            }

            // Trigger a turnover (if logic is available)
            if (_eventHandler != null)
            {
                _eventHandler.HandleActionResult(new ActionResult {
                    Outcome = ActionResultOutcome.Turnover,
                    PrimaryPlayer = state.GetPlayerById(player.PlayerID),
                    Reason = "Step Violation"
                }, state);
            }

            // Optionally, reset the player's step count or take other corrective action
            var simPlayer = state.GetPlayerById(player.PlayerID);
if (simPlayer != null)
    simPlayer.ResetSteps(simPlayer.Position);
        }
        
        // Special situation positioning is now handled by TacticPositioner.
        // Removed legacy helpers from MovementSimulator.

        // --- Stumbling Effect Handling (to be added in movement logic) ---
        // In ApplyAcceleration or UpdatePlayersMovement:
        // if (player.IsStumbling) {
        //     Reduce maxAccel/maxDecel, cap EffectiveSpeed, decrement StumbleTimer by deltaTime.
        //     If StumbleTimer <= 0, set IsStumbling = false;
        // }

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
            return player.CurrentAction == PlayerAction.JumpingForShot && player.JumpOriginatedOutsideGoalArea;
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
            Vector3 addVec = new Vector3(fromGoal.x, 0, fromGoal.y) * player.Velocity.magnitude * 0.2f;
player.Velocity += addVec;
        }

    }

 }