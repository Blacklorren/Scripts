using System;
using HandballManager.Data;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Status for passive play checks.
    /// </summary>
    public enum PassivePlayStatus
    {
        Okay,
        WarningTriggered,
        ViolationTriggered
    }

    /// <summary>
    /// Manages passive play detection and warning system in match simulation.
    /// </summary>
    public class PassivePlayManager
    {
        public bool PassivePlayWarningActive { get; private set; } = false;
        /// <summary>
        /// Timer for intent-based passive play (resets on attacking intent)
        /// </summary>
        public float AttackIntentTimer { get; private set; } = 0f;
        /// <summary>
        /// Timer for absolute attack duration (never resets except on possession change)
        /// </summary>
        public float AbsoluteAttackTimer { get; private set; } = 0f;
        public int? WarningTeamSimId { get; private set; } = null;

        private const float ATTACK_INTENT_TIME_LIMIT = 25.0f; // 25 seconds for intent-based passive play
        private const float ABSOLUTE_ATTACK_TIME_LIMIT = 40.0f; // 40 seconds absolute attack limit
        private const int PASSES_AFTER_WARNING_LIMIT = 4;

        private int _passesSinceWarning = 0;
        /// <summary>
        /// Number of passes made since the passive play warning was issued.
        /// </summary>
        public int PassesSinceWarning => _passesSinceWarning;
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
        public PassivePlayStatus Update(float deltaTime)
        {
            if (_matchState == null) return PassivePlayStatus.Okay;
            int currentPossession = _previousPossessionTeamId;

            // Detect change of possession
            if (_matchState.PossessionTeamId != _previousPossessionTeamId)
            {
                ResetAll();
                _previousPossessionTeamId = _matchState.PossessionTeamId;
                return PassivePlayStatus.Okay;
            }

            if (!PassivePlayWarningActive)
            {
                AttackIntentTimer += deltaTime;
                AbsoluteAttackTimer += deltaTime;
                // Trigger warning if either timer exceeds its threshold
                if (AttackIntentTimer >= ATTACK_INTENT_TIME_LIMIT || AbsoluteAttackTimer >= ABSOLUTE_ATTACK_TIME_LIMIT)
                {
                    TriggerPassivePlayWarning(_matchState.PossessionTeamId);
                    return PassivePlayStatus.WarningTriggered;
                }
            }
            // No timer after warning, only passes
            return PassivePlayStatus.Okay;
        }

        /// <summary>
        /// Call this when a pass is made by the team in possession.
        /// </summary>
        public PassivePlayStatus OnPassMade(int teamSimId)
        {
            if (!PassivePlayWarningActive || WarningTeamSimId != teamSimId) return PassivePlayStatus.Okay;
            _passesSinceWarning++;
            if (_passesSinceWarning > PASSES_AFTER_WARNING_LIMIT)
            {
                TriggerPassivePlayViolation();
                return PassivePlayStatus.ViolationTriggered;
            }
            return PassivePlayStatus.Okay;
        }

        /// <summary>
        /// Call this when a defensive sanction (yellow card or 2 min) is given.
        /// </summary>
        public void ResetAttackTimer()
        {
            AttackIntentTimer = 0f;
            if (PassivePlayWarningActive)
                ResetAll();
        }

        private void ResetAll()
        {
            PassivePlayWarningActive = false;
            AttackIntentTimer = 0f;
            AbsoluteAttackTimer = 0f;
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
            // Violation consequence will be handled by event handler. Only reset state here.
            ResetAll();
            // Implement turnover logic in MatchSimulator or event handler
        }

        public void ResetPassivePlay()
        {
            ResetAll();
        }
        /// <summary>
        /// Call this when attacking intent is clearly demonstrated (e.g., movement towards goal).
        /// Resets the attack timer and warning if active.
        /// </summary>
        /// <summary>
        /// Call this when attacking intent is clearly demonstrated (e.g., movement towards goal).
        /// Resets only the intent-based timer (not the absolute timer).
        /// </summary>
        public void NotifyAttackingIntent()
        {
            AttackIntentTimer = 0f;
            if (PassivePlayWarningActive)
                ResetAll();
        }
    }
}
