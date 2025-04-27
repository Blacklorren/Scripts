using HandballManager.Simulation.Engines;
using HandballManager.Data;
using System.Linq;
using UnityEngine;

namespace HandballManager.Simulation.AI.Evaluation
{
    /// <summary>
    /// Evaluates game state influences on AI decision-making, modulated by player mental attributes.
    /// </summary>
    public class GameStateEvaluator : IGameStateEvaluator
    {
        // Modulate risk/aggression modifiers by player composure, determination, leadership, anticipation
        public float GetAttackRiskModifier(MatchState state, int playerTeamId)
        {
            // Example: trailing late in game increases risk
            float baseRisk = 1.0f;
            if (state != null)
            {
                int goalDiff = state.GetScoreDifference(playerTeamId);
                float timeLeft = state.GetTimeLeftSeconds();
                if (goalDiff < 0 && timeLeft < 300f) // Losing, last 5 minutes
                    baseRisk = 1.15f;
                else if (goalDiff > 0 && timeLeft < 180f) // Winning, last 3 minutes
                    baseRisk = 0.9f;
            }
            return baseRisk;
        }

        public float GetDefensiveAggressionModifier(MatchState state, int playerTeamId)
        {
            // Example: trailing increases aggression, winning decreases
            float baseAggression = 1.0f;
            if (state != null)
            {
                int goalDiff = state.GetScoreDifference(playerTeamId);
                if (goalDiff < 0)
                    baseAggression = 1.1f;
                else if (goalDiff > 0)
                    baseAggression = 0.9f;
            }
            return baseAggression;
        }

        public float GetGoalkeeperPassSafetyModifier(MatchState state, int playerTeamId)
        {
            // Example: winning = more safety, losing = more risk
            float modifier = 1.0f;
            if (state != null)
            {
                int goalDiff = state.GetScoreDifference(playerTeamId);
                if (goalDiff > 0)
                    modifier = 1.1f;
                else if (goalDiff < 0)
                    modifier = 0.9f;
            }
            return modifier;
        }

        public bool IsCounterAttackOpportunity(SimPlayer player, MatchState state)
        {
            // 1. Recognition factor (as before)
            float anticipation = player?.BaseData?.Anticipation ?? 50f;
            float composure = player?.BaseData?.Composure ?? 50f;
            float determination = player?.BaseData?.Determination ?? 50f;
            float recognition = (anticipation + composure + determination) / 300f;

            // 2. Ball recovery: possession just changed to player's team in their own half
            bool hasBall = state.Ball.Holder == player;
            bool ownHalf = (player.TeamSimId == 0) ? player.Position.y < 20f : player.Position.y > 20f;
            // Proxy for 'just recovered': team was not last to touch ball
            bool justRecovered = state.Ball.LastTouchedByTeamId != player.TeamSimId && hasBall;

            // 3. Count number of opponents between player and opponent goal
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            int defendersAhead = 0;
            if (opponents != null)
            {
                defendersAhead = opponents.Where(opp =>
                    (player.TeamSimId == 0) ? opp.Position.y > player.Position.y : opp.Position.y < player.Position.y
                ).Count();
            }

            // 4. Open space check: no opponent within 5m in a 30-degree cone towards goal
            Vector2 goalPos = (player.TeamSimId == 0) ? new Vector2(20f, 40f) : new Vector2(20f, 0f);
            Vector2 toGoal = (goalPos - player.Position).normalized;
            bool openLane = true;
            if (opponents != null)
            {
                openLane = !opponents.Any(opp =>
                    Vector2.Dot((opp.Position - player.Position).normalized, toGoal) > 0.85f && // ~30 deg cone
                    Vector2.Distance(opp.Position, player.Position) < 5f
                );
            }

            // Combine: must recognize, have ball, just recovered in own half, few defenders ahead, and open lane
            return recognition > 0.55f && hasBall && ownHalf && justRecovered && defendersAhead <= 2 && openLane;
        }
    }
}
