using HandballManager.Gameplay; // For Handedness, PlayerRole etc. if needed later
using HandballManager.Data; // Access PlayerData constants/enums if needed
using HandballManager.Simulation; // Added to find SimTeam etc.
using HandballManager.Simulation.Physics; // Added to find JumpSimulator
using UnityEngine;
using HandballManager.Core; // Added for SimConstants
using static HandballManager.Simulation.AI.Decision.DefaultOffensiveDecisionMaker; // Added for ScreenUseData

namespace HandballManager.Simulation.Engines // Changed namespace
{
    /// <summary>
    /// Represents the state and behaviour of a player within the simulation engine.
    /// This class holds transient data specific to a match instance, like position,
    /// current actions, and temporary states (e.g., step count, dribble status).
    /// It links back to the persistent PlayerData via the Data property.
    /// </summary>
    public class SimPlayer
    {
        /// <summary>Reference to the persistent player data.</summary>
        public Data.PlayerData BaseData { get; private set; }

        /// <summary>The unique ID of the player.</summary>
        public int PlayerID => BaseData.PlayerID;

        /// <summary>Gets the unique ID of the player (Method version for compatibility).</summary>
        /// <returns>The player's unique ID.</returns>
        public int UniqueID() => PlayerID;

        /// <summary>Gets the unique ID of the player (Method version for compatibility).</summary>
        /// <returns>The player's unique ID.</returns>
        public int GetPlayerId() => PlayerID;

        /// <summary>The player's current 2D position on the court.</summary>
        public Vector2 Position { get; set; } = Vector2.zero;

        /// <summary>Gets the player's current 2D position on the court (Method version for compatibility).</summary>
        /// <returns>The player's current position.</returns>
        public Vector2 CurrentPosition()
        {
            return Position;
        }

        /// <summary>The player's current 3D orientation.</summary>
        public Quaternion Rotation { get; set; } = Quaternion.identity;

        /// <summary>The player's current velocity vector.</summary>
        public Vector3 Velocity { get; set; } = Vector3.zero;

        /// <summary>Gets the vertical component of the player's velocity.</summary>
        public float VerticalVelocity
        {
            get => Velocity.y;
            set
            {
                Vector3 currentVelocity = Velocity;
                currentVelocity.y = value;
                Velocity = currentVelocity; // Assign the modified Vector3 back
            }
        }

        /// <summary>The normalized direction of the player's current movement, or Vector3.zero if stationary.</summary>
        public Vector3 CurrentMovementDirection
        {
            get
            {
                // Use sqrMagnitude for efficiency and avoid sqrt
                if (Velocity.sqrMagnitude > 0.001f) // Threshold to consider player moving
                {
                    return Velocity.normalized;
                }
                return Vector3.zero;
            }
        }

        /// <summary>The ID of the team this player belongs to in the simulation (0 for Home, 1 for Away).</summary>
        public int TeamId { get; private set; } = -1; // Initialize to invalid ID

        /// <summary>Alias for TeamId for backward compatibility.</summary>
        public int TeamSimId => TeamId;

        /// <summary>Gets the ID of the team this player belongs to (Method version for compatibility).</summary>
        /// <returns>The team ID (0 for Home, 1 for Away).</returns>
        public int GetTeamId()
        {
            return TeamId;
        }

        /// <summary>Current tactical role/position assigned to the player.</summary>
        public PlayerPosition TacticalRole { get; set; } // Changed type from PlayerRole to PlayerPosition

        /// <summary>Alias for TacticalRole for backward compatibility.</summary>
        public PlayerPosition AssignedTacticalRole => TacticalRole;

        /// <summary>Checks if the player's assigned tactical role is Goalkeeper.</summary>
        /// <returns>True if the player is a Goalkeeper, false otherwise.</returns>
        public bool IsGoalkeeper()
        {
            return TacticalRole == PlayerPosition.Goalkeeper;
        }

        /// <summary>Target position assigned by the tactical system.</summary>
        public Vector2 TacticalTargetPosition { get; set; } = Vector2.zero;

        /// <summary>Current action being performed by the player (e.g., Moving, Passing, Shooting).</summary>
        public PlayerAction CurrentAction { get; set; } = PlayerAction.Idle; // Default state

        /// <summary>Target SimPlayer for the current action, if applicable (e.g., pass target).</summary>
        public SimPlayer ActionTargetPlayer { get; set; } = null;

        /// <summary>Alias for ActionTargetPlayer for backward compatibility.</summary>
        public SimPlayer TargetPlayer
        {
            get => ActionTargetPlayer;
            set => ActionTargetPlayer = value;
        }

        private Vector2 _actionTargetPosition = Vector2.zero;

        /// <summary>Gets or sets the target position for the current action.</summary>
        public Vector2 TargetPosition
        {
            get => _actionTargetPosition;
            set => _actionTargetPosition = value; // Allow direct setting
        }

        /// <summary>Timer related to the current action.</summary>
        public float ActionTimer { get; set; } = 0f;

        // --- Possession State ---
        /// <summary>Indicates if this player currently possesses the ball.</summary>
        public bool HasBall { get; set; } = false;

        // --- Handball Step Tracking & Dribble Rules (Moved from PlayerData) ---
        public int StepCount { get; private set; } = 0;
        public bool IsDribbling { get; set; } = false;
        /// <summary>True if the player has already dribbled since gaining possession (for double dribble rule).</summary>
        public bool HasDribbledSincePossession { get; set; } = false;
        public Vector2 LastStepPosition { get; set; } = Vector2.zero;
        public bool IsFirstStepAfterCatch { get; set; } = false;
        public const float StepDistanceThreshold = 0.5f; // meters, adjust as needed

        // --- Other SimPlayer Specific Properties & Methods ---
        /// <summary>Link to the JumpSimulator managing this player's jumps.</summary>
        public JumpSimulator JumpSim { get; private set; }

        /// <summary>Indicates if the player is currently controlled by the user.</summary>
        public bool IsUserControlled { get; set; } = false;

        /// <summary>Indicates whether the player has recently received a pass.</summary>
        public bool ReceivedPassRecently { get; set; } = false;

        /// <summary>Cooldown timer for actions like tackling or shooting.</summary>
        public float ActionCooldown { get; set; } = 0f;

        /// <summary>Timer tracking the duration of a suspension.</summary>
        public float SuspensionTimer { get; set; } = 0f;

        /// <summary>Checks if the player is currently suspended.</summary>
        /// <returns>True if the suspension timer is active, false otherwise.</returns>
        public bool IsSuspended()
        {
            return SuspensionTimer > 0f;
        }

        // --- State Flags (derived or temporary) ---
        public bool IsOnCourt { get; set; } = true; // Default to on court, managed by SubstitutionManager

        // --- New Properties & Methods ---

        /// <summary>Player's stamina level (0 to 1).</summary>
        public float Stamina { get; set; } = 1f;

        /// <summary>The player's calculated speed, affected by base speed and current stamina.</summary>
        public float EffectiveSpeed { get; set; }

        /// <summary>Player's look direction.</summary>
        public Vector2 LookDirection { get; set; } = Vector2.right;

        /// <summary>Player's vertical position (height above ground).</summary>
        public float VerticalPosition { get; set; } = 0f;

        /// <summary>Player's assigned formation slot role.</summary>
        public string AssignedFormationSlotRole { get; set; } = null;

        /// <summary>Player's planned action (AI's intended next action).</summary>
        public PlayerAction PlannedAction { get; set; } = PlayerAction.Idle;

        /// <summary>Player's yellow card count.</summary>
        public int YellowCardCount { get; set; } = 0;

        /// <summary>Player's two-minute suspension count.</summary>
        public int TwoMinuteSuspensionCount { get; set; } = 0;

        /// <summary>Player's stumble state.</summary>
        private bool isStumbling = false;

        /// <summary>Player's stumble timer.</summary>
        private float stumbleTimer = 0f;
        /// <summary>
        /// Gets the remaining time for the player's stumble state (0 if not stumbling).
        /// </summary>
        public float StumbleTimer => stumbleTimer;

        /// <summary>Gets whether the player is currently stumbling.</summary>
        public bool IsStumbling => isStumbling;

        // --- Jump State (Complementary to JumpSimulator) ---
        public Vector2? JumpOrigin { get; set; } = null; // Position where the current jump started
        public bool JumpOriginatedOutsideGoalArea { get; set; } = false; // Context for rule checks
        private bool isRecoveringFromJump = false;
        private float jumpRecoveryTimer = 0f;
        public bool IsRecoveringFromJump => isRecoveringFromJump; // Read-only access

        // --- Jump State (Managed by JumpSimulator but stored here) ---
        public Vector2 JumpStartVelocity { get; set; } = Vector2.zero;
        public float JumpTimer { get; set; } = 0f;
        public bool IsJumping { get; set; } = false; // Actively in the air
        public bool JumpActive { get; set; } = false; // Jump simulation is running
        public float JumpInitialHeight { get; set; } = 0f;

        /// <summary>True if the player is currently making a screen.</summary>
        public bool IsScreening { get; set; } // Simple flag for now

        /// <summary>Data related to the current screen being set or used.</summary>
        private ScreenUseData _currentScreenUseData; // Renamed from property

        /// <summary>Gets data related to the current screen being set or used (Method version for compatibility).</summary>
        /// <returns>The current screen use data.</returns>
        public ScreenUseData CurrentScreenUseData()
        {
            return _currentScreenUseData;
        }

        /// <summary>Sets the data related to the current screen being set or used.</summary>
        /// <param name="data">The screen use data.</param>
        public void SetCurrentScreenUseData(ScreenUseData data)
        {
            _currentScreenUseData = data;
        }

        // --- Other State ---

        /// <summary>True if the player is currently on the court.</summary>

        /// <summary>
        /// Initializes a new instance of the SimPlayer class.
        /// </summary>
        /// <param name="data">The persistent player data.</param>
        /// <param name="teamId">The simulation ID of the team (0 for Home, 1 for Away).</param>
        public SimPlayer(Data.PlayerData data, int teamId)
        {
            BaseData = data;
            TeamId = teamId;
            JumpSim = new JumpSimulator(this); // Removed the extra gravity argument
            Position = Vector2.zero; // Initial position set by setup logic
            Rotation = Quaternion.identity;
            TacticalTargetPosition = Position;
            LookDirection = teamId == 0 ? Vector2.right : Vector2.left; // Default look towards opponent goal
            // Initialize position, rotation etc. based on team/formation if needed
        }

        // --- Update Logic ---
        public void Update(float deltaTime)
        {
            // Reduce cooldowns
            if (ActionCooldown > 0) ActionCooldown -= deltaTime;

            // Update position based on velocity (if using physics)
            // Position += new Vector2(Velocity.x, Velocity.z) * deltaTime;

            // Update jump simulation
            JumpSim.Update(deltaTime);
            VerticalPosition = JumpSim.CurrentHeight; // Keep VerticalPosition synced

            // Update jump recovery state
            UpdateJumpRecovery(deltaTime);
            if (IsRecoveringFromJump) {
                // Apply movement restrictions or modifications during recovery?
                // Example: Velocity *= 0.8f;
            }

            // Update stumble state
            UpdateStumble(deltaTime);

            // Update stamina and effective speed
            UpdateStamina(deltaTime);
            UpdateEffectiveSpeed();

            // Other per-frame updates (Stamina, AI decision triggers, etc.)
            // UpdateStamina(deltaTime, Velocity.magnitude > 0.1f);

            // Update Step Counting (if relevant)
            // Steps should not count during jump or recovery/stumble
            if (HasBall && !IsDribbling && !isStumbling && !JumpSim.IsJumping && !IsRecoveringFromJump)
            {
                TryIncrementStep(Position); // Assumes Position is updated by physics/movement logic
            }
        }

        /// <summary>Updates the player's effective maximum speed based on base attributes and current stamina.</summary>
        public void UpdateEffectiveSpeed()
        {
            float baseSpeedAttr = BaseData?.Speed ?? 75f; // Default value if data missing
            float maxSpeedPossible = Core.SimConstants.CalculateMaxSpeed(baseSpeedAttr);

            float staminaFactor = 1.0f;
            if (Stamina < Core.SimConstants.PLAYER_STAMINA_LOW_THRESHOLD)
            {
                staminaFactor = Mathf.Lerp(Core.SimConstants.PLAYER_STAMINA_MIN_SPEED_FACTOR, 1.0f, Stamina / Core.SimConstants.PLAYER_STAMINA_LOW_THRESHOLD);
            }
            EffectiveSpeed = maxSpeedPossible * staminaFactor;

            // Reduce speed if dribbling (potentially attribute-based later)
            if (IsDribbling)
            {
                EffectiveSpeed *= Core.SimConstants.PLAYER_DRIBBLING_SPEED_MULTIPLIER;
            }
        }

        /// <summary>Updates the player's stamina based on movement intensity and other factors.</summary>
        public void UpdateStamina(float deltaTime)
        {
            // Estimate movement intensity
            float movementIntensity = (Velocity.magnitude / (EffectiveSpeed > 0.1f ? EffectiveSpeed : 1f));
            movementIntensity = Mathf.Clamp01(movementIntensity);

            // Base drain rate (adjust based on player's Stamina attribute?)
            float drainRate = Core.SimConstants.PLAYER_STAMINA_DRAIN_RATE * movementIntensity;

            // Increased drain if holding ball or in high-intensity action?
            if (HasBall || CurrentAction == PlayerAction.Dribbling || CurrentAction == PlayerAction.Shooting || CurrentAction == PlayerAction.JumpingForShot)
            {
                drainRate *= Core.SimConstants.PLAYER_STAMINA_POSSESSION_DRAIN_MULTIPLIER;
            }

            Stamina -= drainRate * deltaTime;

            // Recovery when idle/low intensity
            if (movementIntensity < 0.1f && !HasBall)
            {
                Stamina += Core.SimConstants.PLAYER_STAMINA_RECOVERY_RATE * deltaTime;
            }

            Stamina = Mathf.Clamp01(Stamina); // Clamp to [0, 1]
        }

        /// <summary>Starts a stumble state for a given duration.</summary>
        internal void StartStumble(float duration)
        {
            if (!isStumbling) // Avoid restarting if already stumbling
            {
                isStumbling = true;
                stumbleTimer = duration;
                // Optionally interrupt current action
                // SetAction(PlayerAction.Idle);
                Debug.Log($"Player {BaseData.PlayerID} starts stumbling for {duration}s.");
            }
        }

        /// <summary>Updates the stumble timer and state.</summary>
        internal void UpdateStumble(float deltaTime)
        {
            if (isStumbling)
            {
                stumbleTimer -= deltaTime;
                if (stumbleTimer <= 0f)
                {
                    isStumbling = false;
                    stumbleTimer = 0f;
                    Debug.Log($"Player {BaseData.PlayerID} recovers from stumble.");
                }
            }
        }

        /// <summary>Starts the jump recovery state after landing.</summary>
        internal void StartJumpRecovery(float duration)
        {
            if (!isRecoveringFromJump) // Avoid restarting recovery
            {
                isRecoveringFromJump = true;
                jumpRecoveryTimer = duration;
                // Could also force player into idle briefly
                // SetAction(PlayerAction.Idle);
                Debug.Log($"Player {PlayerID} starting jump recovery ({duration}s).");
            }
        }

        /// <summary>Updates the jump recovery timer and state.</summary>
        public void UpdateJumpRecovery(float deltaTime)
        {
            if (isRecoveringFromJump)
            {
                jumpRecoveryTimer -= deltaTime;
                if (jumpRecoveryTimer <= 0f)
                {
                    isRecoveringFromJump = false;
                    jumpRecoveryTimer = 0f;
                    Debug.Log($"Player {BaseData.PlayerID} finished jump recovery.");
                }
            }
        }

        // --- Ball Possession & Rules Logic (Moved from PlayerData) ---
        public void StartPossession(Vector2 currentPosition)
        {
            HasBall = true;
            ResetSteps(currentPosition); // Reset steps and mark as first step potential
            // Reset dribble state ONLY on new possession for double dribble rule clarity.
            HasDribbledSincePossession = false;
            IsDribbling = false; // Ensure not marked as dribbling initially
        }

        public void LosePossession()
        {
            HasBall = false;
            ResetSteps(Position); // Reset steps, position doesn't matter as much here
            // Reset dribble state ONLY on possession loss for double dribble rule clarity.
            HasDribbledSincePossession = false;
            IsDribbling = false;
        }

        public void StartDribble()
        {
            if (!HasBall) return; // Cannot dribble without the ball
            if (HasDribbledSincePossession)
            {
                Debug.LogWarning($"Player {BaseData.PlayerID} attempting double dribble!");
                // Potentially trigger a turnover event here
                return;
            }
            IsDribbling = true;
            HasDribbledSincePossession = true; // Mark that a dribble has occurred
            ResetSteps(Position); // Reset step count on start of dribble
        }

        public void EndDribble()
        {
            if (!IsDribbling) return;
            IsDribbling = false;
            ResetSteps(Position); // Reset steps when picking up the ball after dribbling
        }

        /// <summary>Checks and increments the step count if the player moves beyond the threshold while not dribbling.</summary>
        public void TryIncrementStep(Vector2 currentPosition)
        {
            if (HasBall && !IsDribbling)
            {
                float dist = Vector2.Distance(currentPosition, LastStepPosition);
                if (dist >= StepDistanceThreshold)
                {
                    if (IsFirstStepAfterCatch)
                    {
                        // This was the 'zero step' (landing or first move after catch)
                        IsFirstStepAfterCatch = false; // Subsequent moves are counted
                    }
                    else
                    {
                        StepCount++;
                    }
                    LastStepPosition = currentPosition;

                    // Optional: Check for violation immediately after incrementing
                    // if (ExceededStepLimit()) { /* Trigger violation */ }
                }
            }
        }

        /// <summary>Resets the step count and related flags.</summary>
        public void ResetSteps(Vector2 currentPosition)
        {
            StepCount = 0;
            LastStepPosition = currentPosition;
            IsFirstStepAfterCatch = true; // Ready for a potential 'zero step'
            // Do NOT reset HasDribbledSincePossession here.
        }

        /// <summary>Checks if the player has committed a double dribble violation (by attempting to dribble again).</summary>
        public bool IsDoubleDribbleViolationAttempt() // Renamed for clarity
        {
            // Violation occurs if trying to START a dribble when HasDribbledSincePossession is already true.
            // The StartDribble method handles this check.
            return HasDribbledSincePossession;
        }

        /// <summary>Checks if the player has exceeded the allowed number of steps without dribbling.</summary>
        public bool ExceededStepLimit()
        {
            // Rule: Max 3 steps allowed *after* the initial catch/landing ('zero step').
            // So, StepCount > 3 means 4 or more counted steps.
            return StepCount > 3;
        }

        // --- Placeholder methods (potentially moved from old SimPlayer or added)
        public void MarkAsPassRecipient()
        {
            ReceivedPassRecently = true;
            // Potentially add a timer to reset this flag automatically
        }

        public void ClearPassRecipientFlag()
        {
            ReceivedPassRecently = false;
        }

        public void SetAction(PlayerAction action, SimPlayer targetPlayer = null, Vector2 targetPosition = default, float duration = 0f)
        {
            if (isStumbling || IsSuspended()) { // Use the new IsSuspended method
                 // Keep current action (likely Idle)
                  return;
            }

            CurrentAction = action;
            ActionTargetPlayer = targetPlayer;
            TargetPosition = (targetPosition == default && targetPlayer != null) ? targetPlayer.Position : targetPosition;
            ActionTimer = duration;

            // Reset flags or states when starting certain actions
            if (action != PlayerAction.ReceivingPass) {
                  ClearPassRecipientFlag();
            }
            // Example: Ensure not dribbling if starting a pass/shot
            if (action == PlayerAction.PreparingPass || action == PlayerAction.Passing || action == PlayerAction.PreparingShot || action == PlayerAction.Shooting) {
                  if (IsDribbling) { EndDribble(); } // Stop dribbling to shoot/pass
            }


            // Debug.Log($"Player {BaseData.UniqueID} action set to {action} (Timer: {duration})");
        }

        public override string ToString()
        {
            string status = IsSuspended() ? "SUSPENDED" : IsOnCourt ? "On Court" : "Bench"; // Use the new IsSuspended method
            string ball = HasBall ? " (HasBall)" : "";
            // Use TeamId directly
            return $"SimPlayer {BaseData.PlayerID} [{BaseData?.FullName ?? "Unknown"}] Team:{TeamId} Pos:{Position:F1} Action:{CurrentAction} {status}{ball} Stamina: {Stamina:P0}";
        }
    }

    // --- Enums (Consider moving to a dedicated Enums file if not already done) ---
    public enum PlayerAction
    {
        GoalkeeperSaving,
        Idle,
        MovingToPosition,
        MovingWithBall, // Generic movement while holding ball
        Dribbling,
        ReceivingPass,
        PreparingPass,
        Passing,
        PreparingShot,
        Shooting,
        JumpingForShot,
        Landing,
        Tackling,
        Intercepting,
        Blocking,
        DefendingPlayer, // Marking or zonal defense
        WaitingForPass,
        SettingScreen, // << ADDED
        ReturningToDefense,
        CelebratingGoal,
        ArguingWithRef // :)
    }
}
