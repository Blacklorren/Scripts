using System;

namespace HandballManager.Data
{
    /// <summary>
    /// Stores detailed individual statistics for a single player during a match.
    /// </summary>
    [Serializable]
    public class PlayerMatchStats
    {
        public int GoalsScored = 0;
        public int ShotsTaken = 0;
        public int ShotsOnTarget = 0;
        public int Assists = 0;
        public int SavesMade = 0; // For goalkeepers
        public int Turnovers = 0;
        public int FoulsCommitted = 0;
        public int YellowCards = 0;
        public int TwoMinuteSuspensions = 0;
        public int RedCards = 0;
        public int BlocksMade = 0;
        public int Interceptions = 0;
        public int MinutesPlayed = 0; // Optional: incremented by simulation/substitution logic
        public bool Participated = false; // Set to true if player took part in the match
    }
}
