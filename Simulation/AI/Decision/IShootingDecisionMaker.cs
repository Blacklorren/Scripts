using HandballManager.Gameplay;
using HandballManager.Simulation.Engines; // For Tactic

namespace HandballManager.Simulation.AI.Decision // Updated to match new folder structure
{
    /// <summary>
    /// Interface for evaluating the desirability of taking a shot for an AI player.
    /// </summary>
    public interface IShootingDecisionMaker
    {
        /// <summary>
        /// Calculates a score representing how desirable taking a shot is in the current situation.
        /// </summary>
        /// <param name="shooter">The player considering the shot.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's current tactic.</param>
        /// <returns>A score between 0 and 1 (higher is more desirable).</returns>
        float EvaluateShootScore(SimPlayer shooter, MatchState state, Tactic tactic);

        /// <summary>
        /// Returns a float score representing the desirability of shooting in the current context.
        /// </summary>
        float DecideAction(SimPlayer shooter, MatchState state);

    }
}