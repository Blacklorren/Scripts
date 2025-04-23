using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text; // For StringBuilder
using System.IO; // For File Export
using Zenject;
using HandballManager.Simulation.Engines;
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Core;
using System; // For Math, Random

// Ensure this test script is within the HandballManager.Tests.PlayMode assembly

public class MatchRealismTests : ZenjectIntegrationTestFixture
{
    // --- Test Configuration ---
    // Consider making these configurable via a ScriptableObject or test setup if needed
    private const int NUM_SIMULATIONS_TO_RUN = 10; // START SMALL! Increase later.
    private const float TEST_TIMEOUT_SECONDS = 300f; // Timeout for the entire test method
    private const float SIMULATION_TIMEOUT_PER_MATCH_SECONDS = 30f; // Timeout per individual match simulation
    private const int TEST_RANDOM_SEED = -1; // -1 for time-based, or set a specific seed
    private const int HOME_TEAM_AVG_ABILITY = 70; // Fixed home ability for this simple test
    private const int AWAY_TEAM_AVG_ABILITY = 70; // Fixed away ability
    private const int ABILITY_VARIANCE = 8;
    private const bool EXPORT_RESULTS = true;
    private const string EXPORT_FILENAME = "RealismTestResults.csv";

    // --- TacticData Config (Simple) ---
    private readonly TacticData homeTacticData = new TacticData(); // Or create a specific one
    private readonly TacticData awayTacticData = new TacticData();

    // --- Data Storage ---
    private System.Random testRandom; // Random for test setup only
    private static int _nextTestPlayerId = 20000; // Separate ID counter for test players
    private List<string[]> summaryExportData; // For CSV

    #region Constants for Realism Checks (Copied from MatchEngineTester)
    private const double TYPICAL_MIN_GOALS = 50;
    private const double TYPICAL_MAX_GOALS = 75;
    private const double TYPICAL_MIN_DRAW_PCT = 5;
    private const double TYPICAL_MAX_DRAW_PCT = 20;
    private const double TYPICAL_MIN_SHOTS_TEAM = 45;
    private const double TYPICAL_MAX_SHOTS_TEAM = 70;
    private const double TYPICAL_MIN_SHOOT_PCT = 45;
    private const double TYPICAL_MAX_SHOOT_PCT = 65;
    private const double TYPICAL_MIN_SAVE_PCT = 25;
    private const double TYPICAL_MAX_SAVE_PCT = 40;
    private const double TYPICAL_MIN_TURNOVERS_TEAM = 8;
    private const double TYPICAL_MAX_TURNOVERS_TEAM = 18;
    private const double TYPICAL_MIN_SUSP_TEAM = 1;
    private const double TYPICAL_MAX_SUSP_TEAM = 5;
    #endregion

    // Use UnityTest for Play Mode tests that can span multiple frames
    [UnityTest]
    [Timeout(1000 * (int)TEST_TIMEOUT_SECONDS)] // Timeout in milliseconds for the whole test method
    public IEnumerator RunMultipleSimulations_AnalyzesBasicStats()
    {
        Debug.Log($"===== Starting Match Realism Test (Play Mode) =====");
        LogConfiguration(); // Log settings

        // Setup Zenject and Random Generator
        PreInstall();
        // TODO: Add any additional bindings or installers if needed
        PostInstall();
        testRandom = (TEST_RANDOM_SEED == -1) ? new System.Random() : new System.Random(TEST_RANDOM_SEED);

        // Initialize CSV export data
        summaryExportData = new List<string[]> { GetCsvHeader() };

        List<MatchResult> validResults = new List<MatchResult>();
        int failedSimulations = 0;
        var cancellationTokenSource = new CancellationTokenSource(); // For potential cancellation

        // Get the MatchEngine instance via Zenject
        // Assuming MatchEngine is registered AsSingle or similar in your installer
        var matchEngine = Container.Resolve<IMatchEngine>();
        Assert.IsNotNull(matchEngine, "Failed to resolve IMatchEngine from Zenject container.");

        Debug.Log($"Starting {NUM_SIMULATIONS_TO_RUN} simulations...");
        System.Diagnostics.Stopwatch totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < NUM_SIMULATIONS_TO_RUN; i++)
        {
            Debug.Log($"--- Running Simulation #{i + 1} ---");
            matchEngine.ResetMatch(); // Ensure clean state for each run

            // --- Create Teams using adapted logic ---
            TeamData homeTeam = SetupTestTeam("Test Home", HOME_TEAM_AVG_ABILITY, 1, homeTacticData);
            TeamData awayTeam = SetupTestTeam("Test Away", AWAY_TEAM_AVG_ABILITY, 2, awayTacticData);

            if (homeTeam == null || awayTeam == null)
            {
                Assert.Fail($"Failed to create test teams for Simulation #{i + 1}. Check PlayerData/TeamData setup.");
                yield break; // Stop test if setup fails
            }

            // --- Run Simulation Async within Coroutine ---
            MatchResult currentResult = null;
            Task<MatchResult> simulationTask = null;
            bool timedOut = false;
            simulationTask = matchEngine.SimulateMatchAsync(
                homeTeam, awayTeam,
                TacticConverter.FromData(homeTacticData),
                TacticConverter.FromData(awayTacticData),
                cancellationTokenSource.Token
            );

            float startTime = Time.time;
            while (!simulationTask.IsCompleted)
            {
                if (Time.time - startTime > SIMULATION_TIMEOUT_PER_MATCH_SECONDS)
                {
                    Debug.LogError($"Simulation #{i + 1} timed out after {SIMULATION_TIMEOUT_PER_MATCH_SECONDS}s!");
                    cancellationTokenSource.Cancel(); // Attempt to cancel
                    timedOut = true;
                    break;
                }
                yield return null; // Wait for the next frame
            }

            try
            {
                if (timedOut)
                {
                    failedSimulations++;
                    continue; // Skip to next simulation
                }

                if (simulationTask.IsCompletedSuccessfully)
                {
                    currentResult = simulationTask.Result;
                    if (ValidateResult(currentResult, i + 1))
                    {
                        validResults.Add(currentResult);
                        Debug.Log($"Simulation #{i + 1} finished: {currentResult}");
                    }
                    else
                    {
                        failedSimulations++;
                    }
                }
                else if (simulationTask.IsFaulted)
                {
                    Debug.LogError($"Simulation #{i + 1} failed with exception: {simulationTask.Exception}");
                    failedSimulations++;
                }
                else if (simulationTask.IsCanceled)
                {
                    Debug.LogWarning($"Simulation #{i + 1} was cancelled.");
                    failedSimulations++; // Count cancellations as failures for analysis
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.LogWarning($"Simulation #{i + 1} was cancelled via exception.");
                failedSimulations++;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Unexpected error running simulation #{i + 1}: {ex}");
                failedSimulations++;
            }
            // Short pause between simulations if needed
            // yield return new WaitForSeconds(0.1f);
        }

        totalStopwatch.Stop();
        Debug.Log($"--- Batch Finished ---");
        Debug.Log($"Completed {NUM_SIMULATIONS_TO_RUN} simulations in {totalStopwatch.Elapsed.TotalSeconds:F2} seconds.");
        Debug.Log($"Successful Results: {validResults.Count}, Failed/Invalid: {failedSimulations}");

        // --- Analyze Results ---
        if (validResults.Any())
        {
            AnalyzeResults(validResults, HOME_TEAM_AVG_ABILITY, AWAY_TEAM_AVG_ABILITY);
        }
        else
        {
            Debug.LogWarning("No valid simulation results to analyze.");
            Assert.Fail("No valid simulations completed successfully."); // Fail the test if no results
        }

        // --- Export ---
        if (EXPORT_RESULTS)
        {
            ExportSummaryToCsv();
        }

        // --- Cleanup ---
        Debug.Log("===== Match Realism Test Finished =====");
    }

    #region Helper Methods (Adapted from MatchEngineTester.cs)

    // LogConfiguration method (copied from MatchEngineTester, adapted slightly)
    private void LogConfiguration()
    {
        Debug.Log("--- Test Configuration ---");
        Debug.Log($"Simulations To Run: {NUM_SIMULATIONS_TO_RUN}");
        Debug.Log($"Random Seed: {(TEST_RANDOM_SEED == -1 ? "Time-based" : TEST_RANDOM_SEED.ToString())}");
        Debug.Log($"Home Ability: {HOME_TEAM_AVG_ABILITY}");
        Debug.Log($"Away Ability: {AWAY_TEAM_AVG_ABILITY}");
        Debug.Log($"Ability Variance: +/- {ABILITY_VARIANCE}");
        Debug.Log($"Home Tactic: {homeTacticData?.Name ?? "Default"}"); // Safe access
        Debug.Log($"Away Tactic: {awayTacticData?.Name ?? "Default"}");
        Debug.Log($"Export Results: {EXPORT_RESULTS} (Filename: {EXPORT_FILENAME})");
        Debug.Log("-------------------------");
    }

    // SetupTestTeam method (adapted from MatchEngineTester)
    private TeamData SetupTestTeam(string name, int averageAbility, int teamIdSuffix, TacticData tacticData)
    {
        // Create TeamData instance (constructor handles ID)
        TeamData team = new TeamData
        {
            // TeamID is set by constructor
            Name = name,
            Reputation = 5000,
            Budget = 1000000,
            LeagueID = 1
        };
        team.Tactics = new List<TacticData> { tacticData };
        team.CurrentTacticID = tacticData.TacticID;

        int playersToCreate = 14; // Create a squad
        int gkCreated = 0;
        var positions = Enum.GetValues(typeof(PlayerPosition)).Cast<PlayerPosition>().ToList();

        for (int i = 0; i < playersToCreate; i++)
        {
            PlayerPosition pos;
            // Ensure 2 GKs
            if (gkCreated < 2 && i >= playersToCreate - 2) { pos = PlayerPosition.Goalkeeper; }
            else
            {
                pos = positions[i % positions.Count];
                if (pos == PlayerPosition.Goalkeeper && gkCreated >= 2)
                {
                    pos = PlayerPosition.CentreBack; // Failsafe if cycling lands on GK again
                }
            }
            if (pos == PlayerPosition.Goalkeeper) gkCreated++;

            // Estimate CA/PA
            int ca = averageAbility + testRandom.Next(-ABILITY_VARIANCE, ABILITY_VARIANCE + 1);
            int pa = ca + testRandom.Next(5, 15);
            ca = Mathf.Clamp(ca, 30, 99);
            pa = Mathf.Clamp(pa, ca, 100);

            PlayerData player = CreatePlaceholderPlayer($"P{i + 1}", pos, ca, pa);
            if (player == null)
            {
                Debug.LogError($"Failed to create placeholder player {i + 1} for team {name}");
                continue; // Skip this player if creation failed
            }
            team.AddPlayer(player); // Use TeamData's AddPlayer method
        }

        // Validation after creation
        if (team.Roster.Count(p => p.PrimaryPosition == PlayerPosition.Goalkeeper) < 1)
        {
            Debug.LogError($"Team {name} setup failed: No Goalkeeper added!"); return null;
        }
        if (team.Roster.Count < 7)
        {
            Debug.LogError($"Team {name} setup failed: Only {team.Roster.Count} players added (need at least 7)!"); return null;
        }

        team.UpdateWageBill();
        return team;
    }

    // CreatePlaceholderPlayer method (adapted from MatchEngineTester and PlayerData structure)
    private PlayerData CreatePlaceholderPlayer(string nameSuffix, PlayerPosition pos, int caEstimate, int pa)
    {
        try
        {
            // PlayerData constructor handles ID, personality, initial familiarity etc.
            PlayerData player = new PlayerData();

            // Basic Info
            player.FirstName = "Test";
            player.LastName = $"{pos}{nameSuffix}";
            player.Age = testRandom.Next(19, 31);
            player.PrimaryPosition = pos;
            player.PotentialAbility = pa; // Set potential before calculating initial CA based on attributes

            // --- IMPORTANT: Instantiate BaseData if PlayerData constructor doesn't ---
            // Check PlayerData constructor - if it doesn't do `BaseData = new BaseData();`, add it here:
            // if (player.BaseData == null) player.BaseData = new BaseData(); // Critical if constructor doesn't handle it

            // Contract & Status
            player.Wage = 1000 + (caEstimate * testRandom.Next(40, 70));
            player.Morale = (float)testRandom.NextDouble() * 0.4f + 0.5f;
            player.Condition = 1.0f;
            player.Resilience = testRandom.Next(40, 90);
            player.CurrentInjuryStatus = InjuryStatus.Healthy;
            player.TransferStatus = TransferStatus.Unavailable;

            // Attributes - Set via BaseData property
            Func<int, int, int> GetRandomAttr = (baseVal, variance) =>
                Mathf.Clamp(baseVal + testRandom.Next(-variance, variance + 1), 10, 99);

            int baseSkill = caEstimate;
            int skillVariance = 15;

            // Technical
            player.BaseData.ShootingAccuracy = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.ShootingPower = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Passing = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Technique = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Dribbling = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Tackling = GetRandomAttr(baseSkill - 5, skillVariance);
            player.BaseData.Blocking = GetRandomAttr(baseSkill - 5, skillVariance);

            // Physical
            player.BaseData.Speed = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Agility = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Strength = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Jumping = GetRandomAttr(baseSkill - 5, skillVariance);
            player.BaseData.Stamina = GetRandomAttr(baseSkill + 5, skillVariance);
            player.BaseData.NaturalFitness = GetRandomAttr(baseSkill, skillVariance);
            player.Resilience = GetRandomAttr(baseSkill, skillVariance); // Resilience is on PlayerData, not BaseData in snippet

            // Mental
            player.BaseData.Composure = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Concentration = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Anticipation = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.DecisionMaking = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Teamwork = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.WorkRate = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Positioning = GetRandomAttr(baseSkill - 5, skillVariance);
            player.BaseData.Aggression = GetRandomAttr(baseSkill - 10, skillVariance);
            player.BaseData.Bravery = GetRandomAttr(baseSkill, skillVariance);
            player.BaseData.Leadership = GetRandomAttr(baseSkill - 15, skillVariance);
            player.Determination = GetRandomAttr(baseSkill, skillVariance); // Determination is on PlayerData, not BaseData

            // Goalkeeping
            player.BaseData.Reflexes = 10; player.BaseData.Handling = 10; player.BaseData.PositioningGK = 10;
            player.BaseData.OneOnOnes = 10; player.BaseData.PenaltySaving = 10; player.BaseData.Throwing = 10;
            player.BaseData.Communication = 10;

            if (pos == PlayerPosition.Goalkeeper)
            {
                int gkVariance = 10;
                player.BaseData.Reflexes = GetRandomAttr(baseSkill + 10, gkVariance);
                player.BaseData.Handling = GetRandomAttr(baseSkill + 5, gkVariance);
                player.BaseData.PositioningGK = GetRandomAttr(baseSkill + 5, gkVariance);
                player.BaseData.OneOnOnes = GetRandomAttr(baseSkill, gkVariance);
                player.BaseData.PenaltySaving = GetRandomAttr(baseSkill - 5, gkVariance);
                player.BaseData.Throwing = GetRandomAttr(baseSkill - 5, gkVariance);
                player.BaseData.Communication = GetRandomAttr(baseSkill, gkVariance);
            }

            // Calculate CA *after* setting attributes, constructor might do this anyway
            player.CalculateCurrentAbility();
            // Ensure PA is valid after CA recalculation
            player.PotentialAbility = Mathf.Clamp(pa, player.CurrentAbility, 100);

            // Constructor should handle personality, no need to call GeneratePersonality() again

            return player;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error creating placeholder player {nameSuffix} for position {pos}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            return null; // Return null if creation fails
        }
    }

    // AnalyzeResults method (copied from MatchEngineTester)
    private void AnalyzeResults(List<MatchResult> results, int homeAbilityParam, int awayAbilityParam)
    {
        Debug.Log($"\n----- Analysis for Home Ability: {homeAbilityParam} vs Away Ability: {awayAbilityParam} ({results.Count} valid matches) -----");
        int numMatches = results.Count;

        // Aggregate Score Statistics
        List<int> homeScores = results.Select(r => r.HomeScore).ToList();
        List<int> awayScores = results.Select(r => r.AwayScore).ToList();
        List<int> totalGoals = results.Select(r => r.HomeScore + r.AwayScore).ToList();

        double avgHomeScore = homeScores.Average();
        double avgAwayScore = awayScores.Average();
        double avgTotalGoals = totalGoals.Average();

        double stdDevHome = CalculateStandardDeviation(homeScores);
        double stdDevAway = CalculateStandardDeviation(awayScores);
        double stdDevTotal = CalculateStandardDeviation(totalGoals);

        int minHome = homeScores.Min();
        int maxHome = homeScores.Max();
        int minAway = awayScores.Min();
        int maxAway = awayScores.Max();
        int minTotal = totalGoals.Min();
        int maxTotal = totalGoals.Max();

        // Aggregate Team Statistics
        double avgHomeShots = results.Average(r => r.HomeStats.ShotsTaken);
        double avgAwayShots = results.Average(r => r.AwayStats.ShotsTaken);
        double avgHomeSOT = results.Average(r => r.HomeStats.ShotsOnTarget);
        double avgAwaySOT = results.Average(r => r.AwayStats.ShotsOnTarget);
        double avgHomeSaves = results.Average(r => r.HomeStats.SavesMade);
        double avgAwaySaves = results.Average(r => r.AwayStats.SavesMade);
        double avgHomeTurnovers = results.Average(r => r.HomeStats.Turnovers);
        double avgAwayTurnovers = results.Average(r => r.AwayStats.Turnovers);
        double avgHomeFouls = results.Average(r => r.HomeStats.FoulsCommitted);
        double avgAwayFouls = results.Average(r => r.AwayStats.FoulsCommitted);
        double avgHomeSuspensions = results.Average(r => r.HomeStats.TwoMinuteSuspensions);
        double avgAwaySuspensions = results.Average(r => r.AwayStats.TwoMinuteSuspensions);

        // Calculate overall percentages using totals
        long totalHomeShots = results.Sum(r => (long)r.HomeStats.ShotsTaken);
        long totalAwayShots = results.Sum(r => (long)r.AwayStats.ShotsTaken);
        long totalHomeGoals = results.Sum(r => (long)r.HomeStats.GoalsScored);
        long totalAwayGoals = results.Sum(r => (long)r.AwayStats.GoalsScored);
        long totalHomeSOT = results.Sum(r => (long)r.HomeStats.ShotsOnTarget);
        long totalAwaySOT = results.Sum(r => (long)r.AwayStats.ShotsOnTarget);
        long totalHomeSaves = results.Sum(r => (long)r.HomeStats.SavesMade);
        long totalAwaySaves = results.Sum(r => (long)r.AwayStats.SavesMade);

        float overallHomeShootPct = totalHomeShots == 0 ? 0f : (float)totalHomeGoals / totalHomeShots * 100f;
        float overallAwayShootPct = totalAwayShots == 0 ? 0f : (float)totalAwayGoals / totalAwayShots * 100f;
        float overallHomeSotPct = totalHomeShots == 0 ? 0f : (float)totalHomeSOT / totalHomeShots * 100f;
        float overallAwaySotPct = totalAwayShots == 0 ? 0f : (float)totalAwaySOT / totalAwayShots * 100f;
        float overallHomeSavePct = totalAwaySOT == 0 ? 0f : (float)totalHomeSaves / totalAwaySOT * 100f; // Home saves vs Away SOT
        float overallAwaySavePct = totalHomeSOT == 0 ? 0f : (float)totalAwaySaves / totalHomeSOT * 100f; // Away saves vs Home SOT

        // Calculate Win/Draw/Loss Percentage
        int homeWins = results.Count(r => r.HomeScore > r.AwayScore);
        int awayWins = results.Count(r => r.AwayScore > r.HomeScore);
        int draws = results.Count(r => r.HomeScore == r.AwayScore);

        double homeWinPct = (double)homeWins / numMatches * 100.0;
        double awayWinPct = (double)awayWins / numMatches * 100.0;
        double drawPct = (double)draws / numMatches * 100.0;

        // --- Log Summary ---
        Debug.Log($"--- Scores ---");
        Debug.Log($"Avg H Score: {avgHomeScore:F2} (StdDev:{stdDevHome:F2}|Min:{minHome}|Max:{maxHome})");
        Debug.Log($"Avg A Score: {avgAwayScore:F2} (StdDev:{stdDevAway:F2}|Min:{minAway}|Max:{maxAway})");
        Debug.Log($"Avg Total Goals: {avgTotalGoals:F2} (StdDev:{stdDevTotal:F2}|Min:{minTotal}|Max:{maxTotal})");
        Debug.Log($"--- Outcomes ---");
        Debug.Log($"H Wins: {homeWins} ({homeWinPct:F1}%) | A Wins: {awayWins} ({awayWinPct:F1}%) | Draws: {draws} ({drawPct:F1}%)");
        Debug.Log($"--- Team Statistics (Averages Per Match) ---");
        Debug.Log($"            |   Home   |   Away   |");
        Debug.Log($"------------|----------|----------|");
        Debug.Log($"Shots       | {avgHomeShots,8:F1} | {avgAwayShots,8:F1} |");
        Debug.Log($"SOT         | {avgHomeSOT,8:F1} | {avgAwaySOT,8:F1} |");
        Debug.Log($"Saves       | {avgHomeSaves,8:F1} | {avgAwaySaves,8:F1} |");
        Debug.Log($"Turnovers   | {avgHomeTurnovers,8:F1} | {avgAwayTurnovers,8:F1} |");
        Debug.Log($"Fouls       | {avgHomeFouls,8:F1} | {avgAwayFouls,8:F1} |");
        Debug.Log($"2min Susp   | {avgHomeSuspensions,8:F1} | {avgAwaySuspensions,8:F1} |");
        Debug.Log($"--- Overall Percentages ---");
        Debug.Log($"Shooting %  | {overallHomeShootPct,8:F1}% | {overallAwayShootPct,8:F1}% |");
        Debug.Log($"SOT %       | {overallHomeSotPct,8:F1}% | {overallAwaySotPct,8:F1}% |");
        Debug.Log($"Save %      | {overallHomeSavePct,8:F1}% | {overallAwaySavePct,8:F1}% |");
        Debug.Log("---------------------------------");

        // --- Realism Check Notes ---
        Debug.Log("--- Realism Check Notes (Approximate Handball Values) ---");
        CheckStatRealism("Avg Total Goals", avgTotalGoals, TYPICAL_MIN_GOALS, TYPICAL_MAX_GOALS);
        CheckStatRealism("Draw %", drawPct, TYPICAL_MIN_DRAW_PCT, TYPICAL_MAX_DRAW_PCT);
        CheckStatRealism("Avg Shots (Team)", (avgHomeShots + avgAwayShots) / 2.0, TYPICAL_MIN_SHOTS_TEAM, TYPICAL_MAX_SHOTS_TEAM);
        CheckStatRealism("Overall Shooting %", (overallHomeShootPct + overallAwayShootPct) / 2.0f, TYPICAL_MIN_SHOOT_PCT, TYPICAL_MAX_SHOOT_PCT);
        CheckStatRealism("Overall Save %", (overallHomeSavePct + overallAwaySavePct) / 2.0f, TYPICAL_MIN_SAVE_PCT, TYPICAL_MAX_SAVE_PCT);
        CheckStatRealism("Avg Turnovers (Team)", (avgHomeTurnovers + avgAwayTurnovers) / 2.0, TYPICAL_MIN_TURNOVERS_TEAM, TYPICAL_MAX_TURNOVERS_TEAM);
        CheckStatRealism("Avg 2min Susp (Team)", (avgHomeSuspensions + avgAwaySuspensions) / 2.0, TYPICAL_MIN_SUSP_TEAM, TYPICAL_MAX_SUSP_TEAM);
        Debug.Log("---------------------------------");

        // --- Add Data Row for Export ---
        if (EXPORT_RESULTS)
        {
            summaryExportData.Add(new string[] {
                 homeAbilityParam.ToString(),
                 awayAbilityParam.ToString(), // Use parameter
                 numMatches.ToString(),
                 $"{homeTacticData?.Name}/{homeTacticData?.Type}", // Safe access
                 $"{awayTacticData?.Name}/{awayTacticData?.Type}", // Safe access
                 avgHomeScore.ToString("F2"), avgAwayScore.ToString("F2"), avgTotalGoals.ToString("F2"), stdDevTotal.ToString("F2"),
                 homeWinPct.ToString("F1"), awayWinPct.ToString("F1"), drawPct.ToString("F1"),
                 avgHomeShots.ToString("F1"), avgAwayShots.ToString("F1"), overallHomeShootPct.ToString("F1"), overallAwayShootPct.ToString("F1"),
                 overallHomeSavePct.ToString("F1"), overallAwaySavePct.ToString("F1"), avgHomeTurnovers.ToString("F1"), avgAwayTurnovers.ToString("F1"),
                 avgHomeSuspensions.ToString("F1"), avgAwaySuspensions.ToString("F1")
             });
        }
    }

    // ValidateResult method for simulation output
    private bool ValidateResult(MatchResult result, int simulationNumber)
    {
        if (result == null)
        {
            Debug.LogWarning($"Simulation #{simulationNumber}: Result is null.");
            return false;
        }
        // Example checks, expand as needed for your domain
        if (result.HomeScore < 0 || result.AwayScore < 0)
        {
            Debug.LogWarning($"Simulation #{simulationNumber}: Invalid score values (Home: {result.HomeScore}, Away: {result.AwayScore}).");
            return false;
        }
        // Add further checks here if needed
        return true;
    }

    // GetCsvHeader method (copied from MatchEngineTester)
    private string[] GetCsvHeader()
    {
        return new string[] {
             "HomeAvgAbility", "AwayAvgAbility", "ValidSims", "HomeTactic", "AwayTactic",
             "AvgHomeScore", "AvgAwayScore", "AvgTotalGoals", "StdDevTotalGoals",
             "HomeWinPct", "AwayWinPct", "DrawPct",
             "AvgHomeShots", "AvgAwayShots", "HomeShootPct", "AwayShootPct",
             "HomeSavePct", "AwaySavePct", "AvgHomeTurnovers", "AvgAwayTurnovers",
             "AvgHomeSuspensions", "AvgAwaySuspensions"
         };
    }

    // ExportSummaryToCsv method (copied from MatchEngineTester)
    private void ExportSummaryToCsv()
    {
        if (summaryExportData == null || summaryExportData.Count <= 1)
        {
            Debug.LogWarning("No summary data available to export.");
            return;
        }
        // Ensure persistentDataPath exists (it usually does, but good practice)
        if (!Directory.Exists(Application.persistentDataPath))
        {
            Directory.CreateDirectory(Application.persistentDataPath);
        }
        string filePath = Path.Combine(Application.persistentDataPath, EXPORT_FILENAME);
        StringBuilder sb = new StringBuilder();
        try
        {
            Debug.Log($"Attempting to export summary results to: {filePath}");
            foreach (var row in summaryExportData) { sb.AppendLine(string.Join(",", row)); }
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Successfully exported {summaryExportData.Count - 1} summary data rows to {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to export results to CSV: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // CheckStatRealism method (copied from MatchEngineTester)
    private void CheckStatRealism(string statName, double value, double typicalMin, double typicalMax)
    {
        string message;
        if (value >= typicalMin && value <= typicalMax) { message = $"OK ({value:F1} is within typical {typicalMin:F1}-{typicalMax:F1} range)"; }
        else if (value < typicalMin) { message = $"WARNING - Potentially LOW ({value:F1} < typical min {typicalMin:F1})"; }
        else { message = $"WARNING - Potentially HIGH ({value:F1} > typical max {typicalMax:F1})"; }
        Debug.Log($"{statName}: {message}");
    }

    // CalculateStandardDeviation method (copied from MatchEngineTester)
    private double CalculateStandardDeviation(List<int> values)
    {
        if (values == null || values.Count < 2) { return 0; }
        double average = values.Average();
        double sumOfSquaresOfDifferences = values.Sum(val => Math.Pow(val - average, 2));
        double variance = sumOfSquaresOfDifferences / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    #endregion
}