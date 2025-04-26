using System;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Simulation.Engines;
using HandballManager.Data;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.AI;
using HandballManager.Simulation.Events.Interfaces;

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Handles tactical substitutions during a match.
    /// </summary>
    public static class SubstitutionManager
    {
        /// <summary>
        /// Attempts to perform a substitution for the given team in the match.
        /// </summary>
        /// <param name="matchState">The current match state.</param>
        /// <param name="playerOut">The player to be substituted out.</param>
        /// <param name="playerIn">The player to be substituted in.</param>
        /// <param name="eventHandler">The event logger for match events.</param>
        /// <param name="tacticPositioner">The tactic positioner (optional, for position reset).</param>
        /// <param name="aiController">The player AI controller (optional, for AI reset).</param>
        /// <returns>True if substitution was successful, false otherwise.</returns>
        public static bool TrySubstitute(
            MatchState matchState,
            SimPlayer playerOut,
            SimPlayer playerIn,
            IMatchEventHandler eventHandler,
            ITacticPositioner tacticPositioner = null,
            IPlayerAIController aiController = null)
        {
            if (matchState == null || playerOut == null || playerIn == null)
                throw new ArgumentNullException("MatchState, playerOut, and playerIn must not be null.");

            // Determine which team
            bool isHome = matchState.IsHomeTeam(playerOut);
            var onCourt = isHome ? matchState.HomePlayersOnCourt : matchState.AwayPlayersOnCourt;
            var bench = isHome ? matchState.HomeBench : matchState.AwayBench;

            // Validation
            if (!onCourt.Contains(playerOut))
                return false; // Player out is not on court
            if (!bench.Contains(playerIn))
                return false; // Player in is not on bench
            if (playerOut == playerIn)
                return false; // Can't substitute same player
            if (playerOut.IsSuspended() || playerIn.IsSuspended())
                return false; // Can't substitute suspended players
            if (playerIn.IsOnCourt)
                return false; // Player in is already on court

            // Perform substitution
            onCourt.Remove(playerOut);
            bench.Add(playerOut);
            bench.Remove(playerIn);
            onCourt.Add(playerIn);

            playerOut.IsOnCourt = false;
            playerIn.IsOnCourt = true;

            // Optionally reset position and AI
            if (tacticPositioner != null && playerIn != null && matchState != null)
            {
                var targetPos = tacticPositioner.GetPlayerTargetPosition(matchState, playerIn);
                playerIn.Position = targetPos;
                playerIn.TargetPosition = targetPos;
            }

            // Log event
            eventHandler?.LogEvent(
                matchState,
                $"Substitution: {playerOut.BaseData?.FullName ?? "Unknown"} out, {playerIn.BaseData?.FullName ?? "Unknown"} in",
                isHome ? 0 : 1,
                playerIn.BaseData?.PlayerID ?? -1
            );

            return true;
        }
    }
}
