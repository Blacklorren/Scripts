using System;
using HandballManager.Data;
using HandballManager.Simulation.Engines;

namespace HandballManager.Simulation.Events.Interfaces
{
    public interface IMatchFinalizer
    {
        void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler);

        /// <summary>
        /// Finalizes the match result based on the match state and date.
        /// </summary>
        /// <param name="state">The current match state, can be null if initialization failed.</param>
        /// <param name="matchDate">The date when the match occurred.</param>
        /// <returns>The finalized match result with scores and statistics.</returns>
        MatchResult FinalizeResult(MatchState state, DateTime matchDate);
    }
}