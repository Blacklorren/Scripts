using System;
using System.Threading;

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Defines a contract for creating instances of the MatchSimulator.
    /// Encapsulates the setup and dependency injection required for MatchSimulator initialization,
    /// ensuring a clean state for each match simulation.
    /// </summary>
    public interface IMatchSimulatorFactory
    {
        /// <summary>
        /// Creates a new instance using individual team and tactic parameters (legacy).
        /// </summary>
        /// <summary>
        /// Creates a new, configured instance using a pre-configured MatchState.
        /// </summary>
        MatchSimulator Create(
            HandballManager.Simulation.Engines.MatchState matchState,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }
}