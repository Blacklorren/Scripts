using HandballManager.Data;
using HandballManager.Gameplay;

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Provides tactics for a given team (user or AI coach).
    /// </summary>
    public interface ITacticProvider
    {
        /// <summary>
        /// Returns the tactic to use for the specified team.
        /// </summary>
        Tactic GetTacticForTeam(TeamData team);
    }
}
