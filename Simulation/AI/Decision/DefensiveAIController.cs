using HandballManager.Data;
using UnityEngine;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Gameplay; // For Tactic
using HandballManager.Core; // For PlayerAction
using System.Linq; // For Linq
using HandballManager.Simulation.Engines; // Added for MatchState
using HandballManager.Simulation.Utils; // Added for IGeometryProvider

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Concrete implementation for AI logic specific to defensive players.
    /// </summary>
    public class DefensiveAIController : IDefensiveAIController
    {
        // Dependencies
        private readonly IDefensiveDecisionMaker _defenseDecisionMaker;
        private readonly IGameStateEvaluator _gameStateEvaluator;
        private readonly ITacticalEvaluator _tacticalEvaluator;
        private readonly IPersonalityEvaluator _personalityEvaluator;
        private readonly ITacticPositioner _tacticPositioner; // Added for positioning
        private readonly IGeometryProvider _geometryProvider; // Added

        // Constants (Consider moving relevant constants here from PlayerAIController)
        private const float BASE_ACTION_THRESHOLD = 0.35f;
        private const float TACKLE_PREP_TIME = 0.3f;
        private const float MIN_ACTION_TIMER = 0.5f; // Placeholder, might need adjustment

        public DefensiveAIController(
            IDefensiveDecisionMaker defenseDecisionMaker,
            IGameStateEvaluator gameStateEvaluator,
            ITacticalEvaluator tacticalEvaluator,
            IPersonalityEvaluator personalityEvaluator,
            ITacticPositioner tacticPositioner, // Added
            IGeometryProvider geometryProvider // Added
            )
        {
            _defenseDecisionMaker = defenseDecisionMaker;
            _gameStateEvaluator = gameStateEvaluator;
            _tacticalEvaluator = tacticalEvaluator;
            _personalityEvaluator = personalityEvaluator;
            _tacticPositioner = tacticPositioner; // Added
            _geometryProvider = geometryProvider; // Added
        }

        /// <summary>
        /// Determines the best defensive action for the player based on the current game state.
        /// </summary>
        /// <param name="state">Current match state.</param>
        /// <param name="simPlayer">The SimPlayer instance representing the player in the simulation.</param>
        /// <param name="tactic">The current team tactic.</param>
        /// <returns>The decided PlayerAction.</returns>
        public PlayerAction DetermineDefensiveAction(MatchState state, SimPlayer simPlayer, Tactic tactic)
        {
            if (simPlayer == null || state == null || _defenseDecisionMaker == null)
            { 
                Debug.LogError($"[DefensiveAI] Missing SimPlayer, State, or DecisionMaker for {simPlayer?.BaseData?.FullName ?? "Unknown"}. Returning Idle.");
                return PlayerAction.Idle;
            }
            if (tactic == null)
            {
                Debug.LogWarning($"[DefensiveAI] Missing Tactic for {simPlayer.BaseData.FullName}. Returning Idle.");
                return PlayerAction.Idle; // Need tactic for evaluation
            }

            // --- Delegate Decision Making --- 
            DefensiveAction decision = _defenseDecisionMaker.DecideDefensiveAction(simPlayer, state, tactic);

            // --- Return Action --- 
            // PlayerAIController will handle setting the SimPlayer's state (ActionTimer, TargetPlayer etc.) based on the DefensiveAction object.
            // Access FullName via BaseData
            Debug.Log($"[DefensiveAI] {simPlayer.BaseData.FullName}: Delegated Decision={decision.Action}, TargetPos={decision.TargetPosition}, TargetPlayer={decision.TargetPlayer?.BaseData?.FullName ?? "None"}");
            return decision.Action; // Return only the action type for now
        }

        /// <summary>
        /// Calculates the ideal defensive position for the player based on the tactic and current state.
        /// This might be used by PlayerAIController to guide movement.
        /// </summary>
        /// <param name="state">Current match state.</param>
        /// <param name="simPlayer">The SimPlayer instance representing the player in the simulation.</param>
        /// <param name="tactic">The current team tactic.</param>
        /// <returns>The calculated target defensive position.</returns>
        public Vector2 CalculateDefensivePosition(MatchState state, SimPlayer simPlayer, Gameplay.Tactic tactic)
        {
            // Use simPlayer (which contains BaseData of type PlayerData) instead of separate PlayerData
            if (_tacticPositioner == null || simPlayer == null || state == null || tactic == null)
            {
                Debug.LogWarning($"[DefensiveAI] Missing dependencies for calculating defensive position for {simPlayer?.BaseData?.FullName ?? "Unknown"}. Returning current position.");
                // Now we can safely access Position as simPlayer is SimPlayer type
                return simPlayer?.Position ?? Vector2.zero; 
            }

            // --- Delegate Positioning --- 
            Vector2 targetPosition = _tacticPositioner.GetPlayerTargetPosition(state, simPlayer);
            // Apply potential adjustments based on game state (e.g., ball position, specific threats)
            // This logic might belong in a more specialized positioning service or within the TacticPositioner itself.
            targetPosition = AdjustPositionBasedOnBall(state, simPlayer, targetPosition);

            return targetPosition;
        }

        private Vector2 AdjustPositionBasedOnBall(MatchState state, SimPlayer simPlayer, Vector2 targetPosition)
        {
            // Placeholder: Basic adjustment - slightly shift towards the ball if it's nearby?
            // More complex logic needed here based on defensive principles.
            if (state.Ball != null)
            {
                float distanceToBall = Vector2.Distance(simPlayer.Position, state.Ball.Position);
                if (distanceToBall < 10f) // Example threshold
                {
                    // Vector2 directionToBall = (state.Ball.Position - simPlayer.Position).normalized;
                    // Slightly nudge towards the ball - very basic example
                    // targetPosition += directionToBall * 0.5f; 
                }
            }
            return targetPosition; // Return potentially adjusted position
        }
    }
}