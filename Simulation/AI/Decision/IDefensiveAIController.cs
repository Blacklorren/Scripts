using UnityEngine;
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Core;
using HandballManager.Simulation.Engines; // Added for MatchState & SimPlayer

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Interface for AI logic specific to defensive players.
    /// Defines the actions and calculations a defensive AI can perform.
    /// </summary>
    public interface IDefensiveAIController
    {
        /// <summary>
        /// Determines the best defensive action for a player.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="simPlayer">The SimPlayer instance representing the player in the simulation.</param>
        /// <param name="tactic">The current team tactic.</param>
        /// <returns>The determined defensive action.</returns>
        PlayerAction DetermineDefensiveAction(MatchState state, SimPlayer simPlayer, Tactic tactic);

        /// <summary>
        /// Calculates the ideal defensive position for the player based on the tactic and current state.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="simPlayer">The SimPlayer instance representing the player in the simulation.</param>
        /// <param name="tactic">The current tactic being used by the player's team.</param>
        /// <returns>The target defensive position vector.</returns>
        Vector2 CalculateDefensivePosition(MatchState state, SimPlayer simPlayer, Gameplay.Tactic tactic);

        // Add other defensive-specific methods as needed
    }
}