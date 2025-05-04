using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.AI.Positioning // Updated to match new folder location
{
    /// <summary>
    /// Defines a contract for positioning players according to tactical formations and game situations.
    /// Calculates optimal positions for players based on the current match state and tactical setup.
    /// </summary>
    public interface ITacticPositioner
    {
        /// <summary>
        /// Calculates the optimal position for a player based on tactical considerations.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="player">The player to calculate position for.</param>
        /// <returns>The target position vector for the player.</returns>
        Vector2 GetPlayerTargetPosition(MatchState state, SimPlayer player);

        /// <summary>
        /// Updates the tactical positioning for all players in the match.
        /// </summary>
        /// <param name="state">The current match state containing all player data.</param>
        void UpdateTacticalPositioning(MatchState state);

        /// <summary>
        /// Calculates defensive positions for players based on the current match state.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="defendingTeamId">The team ID of the defending team.</param>
        void PositionDefensivePlayers(MatchState state, int defendingTeamId);

        /// <summary>
        /// Calculates offensive positions for players based on the current match state.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="attackingTeamId">The team ID of the attacking team.</param>
        void PositionOffensivePlayers(MatchState state, int attackingTeamId);

        /// <summary>
        /// Positions players for a free throw situation.
        /// </summary>
        void PositionForFreeThrow(MatchState state);

        /// <summary>
        /// Positions players for a penalty situation.
        /// </summary>
        void PositionForPenalty(MatchState state);

        /// <summary>
        /// Positions players for a kickoff situation.
        /// </summary>
        void PositionForKickOff(MatchState state);

        /// <summary>
        /// Positions players for a throw-in situation.
        /// </summary>
        void PositionForThrowIn(MatchState state);

        /// <summary>
        /// Positions players for a goal throw situation.
        /// </summary>
        void PositionForGoalThrow(MatchState state);

        /// <summary>
        /// Returns a basic screen spot for the screener (e.g., pivot) to set a screen for a target teammate.
        /// This is a simple implementation: places the screener between the defender and the teammate, offset by a small distance.
        /// </summary>
        Vector2 GetScreenSpotForScreener(SimPlayer screener, SimPlayer targetTeammate, SimPlayer defender, float offset = 0.7f);

        /// <summary>
        /// Returns the angle (in degrees) between defender, screener, and screen user (for evaluating screen effectiveness).
        /// </summary>
        float GetScreenAngleBetweenDefenderAndTarget(SimPlayer defender, SimPlayer screener, SimPlayer user);
    }
}