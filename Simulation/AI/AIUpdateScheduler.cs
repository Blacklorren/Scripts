using System;
using System.Collections.Generic;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.AI
{
    /// <summary>
    /// Schedules AI updates for players based on Level of Detail (LOD) priorities.
    /// </summary>
    public class AIUpdateScheduler
    {
        // --- Update Frequencies (seconds, tuned for human-like decision rates) ---
        public const float HIGH_PRIORITY_UPDATE_RATE = 0.33f; // ~3 Hz (every 0.33s)
        public const float MEDIUM_PRIORITY_UPDATE_RATE = 0.5f; // 2 Hz (every 0.5s)
        public const float LOW_PRIORITY_UPDATE_RATE = 1.0f; // 1 Hz (every 1s)

        // --- Priority Radii (meters) ---
        public const float HIGH_PRIORITY_RADIUS = 4.0f; // e.g. 4m for high-priority
        public const float MEDIUM_PRIORITY_RADIUS = 9.0f; // e.g. 9m for medium-priority

        private readonly Dictionary<int, float> _nextUpdateTimes = new Dictionary<int, float>();

        public enum AIUpdatePriority { Low, Medium, High }

        public bool ShouldUpdatePlayer(SimPlayer player, MatchState state, float currentTime)
        {
            if (!_nextUpdateTimes.ContainsKey(player.GetPlayerId()))
            {
                _nextUpdateTimes[player.GetPlayerId()] = currentTime;
                return true;
            }
            return currentTime >= _nextUpdateTimes[player.GetPlayerId()];
        }

        public void ScheduleNextUpdate(SimPlayer player, MatchState state, float currentTime)
        {
            float rate = GetPlayerUpdateRate(player, state);
            _nextUpdateTimes[player.GetPlayerId()] = currentTime + rate;
        }

        public float GetPlayerUpdateRate(SimPlayer player, MatchState state)
        {
            var priority = CalculatePlayerPriority(player, state);
            switch (priority)
            {
                case AIUpdatePriority.High:
                    return HIGH_PRIORITY_UPDATE_RATE;
                case AIUpdatePriority.Medium:
                    return MEDIUM_PRIORITY_UPDATE_RATE;
                case AIUpdatePriority.Low:
                default:
                    return LOW_PRIORITY_UPDATE_RATE;
            }
        }

        public AIUpdatePriority CalculatePlayerPriority(SimPlayer player, MatchState state)
        {
            // 1. Ball carrier
            if (player.HasBall)
                return AIUpdatePriority.High;

            // 2. Distance to ball
            Vector2 ballPos = state.Ball.Position;
            float dist = Vector2.Distance(player.Position, ballPos);
            if (dist < HIGH_PRIORITY_RADIUS)
                return AIUpdatePriority.High;
            if (dist < MEDIUM_PRIORITY_RADIUS)
                return AIUpdatePriority.Medium;

            // 3. In scoring position (example: inside 9m arc and attacking)
            if (IsInScoringPosition(player, state))
                return AIUpdatePriority.High;

            return AIUpdatePriority.Low;
        }

        private bool IsInScoringPosition(SimPlayer player, MatchState state)
        {
            // Example: inside 9m arc and on attacking team
            // You may want to refine this logic based on your rules
            float goalLineX = (player.TeamSimId == 0) ? ActionResolverConstants.PITCH_LENGTH : 0f;
            float distToGoal = Mathf.Abs(player.Position.x - goalLineX);
            return distToGoal < 9.0f;
        }
    }
}
