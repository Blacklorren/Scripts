using HandballManager.Simulation.AI.Decision;
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.AI
{
    /// <summary>
    /// Composite implementation of the player AI service interface.
    /// </summary>
    public class CompositeAIService : IPlayerAIService
    {
        private readonly IOffensiveDecisionMaker _offensiveDecisionMaker;
        private readonly IDefensiveDecisionMaker _defensiveDecisionMaker;

        public CompositeAIService(
            IOffensiveDecisionMaker offensiveDecisionMaker,
            IDefensiveDecisionMaker defensiveDecisionMaker)
        {
            _offensiveDecisionMaker = offensiveDecisionMaker;
            _defensiveDecisionMaker = defensiveDecisionMaker;
        }

        public void ProcessDecisions()
        {
            // Basic implementation example
            var offensiveResult = _offensiveDecisionMaker.MakePassDecision(null);
            var defensiveResult = _defensiveDecisionMaker.MakeTackleDecision(null);

#if UNITY_EDITOR
            UnityEngine.Debug.Log($"Processed AI decisions - Offensive: {offensiveResult.Confidence}, Defensive: {defensiveResult.Confidence}");
#endif
        }
    }
}
