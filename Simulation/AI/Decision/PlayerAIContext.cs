using HandballManager.Gameplay;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Context information provided to AI decision makers.
    /// </summary>
    public class PlayerAIContext
    {
        /// <summary>
        /// Gets or sets the tactic positioner for tactical/screening decisions.
        /// </summary>
        public ITacticPositioner TacticPositioner { get; set; }
        /// <summary>
        /// Gets or sets the current match state.
        /// </summary>
        public MatchState MatchState { get; set; }

        /// <summary>
        /// Gets or sets the player making the decision.
        /// </summary>
        public SimPlayer Player { get; set; }

        /// <summary>
        /// Gets or sets the current tactical setup.
        /// </summary>
        public Tactic Tactics { get; set; }
    }
}
