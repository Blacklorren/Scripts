using System;

namespace HandballManager.Data
{
    [Serializable]
    public class HeadToHeadRecord
    {
        public int OpponentTeamID;
        public int Wins = 0;
        public int Draws = 0;
        public int Losses = 0;
        public int GoalsFor = 0;
        public int GoalsAgainst = 0;
        public int Points => (Wins * 2) + (Draws * 1);
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public HeadToHeadRecord() { }
        public HeadToHeadRecord(int opponentId)
        {
            OpponentTeamID = opponentId;
        }
    }
}
