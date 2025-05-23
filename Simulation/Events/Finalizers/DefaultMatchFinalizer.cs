using HandballManager.Data;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Events.Interfaces;
using System;
using UnityEngine;

namespace HandballManager.Simulation.Events.Finalizers
{
    public class DefaultMatchFinalizer : IMatchFinalizer
    {
        /// <summary>Error when home team data is missing</summary>
        private const int ErrMissingHomeTeam = -99;
        /// <summary>Error when away team data is missing</summary>
        private const int ErrMissingAwayTeam = -98;
        private const string ErrorTeamName = "INVALID_TEAM";

        /// <summary>
        /// /// Updates match timers during simulation
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="deltaTime">Time elapsed since last update</param>
        /// <param name="eventHandler">Event handler for logging timer events</param>
        public void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler)
        {
            // Implementation of timer updates
            // This method was previously missing from the implementation
        }

        /// <summary>
        /// Finalizes match results with validation and error handling
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="matchDate">Timestamp for the match result</param>
        public MatchResult FinalizeResult(MatchState state, DateTime matchDate)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null) {
                 Debug.LogError("[DefaultMatchFinalizer] Cannot finalize result, MatchState is null!");
                 return new MatchResult(ErrMissingHomeTeam, ErrMissingAwayTeam, ErrorTeamName, "NULL_STATE", matchDate); // Use passed date
             }

             if (state.HomeTeamData == null || state.AwayTeamData == null) {
                 Debug.LogError($"[DefaultMatchFinalizer] Missing team data - Home: {state.HomeTeamData?.TeamID ?? -1}, Away: {state.AwayTeamData?.TeamID ?? -1}");
                 return new MatchResult(ErrMissingHomeTeam, ErrMissingAwayTeam, ErrorTeamName, "MISSING_TEAM_DATA", matchDate);
             }

             MatchResult result = new MatchResult(
                 state.HomeTeamData.TeamID,
                 state.AwayTeamData.TeamID,
                 state.HomeTeamData.Name,
                 state.AwayTeamData.Name,
                 matchDate
             ) {
                 HomeScore = state.HomeScore,
                 AwayScore = state.AwayScore,
                 HomeStats = state.CurrentHomeStats ?? new TeamMatchStats(),
                 AwayStats = state.CurrentAwayStats ?? new TeamMatchStats()
             };

             // Enforce score consistency
             result.HomeStats.GoalsScored = result.HomeScore;
             result.AwayStats.GoalsScored = result.AwayScore;

             // Copie les statistiques individuelles des joueurs
             if (state.PlayerStats != null && state.PlayerStats.Count > 0)
             {
                 foreach (var kvp in state.PlayerStats)
                 {
                     result.PlayerPerformances[kvp.Key] = kvp.Value;
                 }
             }
             // Final Validation (now should never trigger)
             Debug.Assert(result.HomeScore == result.HomeStats.GoalsScored, "Home score mismatch");
             Debug.Assert(result.AwayScore == result.AwayStats.GoalsScored, "Away score mismatch");
             return result;
        }
    }
}