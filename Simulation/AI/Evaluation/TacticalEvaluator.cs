using HandballManager.Gameplay;
using HandballManager.Core;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.AI.Evaluation
{
    /// <summary>
    /// Evaluates tactical influences on AI decision-making, factoring in player tactical attributes.
    /// </summary>
    public class TacticalEvaluator : ITacticalEvaluator
    {
        // Team tactical instructions are modulated by player TacticalAwareness, Teamwork, Leadership
        public float GetRiskModifier(Tactic tactic)
        {
            // Use tactic.RiskTakingLevel (0-100), higher = riskier
            float baseRisk = tactic?.RiskTakingLevel ?? 50f;
            // Default: 1.0 for neutral, >1 for high risk, <1 for low risk
            return Mathf.Lerp(0.85f, 1.15f, baseRisk / 100f);
        }

        public float GetPaceModifier(Tactic tactic)
        {
            // Convert TacticPace enum to a float for lerp (0=Slow, 50=Normal, 100=Fast)
            float basePace = TacticPaceToFloat(tactic?.Pace ?? TacticPace.Normal);
            return Mathf.Lerp(0.85f, 1.15f, basePace / 100f);
        }

        private float TacticPaceToFloat(TacticPace pace)
        {
            switch (pace)
            {
                case TacticPace.Slow: return 0f;
                case TacticPace.Normal: return 50f;
                case TacticPace.Fast: return 100f;
                default: return 50f;
            }
        }

        public bool DoesActionMatchFocus(SimPlayer player, SimPlayer potentialTarget, PlayerAction actionType, OffensiveFocusPlay focus)
        {
            // Players with higher TacticalAwareness and Teamwork are more likely to follow focus
            float tacticalAwareness = player?.BaseData?.TacticalAwareness ?? 50f;
            float teamwork = player?.BaseData?.Teamwork ?? 50f;
            float leadership = player?.BaseData?.Leadership ?? 50f;
            float focusFollowChance = (tacticalAwareness + teamwork + leadership) / 300f;
            // For demo: if focusFollowChance > 0.5, strictly follow focus, else allow flexibility
            if (focusFollowChance > 0.5f)
            {
                // Strictly follow focus
                // (actual focus logic would go here)
                return true;
            }
            else
            {
                // More flexible, may improvise
                return false;
            }
        }
    }
}
