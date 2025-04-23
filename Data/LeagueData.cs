using System;

namespace HandballManager.Data
{
    /// <summary>
    /// Data Transfer Object for a League.
    /// </summary>
    [Serializable]
    public class LeagueData
    {
        public int LeagueID;
        public string Name;
        // Add standings, teams list etc. here as needed
    }
}
