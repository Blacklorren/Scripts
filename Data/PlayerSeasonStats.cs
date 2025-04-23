using System;

namespace HandballManager.Data
{
    [Serializable]
    public class PlayerSeasonStats
    {
        public int PlayerID;
        public string PlayerName;
        public int TeamID;
        public int LeagueID;
        public int MatchesPlayed = 0;
        public int Goals = 0;
        public int Assists = 0;
        public int TwoMinuteSuspensions = 0;
        public int RedCards = 0;
        public int ShotsTaken = 0;
        public int ShotsOnTarget = 0;
        public int SavesMade = 0;
        public int PenaltiesScored = 0;
        public int PenaltiesTaken = 0;
        public float ShotAccuracy => ShotsTaken > 0 ? (float)ShotsOnTarget / ShotsTaken * 100f : 0f;
        public float GoalEfficiency => ShotsOnTarget > 0 ? (float)Goals / ShotsOnTarget * 100f : 0f;
        public float GoalsPerMatch => MatchesPlayed > 0 ? (float)Goals / MatchesPlayed : 0f;
        public PlayerSeasonStats() { }
        public PlayerSeasonStats(int playerId, string playerName, int teamId, int leagueId)
        {
            PlayerID = playerId;
            PlayerName = playerName;
            TeamID = teamId;
            LeagueID = leagueId;
        }
    }
}
