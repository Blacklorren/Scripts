using HandballManager.Simulation.AI.Decision;
using HandballManager.Simulation.AI.Evaluation;
using HandballManager.Simulation.AI.Positioning;
using System;

namespace HandballManager.Simulation.AI
{
    /// <summary>
    /// Implementation of the IAIServiceBundle interface that aggregates all AI-related services.
    /// This class follows the Facade pattern to simplify access to multiple AI components.
    /// </summary>
    public class AIServiceBundle : IAIServiceBundle
    {
        /// <summary>Gets the player AI controller.</summary>
        public IPlayerAIController PlayerController { get; }
        
        /// <summary>Gets the personality evaluator.</summary>
        public IPersonalityEvaluator PersonalityEvaluator { get; }
        
        /// <summary>Gets the tactical evaluator.</summary>
        public ITacticalEvaluator TacticalEvaluator { get; }
        
        /// <summary>Gets the game state evaluator.</summary>
        public IGameStateEvaluator GameStateEvaluator { get; }
        
        /// <summary>Gets the passing decision maker.</summary>
        public IPassingDecisionMaker PassingDecisionMaker { get; }
        
        /// <summary>Gets the shooting decision maker.</summary>
        public IShootingDecisionMaker ShootingDecisionMaker { get; }
        
        /// <summary>Gets the dribbling decision maker.</summary>
        public IDribblingDecisionMaker DribblingDecisionMaker { get; }
        
        /// <summary>Gets the defensive decision maker.</summary>
        public IDefensiveDecisionMaker DefensiveDecisionMaker { get; }
        
        /// <summary>Gets the goalkeeper positioner.</summary>
        public IGoalkeeperPositioner GoalkeeperPositioner { get; }

        /// <summary>
        /// Initializes a new instance of the AIServiceBundle with all required AI components.
        /// If any evaluator is not provided, uses the default attribute-driven implementation.
        /// </summary>
        public AIServiceBundle(
            IPlayerAIController playerController,
            IPersonalityEvaluator personalityEvaluator = null,
            ITacticalEvaluator tacticalEvaluator = null,
            IGameStateEvaluator gameStateEvaluator = null,
            IPassingDecisionMaker passingDecisionMaker = null,
            IShootingDecisionMaker shootingDecisionMaker = null,
            IDribblingDecisionMaker dribblingDecisionMaker = null,
            IDefensiveDecisionMaker defensiveDecisionMaker = null,
            IGoalkeeperPositioner goalkeeperPositioner = null)
        {
            PlayerController = playerController ?? throw new ArgumentNullException(nameof(playerController));
            // Use default attribute-driven evaluators if not provided
            PersonalityEvaluator = personalityEvaluator ?? new PersonalityEvaluator();
            TacticalEvaluator = tacticalEvaluator ?? new TacticalEvaluator();
            GameStateEvaluator = gameStateEvaluator ?? new GameStateEvaluator();
            PassingDecisionMaker = passingDecisionMaker ?? throw new ArgumentNullException(nameof(passingDecisionMaker));
            ShootingDecisionMaker = shootingDecisionMaker ?? throw new ArgumentNullException(nameof(shootingDecisionMaker));
            DribblingDecisionMaker = dribblingDecisionMaker ?? throw new ArgumentNullException(nameof(dribblingDecisionMaker));
            DefensiveDecisionMaker = defensiveDecisionMaker ?? throw new ArgumentNullException(nameof(defensiveDecisionMaker));
            GoalkeeperPositioner = goalkeeperPositioner ?? throw new ArgumentNullException(nameof(goalkeeperPositioner));
        }
    }
}