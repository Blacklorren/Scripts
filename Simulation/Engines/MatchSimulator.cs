
using UnityEngine;
using HandballManager.Simulation.AI; // Updated from Engines to AI for PlayerAIController
using HandballManager.Simulation.Engines; // For PassivePlayManager
using HandballManager.Simulation.Physics; // For MovementSimulator and other physics components
using HandballManager.Core; // For Enums (GamePhase)
using System; // For Exception, ArgumentNullException
using System.Threading;
using HandballManager.Simulation.Utils;
using HandballManager.Data;
using HandballManager.Simulation.Events.Interfaces;
using System.Linq; // For Linq (used in ResolvePendingActions)

namespace HandballManager.Simulation.Engines
{
    /// <summary>
    /// Core class responsible for orchestrating the detailed simulation of a handball match.
    /// Manages the main simulation loop and delegates tasks to specialized services via injected dependencies.
    /// </summary>
    public class MatchSimulator
    {
        // Add cancellation support
        private CancellationTokenSource _cancellationSource;
        
        // --- Simulation Constants ---
        // Time step remains fundamental to the loop orchestration
        public const float TIME_STEP_SECONDS = 0.1f;
        // Match duration might be determined by external config or TimeManager later
        private const float DEFAULT_MATCH_DURATION_SECONDS = 60f * 60f;
        // Distance in meters for ball pickup detection
        public const float LOOSE_BALL_PICKUP_RADIUS = 1.5f;
        // --- Stamina Constants ---
        /// <summary>Base stamina drain rate per second for normal movement.</summary>
        public const float BASE_STAMINA_DRAIN_PER_SECOND = 0.002f;
        /// <summary>Multiplier applied to stamina drain when sprinting.</summary>
        public const float SPRINT_STAMINA_MULTIPLIER = 2.5f;

        // --- Dependencies (Injected) ---
        private readonly IPhaseManager _phaseManager;
        private readonly ISimulationTimer _simulationTimer;
        private readonly IBallPhysicsCalculator _ballPhysicsCalculator;
        private readonly IMovementSimulator _movementSimulator; // Changed to interface
        private readonly IPlayerAIController _aiController; // Changed to interface
        private readonly IActionResolver _actionResolver; // Changed to interface
        private readonly IEventDetector _eventDetector;
        private readonly IMatchEventHandler _eventHandler;
        private readonly IPlayerSetupHandler _playerSetupHandler; // Needed for initialization phase
        private readonly IMatchFinalizer _matchFinalizer;
        private readonly IGeometryProvider _geometryProvider; // May not be needed directly if services handle all geometry

        // --- Simulation State (Managed Externally, Passed In) ---
        private readonly MatchState _state;
        private PassivePlayManager _passivePlayManager;

        // --- Simulation Control ---
        private bool _isInitialized = false;
        // Seed is now primarily managed by MatchState initialization

        /// <summary>
        /// Initializes a new MatchSimulator instance with required dependencies and the initial MatchState.
        /// The MatchState should already be populated with teams, tactics, and players.
        /// </summary>
        /// <param name="initialState">The fully initialized MatchState object.</param>
        /// <param name="phaseManager">Service for managing game phases.</param>
        /// <param name="timer">Service for updating simulation timers.</param>
        /// <param name="ballPhysicsCalculator">Service for ball physics calculations.</param>
        /// <param name="movementSimulator">Engine for player/ball movement updates.</param>
        /// <param name="aiController">Engine for AI player decisions.</param>
        /// <param name="actionResolver">Engine for resolving discrete actions.</param>
        /// <param name="eventDetector">Service for detecting simulation events.</param>
        /// <param name="eventHandler">Service for handling simulation events and state changes.</param>
        /// <param name="playerSetupHandler">Service used for initial player setup validation/logging.</param>
        /// <param name="matchFinalizer">Service for finalizing the match result.</param>
        /// <param name="geometryProvider">Service providing pitch geometry info.</param>
        /// <exception cref="ArgumentNullException">Thrown if any required dependency or the initial state is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the initial state is invalid (e.g., missing players).</exception>
        public MatchSimulator(
            MatchState initialState, // State is now passed in
            IPhaseManager phaseManager,
            ISimulationTimer timer,
            IBallPhysicsCalculator ballPhysicsCalculator,
            IMovementSimulator movementSimulator, // Changed to interface
            IPlayerAIController aiController, // Changed to interface
            IActionResolver actionResolver, // Changed to interface
            IEventDetector eventDetector,
            IMatchEventHandler eventHandler,
            IPlayerSetupHandler playerSetupHandler,
            IMatchFinalizer matchFinalizer,
            IGeometryProvider geometryProvider)
        {
            // --- Dependency and State Validation ---
            _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
            _phaseManager = phaseManager ?? throw new ArgumentNullException(nameof(phaseManager));
            _simulationTimer = timer ?? throw new ArgumentNullException(nameof(timer));
            _ballPhysicsCalculator = ballPhysicsCalculator ?? throw new ArgumentNullException(nameof(ballPhysicsCalculator));
            _movementSimulator = movementSimulator ?? throw new ArgumentNullException(nameof(movementSimulator));
            _aiController = aiController ?? throw new ArgumentNullException(nameof(aiController));
            _actionResolver = actionResolver ?? throw new ArgumentNullException(nameof(actionResolver));
            _eventDetector = eventDetector ?? throw new ArgumentNullException(nameof(eventDetector));
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _playerSetupHandler = playerSetupHandler ?? throw new ArgumentNullException(nameof(playerSetupHandler)); // Keep reference if needed
            _matchFinalizer = matchFinalizer ?? throw new ArgumentNullException(nameof(matchFinalizer));
            _geometryProvider = geometryProvider ?? throw new ArgumentNullException(nameof(geometryProvider));

            // Basic validation of the passed-in state
            if (_state.HomeTeamData == null || _state.AwayTeamData == null ||
                _state.HomeTactic == null || _state.AwayTactic == null ||
                _state.Ball == null || _state.AllPlayers == null || _state.RandomGenerator == null ||
                _state.HomePlayersOnCourt == null || _state.AwayPlayersOnCourt == null)
            {
                throw new InvalidOperationException("Initial MatchState is missing critical data.");
            }
             // Ensure lineups seem valid (correct count after setup)
             // Setup (PopulatePlayers/SelectLineups) is assumed to have happened BEFORE this constructor
             if (_state.HomePlayersOnCourt.Count != 7 || _state.AwayPlayersOnCourt.Count != 7)
             {
                 _eventHandler.LogEvent(_state, $"ERROR: Invalid lineup count detected during MatchSimulator init. H:{_state.HomePlayersOnCourt.Count} A:{_state.AwayPlayersOnCourt.Count}");
                 throw new InvalidOperationException("Invalid player lineup count in initial MatchState.");
             }

             // Initialize Phase Manager (runs initial setup like PreKickOff)
             try
             {
                 _phaseManager.TransitionToPhase(_state, GamePhase.PreKickOff, forceSetup: true);
                 _phaseManager.HandlePhaseTransitions(_state); // Run initial PreKickOff setup immediately
                 _isInitialized = true;
                 _eventHandler.LogEvent(_state, $"MatchSimulator Initialized. Seed: {_state.RandomGenerator.GetHashCode()}"); // Log seed if needed

                  // --- Passive Play Manager ---
                  _passivePlayManager = new PassivePlayManager(_state);

                  // Inject PassivePlayManager into event handler if supported
                  if (_eventHandler is HandballManager.Simulation.Events.Handlers.DefaultMatchEventHandler defaultHandler)
                  {
                      defaultHandler.SetPassivePlayManager(_passivePlayManager);
                  }

                  // --- Subscribe to pass completion event for passive play ---
                  if (_state.Ball != null)
                  {
                      _state.Ball.OnPassCompletedBetweenTeammates += (teamSimId) =>
                      {
                          var status = _passivePlayManager.OnPassMade(teamSimId);
                          if (status == PassivePlayStatus.ViolationTriggered && _eventHandler is HandballManager.Simulation.Events.Handlers.DefaultMatchEventHandler defaultHandler)
                          {
                              // Construct ActionResult for turnover
                              var state = _state;
                              var passiveTurnoverResult = new ActionResult {
                                  Outcome = ActionResultOutcome.Turnover,
                                  PrimaryPlayer = null,
                                  SecondaryPlayer = null,
                                  Reason = "Passive Play Violation (Pass Limit)",
                                  ImpactPosition = state.Ball?.Position ?? state.PlayersOnCourt.FirstOrDefault()?.Position ?? Vector2.zero
                              };
                              defaultHandler.HandleTurnover(passiveTurnoverResult, state);
                              // No need to call ResetPassivePlay here; PassivePlayManager resets itself.
                          }
                      };
                  }
             }
             catch (Exception ex)
             {
                  _eventHandler.HandleStepError(_state, "Initialization Phase Setup", ex);
                  _isInitialized = false; // Mark as failed
                  // Let the exception propagate up to MatchEngine
                  throw;
             }
        }

        /// <summary>
        /// Runs the main simulation loop from the current state until the match finishes.
        /// Assumes the MatchState and dependencies were properly initialized in the constructor.
        /// </summary>
        /// <param name="matchDate">The date the match occurred (passed to finalizer).</param>
        /// <returns>The MatchResult containing score and statistics.</returns>
        public MatchResult SimulateMatch(DateTime matchDate)
        {
            if (!_isInitialized || _state == null)
            {
                _eventHandler?.LogEvent(_state, "Match Simulation cannot start: Not Initialized.");
                return _matchFinalizer.FinalizeResult(null, matchDate);
            }

            try
            {
                _eventHandler.LogEvent(_state, "Match Simulation Started");
                
                while (_state.CurrentPhase != GamePhase.Finished && !_cancellationSource.Token.IsCancellationRequested)
                {
                    // --- Passive Play Warning System ---
                    _passivePlayManager.Update(TIME_STEP_SECONDS);

                    // Exemple d'intégration pour chaque passe (à placer dans la logique de passe réelle) :
                    // _passivePlayManager.OnPassMade(_state.PossessionTeamId);

                    // Exemple d'intégration pour une sanction défensive (à placer dans la logique de gestion des sanctions) :
                    // _passivePlayManager.ResetAttackTimer();

                    // Check for external cancellation if token was implemented
                    // cancellationToken.ThrowIfCancellationRequested();
                }

                if (_cancellationSource.Token.IsCancellationRequested)
                {
                    _eventHandler.LogEvent(_state, "Match Simulation cancelled by request");
                    _phaseManager.TransitionToPhase(_state, GamePhase.Finished);
                }

                return _matchFinalizer.FinalizeResult(_state, matchDate);
            }
            catch (OperationCanceledException)
            {
                _eventHandler.LogEvent(_state, "Match Simulation cancelled");
                return _matchFinalizer.FinalizeResult(_state, matchDate);
            }
            finally
            {
                CleanupResources();
            }
        }

        private void CleanupResources()
        {
            _cancellationSource?.Dispose();
            _cancellationSource = new CancellationTokenSource();
        }

        public void CancelSimulation()
        {
            _cancellationSource?.Cancel();
        }
    }
}