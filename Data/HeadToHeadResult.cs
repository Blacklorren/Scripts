using System;
using System.Collections.Generic;

namespace HandballManager.Data
{
    [Serializable]
    public class HeadToHeadResult
    {
        public int Team1ID;
        public int Team2ID;
        public List<MatchResult> Matches = new List<MatchResult>();
        public HeadToHeadResult() { }
        public HeadToHeadResult(int team1Id, int team2Id)
        {
            Team1ID = team1Id;
            Team2ID = team2Id;
        }
        public void AddMatch(MatchResult result)
        {
            if ((result.HomeTeamID == Team1ID && result.AwayTeamID == Team2ID) ||
                (result.HomeTeamID == Team2ID && result.AwayTeamID == Team1ID))
            {
                Matches.Add(result);
            }
        }
    }
}
