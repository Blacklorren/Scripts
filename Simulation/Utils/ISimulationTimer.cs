using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Events;
using HandballManager.Simulation.Events.Interfaces;

namespace HandballManager.Simulation.Utils // Changed from Interfaces to Utils
{
    public interface ISimulationTimer
    {
        void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler);
    }
}