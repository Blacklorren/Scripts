using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Linq operations
using HandballManager.Data;          // Core data structures
using HandballManager.Simulation;    // Simulation engines
using HandballManager.Gameplay;    // Gameplay systems (Tactic, Contract, Transfer)
using HandballManager.Management;    // League, Schedule, Finance managers
using HandballManager.Simulation.Engines;
using System.Threading;
using HandballManager.Simulation.Events;
using HandballManager.Simulation.Installers;
using Zenject;
using HandballManager.Core;  // For GameState enum
using HandballManager.UI;    // For IUIManager

namespace HandballManager.Core.GameManager
{
    // Basic placeholder HandballManager.Data.MatchInfo for schedule (ensure namespace matches if defined elsewhere)
    
        /// <summary>
    /// Singleton GameManager responsible for overall game state,
    /// managing core systems, and triggering the main game loop updates.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // --- Singleton Instance ---
        private static GameManager instance;
        public static GameManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<GameManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("GameManager");
                        instance = go.AddComponent<GameManager>();
                    }
                }
                return instance;
            }
        }

        // --- Dependencies (Injected via Zenject) ---
        [Inject] public TimeManager TimeManager { get; private set; }
        [Inject] public LeagueManager LeagueManagerDI { get; private set; }
        [Inject] public ScheduleManager ScheduleManager { get; private set; }
        [Inject] public IUIManager UIManagerRef { get; private set; }
        [Inject] public IMatchSimulationCoordinator simCoordinator { get; private set; } // Renamed for clarity
        [Inject] public IEventBus EventBus { get; private set; }
        [Inject] public SaveDataManager SaveDataManager { get; private set; } // Harmonized
        [Inject] public FinanceManager FinanceManager { get; private set; } // Harmonized
        [Inject] public TrainingSimulator TrainingSimulator { get; private set; } // Harmonized
        [Inject] public MoraleSimulator MoraleSimulator { get; private set; } // Harmonized
        [Inject] public PlayerDevelopment PlayerDevelopment { get; private set; } // Harmonized

        // --- Game Data Lists ---
        public List<LeagueData> AllLeagues { get; private set; } = new();
        public List<TeamData> AllTeams { get; private set; } = new();
        public List<PlayerData> AllPlayers { get; private set; } = new(); // Caution: Potentially large!
        public List<StaffData> AllStaff { get; private set; } = new();
        public TeamData PlayerTeam { get; private set; } // Reference to the player-controlled team within AllTeams
 
        // --- Game State ---
        public GameState CurrentState { get; private set; } = GameState.MainMenu; // État par défaut
        
        // --- Match State ---
        private MatchState _currentMatchStateForUI; // État du match actuel pour l'UI

        // --- Constants ---
        private const string SAVE_FILE_NAME = "handball_manager_save.json";
        // Use configurable season dates or constants
        private static readonly DateTime SEASON_START_DATE = new DateTime(DateTime.Now.Year, 7, 1); // July 1st
        private static readonly DateTime OFFSEASON_START_DATE = new DateTime(DateTime.Now.Year, 6, 1); // June 1st

        // --- Unity Methods ---
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Duplicate GameManager detected. Destroying new instance.");
                Destroy(this.gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(this.gameObject);

            InitializeSystems();
        }

        private void Start()
        {
            Debug.Log("GameManager Started. Initial State: " + CurrentState);
            UIManagerRef?.UpdateUIForGameState(CurrentState); // Ensure UI matches initial state
        }

        private void Update()
        {
            // Simple state-based updates or debug controls
            if (Input.GetKeyDown(KeyCode.Space) && IsInActivePlayState())
            {
                AdvanceTime();
            }
             else if (Input.GetKeyDown(KeyCode.M) && IsInActivePlayState() && PlayerTeam != null)
             {
                SimulateNextPlayerMatch(); // Debug key to simulate the next scheduled match
            }
             else if (Input.GetKeyDown(KeyCode.F5)) // Quick Save
             {
                 SaveGame();
             }
             else if (Input.GetKeyDown(KeyCode.F9)) // Quick Load
             {
                 LoadGame();
             }
        }

        // --- Initialization ---
        private void InitializeSystems()
        {
            // Initialize service container
            // serviceContainer = new ServiceContainer();
            
            // Register event bus first
            // serviceContainer.Bind<IEventBus, EventBus>();
            
            // Register core services
            // var timeManager = new TimeManager(new DateTime(2024, 7, 1)); // Default start date
            // serviceContainer.BindInstance(timeManager);
            
            // Find essential MonoBehaviour systems
            // UIManagerRef = UIManager.Instance;
            // if (UIManagerRef == null) Debug.LogError("UIManager could not be found or created!");
            // serviceContainer.BindInstance(UIManagerRef);

            // Register simulation services
            // serviceContainer.Bind<IMatchEngine, MatchEngine>();
            // serviceContainer.Bind<IMatchSimulationCoordinator, MatchSimulationCoordinator>();
            // serviceContainer.BindInstance<TrainingSimulator>(new TrainingSimulator());
            // serviceContainer.BindInstance<MoraleSimulator>(new MoraleSimulator());
            // serviceContainer.BindInstance<PlayerDevelopment>(new PlayerDevelopment());
            // serviceContainer.BindInstance<TransferManager>(new TransferManager());
            // serviceContainer.BindInstance<ContractManager>(new ContractManager());
            // serviceContainer.BindInstance<LeagueManager>(new LeagueManager());
            // serviceContainer.BindInstance<ScheduleManager>(new ScheduleManager());
            // serviceContainer.BindInstance<FinanceManager>(new FinanceManager());
            
            // Install simulation bindings
            // var simulationInstaller = gameObject.AddComponent<SimulationInstaller>();
            // simulationInstaller.InstallBindings();
            
            Debug.Log("Core systems initialized with dependency injection.");
            
            // Subscribe to TimeManager events AFTER all systems are initialized
            TimeManager.OnDayAdvanced += HandleDayAdvanced;
            TimeManager.OnWeekAdvanced += HandleWeekAdvanced;
            TimeManager.OnMonthAdvanced += HandleMonthAdvanced;
        }

        // --- Game State Management ---
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState && newState != GameState.SimulatingMatch) // Allow re-entering sim state
            {
                // Avoid redundant state changes unless it's starting a simulation again
                // Debug.LogWarning($"Attempted to change state to {newState}, but already in this state.");
                return;
            }

            GameState oldState = CurrentState;
            CurrentState = newState;
            Debug.Log($"GameState changed from {oldState} to {newState}");

            // Notify UI and other systems about the state change
            UIManagerRef?.UpdateGameStateUI(newState);
            EventBus?.Publish(new GameStateChangedEvent { OldState = oldState, NewState = newState });

            // Handle state-specific logic (like showing/hiding UI panels)
            HandleStateChangeUI(oldState, newState);
        }

        /// <summary>
        /// Handles UI changes specifically related to game state transitions.
        /// Separated from ChangeState for clarity.
        /// </summary>
        private void HandleStateChangeUI(GameState oldState, GameState newState)
        {
            // --- Tactics Panel Logic ---
            // Note: GameState n'a pas de valeurs Timeout ou HalfTime, nous utilisons donc uniquement Paused
            // Si vous avez besoin de distinguer entre différents types de pauses, vous pouvez utiliser une variable supplémentaire
            bool shouldShowTactics = newState == GameState.Paused;
            bool wasShowingTactics = oldState == GameState.Paused;

            if (shouldShowTactics)
            {
                // Si nous sommes en pause mais que nous n'avons pas d'état de match, nous utilisons directement la tactique de l'équipe du joueur
                if (PlayerTeam != null)
                {
                    // Deux cas possibles : soit nous avons un état de match valide, soit nous utilisons directement la tactique de l'équipe
                    if (_currentMatchStateForUI != null)
                    {
                        // Cas 1 : Nous avons un état de match valide
                        // Nous allons récupérer la tactique directement à partir de l'équipe du joueur
                        // au lieu d'utiliser TeamSim qui cause des problèmes de compilation
                        var playerTactic = TacticConverter.FromData(PlayerTeam.CurrentTactic ?? new TacticData());
                        UIManagerRef?.ShowTacticsPanel(playerTactic, true); // Show and enable interaction
                    }
                    else
                    {
                        // Cas 2 : Nous n'avons pas d'état de match, nous utilisons directement la tactique de l'équipe
                        var playerTactic = TacticConverter.FromData(PlayerTeam.CurrentTactic ?? new TacticData());
                        UIManagerRef?.ShowTacticsPanel(playerTactic, true); // Show and enable interaction
                    }
                }
                else
                {
                    Debug.LogWarning("Cannot show tactics panel: Match state or Player team is null.");
                    UIManagerRef?.HideTacticsPanel(); // Ensure hidden if state is invalid
                }
            }
            else if (wasShowingTactics) // If we are LEAVING a state where tactics were shown
            {
                UIManagerRef?.HideTacticsPanel();
            }

            // --- Other UI Panel Logic (Example) ---
            if (newState == GameState.ManagingTeam)
            {
                // Ensure other relevant panels are shown/hidden
            }
            // Add more UI handling logic for other states as needed
        }

        /// <summary>Checks if the game is in a state where time can advance or saving is allowed.</summary>
        public bool IsInActivePlayState()
        {
            return CurrentState == GameState.ManagingTeam || CurrentState == GameState.InSeason || CurrentState == GameState.OffSeason;
        }

        // --- Game Actions ---
        public void SimulateNextPlayerMatch()
        {
            if (PlayerTeam == null) return;

            // Get next scheduled match for player team
            var nextMatch = ScheduleManager.GetUpcomingMatchesForTeam(PlayerTeam.TeamID, TimeManager.CurrentDate)
                .FirstOrDefault(m => m.HomeTeamID == PlayerTeam.TeamID || m.AwayTeamID == PlayerTeam.TeamID);

            if(nextMatch != default(HandballManager.Data.MatchInfo))
            {
                // Get team references from IDs
                var homeTeam = AllTeams.FirstOrDefault(t => t.TeamID == nextMatch.HomeTeamID);
                var awayTeam = AllTeams.FirstOrDefault(t => t.TeamID == nextMatch.AwayTeamID);

                if (homeTeam != null && awayTeam != null)
                {
                    ChangeState(GameState.SimulatingMatch);
                    var progress = new Progress<float>();
                    var cancellationToken = new CancellationTokenSource().Token;
                    SimulateMatch(homeTeam, awayTeam, TacticConverter.FromData(homeTeam.CurrentTactic), TacticConverter.FromData(awayTeam.CurrentTactic),
                        UnityEngine.Random.Range(1, 999999), progress, cancellationToken);
                    ChangeState(GameState.MatchReport);
                }
                else
                {
                    Debug.LogError("Could not find teams for match simulation");
                }
            }
            else
            {
                Debug.Log("No scheduled matches found for player team on current date");
            }
        }
        public void StartNewGame()
        {
            Debug.Log("Starting New Game...");
            ChangeState(GameState.Loading);
            UIManagerRef?.DisplayPopup("Loading New Game...");

            // 1. Clear Existing Data
            AllLeagues.Clear(); AllTeams.Clear(); AllPlayers.Clear(); AllStaff.Clear(); PlayerTeam = null;
            // Ensure managers are available before using them
            if (LeagueManagerDI == null || ScheduleManager == null || TimeManager == null)
            {
                Debug.LogError("Cannot start new game: Core managers not injected!");
                UIManagerRef?.DisplayPopup("Error: Missing core game components!");
                ChangeState(GameState.MainMenu); // Revert to main menu
                return;
            }

            LeagueManagerDI.ResetTablesForNewSeason(); // Ensure tables are clear
            ScheduleManager.HandleSeasonTransition(); // Clear old schedule
            // Schedule generation happens after loading teams

            // 2. Load Default Database
            LoadDefaultDatabase(); // Populates the lists
            if (AllTeams.Count == 0)
            {
                Debug.LogError("Failed to load default database or database is empty!");
                UIManagerRef?.DisplayPopup("Error: Could not load team data!");
                ChangeState(GameState.MainMenu); // Revert
                return;
            }

            // 3. Assign Player Team
            PlayerTeam = AllTeams[0]; // Simplified: Assign first team
            if (PlayerTeam == null) // Should not happen if AllTeams is not empty, but defensive check
            {
                 Debug.LogError("Failed to assign player team even though teams exist!");
                 UIManagerRef?.DisplayPopup("Error: Could not assign player team!");
                 ChangeState(GameState.MainMenu);
                 return;
            }
            Debug.Log($"Player assigned control of team: {PlayerTeam.Name} (ID: {PlayerTeam.TeamID})");

            // 4. Set Initial Time
            TimeManager.SetDate(new DateTime(2024, 7, 1)); // Standard start date

            // 5. Initial Setup (schedule, league tables)
            ScheduleManager.GenerateNewSchedule(AllTeams, TimeManager.CurrentDate); // Generate AFTER teams are loaded
            foreach(var league in AllLeagues)
            { // Initialize tables for all leagues
                LeagueManagerDI.InitializeLeagueTable(league.LeagueID, AllTeams, true);
            }

            // 6. Transition to Initial Game State
            Debug.Log("New Game Setup Complete.");
            // Check UIManager again before using
            if (UIManagerRef != null)
            {
                UIManagerRef?.ShowTeamScreen(PlayerTeam); // Show team screen initially
                ChangeState(GameState.ManagingTeam); // Set state to managing team
            }
            else
            {
                 Debug.LogError("UIManager not available after setup, cannot show team screen.");
                 ChangeState(GameState.OffSeason); // Fallback state
            }
        }

        /// <summary>Loads initial data into the game lists. Placeholder implementation.</summary>
        private void LoadDefaultDatabase()
        {
            Debug.Log("Loading Default Database (Placeholders)...");
            // TODO: Replace with actual loading from files (ScriptableObjects, JSON, etc.)

            AllLeagues.Add(new LeagueData { LeagueID = 1, Name = "Handball Premier League" });

            TeamData pTeam = CreatePlaceholderTeam(1, "HC Player United", 5000, 1000000);
            AllTeams.Add(pTeam);
            AllPlayers.AddRange(pTeam.Roster);

            for (int i = 2; i <= 8; i++) {
                TeamData aiTeam = CreatePlaceholderTeam(i, $"AI Team {i-1}", 4000 + (i*100), 750000 - (i*20000));
                aiTeam.LeagueID = 1;
                AllTeams.Add(aiTeam);
                AllPlayers.AddRange(aiTeam.Roster);
            }
             Debug.Log($"Loaded {AllLeagues.Count} leagues, {AllTeams.Count} teams, {AllPlayers.Count} players.");
        }

        /// <summary>
        /// Orchestration centrale du chargement :
        /// Toute la logique de répartition des données chargées doit rester ici (GameManager).
        /// SaveDataManager ne manipule que des types Data purs (DTOs, primitives).
        /// Ajouter toute nouvelle donnée à restaurer ici, en passant par les méthodes d’import des managers.
        /// </summary>
        public void LoadGame()
        {
            // Use the injected SaveDataManager
            if (SaveDataManager == null)
            {
                Debug.LogError("SaveDataManager not injected. Cannot load game.");
                UIManagerRef?.DisplayPopup("Error: Save system unavailable.");
                return;
            }

            // Get the most recent save file path
            string filePath = SaveDataManager.GetMostRecentSavePath();
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("No save files found."); 
                UIManagerRef?.DisplayPopup("No save files found."); 
                return;
            }

            Debug.Log($"Loading Game from {filePath}...");
            ChangeState(GameState.Loading); 
            UIManagerRef?.DisplayPopup("Loading Game...");

            try
            {
                // Load the game using the SaveDataManager
                SaveData saveData = SaveDataManager.LoadGame(filePath);

                if (saveData != null)
                {
                    // Restore Data Lists (Clear existing first)
                    AllLeagues = saveData.Leagues ?? new List<LeagueData>();
                    AllTeams = saveData.Teams ?? new List<TeamData>();
                    AllPlayers = saveData.Players ?? new List<PlayerData>();
                    AllStaff = saveData.Staff ?? new List<StaffData>();

                    // Restore Time
                    TimeManager.SetDate(new DateTime(saveData.CurrentDateTicks));

                    // Restore Player Team Reference
                    PlayerTeam = AllTeams.FirstOrDefault(t => t.TeamID == saveData.PlayerTeamID);
                    if (PlayerTeam == null && AllTeams.Count > 0) {
                        Debug.LogWarning($"Saved Player Team ID {saveData.PlayerTeamID} not found, assigning first team."); 
                        PlayerTeam = AllTeams[0];
                    }

                    // Restore LeagueManager state from lists
                    Dictionary<int, List<LeagueStandingEntry>> loadedTables = new Dictionary<int, List<LeagueStandingEntry>>();
                    if (saveData.LeagueTableKeys != null && saveData.LeagueTableValues != null && saveData.LeagueTableKeys.Count == saveData.LeagueTableValues.Count) {
                        for(int i=0; i<saveData.LeagueTableKeys.Count; i++) {
                            loadedTables.Add(saveData.LeagueTableKeys[i], saveData.LeagueTableValues[i]);
                        }
                    }
                    LeagueManagerDI?.RestoreTablesFromSave(loadedTables);

                    // TODO: Restaurer le planning via ScheduleManager si la sauvegarde du planning est implémentée
                    // if (saveData.ScheduleKeys != null && saveData.ScheduleValues != null)
                    //     ScheduleManager?.RestoreSchedulesFromSave(...); // à définir

                    // Restore Game State (Set directly before ChangeState triggers UI/logic)
                    CurrentState = saveData.CurrentGameState;

                    Debug.Log($"Game Loaded Successfully. Date: {TimeManager.CurrentDate.ToShortDateString()}, State: {CurrentState}");

                    // Publish load completed event
                    EventBus?.Publish(new GameStateChangedEvent
                    {
                        OldState = GameState.Loading,
                        NewState = CurrentState
                    });

                    // Trigger state logic and UI update for loaded state
                    ChangeState(CurrentState); 
                } else { 
                    throw new Exception("Failed to deserialize save data (SaveData is null)."); 
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading game: {e.Message}\n{e.StackTrace}");
                UIManagerRef?.DisplayPopup($"Error loading game: {e.Message}");
                // Revert to main menu on failure
                AllLeagues.Clear(); AllTeams.Clear(); AllPlayers.Clear(); AllStaff.Clear(); PlayerTeam = null;
                // Attempt re-initialization, checking dependencies
                if (TimeManager != null) InitializeSystems(); else Debug.LogError("Cannot re-initialize systems: TimeManager missing.");
                ChangeState(GameState.MainMenu);
            }
        }

        /// <summary>
        /// Orchestration centrale de la sauvegarde :
        /// Toute la logique de collecte des états à sauvegarder doit rester ici (GameManager).
        /// SaveDataManager ne manipule que des types Data purs (DTOs, primitives).
        /// Ajouter toute nouvelle donnée à sauvegarder ici, en passant par les méthodes d’export des managers.
        /// </summary>
        public void SaveGame()
        {
             if (!IsInActivePlayState() && CurrentState != GameState.MainMenu && CurrentState != GameState.Paused)
             {
                 Debug.LogWarning($"Cannot save game in current state: {CurrentState}");
                 UIManagerRef?.DisplayPopup($"Cannot save in state: {CurrentState}"); return;
             }

             // Use the injected SaveDataManager
             if (SaveDataManager == null)
             {
                 Debug.LogError("SaveDataManager not injected. Cannot save game.");
                 UIManagerRef?.DisplayPopup("Error: Save system unavailable.");
                 return;
             }
             // Check other essential managers needed for saving
             if (LeagueManagerDI == null || TimeManager == null)
             {
                 Debug.LogError("Cannot save game: LeagueManager or TimeManager not available.");
                 UIManagerRef?.DisplayPopup("Error: Core components missing for save.");
                 return;
             }

            Debug.Log("Saving Game...");
            UIManagerRef?.DisplayPopup("Saving Game..."); // Temporary popup

            try
            {
                // Get league tables from LeagueManager
                var leagueTables = LeagueManagerDI?.GetTablesForSave() ?? new Dictionary<int, List<LeagueStandingEntry>>();
                 string savePath = SaveDataManager.SaveGame(
                      CurrentState,
                      AllTeams,
                      TimeManager,
                      PlayerTeam?.TeamID ?? -1,
                      AllLeagues,
                      AllPlayers,
                      AllStaff,
                      leagueTables
                  );

                 Debug.Log($"Game Saved Successfully to {savePath}.");
                 UIManagerRef?.DisplayPopup("Game Saved!");

                // Publish save completed event
                EventBus?.Publish(new GameStateChangedEvent
                {
                    OldState = GameState.Loading,
                    NewState = CurrentState
                });
            }
             catch (Exception e)
             {
                 Debug.LogError($"Error saving game: {e.Message}\n{e.StackTrace}");
                 UIManagerRef?.DisplayPopup($"Error saving game: {e.Message}");
             }
        }

        /// <summary>Advances time by one day, triggering relevant daily updates.</summary>
        public void AdvanceTime()
        {
            if (!IsInActivePlayState()) { Debug.LogWarning($"Cannot advance time in state: {CurrentState}"); return; }
            TimeManager?.AdvanceDay(); // Events trigger daily processing
        }

        // --- Event Handlers ---
        private void HandleDayAdvanced()
        {
            // 1. Check for scheduled matches today
            List<HandballManager.Data.MatchInfo> matchesToday = ScheduleManager?.GetMatchesForDate(TimeManager.CurrentDate) ?? new List<HandballManager.Data.MatchInfo>();
            bool playerMatchSimulatedToday = false;
            foreach (var matchInfo in matchesToday)
            {
                if (playerMatchSimulatedToday) break; // Only process one player match per day step

                 TeamData home = AllTeams.FirstOrDefault(t => t.TeamID == matchInfo.HomeTeamID);
                 TeamData away = AllTeams.FirstOrDefault(t => t.TeamID == matchInfo.AwayTeamID);
                 if (home != null && away != null) {
                     bool isPlayerMatch = (home == PlayerTeam || away == PlayerTeam);
                     if (isPlayerMatch) {
                          Debug.Log($"Player match scheduled today: {home.Name} vs {away.Name}. Triggering simulation.");
                          SimulateMatch(home, away); // This changes state and pauses time
                          playerMatchSimulatedToday = true;
                     } else {
                          // AI vs AI match - Simulate silently in the background
                          SimulateMatch(home, away);
                     }
                 } else { Debug.LogWarning($"Could not find teams for scheduled match: HomeID={matchInfo.HomeTeamID}, AwayID={matchInfo.AwayTeamID}"); }
            }

            // If a player match was triggered and paused time, stop further daily processing
            if (playerMatchSimulatedToday) return;

            // --- Continue Daily Processing if no player match paused time ---

            // 2. Update player injury status (ALL players)
             foreach (var player in AllPlayers) { player.UpdateInjuryStatus(TimeManager.CurrentDate); }

            // 3. Process transfer/contract daily steps (Placeholders)
             // TransferManager?.ProcessDaily();
             // ContractManager?.ProcessDaily();

            // 4. Update player condition (non-training recovery)
              foreach (var player in AllPlayers) {
                 if (!player.IsInjured(TimeManager.CurrentDate) && player.Condition < 1.0f) {
                     player.Condition = Mathf.Clamp(player.Condition + 0.02f * (player.NaturalFitness / 75f), 0.1f, 1f);
                 }
              }
             // 5. News Generation TODO
        }

         private void HandleWeekAdvanced()
         {
             Debug.Log($"GameManager handling Week Advanced: Week starting {TimeManager.CurrentDate.ToShortDateString()}");
             // Check all required managers for weekly processing
             if (LeagueManagerDI == null || FinanceManager == null || TrainingSimulator == null || MoraleSimulator == null)
             { // Added MoraleSimulator check
                 Debug.LogError("One or more managers are null during HandleWeekAdvanced! Aborting weekly tasks."); return;
             }

             // 1. Simulate Training for ALL Teams
             foreach (var team in AllTeams) {
                 TrainingFocus focus = (team == PlayerTeam) ? TrainingFocus.General : GetAITrainingFocus(team); // TODO: Get player focus setting
                 TrainingSimulator.SimulateWeekTraining(team, focus, Intensity.Normal, TimeManager.CurrentDate);
             }

             // 2. Update Morale for ALL Teams
              foreach (var team in AllTeams) { MoraleSimulator.UpdateMoraleWeekly(team, TimeManager.CurrentDate, LeagueManagerDI); }

             // 3. Update Finances for ALL Teams
             FinanceManager.ProcessWeeklyPayments(AllTeams);

             // 4. Update League Tables
             LeagueManagerDI.UpdateStandings(); // Recalculate and sort tables based on results processed daily
         }

        private void HandleMonthAdvanced()
        {
            Debug.Log($"GameManager handling Month Advanced: New Month {TimeManager.CurrentDate:MMMM yyyy}");
            // Check required managers for monthly processing
            if (FinanceManager == null)
            { 
                Debug.LogError("FinanceManager is null during HandleMonthAdvanced! Aborting monthly tasks."); return;
            }

            // 1. Monthly Finances
            FinanceManager.ProcessMonthly(AllTeams);

            // 2. Scouting / Youth Dev TODOs...

            // 3. Check Season Transition
             CheckSeasonTransition();
        }

        // --- Simulation Trigger ---
        // Using the injected simCoordinator from line 61

        // Surcharge qui accepte les arguments supplémentaires mais les ignore
        public void SimulateMatch(TeamData home, TeamData away, Tactic homeTactic, Tactic awayTactic, int seed, IProgress<float> progress, CancellationToken cancellationToken)
        {
            // Appel à la méthode principale en ignorant les arguments supplémentaires
            SimulateMatch(home, away, homeTactic, awayTactic);
        }

        public async void SimulateMatch(TeamData home, TeamData away, Tactic homeTactic = null, Tactic awayTactic = null)
        {
            using var cts = new System.Threading.CancellationTokenSource();
            try
            {
                if (simCoordinator == null)
                {
                    Debug.LogError("Simulation coordinator not available!");
                    return;
                }

                if (home == null || away == null)
                {
                    Debug.LogError("Cannot simulate match with null teams.");
                    return;
                }

                bool isPlayerMatch = (home == PlayerTeam || away == PlayerTeam);
                if (isPlayerMatch)
                {
                    ChangeState(GameState.SimulatingMatch);
                }

                // Handle null tactics safely
                Tactic validatedHomeTactic = homeTactic ?? (home != null ? TacticConverter.FromData(home.CurrentTactic) : null) ?? new Tactic();
                Tactic validatedAwayTactic = awayTactic ?? (away != null ? TacticConverter.FromData(away.CurrentTactic) : null) ?? new Tactic();

                // Should handle cancellation before processing results
                MatchResult result = await simCoordinator.RunSimulationAsync(
                    home,
                    away,
                    validatedHomeTactic,
                    validatedAwayTactic,
                    cts.Token
                ).ConfigureAwait(true); // Keep context for Unity thread

                if (result == null) return;

                result.MatchDate = TimeManager.CurrentDate;
                Debug.Log($"Match Result: {result}");

                // --- Post-Match Processing ---
                // 1. Update Morale
                MoraleSimulator.UpdateMoralePostMatch(home, result, TimeManager.CurrentDate, AllTeams);
                MoraleSimulator.UpdateMoralePostMatch(away, result, TimeManager.CurrentDate, AllTeams);

                // 2. Apply Fatigue
                Action<TeamData> processFatigue = (team) => {
                 if(team?.Roster != null) {
                     // Apply to players assumed to have played (needs better tracking from MatchEngine ideally)
                     foreach(var p in team.Roster.Take(10)) { // Simple: affect first 10 players
                         if (!p.IsInjured(TimeManager.CurrentDate)) p.Condition = Mathf.Clamp(p.Condition - UnityEngine.Random.Range(0.1f, 0.25f), 0.1f, 1.0f);
                     }
                 }
            };
            processFatigue(home);
            processFatigue(away);

            // 3. Update League Tables
            LeagueManagerDI.ProcessMatchResult(result, AllTeams); // Send result to league manager (AllTeams injected)

            // 4. Generate News TODO


            // --- Update UI and State for Player Match ---
            if (isPlayerMatch) {
                UIManagerRef?.ShowMatchPreview(result); // Show results panel
                ChangeState(GameState.MatchReport); // Go to report state (Time remains paused)
            }
            simCoordinator.CleanupResources();
            }
            catch (ValidationException ex)
            {
                HandleInvalidMatchState(ex);
            }
            catch (SimulationException ex) when (ex.ErrorType == SimulationErrorType.RuntimeError)
            {
                HandleRuntimeSimulationFailure(ex);
            }
            finally
            {
                CleanupSimulationResources();
            }
            // AI vs AI match processing finishes here for GameManager. Time continues if not paused by player match.
        }

        /// <summary>
        /// Handles invalid match state exceptions by logging the error and resetting the game state.
        /// </summary>
        private void HandleInvalidMatchState(ValidationException ex)
        {
            Debug.LogError($"Match validation error: {ex.Message}");
            UIManagerRef?.DisplayPopup($"Match simulation failed: {ex.Message}");

            // Reset to a safe state
            ChangeState(GameState.ManagingTeam);

            // Additional cleanup if needed
            CleanupSimulationResources();
        }

        /// <summary>
        /// Handles runtime simulation failures by logging the error and resetting the game state.
        /// </summary>
        private void HandleRuntimeSimulationFailure(SimulationException ex)
        {
            Debug.LogError($"Match simulation runtime error: {ex.Message}");
            UIManagerRef?.DisplayPopup($"Match simulation failed: {ex.Message}");

            // Reset to a safe state
            ChangeState(GameState.ManagingTeam);

            // Additional cleanup if needed
            CleanupSimulationResources();
        }

        /// <summary>
        /// Cleans up any resources used during simulation.
        /// </summary>
        private void CleanupSimulationResources()
        {
            // Release any resources that might be held during simulation
            // This could include temporary data structures, cached results, etc.
            // Currently a placeholder for future implementation
        }

        // --- Season Transition Logic ---
        private void CheckSeasonTransition()
         {
             DateTime currentDate = TimeManager.CurrentDate;
             int currentYear = currentDate.Year;
             DateTime offSeasonStart = new DateTime(currentYear, OFFSEASON_START_DATE.Month, OFFSEASON_START_DATE.Day);
             DateTime newSeasonStart = new DateTime(currentYear, SEASON_START_DATE.Month, SEASON_START_DATE.Day);
             DateTime nextSeasonStartCheck = newSeasonStart;

             if (currentDate.Date >= newSeasonStart.Date) { // If it's July 1st or later this year...
                 offSeasonStart = offSeasonStart.AddYears(1); // ...off-season starts next year...
                 nextSeasonStartCheck = newSeasonStart.AddYears(1); // ...and next season starts next year.
             }
             // Else (before July 1st), use current year's dates for checks.

             // Trigger OffSeason start?
             if (CurrentState == GameState.InSeason && currentDate.Date >= offSeasonStart.Date && currentDate.Date < nextSeasonStartCheck.Date) {
                 StartOffSeason();
             }
             // Trigger New Season start? (Only on the exact date)
             else if ((CurrentState == GameState.OffSeason || CurrentState == GameState.MainMenu) && currentDate.Date == newSeasonStart.Date) {
                  StartNewSeason();
             }
         }

        private void StartOffSeason()
        {
            if (CurrentState == GameState.OffSeason) return; // Avoid double trigger
            Debug.Log($"--- Starting Off-Season {TimeManager.CurrentDate.Year} ---");

            // Check dependencies before proceeding
            if (LeagueManagerDI == null || PlayerDevelopment == null || ScheduleManager == null || TimeManager == null)
            {
                Debug.LogError("Cannot start off-season: Missing required managers!");
                // Attempt to recover or revert state?
                // For now, just log and potentially stop further processing.
                // ChangeState(GameState.MainMenu); // Or a specific error state?
                return;
            }

            ChangeState(GameState.OffSeason);

            LeagueManagerDI.FinalizeSeason(AllTeams); // Awards, Promotions/Relegations (AllTeams injected)

            // ContractManager?.ProcessExpiries(AllPlayers, AllStaff, AllTeams); // TODO

            foreach(var player in AllPlayers) { PlayerDevelopment.ProcessAnnualDevelopment(player); }

            // Staff Expiries TODO
            // News TODO

            // Generate New Schedule (clears old one implicitly)
             ScheduleManager?.GenerateNewSchedule(AllTeams, TimeManager.CurrentDate);

            UIManagerRef?.DisplayPopup("Off-Season has begun!");
        }

        private void StartNewSeason()
        {
             if (CurrentState == GameState.InSeason) return; // Avoid double trigger
             Debug.Log($"--- Starting New Season {TimeManager.CurrentDate.Year}/{(TimeManager.CurrentDate.Year + 1)} ---");

             // Check dependencies
             if (LeagueManagerDI == null || TimeManager == null)
             {
                 Debug.LogError("Cannot start new season: Missing LeagueManager or TimeManager!");
                 // ChangeState(GameState.MainMenu); // Or error state
                 return;
             }

             ChangeState(GameState.InSeason);

             // Ensure League Tables are ready/reset for the new season
             // FinalizeSeason might have already reset them, or init here if needed.
             foreach(var league in AllLeagues) {
                 LeagueManagerDI?.InitializeLeagueTable(league.LeagueID, AllTeams, true); // Re-initialize based on current team league IDs
             }

             // League Structure Updates (Promotions reflected in TeamData.LeagueID) TODO
             // Season Objectives TODO
             // News TODO
             // Transfer Window updates TODO

             UIManagerRef?.DisplayPopup("The new season has started!");
        }

        // --- OnDestroy ---
        private void OnDestroy()
        {
             if (TimeManager != null) {
                TimeManager.OnDayAdvanced -= HandleDayAdvanced; TimeManager.OnWeekAdvanced -= HandleWeekAdvanced; TimeManager.OnMonthAdvanced -= HandleMonthAdvanced;
             }
             if (instance == this) { instance = null; }
         }

        // --- Helper Methods ---
        private TeamData CreatePlaceholderTeam(int id, string name, int reputation, float budget)
        {
            // Check dependencies used by CreatePlaceholderPlayer
            if (TimeManager == null)
            {
                Debug.LogError("Cannot create placeholder team: TimeManager is null.");
                return null; // Cannot create players without time context for contracts
            }

             TeamData team = new TeamData { TeamID = id, Name = name, Reputation = reputation, Budget = budget, LeagueID = 1 };
             team.CurrentTactic = new TacticData { Name = "Balanced Default", TacticID = Guid.NewGuid() };
             team.Roster = new List<PlayerData>();
             // Add players (Ensure PlayerData constructor assigns ID)
             team.AddPlayer(CreatePlaceholderPlayer(name + " GK", PlayerPosition.Goalkeeper, 25, 65, 75, team.TeamID));
             team.AddPlayer(CreatePlaceholderPlayer(name + " PV", PlayerPosition.Pivot, 28, 70, 72, team.TeamID));
             team.AddPlayer(CreatePlaceholderPlayer(name + " LB", PlayerPosition.LeftBack, 22, 72, 85, team.TeamID));
             team.AddPlayer(CreatePlaceholderPlayer(name + " RW", PlayerPosition.RightWing, 24, 68, 78, team.TeamID));
             team.AddPlayer(CreatePlaceholderPlayer(name + " CB", PlayerPosition.CentreBack, 26, 75, 78, team.TeamID));
             team.AddPlayer(CreatePlaceholderPlayer(name + " RB", PlayerPosition.RightBack, 23, 66, 82, team.TeamID));
             team.AddPlayer(CreatePlaceholderPlayer(name + " LW", PlayerPosition.LeftWing, 21, 69, 88, team.TeamID));
             for(int i=0; i<7; i++) {
                 PlayerPosition pos = (PlayerPosition)(i % 7);
                 if(pos == PlayerPosition.Goalkeeper) pos = PlayerPosition.Pivot; // Avoid too many GKs
                 team.AddPlayer(CreatePlaceholderPlayer(name + $" Sub{i+1}", pos, UnityEngine.Random.Range(19, 29), UnityEngine.Random.Range(50, 65), UnityEngine.Random.Range(60, 80), team.TeamID));
             }
            team.UpdateWageBill();
            return team;
        }

        private PlayerData CreatePlaceholderPlayer(string name, PlayerPosition pos, int age, int caEstimate, int pa, int? teamId)
        {
            // Assumes PlayerData constructor handles ID generation
            // Check dependency needed for contract date calculation
            if (TimeManager == null) 
            {
                 Debug.LogError("Cannot create placeholder player: TimeManager is null.");
                 return null; // Cannot set contract expiry
            }

             PlayerData player = new PlayerData {
                 FirstName = name.Contains(" ") ? name.Split(' ')[0] : name, LastName = name.Contains(" ") ? name.Split(' ')[1] : "Player", Age = age,
                 PrimaryPosition = pos, PotentialAbility = pa, CurrentTeamID = teamId,
                 ShootingAccuracy = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90), Passing = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90),
                 Speed = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-10, 10), 30, 90), Strength = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-10, 10), 30, 90),
                 DecisionMaking = Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90),
                 Reflexes = (pos == PlayerPosition.Goalkeeper) ? Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90) : 20,
                 PositioningGK = (pos == PlayerPosition.Goalkeeper) ? Mathf.Clamp(caEstimate + UnityEngine.Random.Range(-5, 5), 30, 90) : 20,
                 Wage = 1000 + (caEstimate * UnityEngine.Random.Range(40, 60)), ContractExpiryDate = TimeManager.CurrentDate.AddYears(UnityEngine.Random.Range(1, 4)),
                 Morale = UnityEngine.Random.Range(0.6f, 0.8f), Condition = 1.0f, Resilience = UnityEngine.Random.Range(40, 85)
             };
             // player.CalculateCurrentAbility(); // Constructor should call this
             return player;
        }

        private TrainingFocus GetAITrainingFocus(TeamData team) {
             Array values = Enum.GetValues(typeof(TrainingFocus));
             return (TrainingFocus)values.GetValue(UnityEngine.Random.Range(0, values.Length - 1)); // Exclude YouthDevelopment for now
        }

    } // End GameManager Class
}