using HandballManager.Data;
using UnityEngine;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Gameplay; // For Tactic
using HandballManager.Core; // For PlayerAction
using System.Collections.Generic; // For List
using System.Linq; // For Linq
using static HandballManager.Simulation.AI.Decision.DefaultOffensiveDecisionMaker; // For ScreenDecisionData
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Utils; // Added for IGeometryProvider
using Zenject; // Added for dependency injection

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Concrete implementation for AI logic specific to offensive players.
    /// </summary>
    public class OffensiveAIController : IOffensiveAIController
    {
        // --- Dependencies ---
        private readonly IPassingDecisionMaker _passDecisionMaker;
        private readonly IShootingDecisionMaker _shootDecisionMaker;
        private readonly IDribblingDecisionMaker _dribbleDecisionMaker;
        private readonly IGameStateEvaluator _gameStateEvaluator;
        private readonly ITacticalEvaluator _tacticalEvaluator;
        private readonly IPersonalityEvaluator _personalityEvaluator;
        private readonly ITacticPositioner _tacticPositioner; // Added for positioning
        private readonly IOffensiveDecisionMaker _offensiveDecisionMaker; // For screen evaluation
        private readonly IGeometryProvider _geometryProvider; // Added
        private readonly PassivePlayManager _passivePlayManager; // Added dependency

        [Inject]
        public OffensiveAIController(
            IPassingDecisionMaker passDecisionMaker,
            IShootingDecisionMaker shootDecisionMaker,
            IDribblingDecisionMaker dribbleDecisionMaker,
            IGameStateEvaluator gameStateEvaluator,
            ITacticalEvaluator tacticalEvaluator,
            IPersonalityEvaluator personalityEvaluator,
            ITacticPositioner tacticPositioner, // Added
            IOffensiveDecisionMaker offensiveDecisionMaker, // Added
            IGeometryProvider geometryProvider, // Added
            PassivePlayManager passivePlayManager // Added parameter
            )
        {
            _passDecisionMaker = passDecisionMaker;
            _shootDecisionMaker = shootDecisionMaker;
            _dribbleDecisionMaker = dribbleDecisionMaker;
            _gameStateEvaluator = gameStateEvaluator;
            _tacticalEvaluator = tacticalEvaluator;
            _personalityEvaluator = personalityEvaluator;
            _tacticPositioner = tacticPositioner; // Added
            _offensiveDecisionMaker = offensiveDecisionMaker; // Added
            _geometryProvider = geometryProvider; // Added
            _passivePlayManager = passivePlayManager; // Store dependency

            Debug.Log($"OffensiveAIController Initialized. TacticPositioner: {_tacticPositioner != null}, PassDecisionMaker: {_passDecisionMaker != null}, ShootDecisionMaker: {_shootDecisionMaker != null}, DribbleDecisionMaker: {_dribbleDecisionMaker != null}, GameStateEvaluator: {_gameStateEvaluator != null}, TacticalEvaluator: {_tacticalEvaluator != null}, PersonalityEvaluator: {_personalityEvaluator != null}, OffensiveDecisionMaker: {_offensiveDecisionMaker != null}, GeometryProvider: {_geometryProvider != null}, PassivePlayManager: {_passivePlayManager != null}");
        }

        public PlayerAction DetermineOffensiveAction(MatchState state, SimPlayer player)
        {
            if (player == null || state == null || !player.HasBall) return PlayerAction.Idle;

            // Basic evaluations (can be expanded)
            float shootScore = _shootDecisionMaker.DecideAction(player, state); // Pass SimPlayer
            float passScore = _passDecisionMaker.DecideAction(player, state); // Pass SimPlayer
            float dribbleScore = _dribbleDecisionMaker.DecideAction(player, state); // Pass SimPlayer

            // Apply modifiers and decide action
            // ...

            return PlayerAction.Idle; // Default fallback
        }

        public OffBallOffensiveDecision DetermineOffBallOffensiveAction(MatchState state, SimPlayer player, Gameplay.Tactic tactic)
        {
            if (player == null || state == null || tactic == null || player.HasBall)
            {
                // Return a default action like MovingToPosition if conditions not met
                return new OffBallOffensiveDecision(PlayerAction.MovingToPosition);
            }

            // --- 1. Screen Evaluation (Using IOffensiveDecisionMaker) --- 
            PlayerAIContext screenContext = new PlayerAIContext
            {
                Player = player,
                MatchState = state,
                Tactics = tactic
                // TacticPositioner can be set here if available
            };
            ScreenDecisionData? screenData = _offensiveDecisionMaker.EvaluateScreenOpportunity(screenContext); // Pass SimPlayer via context

            if (screenData.HasValue && screenData.Value.ShouldSetScreen)
            {
                // Return decision to set screen
                return new OffBallOffensiveDecision(PlayerAction.SettingScreen, screenData.Value);
            }

            // --- 2. Positioning Logic (Fallback if not screening) ---
            // Calculate the ideal tactical position
            Vector2 targetPosition = CalculateOffensivePosition(state, player, tactic); // Use the updated method

            // Determine if the player should move
            float distToTargetSqr = (player.Position - targetPosition).sqrMagnitude;
            // Use a threshold, perhaps from SimConstants or defined here
            const float DIST_TO_TARGET_MOVE_THRESHOLD_SQR = 0.36f; // Example: 0.6 * 0.6

            if (distToTargetSqr > DIST_TO_TARGET_MOVE_THRESHOLD_SQR)
            {
                return new OffBallOffensiveDecision(PlayerAction.MovingToPosition);
            }
            else
            {
                // Close enough, stay idle or allow current non-movement action
                // If the current action IS a movement action but we are close, switch to Idle
                if (player.PlannedAction == PlayerAction.MovingToPosition)
                {
                    return new OffBallOffensiveDecision(PlayerAction.Idle);
                }
                // Otherwise, let the current non-movement action continue (or default to Idle if none)
                return new OffBallOffensiveDecision(player.PlannedAction == PlayerAction.Idle ? PlayerAction.Idle : player.PlannedAction);
            }
        }

        // Renamed back and signature changed to match interface IOffensiveAIController
        public Vector2 CalculateOffensivePosition(MatchState state, SimPlayer player, Gameplay.Tactic tactic)
        {
            // Rely on ITacticPositioner for the core logic
            if (_tacticPositioner != null)
            {
                // Pass the SimPlayer directly to GetTargetPosition
                return _tacticPositioner.GetPlayerTargetPosition(state, player);
            }
            else
            {
                Debug.LogError("[OffensiveAIController] TacticPositioner dependency not set!");
                // Fallback: return current position or a default safe spot?
                return player?.Position ?? Vector2.zero;
            }
        }

        public SimPlayer FindBestPassTarget(MatchState state, SimPlayer passer)
        {
            if (passer == null || state == null || _passDecisionMaker == null)
            {
                return null;
            }

            // Delegate to the passing decision maker
            // The EvaluatePassOptions might already do this, or we need a specific method.
            // Assuming EvaluatePassOptions returns ranked options:
            Tactic tactic = (passer.TeamId == 0) ? state.HomeTactic : state.AwayTactic;
            var passOptions = _passDecisionMaker.EvaluatePassOptions(passer, state, tactic, false); // Pass SimPlayer
            var bestOption = passOptions.OrderByDescending(p => p.Score).FirstOrDefault();

            // Use BaseData to access FullName for the target player
            Debug.Log($"[OffensiveAI] Finding best pass target for {passer.BaseData?.FullName ?? "Unknown"}. Best target: {bestOption?.Player?.BaseData?.FullName ?? "None"} (Score: {bestOption?.Score ?? 0f})");
            // Return SimPlayer directly
            return bestOption?.Player;
        }

        public (PlayerAction action, SimPlayer targetPlayer) HandleSetPieceAction(MatchState state, SimPlayer player, Gameplay.Tactic tactic)
        {
            // Logic moved from PlayerAIController.HandleSetPieceAndPenaltyPhases
            // Assumes 'player' is the one with the ball (shooter/passer)
            bool canShoot = EvaluateSetPieceShootingChance(player, state);
            if (canShoot)
            {
                return (PlayerAction.PreparingShot, null);
            }
            else
            {
                SimPlayer bestTarget = FindBestPassTarget(state, player); // Call with SimPlayer, receive SimPlayer
                if (bestTarget != null)
                {
                    return (PlayerAction.PreparingPass, bestTarget);
                }
                else
                {
                    // If no pass target, default to Idle (or maybe hold ball?)
                    return (PlayerAction.Idle, null);
                }
            }
        }

        public PlayerAction HandlePenaltyAction(MatchState state, SimPlayer player)
        {
            // Logic moved from PlayerAIController.HandleSetPieceAndPenaltyPhases
            // Assumes 'player' is the designated penalty taker
            return PlayerAction.PreparingShot;
        }

        // --- Private Helper Methods ---

        // Moved from PlayerAIController.cs
        private bool EvaluateSetPieceShootingChance(SimPlayer player, MatchState state)
        {
            if (_passivePlayManager == null) { 
                Debug.LogError("[OffensiveAI] PassivePlayManager dependency not set!");
                return false; 
            }

            // Example logic: always shoot if it's the very end of the half or if passive play is active
            bool isEndOfHalf = (state.MatchDurationSeconds - state.MatchTimeSeconds) < 5f;
            // Use injected PassivePlayManager
            bool isPassivePlayWarningForTeam = _passivePlayManager.PassivePlayWarningActive && _passivePlayManager.WarningTeamSimId == player.TeamSimId;
            int passesSinceWarning = isPassivePlayWarningForTeam ? _passivePlayManager.PassesSinceWarning : 0;

            // TODO: Add more sophisticated evaluation based on player skill, distance, angle, defenders?
            // For now, only shoot if forced by time or passive play.
            bool shouldShoot = isEndOfHalf || isPassivePlayWarningForTeam;
            Debug.Log($"[OffensiveAI] {player.BaseData.FullName} SetPiece Shoot Eval: EndOfHalf={isEndOfHalf}, PassiveWarn={isPassivePlayWarningForTeam}, PassesSinceWarn={passesSinceWarning}, Result={shouldShoot}");
            return shouldShoot;
        }
    }
}