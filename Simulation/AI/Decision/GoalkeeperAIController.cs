using HandballManager.Data;
using UnityEngine;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Gameplay; // For Tactic
using HandballManager.Core; // For PlayerAction
using System.Linq; // For Linq
using HandballManager.Simulation.Engines; // Added for MatchState
using HandballManager.Simulation.Utils; // For IGeometryProvider

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Concrete implementation for AI logic specific to goalkeepers.
    /// </summary>
    public class GoalkeeperAIController : IGoalkeeperAIController
    {
        // Dependencies
        private readonly IGoalkeeperPositioner _gkPositioner;
        private readonly IPassingDecisionMaker _passDecisionMaker;
        private readonly IGameStateEvaluator _gameStateEvaluator;
        private readonly IPersonalityEvaluator _personalityEvaluator;
        private readonly ITacticalEvaluator _tacticalEvaluator;
        private readonly IGeometryProvider _geometry;

        // Constants (Consider moving relevant constants here from PlayerAIController)
        private const float GK_PASS_PREP_TIME_BASE = 0.6f;
        private const float GK_PASS_PREP_TIME_RANDOM_FACTOR = 0.2f;
        private const float GK_PASS_ATTEMPT_THRESHOLD = 0.5f; // GK's base threshold for passing vs holding
        private const float MIN_ACTION_TIMER = 0.5f; // Placeholder

        public GoalkeeperAIController(
            IGoalkeeperPositioner gkPositioner,
            IPassingDecisionMaker passDecisionMaker,
            IGameStateEvaluator gameStateEvaluator,
            IPersonalityEvaluator personalityEvaluator,
            ITacticalEvaluator tacticalEvaluator,
            IGeometryProvider geometry
            )
        {
            _gkPositioner = gkPositioner;
            _passDecisionMaker = passDecisionMaker;
            _gameStateEvaluator = gameStateEvaluator;
            _personalityEvaluator = personalityEvaluator;
            _tacticalEvaluator = tacticalEvaluator;
            _geometry = geometry;
        }

        public PlayerAction DetermineGoalkeeperAction(MatchState state, SimPlayer player)
        {
            // Check if player is null or not a Goalkeeper using BaseData
            if (player?.BaseData == null || state == null || player.BaseData.PrimaryPosition != PlayerPosition.Goalkeeper) return PlayerAction.Idle;

            Tactic tactic = (player.TeamId == 0) ? state.HomeTactic : state.AwayTactic; // Use SimPlayer.TeamId
            if (tactic == null) return PlayerAction.Idle; // Need tactic

            // --- Fetch Modifiers --- 
            // Assuming evaluators can handle SimPlayer or extract PlayerData if needed
            float personalityRisk = _personalityEvaluator.GetRiskModifier(player.BaseData); // Pass PlayerData from SimPlayer
            float gameStatePassSafety = _gameStateEvaluator.GetGoalkeeperPassSafetyModifier(state, player.TeamId);
            float tacticRisk = _tacticalEvaluator.GetRiskModifier(tactic);

            PlayerAction chosenAction = PlayerAction.Idle;
            SimPlayer targetPlayer = null; // Changed type to SimPlayer
            // float prepTime = MIN_ACTION_TIMER; // Prep time handled by PlayerAIController
            // float randomFactor = 0f;

            // --- Priority 1: Has Ball - Decide pass vs hold --- 
            // Check SimPlayer instance equality
            if (player == state.Ball.Holder) 
            {
                // Evaluate pass options - Assuming GetBestPassOption now takes SimPlayer and returns PassOption with SimPlayer target
                PassOption bestPass = _passDecisionMaker.GetBestPassOption(player, state, tactic);
                float weightedPassScore = (bestPass?.Score ?? 0f) * personalityRisk * gameStatePassSafety * tacticRisk;
                float passThreshold = GK_PASS_ATTEMPT_THRESHOLD * gameStatePassSafety * tacticRisk;

                // Check if the target SimPlayer exists
                if (bestPass?.Player != null && weightedPassScore > passThreshold)
                {
                    chosenAction = PlayerAction.PreparingPass;
                    targetPlayer = bestPass.Player; // Assign SimPlayer
                    // prepTime = GK_PASS_PREP_TIME_BASE;
                    // randomFactor = GK_PASS_PREP_TIME_RANDOM_FACTOR;
                }
                else
                {
                    // No PlayerAction.HoldingBall exists; use Idle to represent holding the ball
chosenAction = PlayerAction.Idle; // Hold if no good pass (Idle means standing with the ball)
                }
            }
            // --- Priority 2: Opponent has ball - Decide save/position vs intercept/challenge --- 
            // Check SimPlayer's TeamId
            else if (state.Ball.Holder != null && state.Ball.Holder.TeamId != player.TeamId)
            {
                // Basic logic: If ball is close and moving towards goal, attempt save/block
                // More complex logic would involve _gkPositioner or specific save evaluation
                float distToBallSqr = (player.Position - new Vector2(state.Ball.Position.x, state.Ball.Position.y)).sqrMagnitude; // Use SimPlayer.Position
                bool ballMovingTowardsGoal = IsBallMovingTowardsGoal(state, player.TeamId);

                // Example threshold - needs refinement
                if (distToBallSqr < 25f && ballMovingTowardsGoal) // If ball is within 5 units and coming towards goal
                {
                    chosenAction = PlayerAction.GoalkeeperSaving; // Or AttemptingBlock
                }
                else
                {
                    // Default action is to move to optimal position - REMOVED, handled by DefensiveDecisionMaker
                    // chosenAction = PlayerAction.MovingToPosition;
                    chosenAction = PlayerAction.Idle; // Default to Idle if not saving, positioning handled elsewhere
                }
            }
            // --- Priority 3: Loose Ball --- (Simplified)
            else if (state.Ball.Holder == null)
            {
                 // Move towards ball if close and safe?
                 // For now, default to positioning - REMOVED, handled by DefensiveDecisionMaker
                 // chosenAction = PlayerAction.MovingToPosition;
                 chosenAction = PlayerAction.Idle; // Default to Idle, positioning handled elsewhere
            }
            // --- Priority 4: Own team has ball (not GK) --- 
            else
            {
                 // Support play? Usually just position optimally. - REMOVED, handled by DefensiveDecisionMaker
                 // chosenAction = PlayerAction.MovingToPosition;
                 chosenAction = PlayerAction.Idle; // Default to Idle, positioning handled elsewhere
            }

            // --- Return Action --- 
            // PlayerAIController will handle setting SimPlayer state (ActionTimer, TargetPlayer etc.)
            // We might need to return a richer object later (Action, TargetPlayer, TargetPosition, PrepTime)
            // Use BaseData for name logging
            Debug.Log($"[GoalkeeperAI] {player.BaseData?.FullName}: Action={chosenAction}, Target={targetPlayer?.BaseData?.FullName ?? "None"}");
            return chosenAction;
        }

        public PlayerAction HandlePenaltySaveAction(MatchState state, SimPlayer player)
        {
            // Logic moved from PlayerAIController.HandleSetPieceAndPenaltyPhases
            // Assumes 'player' is the goalkeeper
            // Basic logic: always try to save
            // TODO: Add more sophisticated save direction/timing logic?
            Debug.Log($"[GoalkeeperAI] {player.BaseData?.FullName} handling penalty save."); // Use BaseData for name
            return PlayerAction.GoalkeeperSaving;
        }

        public Vector2 CalculateGoalkeeperPosition(MatchState state, SimPlayer player)
        {
            if (_gkPositioner != null && player != null && state != null)
            {
                // If opponent has the ball, use defensive positioning
                if (state.Ball.Holder != null && state.Ball.Holder.TeamId != player.TeamId)
                {
                    return _gkPositioner.GetGoalkeeperDefensivePosition(player, state);
                }
                // If own team has the ball, use attacking support positioning
                else if (state.Ball.Holder != null && state.Ball.Holder.TeamId == player.TeamId)
                {
                    return _gkPositioner.GetGoalkeeperAttackingSupportPosition(player, state);
                }
                // Default to defensive position if ambiguous
                else
                {
                    return _gkPositioner.GetGoalkeeperDefensivePosition(player, state);
                }
            }
            Debug.LogWarning($"[GoalkeeperAI] Could not calculate position for {player?.BaseData?.FullName}. Returning current position.");
            return player?.Position ?? Vector2.zero;
        }

        // Helper function (example - needs proper implementation)
        private bool IsBallMovingTowardsGoal(MatchState state, int teamId)
        {
            if (state.Ball.Velocity.sqrMagnitude < 0.1f) return false;
            Vector3 goalCenter3D = (teamId == 0) ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
            Vector2 goalCenter2D = new Vector2(goalCenter3D.x, goalCenter3D.y);
            Vector2 ballToGoal = goalCenter2D - new Vector2(state.Ball.Position.x, state.Ball.Position.y);
            // Check if velocity vector has a positive component in the direction of the goal
            return Vector2.Dot(state.Ball.Velocity.normalized, ballToGoal.normalized) > 0.5f; // Threshold dot product
        }
    }
}