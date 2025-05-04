using HandballManager.Simulation.Engines; // Added to resolve MatchEvent type
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Data;
using HandballManager.Gameplay;
using System; // Required for ArgumentNullException
using HandballManager.Core;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Events.Calculators;

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Contains the complete dynamic state of the simulated match at any given time.
    /// Includes team data, scores, phase, ball/player states, timers, and event logs.
    /// </summary>
    public class MatchState
    {
        /// <summary>
        /// The date of the match (used for injury checks, etc.)
        /// </summary>
        public DateTime MatchDate { get; }

        /// <summary>
        /// Provides AI evaluation of the current game state (risk, aggression, etc.).
        /// </summary>
        public IGameStateEvaluator GameStateEvaluator { get; set; }

        /// <summary>
        /// The random seed used to initialize the simulation's random number generator.
        /// </summary>
        public int RandomSeed { get; private set; }
        // --- Static Match Info ---
        public TeamData HomeTeamData { get; }
        public TeamData AwayTeamData { get; }
        public Tactic HomeTactic { get; }
        public Tactic AwayTactic { get; }

        // --- Dynamic State ---
        /// <summary>Current elapsed time in the match simulation (seconds).</summary>
        public float MatchTimeSeconds { get; set; } = 0f;
        /// <summary>
        /// If true, the match clock is paused and MatchTimeSeconds will not advance.
        /// </summary>
        public bool IsClockPaused { get; set; } = false;
        /// <summary>Total duration of the match in seconds.</summary>
        public float MatchDurationSeconds { get; set; } = 3600f;
        /// <summary>Current score for the home team.</summary>
        public int HomeScore { get; set; } = 0;
        /// <summary>Current score for the away team.</summary>
        public int AwayScore { get; set; } = 0;
        /// <summary>The current phase of the match simulation.</summary>
        public GamePhase CurrentPhase { get; set; } = GamePhase.PreKickOff;
        /// <summary>Team currently considered in possession. 0=Home, 1=Away, -1=None/Contested.</summary>
        public int PossessionTeamId { get; set; } = -1;

        // --- Simulation Objects ---
        /// <summary>The state of the ball.</summary>
        public SimBall Ball { get; }
        /// <summary>Dictionary mapping PlayerData ID to the dynamic SimPlayer state.</summary>
        public Dictionary<int, SimPlayer> AllPlayers { get; } = new Dictionary<int, SimPlayer>();
        /// <summary>List of home team players currently on the court.</summary>
        public List<SimPlayer> HomePlayersOnCourt { get; } = new List<SimPlayer>(7); // Initialize capacity
        /// <summary>List of away team players currently on the court.</summary>
        public List<SimPlayer> AwayPlayersOnCourt { get; } = new List<SimPlayer>(7); // Initialize capacity

        // --- Bench Players (Implemented state variables) ---
        /// <summary>List of home team players currently on the bench.</summary>
        public List<SimPlayer> HomeBench { get; } = new List<SimPlayer>();
        /// <summary>List of away team players currently on the bench.</summary>
        public List<SimPlayer> AwayBench { get; } = new List<SimPlayer>();
        // Note: Logic for substitutions needs to be implemented elsewhere (e.g., MatchSimulator or AI).

        // --- Event Log ---
        /// <summary>Log of significant events that occurred during the simulation.</summary>
        public List<MatchEvent> MatchEvents { get; } = new List<MatchEvent>();

        /// <summary>Timer for team shorthanded penalty after a red card (index 0=Home, 1=Away).</summary>
        public float[] TeamPenaltyTimer { get; private set; } = new float[2] { 0f, 0f };

        /// <summary>
        /// Indicates if passive play warning is currently active for the team in possession.
        /// 0 = Home, 1 = Away, -1 = None
        /// </summary>
        public int PassivePlayWarningTeamId { get; set; } = -1;

        /// <summary>
        /// Returns the score difference for the specified team (0=Home, 1=Away).
        /// Positive if the team is leading, negative if trailing.
        /// </summary>
        public int GetScoreDifference(int teamSimId)
        {
            if (teamSimId == 0)
                return HomeScore - AwayScore;
            else if (teamSimId == 1)
                return AwayScore - HomeScore;
            else
                throw new ArgumentException("Invalid teamSimId (must be 0=Home or 1=Away)");
        }

        /// <summary>
        /// Returns the time left in the match in seconds.
        /// </summary>
        public float GetTimeLeftSeconds()
        {
            return MatchDurationSeconds - MatchTimeSeconds;
        }

        /// <summary>
        /// Dictionnaire des statistiques individuelles des joueurs pour ce match (clé = PlayerID).
        /// </summary>
        public Dictionary<int, PlayerMatchStats> PlayerStats { get; } = new Dictionary<int, PlayerMatchStats>();

        // --- Game Progression & Temporary State ---
        /// <summary>Flag indicating if the simulation has passed the half-time mark.</summary>
        public bool HalfTimeReached { get; set; } = false;
        /// <summary>Flag indicating if the simulation is currently in the second half.</summary>
        public bool IsSecondHalf { get; set; } = false;
        /// <summary>Stores which team (0 or 1) kicked off the first half.</summary>
        public int FirstHalfKickOffTeamId { get; set; } = -1;
        /// <summary>Stores the game phase that was active before a timeout began.</summary>
        public GamePhase PhaseBeforeTimeout { get; set; } = GamePhase.Finished; // Default safe value
        /// <summary>Countdown timer for the current timeout duration (seconds).</summary>
        public float TimeoutTimer { get; set; } = 0f;

        // --- Timeout Counts (Implemented state variables) ---
        /// <summary>Number of timeouts remaining for the home team.</summary>
        public int HomeTimeoutsRemaining { get; set; } = 3; // Default based on common rules
        /// <summary>Number of timeouts remaining for the away team.</summary>
        public int AwayTimeoutsRemaining { get; set; } = 3; // Default based on common rules

        /// <summary>Nombre de temps morts utilisés par mi-temps pour chaque équipe [0]=1ère mi-temps, [1]=2ème mi-temps.</summary>
        public int[] HomeTimeoutsUsedByHalf { get; set; } = new int[2];
        public int[] AwayTimeoutsUsedByHalf { get; set; } = new int[2];
        /// <summary>True si un timeout a déjà été posé dans les 5 dernières minutes (par équipe).</summary>
        public bool HomeTimeoutUsedLast5Min { get; set; } = false;
        public bool AwayTimeoutUsedLast5Min { get; set; } = false;

        /// <summary>True si le 1er timeout a été perdu faute d'utilisation en 1ère mi-temps.</summary>
        public bool HomeFirstTimeoutLost { get; set; } = false;
        public bool AwayFirstTimeoutLost { get; set; } = false;
        // Note: Logic for calling timeouts and enforcing limits needs implementation elsewhere.

        // --- Randomness ---
        /// <summary>The pseudo-random number generator used for this specific match simulation instance.</summary>
        public System.Random RandomGenerator { get; }

        // --- Temporary Stats (Accumulated during simulation) ---
        /// <summary>Accumulated match statistics for the home team.</summary>
        public TeamMatchStats CurrentHomeStats { get; private set; }
        /// <summary>Accumulated match statistics for the away team.</summary>
        public TeamMatchStats CurrentAwayStats { get; private set; }

        /// <summary>
        /// Initializes a new MatchState instance.
        /// </summary>
        /// <param name="homeTeam">Home team data (required).</param>
        /// <param name="awayTeam">Away team data (required).</param>
        /// <param name="homeTactic">Home team tactic (required).</param>
        /// <param name="awayTactic">Away team tactic (required).</param>
        /// <param name="randomSeed">Seed for the simulation's random number generator.</param>
        /// <exception cref="ArgumentNullException">Thrown if required parameters (teams, tactics) are null.</exception>
        public MatchState(TeamData homeTeam, TeamData awayTeam, Tactic homeTactic, Tactic awayTactic, int randomSeed, DateTime matchDate)
        {
            MatchDate = matchDate;
            PlayerStats = new Dictionary<int, PlayerMatchStats>();
            // --- Constructor Validation ---
            HomeTeamData = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
            AwayTeamData = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
            HomeTactic = homeTactic ?? throw new ArgumentNullException(nameof(homeTactic));
            AwayTactic = awayTactic ?? throw new ArgumentNullException(nameof(awayTactic));

            // Initialize critical components
            RandomSeed = randomSeed;
            RandomGenerator = new System.Random(randomSeed);
            Ball = new SimBall(); // Ensure ball is created
            CurrentHomeStats = new TeamMatchStats(); // Ensure stats objects are created
            CurrentAwayStats = new TeamMatchStats();
            TeamPenaltyTimer = new float[2] { 0f, 0f }; // Initialize penalty timers

            HomeTimeoutsUsedByHalf = new int[2];
            AwayTimeoutsUsedByHalf = new int[2];
            HomeTimeoutUsedLast5Min = false;
            AwayTimeoutUsedLast5Min = false;
            HomeFirstTimeoutLost = false;
            AwayFirstTimeoutLost = false;
        }

        /// <summary>
        /// Appelle à utiliser un timeout pour une équipe (0=home, 1=away), enregistre l'utilisation par mi-temps et en toute fin de match.
        /// </summary>
        public void RegisterTimeoutUsage(int teamSimId, float matchTimeSeconds, float matchDurationSeconds)
        {
            bool isFirstHalf = matchTimeSeconds < matchDurationSeconds / 2f;
            bool isLast5Min = (matchDurationSeconds - matchTimeSeconds) <= 300f;
            int halfIdx = isFirstHalf ? 0 : 1;
            if (teamSimId == 0)
            {
                HomeTimeoutsUsedByHalf[halfIdx]++;
                if (isLast5Min) HomeTimeoutUsedLast5Min = true;
            }
            else if (teamSimId == 1)
            {
                AwayTimeoutsUsedByHalf[halfIdx]++;
                if (isLast5Min) AwayTimeoutUsedLast5Min = true;
            }
        }

        /// <summary>
        /// Invalide le 1er timeout si non utilisé en 1ère mi-temps (à appeler à la mi-temps).
        /// </summary>
        public void InvalidateFirstTimeoutIfNotUsed()
        {
            if (HomeTimeoutsUsedByHalf[0] == 0)
            {
                HomeFirstTimeoutLost = true;
                if (HomeTimeoutsRemaining > 0) HomeTimeoutsRemaining--;
            }
            if (AwayTimeoutsUsedByHalf[0] == 0)
            {
                AwayFirstTimeoutLost = true;
                if (AwayTimeoutsRemaining > 0) AwayTimeoutsRemaining--;
            }
        }

        /// <summary>
        /// Retourne true si un timeout peut être posé par l'équipe (en tenant compte des règles strictes).
        /// </summary>
        public bool CanTriggerTimeout(int teamSimId, float matchTimeSeconds, float matchDurationSeconds)
        {
            bool isFirstHalf = matchTimeSeconds < matchDurationSeconds / 2f;
            bool isLast5Min = (matchDurationSeconds - matchTimeSeconds) <= 300f;
            if (teamSimId == 0)
            {
                if (HomeTimeoutsRemaining <= 0) return false;
                if (isLast5Min && HomeTimeoutUsedLast5Min) return false; // Un seul timeout possible dans les 5 dernières minutes
                if (!isFirstHalf && HomeFirstTimeoutLost && HomeTimeoutsRemaining == 2) return false; // 1er timeout perdu
            }
            else if (teamSimId == 1)
            {
                if (AwayTimeoutsRemaining <= 0) return false;
                if (isLast5Min && AwayTimeoutUsedLast5Min) return false;
                if (!isFirstHalf && AwayFirstTimeoutLost && AwayTimeoutsRemaining == 2) return false;
            }
            return true;
        }

        /// <summary>
        /// Retourne le différentiel de buts encaissés sur les X dernières minutes (positif = l'équipe subit).
        /// </summary>
        public int GetGoalDifferentialLastMinutes(int teamSimId, float matchTimeSeconds, float minutes)
        {
            float fromTime = matchTimeSeconds - minutes * 60f;
            int goalsFor = 0, goalsAgainst = 0;
            foreach (var evt in MatchEvents)
            {
                if (evt.TimeSeconds < fromTime) continue;
                if (evt.Description.Contains("But") || evt.Description.Contains("Goal"))
                {
                    if (evt.TeamId == (teamSimId == 0 ? HomeTeamData.TeamID : AwayTeamData.TeamID))
                        goalsFor++;
                    else if (evt.TeamId == (teamSimId == 0 ? AwayTeamData.TeamID : HomeTeamData.TeamID))
                        goalsAgainst++;
                }
            }
            return goalsAgainst - goalsFor;
        }

        // --- Utility Accessors ---

        /// <summary>
        /// Gets an enumerable collection of all players currently on the court (both teams).
        /// Uses yield return for potentially better enumeration performance compared to Concat().
        /// </summary>
        public IEnumerable<SimPlayer> PlayersOnCourt
        {
            get
            {
                // Iterate directly over the lists if they are guaranteed not to be modified
                // during the enumeration by external code accessing this property.
                if (HomePlayersOnCourt != null) {
                     foreach (var player in HomePlayersOnCourt) {
                         if (player != null) yield return player; // Add null check within loop
                     }
                }
                if (AwayPlayersOnCourt != null) {
                     foreach (var player in AwayPlayersOnCourt) {
                         if (player != null) yield return player; // Add null check within loop
                     }
                }
            }
        }

        /// <summary>
        /// Gets a simulation player object by their unique PlayerData ID.
        /// </summary>
        /// <param name="playerId">The ID of the player to retrieve.</param>
        /// <returns>The SimPlayer object, or null if not found.</returns>
        public SimPlayer GetPlayerById(int playerId)
        {
            AllPlayers.TryGetValue(playerId, out SimPlayer player);
            return player;
        }

         /// <summary>
         /// Gets the list of players currently on court for the specified team simulation ID.
         /// </summary>
         /// <param name="teamSimId">The simulation team ID (0 for home, 1 for away).</param>
         /// <returns>The list of players on court, or null if the ID is invalid or lists are null.</returns>
         public List<SimPlayer> GetTeamOnCourt(int teamSimId)
         {
             // Validate teamSimId parameter
             if (teamSimId == 0) return HomePlayersOnCourt;
             if (teamSimId == 1) return AwayPlayersOnCourt;

             Debug.LogWarning($"[MatchState] GetTeamOnCourt called with invalid teamSimId: {teamSimId}");
             return null; // Return null for invalid ID
         }

          /// <summary>
          /// Gets the list of players currently on court for the opposing team.
          /// </summary>
          /// <param name="teamSimId">The simulation team ID (0 for home, 1 for away) of the *reference* team.</param>
          /// <returns>The list of opposing players on court, or null if the ID is invalid or lists are null.</returns>
          public List<SimPlayer> GetOpposingTeamOnCourt(int teamSimId)
          {
             // Validate teamSimId parameter before determining opponent
             if (teamSimId == 0) return AwayPlayersOnCourt;
             if (teamSimId == 1) return HomePlayersOnCourt;

             Debug.LogWarning($"[MatchState] GetOpposingTeamOnCourt called with invalid teamSimId: {teamSimId}");
             return null; // Return null for invalid ID
          }

        /// <summary>
        /// Gets the goalkeeper currently on court for the specified team.
        /// </summary>
        /// <param name="teamSimId">The simulation team ID (0 for home, 1 for away).</param>
        /// <returns>The SimPlayer for the goalkeeper, or null if not found or not on court.</returns>
        public SimPlayer GetGoalkeeper(int teamSimId)
        {
            var teamList = GetTeamOnCourt(teamSimId); // Use validated getter
            // Use FirstOrDefault with null check on BaseData and IsOnCourt check
            return teamList?.FirstOrDefault(p => p?.BaseData?.PrimaryPosition == PlayerPosition.Goalkeeper && p.IsOnCourt && !p.IsSuspended());
        }

         /// <summary>Helper to check if a player belongs to the Home team (TeamSimId 0).</summary>
         /// <param name="player">The player to check.</param>
         /// <returns>True if the player is on the home team, false otherwise or if player is null.</returns>
         public bool IsHomeTeam(SimPlayer player) => player?.TeamSimId == 0; // Safe access with ?.
        /// <summary>
        /// Gets a list of opponents who might be able to intercept a pass along the given path.
        /// </summary>
        /// <param name="passer">The player attempting to pass.</param>
        /// <param name="passDirection">The normalized direction of the pass.</param>
        /// <param name="passDistance">The distance of the pass.</param>
        /// <returns>A list of potential intercepting players.</returns>
        public List<SimPlayer> GetPotentialInterceptors(SimPlayer passer, Vector3 passDirection, float passDistance)
        {
            List<SimPlayer> potentialInterceptors = new List<SimPlayer>();

            if (passer == null)
                return potentialInterceptors;

            // Get opposing team players
            List<SimPlayer> opposingTeam = GetOpposingTeamOnCourt(passer.TeamSimId);
            if (opposingTeam == null)
                return potentialInterceptors;

            Vector2 passStart2D = CoordinateUtils.To2DGround(ActionCalculatorUtils.GetPosition3D(passer));
            Vector2 passEnd2D = CoordinateUtils.To2DGround(ActionCalculatorUtils.GetPosition3D(passer) + passDirection * passDistance);
            float radiusSqr = ActionResolverConstants.PRE_PASS_INTERCEPTION_RADIUS * ActionResolverConstants.PRE_PASS_INTERCEPTION_RADIUS;

            foreach (SimPlayer opponent in opposingTeam)
            {
                // Calculate squared distance from player to pass line
                float distToLine = SimulationUtils.CalculateDistanceToLine(
                    opponent.Position,
                    passStart2D,
                    passEnd2D
                );
                float distToLineSqr = distToLine * distToLine;

                // Check if player is close enough to potentially intercept
                if (distToLineSqr <= radiusSqr)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    UnityEngine.Debug.Log($"[PrePassInterception] Potential interceptor: {opponent.BaseData?.FullName} (distToLine: {distToLine:F2})");
#endif
                    potentialInterceptors.Add(opponent);
                }
            }

            return potentialInterceptors;
        }

        /// <summary>
        /// Assigns the initial formation slot roles to players based on the team's offensive tactic.
        /// Should be called *after* the MatchState is constructed and player lists are populated.
        /// </summary>
        public void InitializePlayerRoles()
        {
            AssignRolesForTeam(HomePlayersOnCourt, HomeTactic?.OffensiveFormationData, 0);
            AssignRolesForTeam(AwayPlayersOnCourt, AwayTactic?.OffensiveFormationData, 1);
        }

        private void AssignRolesForTeam(List<SimPlayer> players, FormationData formation, int teamSimId)
        {
            if (players == null || formation == null || formation.Slots == null)
            {
                Debug.LogError($"[MatchState] Cannot assign roles for Team {teamSimId}: Player list or FormationData is null.");
                return;
            }

            // Reset existing roles before assigning new ones
            foreach(var player in players) {
                if(player != null) player.AssignedFormationSlotRole = null;
            }

            HashSet<SimPlayer> assignedPlayers = new HashSet<SimPlayer>();
            int assignedCount = 0;
            int playerCount = players.Count(p => p != null); // Count non-null players

            // Iterate through defined formation slots
            foreach (var slot in formation.Slots)
            {
                if (slot == null) continue;

                // Find the first *unassigned* player on the court matching the slot's associated position
                SimPlayer playerToAssign = players.FirstOrDefault(p =>
                    p != null &&
                    !assignedPlayers.Contains(p) &&
                    p.BaseData != null &&
                    p.BaseData.PrimaryPosition == slot.AssociatedPosition);

                if (playerToAssign != null)
                {
                    playerToAssign.AssignedFormationSlotRole = slot.RoleName;
                    assignedPlayers.Add(playerToAssign);
                    assignedCount++;
                    // Debug.Log($"[MatchState] Assigned Role '{slot.RoleName}' to Player {playerToAssign.GetPlayerId()} (Team {teamSimId}) based on Position {slot.AssociatedPosition}");
                }
                else
                {
                    Debug.LogWarning($"[MatchState] No unassigned player found for Team {teamSimId} matching slot '{slot.RoleName}' (Position: {slot.AssociatedPosition}) in formation '{formation.FormationName}'.");
                }
            }

            // Log if not all players were assigned a role (e.g., formation has fewer slots than players on court)
            if (assignedCount < playerCount)
            {
                Debug.LogWarning($"[MatchState] Team {teamSimId}: Only assigned roles to {assignedCount} out of {playerCount} players based on formation '{formation.FormationName}'. Some players may not have a specific formation role assigned.");
                // Log unassigned players
                foreach(var player in players)
                {
                    if(player != null && !assignedPlayers.Contains(player))
                    {
                         Debug.LogWarning($"[MatchState] Team {teamSimId}: Player {player.GetPlayerId()} ({player.BaseData?.PrimaryPosition}) remains unassigned.");
                    }
                }
            }
             // Log if not all slots were filled (e.g., fewer players than slots)
            if (assignedCount < formation.Slots.Count)
            {
                 Debug.LogWarning($"[MatchState] Team {teamSimId}: Only filled {assignedCount} out of {formation.Slots.Count} slots in formation '{formation.FormationName}'. Some slots may be empty.");
            }
        }
    }
}