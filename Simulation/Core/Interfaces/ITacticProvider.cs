using HandballManager.Data;
using HandballManager.Gameplay;

namespace HandballManager.Simulation.Core.Interfaces
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
