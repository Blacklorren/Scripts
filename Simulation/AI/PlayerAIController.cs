using UnityEngine;
using Zenject; // Added for InjectAttribute

using HandballManager.Gameplay;
using HandballManager.Data;
using HandballManager.Simulation.AI.Decision; // Updated from DecisionMakers to Decision
using static HandballManager.Simulation.AI.Decision.DefaultOffensiveDecisionMaker; // For ScreenDecisionData/ScreenUseData
using HandballManager.Simulation.AI.Evaluation; // Updated from Evaluators to Evaluation
using HandballManager.Simulation.AI.Positioning;  // Correct
using HandballManager.Simulation.Physics;         // Correct
using System;
using System.Linq;
using HandballManager.Simulation.Engines;
using HandballManager.Core;
using HandballManager.Simulation.Utils;

namespace HandballManager.Simulation.AI
{
    /// <summary>
    /// Implementation of the IPlayerAIController that orchestrates AI decisions for all players in a match.
    /// </summary>
    public class PlayerAIController : IPlayerAIController 
    {
        // --- For AI integration of tactical substitutions ---
        private MatchSimulator _matchSimulator;
        /// <summary>
        /// Public setter for injecting the match simulator (necessary for AI to trigger substitutions).
        /// </summary>
        public void SetMatchSimulator(MatchSimulator simulator)
        {
            _matchSimulator = simulator;
        }

    
        #region Constants
        // General Thresholds/Timing relevant to the Controller's orchestration
        private const float DIST_TO_TARGET_IDLE_THRESHOLD = 0.5f; // Distance within target to consider arrived/idle
        private const float DIST_TO_TARGET_MOVE_TRIGGER_THRESHOLD = 0.6f; // Min distance difference to trigger a move command
        private const float BASE_ACTION_THRESHOLD = 0.35f; // Minimum weighted score needed to attempt *any* specific action over just moving/holding
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f; // Squared distance threshold for near-zero checks

        // Action Timers
        private const float SHOT_PREP_TIME_BASE = 0.6f;
        private const float SHOT_PREP_TIME_RANDOM_FACTOR = 0.3f;
        private const float PASS_PREP_TIME_BASE = 0.4f;
        private const float PASS_PREP_TIME_RANDOM_FACTOR = 0.2f;
        private const float GK_PASS_PREP_TIME_BASE = 0.6f;
        private const float GK_PASS_PREP_TIME_RANDOM_FACTOR = 0.2f;
        private const float TACKLE_PREP_TIME = 0.3f;
        private const float GK_PASS_ATTEMPT_THRESHOLD = 0.5f; // GK's base threshold for passing vs holding

        // Reaction/Movement Constants
        private const float LOOSE_BALL_REACTION_RANGE_MULTIPLIER = 1.8f;
        private const float ARRIVAL_VELOCITY_DAMPING_FACTOR = 0.5f;
        private const float PREP_VELOCITY_DAMPING_FACTOR = 0.1f;
        private const float MIN_ACTION_TIMER = 0.5f; // More realistic: 500 ms to simulate human decision-making time

        // --- Timeout management ---
        private const float TIMEOUT_TRIGGER_THRESHOLD = 0.9f; // Threshold for triggering a timeout
        private const float TIMEOUT_COOLDOWN_SECONDS = 30f; // Cooldown time between timeouts
        #endregion

        #region Dependencies (Injected)
        private readonly ITacticPositioner _tacticPositioner;
        private readonly IGoalkeeperPositioner _gkPositioner;
        private readonly IGeometryProvider _geometry;
        // Removed unused decision makers
        private readonly ITacticalEvaluator _tacticalEvaluator;
        private readonly IPersonalityEvaluator _personalityEvaluator;
        private readonly IGameStateEvaluator _gameStateEvaluator;
        private readonly IBallPhysicsCalculator _ballPhysics;
        // IOffensiveDecisionMaker removed - Logic moved to OffensiveAIController
        // Injected role-specific controllers
        private readonly IOffensiveAIController _offensiveAIController;
        private readonly IDefensiveAIController _defensiveAIController; // Keep this for now if still used elsewhere, or remove if fully replaced
        private readonly IGoalkeeperAIController _goalkeeperAIController;
        private readonly JumpSimulator _jumpSimulator; // Added dependency for jump decisions
        #endregion

        #region Constructor
        [Inject]
        public PlayerAIController(
            ITacticPositioner tacticPositioner, // Use interface type
            IGoalkeeperPositioner gkPositioner,
            IGeometryProvider geometry,
            // Removed unused decision makers
            ITacticalEvaluator tacticalEvaluator,
            IPersonalityEvaluator personalityEvaluator,
            IGameStateEvaluator gameStateEvaluator,
            IBallPhysicsCalculator ballPhysics,
            // IOffensiveDecisionMaker removed
            // Inject new controllers
            IOffensiveAIController offensiveAIController,
            IDefensiveAIController defensiveAIController, // Keep param if needed
            IGoalkeeperAIController goalkeeperAIController,
            JumpSimulator jumpSimulator // Added parameter for jump simulator
            )
        {
            _tacticPositioner = tacticPositioner;
            _gkPositioner = gkPositioner;
            _geometry = geometry;
            // Removed assignments for unused decision makers
            _tacticalEvaluator = tacticalEvaluator;
            _personalityEvaluator = personalityEvaluator;
            _gameStateEvaluator = gameStateEvaluator;
            _ballPhysics = ballPhysics;
            // _offensiveDecisionMaker assignment removed

            // Assign new controllers
            _offensiveAIController = offensiveAIController;
            _defensiveAIController = defensiveAIController; // Keep assignment if needed
            _goalkeeperAIController = goalkeeperAIController;
            _jumpSimulator = jumpSimulator; // Initialize jump simulator

            Debug.Log("PlayerAIController initialized with role-specific controllers.");
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the base preparation time for a given player action.
        /// </summary>
        private float GetPrepTime(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Shooting:
                    return SHOT_PREP_TIME_BASE;
                case PlayerAction.Passing:
                    return PASS_PREP_TIME_BASE;

                case PlayerAction.Tackling:
                    return TACKLE_PREP_TIME;
                default:
                    return MIN_ACTION_TIMER;
            }
        }

        /// <summary>
        /// Returns the randomization factor for the preparation time of a given player action.
        /// </summary>
        private float GetPrepTimeRandomFactor(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Shooting:
                    return SHOT_PREP_TIME_RANDOM_FACTOR;
                case PlayerAction.Passing:
                    return PASS_PREP_TIME_RANDOM_FACTOR;

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Returns true if the given action can be interrupted by a new decision.
        /// </summary>
        private bool IsInterruptible(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Idle:
                case PlayerAction.MovingToPosition:
                case PlayerAction.MovingWithBall:
                case PlayerAction.ReceivingPass:
                case PlayerAction.DefendingPlayer:
                case PlayerAction.WaitingForPass:
                case PlayerAction.ReturningToDefense:
                    return true; // Interruptible actions
                case PlayerAction.Shooting:
                case PlayerAction.Passing:
                case PlayerAction.Tackling:
                case PlayerAction.JumpingForShot:
                case PlayerAction.Landing:
                case PlayerAction.Blocking:
                case PlayerAction.Intercepting:
                case PlayerAction.CelebratingGoal:
                case PlayerAction.ArguingWithRef:
                    return false; // Not interruptible
                default:
                    return true; // Default to interruptible
            }
        }

        /// <summary>
        /// Decides the action for a goalkeeper based on the match state and tactic.
        /// </summary>
        private void DecideGoalkeeperAction(SimPlayer player, MatchState state, Tactic tactic, bool hasBall)
        {
            if (_goalkeeperAIController != null)
            {
                // Delegate to the injected goalkeeper AI controller
                PlayerAction action = _goalkeeperAIController.DetermineGoalkeeperAction(state, player);
                SetPlayerAction(player, action, GetPrepTime(action), GetPrepTimeRandomFactor(action), state);
            }
            else
            {
                // Fallback: just idle if no controller
                SetPlayerAction(player, PlayerAction.Idle, MIN_ACTION_TIMER, 0f, state);
            }
        }

        /// <summary>
        /// Updates decisions for all players currently on the court. Iterates a copy for safety.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="timeStep">The time step for the simulation update in seconds.</param>
        // --- LOD AI Update Scheduler ---
        private AIUpdateScheduler _aiUpdateScheduler = new AIUpdateScheduler();

        public void UpdatePlayerDecisions(MatchState state, float timeStep)
        {
            if (state == null)
            {
                Debug.LogError("[PlayerAIController] UpdatePlayerDecisions called with null state.");
                return;
            }

            // --- AI ADDITION: Tactical substitution management ---
            EvaluateAndPerformSubstitutions(state);

            // --- AI ADDITION: Timeout management ---
            TryTriggerTimeoutIfNeeded(state);

            float currentTime = Time.time;

            foreach (var player in state.PlayersOnCourt.ToList())
            {
                if (player == null) continue;
                try
                {
                    // LOD: Only update if scheduler allows
                    if (!_aiUpdateScheduler.ShouldUpdatePlayer(player, state, currentTime))
                        continue;

                    Tactic tactic = (player.TeamSimId == 0) ? state.HomeTactic : state.AwayTactic;

                    // --- Fatigue Check for AI Decision Penalty ---
                    float stamina = player.Stamina;
                    float aiFatigueThreshold = 0.25f; // Penalty applies below 25% stamina
                    float maxAIFatiguePenalty = 0.15f; // Max 15% reduction in effectiveness scores
                    float aiFatiguePenaltyFactor = 1.0f; // Default: no penalty
                    if (stamina < aiFatigueThreshold)
                    {
                        // Non-linear scaling: penalty increases as stamina drops further below threshold
                        aiFatiguePenaltyFactor = 1.0f - (Mathf.Pow((aiFatigueThreshold - stamina) / aiFatigueThreshold, 1.5f) * maxAIFatiguePenalty);
                    }
                    // NOTE: This aiFatiguePenaltyFactor (value between ~0.85 and 1.0) should be multiplied
                    // with scores derived from mental attributes (like DecisionMaking, Concentration)
                    // within the specific decision-making logic (e.g., inside Offensive/Defensive/GK controllers
                    // or evaluators) where those attributes are used. Applying it globally here is less targeted.
                    // For now, we'll log it as a placeholder for where it *could* be applied.
                    // Example usage (inside a specific decision logic):
                    // float decisionScore = CalculateDecisionScore(player.BaseData.DecisionMaking * aiFatiguePenaltyFactor, ...);

                    // --- Handle Specific Game Phases First ---
                    switch (state.CurrentPhase)
                    {
                        case GamePhase.HomeSetPiece:
                        case GamePhase.AwaySetPiece:
                            // Offensive players handle set pieces (shooter/passer)
                            if (player.HasBall && _offensiveAIController != null)
                            {
                                var (action, target) = _offensiveAIController.HandleSetPieceAction(state, player, tactic);
                                SetPlayerAction(player, action, GetPrepTime(action), GetPrepTimeRandomFactor(action), state);
                                player.TargetPlayer = target; // Set target if passing
                            }
                            else // Other players (including defenders) just position
                            {
                                SetPlayerToMoveToTacticalPosition(player, state, tactic);
                                player.PlannedAction = PlayerAction.MovingToPosition;
                            }
                            // Update LookDirection and schedule next update even if action decided here
                            SimulationUtils.UpdateLookDirectionToBallOrOpponent(player, state);
                            _aiUpdateScheduler.ScheduleNextUpdate(player, state, currentTime);
                            continue; // Skip general logic for this player

                        case GamePhase.HomePenalty:
                        case GamePhase.AwayPenalty:
                            int takerId = tactic?.PrimaryPenaltyTakerPlayerID ?? -1;
                            bool isTaker = player.GetPlayerId() == takerId || player.HasBall; // Fallback if taker ID is missing
                            bool isGk = player.AssignedTacticalRole == PlayerPosition.Goalkeeper;

                            if (isTaker && _offensiveAIController != null)
                            {
                                PlayerAction action = _offensiveAIController.HandlePenaltyAction(state, player);
                                SetPlayerAction(player, action, GetPrepTime(action), GetPrepTimeRandomFactor(action), state);
                            }
                            else if (isGk && _goalkeeperAIController != null)
                            {
                                PlayerAction action = _goalkeeperAIController.HandlePenaltySaveAction(state, player);
                                SetPlayerAction(player, action, GetPrepTime(action), GetPrepTimeRandomFactor(action), state);
                            }
                            else // Other players are idle during penalty
                            {
                                SetPlayerAction(player, PlayerAction.Idle, MIN_ACTION_TIMER, 0f, state);
                            }
                            // Update LookDirection and schedule next update even if action decided here
                            SimulationUtils.UpdateLookDirectionToBallOrOpponent(player, state);
                            _aiUpdateScheduler.ScheduleNextUpdate(player, state, currentTime);
                            continue; // Skip general logic for this player
                    }
                    // --- End Specific Game Phases ---

                    DecidePlayerAction(player, state, tactic); // Calls DecideOffensiveAction internally

                    // --- Loose ball pursuit: Only the closest player to the loose ball will chase it ---
                    if (state.Ball != null && state.Ball.IsLoose && player.PlannedAction == PlayerAction.MovingToPosition && IsClosestToLooseBall(player, state))
                    {
                        // Set the target position to the ball position for the closest player
                        // Convert 3D ball position (x,y,z) to 2D ground position (x,z) to match player's 2D position (x,y)
                        player.TargetPosition = new Vector2(state.Ball.Position.x, state.Ball.Position.z);
                    }

                    // Update LookDirection to face ball or nearest opponent
                    SimulationUtils.UpdateLookDirectionToBallOrOpponent(player, state);

                    // Schedule next update for this player
                    _aiUpdateScheduler.ScheduleNextUpdate(player, state, currentTime);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayerAIController] Error updating player {player?.GetPlayerId()}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// AI logic to trigger a timeout if tactical/urgency conditions are met.
        /// Triggers timeout if team is behind by 2+ goals in last 5 minutes and has timeouts left.
        /// </summary>
        private void TryTriggerTimeoutIfNeeded(MatchState state)
        {
            if (_matchSimulator == null || state == null || state.CurrentPhase == GamePhase.Timeout)
                return;

            float matchTime = state.MatchTimeSeconds;
            float matchDuration = state.MatchDurationSeconds;
            float timeLeft = matchDuration - matchTime;
            bool isFirstHalf = matchTime < matchDuration / 2f;
            bool isLast5Min = timeLeft <= 300f;
            float windowMinutes = isLast5Min ? 5f : 3f; // On peut être plus strict sur la dernière période
            int goalDiffThreshold = 3; // Peut être 4 selon la sévérité voulue

            // HOME
            if (state.PossessionTeamId == 0 && state.CanTriggerTimeout(0, matchTime, matchDuration))
            {
                int diff = state.GetGoalDifferentialLastMinutes(0, matchTime, windowMinutes);
                if (diff >= goalDiffThreshold)
                {
                    _matchSimulator.TriggerTimeout(0);
                    state.RegisterTimeoutUsage(0, matchTime, matchDuration);
                    state.HomeTimeoutsRemaining--;
                    return;
                }
            }
            // AWAY
            if (state.PossessionTeamId == 1 && state.CanTriggerTimeout(1, matchTime, matchDuration))
            {
                int diff = state.GetGoalDifferentialLastMinutes(1, matchTime, windowMinutes);
                if (diff >= goalDiffThreshold)
                {
                    _matchSimulator.TriggerTimeout(1);
                    state.RegisterTimeoutUsage(1, matchTime, matchDuration);
                    state.AwayTimeoutsRemaining--;
                    return;
                }
            }
        }

        /// <summary>
        /// Analyzes the match state and triggers a tactical substitution if necessary (AI).
        /// Criteria: Checks for low stamina, injury, or poor performance based on recent stats.
        /// </summary>
        // --- Substitution Cooldown State ---
        private float _lastSubstitutionTimeHome = -999f;
        private float _lastSubstitutionTimeAway = -999f;
        private const float SUB_COOLDOWN_SECONDS = 30f; // Prevent more than one sub per team per 30s

        private void EvaluateAndPerformSubstitutions(MatchState state)
        {
            if (_matchSimulator == null || state == null)
                return;
            float now = state.MatchTimeSeconds; // Assumes MatchState exposes current time in seconds

            // Home team
            if (now - _lastSubstitutionTimeHome >= SUB_COOLDOWN_SECONDS)
            {
                foreach (var playerOut in state.HomePlayersOnCourt)
                {
                    if (playerOut == null || playerOut.IsSuspended() || !playerOut.IsOnCourt)
                        continue;

                    // --- Substitution Criteria (Mirrors Away Team Logic) ---
                    bool needsSub = false;
                    // 1. Stamina
                    if (playerOut.Stamina < 0.1f)
                        needsSub = true;
                    // 2. Injury
                    else if (playerOut.BaseData != null && playerOut.BaseData.IsInjured(state.MatchDate))
                        needsSub = true;
                    // 3. Performance (Based on recent stats)
                    else if (playerOut.BaseData != null && state.PlayerStats.TryGetValue(playerOut.GetPlayerId(), out var stats))
                    {
                        int missedShots = stats.ShotsTaken - stats.ShotsOnTarget;
                        if (missedShots >= 3 || stats.Turnovers >= 2 || stats.FoulsCommitted >= 3)
                            needsSub = true;
                    }
                    // 4. Tactical (placeholder: e.g., role mismatch)
                    // TODO: Integrate tactical evaluator for smarter checks

                    if (!needsSub)
                        continue;

                    // --- Find Bench Candidate ---
                    var candidate = state.HomeBench
                        .Where(p => p != null
                                    && !p.IsSuspended()
                                    && !p.IsOnCourt
                                    && !p.BaseData.IsInjured(state.MatchDate)
                                    && p.Stamina > 0.5f
                                    && (p.BaseData.PrimaryPosition == playerOut.AssignedTacticalRole
                                        || p.BaseData.PrimaryPosition == playerOut.BaseData.PrimaryPosition))
                        .OrderByDescending(p => p.Stamina)
                        .FirstOrDefault();

                    if (candidate != null)
                    {
                        // Assign the specific formation role from the outgoing player to the incoming one
                        candidate.AssignedFormationSlotRole = playerOut.AssignedFormationSlotRole;

                        if (_matchSimulator.TrySubstitute(playerOut, candidate))
                        {
                            _lastSubstitutionTimeHome = now;
                            // Stats are NOT reset upon substitution
                            break; // Only one sub per tick per team
                        }
                    }
                }
            }

            // Away team
            if (now - _lastSubstitutionTimeAway >= SUB_COOLDOWN_SECONDS)
            {
                foreach (var playerOut in state.AwayPlayersOnCourt)
                {
                    if (playerOut == null || playerOut.IsSuspended() || !playerOut.IsOnCourt)
                        continue;

                    bool needsSub = false;
                    if (playerOut.Stamina < 0.1f)
                        needsSub = true;
                    else if (playerOut.BaseData != null && playerOut.BaseData.IsInjured(state.MatchDate))
                        needsSub = true;
                    else if (playerOut.BaseData != null && state.PlayerStats.TryGetValue(playerOut.GetPlayerId(), out var stats))
                    {
                        int missedShots = stats.ShotsTaken - stats.ShotsOnTarget;
                        if (missedShots >= 3 || stats.Turnovers >= 2 || stats.FoulsCommitted >= 3)
                            needsSub = true;
                    }
                    // TODO: Tactical needs

                    if (!needsSub)
                        continue;

                    var candidate = state.AwayBench
                        .Where(p => p != null
                                    && !p.IsSuspended()
                                    && !p.IsOnCourt
                                    && !p.BaseData.IsInjured(state.MatchDate)
                                    && p.Stamina > 0.5f
                                    && (p.BaseData.PrimaryPosition == playerOut.AssignedTacticalRole
                                        || p.BaseData.PrimaryPosition == playerOut.BaseData.PrimaryPosition))
                        .OrderByDescending(p => p.Stamina)
                        .FirstOrDefault();

                    if (candidate != null)
                    {
                        // Assign the specific formation role from the outgoing player to the incoming one
                        candidate.AssignedFormationSlotRole = playerOut.AssignedFormationSlotRole;

                        if (_matchSimulator.TrySubstitute(playerOut, candidate))
                        {
                            _lastSubstitutionTimeAway = now;
                            // Stats are NOT reset upon substitution
                            break;
                        }
                    }
                }
            }
        }
        #endregion
        #region Private Decision Logic
        /// <summary>Decides the primary action for a player based on their role and ball possession.</summary>
        private void DecidePlayerAction(SimPlayer player, MatchState state, Tactic tactic)
        {
            if (player == null || state == null || tactic == null) return;

            // --- Phase-Specific Overrides --- 
            if (player.ActionTimer > 0 && !IsInterruptible(player.PlannedAction))
            {
                return; // Don't interrupt non-interruptible actions
            }

            switch (state.CurrentPhase)
            {
                case GamePhase.HomePenalty:
                case GamePhase.AwayPenalty:
                    DecidePenaltyAction(player, state); // Restore call, remove tactic arg if DecidePenaltyAction doesn't need it
                    return; // Handled by penalty logic

                case GamePhase.HomeSetPiece:
                case GamePhase.AwaySetPiece:
                    DecideSetPieceAction(player, state, tactic); // Restore call
                    return; // Handled by set piece logic
            }

            // --- Default Action Logic (based on ball possession/role) ---
            bool hasBall = state.Ball.Holder?.PlayerID == player.PlayerID;

            // Goalkeeper Logic - Restore call to helper
            if (player.BaseData.PrimaryPosition == PlayerPosition.Goalkeeper) // Use BaseData.PrimaryPosition for role check
            {
                DecideGoalkeeperAction(player, state, tactic, hasBall);
            }
            // Field Player Logic
            else
            {
                if (hasBall)
                {
                    DecideOffensiveAction(player, state, tactic); // Restore call
                }
                else
                {
                    DecideOffBallAction(player, state, tactic); // Restore call
                }
            }
        }

        /// <summary>Decides player actions during a penalty phase.</summary>
        private Vector2 ChoosePenaltyShotTarget(SimPlayer shooter, SimPlayer goalkeeper, MatchState state)
        {
            // Simple logic: aim for the center of the goal, slightly offset based on handedness
            // (Assumes goal is at positive X for Away, negative X for Home)
            var pitch = _geometry; // Use injected geometry provider
            float goalY = 0f; // Center
            float goalX;
            if (shooter.TeamId == 0) // Home team shoots right
                goalX = pitch?.AwayGoalCenter3D.x ?? 20f;
            else
                goalX = pitch?.HomeGoalCenter3D.x ?? -20f;

            float offset = 0.7f; // Offset for handedness
            if (shooter.BaseData.PreferredHand == Handedness.Left)
                goalY += offset;
            else if (shooter.BaseData.PreferredHand == Handedness.Right)
                goalY -= offset;
            // Otherwise, center
            return new Vector2(goalX, goalY);
        }

        /// <summary>
        /// Determines the dive target for the goalkeeper during a penalty.
        /// Simple logic: randomly choose left, center, or right, or bias based on shooter's handedness if available.
        /// </summary>
        private Vector2 ChoosePenaltyDiveTarget(SimPlayer goalkeeper, SimPlayer shooter, MatchState state)
        {
            var pitch = _geometry;
            float goalX = (goalkeeper.TeamId == 0) ? pitch?.HomeGoalCenter3D.x ?? -20f : pitch?.AwayGoalCenter3D.x ?? 20f;
            float[] yOffsets = { -1.0f, 0f, 1.0f }; // left, center, right (relative to goal center)
            float goalY = 0f;

            // Bias: If shooter is left-handed, GK might favor diving to GK's left (goal's right)
            int bias = 1; // 0=left, 1=center, 2=right
            if (shooter != null && shooter.BaseData != null)
            {
                if (shooter.BaseData.PreferredHand == Handedness.Left)
                    bias = 2; // right
                else if (shooter.BaseData.PreferredHand == Handedness.Right)
                    bias = 0; // left
                else
                    bias = 1; // center
            }
            // Add randomness
            int diveChoice = UnityEngine.Random.value < 0.5f ? bias : UnityEngine.Random.Range(0, 3);
            goalY = yOffsets[diveChoice];
            // Scale Y offset to match goal width (assume 3m width, adjust as needed)
            float goalWidth = pitch?.GoalWidth ?? 3f;
            goalY = (pitch != null ? ((goalkeeper.TeamId == 0 ? pitch.HomeGoalCenter3D.y : pitch.AwayGoalCenter3D.y) + goalY * (goalWidth / 2f * 0.85f)) : goalY * 1.2f);
            return new Vector2(goalX, goalY);
        }

        private void DecidePenaltyAction(SimPlayer player, MatchState state)
        {
             SimPlayer shooter = state.AllPlayers.Values.FirstOrDefault(p => p != null && p.HasBall);
bool isShooter = shooter != null && shooter.PlayerID == player.PlayerID;
             bool isGoalkeeper = player.BaseData.PrimaryPosition == PlayerPosition.Goalkeeper; // Check role

             if (isShooter)
             {
                  // Shooter Logic: Choose target and prepare shot
                  SimPlayer opponentGk = state.GetGoalkeeper(player.TeamId == 0 ? 1 : 0);
                  Vector2 targetGoalPos = ChoosePenaltyShotTarget(player, opponentGk, state); // Assuming ChoosePenaltyShotTarget exists
                  SetPlayerAction(player, PlayerAction.PreparingShot, GetPrepTime(PlayerAction.Shooting), GetPrepTimeRandomFactor(PlayerAction.Shooting), state, targetGoalPos);
             }
             else if (isGoalkeeper)
             {
                 // Goalkeeper Logic: Use the controller
                 if (_goalkeeperAIController != null)
                 {
                     // Pass SimPlayer to HandlePenaltySaveAction
                     PlayerAction gkAction = _goalkeeperAIController.HandlePenaltySaveAction(state, player);
                     Vector2 diveTarget = ChoosePenaltyDiveTarget(player, shooter, state); // Pass shooter SimPlayer if available
                     SetPlayerAction(player, gkAction, 0f, 0f, state, diveTarget); // Immediate action for save
                 }
                 else
                 {
                     Debug.LogWarning($"[PlayerAIController] GoalkeeperAIController not injected for {player.BaseData?.FullName} during penalty.");
                     SetPlayerAction(player, PlayerAction.Idle, MIN_ACTION_TIMER, 0f, state); // Fallback
                 }
             }
             else
             {
                  // Other players: Idle
                  SetPlayerAction(player, PlayerAction.Idle, MIN_ACTION_TIMER, 0f, state);
             }
        }

        private void DecideSetPieceAction(SimPlayer player, MatchState state, Tactic tactic)
        {
             // Logic based on version after SimPlayer update but before corruption
             bool isHomeSetPiece = state.CurrentPhase == GamePhase.HomeSetPiece;
             bool playerIsOnAttackingTeam = (isHomeSetPiece && player.TeamId == 0) || (!isHomeSetPiece && player.TeamId == 1);

             SimPlayer thrower = state.Ball.Holder;
             if (thrower == null) {
                 if (player.PlannedAction != PlayerAction.Idle)
                    SetPlayerAction(player, PlayerAction.Idle, MIN_ACTION_TIMER, 0f, state);
                 return;
             }

             if (player == thrower)
             {
                 // Thrower Logic: Use Offensive AI Controller
                 if (_offensiveAIController != null && player.PlannedAction != PlayerAction.PreparingShot && player.PlannedAction != PlayerAction.Shooting &&
                     player.PlannedAction != PlayerAction.PreparingPass && player.PlannedAction != PlayerAction.Passing)
                 {
                    // Delegate decision entirely to the Offensive controller
                    var (action, targetPlayer) = _offensiveAIController.HandleSetPieceAction(state, player, tactic);
                    SetPlayerAction(player, action, MIN_ACTION_TIMER, 0f, state);
                    Debug.Log($"[AI SetPiece] Thrower {player.BaseData?.FullName} decided action: {action}");
                 }
                 else if (_offensiveAIController == null)
                 {
                    Debug.LogWarning("[PlayerAIController] OffensiveAIController not injected for set piece thrower.");
                    SetPlayerAction(player, PlayerAction.Idle, MIN_ACTION_TIMER, 0f, state); // Fallback
                 }
             }
             else if (!playerIsOnAttackingTeam && player.BaseData.PrimaryPosition != PlayerPosition.Goalkeeper) // Use BaseData for role
             {
                 // Defender Logic: Attempt block or position
                 if (_defensiveAIController != null)
                 {
                    // Let defensive controller handle block/positioning decision
                    PlayerAction defensiveAction = _defensiveAIController.DetermineDefensiveAction(state, player, tactic); // Pass SimPlayer
                    Vector2 targetPosition = _defensiveAIController.CalculateDefensivePosition(state, player, tactic); // Pass SimPlayer

                    if (defensiveAction == PlayerAction.Blocking)
                    {
                         // Check legality before setting block action
                         float distanceToBallSqr = (player.Position - new Vector2(state.Ball.Position.x, state.Ball.Position.y)).sqrMagnitude;
                         bool isLegalDistance = distanceToBallSqr > (SimConstants.SET_PIECE_DEFENDER_DISTANCE * SimConstants.SET_PIECE_DEFENDER_DISTANCE);
                         if(isLegalDistance)
                         {
                             SetPlayerAction(player, PlayerAction.Blocking, GetPrepTime(PlayerAction.Blocking), GetPrepTimeRandomFactor(PlayerAction.Blocking), state, state.Ball.Position);

                             // Jump logic (if needed, should be handled by Defensive AI or Action Resolver?)
                              if (_jumpSimulator != null && JumpDecisionUtils.ShouldJumpForBlock(player, state)) // Pass SimPlayer
                              {
                                  float verticalVelocity = CalculateJumpVerticalVelocity(player); // Pass SimPlayer
                                  _jumpSimulator.StartJump();
                                  Debug.Log($"[AI SetPiece] Defender {player.BaseData?.FullName} is JUMPING to block.");
                              }

                              Debug.Log($"[AI SetPiece] Defender {player.BaseData?.FullName} attempting block.");
                         }
                         else // Too close, must reposition
                         {
                             SetPlayerToMoveToTacticalPosition(player, state, tactic); // Reposition if illegal block
                         }
                    }
                    else if (defensiveAction == PlayerAction.MovingToPosition)
                    {
                         SetPlayerToMoveToTacticalPosition(player, state, tactic);
                    }
                    else // Idle or other action
                    {
                         SetPlayerAction(player, PlayerAction.Idle, MIN_ACTION_TIMER, 0f, state);
                    }
                 }
                 else
                 {
                    Debug.LogWarning("[PlayerAIController] DefensiveAIController not injected for set piece defender.");
                    SetPlayerToMoveToTacticalPosition(player, state, tactic); // Fallback positioning
                 }
             }
             else if (player.BaseData.PrimaryPosition != PlayerPosition.Goalkeeper) // Other attackers or distant defenders
             {
                 // Move to tactical position if not already moving or idle
                 if (player.PlannedAction != PlayerAction.MovingToPosition && player.PlannedAction != PlayerAction.Idle)
                 {
                    SetPlayerToMoveToTacticalPosition(player, state, tactic);
                 }
             }
             // GK logic is handled by DecideGoalkeeperAction, called from DecidePlayerAction
        }

        /// <summary>Decides the action for an offensive player *with* the ball.</summary>
        private void DecideOffensiveAction(SimPlayer player, MatchState state, Tactic tactic)
        {
            // Restore OffensiveAction logic (using SimPlayer)
            if (player == null || state == null || tactic == null || !player.HasBall) return;

            if (_offensiveAIController != null)
            {
                // Delegate the core decision logic to the OffensiveAIController
                PlayerAction action = _offensiveAIController.DetermineOffensiveAction(state, player);
SetPlayerAction(player, action, GetPrepTime(action), GetPrepTimeRandomFactor(action), state);

                // PlayerAIController still handles *setting* the action based on the decision made by the OffensiveAIController
                // Assume DecideAction sets player.PlannedAction, TargetPosition, TargetPlayer internally or PlayerAIController retrieves them
                Debug.Log($"[PlayerAIController] Offensive action for {player.BaseData?.FullName}: {player.PlannedAction}");
            }
            else
            {
                Debug.LogWarning("[PlayerAIController] OffensiveAIController not injected for offensive action.");
                SetPlayerToMoveToTacticalPosition(player, state, tactic); // Fallback
            }
        }

        /// <summary>Decides the best action for a player *without* the ball.</summary>
        private void DecideOffBallAction(SimPlayer player, MatchState state, Tactic tactic)
        {
            // Restore OffBallAction logic (using SimPlayer)
            if (player == null || state == null || tactic == null || player.HasBall) return;

            bool ownTeamHasPossession = state.PossessionTeamId == player.TeamSimId;

            if (ownTeamHasPossession)
            {
                // Offensive Off-Ball Logic
                if (_offensiveAIController != null)
                {
                    // Delegate decision AND positioning to Offensive Controller
                     var decision = _offensiveAIController.DetermineOffBallOffensiveAction(state, player, tactic);
                    player.PlannedAction = decision.Action;
                    if (decision.ScreenData.HasValue)
                    {
                        var screenUseData = new DefaultOffensiveDecisionMaker.ScreenUseData {
                            Screener = decision.ScreenData.Value.Screener,
                            User = decision.ScreenData.Value.User,
                            UseSpot = decision.ScreenData.Value.ScreenSpot,
                            EffectivenessAngle = decision.ScreenData.Value.EffectivenessAngle
                        };
                        player.SetCurrentScreenUseData(screenUseData);
                    }

                    // Assume OffensiveAIController handles setting the action/target internally
                     Debug.Log($"[PlayerAIController] Off-ball offensive action for {player.BaseData?.FullName}: {player.PlannedAction}");
                }
                else
                {
                     Debug.LogWarning("[PlayerAIController] OffensiveAIController not injected for off-ball offensive action.");
                     SetPlayerToMoveToTacticalPosition(player, state, tactic); // Fallback positioning
                }
            }
            else // Opponent has possession or ball is loose
            {
                // Defensive Off-Ball Logic
                 if (_defensiveAIController != null)
                 {
                    // Delegate decision AND positioning to Defensive Controller
                     PlayerAction action = _defensiveAIController.DetermineDefensiveAction(state, player, tactic);
SetPlayerAction(player, action, GetPrepTime(action), GetPrepTimeRandomFactor(action), state);
Debug.Log($"[PlayerAIController] Off-ball defensive action for {player.BaseData?.FullName}: {player.PlannedAction}");
                 }
                 else
                 {
                      Debug.LogWarning("[PlayerAIController] DefensiveAIController not injected for off-ball defensive action.");
                      SetPlayerToMoveToTacticalPosition(player, state, tactic); // Fallback positioning
                 }
            }
        }

        /// <summary>Calculates the target position based on tactical role and game state.</summary>
        // Restore SetPlayerToMoveToTacticalPosition logic AFTER SimPlayer update (Step 1377)
        private void SetPlayerToMoveToTacticalPosition(SimPlayer player, MatchState state, Tactic tactic)
        {
            // Restore logic from Step 1413
            if (player == null || state == null || tactic == null) return;

            Vector2 targetPos;
            // Use BaseData.PrimaryPosition for role check
            if (player.BaseData.PrimaryPosition == PlayerPosition.Goalkeeper)
            {
                if (_goalkeeperAIController != null)
                {
                    // Pass SimPlayer to CalculateGoalkeeperPosition
                    targetPos = _goalkeeperAIController.CalculateGoalkeeperPosition(state, player);
                }
                else
                {
                     Debug.LogWarning($"[PlayerAIController] GoalkeeperAIController not injected for positioning {player.BaseData?.FullName}.");
                     // Fallback: Use TacticPositioner for goalkeeper positioning if necessary?
                     targetPos = _tacticPositioner.GetPlayerTargetPosition(state, player); // Fallback: use general player positioning
                }
            }
            else
            { 
                // Use TacticPositioner for field player positioning
                targetPos = _tacticPositioner.GetPlayerTargetPosition(state, player);
            }

            // --- Movement Logic (Common for GK and Field Players) ---
            float distSq = (targetPos - player.Position).sqrMagnitude;

            // Only issue a new move command if the target changed significantly or player is idle
            if (distSq > DIST_TO_TARGET_MOVE_TRIGGER_THRESHOLD * DIST_TO_TARGET_MOVE_TRIGGER_THRESHOLD || player.PlannedAction == PlayerAction.Idle)
            {
                SetPlayerAction(player, PlayerAction.MovingToPosition, 0f, 0f, state, targetPos);
            }
            else if (player.PlannedAction == PlayerAction.MovingToPosition && distSq < DIST_TO_TARGET_IDLE_THRESHOLD * DIST_TO_TARGET_IDLE_THRESHOLD)
            {
                // If already moving and close enough, switch to idle
                SetPlayerAction(player, PlayerAction.Idle, MIN_ACTION_TIMER, 0f, state);
                player.Velocity *= ARRIVAL_VELOCITY_DAMPING_FACTOR; // Dampen velocity on arrival
            }
            // Else: Continue current action (likely already MovingToPosition towards an older target, or doing something else)
        }

        /// <summary>
        /// Evaluates potential passing targets and selects the best receiver.
        /// Implementation required by IPlayerAIController.
        /// Renamed from SelectPassingTarget.
        /// </summary>
        public SimPlayer FindBestPassTarget(MatchState state, SimPlayer passer) // Updated signature to use SimPlayer
        {
             // Restore logic from Step 1413, adapted for SimPlayer signature
             // Delegate to the OffensiveAIController's implementation
            if (_offensiveAIController != null && passer != null)
            {
                // Directly use the SimPlayer passer
                SimPlayer targetSim = _offensiveAIController.FindBestPassTarget(state, passer);
                return targetSim; // Return the SimPlayer target
            }

            // Fallback or if controller/passer is null
            Debug.LogWarning("[PlayerAIController] FindBestPassTarget called, but OffensiveAIController is missing or passer is null.");
            return null;
        }

        #endregion
        #region Interface Implementations

        /// <summary>
        /// Determines the best action for a specific player based on the current game state.
        /// Implementation required by IPlayerAIController.
        /// </summary>
        public PlayerAction DeterminePlayerAction(MatchState state, SimPlayer player) // Updated signature to use SimPlayer
        {
            if (state == null || player == null) // Use SimPlayer directly
            {
                Debug.LogError("[PlayerAIController] DeterminePlayerAction called with null state or SimPlayer.");
                return PlayerAction.Idle; // Default fallback
            }

            // SimPlayer simPlayer = state.GetPlayerById(player.PlayerID); // No longer needed, player is already SimPlayer
            // if (simPlayer == null)
            // {
            //     Debug.LogWarning($"[PlayerAIController] Could not find SimPlayer for PlayerData {player.FullName} (ID: {player.PlayerID}) in DeterminePlayerAction.");
            //     return PlayerAction.Idle; // Player might not be on court
            // }

            try
            {
                // Get necessary context for the internal DecidePlayerAction
                Tactic tactic = (player.TeamSimId == 0) ? state.HomeTactic : state.AwayTactic;
                bool hasBall = state.Ball.Holder != null && state.Ball.Holder.PlayerID == player.PlayerID;

                // Call the internal decision logic using the provided SimPlayer
                DecidePlayerAction(player, state, tactic); // Pass SimPlayer directly

                // Return the action decided by the internal method
                return player.PlannedAction;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerAIController] Error in DeterminePlayerAction for {player.BaseData?.FullName}: {e}");
                // Safely fallback in case of error during decision
                if(player != null) player.PlannedAction = PlayerAction.Idle;
                return PlayerAction.Idle;
            }
        }

        #endregion

    /// <summary>
    /// Sets the action, timer, and optionally target for a SimPlayer.
    /// </summary>
    private void SetPlayerAction(SimPlayer player, PlayerAction action, float actionTimer, float randomFactor, MatchState state, Vector2? targetPosition = null, SimPlayer targetPlayer = null)
    {
        if (player == null) return;
        player.PlannedAction = action;
        player.ActionTimer = actionTimer + UnityEngine.Random.Range(0f, randomFactor);
        if (targetPosition.HasValue)
            player.TargetPosition = targetPosition.Value;
        if (targetPlayer != null)
            player.TargetPlayer = targetPlayer;
    }

        /// <summary>
    /// Calculates the vertical velocity for a player's jump based on their jumping attribute.
    /// </summary>
    /// <param name="player">The SimPlayer whose jump is being calculated.</param>
    /// <returns>Vertical velocity for the jump (m/s).</returns>
    private float CalculateJumpVerticalVelocity(SimPlayer player)
    {
        // Use player.BaseData.Jumping (0-100), default to 50 if missing
        float jumpAttribute = player.BaseData?.Jumping ?? 50f;
        float t = Mathf.Clamp01(jumpAttribute / 100f);
        return Mathf.Lerp(SimConstants.MIN_JUMP_VERTICAL_VELOCITY, SimConstants.MAX_JUMP_VERTICAL_VELOCITY, t);
    }

    /// <summary>
    /// Returns true if the player is the closest to the loose ball on the court.
    /// </summary>
    private bool IsClosestToLooseBall(SimPlayer player, MatchState state)
    {
        if (state?.Ball == null || player == null) return false;
        float minDist = float.MaxValue;
        SimPlayer closest = null;
        foreach (var p in state.PlayersOnCourt)
        {
            if (p == null) continue;
            // Convert 3D ball position (x,y,z) to 2D ground position (x,z) to match player's 2D position (x,y)
            float dist = Vector2.Distance(p.Position, new Vector2(state.Ball.Position.x, state.Ball.Position.z));
            if (dist < minDist)
            {
                minDist = dist;
                closest = p;
            }
        }
        return closest == player;
    }
}
}