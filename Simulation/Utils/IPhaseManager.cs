using HandballManager.Core;
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.Utils 
{
    public interface IPhaseManager
    {
        bool CheckAndHandleHalfTime(MatchState state, float timeBeforeStep, float timeAfterStep);
        bool CheckAndHandleFullTime(MatchState state, float timeAfterStep);
        void TransitionToPhase(MatchState state, GamePhase newPhase, bool forceSetup = false);
        void HandlePhaseTransitions(MatchState state);
        int DetermineKickoffTeam(MatchState state);
        bool SetupForKickOff(MatchState state, int startingTeamId); // Expose specific setups if needed externally
        bool SetupForSetPiece(MatchState state);
        bool SetupForPenalty(MatchState state);
        bool SetupForHalfTime(MatchState state);
    }
}