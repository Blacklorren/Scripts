
using UnityEngine;
using HandballManager.Simulation.AI; // Updated from Engines to AI for PlayerAIController
using HandballManager.Simulation.AI.Positioning; // For ITacticPositioner
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
            // --- Injection du simulateur dans l'IA si possible ---
            if (_aiController is HandballManager.Simulation.AI.PlayerAIController concreteAI)
            {
                concreteAI.SetMatchSimulator(this);
            }
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
        /// <summary>
        /// Runs the main simulation loop from the current state until the match finishes.
        /// Handles timeout phase by pausing match time and player actions.
        /// </summary>
        public MatchResult SimulateMatch(DateTime matchDate)
        {
            float perSecondAccumulator = 0f;
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
                    if (_state.CurrentPhase == GamePhase.Timeout)
                    {
                        // Only decrement TimeoutTimer, do not advance match time or update player actions
                        _state.TimeoutTimer -= TIME_STEP_SECONDS;
                        if (_state.TimeoutTimer <= 0f)
                        {
                            _eventHandler.LogEvent(_state, $"Timeout ended for team {_state.PossessionTeamId}");
                            _state.TimeoutTimer = 0f;
                            // Restore previous phase
                            _phaseManager.TransitionToPhase(_state, _state.PhaseBeforeTimeout);
                        }
                        continue; // Skip normal gameplay updates
                    }

                    // --- Passive Play Warning System ---
                    _passivePlayManager.Update(TIME_STEP_SECONDS);

                    // --- Check for half-time transition and invalidate unused timeout ---
                    if (!_state.IsSecondHalf && _state.HalfTimeReached)
                    {
                        _state.IsSecondHalf = true;
                        _state.InvalidateFirstTimeoutIfNotUsed();
                        _eventHandler.LogEvent(_state, "Mi-temps atteinte : 1er timeout perdu si non utilisé");
                    }

                    // (Normal simulation logic would go here)

                    // --- Suivi du temps de jeu individuel ---
                    // On incrémente les minutes jouées pour chaque joueur sur le terrain toutes les secondes
                    // (On suppose TIME_STEP_SECONDS = 0.1f, donc on accumule et on update chaque seconde)
                    perSecondAccumulator += TIME_STEP_SECONDS;
                    if (perSecondAccumulator >= 1.0f)
                    {
                        foreach (var p in _state.HomePlayersOnCourt.Concat(_state.AwayPlayersOnCourt))
                        {
                            if (p == null) continue;
                            int pid = p.GetPlayerId();
                            if (!_state.PlayerStats.ContainsKey(pid))
                                _state.PlayerStats[pid] = new PlayerMatchStats();
                            _state.PlayerStats[pid].MinutesPlayed++;
                            _state.PlayerStats[pid].Participated = true;
                        }
                        perSecondAccumulator = 0f;
                    }

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
        /// <summary>
        /// Attempts to perform a tactical substitution during the match simulation.
        /// </summary>
        /// <param name="playerOut">The player to be substituted out (must be on court).</param>
        /// <param name="playerIn">The player to be substituted in (must be on bench).</param>
        /// <returns>True if the substitution was successful, false otherwise.</returns>
        public bool TrySubstitute(SimPlayer playerOut, SimPlayer playerIn)
        {
            // Use the static SubstitutionManager and inject dependencies from this simulator
            // _state: current MatchState
            // _eventHandler: event logger
            // _aiController: AI controller (implements IPlayerAIController)
            // _phaseManager or another dependency could implement ITacticPositioner if available
            ITacticPositioner tacticPositioner = _aiController as ITacticPositioner; // Try cast if applicable
            return SubstitutionManager.TrySubstitute(
                _state,
                playerOut,
                playerIn,
                _eventHandler,
                tacticPositioner,
                _aiController
            );
        }
        /// <summary>
        /// Triggers a timeout for the specified team if allowed.
        /// </summary>
        /// <param name="teamSimId">0=Home, 1=Away</param>
        /// <returns>True if timeout was successfully triggered, false otherwise.</returns>
        public bool TriggerTimeout(int teamSimId)
        {
            // Only allow timeout if not already in timeout and team is in possession
            if (_state.CurrentPhase == GamePhase.Timeout)
            {
                _eventHandler.LogEvent(_state, "Timeout request ignored: already in timeout phase.");
                return false;
            }
            if (_state.PossessionTeamId != teamSimId)
            {
                _eventHandler.LogEvent(_state, $"Timeout request denied: Team {teamSimId} not in possession.");
                return false;
            }
            // Check remaining timeouts
            if (teamSimId == 0 && _state.HomeTimeoutsRemaining <= 0)
            {
                _eventHandler.LogEvent(_state, "Home team has no timeouts remaining.");
                return false;
            }
            if (teamSimId == 1 && _state.AwayTimeoutsRemaining <= 0)
            {
                _eventHandler.LogEvent(_state, "Away team has no timeouts remaining.");
                return false;
            }
            // Store previous phase
            _state.PhaseBeforeTimeout = _state.CurrentPhase;
            // Decrement timeout count
            if (teamSimId == 0) _state.HomeTimeoutsRemaining--;
            else if (teamSimId == 1) _state.AwayTimeoutsRemaining--;
            // Set timeout phase and timer
            _phaseManager.TransitionToPhase(_state, GamePhase.Timeout);
            _state.TimeoutTimer = 60f; // Standard timeout duration (can be made configurable)
            _eventHandler.LogEvent(_state, $"Timeout started for team {teamSimId}");
            return true;
        }
    }
}