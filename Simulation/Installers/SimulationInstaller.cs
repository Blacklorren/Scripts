using UnityEngine;
using Zenject;
using System;
using HandballManager.Simulation.AI.Decision;
using HandballManager.Simulation.Engines;
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Simulation.AI.Evaluation;
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

            // Bind PassivePlayManager by getting it from the active simulator via the coordinator
            // This assumes IMatchSimulationCoordinator provides access to the current IMatchSimulator instance
            // and that instance is the concrete MatchSimulator type holding PassivePlayManager.
            Container.Bind<PassivePlayManager>().FromMethod(ctx => {
                var coordinator = ctx.Container.Resolve<IMatchSimulationCoordinator>();
                // IMPORTANT: Assumes GetCurrentSimulator() or similar exists and returns the active MatchSimulator
                // We might need to adjust this based on the actual IMatchSimulationCoordinator interface.
                // For now, let's assume it returns the concrete type or can be cast.
                var simulator = coordinator.CurrentSimulator; // Now using the interface property
                if (simulator is MatchSimulator concreteSimulator)
                {
                    return concreteSimulator.PassivePlayManager;
                }
                Debug.LogError("[SimulationInstaller] Could not resolve PassivePlayManager. Active MatchSimulator not found or not of expected type in coordinator.");
                // Return null or throw? Returning null might hide issues later.
                // Throwing might be better during development.
                throw new ZenjectException("Failed to resolve PassivePlayManager from MatchSimulationCoordinator.");
            }).AsSingle(); // Assuming one PassivePlayManager per simulation coordinator lifecycle

            // AI services
            // Ensure evaluators are bound
            Container.Bind<ITacticalEvaluator>().To<TacticalEvaluator>().AsSingle();
            Container.Bind<IPersonalityEvaluator>().To<PersonalityEvaluator>().AsSingle();
            Container.Bind<IGameStateEvaluator>().To<GameStateEvaluator>().AsSingle();
            // Explicitly inject evaluators into DefaultOffensiveDecisionMaker
            Container.Bind<IOffensiveDecisionMaker>().FromMethod(ctx =>
                new DefaultOffensiveDecisionMaker(
                    ctx.Container.Resolve<ITacticalEvaluator>(),
                    ctx.Container.Resolve<IPersonalityEvaluator>(),
                    ctx.Container.Resolve<IGameStateEvaluator>()
                )).AsSingle();
            Container.Bind<IDefensiveDecisionMaker>().FromMethod(ctx =>
                new DefaultDefensiveDecisionMaker(
                    ctx.Container.Resolve<ITacticalEvaluator>(),
                    ctx.Container.Resolve<IPersonalityEvaluator>(),
                    ctx.Container.Resolve<IGameStateEvaluator>()
                )).AsSingle();
            // Bind the new role-specific AI controllers
            Container.Bind<IOffensiveAIController>().To<OffensiveAIController>().AsSingle();
            Container.Bind<IDefensiveAIController>().To<DefensiveAIController>().AsSingle();
            Container.Bind<IGoalkeeperAIController>().To<GoalkeeperAIController>().AsSingle();
            // Bind the main PlayerAIController, injecting the role-specific controllers
            Container.Bind<IPlayerAIController>().To<PlayerAIController>().AsSingle();
            // Container.Bind<IPlayerAIService>().To<CompositeAIService>().AsSingle(); // Keep or remove depending on CompositeAIService usage

            // Tactic provider (uses runtime values)
            Container.Bind<ITacticProvider>().FromMethod(_ => new DefaultTacticProvider(userTeam, userTactic, aiTactic)).AsSingle();

            // Simulation service bundle
            Container.Bind<ISimulationServiceBundle>().To<SimulationServiceBundle>().AsSingle();

            // Geometry provider and physics calculator
            Container.Bind<IGeometryProvider>().To<PitchGeometryProvider>().AsSingle();
            Container.Bind<IBallPhysicsCalculator>().To<DefaultBallPhysicsCalculator>().AsSingle();

            // Movement simulator
            Container.Bind<IMovementSimulator>().To<MovementSimulator>().AsSingle().OnInstantiated((ctx, instance) => {
                // Récupérer le MovementSimulator concret
                if (instance is MovementSimulator movementSimulator)
                {
                    // Créer un delegate pour le PassivePlayManager
                    // Cela sera appelé lorsqu'une intention d'attaque est détectée
                    Action attackingIntentHandler = () => {
                        // Obtenir le PassivePlayManager actuel via le MatchSimulator
                        var coordinator = ctx.Container.Resolve<IMatchSimulationCoordinator>();
                        if (coordinator.CurrentSimulator is MatchSimulator matchSimulator && 
                            matchSimulator.PassivePlayManager != null)
                        {
                            matchSimulator.PassivePlayManager.NotifyAttackingIntent();
                        }
                    };
                    
                    // Réinstancier le PlayerPhysicsEngine avec le nouveau delegate
                    // La méthode UpdatePlayerPhysicsEngine a été ajoutée à MovementSimulator
                    movementSimulator.UpdatePlayerPhysicsEngine(attackingIntentHandler);
                    
                    // Alternativement, si MovementSimulator est déjà configuré pour utiliser le delegate,
                    // nous n'avons pas besoin de faire quoi que ce soit ici
                }
            });

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