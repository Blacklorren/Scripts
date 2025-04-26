using System; // For Serializable, DateTime, Guid
using System.Collections.Generic;
using System.Linq;
using HandballManager.Core;
using UnityEngine;
using static HandballManager.Data.TeamMatchStats; // Required for Debug.Log potentially

namespace HandballManager.Data
{
    /// <summary>
    /// Stores detailed team statistics for a single match.
    /// </summary>
    [Serializable]
    public class TeamMatchStats
    {
        // Offensive Stats
        public int ShotsTaken { get; set; } = 0;
        public int ShotsOnTarget { get; set; } = 0; // Shots likely to score if not saved/blocked well
        public int GoalsScored { get; set; } = 0; // Should match the team's score in MatchResult
        public int Turnovers { get; set; } = 0; // Lost possession via bad pass, failed dribble, offensive foul etc.
        public int PenaltiesAwarded { get; set; } = 0; // 7m shots awarded *to* this team
        public int PenaltiesScored { get; set; } = 0; // 7m shots scored *by* this team
        // Optional: public int Assists { get; set; } = 0;
        // Optional: public int FastBreakGoals { get; set; } = 0;

        // Defensive Stats
        public int SavesMade { get; set; } = 0; // Saves made by this team's goalkeeper(s)
        public int BlocksMade { get; set; } = 0; // Shots blocked by this team's field players
        public int Interceptions { get; set; } = 0; // Passes intercepted by this team

        // Disciplinary Stats
        public int FoulsCommitted { get; set; } = 0;
        public int TwoMinuteSuspensions { get; set; } = 0; // Number of 2min penalties received
        public int RedCards { get; set; } = 0; // Number of direct red cards received

        // --- Calculated Properties ---

    // PlayerSnapshotDto and MatchSnapshotDto have been moved to their own files in HandballManager.Data.
    // Please use 'using HandballManager.Data;' and reference these DTOs directly.

        /// <summary>Goals Scored / Shots Taken * 100</summary>
        public float ShootingPercentage => ShotsTaken == 0 ? 0f : (float)GoalsScored / ShotsTaken * 100f;

        /// <summary>Shots On Target / Shots Taken * 100</summary>
        public float ShotsOnTargetPercentage => ShotsTaken == 0 ? 0f : (float)ShotsOnTarget / ShotsTaken * 100f;

        /// <summary>Penalties Scored / Penalties Awarded * 100</summary>
        public float PenaltyConversionPercentage => PenaltiesAwarded == 0 ? 0f : (float)PenaltiesScored / PenaltiesAwarded * 100f;

        // Optional: Calculate Save Percentage (requires opponent's SOT) - Cannot be calculated purely within this class.
        // public float SavePercentage(int opponentShotsOnTarget) => opponentShotsOnTarget == 0 ? 0f : (float)SavesMade / opponentShotsOnTarget * 100f;
    }


    /// <summary>
    /// Stores the outcome and detailed statistics of a simulated match.
    /// </summary>
    [Serializable]
    public class MatchResult
    {
        /// <summary>
        /// Retourne toutes les statistiques individuelles des joueurs pour ce match (PlayerID, PlayerMatchStats).
        /// </summary>
        public IEnumerable<(int playerId, PlayerMatchStats stats)> GetAllPlayerStats()
        {
            return PlayerPerformances.Select(kvp => (kvp.Key, kvp.Value));
        }

        // --- Core Match Identifiers & Outcome ---
        public Guid MatchID { get; private set; } // Unique identifier for the match
        public int HomeTeamID { get; set; }
        public int AwayTeamID { get; set; }
        public string HomeTeamName { get; set; } = "Home";
        public string AwayTeamName { get; set; } = "Away";
        public int HomeScore { get; set; } = 0;
        public int AwayScore { get; set; } = 0;
        public DateTime MatchDate { get; set; } // Date the match was played

        // --- Error Handling ---
        public string ErrorMessage { get; set; } // Stores any error message if the match simulation fails
        public bool IsAborted { get; set; } = false; // Indicates if the match was aborted due to an error

        // --- Detailed Statistics ---
        public TeamMatchStats HomeStats { get; set; } = new TeamMatchStats();
        public TeamMatchStats AwayStats { get; set; } = new TeamMatchStats();

        /// <summary>
        /// List of key events that occurred during the match (goals, fouls, etc.).
        /// </summary>
        public List<MatchEventDto> MatchEvents { get; set; } = new List<MatchEventDto>();

        /// <summary>
        /// A serializable snapshot of the final state of the match at the end of simulation.
        /// </summary>
        public MatchSnapshotDto FinalMatchSnapshot { get; set; } // DTO now imported from HandballManager.Data

        /// <summary>
        /// Statistiques individuelles des joueurs pour ce match (clé = PlayerID).
        /// </summary>
        public Dictionary<int, PlayerMatchStats> PlayerPerformances { get; set; } = new Dictionary<int, PlayerMatchStats>();
        // public int Attendance { get; set; }


        /// <summary>
        /// Default constructor for serialization only. Do not use for new results.
        /// </summary>
        public MatchResult()
        {
            PlayerPerformances = new Dictionary<int, PlayerMatchStats>();
            MatchID = Guid.NewGuid();
            HomeStats = new TeamMatchStats();
            AwayStats = new TeamMatchStats();
            MatchDate = DateTime.MinValue; // Should be set by deserializer
            MatchEvents = new List<MatchEventDto>();
        }

        /// <summary>
        /// Constructor for MatchResult. Initializes core info and stats objects.
        /// </summary>
        /// <param name="homeId">Home Team ID.</param>
        /// <param name="awayId">Away Team ID.</param>
        /// <param name="homeName">Home Team Name.</param>
        /// <param name="awayName">Away Team Name.</param>
        /// <param name="matchDate">Date of the match (must be provided by simulation layer).</param>
        public MatchResult(int homeId, int awayId, string homeName, string awayName, DateTime matchDate)
        {
            PlayerPerformances = new Dictionary<int, PlayerMatchStats>();
            MatchID = Guid.NewGuid();
            HomeTeamID = homeId;
            AwayTeamID = awayId;
            HomeTeamName = homeName ?? "Home";
            AwayTeamName = awayName ?? "Away";
            HomeStats = new TeamMatchStats();
            AwayStats = new TeamMatchStats();
            MatchDate = matchDate;
            MatchEvents = new List<MatchEventDto>();
        }

         /// <summary>
         /// Returns a string representation of the match scoreline.
         /// </summary>
         public override string ToString()
         {
             return $"{HomeTeamName} {HomeScore} - {AwayScore} {AwayTeamName}";
         }

         /// <summary>
         /// Determines the result from the perspective of the specified team ID.
         /// </summary>
         /// <param name="teamIdPerspective">The ID of the team whose perspective to use.</param>
         /// <returns>Win, Draw, or Loss.</returns>
         public MatchOutcome GetOutcomeForTeam(int teamIdPerspective)
         {
             if (HomeScore == AwayScore) return MatchOutcome.Draw;

             if (teamIdPerspective == HomeTeamID)
             {
                 return (HomeScore > AwayScore) ? MatchOutcome.Win : MatchOutcome.Loss;
             }
             else if (teamIdPerspective == AwayTeamID)
             {
                 return (AwayScore > HomeScore) ? MatchOutcome.Win : MatchOutcome.Loss;
             }
             else
             {
                  // Only log warning if team ID is valid but doesn't match
                  if (teamIdPerspective > 0) {
                     Debug.LogWarning($"Team ID {teamIdPerspective} did not participate in match {MatchID} ({HomeTeamName} vs {AwayTeamName}).");
                  }
                 return MatchOutcome.Draw; // Or throw exception? Return Draw for safety.
             }
         }
    

     /// <summary>
     /// Represents the outcome of a match for one team.
     /// </summary>
     public enum MatchOutcome
     {
         Win,
         Draw,
         Loss
     }
    }
} 