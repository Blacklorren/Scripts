using UnityEngine;

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
        private const float MIN_ACTION_TIMER = 0.1f;

        // Score Adjustment Factors (Magic Number replacements)
        private const float DRIBBLING_RISK_ADJUSTMENT = 0.9f;
        private const float PASS_SAFETY_ADJUSTMENT = 0.9f; // Factor applied when dividing pass score by risk (>1 means less likely)
        private const float PASS_SAFETY_MIN_DIVISOR = 0.1f; // Minimum divisor for pass score adjustment

        // --- Phase-specific Offensive Modifiers ---
        // Transition (fast break) phase multipliers
        private const float TRANSITION_DRIBBLE_MODIFIER = 1.3f; // Encourage direct dribbling
        private const float TRANSITION_PASS_MODIFIER = 1.15f;    // Encourage direct forward passes
        private const float TRANSITION_SHOOT_MODIFIER = 1.15f;   // Encourage quick shots
        private const float TRANSITION_COMPLEX_PASS_MODIFIER = 0.8f; // Deprioritize complex passes
        private const float TRANSITION_SCREEN_MODIFIER = 0.8f;   // Deprioritize screens during fast break
        private const float TRANSITION_PREP_TIME_FACTOR = 0.85f; // Faster prep for shot/pass
        private const float TRANSITION_ACTION_THRESHOLD_FACTOR = 0.85f; // Lower threshold to act quickly

        // Positional attack phase multipliers
        private const float POSITIONAL_SCREEN_MODIFIER = 1.15f;  // Encourage tactical screens
        private const float POSITIONAL_FORMATION_PASS_MODIFIER = 1.1f; // Encourage formation-maintaining passes
        #endregion

        #region Dependencies (Injected)
        private readonly ITacticPositioner _tacticPositioner;
        private readonly IGoalkeeperPositioner _gkPositioner;
        private readonly IPassingDecisionMaker _passDecisionMaker;
        private readonly IShootingDecisionMaker _shootDecisionMaker;
        private readonly IDribblingDecisionMaker _dribbleDecisionMaker;
        private readonly IDefensiveDecisionMaker _defenseDecisionMaker;
        private readonly ITacticalEvaluator _tacticalEvaluator;
        private readonly IPersonalityEvaluator _personalityEvaluator;
        private readonly IGameStateEvaluator _gameStateEvaluator;
        private readonly IBallPhysicsCalculator _ballPhysics;
        private readonly IOffensiveDecisionMaker _offensiveDecisionMaker;
        #endregion

        #region Constructor
        public PlayerAIController(
            TacticPositioner tacticPositioner,
            IGoalkeeperPositioner gkPositioner,
            IPassingDecisionMaker passDecisionMaker,
            IShootingDecisionMaker shootDecisionMaker,
            IDribblingDecisionMaker dribbleDecisionMaker,
            IDefensiveDecisionMaker defenseDecisionMaker,
            ITacticalEvaluator tacticalEvaluator,
            IPersonalityEvaluator personalityEvaluator,
            IGameStateEvaluator gameStateEvaluator,
            IBallPhysicsCalculator ballPhysics,
            IOffensiveDecisionMaker offensiveDecisionMaker)
        {
            // Null checks for all dependencies ensure they are provided
            _tacticPositioner = tacticPositioner ?? throw new ArgumentNullException(nameof(tacticPositioner));
            _gkPositioner = gkPositioner ?? throw new ArgumentNullException(nameof(gkPositioner));
            _passDecisionMaker = passDecisionMaker ?? throw new ArgumentNullException(nameof(passDecisionMaker));
            _shootDecisionMaker = shootDecisionMaker ?? throw new ArgumentNullException(nameof(shootDecisionMaker));
            _dribbleDecisionMaker = dribbleDecisionMaker ?? throw new ArgumentNullException(nameof(dribbleDecisionMaker));
            _defenseDecisionMaker = defenseDecisionMaker ?? throw new ArgumentNullException(nameof(defenseDecisionMaker));
            _tacticalEvaluator = tacticalEvaluator ?? throw new ArgumentNullException(nameof(tacticalEvaluator));
            _personalityEvaluator = personalityEvaluator ?? throw new ArgumentNullException(nameof(personalityEvaluator));
            _gameStateEvaluator = gameStateEvaluator ?? throw new ArgumentNullException(nameof(gameStateEvaluator));
            _ballPhysics = ballPhysics ?? throw new ArgumentNullException(nameof(ballPhysics));
            _offensiveDecisionMaker = offensiveDecisionMaker ?? throw new ArgumentNullException(nameof(offensiveDecisionMaker));
        }
        #endregion

        #region Public Methods
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

                    if (player.HasBall)
                    {
                        DecidePlayerAction(player, state, tactic); // Calls DecideOffensiveAction internally
                    }
                    else
                    {
                        DecideOffBallAction(player, state, tactic);
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
        /// Decides the best off-ball action (including setting screens) for a player without the ball.
        /// </summary>
        private void DecideOffBallAction(SimPlayer player, MatchState state, Tactic tactic)
        {
            if (player?.BaseData == null || state == null || tactic == null) return;
            if (player.HasBall) return; // Defensive: Only off-ball

            // Evaluate screen opportunity
            var aiContext = new PlayerAIContext { Player = player, MatchState = state, Tactics = tactic, TacticPositioner = _tacticPositioner };
            var screenOpportunity = (_offensiveDecisionMaker as DefaultOffensiveDecisionMaker)?.EvaluateScreenOpportunity(aiContext);
            if (screenOpportunity != null && screenOpportunity.IsSuccessful && screenOpportunity.Confidence > 0.5f)
            {
                // Use new ScreenDecisionData for screen positioning
                if (screenOpportunity.Data is ScreenDecisionData screenData)
                {
                    player.TargetPlayer = screenData.User;
                    player.TargetPosition = screenData.ScreenSpot;
                }
                else
                {
                    player.TargetPlayer = screenOpportunity.Data as SimPlayer;
                }
                player.PlannedAction = PlayerAction.SettingScreen;
                SetPlayerAction(player, PlayerAction.SettingScreen, 0.0f, 0.0f, state);
                return;
            }

            // Default: move to tactical position
            SetPlayerToMoveToTacticalPosition(player, state, tactic);
        }

        /// <summary>
        /// Determines the best action for a specific player based on the current game state.
        /// Implementation of IPlayerAIController.DeterminePlayerAction
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to make a decision for.</param>
        /// <returns>The action the player should take.</returns>
        public PlayerAction DeterminePlayerAction(MatchState state, PlayerData player)
        {
            // Validation des paramètres
            if (state == null || player == null)
            {
                Debug.LogWarning("[PlayerAIController] DeterminePlayerAction called with null state or player.");
                return PlayerAction.Idle;
            }

            // Find the SimPlayer corresponding to this PlayerData
            SimPlayer simPlayer = state.PlayersOnCourt.FirstOrDefault(p => p.GetPlayerId() == player.PlayerID);
            if (simPlayer == null)
            {
                // Essayer de trouver le joueur dans AllPlayers si pas trouvé sur le terrain
                if (state.AllPlayers.TryGetValue(player.PlayerID, out simPlayer))
                {
                    if (!simPlayer.IsOnCourt)
                    {
                        // Le joueur existe mais n'est pas sur le terrain
                        return PlayerAction.Idle;
                    }
                }
                else
                {
                    // Joueur non trouvé du tout
                    Debug.LogWarning($"[PlayerAIController] Player {player.PlayerID} not found in match state.");
                    return PlayerAction.Idle;
                }
            }

            // Get the appropriate tactic
            Tactic tactic = (simPlayer.TeamSimId == 0) ? state.HomeTactic : state.AwayTactic;
            
            // Use the existing decision logic
            DecidePlayerAction(simPlayer, state, tactic);
            
            // Return the action that was decided
            return simPlayer.CurrentAction;
        }
        #endregion

        #region Core Decision Logic
        /// <summary>
        /// Core decision logic router for a single player. Delegates specific evaluations
        /// to specialized components and sets the player's intended action.
        /// </summary>
        /// <param name="player">The player making the decision.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The player's team tactic (can be null, handled gracefully).</param>
        private void DecidePlayerAction(SimPlayer player, MatchState state, Tactic tactic)
        {
            // Ensure player, essential data, and state are valid
            if (player?.BaseData == null || state == null)
            {
                 Debug.LogWarning($"[PlayerAIController] DecidePlayerAction skipped: Null player, BaseData, or state for PlayerID: {player?.GetPlayerId() ?? -1}.");
                 return;
            }

             // Gracefully handle null tactic by using a default instance
             if (tactic == null)
             {
                  tactic = Tactic.Default; // Assuming a static Default Tactic exists
                  Debug.LogWarning($"[PlayerAIController] Using default tactic for player {player.GetPlayerId()} due to null tactic provided.");
             }

            // --- Pre-Checks & State Persistence ---
            if (ShouldSkipDecision(player, state)) return; // Check suspension, active timers, receiving pass etc.

            // --- Determine Basic Game Context ---
            bool isOwnTeamPossession = player.TeamSimId == state.PossessionTeamId && state.PossessionTeamId != -1;
            bool hasBall = player.HasBall;

            // --- Action Decision Branching ---
            if (player.AssignedTacticalRole == PlayerPosition.Goalkeeper)
            {
                DecideGoalkeeperAction(player, state, tactic);
            }
            else // Field Player Decisions
            {
                // 1. Immediate Reactions (e.g., chase nearby loose ball)
                if (TryHandleReactions(player, state)) return;

                // 2. Standard Phase Actions (Offense / Defense)
                if (isOwnTeamPossession)
                {
                    DecideOffensiveAction(player, state, tactic, hasBall);
                }
                else // Opponent possession or ball is loose further away
                {
                    DecideDefensiveAction(player, state, tactic);
                }
            }

            // --- Fallback Positioning (If no specific action decided or needed) ---
            ApplyFallbackPositioning(player, state, tactic);
        }
        #endregion

        #region Decision Sub-Logic
        /// <summary>Checks conditions where AI decision should be skipped for this step.</summary>
        /// <param name="player">The player being checked.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>True if the decision logic should be skipped, false otherwise.</returns>
        private bool ShouldSkipDecision(SimPlayer player, MatchState state)
        {
            // Cannot decide if suspended
            if (player.SuspensionTimer > 0) { player.CurrentAction = PlayerAction.Suspended; player.TargetPosition = player.Position; player.TargetPlayer = null; return true; }

            // Cannot decide if preparing/executing an action
            if (player.ActionTimer > 0) { return true; }

            // Persist receiving state if the intended pass is still in flight towards this player
            if (player.CurrentAction == PlayerAction.ReceivingPass && state.Ball.IsInFlight && state.Ball.IntendedTarget == player)
            {
                // Update target position based on where the ball is predicted to go
                player.TargetPosition = _ballPhysics.EstimatePassInterceptPoint(state.Ball, player);
                return true; // Continue receiving
            }

            // Persist intercept attempt state while ball is in flight
            if (player.CurrentAction == PlayerAction.AttemptingIntercept && state.Ball.IsInFlight)
            {
                // Re-evaluate intercept viability or simply update target? Updating target is simpler for now.
                player.TargetPosition = _ballPhysics.EstimatePassInterceptPoint(state.Ball, player);
                return true; // Continue intercept attempt
            }

            // State cleanup: Clear target player if not actively marking or tackling
            if (player.CurrentAction != PlayerAction.MarkingPlayer && player.CurrentAction != PlayerAction.AttemptingTackle)
            {
                player.TargetPlayer = null;
            }

            // No reason to skip
            return false;
        }

        /// <summary>Handles simple, immediate reactions like chasing a very close loose ball.</summary>
        /// <param name="player">The player reacting.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>True if a reaction was handled, false otherwise.</returns>
        private bool TryHandleReactions(SimPlayer player, MatchState state)
        {
            // React to nearby loose ball
            if (state.Ball.IsLoose)
            {
                Vector2 ballPos2D = new Vector2(state.Ball.Position.x, state.Ball.Position.z);
                if (Vector2.Distance(player.Position, ballPos2D) < MatchSimulator.LOOSE_BALL_PICKUP_RADIUS * LOOSE_BALL_REACTION_RANGE_MULTIPLIER)
                {
                    player.PlannedAction = PlayerAction.ChasingBall;
                    player.CurrentAction = PlayerAction.ChasingBall;
                    player.TargetPosition = ballPos2D;
                    player.TargetPlayer = null; // Ensure no target player while chasing
                    return true; // Reaction handled
                }
            }
            // More complex reactions (intercept, receive) handled by ShouldSkipDecision persistence check
            return false; // No simple reaction handled
        }

        /// <summary>Determines the best offensive action for a field player.</summary>
        /// <param name="player">The player deciding.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's tactic.</param>
        /// <param name="hasBall">Whether the player currently has possession.</param>
        private void DecideOffensiveAction(SimPlayer player, MatchState state, Tactic tactic, bool hasBall)
        {
            // Added null checks for safety, though should be caught by DecidePlayerAction
            if (player?.BaseData == null || state == null || tactic == null) return;

            // --- Phase-specific Score Modifiers ---
            // These are applied after base score calculation and risk/personality modifiers
            float phaseDribbleModifier = 1f;
            float phasePassModifier = 1f;
            float phaseShootModifier = 1f;
            float phaseScreenModifier = 1f;
            float phaseComplexPassModifier = 1f;
            float prepTimeFactor = 1f;
            float actionThresholdFactor = 1f;

            // If player doesn't have the ball, their offensive action is to get into position
            if (!hasBall)
            {
                SetPlayerToMoveToTacticalPosition(player, state, tactic);
                return;
            }

            // --- Evaluate Screen Opportunities First ---
            var aiContext = new PlayerAIContext { Player = player, MatchState = state, Tactics = tactic, TacticPositioner = _tacticPositioner };
            var screenOpportunity = (_offensiveDecisionMaker as DefaultOffensiveDecisionMaker)?.EvaluateScreenOpportunity(aiContext);
if (screenOpportunity != null)
    screenOpportunity.Confidence *= phaseScreenModifier;
if (screenOpportunity != null && screenOpportunity.IsSuccessful && screenOpportunity.Confidence > 0.5f)
{
    // Use new ScreenDecisionData for screen positioning
    if (screenOpportunity.Data is ScreenDecisionData screenData)
    {
        player.TargetPlayer = screenData.User;
        player.TargetPosition = screenData.ScreenSpot;
    }
    else
    {
        player.TargetPlayer = screenOpportunity.Data as SimPlayer;
    }
    player.PlannedAction = PlayerAction.SettingScreen;
    SetPlayerAction(player, PlayerAction.SettingScreen, 0.0f, 0.0f, state); // No prep time for screen (can adjust)
    return;
}

            var useScreen = (_offensiveDecisionMaker as DefaultOffensiveDecisionMaker)?.CanUseScreen(aiContext);
if (useScreen != null)
    useScreen.Confidence *= phaseScreenModifier;
if (useScreen != null && useScreen.IsSuccessful && useScreen.Confidence > 0.5f)
{
    // Defensive AI logic here (example: marking, blocking, positioning)
    Vector2 markingTarget = Vector2.zero; // TODO: Replace with actual marking logic
    bool isJumpPlanned = player.PlannedAction == PlayerAction.Jumping || player.PlannedAction == PlayerAction.AttemptingBlock;
    bool willBeInAir = isJumpPlanned || (player.CurrentAction == PlayerAction.Jumping && player.JumpOriginatedOutsideGoalArea);
    var pitchGeometry = (_tacticPositioner as HandballManager.Simulation.AI.Positioning.TacticPositioner)?.Geometry as HandballManager.Simulation.Utils.PitchGeometryProvider;
    if (!willBeInAir && pitchGeometry != null && pitchGeometry.IsInGoalArea(new Vector3(markingTarget.x, SimConstants.BALL_RADIUS, markingTarget.y), player.TeamSimId == 0))
    {
        // Reroute around the 6m zone if not about to jump
        markingTarget = PitchGeometryProvider.CalculatePathAroundGoalArea(player.Position, markingTarget, player.TeamSimId, pitchGeometry);
    }
    // If the path to target crosses the 6m zone and not jumping, reroute
    if (!willBeInAir && pitchGeometry.WouldCrossGoalArea(player.Position, markingTarget, player.TeamSimId))
    {
        markingTarget = PitchGeometryProvider.CalculatePathAroundGoalArea(player.Position, markingTarget, player.TeamSimId, pitchGeometry);
    }
    // Use markingTarget for movement/positioning

    if (useScreen.Data is ScreenUseData useData)
    {
        player.TargetPlayer = useData.Screener;
        player.TargetPosition = useData.UseSpot;
    }
    else
    {
        player.TargetPlayer = useScreen.Data as SimPlayer;
    }
    player.PlannedAction = PlayerAction.UsingScreen;
    SetPlayerAction(player, PlayerAction.UsingScreen, 0.0f, 0.0f, state);
    return;
}

            // --- Evaluate Action Scores ---
float shootScore = _shootDecisionMaker.EvaluateShootScore(player, state, tactic);
PassOption bestPass = _passDecisionMaker.GetBestPassOption(player, state, tactic);
float passScore = bestPass?.Score ?? 0f;
float dribbleScore = _dribbleDecisionMaker.EvaluateDribbleScore(player, state, tactic);

// --- Phase Detection ---
// Note: GamePhase is defined in Core.Enums.cs, state.CurrentPhase is available
bool isTransitionPhase = state.CurrentPhase == GamePhase.TransitionToHomeAttack || state.CurrentPhase == GamePhase.TransitionToAwayAttack;
bool isPositionalAttackPhase = state.CurrentPhase == GamePhase.HomeAttack || state.CurrentPhase == GamePhase.AwayAttack;


if (isTransitionPhase)
{
    // Fast break: prioritize direct actions
    phaseDribbleModifier = TRANSITION_DRIBBLE_MODIFIER;
    phasePassModifier = TRANSITION_PASS_MODIFIER;
    phaseShootModifier = TRANSITION_SHOOT_MODIFIER;
    phaseScreenModifier = TRANSITION_SCREEN_MODIFIER;
    phaseComplexPassModifier = TRANSITION_COMPLEX_PASS_MODIFIER;
    prepTimeFactor = TRANSITION_PREP_TIME_FACTOR;
    actionThresholdFactor = TRANSITION_ACTION_THRESHOLD_FACTOR;
}
else if (isPositionalAttackPhase)
{
    // Settled attack: encourage tactical play
    phaseScreenModifier = POSITIONAL_SCREEN_MODIFIER;
    // Could extend: phasePassModifier = POSITIONAL_FORMATION_PASS_MODIFIER; (if pass type detectable)
}
// (Other phases: keep modifiers at 1.0)

            // --- Apply Context Modifiers ---
            float tacticalRiskMod = _tacticalEvaluator.GetRiskModifier(tactic);
            float personalityRiskMod = _personalityEvaluator.GetRiskModifier(player.BaseData);
            float gameStateRiskMod = _gameStateEvaluator.GetAttackRiskModifier(state, player.TeamSimId);
            float combinedRiskFactor = tacticalRiskMod * personalityRiskMod * gameStateRiskMod;

            // Adjust scores based on risk
shootScore *= combinedRiskFactor;
dribbleScore *= combinedRiskFactor * DRIBBLING_RISK_ADJUSTMENT;
passScore /= Mathf.Max(PASS_SAFETY_MIN_DIVISOR, combinedRiskFactor * PASS_SAFETY_ADJUSTMENT); // Divide for safety bias

// Apply personality tendency modifiers
shootScore *= _personalityEvaluator.GetShootingTendencyModifier(player.BaseData);
passScore *= _personalityEvaluator.GetPassingTendencyModifier(player.BaseData);
dribbleScore *= _personalityEvaluator.GetDribblingTendencyModifier(player.BaseData);

// --- Apply Phase-specific Modifiers (after risk/personality) ---
dribbleScore *= phaseDribbleModifier;
passScore *= phasePassModifier;
shootScore *= phaseShootModifier;
// Note: Complex pass and screen modifiers would require more granular pass/screen type logic.
// If pass is complex (not implemented here), could apply phaseComplexPassModifier.
// For screen opportunities, see below.

            // --- Choose Best Action ---
// Lower threshold for taking action during transition (faster decision-making)
float phaseActionThreshold = BASE_ACTION_THRESHOLD * actionThresholdFactor;
float bestScore = phaseActionThreshold;
PlayerAction chosenAction = PlayerAction.MovingWithBall; // Default
SimPlayer passTarget = bestPass?.Player; // Store potential target early

if (shootScore > bestScore) { bestScore = shootScore; chosenAction = PlayerAction.PreparingShot; }
if (passScore > bestScore) { bestScore = passScore; chosenAction = PlayerAction.PreparingPass; } // Pass target already stored
if (dribbleScore > bestScore) { chosenAction = PlayerAction.Dribbling; }

            // --- Set Player State ---
            switch (chosenAction)
            {
                case PlayerAction.PreparingShot:
                    player.PlannedAction = PlayerAction.PreparingShot;
                    // Reduce shot prep time during transition phases for quick shots
                    float shotPrepBase = SHOT_PREP_TIME_BASE * prepTimeFactor;
                    SetPlayerAction(player, PlayerAction.PreparingShot, shotPrepBase, SHOT_PREP_TIME_RANDOM_FACTOR, state);
                    break;
                case PlayerAction.PreparingPass:
                    // **FIXED:** Check passTarget (derived from bestPass.Player) is not null *before* using it.
                    if (passTarget != null)
                    {
                        player.TargetPlayer = passTarget;
                        player.PlannedAction = PlayerAction.PreparingPass;
                        // Reduce pass prep time during transition phases for quick passes
                        float passPrepBase = PASS_PREP_TIME_BASE * prepTimeFactor;
                        SetPlayerAction(player, PlayerAction.PreparingPass, passPrepBase, PASS_PREP_TIME_RANDOM_FACTOR, state);
                    }
                    else
                    {
                        Debug.LogWarning($"Player {player.GetPlayerId()} chose Pass but passTarget is null (Pass Score: {passScore}). Falling back.");
                        SetPlayerToMoveToTacticalPosition(player, state, tactic); // Fallback if target invalid
                    }
                    break;
                case PlayerAction.Dribbling: // Treat Dribbling decision as MovingWithBall state for movement sim
                     player.PlannedAction = PlayerAction.Dribbling;
                     player.CurrentAction = PlayerAction.MovingWithBall;
                     SetPlayerToMoveToTacticalPosition(player, state, tactic);
                    break;
                case PlayerAction.MovingWithBall: // Default action
                default:
                     player.PlannedAction = PlayerAction.MovingWithBall;
                     player.CurrentAction = PlayerAction.MovingWithBall;
                     SetPlayerToMoveToTacticalPosition(player, state, tactic);
                    break;
            }
        }

        /// <summary>Determines the best defensive action for a field player.</summary>
        /// <param name="player">The player deciding.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's tactic.</param>
        private void DecideDefensiveAction(SimPlayer player, MatchState state, Tactic tactic)
        {
             // Added null checks for safety
             if (player?.BaseData == null || state == null || tactic == null) return;

            // Delegate decision to the specialized maker
            DefensiveAction defensiveChoice = _defenseDecisionMaker.DecideDefensiveAction(player, state, tactic);

            // Apply the chosen action
            player.TargetPlayer = defensiveChoice.TargetPlayer;
            player.TargetPosition = defensiveChoice.TargetPosition;

            // Set timer only if it's a preparatory action like Tackle
            if (defensiveChoice.Action == PlayerAction.AttemptingTackle)
            {
                // Use SetPlayerAction to handle timer and state correctly
                player.PlannedAction = PlayerAction.AttemptingTackle;
                SetPlayerAction(player, PlayerAction.AttemptingTackle, TACKLE_PREP_TIME, 0f, state);
            }
            else
            {
                // For non-timed actions (Mark, Block, Move), just set the state directly
                player.PlannedAction = defensiveChoice.Action;
                player.CurrentAction = defensiveChoice.Action;
                // Ensure timer is clear for non-preparatory actions
                 if(player.ActionTimer > 0) player.ActionTimer = 0f;
            }
        }

        /// <summary>Decision logic for Goalkeepers.</summary>
        /// <param name="gk">The goalkeeper SimPlayer.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's tactic.</param>
        private void DecideGoalkeeperAction(SimPlayer gk, MatchState state, Tactic tactic)
        {
             // Added null checks for safety
             if (gk?.BaseData == null || state == null || tactic == null) return;

            bool isOwnTeamPossession = gk.TeamSimId == state.PossessionTeamId && state.PossessionTeamId != -1;

            // Priority 1: Has Ball - Decide pass vs hold
            if (gk.HasBall)
            {
                PassOption bestPass = _passDecisionMaker.GetBestPassOption(gk, state, tactic);
                float passThreshold = GK_PASS_ATTEMPT_THRESHOLD * _gameStateEvaluator.GetGoalkeeperPassSafetyModifier(state, gk.TeamSimId);

                if (bestPass?.Player != null && bestPass.Score > passThreshold) // Check Player is not null too
                {
                    gk.TargetPlayer = bestPass.Player;
                    gk.PlannedAction = PlayerAction.PreparingPass;
                    SetPlayerAction(gk, PlayerAction.PreparingPass, GK_PASS_PREP_TIME_BASE, GK_PASS_PREP_TIME_RANDOM_FACTOR, state);
                }
                else
                {
                    gk.CurrentAction = PlayerAction.Idle;
                    gk.TargetPosition = gk.Position;
                    gk.Velocity = Vector2.zero;
                }
                return;
            }

            // Priority 2: Opponent Shot In Flight - Position to Save
            bool isShotIncoming = state.Ball.IsInFlight && state.Ball.LastShooter != null && state.Ball.LastShooter.TeamSimId != gk.TeamSimId;
            if (isShotIncoming)
            {
                Vector3 predictedImpact = _ballPhysics.EstimateBallGoalLineImpact3D(state.Ball, gk.TeamSimId);
                gk.TargetPosition = _gkPositioner.GetGoalkeeperSavePosition(gk, state, predictedImpact);
                gk.PlannedAction = PlayerAction.GoalkeeperPositioning;
                gk.CurrentAction = PlayerAction.GoalkeeperPositioning;
                gk.TargetPlayer = null;
                return;
            }

            // Priority 3: Opponent Has Ball / Ball Loose Nearby - Position Defensively
            if (!isOwnTeamPossession)
            {
                gk.TargetPosition = _gkPositioner.GetGoalkeeperDefensivePosition(gk, state);
                gk.PlannedAction = PlayerAction.GoalkeeperPositioning;
                gk.CurrentAction = PlayerAction.GoalkeeperPositioning;
                gk.TargetPlayer = null;
                return;
            }

            // Priority 4: Own Team Has Ball - Provide Support / Default Positioning
            gk.TargetPosition = _gkPositioner.GetGoalkeeperAttackingSupportPosition(gk, state);
            if (Vector2.Distance(gk.Position, gk.TargetPosition) > DIST_TO_TARGET_MOVE_TRIGGER_THRESHOLD)
            {
                gk.PlannedAction = PlayerAction.GoalkeeperPositioning;
                gk.CurrentAction = PlayerAction.GoalkeeperPositioning;
            }
            else if (gk.CurrentAction == PlayerAction.GoalkeeperPositioning) // Arrived
            {
                  gk.CurrentAction = PlayerAction.Idle;
                  gk.Velocity *= ARRIVAL_VELOCITY_DAMPING_FACTOR;
            }
            gk.TargetPlayer = null;
        }
        #endregion

        #region Helper Methods
        /// <summary>Applies fallback positioning if no specific action was chosen.</summary>
        /// <param name="player">The player to position.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's tactic.</param>
        private void ApplyFallbackPositioning(SimPlayer player, MatchState state, Tactic tactic)
        {
             // Ensure player moves towards tactical position if idle or has arrived at previous target
            bool needsRepositioning = player.CurrentAction == PlayerAction.Idle ||
                                      (IsMovementAction(player.CurrentAction) && Vector2.Distance(player.Position, player.TargetPosition) < DIST_TO_TARGET_IDLE_THRESHOLD);

            // Also reposition if marking state is invalid
            if (player.CurrentAction == PlayerAction.MarkingPlayer && (player.TargetPlayer == null || !player.TargetPlayer.IsOnCourt || player.TargetPlayer.SuspensionTimer > 0))
            {
                needsRepositioning = true;
            }

            if (needsRepositioning)
            {
                player.PlannedAction = PlayerAction.MovingToPosition;
                SetPlayerToMoveToTacticalPosition(player, state, tactic);
            }
        }

        /// <summary>Commands the player to move towards their calculated tactical position.</summary>
        /// <param name="player">The player to position.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's tactic.</param>
        private void SetPlayerToMoveToTacticalPosition(SimPlayer player, MatchState state, Tactic tactic)
        {
            // Basic validation
            if (player?.BaseData == null || state == null || _tacticPositioner == null || tactic == null) return;

            // Goalkeepers positioning is handled entirely within DecideGoalkeeperAction
            if (player.AssignedTacticalRole == PlayerPosition.Goalkeeper) return;

            Vector2 tacticalPos = _tacticPositioner.GetPlayerTargetPosition(state, player);

            // Apply personality modifier for work rate/laziness (if implemented)
            // tacticalPos = _personalityEvaluator.AdjustPositionForWorkRate(player.BaseData, player.Position, tacticalPos);

            // Check if significant movement is required
            if (Vector2.Distance(player.Position, tacticalPos) > DIST_TO_TARGET_MOVE_TRIGGER_THRESHOLD)
            {
                player.TargetPosition = tacticalPos;
                player.TargetPlayer = null; // Clear target player when moving positionally

                // Set movement action state only if not already moving appropriately or if idle
                if (player.CurrentAction == PlayerAction.Idle || !IsMovementAction(player.CurrentAction))
                {
                    player.CurrentAction = player.HasBall ? PlayerAction.MovingWithBall : PlayerAction.MovingToPosition;
                }
                // If already MovingWithBall/MovingToPosition, just updating TargetPosition is sufficient.
            }
            else if (IsMovementAction(player.CurrentAction) && player.CurrentAction != PlayerAction.MarkingPlayer) // Arrived at target, excluding marking state
            {
                // Transition general movement actions to Idle
                if (player.CurrentAction == PlayerAction.MovingToPosition || player.CurrentAction == PlayerAction.MovingWithBall || player.CurrentAction == PlayerAction.Dribbling)
                {
                    player.CurrentAction = PlayerAction.Idle;
                    player.TargetPosition = player.Position; // Stop targeting the old pos
                    player.Velocity *= ARRIVAL_VELOCITY_DAMPING_FACTOR;
                    player.TargetPlayer = null;
                }
                 // Let MarkingPlayer persist even when close, TacticPositioner adjusts target pos
            }
            // If Idle and already close, remain Idle.
        }


        /// <summary>Sets the player's action, timer, and resets positional targets/velocity appropriately.</summary>
        /// <param name="player">The player whose action to set.</param>
        /// <param name="action">The action to set.</param>
        /// <param name="baseTime">The base preparation time for the action.</param>
        /// <param name="randomFactor">The random variance factor for the prep time (0 to 1).</param>
        /// <param name="state">The current match state (for random number generation).</param>
        private void SetPlayerAction(SimPlayer player, PlayerAction action, float baseTime, float randomFactor, MatchState state)
        {
            if (player == null || state == null) return;

            player.CurrentAction = action;

            // Use state's random generator safely
            float randomValue = (state.RandomGenerator != null) ? (float)state.RandomGenerator.NextDouble() : 0.5f;

            // Apply personality hesitation modifier (if evaluator available)
            float hesitationMod = _personalityEvaluator?.GetHesitationModifier(player.BaseData) ?? 1.0f;

            // Calculate and set timer, ensuring a minimum duration
            player.ActionTimer = Mathf.Max(MIN_ACTION_TIMER, (baseTime + randomValue * randomFactor) * hesitationMod);

            // Stop movement during preparation, unless it's inherently a movement action
            if (!IsMovementAction(action))
            {
                 player.TargetPosition = player.Position; // Target current spot
                 player.Velocity *= PREP_VELOCITY_DAMPING_FACTOR; // Drastically reduce speed
            }

            // Clear TargetPlayer unless it's inherently needed by the action
            // (TargetPlayer for pass/tackle is set *before* calling this)
            if (action != PlayerAction.PreparingPass && action != PlayerAction.AttemptingTackle && action != PlayerAction.MarkingPlayer)
            {
                player.TargetPlayer = null;
            }
        }

        /// <summary>Checks if a PlayerAction primarily involves player movement.</summary>
        /// <param name="action">The action to check.</param>
        /// <returns>True if the action is a movement state, false otherwise.</returns>
        private bool IsMovementAction(PlayerAction action)
        {
            // Defines actions where the player is actively trying to change position
            switch (action)
            {
                case PlayerAction.MovingToPosition:
                case PlayerAction.MovingWithBall:
                case PlayerAction.ChasingBall:
                case PlayerAction.MarkingPlayer:       // Constant adjustment = movement
                case PlayerAction.ReceivingPass:       // Movement to intercept point
                case PlayerAction.AttemptingIntercept: // Movement towards intercept point
                case PlayerAction.GoalkeeperPositioning:
                case PlayerAction.AttemptingBlock:     // Movement towards block spot
                case PlayerAction.Dribbling:           // Explicit dribbling/running
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Resets a player's state to a safe default (Idle) in case of an error during decision making.</summary>
        /// <param name="player">The player whose state needs resetting.</param>
        private void ResetPlayerStateOnError(SimPlayer player)
        {
            if (player != null && player.SuspensionTimer <= 0)
            {
                player.CurrentAction = PlayerAction.Idle;
                player.ActionTimer = 0f;
                player.TargetPlayer = null;
                player.TargetPosition = player.Position;
                player.Velocity = Vector2.zero; // Stop movement completely on error
            }
        }
        #endregion

        #region Interface Implementation
        /// <summary>
        /// Calculates the optimal position for a player based on tactical considerations.
        /// Implementation of IPlayerAIController.CalculatePlayerPosition
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to calculate position for.</param>
        /// <returns>The target position vector for the player.</returns>
        public Vector2 CalculatePlayerPosition(MatchState state, PlayerData player)
        {
            // Validation des paramètres
            if (state == null || player == null)
            {
                Debug.LogWarning("[PlayerAIController] CalculatePlayerPosition called with null state or player.");
                return Vector2.zero;
            }

            // Trouver le SimPlayer correspondant au PlayerData
            SimPlayer simPlayer = state.GetPlayerById(player.PlayerID);
            if (simPlayer == null)
            {
                Debug.LogWarning($"[PlayerAIController] Player {player.PlayerID} not found in match state.");
                return Vector2.zero;
            }

            // Obtenir la tactique appropriée
            Tactic tactic = (simPlayer.TeamSimId == 0) ? state.HomeTactic : state.AwayTactic;

            // Utiliser le positionneur tactique pour déterminer la position optimale
            if (simPlayer.AssignedTacticalRole == PlayerPosition.Goalkeeper)
            {
                // Position spécifique pour le gardien de but
                if (simPlayer.TeamSimId == state.PossessionTeamId)
                {
                    // En attaque
                    return _gkPositioner.GetGoalkeeperAttackingSupportPosition(simPlayer, state);
                }
                else
                {
                    // En défense
                    return _gkPositioner.GetGoalkeeperDefensivePosition(simPlayer, state);
                }
            }
            else
            {
                // Position pour les joueurs de champ basée sur la tactique
                return _tacticPositioner.GetPlayerTargetPosition(state, simPlayer);
            }
        }

        /// <summary>
        /// Evaluates potential passing targets and selects the best receiver.
        /// Implementation of IPlayerAIController.FindBestPassTarget
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="passer">The player attempting to pass.</param>
        /// <returns>The best player to receive the pass, or null if no good option exists.</returns>
        public PlayerData FindBestPassTarget(MatchState state, PlayerData passer)
        {
            // Validation des paramètres
            if (state == null || passer == null)
            {
                Debug.LogWarning("[PlayerAIController] FindBestPassTarget called with null state or passer.");
                return null;
            }

            // Trouver le SimPlayer correspondant au passeur
            SimPlayer simPasser = state.GetPlayerById(passer.PlayerID);
            if (simPasser == null)
            {
                Debug.LogWarning($"[PlayerAIController] Passer {passer.PlayerID} not found in match state.");
                return null;
            }

            // Obtenir la tactique appropriée
            Tactic tactic = (simPasser.TeamSimId == 0) ? state.HomeTactic : state.AwayTactic;

            // Utiliser le décideur de passes pour évaluer les options
            PassOption bestPass = _passDecisionMaker.GetBestPassOption(simPasser, state, tactic);

            // Retourner le PlayerData du meilleur receveur, ou null si aucune bonne option
            return bestPass?.Player?.BaseData;
        }
        #endregion
        /// <summary>
        /// Calculates a rerouted target position that avoids the 6m goal area if the direct path crosses it.
        /// Checks if the segment from 'from' to 'to' crosses the 6m goal area for the given team.
        /// </summary>

    } // End Class PlayerAIController
    

} // End Namespace