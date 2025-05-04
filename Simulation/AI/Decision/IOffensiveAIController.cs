using HandballManager.Data;
using UnityEngine;
using static HandballManager.Simulation.AI.Decision.DefaultOffensiveDecisionMaker; // For ScreenDecisionData
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Structure to hold the result of an off-ball offensive decision.
    /// </summary>
    public struct OffBallOffensiveDecision
    {
        public PlayerAction Action { get; set; }
        public ScreenDecisionData? ScreenData { get; set; } // Nullable if not setting screen

        public OffBallOffensiveDecision(PlayerAction action, ScreenDecisionData? screenData = null)
        {
            Action = action;
            ScreenData = screenData;
        }
    }
    /// <summary>
    /// Defines the contract for AI logic specific to offensive players.
    /// </summary>
    public interface IOffensiveAIController
    {
        /// <summary>
        /// Determines the best offensive action for a player.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to make a decision for.</param>
        /// <returns>The determined offensive action.</returns>
        PlayerAction DetermineOffensiveAction(MatchState state, SimPlayer player);

        /// <summary>
        /// Calculates the optimal offensive position for a player.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to calculate position for.</param>
        /// <param name="tactic">The current tactic being used by the player's team.</param>
        /// <returns>The target offensive position vector.</returns>
        Vector2 CalculateOffensivePosition(MatchState state, SimPlayer player, Gameplay.Tactic tactic);

        /// <summary>
        /// Determines the best off-ball action for an offensive player (e.g., positioning, setting screens).
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to make a decision for.</param>
        /// <param name="tactic">The current tactic being used by the player's team.</param> // Added missing param doc
        /// <returns>An OffBallOffensiveDecision struct containing the action and potential screen details.</returns>
        OffBallOffensiveDecision DetermineOffBallOffensiveAction(MatchState state, SimPlayer player, Gameplay.Tactic tactic);

        /// <summary>
        /// Determines the action for an offensive player during a set piece.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player data.</param>
        /// <param name="tactic">The current tactic.</param>
        /// <returns>The determined player action and potential target player.</returns>
        (PlayerAction action, SimPlayer targetPlayer) HandleSetPieceAction(MatchState state, SimPlayer player, Gameplay.Tactic tactic);

        /// <summary>
        /// Determines the action for the designated penalty taker.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player data.</param>
        /// <returns>The determined player action.</returns>
        PlayerAction HandlePenaltyAction(MatchState state, SimPlayer player);

        /// <summary>
        /// Finds the best passing target for the given player.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="passer">The player looking to pass.</param>
        /// <returns>The SimPlayer of the best target, or null if none found.</returns>
        SimPlayer FindBestPassTarget(MatchState state, SimPlayer passer);

        // Add other offensive-specific methods as needed


    }
}