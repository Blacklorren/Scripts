using UnityEngine;
using HandballManager.Core;
using System;
using System.Linq;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Events.Interfaces;
using HandballManager.Simulation.AI.Positioning; // Added for SimConstants

namespace HandballManager.Simulation.Utils // Changed from Services to Utils
{
    /// <summary>
    /// Manages game phase transitions and player positioning for different match situations.
    /// Handles setup for kickoffs, set pieces, penalties, and half-time transitions.
    /// </summary>
    public class DefaultPhaseManager : IPhaseManager
    {
        private readonly IPlayerSetupHandler _playerSetupHandler;
        private readonly IMatchEventHandler _eventHandler;
        private readonly IGeometryProvider _geometry;
        private readonly TacticPositioner _tacticPositioner; 
        private bool _setupPending = false;
        
        // Constants for boundary checking and positioning
        private const float PENALTY_SHOOTER_OFFSET = 0.2f;

        // Constants
        private const float SET_PIECE_DEFENDER_DISTANCE = 3.0f;
        private const float SET_PIECE_DEFENDER_DISTANCE_SQ = SET_PIECE_DEFENDER_DISTANCE * SET_PIECE_DEFENDER_DISTANCE;
        private const float DEF_GK_DEPTH = 0.5f;
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f;
        private const float HALF_DURATION_SECONDS = 30f * 60f;
        private const float FULL_DURATION_SECONDS = 60f * 60f;
        private const float PLAYER_SPACING_OFFSET = 0.5f; // Spacing between players behind free throw line
        private const float SAFETY_FACTOR = 1.05f; // Safety factor for minimum distances


        // Inject TacticPositioner - ideally via interface if extracted later
        /// <summary>
        /// Initializes a new instance of the DefaultPhaseManager class.
        /// </summary>
        /// <param name="playerSetupHandler">Handler for positioning players on the court</param>
        /// <param name="eventHandler">Handler for match events</param>
        /// <param name="geometry">Provider of court geometry information</param>
        /// <param name="tacticPositioner">Tactical positioning service</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is null</exception>
        public DefaultPhaseManager(IPlayerSetupHandler playerSetupHandler, IMatchEventHandler eventHandler, IGeometryProvider geometry, TacticPositioner tacticPositioner)
        {
            _playerSetupHandler = playerSetupHandler ?? throw new ArgumentNullException(nameof(playerSetupHandler));
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _tacticPositioner = tacticPositioner ?? throw new ArgumentNullException(nameof(tacticPositioner)); // Store TacticPositioner
        }

        /// <summary>
        /// Checks if half-time has been reached and handles the transition if needed.
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="timeBeforeStep">Match time before the current simulation step</param>
        /// <param name="timeAfterStep">Match time after the current simulation step</param>
        /// <returns>True if half-time was reached and handled, false otherwise</returns>
        public bool CheckAndHandleHalfTime(MatchState state, float timeBeforeStep, float timeAfterStep)
        {
             if (state != null && state.CurrentPhase != GamePhase.HalfTime &&
                 !state.HalfTimeReached &&
                 timeBeforeStep < HALF_DURATION_SECONDS && timeAfterStep >= HALF_DURATION_SECONDS)
             {
                 _eventHandler.LogEvent(state, "Half Time Reached.");
                 state.HalfTimeReached = true;
                 TransitionToPhase(state, GamePhase.HalfTime);
                 return true;
             }
             return false;
        }

        /// <summary>
        /// Checks if full-time has been reached and handles the transition if needed.
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="timeAfterStep">Match time after the current simulation step</param>
        /// <returns>True if full-time was reached and handled, false otherwise</returns>
        public bool CheckAndHandleFullTime(MatchState state, float timeAfterStep)
        {
             if (state != null && timeAfterStep >= FULL_DURATION_SECONDS && state.CurrentPhase != GamePhase.Finished) {
                 _eventHandler.LogEvent(state, "Full Time Reached.");
                 if (state.Ball != null && state.Ball.Position.y > SimConstants.BALL_DEFAULT_HEIGHT * 3f) {
                     _eventHandler.LogEvent(state, "Ball in air at full time - waiting for it to land.");
                     return false;
                 }
                 TransitionToPhase(state, GamePhase.Finished);
                 return true;
             }
             return false;
        }

        /// <summary>
        /// Transitions the match to a new game phase and determines if setup is required.
        /// Sets _setupPending only for phases that require player/ball setup (see list below).
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="newPhase">The new phase to transition to</param>
        /// <param name="forceSetup">Whether to force setup for the new phase</param>
        public void TransitionToPhase(MatchState state, GamePhase newPhase, bool forceSetup = false)
        {
            // --- Clock pausing logic ---
            // Pause clock for Timeout, HalfTime, Penalty, SetPiece, and PreKickOff
            switch (newPhase)
            {
                case GamePhase.Timeout:
                case GamePhase.HalfTime:
                case GamePhase.HomePenalty:
                case GamePhase.AwayPenalty:
                case GamePhase.HomeSetPiece:
                case GamePhase.AwaySetPiece:
                case GamePhase.PreKickOff:
                    state.IsClockPaused = true;
                    break;
                default:
                    state.IsClockPaused = false;
                    break;
            }
            // --- End clock pausing logic ---
             if (state == null) {
                 Debug.LogError("[DefaultPhaseManager] Cannot transition phase: MatchState is null.");
                 return;
             }
             
             if (state.CurrentPhase == newPhase) return;

             _eventHandler.LogEvent(state, $"Phase transition: {state.CurrentPhase} -> {newPhase}"); // Log transition

             state.CurrentPhase = newPhase;

             // Only these phases require setup (see ExecutePhaseSetup):
             // PreKickOff, HomeSetPiece, AwaySetPiece, HomePenalty, AwayPenalty, HalfTime, or forceSetup == true
             if (forceSetup || newPhase == GamePhase.PreKickOff || newPhase == GamePhase.HomeSetPiece ||
                 newPhase == GamePhase.AwaySetPiece || newPhase == GamePhase.HomePenalty ||
                 newPhase == GamePhase.AwayPenalty || newPhase == GamePhase.HalfTime)
             {
                 _setupPending = true; // Set _setupPending for phases that require setup
             }
        }

        /// <summary>
        /// Handles pending phase transitions and executes automatic phase transitions.
        /// If a setup is pending (e.g., after entering a phase that requires setup), executes the required setup.
        /// If setup fails, transitions to ContestedBall and resets possession.
        /// Then, checks if an automatic phase transition is required (e.g., KickOff → HomeAttack).
        /// </summary>
        /// <param name="state">Current match state</param>
        public void HandlePhaseTransitions(MatchState state)
        {
             if (state == null) return;
             GamePhase phaseBeforeSetup = state.CurrentPhase;

             if (_setupPending)
             {
                 _setupPending = false;
                 if (!ExecutePhaseSetup(state, phaseBeforeSetup))
                 {
                     Debug.LogError($"[DefaultPhaseManager] Setup failed for phase {phaseBeforeSetup}. Reverting to ContestedBall.");
                     TransitionToPhase(state, GamePhase.ContestedBall);
                     _eventHandler.HandlePossessionChange(state, -1, true); // Ensure correct state
                     return;
                 }
             }
             ExecuteAutomaticPhaseTransitions(state);
             // Runtime assertion for valid phase transitions
             GamePhase phaseAfter = state.CurrentPhase;
             Debug.Assert(
                 phaseAfter == phaseBeforeSetup ||
                 (phaseBeforeSetup == GamePhase.PreKickOff && phaseAfter == GamePhase.KickOff) ||
                 (phaseBeforeSetup == GamePhase.KickOff && (phaseAfter == GamePhase.HomeAttack || phaseAfter == GamePhase.AwayAttack)) ||
                 (phaseBeforeSetup == GamePhase.TransitionToHomeAttack && phaseAfter == GamePhase.HomeAttack) ||
                 (phaseBeforeSetup == GamePhase.TransitionToAwayAttack && phaseAfter == GamePhase.AwayAttack) ||
                 (phaseBeforeSetup == GamePhase.HalfTime && phaseAfter == GamePhase.PreKickOff),
                 $"[DefaultPhaseManager] Unexpected phase transition: {phaseBeforeSetup} -> {phaseAfter}"
             );
        }

        /// <summary>
        /// Executes setup logic for phases that require special player/ball positioning or state changes.
        /// Only called for phases where _setupPending was set (see TransitionToPhase).
        /// Returns true if setup succeeds, false if setup fails and a fallback is needed.
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="currentPhase">Phase to set up</param>
        /// <returns>True if setup succeeded, false otherwise</returns>
        private bool ExecutePhaseSetup(MatchState state, GamePhase currentPhase)
        {
             if (state == null) { Debug.LogError("[DefaultPhaseManager] Cannot execute phase setup: MatchState is null."); return false; }
             bool setupSuccess = true;
             switch (currentPhase)
             {
                 case GamePhase.PreKickOff:
                     int startingTeamId = DetermineKickoffTeam(state);
                     setupSuccess = SetupForKickOff(state, startingTeamId);
                     if (setupSuccess) _eventHandler.LogEvent(state, $"Setup for Kickoff. Team {startingTeamId} starts.", startingTeamId);
                     break;
                 case GamePhase.HomeSetPiece: case GamePhase.AwaySetPiece:
                      setupSuccess = SetupForSetPiece(state);
                      if (setupSuccess) _eventHandler.LogEvent(state, $"Setup for Set Piece ({currentPhase}).");
                      break;
                 case GamePhase.HomePenalty: case GamePhase.AwayPenalty:
                      setupSuccess = SetupForPenalty(state);
                      if (setupSuccess) _eventHandler.LogEvent(state, $"Setup for Penalty ({currentPhase}).");
                      break;
                 case GamePhase.HalfTime:
                      setupSuccess = SetupForHalfTime(state);
                      if (setupSuccess) _eventHandler.LogEvent(state, "Half Time setup actions completed.");
                      break;
                 // Other phases do not require setup
                 case GamePhase.KickOff: case GamePhase.HomeAttack: case GamePhase.AwayAttack:
                 case GamePhase.TransitionToHomeAttack: case GamePhase.TransitionToAwayAttack:
                 case GamePhase.ContestedBall: case GamePhase.Timeout: case GamePhase.Finished:
                      break;
                 default: Debug.LogWarning($"[DefaultPhaseManager] Unhandled phase in ExecutePhaseSetup: {currentPhase}"); break;
             }
             return setupSuccess;
        }

        /// <summary>
        /// Handles automatic transitions between phases that do not require user input or additional setup.
        /// For example, after KickOff setup, transitions to HomeAttack/AwayAttack; after TransitionToHomeAttack, transitions to HomeAttack, etc.
        /// Does nothing for Finished, HalfTime, or Timeout phases.
        /// </summary>
        /// <param name="state">Current match state</param>
        private void ExecuteAutomaticPhaseTransitions(MatchState state)
        {
             if (state == null) return;
             GamePhase currentPhase = state.CurrentPhase;
             if (currentPhase == GamePhase.Finished || currentPhase == GamePhase.HalfTime || currentPhase == GamePhase.Timeout) return;

             GamePhase nextPhase = currentPhase;
             switch (currentPhase)
             {
                 case GamePhase.KickOff:
                       // After kickoff, automatically start the attack for the team in possession
                       if(state.PossessionTeamId == 0 || state.PossessionTeamId == 1) {
                           nextPhase = (state.PossessionTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack;
                       } else { Debug.LogWarning($"[DefaultPhaseManager] Invalid possession ({state.PossessionTeamId}) after KickOff setup."); }
                      break;
                 case GamePhase.TransitionToHomeAttack: nextPhase = GamePhase.HomeAttack; break;
                 case GamePhase.TransitionToAwayAttack: nextPhase = GamePhase.AwayAttack; break;
             }
             if (nextPhase != currentPhase) { TransitionToPhase(state, nextPhase); }
        }

        /// <summary>
        /// Determines which team should take the kickoff based on match context.
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <returns>Team ID (0 for home, 1 for away) that should take the kickoff</returns>
        public int DetermineKickoffTeam(MatchState state)
        {
             if (state.FirstHalfKickOffTeamId == -1) {
                 int startingTeamId = state.RandomGenerator.Next(0, 2);
                 state.FirstHalfKickOffTeamId = startingTeamId;
                 return startingTeamId;
             } else if (state.IsSecondHalf) {
                 return 1 - state.FirstHalfKickOffTeamId;
             } else {
                 if (state.PossessionTeamId != 0 && state.PossessionTeamId != 1) {
                     Debug.LogWarning($"[DefaultPhaseManager] Invalid PossessionTeamId ({state.PossessionTeamId}) during post-goal kickoff determination. Defaulting.");
                     return state.RandomGenerator.Next(0, 2);
                 }
                 return state.PossessionTeamId; // Team that conceded restarts
             }
        }

        /// <summary>
        /// Sets up the match for a kickoff.
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="startingTeamId">Team ID that will take the kickoff</param>
        /// <returns>True if setup was successful, false otherwise</returns>
        public bool SetupForKickOff(MatchState state, int startingTeamId)
        {
             if (state?.Ball == null) return false;
             state.PossessionTeamId = startingTeamId;
             try {
                 _playerSetupHandler.PlacePlayersInFormation(state, state.HomePlayersOnCourt.ToList(), true, true);
                 _playerSetupHandler.PlacePlayersInFormation(state, state.AwayPlayersOnCourt.ToList(), false, true);
             } catch (Exception ex) { Debug.LogError($"Error placing players in formation for kickoff: {ex.Message}"); return false; }

             state.Ball.Stop(); state.Ball.Position = _geometry.Center;
            state.Ball.MakeLoose(_geometry.Center, Vector3.zero, -1); // Utilise MakeLoose pour r�initialiser LastShooter
            state.Ball.ResetPassContext();

            SimPlayer startingPlayer = _playerSetupHandler.FindPlayerByPosition(state, state.GetTeamOnCourt(startingTeamId), PlayerPosition.CentreBack)
                                    ?? state.GetTeamOnCourt(startingTeamId)?.FirstOrDefault(p => p != null && p.IsOnCourt && !p.IsGoalkeeper());
             if (startingPlayer == null) {
                 Debug.LogError($"Could not find starting player for kickoff for Team {startingTeamId}");
                 state.Ball.MakeLoose(_geometry.Center, Vector3.zero, -1);
                 TransitionToPhase(state, GamePhase.ContestedBall);
                 return false;
             }

              Vector3 offset = Vector3.right * (startingTeamId == 0 ? -0.1f : 0.1f);
              Vector3 playerStartPos3D = _geometry.Center + offset;
              startingPlayer.Position = CoordinateUtils.To2DGround(playerStartPos3D);
              startingPlayer.TargetPosition = startingPlayer.Position; startingPlayer.CurrentAction = PlayerAction.Idle;
              state.Ball.SetPossession(startingPlayer);

             state.CurrentPhase = GamePhase.KickOff; // Set AFTER setup

             foreach(var p in state.PlayersOnCourt) {
                  if(p != null && p != startingPlayer && !p.IsSuspended()) { p.CurrentAction = PlayerAction.Idle; }
             }
             return true;
        }

        /// <summary>
        /// Sets up the match for a set piece (free throw).
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <returns>True if setup was successful, false otherwise</returns>
        public bool SetupForSetPiece(MatchState state)
        {
             if (state?.Ball == null) return false;
             int attackingTeamId = state.PossessionTeamId;
             int defendingTeamId = 1 - attackingTeamId;
             if (attackingTeamId == -1) {
                 Debug.LogError("Cannot setup Set Piece: PossessionTeamId is -1. Reverting.");
                 TransitionToPhase(state, GamePhase.ContestedBall); return false;
             }
             Vector3 ballPos3D = state.Ball.Position;
             Vector2 ballPos2D = CoordinateUtils.To2DGround(ballPos3D);

             SimPlayer thrower = state.GetTeamOnCourt(attackingTeamId)?
                                    .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                                    .OrderBy(p => Vector2.Distance(p.Position, ballPos2D))
                                    .FirstOrDefault();
             if (thrower == null) {
                 Debug.LogError($"Cannot find thrower for Set Piece Team {attackingTeamId}. Reverting.");
                 state.Ball.MakeLoose(ballPos3D, Vector3.zero, -1);
                 TransitionToPhase(state, GamePhase.ContestedBall); return false;
             }

             state.Ball.SetPossession(thrower);
             Vector3 throwerOffset = new Vector3(0.05f, 0f, 0.05f);
             Vector3 throwerPos3D = ballPos3D + throwerOffset;
             thrower.Position = CoordinateUtils.To2DGround(throwerPos3D);
             thrower.TargetPosition = thrower.Position; thrower.CurrentAction = PlayerAction.Idle;

             // Position other players using TacticPositioner
             foreach (var player in state.PlayersOnCourt.ToList()) {
                  if (player == null || player == thrower || player.IsSuspended()) continue;
                  try {
                     Vector2 targetTacticalPos = _tacticPositioner.GetPlayerTargetPosition(player, state); // Use injected TacticPositioner
                     player.Position = targetTacticalPos;

                     if (player.TeamSimId == defendingTeamId) {
                         Vector2 vecFromBall = player.Position - ballPos2D;
                         float currentDistSq = vecFromBall.sqrMagnitude;
                         if (currentDistSq > MIN_DISTANCE_CHECK_SQ && currentDistSq < SET_PIECE_DEFENDER_DISTANCE_SQ) {
                              player.Position = ballPos2D + vecFromBall.normalized * SET_PIECE_DEFENDER_DISTANCE * SAFETY_FACTOR;
                         }
                     }
                     player.CurrentAction = PlayerAction.Idle; player.TargetPosition = player.Position; player.Velocity = Vector2.zero;
                  } catch (Exception ex) { Debug.LogError($"Error positioning player {player.GetPlayerId()} for set piece: {ex.Message}"); }
             }
             return true;
        }

        /// <summary>
        /// Sets up the match for a penalty shot.
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <returns>True if setup was successful, false otherwise</returns>
        public bool SetupForPenalty(MatchState state)
        {
             if (state?.Ball == null) return false;
             int shootingTeamId = state.PossessionTeamId;
             int defendingTeamId = 1 - shootingTeamId;
             if (shootingTeamId == -1) {
                  Debug.LogError("Cannot setup Penalty: PossessionTeamId is -1. Reverting.");
                  TransitionToPhase(state, GamePhase.ContestedBall); return false;
             }
             bool shootingHome = (shootingTeamId == 0);

              Vector3 penaltySpot3D = shootingHome ? _geometry.AwayPenaltySpot3D : _geometry.HomePenaltySpot3D;
            state.Ball.Stop(); state.Ball.Position = penaltySpot3D; state.Ball.MakeLoose(penaltySpot3D, Vector3.zero, -1);

            SimPlayer shooter = state.GetTeamOnCourt(shootingTeamId)?
                                   .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                                   .OrderByDescending(p => p.BaseData?.ShootingAccuracy ?? 0)
                                   .FirstOrDefault();
              if (shooter == null) {
                  Debug.LogError($"Cannot find penalty shooter for Team {shootingTeamId}. Reverting.");
                  state.Ball.MakeLoose(penaltySpot3D, Vector3.zero, -1);
                  TransitionToPhase(state, GamePhase.ContestedBall); return false;
              }

              Vector2 penaltySpot2D = CoordinateUtils.To2DGround(penaltySpot3D);
              shooter.Position = penaltySpot2D + Vector2.right * (shootingHome ? -PENALTY_SHOOTER_OFFSET : PENALTY_SHOOTER_OFFSET);
              shooter.TargetPosition = shooter.Position; shooter.CurrentAction = PlayerAction.PreparingShot; shooter.ActionTimer = 1.0f;

              SimPlayer gk = state.GetGoalkeeper(defendingTeamId);
              if (gk != null) {
                   Vector3 goalCenter3D = defendingTeamId == 0 ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
                   Vector2 gkPos2D = new Vector2(goalCenter3D.x + (defendingTeamId == 0 ? DEF_GK_DEPTH : -DEF_GK_DEPTH), goalCenter3D.z);
                   gk.Position = gkPos2D;
                   gk.TargetPosition = gk.Position; gk.CurrentAction = PlayerAction.GoalkeeperSaving;
              } else { Debug.LogWarning($"No Goalkeeper found for defending team {defendingTeamId} during penalty setup."); }

              Vector2 opponentGoalCenter2D = shootingHome ? CoordinateUtils.To2DGround(_geometry.AwayGoalCenter3D) : CoordinateUtils.To2DGround(_geometry.HomeGoalCenter3D);
              float freeThrowLineX = opponentGoalCenter2D.x + (shootingHome ? -_geometry.FreeThrowLineRadius : _geometry.FreeThrowLineRadius);

              // Enhanced boundary checking for players during penalty setup
              foreach (var player in state.PlayersOnCourt.ToList()) {
                   if (player == null || player == shooter || player == gk || player.IsSuspended()) continue;
                   try {
                      // Get tactical position first
                      player.Position = _tacticPositioner.GetPlayerTargetPosition(player, state); // Use injected TacticPositioner
                      
                      // Check if player is on the wrong side of the free throw line
                      bool isPlayerBeyondLine = (shootingHome && player.Position.x >= freeThrowLineX) || 
                                               (!shootingHome && player.Position.x <= freeThrowLineX);
                      
                      if (isPlayerBeyondLine) {
                           // Calculate unique offset based on player ID to avoid clustering
                           int playerIndex = player.GetPlayerId() % 5;
                           float offsetX = shootingHome ? 
                               -(PLAYER_SPACING_OFFSET + playerIndex * PLAYER_SPACING_OFFSET) : 
                               (PLAYER_SPACING_OFFSET + playerIndex * PLAYER_SPACING_OFFSET);

                        // Position player behind the free throw line with proper spacing
                        // Create a new Vector2 instead of trying to modify Position.x directly
                        float newY = player.Position.y;

                        // Ensure player is not too close to the sidelines
                        float halfWidth = _geometry.PitchWidth / 2f - _geometry.SidelineBuffer;
                        newY = Mathf.Clamp(newY, -halfWidth, halfWidth);

                        // Set the complete position vector
                        player.Position = new Vector2(freeThrowLineX + offsetX, newY);
                    }
                      
                      // Reset player state
                      player.CurrentAction = PlayerAction.Idle; 
                      player.TargetPosition = player.Position; 
                      player.Velocity = Vector2.zero;
                   } catch (Exception ex) { 
                       Debug.LogError($"Error positioning player {player.GetPlayerId()} for penalty: {ex.Message}"); 
                       // Try to recover with a safe position if possible
                       if (player != null) {
                           try {
                               // Place in a safe default position if positioning failed
                               float safeX = freeThrowLineX + (shootingHome ? -PLAYER_SPACING_OFFSET * 2 : PLAYER_SPACING_OFFSET * 2);
                               player.Position = new Vector2(safeX, 0);
                               player.TargetPosition = player.Position;
                               player.CurrentAction = PlayerAction.Idle;
                               player.Velocity = Vector2.zero;
                           } catch (Exception fallbackEx) {
                               Debug.LogError($"Failed to recover player position: {fallbackEx.Message}");
                           }
                       }
                   }
              }
              return true;
        }

        /// <summary>
        /// Sets up the match for the second half after half-time.
        /// Recovers player stamina and prepares for second half kickoff.
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <returns>True if setup was successful, false otherwise</returns>
        public bool SetupForHalfTime(MatchState state)
        {
             if (state == null) return false;
             foreach (var player in state.AllPlayers.Values) {
                 if (player?.BaseData == null) continue;
                 try {
                    float recoveryAmount = (1.0f - player.Stamina) * 0.4f;
                    recoveryAmount *= Mathf.Lerp(0.8f, 1.2f, (player.BaseData.NaturalFitness > 0 ? player.BaseData.NaturalFitness : 50f) / 100f);
                    player.Stamina = Mathf.Clamp01(player.Stamina + recoveryAmount);
                    player.UpdateEffectiveSpeed();
                 } catch (Exception ex) { Debug.LogError($"Error recovering stamina for player {player.GetPlayerId()}: {ex.Message}"); }
             }
             state.IsSecondHalf = true;
             TransitionToPhase(state, GamePhase.PreKickOff);
             return true;
        }

        /// <summary>
        /// Converts a simulation team ID (0 or 1) to the actual team ID from team data.
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="simId">Simulation team ID (0=home, 1=away)</param>
        /// <returns>The actual team ID or null if not found</returns>
        private int? GetTeamIdFromSimId(MatchState state, int simId)
        {
            if (state == null) return null;
            if (simId == 0) return state.HomeTeamData?.TeamID;
            if (simId == 1) return state.AwayTeamData?.TeamID;
            return null;
        }

        public static GameSituationType MapPhaseToSituationType(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.PreKickOff:
                case GamePhase.KickOff:
                    return GameSituationType.KickOff;

                case GamePhase.HomeSetPiece:
                case GamePhase.AwaySetPiece:
                    return GameSituationType.FreeThrow; // Default - may need context to determine if it's a throw-in

                case GamePhase.HomePenalty:
                case GamePhase.AwayPenalty:
                    return GameSituationType.Penalty;

                case GamePhase.Timeout:
                    return GameSituationType.Timeout;

                case GamePhase.HalfTime:
                    return GameSituationType.HalfTime;

                // For attack, transition, and contested phases, use Normal
                default:
                    return GameSituationType.Normal;
            }
        }
    }
}