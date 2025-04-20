using System;
using HandballManager.Data;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Manages passive play detection and warning system in match simulation.
    /// </summary>
    public class PassivePlayManager
    {
        public bool PassivePlayWarningActive { get; private set; } = false;
        public float AttackTimer { get; private set; } = 0f;
        public int? WarningTeamSimId { get; private set; } = null;

        private const float ATTACK_TIME_LIMIT = 25.0f; // 25 seconds for an attack
        private const int PASSES_AFTER_WARNING_LIMIT = 4;

        private int _passesSinceWarning = 0;
        private MatchState _matchState;
        private int _previousPossessionTeamId = -2; // -2 means uninitialized

        public PassivePlayManager(MatchState matchState)
        {
            _matchState = matchState;
            _previousPossessionTeamId = matchState?.PossessionTeamId ?? -2;
        }

        /// <summary>
        /// Call this every frame with deltaTime. Should also be called on possession change.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_matchState == null) return;
            int currentPossession = _matchState.PossessionTeamId;

            // Detect change of possession
            if (currentPossession != _previousPossessionTeamId)
            {
                ResetAll();
                _previousPossessionTeamId = currentPossession;
                return;
            }

            if (!PassivePlayWarningActive)
            {
                AttackTimer += deltaTime;
                if (AttackTimer >= ATTACK_TIME_LIMIT)
                {
                    TriggerPassivePlayWarning(_matchState.PossessionTeamId);
                }
            }
            // No timer after warning, only passes
        }

        /// <summary>
        /// Call this when a pass is made by the team in possession.
        /// </summary>
        public void OnPassMade(int teamSimId)
        {
            if (!PassivePlayWarningActive || WarningTeamSimId != teamSimId) return;
            _passesSinceWarning++;
            if (_passesSinceWarning > PASSES_AFTER_WARNING_LIMIT)
            {
                TriggerPassivePlayViolation();
            }
        }

        /// <summary>
        /// Call this when a defensive sanction (yellow card or 2 min) is given.
        /// </summary>
        public void ResetAttackTimer()
        {
            AttackTimer = 0f;
            if (PassivePlayWarningActive)
                ResetAll();
        }

        private void ResetAll()
        {
            PassivePlayWarningActive = false;
            AttackTimer = 0f;
            WarningTeamSimId = null;
            _passesSinceWarning = 0;
        }

        private void TriggerPassivePlayWarning(int teamSimId)
        {
            PassivePlayWarningActive = true;
            _passesSinceWarning = 0;
            WarningTeamSimId = teamSimId;
            Debug.Log($"[PassivePlay] Warning triggered for team {teamSimId}");
        }

        private void TriggerPassivePlayViolation()
        {
            Debug.Log($"[PassivePlay] Violation! Turnover for team {WarningTeamSimId}");
            ResetAll();
            // Implement turnover logic in MatchSimulator or event handler
        }

        public void ResetPassivePlay()
        {
            ResetAll();
        }
    }
}
