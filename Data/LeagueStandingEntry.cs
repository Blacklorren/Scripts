using System;

namespace HandballManager.Data
{
    [Serializable]
    public class LeagueStandingEntry
    {
        public int TeamID;
        public string TeamName;
        public int Played = 0;
        public int Wins = 0;
        public int Draws = 0;
        public int Losses = 0;
        public int GoalsFor = 0;
        public int GoalsAgainst = 0;
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points => (Wins * 2) + (Draws * 1);
        // Head-to-head tracking (not serialized)
        [NonSerialized] public System.Collections.Generic.Dictionary<int, HeadToHeadRecord> HeadToHeadRecords = new System.Collections.Generic.Dictionary<int, HeadToHeadRecord>();
        public LeagueStandingEntry() { }
        public LeagueStandingEntry(int teamId, string teamName)
        {
            TeamID = teamId;
            TeamName = teamName;
            HeadToHeadRecords = new System.Collections.Generic.Dictionary<int, HeadToHeadRecord>();
        }
    }
}
