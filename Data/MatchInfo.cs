using System;

namespace HandballManager.Data
{
    [Serializable]
    public struct MatchInfo
    {
        public DateTime Date;
        public int HomeTeamID;
        public int AwayTeamID;
        public string Location;
        public string Referee;

        public override bool Equals(object obj)
        {
            if (obj is MatchInfo other)
            {
                return Date == other.Date &&
                       HomeTeamID == other.HomeTeamID &&
                       AwayTeamID == other.AwayTeamID;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Date, HomeTeamID, AwayTeamID);
        }

        public static bool operator ==(MatchInfo left, MatchInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MatchInfo left, MatchInfo right)
        {
            return !(left == right);
        }
    }
}
