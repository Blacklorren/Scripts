using HandballManager.Simulation.Engines;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Data;
using HandballManager.Simulation.Events.Interfaces;
using HandballManager.Gameplay;
using HandballManager.Core;
using System;

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
        private float _lastUpdateTime;
        private const float UPDATE_INTERVAL = 30f; // seconds (simulated)

        public TeamAIManager(MatchState state, int aiTeamSimId, IMatchEventHandler eventHandler)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _aiTeamSimId = aiTeamSimId;
            _gameStateEvaluator = state.GameStateEvaluator ?? throw new ArgumentNullException("state.GameStateEvaluator");
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
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

            // --- 1. Losing and little time left ---
            int goalDiff = _state.GetScoreDifference(_aiTeamSimId);
            float timeLeft = _state.GetTimeLeftSeconds();
            if (goalDiff < 0 && timeLeft < 300f) // Losing, last 5 minutes
            {
                tactic.Pace = TacticPace.Fast;
                tactic.RiskTakingLevel = Math.Min(100, tactic.RiskTakingLevel + 20);
                tactic.TeamAggressionLevel = Math.Min(100, tactic.TeamAggressionLevel + 20);
                _eventHandler.LogEvent(_state, $"[AI] Tactical adaptation: Losing (diff {goalDiff}) with {timeLeft:F0}s left. Switching to Fast pace, +risk, +aggression.", _aiTeamSimId);
                return;
            }

            // --- 2. Leading by a large margin ---
            if (goalDiff > 3 && timeLeft < _state.MatchDurationSeconds * 0.5f) // Leading by 4+, first half
            {
                tactic.Pace = TacticPace.Slow;
                tactic.RiskTakingLevel = Math.Max(0, tactic.RiskTakingLevel - 20);
                _eventHandler.LogEvent(_state, $"[AI] Tactical adaptation: Leading (diff {goalDiff}) early. Slowing down pace, reducing risk.", _aiTeamSimId);
                // Optionally, reduce aggression
                return;
            }

            // --- 3. Conceding many goals quickly ---
            int goalsConcededLast5Min = _state.GetGoalDifferentialLastMinutes(_aiTeamSimId, _state.MatchTimeSeconds, 5f); // positive = conceded
            if (goalsConcededLast5Min > 2)
            {
                tactic.DefensiveLineHeight = Math.Min(100, tactic.DefensiveLineHeight + 10);
                tactic.TeamAggressionLevel = Math.Min(100, tactic.TeamAggressionLevel + 10);
                _eventHandler.LogEvent(_state, $"[AI] Tactical adaptation: Conceded {goalsConcededLast5Min} in last 5min. Raising defensive line and aggression.", _aiTeamSimId);
                return;
            }
        }
    }
}
