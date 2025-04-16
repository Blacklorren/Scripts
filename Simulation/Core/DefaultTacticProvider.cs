using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Simulation.Core.Interfaces;

namespace HandballManager.Simulation.Core
{
    /// <summary>
    /// Default implementation of ITacticProvider that returns the user's chosen tactic for the player team
    /// and the AI's tactic for the AI team.
    /// </summary>
    public class DefaultTacticProvider : ITacticProvider
    {
        private readonly TeamData _userTeam;
        private readonly Tactic _userTactic;
        private readonly Tactic _aiTactic;

        public DefaultTacticProvider(TeamData userTeam, Tactic userTactic, Tactic aiTactic)
        {
            _userTeam = userTeam;
            _userTactic = userTactic;
            _aiTactic = aiTactic;
        }

        public Tactic GetTacticForTeam(TeamData team)
        {
            // Return user's tactic if this is the user's team, otherwise return AI tactic
            if (team == _userTeam)
                return _userTactic;
            return _aiTactic;
        }
    }
}
