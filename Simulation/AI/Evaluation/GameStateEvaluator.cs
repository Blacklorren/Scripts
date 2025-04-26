using HandballManager.Simulation.Engines;
using HandballManager.Data;

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
            // Players with high anticipation, composure, determination are more likely to recognize counter chances
            float anticipation = player?.BaseData?.Anticipation ?? 50f;
            float composure = player?.BaseData?.Composure ?? 50f;
            float determination = player?.BaseData?.Determination ?? 50f;
            float recognition = (anticipation + composure + determination) / 300f;
            // If recognition > 0.55, recognize counter-attack
            return recognition > 0.55f;
        }
    }
}
