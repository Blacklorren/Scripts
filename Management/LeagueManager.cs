using System;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Data; // For MatchResult, TeamData, LeagueStandingEntry, HeadToHeadRecord, PlayerSeasonStats, HeadToHeadResult
// using HandballManager.Core; // For PlayerPosition, Enums (removed for decoupling GameManager)
using UnityEngine;

namespace HandballManager.Management
{
    // Les DTOs LeagueStandingEntry, HeadToHeadRecord, PlayerSeasonStats, HeadToHeadResult sont désormais dans HandballManager.Data
    // Toute déclaration ou implémentation locale de ces classes/structs doit être supprimée.
    // Les références doivent pointer vers HandballManager.Data.LeagueStandingEntry, etc.
    
    /// <summary>
    /// Manages league standings, processes results, and handles season finalization.
    /// </summary>
    public class LeagueManager
    {
        // Stores league standings. Key is LeagueID, Value is the list of entries for that league.
        private Dictionary<int, List<LeagueStandingEntry>> _leagueTables = new Dictionary<int, List<LeagueStandingEntry>>();

        // Store player statistics for each league
        private Dictionary<int, List<PlayerSeasonStats>> _playerStats = new Dictionary<int, List<PlayerSeasonStats>>();

        // Stores historical player stats per league, each entry is a list of lists (one per season)
        private Dictionary<int, List<List<PlayerSeasonStats>>> _playerStatsHistory = new Dictionary<int, List<List<PlayerSeasonStats>>>();

        // Store head-to-head results for tiebreaking
        private Dictionary<int, List<HeadToHeadResult>> _headToHeadResults = new Dictionary<int, List<HeadToHeadResult>>();

        // Configuration
        private const int PROMOTION_SPOTS = 2; // Number of teams promoted from each league
        private const int RELEGATION_SPOTS = 2; // Number of teams relegated from each league

        /// <summary>
        /// Processes a match result, updating the relevant league table and player statistics.
        /// </summary>
        /// <param name="result">The completed match result.</param>
        public void ProcessMatchResult(MatchResult result, IEnumerable<TeamData> allTeams)
        {
            // Determine the league ID for this match from the home team
            int leagueId = 1; // Default to league 1 if not found
            var homeTeam = allTeams.FirstOrDefault(t => t.TeamID == result.HomeTeamID);
            if (homeTeam != null) leagueId = homeTeam.LeagueID ?? 1;

            // Initialize league table if it doesn't exist
            if (!_leagueTables.ContainsKey(leagueId))
            {
                InitializeLeagueTable(leagueId, allTeams);
            }

            List<LeagueStandingEntry> table = _leagueTables[leagueId];

            // Find team entries in the league table
            LeagueStandingEntry homeEntry = table.FirstOrDefault(e => e.TeamID == result.HomeTeamID);
            LeagueStandingEntry awayEntry = table.FirstOrDefault(e => e.TeamID == result.AwayTeamID);

            if (homeEntry == null || awayEntry == null)
            {
                Debug.LogWarning($"[LeagueManager] Could not find team entries in league table {leagueId} for match: {result}. Re-initializing table.");
                InitializeLeagueTable(leagueId, allTeams, true); // Force re-init
                table = _leagueTables[leagueId]; // Get the updated table
                homeEntry = table.FirstOrDefault(e => e.TeamID == result.HomeTeamID);
                awayEntry = table.FirstOrDefault(e => e.TeamID == result.AwayTeamID);
                if (homeEntry == null || awayEntry == null)
                {
                    Debug.LogError($"[LeagueManager] Failed to find/create entries for teams {result.HomeTeamID}/{result.AwayTeamID} in league {leagueId}.");
                    return;
                }
            }

            // Update Played, Goals
            homeEntry.Played++;
            awayEntry.Played++;
            homeEntry.GoalsFor += result.HomeScore;
            homeEntry.GoalsAgainst += result.AwayScore;
            awayEntry.GoalsFor += result.AwayScore;
            awayEntry.GoalsAgainst += result.HomeScore;

            // Update Win/Draw/Loss and Points
            if (result.HomeScore > result.AwayScore)
            {
                homeEntry.Wins++;
                awayEntry.Losses++;
            }
            else if (result.AwayScore > result.HomeScore)
            {
                awayEntry.Wins++;
                homeEntry.Losses++;
            }
            else
            {
                homeEntry.Draws++;
                awayEntry.Draws++;
            }

            // Update head-to-head records
            UpdateHeadToHeadRecords(homeEntry, awayEntry, result);

            // Track player statistics
            TrackPlayerStats(result, leagueId, allTeams);

            // Store head-to-head result for tiebreaking
            StoreHeadToHeadResult(result, leagueId);

            // Don't call UpdateStandings here, let the weekly handler do it
            Debug.Log($"[LeagueManager] Processed result for League {leagueId}: {result}");
        }

        /// <summary>
        /// Updates the head-to-head records between two teams
        /// </summary>
        private void UpdateHeadToHeadRecords(LeagueStandingEntry homeEntry, LeagueStandingEntry awayEntry, MatchResult result)
        {
            // Ensure dictionaries are initialized
            if (homeEntry.HeadToHeadRecords == null) homeEntry.HeadToHeadRecords = new Dictionary<int, HeadToHeadRecord>();
            if (awayEntry.HeadToHeadRecords == null) awayEntry.HeadToHeadRecords = new Dictionary<int, HeadToHeadRecord>();

            // Get or create head-to-head records
            if (!homeEntry.HeadToHeadRecords.TryGetValue(awayEntry.TeamID, out var homeH2H))
            {
                homeH2H = new HeadToHeadRecord(awayEntry.TeamID);
                homeEntry.HeadToHeadRecords[awayEntry.TeamID] = homeH2H;
            }

            if (!awayEntry.HeadToHeadRecords.TryGetValue(homeEntry.TeamID, out var awayH2H))
            {
                awayH2H = new HeadToHeadRecord(homeEntry.TeamID);
                awayEntry.HeadToHeadRecords[homeEntry.TeamID] = awayH2H;
            }

            // Update the records
            homeH2H.GoalsFor += result.HomeScore;
            homeH2H.GoalsAgainst += result.AwayScore;
            awayH2H.GoalsFor += result.AwayScore;
            awayH2H.GoalsAgainst += result.HomeScore;

            if (result.HomeScore > result.AwayScore)
            {
                homeH2H.Wins++;
                awayH2H.Losses++;
            }
            else if (result.AwayScore > result.HomeScore)
            {
                awayH2H.Wins++;
                homeH2H.Losses++;
            }
            else
            {
                homeH2H.Draws++;
                awayH2H.Draws++;
            }
        }

        /// <summary>
        /// Stores a head-to-head result for tiebreaking purposes
        /// </summary>
        private void StoreHeadToHeadResult(MatchResult result, int leagueId)
        {
            // Initialize if needed
            if (!_headToHeadResults.ContainsKey(leagueId))
            {
                _headToHeadResults[leagueId] = new List<HeadToHeadResult>();
            }

            // Find existing head-to-head result or create new one
            var h2h = _headToHeadResults[leagueId].FirstOrDefault(h =>
                (h.Team1ID == result.HomeTeamID && h.Team2ID == result.AwayTeamID) ||
                (h.Team1ID == result.AwayTeamID && h.Team2ID == result.HomeTeamID));

            if (h2h == null)
            {
                h2h = new HeadToHeadResult(result.HomeTeamID, result.AwayTeamID);
                _headToHeadResults[leagueId].Add(h2h);
            }

            h2h.AddMatch(result);
        }

        /// <summary>
        /// Calculates and sorts the league standings based on points, goal difference, etc.
        /// Usually called weekly by GameManager.
        /// </summary>
        public void UpdateStandings()
        {
            // Debug.Log("[LeagueManager] Updating all league standings...");
            foreach (var leagueId in _leagueTables.Keys.ToList()) // Iterate over keys copy
            {
                UpdateStandingsForLeague(leagueId);
            }
        }

        /// <summary>
        /// Updates standings for a specific league, including head-to-head tiebreakers.
        /// </summary>
        public void UpdateStandingsForLeague(int leagueId)
        {
            if (!_leagueTables.ContainsKey(leagueId)) return; // No table for this league

            var table = _leagueTables[leagueId];

            // First sort by basic criteria
            var sortedTable = table.OrderByDescending(e => e.Points)
                               .ThenByDescending(e => e.GoalDifference)
                               .ThenByDescending(e => e.GoalsFor)
                               .ToList();

            // Apply head-to-head tiebreakers for teams with equal points, goal difference, and goals for
            ApplyHeadToHeadTiebreakers(sortedTable, leagueId);

            _leagueTables[leagueId] = sortedTable; // Replace with sorted list
            Debug.Log($"[LeagueManager] Updated standings for League {leagueId}.");
        }

        /// <summary>
        /// Applies head-to-head tiebreakers for teams with equal standings
        /// </summary>
        private void ApplyHeadToHeadTiebreakers(List<LeagueStandingEntry> table, int leagueId)
        {
            // Find groups of teams with equal points, goal difference, and goals for
            var tiedGroups = table
                .GroupBy(e => new { e.Points, e.GoalDifference, e.GoalsFor })
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var tiedGroup in tiedGroups)
            {
                var tiedTeams = tiedGroup.ToList();
                if (tiedTeams.Count == 2)
                {
                    // Simple case: just two teams tied
                    ResolveHeadToHeadForTwoTeams(tiedTeams, table);
                }
                else if (tiedTeams.Count > 2)
                {
                    // Complex case: mini-league of 3+ teams
                    ResolveMiniLeagueTiebreaker(tiedTeams, leagueId, table);
                }
            }
        }

        /// <summary>
        /// Resolves a head-to-head tiebreaker between two teams
        /// </summary>
        private void ResolveHeadToHeadForTwoTeams(List<LeagueStandingEntry> tiedTeams, List<LeagueStandingEntry> fullTable)
        {
            var team1 = tiedTeams[0];
            var team2 = tiedTeams[1];

            // Check if they have head-to-head records against each other
            if (team1.HeadToHeadRecords.TryGetValue(team2.TeamID, out var h2h))
            {
                // Compare head-to-head points
                if (h2h.Points > team2.HeadToHeadRecords[team1.TeamID].Points)
                {
                    // Team 1 has better head-to-head, ensure it's ranked higher
                    EnsureTeamRankedHigher(team1, team2, fullTable);
                }
                else if (h2h.Points < team2.HeadToHeadRecords[team1.TeamID].Points)
                {
                    // Team 2 has better head-to-head, ensure it's ranked higher
                    EnsureTeamRankedHigher(team2, team1, fullTable);
                }
                else
                {
                    // Equal head-to-head points, check goal difference in head-to-head matches
                    if (h2h.GoalDifference > team2.HeadToHeadRecords[team1.TeamID].GoalDifference)
                    {
                        EnsureTeamRankedHigher(team1, team2, fullTable);
                    }
                    else if (h2h.GoalDifference < team2.HeadToHeadRecords[team1.TeamID].GoalDifference)
                    {
                        EnsureTeamRankedHigher(team2, team1, fullTable);
                    }
                    else
                    {
                        // Equal goal difference, check away goals or fall back to team name
                        // For simplicity, we'll use team name as final tiebreaker
                        if (string.Compare(team1.TeamName, team2.TeamName) < 0)
                        {
                            EnsureTeamRankedHigher(team1, team2, fullTable);
                        }
                        else
                        {
                            EnsureTeamRankedHigher(team2, team1, fullTable);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resolves a mini-league tiebreaker among 3+ teams
        /// </summary>
        private void ResolveMiniLeagueTiebreaker(List<LeagueStandingEntry> tiedTeams, int leagueId, List<LeagueStandingEntry> fullTable)
        {
            // Create a mini-league table with just these teams
            var miniTable = new List<LeagueStandingEntry>();
            foreach (var team in tiedTeams)
            {
                var miniEntry = new LeagueStandingEntry(team.TeamID, team.TeamName);

                // Calculate mini-league stats based on head-to-head results
                foreach (var opponent in tiedTeams)
                {
                    if (team.TeamID == opponent.TeamID) continue;

                    if (team.HeadToHeadRecords.TryGetValue(opponent.TeamID, out var h2h))
                    {
                        miniEntry.Wins += h2h.Wins;
                        miniEntry.Draws += h2h.Draws;
                        miniEntry.Losses += h2h.Losses;
                        miniEntry.GoalsFor += h2h.GoalsFor;
                        miniEntry.GoalsAgainst += h2h.GoalsAgainst;
                        miniEntry.Played += h2h.Wins + h2h.Draws + h2h.Losses;
                    }
                }

                miniTable.Add(miniEntry);
            }

            // Sort the mini-table
            var sortedMiniTable = miniTable
                .OrderByDescending(e => e.Points)
                .ThenByDescending(e => e.GoalDifference)
                .ThenByDescending(e => e.GoalsFor)
                .ThenBy(e => e.TeamName)
                .ToList();

            // Apply the mini-table order to the full table
            for (int i = 0; i < sortedMiniTable.Count - 1; i++)
            {
                var higherTeam = sortedMiniTable[i];
                var lowerTeam = sortedMiniTable[i + 1];

                var higherTeamInFull = tiedTeams.First(t => t.TeamID == higherTeam.TeamID);
                var lowerTeamInFull = tiedTeams.First(t => t.TeamID == lowerTeam.TeamID);

                EnsureTeamRankedHigher(higherTeamInFull, lowerTeamInFull, fullTable);
            }
        }

        /// <summary>
        /// Ensures that one team is ranked higher than another in the table
        /// </summary>
        private void EnsureTeamRankedHigher(LeagueStandingEntry higherTeam, LeagueStandingEntry lowerTeam, List<LeagueStandingEntry> table)
        {
            int higherIndex = table.IndexOf(higherTeam);
            int lowerIndex = table.IndexOf(lowerTeam);

            if (lowerIndex < higherIndex)
            {
                // Swap positions
                table[lowerIndex] = higherTeam;
                table[higherIndex] = lowerTeam;
            }
        }


        /// <summary>
        /// Performs end-of-season processing including awarding titles, promotions, and relegations.
        /// </summary>
        public void FinalizeSeason(IEnumerable<TeamData> allTeams)
        {
            Debug.Log("[LeagueManager] Finalizing season...");

            // Get all league IDs sorted (assuming lower numbers are higher divisions)
            var leagueIds = _leagueTables.Keys.OrderBy(id => id).ToList();

            // Process each league
            foreach (var leagueId in leagueIds)
            {
                var table = _leagueTables[leagueId];
                if (!table.Any()) continue;

                // Announce league winner
                string winner = table[0].TeamName;
                Debug.Log($"[LeagueManager] League {leagueId} winner: {winner}");

                // Handle promotion (except from top league)
                if (leagueId > 1 && table.Count >= PROMOTION_SPOTS)
                {
                    HandlePromotions(leagueId, table, allTeams);
                }

                // Handle relegation (except from bottom league)
                if (leagueId < leagueIds.Max() && table.Count >= RELEGATION_SPOTS)
                {
                    HandleRelegations(leagueId, table, allTeams);
                }
            }

            // Reset tables for the new season
            ResetTablesForNewSeason();

            // Reset player statistics for the new season
            ResetPlayerStats();

            Debug.Log("[LeagueManager] Season finalization complete.");
        }

        /// <summary>
        /// Handles promotion of teams from a league to the division above
        /// </summary>
        private void HandlePromotions(int leagueId, List<LeagueStandingEntry> table, IEnumerable<TeamData> allTeams)
        {
            int targetLeagueId = leagueId - 1; // Promote to the league above (lower number)
            if (allTeams == null) return;

            Debug.Log($"[LeagueManager] Processing promotions from League {leagueId} to League {targetLeagueId}");

            // Get teams to promote (top N teams)
            for (int i = 0; i < PROMOTION_SPOTS && i < table.Count; i++)
            {
                int teamId = table[i].TeamID;
                string teamName = table[i].TeamName;

                // Find the team in the provided list
                var team = allTeams.FirstOrDefault(t => t.TeamID == teamId);
                if (team != null)
                {
                    // Update the team's league ID
                    team.LeagueID = targetLeagueId;
                    Debug.Log($"[LeagueManager] Promoted: {teamName} (ID: {teamId}) from League {leagueId} to League {targetLeagueId}");
                }
                else
                {
                    Debug.LogWarning($"[LeagueManager] Could not find team {teamName} (ID: {teamId}) for promotion.");
                }
            }
        }

        /// <summary>
        /// Handles relegation of teams from a league to the division below
        /// </summary>
        private void HandleRelegations(int leagueId, List<LeagueStandingEntry> table, IEnumerable<TeamData> allTeams)
        {
            int targetLeagueId = leagueId + 1; // Relegate to the league below (higher number)
            if (allTeams == null) return;

            Debug.Log($"[LeagueManager] Processing relegations from League {leagueId} to League {targetLeagueId}");

            // Get teams to relegate (bottom N teams)
            int tableSize = table.Count;
            for (int i = 0; i < RELEGATION_SPOTS && i < tableSize; i++)
            {
                int teamId = table[tableSize - 1 - i].TeamID;
                string teamName = table[tableSize - 1 - i].TeamName;

                // Find the team in the provided list
                var team = allTeams.FirstOrDefault(t => t.TeamID == teamId);
                if (team != null)
                {
                    // Update the team's league ID
                    team.LeagueID = targetLeagueId;
                    Debug.Log($"[LeagueManager] Relegated: {teamName} (ID: {teamId}) from League {leagueId} to League {targetLeagueId}");
                }
                else
                {
                    Debug.LogWarning($"[LeagueManager] Could not find team {teamName} (ID: {teamId}) for relegation.");
                }
            }
        }

        /// <summary>
        /// Resets the statistics in all league tables for the start of a new season.
        /// Keeps the teams in the table structure.
        /// </summary>
        public void ResetTablesForNewSeason()
        {
            Debug.Log("[LeagueManager] Resetting league table statistics for new season.");
            foreach (var table in _leagueTables.Values)
            {
                foreach (var entry in table)
                {
                    entry.Played = 0;
                    entry.Wins = 0;
                    entry.Draws = 0;
                    entry.Losses = 0;
                    entry.GoalsFor = 0;
                    entry.GoalsAgainst = 0;
                }
            }
        }

        /// <summary>
        /// Tracks league-wide player statistics and returns top performers.
        /// </summary>
        /// <param name="leagueId">Optional league ID to filter by</param>
        /// <param name="count">Number of top players to return</param>
        /// <returns>List of top goal scorers for the specified league</returns>
        public List<PlayerSeasonStats> GetTopScorers(int? leagueId = null, int count = 10)
        {
            List<PlayerSeasonStats> allStats = new List<PlayerSeasonStats>();

            // Collect stats from all leagues or just the specified one
            if (leagueId.HasValue)
            {
                if (_playerStats.ContainsKey(leagueId.Value))
                {
                    allStats.AddRange(_playerStats[leagueId.Value]);
                }
            }
            else
            {
                foreach (var leagueStats in _playerStats.Values)
                {
                    allStats.AddRange(leagueStats);
                }
            }

            // Return top scorers sorted by goals
            return allStats
                .OrderByDescending(p => p.Goals)
                .ThenByDescending(p => p.GoalsPerMatch)
                .ThenBy(p => p.MatchesPlayed)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Gets top goalkeepers based on save percentage
        /// </summary>
        /// <param name="leagueId">Optional league ID to filter by</param>
        /// <param name="count">Number of top goalkeepers to return</param>
        /// <returns>List of top goalkeepers for the specified league</returns>
        public List<PlayerSeasonStats> GetTopGoalkeepers(int? leagueId = null, int count = 5)
        {
            List<PlayerSeasonStats> allStats = new List<PlayerSeasonStats>();

            // Collect stats from all leagues or just the specified one
            if (leagueId.HasValue)
            {
                if (_playerStats.ContainsKey(leagueId.Value))
                {
                    allStats.AddRange(_playerStats[leagueId.Value]);
                }
            }
            else
            {
                foreach (var leagueStats in _playerStats.Values)
                {
                    allStats.AddRange(leagueStats);
                }
            }

            // Return top goalkeepers with at least 5 saves
            return allStats
                .Where(p => p.SavesMade >= 5) // Only include goalkeepers with meaningful stats
                .OrderByDescending(p => p.SavesMade)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Tracks player statistics from a match result
        /// </summary>
        private void TrackPlayerStats(MatchResult result, int leagueId, IEnumerable<TeamData> allTeams)
        {
            // Initialize player stats dictionary for this league if needed
            if (!_playerStats.ContainsKey(leagueId))
            {
                _playerStats[leagueId] = new List<PlayerSeasonStats>();
            }

            if (allTeams == null) return;

            // Get the teams
            var homeTeam = allTeams.FirstOrDefault(t => t.TeamID == result.HomeTeamID);
            var awayTeam = allTeams.FirstOrDefault(t => t.TeamID == result.AwayTeamID);
            if (homeTeam == null || awayTeam == null) return;

            // TODO: When player match stats are added to MatchResult, process them here
            // For now, we'll just log that we would process player stats
            Debug.Log($"[LeagueManager] Would process player stats for match: {result.HomeTeamName} vs {result.AwayTeamName}");

            // Example of how to process player stats when they're available:
            /*
            if (result.PlayerStats != null)
            {
                foreach (var playerStat in result.PlayerStats.Values)
                {
                    // Find the player in our season stats
                    var player = allTeams.FirstOrDefault(p => p.PlayerID == playerStat.PlayerID);
                    if (player == null) continue;
                    
                    // Find or create season stats for this player
                    var seasonStat = _playerStats[leagueId].FirstOrDefault(p => p.PlayerID == player.PlayerID);
                    if (seasonStat == null)
                    {
                        seasonStat = new PlayerSeasonStats(player.PlayerID, player.FullName, player.CurrentTeamID ?? 0, leagueId);
                        _playerStats[leagueId].Add(seasonStat);
                    }
                    
                    // Update the stats
                    seasonStat.MatchesPlayed++;
                    seasonStat.Goals += playerStat.Goals;
                    seasonStat.Assists += playerStat.Assists;
                    seasonStat.ShotsTaken += playerStat.ShotsTaken;
                    seasonStat.ShotsOnTarget += playerStat.ShotsOnTarget;
                    seasonStat.SavesMade += playerStat.SavesMade;
                    seasonStat.TwoMinuteSuspensions += playerStat.TwoMinuteSuspensions;
                    seasonStat.RedCards += playerStat.RedCards;
                    seasonStat.PenaltiesScored += playerStat.PenaltiesScored;
                    seasonStat.PenaltiesTaken += playerStat.PenaltiesTaken;
                }
            }
            */
        }

        /// <summary>
        /// Placeholder for providing data to UI components.
        /// </summary>
        /// <param name="leagueId">The ID of the league table to retrieve.</param>
        /// <returns>The list of standing entries, or null if not found.</returns>
        public List<LeagueStandingEntry> GetLeagueTableForUI(int leagueId, IEnumerable<TeamData> allTeams)
        {
            _leagueTables.TryGetValue(leagueId, out var table);
            if (table == null && leagueId > 0)
            {
                Debug.LogWarning($"[LeagueManager] UI requested table for league {leagueId}, but it doesn't exist yet. Initializing.");
                InitializeLeagueTable(leagueId, allTeams);
                _leagueTables.TryGetValue(leagueId, out table);
            }
            return table; // Return the current state (might not be sorted if called mid-week)
        }

        /// <summary>
        /// Initializes the league table structure for a given league ID based on teams in GameManager.
        /// </summary>
        public void InitializeLeagueTable(int leagueId, IEnumerable<TeamData> allTeams, bool forceReinit = false)
        {
            if (_leagueTables.ContainsKey(leagueId) && !forceReinit) return; // Already exists

            Debug.Log($"[LeagueManager] Initializing league table for LeagueID: {leagueId}");
            if (allTeams == null)
            {
                Debug.LogWarning($"[LeagueManager] Cannot initialize league table: teams not available.");
                _leagueTables[leagueId] = new List<LeagueStandingEntry>();
                return;
            }

            var teamsInLeague = allTeams.Where(t => t.LeagueID == leagueId).ToList();

            List<LeagueStandingEntry> newTable = new List<LeagueStandingEntry>();
            foreach (var team in teamsInLeague)
            {
                // Avoid adding duplicates if re-initializing
                if (!newTable.Any(e => e.TeamID == team.TeamID))
                {
                    newTable.Add(new LeagueStandingEntry
                    {
                        TeamID = team.TeamID,
                        TeamName = team.Name
                        // Stats default to 0
                    });
                }
            }

            if (teamsInLeague.Count > 0 && newTable.Count == 0)
            {
                Debug.LogWarning($"[LeagueManager] No teams found or added for League {leagueId} during initialization.");
            }
            else if (teamsInLeague.Count != newTable.Count && forceReinit)
            {
                Debug.LogWarning($"[LeagueManager] Mismatch between teams in league {leagueId} ({teamsInLeague.Count}) and entries created ({newTable.Count}) during re-initialization.");
            }


            _leagueTables[leagueId] = newTable; // Add or replace the table
        }

        /// <summary>
        /// Used by SaveGame to get the current state.
        /// </summary>
        public Dictionary<int, List<LeagueStandingEntry>> GetTablesForSave()
        {
            return _leagueTables;
        }

        /// <summary>
        /// Used by LoadGame to restore the state.
        /// </summary>
        public void RestoreTablesFromSave(Dictionary<int, List<LeagueStandingEntry>> loadedTables)
        {
            _leagueTables = loadedTables ?? new Dictionary<int, List<LeagueStandingEntry>>();
            Debug.Log($"[LeagueManager] Restored {_leagueTables.Count} league tables from save.");
        }

        /// <summary>
        /// Archives current season player stats and resets them for the new season.
        /// </summary>
        private void ResetPlayerStats()
        {
            if (_playerStats == null || _playerStats.Count == 0)
                return;

            // Ensure history dictionary exists
            if (_playerStatsHistory == null)
                _playerStatsHistory = new Dictionary<int, List<List<PlayerSeasonStats>>>();

            foreach (var kvp in _playerStats)
            {
                int leagueId = kvp.Key;
                var currentStats = kvp.Value;

                // Archive current stats (deep copy)
                var archivedStats = currentStats
                    .Select(stat => new PlayerSeasonStats(stat.PlayerID, stat.PlayerName, stat.TeamID, stat.LeagueID)
                    {
                        MatchesPlayed = stat.MatchesPlayed,
                        Goals = stat.Goals,
                        Assists = stat.Assists,
                        TwoMinuteSuspensions = stat.TwoMinuteSuspensions,
                        RedCards = stat.RedCards,
                        ShotsTaken = stat.ShotsTaken,
                        ShotsOnTarget = stat.ShotsOnTarget,
                        SavesMade = stat.SavesMade,
                        PenaltiesScored = stat.PenaltiesScored,
                        PenaltiesTaken = stat.PenaltiesTaken
                    })
                    .ToList();

                if (!_playerStatsHistory.ContainsKey(leagueId))
                    _playerStatsHistory[leagueId] = new List<List<PlayerSeasonStats>>();
                _playerStatsHistory[leagueId].Add(archivedStats);

                // Reset stats for new season
                foreach (var stat in currentStats)
                {
                    stat.MatchesPlayed = 0;
                    stat.Goals = 0;
                    stat.Assists = 0;
                    stat.TwoMinuteSuspensions = 0;
                    stat.RedCards = 0;
                    stat.ShotsTaken = 0;
                    stat.ShotsOnTarget = 0;
                    stat.SavesMade = 0;
                    stat.PenaltiesScored = 0;
                    stat.PenaltiesTaken = 0;
                }
            }
        }
    }
}