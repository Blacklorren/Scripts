using UnityEngine;
using HandballManager.Data;
using HandballManager.Simulation.Core.Events;
using HandballManager.Simulation.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using HandballManager.Gameplay;
using System;

namespace HandballManager.Simulation.Core
{
    /// <summary>
    /// Coordinates the match simulation process using a component-based approach.
    /// Uses dependency injection to get required services.
    /// </summary>
    public class MatchSimulationCoordinator : MonoBehaviour, IMatchSimulationCoordinator
    {
        // Injected dependencies
        private IMatchEngine _engine;
        private IPlayerAIService _aiService;
        private IEventBus _eventBus;
        private IMatchSimulatorFactory _simulatorFactory;
        private ITacticProvider _tacticProvider; // New dependency for tactics

        // Zenject will inject dependencies via this method
        [Zenject.Inject]
        public void Construct(IMatchSimulatorFactory simulatorFactory, IPlayerAIService aiService, IEventBus eventBus, ITacticProvider tacticProvider)
        {
            _simulatorFactory = simulatorFactory;
            _aiService = aiService;
            _eventBus = eventBus;
            _tacticProvider = tacticProvider;
        }

        /// <summary>
        /// Advances the simulation by one action (AI decision and engine step).
        /// </summary>

        // Explicit interface implementation to satisfy IMatchSimulationCoordinator
        void IMatchSimulationCoordinator.InitializeMatch(TeamData home, TeamData away)
        {
            // Retrieve tactics from the provider (user or AI coach)
            Tactic homeTactic = _tacticProvider.GetTacticForTeam(home);
            Tactic awayTactic = _tacticProvider.GetTacticForTeam(away);
            InitializeMatch(home, away, homeTactic, awayTactic);
        }

        public void SimulateNextAction()
        {
            if (_engine == null)
            {
                Debug.LogWarning("Cannot simulate next action: Engine not initialized.");
                return;
            }
            _engine.Advance(Time.deltaTime);
            Debug.Log("Simulated next action (engine step).");
        }
        
        private bool IsOnMainThread() => Thread.CurrentThread.ManagedThreadId == 1;
        
        /// <summary>
        /// Initializes the coordinator with the required dependencies.
        /// </summary>
        /// <param name="engine">The match engine service.</param>
        /// <param name="aiService">The player AI service.</param>
        /// <param name="eventBus">The event bus for publishing events.</param>
        public async Task<MatchResult> RunSimulationAsync(TeamData home, TeamData away, Tactic homeTactic, Tactic awayTactic, CancellationToken cancellationToken = default)
        {
            if (!IsOnMainThread())
            {
                throw new InvalidOperationException("Simulation must start from main thread");
            }

            try
            {
                return await Task.Run(async () =>
                {
                    return await _engine.SimulateMatchAsync(
                        home,
                        away,
                        homeTactic,
                        awayTactic,
                        cancellationToken
                    );
                }, cancellationToken).ConfigureAwait(true);
            }
            finally
            {
                CleanupResources();
            }
        }

        public void AbortCurrentSimulation()
        {
            // Cleanup ongoing operations
            _engine.ResetMatch();
        }

        public void CleanupResources()
        {
            // Reset all match-related state
            _engine.ResetMatch();
        }

        /// <summary>
        /// Finalizes the match, performing any necessary cleanup or result processing.
        /// </summary>
        public MatchResult FinalizeMatch()
        {
            Debug.Log("Finalizing match: performing final cleanup and result processing.");
            CleanupResources();
            return _engine != null ? _engine.GetMatchResult() : null;
        }


        
        // Change 3: Update interface implementation
        // Remove this non-interface method completely
        // public void Initialize(IMatchEngine engine, IPlayerAIService aiService, IEventBus eventBus)
        
        // Explicit interface implementation for InitializeMatch
        // Now includes tactics and unique random seed
        public void InitializeMatch(TeamData home, TeamData away, Tactic homeTactic, Tactic awayTactic)
        {
            if (!IsOnMainThread())
                throw new InvalidOperationException("Must be called from main thread");

            // Generate a unique random seed for each game (using current time)
            int randomSeed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();

            // Create a new MatchState with provided tactics and seed
            var matchState = new HandballManager.Simulation.Core.MatchData.MatchState(home, away, homeTactic, awayTactic, randomSeed);
            _engine = (IMatchEngine)_simulatorFactory.Create(matchState);

            _eventBus.Publish(new MatchStartedEvent {
                HomeTeam = home,
                AwayTeam = away
            });
        }

        // Update coroutine with proper cancellation
        private System.Collections.IEnumerator RunSimulation(CancellationToken token)
        {
            if (!IsOnMainThread())
                throw new InvalidOperationException("Simulation must run on main thread");
        
            while (!_engine.IsMatchComplete && !token.IsCancellationRequested)
            {
                try 
                {
                    _engine.Advance(Time.deltaTime);
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("Simulation cancelled");
                    yield break;
                }
                yield return null;
            }
            
            if (!token.IsCancellationRequested)
            {
                _eventBus.Publish(new MatchCompletedEvent {
                    Result = _engine.GetMatchResult()
                });
                Debug.Log("Match simulation completed successfully");
            }
        }

        // Fix 2: Update SimulateMatch to handle tactics
        public void SimulateMatch(TeamData home, TeamData away, Tactic homeTactic, Tactic awayTactic, CancellationToken token)
        {
            // Now calls the new InitializeMatch with tactics and unique seed
            InitializeMatch(home, away, homeTactic, awayTactic);
            StartCoroutine(RunSimulation(token));
        }


    }

    /// <summary>
    /// Event fired when a match starts.
    /// </summary>
    public class MatchStartedEvent : EventBase
    {
        /// <summary>
        /// Gets or sets the home team.
        /// </summary>
        public TeamData HomeTeam { get; set; }
        
        /// <summary>
        /// Gets or sets the away team.
        /// </summary>
        public TeamData AwayTeam { get; set; }
    }
    
    /// <summary>
    /// Interface for the player AI service.
    /// </summary>
    public interface IPlayerAIService
    {
        /// <summary>
        /// Processes decisions for all AI-controlled players.
        /// </summary>
        void ProcessDecisions();
    }
    
}