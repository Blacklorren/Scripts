using UnityEngine;
using Zenject;
using HandballManager.Simulation.AI.Decision;
using HandballManager.Simulation.Engines;
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Simulation.Factories;
using HandballManager.Simulation.AI;
using HandballManager.Simulation.Services;
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Physics;
using HandballManager.Simulation.Events.Interfaces;
using HandballManager.Simulation.AI.Positioning;
using HandballManager.Simulation.Events.Detectors;
using HandballManager.Simulation.Events.Handlers;
using HandballManager.Simulation.Events.Finalizers;

namespace HandballManager.Simulation.Installers
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

            // Simulation service bundle
            Container.Bind<ISimulationServiceBundle>().To<SimulationServiceBundle>().AsSingle();

            // Geometry provider and physics calculator
            Container.Bind<IGeometryProvider>().To<PitchGeometryProvider>().AsSingle();
            Container.Bind<IBallPhysicsCalculator>().To<DefaultBallPhysicsCalculator>().AsSingle();

            // Movement simulator
            Container.Bind<IMovementSimulator>().To<MovementSimulator>().AsSingle();

            // Phase manager
            Container.Bind<IPhaseManager>().To<DefaultPhaseManager>().AsSingle();

            // Player setup handler
            Container.Bind<IPlayerSetupHandler>().To<DefaultPlayerSetupHandler>().AsSingle();

            // Event detector
            Container.Bind<IEventDetector>().To<DefaultEventDetector>().AsSingle();

            // Match event handler
            Container.Bind<IMatchEventHandler>().To<DefaultMatchEventHandler>().AsSingle();

            // Match finalizer
            Container.Bind<IMatchFinalizer>().To<DefaultMatchFinalizer>().AsSingle();

            // Simulation timer
            Container.Bind<ISimulationTimer>().To<DefaultSimulationTimer>().AsSingle();

            // Tactic positioner
            Container.Bind<ITacticPositioner>().To<TacticPositioner>().AsSingle();

            Debug.Log("[SimulationInstaller] Simulation dependency installation complete.");
        }
    }
}