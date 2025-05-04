using UnityEngine;
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Defines the contract for AI logic specific to goalkeepers.
    /// </summary>
    public interface IGoalkeeperAIController
    {
        /// <summary>
        /// Determines the best action for the goalkeeper.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The goalkeeper SimPlayer instance.</param>
        /// <returns>The determined goalkeeper action.</returns>
        PlayerAction DetermineGoalkeeperAction(MatchState state, SimPlayer player);

        /// <summary>
        /// Calculates the optimal position for the goalkeeper.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The goalkeeper SimPlayer instance.</param>
        /// <returns>The target goalkeeper position vector.</returns>
        Vector2 CalculateGoalkeeperPosition(MatchState state, SimPlayer player);

        /// <summary>
        /// Determines the action for the goalkeeper during a penalty shootout or in-game penalty.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The goalkeeper SimPlayer instance.</param>
        /// <returns>The determined goalkeeper action (likely GoalkeeperSaving or Idle).</returns>
        PlayerAction HandlePenaltySaveAction(MatchState state, SimPlayer player);

        // Add other goalkeeper-specific methods as needed
    }
}