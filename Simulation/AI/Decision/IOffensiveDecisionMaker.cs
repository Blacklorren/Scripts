using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Consolidated interface for offensive decision making in the AI system.
    /// Combines the functionality of the former IPassingDecisionMaker and IShootingDecisionMaker interfaces.
    /// </summary>
    public struct ScreenDecisionData
    {
        public SimPlayer Screener;
        public SimPlayer User; // Player using the screen
        public SimPlayer Defender; // Defender being screened
        public Vector2 ScreenSpot;
        public bool ShouldSetScreen; // Added flag
        public float EffectivenessAngle; // Added angle
    }

    public interface IOffensiveDecisionMaker
    {
        /// <summary>
        /// Makes a decision about passing the ball to another player.
        /// </summary>
        /// <param name="context">The current AI context containing game state and player information.</param>
        /// <returns>A decision result containing the pass decision details.</returns>
        DecisionResult MakePassDecision(PlayerAIContext context);
        
        /// <summary>
        /// Makes a decision about shooting the ball at the goal.
        /// </summary>
        /// <param name="context">The current AI context containing game state and player information.</param>
        /// <returns>A decision result containing the shot decision details.</returns>
        DecisionResult MakeShotDecision(PlayerAIContext context);
        
        /// <summary>
        /// Makes a decision about dribbling with the ball.
        /// </summary>
        /// <param name="context">The current AI context containing game state and player information.</param>
        /// <returns>A decision result containing the dribble decision details.</returns>
        DecisionResult MakeDribbleDecision(PlayerAIContext context);

        ScreenDecisionData? EvaluateScreenOpportunity(PlayerAIContext context);
    }
}