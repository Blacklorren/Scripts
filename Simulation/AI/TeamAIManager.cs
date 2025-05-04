using HandballManager.Simulation.Engines;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Data;
using HandballManager.Simulation.Events.Interfaces;
using HandballManager.Gameplay;
using HandballManager.Core;
using System;
using System.Linq; // Added for LINQ
using System.Collections.Generic; // Added for List

namespace HandballManager.Simulation.AI
{
    /// <summary>
    /// Handles tactical adaptation for the AI-controlled team during the match.
    /// Evaluates the match situation at regular intervals and adjusts tactics accordingly.
    /// </summary>
    public class TeamAIManager
    {
        private readonly int _aiTeamSimId; // 0 = Home, 1 = Away
        private readonly MatchState _state;
        private readonly IGameStateEvaluator _gameStateEvaluator;
        private readonly IMatchEventHandler _eventHandler;
        private readonly MatchSimulator _matchSimulator;
        private float _lastUpdateTime;
        private const float UPDATE_INTERVAL = 30f; // seconds (simulated)
        private float _lastTimeoutCheckTime = -60f; // Cooldown for timeout checks
        private const float TIMEOUT_CHECK_INTERVAL = 45f; // How often to evaluate timeouts
        private float _lastSubstitutionCheckTime = -60f; // Cooldown for substitution checks
        private const float SUBSTITUTION_CHECK_INTERVAL = 60f; // How often to evaluate subs
        private const float MIN_STAMINA_FOR_SUB = 0.35f; // Stamina threshold to consider subbing OUT
        private const float MIN_STAMINA_DIFF_FOR_SUB = 0.25f; // Bench player needs this much MORE stamina

        public TeamAIManager(MatchState state, int aiTeamSimId, IMatchEventHandler eventHandler, MatchSimulator matchSimulator)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _aiTeamSimId = aiTeamSimId;
            _gameStateEvaluator = state.GameStateEvaluator ?? throw new ArgumentNullException("state.GameStateEvaluator");
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _matchSimulator = matchSimulator ?? throw new ArgumentNullException(nameof(matchSimulator));
            _lastUpdateTime = -UPDATE_INTERVAL; // force update at t=0
        }

        /// <summary>
        /// Call this method regularly (e.g. every simulation tick) to allow tactical adaptation.
        /// </summary>
        public void UpdateAI()
        {
            float now = _state.MatchTimeSeconds;
            if (now - _lastUpdateTime < UPDATE_INTERVAL)
                return;
            _lastUpdateTime = now;

            // Get the tactic instance to modify
            Tactic tactic = (_aiTeamSimId == 0) ? _state.HomeTactic : _state.AwayTactic;
            if (tactic == null) return;

            // --- Existing Tactical Adjustments ---
            bool tacticalAdjustmentMade = PerformTacticalAdjustments(tactic, now);
            if (tacticalAdjustmentMade) return; // Don't consider timeout/subs immediately after tactic change

            // --- Timeout Check ---
            CheckAndTriggerTimeout(now);

            // --- Substitution Check ---
            CheckAndPerformSubstitutions(now);
        }

        private bool PerformTacticalAdjustments(Tactic tactic, float now)
        {
            // --- 1. Losing and little time left ---
            int goalDiff = _state.GetScoreDifference(_aiTeamSimId);
            float timeLeft = _state.GetTimeLeftSeconds();
            if (goalDiff < 0 && timeLeft < 300f) // Losing, last 5 minutes
            {
                tactic.Pace = TacticPace.Fast;
                tactic.RiskTakingLevel = Math.Min(100, tactic.RiskTakingLevel + 20);
                tactic.TeamAggressionLevel = Math.Min(100, tactic.TeamAggressionLevel + 20);
                _eventHandler.LogEvent(_state, $"[AI] Tactical adaptation: Losing (diff {goalDiff}) with {timeLeft:F0}s left. Switching to Fast pace, +risk, +aggression.", _aiTeamSimId);
                return true;
            }

            // --- 2. Leading by a large margin ---
            if (goalDiff > 3 && timeLeft < _state.MatchDurationSeconds * 0.5f) // Leading by 4+, first half
            {
                tactic.Pace = TacticPace.Slow;
                tactic.RiskTakingLevel = Math.Max(0, tactic.RiskTakingLevel - 20);
                _eventHandler.LogEvent(_state, $"[AI] Tactical adaptation: Leading (diff {goalDiff}) early. Slowing down pace, reducing risk.", _aiTeamSimId);
                // Optionally, reduce aggression
                return true;
            }

            // --- 3. Conceding many goals quickly ---
            int goalsConcededLast5Min = _state.GetGoalDifferentialLastMinutes(_aiTeamSimId, now, 5f); // positive = conceded
            if (goalsConcededLast5Min > 2)
            {
                tactic.DefensiveLineHeight = Math.Min(100, tactic.DefensiveLineHeight + 10);
                tactic.TeamAggressionLevel = Math.Min(100, tactic.TeamAggressionLevel + 10);
                _eventHandler.LogEvent(_state, $"[AI] Tactical adaptation: Conceded {goalsConcededLast5Min} in last 5min. Raising defensive line and aggression.", _aiTeamSimId);
                return true;
            }

            return false; // No tactical adjustment made this time
        }

        private void CheckAndTriggerTimeout(float now)
        {
            // Check if enough time passed since last check or if timeout is even possible
            // Updated call to use the correct signature from MatchState
            if (now - _lastTimeoutCheckTime < TIMEOUT_CHECK_INTERVAL || !_state.CanTriggerTimeout(_aiTeamSimId, now, _state.MatchDurationSeconds))
            {
                return;
            }
            _lastTimeoutCheckTime = now;

            // Conditions for calling timeout:

            // 1. Opponent momentum (e.g., conceded 2+ goals recently without scoring)
            // Note: This requires tracking recent scoring events more granularly, which MatchState might not currently do easily.
            // Simplified: Check goal difference change over a short period.
            int goalsConcededLastFewMin = _state.GetGoalDifferentialLastMinutes(_aiTeamSimId, now, 3f * 60f); // Check last 3 min (adjust time as needed)
            bool opponentMomentum = goalsConcededLastFewMin >= 2; // Conceded 2+ more goals than scored in last 3 min

            // 2. Close game, near end of half/match (e.g., last 2 mins, score difference <= 1)
            int currentGoalDiff = _state.GetScoreDifference(_aiTeamSimId);
            bool closeScore = Math.Abs(currentGoalDiff) <= 1;
            
            // Calculate period and time left based on MatchTimeSeconds and MatchDurationSeconds
            float halfDuration = _state.MatchDurationSeconds / 2f;
            bool isFirstHalf = _state.MatchTimeSeconds < halfDuration;
            float timeLeftInPeriodSeconds = isFirstHalf ? halfDuration - _state.MatchTimeSeconds : _state.MatchDurationSeconds - _state.MatchTimeSeconds;

            bool nearEndOfFirstHalf = isFirstHalf && timeLeftInPeriodSeconds < 120f;
            bool nearEndOfMatch = !isFirstHalf && timeLeftInPeriodSeconds < 120f;
            // 3. Breaking opponent momentum (e.g., we scored, close game, near end)
            bool crucialMoment = closeScore && (nearEndOfFirstHalf || nearEndOfMatch);

            // Trigger timeout if conditions met
            if (opponentMomentum || crucialMoment)
            {
                string reason = opponentMomentum ? "opponent momentum" : "crucial moment";
                if (_matchSimulator.TriggerTimeout(_aiTeamSimId))
                {
                    _eventHandler.LogEvent(_state, $"[AI] Timeout called due to {reason}. (Goals conceded last 3min: {goalsConcededLastFewMin}, ScoreDiff: {currentGoalDiff})", _aiTeamSimId);
                    _lastTimeoutCheckTime = now + 120f; // Add extra cooldown after calling one
                }
            }
        }

        private void CheckAndPerformSubstitutions(float now)
        {
            // Check cooldown and if substitutions are allowed in current phase
            if (now - _lastSubstitutionCheckTime < SUBSTITUTION_CHECK_INTERVAL || !IsSubstitutionAllowed(_state))
            {
                return;
            }
            _lastSubstitutionCheckTime = now;

            var teamState = (_aiTeamSimId == 0) ? _state.GetTeamOnCourt(0) : _state.GetTeamOnCourt(1);
            var playersOnCourt = teamState.ToList(); // Copy to avoid modification issues
            var playersOnBench = (_aiTeamSimId == 0) ? _state.HomeBench.ToList() : _state.AwayBench.ToList();

            if (!playersOnCourt.Any() || !playersOnBench.Any()) return; // Nothing to sub

            // Find tired players on court
            var tiredPlayers = playersOnCourt
                .Where(p => p.Stamina < MIN_STAMINA_FOR_SUB)
                .OrderBy(p => p.Stamina) // Prioritize most tired
                .ToList();

            if (!tiredPlayers.Any()) return; // No one is tired enough

            var subsMade = new List<SimPlayer>(); // Track players coming IN to avoid using them twice

            foreach (var playerOut in tiredPlayers)
            {
                // Find best available replacement on bench for the same primary position
                var potentialReplacements = playersOnBench
                    .Where(p => p.BaseData.PrimaryPosition == playerOut.BaseData.PrimaryPosition && // Match position
                                !subsMade.Contains(p) && // Not already used in this check cycle
                                p.Stamina > playerOut.Stamina + MIN_STAMINA_DIFF_FOR_SUB) // Significantly more stamina
                    .OrderByDescending(p => p.Stamina) // Prefer highest stamina replacement
                    .ToList();

                if (potentialReplacements.Any())
                {
                    var playerIn = potentialReplacements.First();

                    if (_matchSimulator.TrySubstitute(playerOut, playerIn))
                    {
                        _eventHandler.LogEvent(_state, $"[AI] Substitution: {playerOut.BaseData.LastName} (St: {playerOut.Stamina:P0}) OUT, {playerIn.BaseData.LastName} (St: {playerIn.Stamina:P0}) IN.", _aiTeamSimId);
                        subsMade.Add(playerIn); // Mark playerIn as used
                        playersOnBench.Remove(playerIn); // Remove from available bench for this cycle
                        // Don't remove playerOut from tiredPlayers list here, just skip if successfully subbed
                        // Potentially limit subs per check? For now, allow multiple if needed.
                    }
                    else
                    {
                        // Log failure? Substitution might fail due to rules (e.g., already subbed recently)
                    }
                }
            }

            if (subsMade.Any())
            {
                _lastSubstitutionCheckTime = now + 30f; // Add extra short cooldown after making subs
            }
        }

        private bool IsSubstitutionAllowed(MatchState state)
        {
            // Allow substitutions only during Timeout or HalfTime phases
            return state.CurrentPhase == GamePhase.Timeout || state.CurrentPhase == GamePhase.HalfTime;
        }
    }
}
