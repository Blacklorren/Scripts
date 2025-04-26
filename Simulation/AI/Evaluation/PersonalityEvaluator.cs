using HandballManager.Data;
using UnityEngine;

namespace HandballManager.Simulation.AI.Evaluation
{
    /// <summary>
    /// Evaluates personality trait influences on AI decision-making using all relevant mental attributes.
    /// </summary>
    public class PersonalityEvaluator : IPersonalityEvaluator
    {
        // Ambition, Determination, Aggression, Volatility, Professionalism, Loyalty, Composure, Leadership, Teamwork
        public float GetRiskModifier(PlayerData playerData)
        {
            // Ambitious, volatile, aggressive, determined players take more risks; professional/loyal take fewer
            float ambition = playerData?.Ambition ?? 50f;
            float volatility = playerData?.Volatility ?? 50f;
            float aggression = playerData?.Aggression ?? 50f;
            float determination = playerData?.Determination ?? 50f;
            float professionalism = playerData?.Professionalism ?? 50f;
            float loyalty = playerData?.Loyalty ?? 50f;
            float risk = (ambition + volatility + aggression + determination) / 4f;
            float caution = (professionalism + loyalty) / 2f;
            // Risk increases modifier, caution decreases
            return Mathf.Lerp(0.85f, 1.15f, (risk - caution + 100f) / 200f);
        }

        public float GetShootingTendencyModifier(PlayerData playerData)
        {
            // Determined, ambitious, aggressive, composed players shoot more
            float determination = playerData?.Determination ?? 50f;
            float ambition = playerData?.Ambition ?? 50f;
            float aggression = playerData?.Aggression ?? 50f;
            float composure = playerData?.Composure ?? 50f;
            float shooting = (determination + ambition + aggression + composure) / 4f;
            return Mathf.Lerp(0.85f, 1.15f, shooting / 100f);
        }

        public float GetPassingTendencyModifier(PlayerData playerData)
        {
            // Teamwork, loyalty, professionalism, leadership increase passing tendency
            float teamwork = playerData?.Teamwork ?? 50f;
            float loyalty = playerData?.Loyalty ?? 50f;
            float professionalism = playerData?.Professionalism ?? 50f;
            float leadership = playerData?.Leadership ?? 50f;
            float passing = (teamwork + loyalty + professionalism + leadership) / 4f;
            return Mathf.Lerp(0.85f, 1.15f, passing / 100f);
        }

        public float GetDribblingTendencyModifier(PlayerData playerData)
        {
            // Volatility, aggression, ambition, determination increase dribbling
            float volatility = playerData?.Volatility ?? 50f;
            float aggression = playerData?.Aggression ?? 50f;
            float ambition = playerData?.Ambition ?? 50f;
            float determination = playerData?.Determination ?? 50f;
            float dribbling = (volatility + aggression + ambition + determination) / 4f;
            return Mathf.Lerp(0.85f, 1.15f, dribbling / 100f);
        }

        public float GetTacklingTendencyModifier(PlayerData playerData)
        {
            // Aggression, determination, bravery, volatility increase tackling; professionalism decreases
            float aggression = playerData?.Aggression ?? 50f;
            float determination = playerData?.Determination ?? 50f;
            float bravery = playerData?.Bravery ?? 50f;
            float volatility = playerData?.Volatility ?? 50f;
            float professionalism = playerData?.Professionalism ?? 50f;
            float tackling = (aggression + determination + bravery + volatility) / 4f;
            return Mathf.Lerp(0.85f, 1.15f, (tackling - professionalism + 100f) / 200f);
        }

        public float GetHesitationModifier(PlayerData playerData)
        {
            // Determination, composure, leadership reduce hesitation; volatility increases
            float determination = playerData?.Determination ?? 50f;
            float composure = playerData?.Composure ?? 50f;
            float leadership = playerData?.Leadership ?? 50f;
            float volatility = playerData?.Volatility ?? 50f;
            float hesitation = (volatility - determination - composure - leadership + 200f) / 400f;
            return Mathf.Lerp(0.8f, 1.2f, hesitation); // Lower is less hesitation
        }

        // Optionally, add work rate effects on positioning
        // public float GetWorkRatePositioningFactor(PlayerData playerData) { ... }
        // public Vector2 AdjustPositionForWorkRate(PlayerData playerData, Vector2 currentPosition, Vector2 targetPosition) { ... }
    }
}
