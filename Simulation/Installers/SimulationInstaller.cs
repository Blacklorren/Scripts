using UnityEngine;
using Zenject;
using HandballManager.Simulation.AI.Decision;
using HandballManager.Simulation.Engines;
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Simulation.Factories;
using HandballManager.Simulation.AI;

namespace HandballManager.Installers
{
    /// <summary>
    /// Installs simulation-related dependencies for the game.
    /// </summary>
    public class SimulationInstaller : MonoInstaller
    {
        // Example: These would be set from your game setup code/UI
        public TeamData userTeam;
        public Tactic userTactic;
        public Tactic aiTactic;

        public override void InstallBindings()
        {
            // Core simulation services
            Container.Bind<IMatchEngine>().To<MatchEngine>().AsSingle();
            Container.Bind<IMatchSimulatorFactory>().To<MatchSimulatorFactory>().AsSingle();
            Container.Bind<IMatchSimulationCoordinator>().To<MatchSimulationCoordinator>().AsSingle();

            // AI services
            Container.Bind<IOffensiveDecisionMaker>().To<DefaultOffensiveDecisionMaker>().AsSingle();
            Container.Bind<IDefensiveDecisionMaker>().To<DefaultDefensiveDecisionMaker>().AsSingle();
            Container.Bind<IPlayerAIService>().To<CompositeAIService>().AsSingle();

            // Tactic provider (uses runtime values)
            Container.Bind<ITacticProvider>().FromMethod(_ => new DefaultTacticProvider(userTeam, userTactic, aiTactic)).AsSingle();

            Debug.Log("[SimulationInstaller] Simulation dependency installation complete.");
        }
    }
}