using HandballManager.Data;
using HandballManager.Core; 
using System.Collections.Generic;
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.Utils 
{
    public interface IPlayerSetupHandler
    {
        bool PopulateAllPlayers(MatchState state);
        bool SelectStartingLineups(MatchState state);
        bool SelectStartingLineup(MatchState state, TeamData team, int teamSimId);
        void PlacePlayersInFormation(MatchState state, List<SimPlayer> players, bool isHomeTeam, bool isKickOff);
        SimPlayer FindPlayerByPosition(MatchState state, List<SimPlayer> lineup, PlayerPosition position); // Removed Core. prefix
    }
}