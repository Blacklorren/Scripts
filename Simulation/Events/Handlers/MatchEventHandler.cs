using UnityEngine;
using System;
using System.Linq;
using HandballManager.Data;
using HandballManager.Simulation.Utils;
using HandballManager.Core;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Physics;

namespace HandballManager.Simulation.Events.Handlers
{
    public partial class MatchEventHandler : DefaultMatchEventHandler
    {
        private readonly MatchSimulator _simulator; // Reference for logging events
        private readonly IGeometryProvider _geometry;
        private readonly JumpSimulator _jumpSimulator;

        /// <summary>
        /// Initializes a new instance of the MatchEventHandler.
        /// </summary>
        /// <param name="simulator">A reference to the parent MatchSimulator for logging.</param>
        /// <param name="geometry">The geometry provider for the match.</param>
        /// <param name="jumpSimulator">The jump simulator for player jumps.</param>
        public MatchEventHandler(MatchSimulator simulator, IGeometryProvider geometry, JumpSimulator jumpSimulator)
            : base(geometry)
        {
            _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
            _jumpSimulator = jumpSimulator ?? throw new ArgumentNullException(nameof(jumpSimulator));
        }

        // Team ID constants - Use Core.SimConstants
        private const int HOME_TEAM_ID = Core.SimConstants.HOME_TEAM_ID;
        private const int AWAY_TEAM_ID = Core.SimConstants.AWAY_TEAM_ID;

        // --- Constants ---
        // Suspension times now reference Core.SimConstants
        private const float DEFAULT_SUSPENSION_TIME = Core.SimConstants.DEFAULT_SUSPENSION_TIME;
        private const float RED_CARD_SUSPENSION_TIME = Core.SimConstants.RED_CARD_SUSPENSION_TIME;
        private const float LOOSE_BALL_MIN_REBOUND_SPEED = 1f;
        private const float LOOSE_BALL_MAX_REBOUND_SPEED = 3f;
        private const float BLOCK_REBOUND_MIN_SPEED_FACTOR = 0.2f;
        private const float BLOCK_REBOUND_MAX_SPEED_FACTOR = 0.5f;
        private const float OOB_RESTART_BUFFER = 0.1f; // How far inside boundary to place restart position
        private float GOAL_THROW_RESTART_DIST => _geometry.GoalAreaRadius + 0.2f; // Distance from goal center
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f; // Minimum squared distance for position checks

        /// <summary>
        /// Main entry point called after an action is resolved or a reactive event occurs.
        /// Routes the result to the appropriate specific handler.
        /// </summary>
        /// <param name="result">The ActionResult containing details of the event.</param>
        /// <param name="state">The current MatchState to be updated.</param>
        public override void HandleActionResult(ActionResult result, MatchState state)
        {
            // Basic validation
            if (state == null) {
                Debug.LogError("[MatchEventHandler] HandleActionResult called with null MatchState.");
                return;
            }

            // Route to specific handler based on outcome
            switch (result.Outcome)
            {
                case ActionResultOutcome.Success:           HandleActionSuccess(result, state); break;
                case ActionResultOutcome.Failure:           HandleActionFailure(result, state); break;
                case ActionResultOutcome.Intercepted:       HandleInterception(result, state); break;
                case ActionResultOutcome.Saved:             HandleSave(result, state); break;
                case ActionResultOutcome.Blocked:           HandleBlock(result, state); break;
                case ActionResultOutcome.Goal:              HandleGoalScored(result, state); break;
                case ActionResultOutcome.Miss:              HandleMiss(result, state); break;
                case ActionResultOutcome.FoulCommitted:     HandleFoul(result, state); break;
                case ActionResultOutcome.OutOfBounds:       HandleOutOfBounds(result, state); break; // Note: Usually called by Simulator directly
                case ActionResultOutcome.Turnover:          HandleTurnover(result, state); break;
                default: Debug.LogWarning($"[MatchEventHandler] Unhandled ActionResult Outcome: {result.Outcome}"); break;
            }
        }

        /// <summary>Handles successful actions like completed passes, shots taken, tackles won.</summary>
        private void HandleActionSuccess(ActionResult result, MatchState state)
        {
            SimPlayer primary = result.PrimaryPlayer; // Player performing the action
            SimPlayer secondary = result.SecondaryPlayer; // Player affected (optional)

            // Use Reason string for specific success types
            switch (result.Reason)
            {
                case "Pass Released":
                    if (primary != null) {
                        Log($"Pass released from {primary.BaseData?.FullName ?? "Unknown"} towards {secondary?.BaseData?.FullName ?? "target"}.", primary);
                        ResetPlayerActionState(primary, result.Outcome); // Passer's action complete
                    }
                    break;

                case "Tackle Won Ball":
                case "Tackle Successful (No Ball)":
                    if (primary != null && secondary != null) {
                        Log($"Tackle by {primary.BaseData?.FullName ?? "Unknown"} on {secondary.BaseData?.FullName ?? "Unknown"} successful!", primary);
                        // Ball made loose by ActionResolver
                        HandlePossessionChange(state, -1, true); // Ball is now contested
                        ResetPlayerActionState(primary, result.Outcome);
                        ResetPlayerActionState(secondary, result.Outcome);
                    }
                    break;

                case "Shot Taken":
                    if (primary != null) {
                        Log($"Shot taken by {primary.BaseData?.FullName ?? "Unknown"}.", primary);
                        IncrementStat(state, primary, stats => stats.ShotsTaken++);
                        ResetPlayerActionState(primary, result.Outcome);
                    }
                    break;

                case "Picked up loose ball":
                    // Logging and state change mostly handled where this reason is generated (CheckForLooseBallPickup)
                    if (primary != null) {
                        Log($"Player {primary.BaseData?.FullName ?? "Unknown"} picked up loose ball.", primary);
                        // Reset state handled there too
                    }
                    break;

                case "Penalty Shot Taken":
                     if (primary != null) {
                        Log($"Penalty shot taken by {primary.BaseData?.FullName ?? "Unknown"}.", primary);
                        IncrementStat(state, primary, stats => stats.ShotsTaken++);
                        // Penalty Awarded stat incremented on foul, Scored incremented on goal
                        ResetPlayerActionState(primary, result.Outcome);
                     }
                    break;

                default:
                     // Log generic success if reason not matched
                     if (primary != null) Log($"Action successful for {primary.BaseData?.FullName ?? "Unknown"}.", primary);
                     else Log($"Action successful.");
                     if (primary != null) ResetPlayerActionState(primary, result.Outcome);
                    break;
            }
        }

        /// <summary>Handles failed actions like tackles evaded or invalid actions.</summary>
        private void HandleActionFailure(ActionResult result, MatchState state)
        {
            SimPlayer primary = result.PrimaryPlayer;
            SimPlayer secondary = result.SecondaryPlayer;
            string reason = result.Reason ?? "Unknown Reason";

            if (reason == "Tackle Evaded" && primary != null) {
                 Log($"Tackle by {primary.BaseData?.FullName ?? "Unknown"} evaded by {secondary?.BaseData?.FullName ?? "opponent"}.", primary);
                 ResetPlayerActionState(primary, result.Outcome); // Tackler resets
                 // Target continues (AI handles)
            }
            else if (primary != null) {
                 Log($"{primary.BaseData?.FullName ?? "Unknown"}'s action failed: {reason}.", primary);
                 ResetPlayerActionState(primary, result.Outcome); // Reset player who failed

                 // Check if failure implies turnover (e.g., bad pass attempt setup failed)
                 if (reason.Contains("Pass Target") || reason.Contains("Pass Inaccurate")) { // Check reason string
                      IncrementStat(state, primary, stats => stats.Turnovers++);
                      HandlePossessionChange(state, -1, true); // Ball becomes contested/loose
                 }
            } else {
                 // Generic failure log if no primary player involved
                 Log($"Action failed: {reason}.");
                 // Assume possession becomes contested if action failed without a clear player
                 HandlePossessionChange(state, -1, true);
            }
        }

         /// <summary>Handles explicit turnovers like inaccurate passes.</summary>
         public override void HandleTurnover(ActionResult result, MatchState state)
         {
             SimPlayer player = result.PrimaryPlayer;
             string reason = result.Reason ?? "Unknown";

             if (player != null) {
                 Log($"Turnover by {player.BaseData?.FullName ?? "Unknown"}. Reason: {reason}", player);
                 IncrementStat(state, player, stats => stats.Turnovers++);
                 // Ball likely already made loose by ActionResolver
                 HandlePossessionChange(state, -1, true); // Ensure state reflects contested ball
                 ResetPlayerActionState(player, result.Outcome);
             } else {
                  Log($"Turnover occurred. Reason: {reason}");
                  HandlePossessionChange(state, -1, true);
             }
         }


        /// <summary>Handles a pass interception event.</summary>
        private void HandleInterception(ActionResult result, MatchState state)
        {
            SimPlayer interceptor = result.PrimaryPlayer;
            SimPlayer passer = result.SecondaryPlayer;

            // Safety checks
            if (interceptor?.BaseData == null) {
                Debug.LogError("[MatchEventHandler] Interception handled with null interceptor.");
                HandlePossessionChange(state, -1, true); // Fallback to contested
                return;
            }

            Log($"Pass from {passer?.BaseData?.FullName ?? "Unknown"} intercepted by {interceptor.BaseData.FullName}!", interceptor);

            // --- Stat Updates ---
            if (passer != null) IncrementStat(state, passer, stats => stats.Turnovers++);
            IncrementStat(state, interceptor, stats => stats.Interceptions++);

            // --- State Updates ---
            state.Ball.SetPossession(interceptor); // Give ball to interceptor
            HandlePossessionChange(state, interceptor.TeamSimId); // Update game phase

             // Reset involved players' states
             ResetPlayerActionState(interceptor, result.Outcome);
             if (passer != null) ResetPlayerActionState(passer, result.Outcome);
             if (state.Ball.IntendedTarget != null) ResetPlayerActionState(state.Ball.IntendedTarget, result.Outcome);
             state.Ball.ResetPassContext(); // Clear pass info from ball state
        }

         /// <summary>Handles a shot saved by the goalkeeper.</summary>
        private void HandleSave(ActionResult result, MatchState state)
        {
            SimPlayer gk = result.PrimaryPlayer;
            SimPlayer shooter = result.SecondaryPlayer;

            if (gk?.BaseData == null) {
                Debug.LogError("[MatchEventHandler] Save handled with null Goalkeeper.");
                // Ball might become loose or OOB depending on context - fallback to contested?
                HandlePossessionChange(state, -1, true);
                if (shooter != null) ResetPlayerActionState(shooter, result.Outcome);
                return;
            }

            Log($"Shot by {shooter?.BaseData?.FullName ?? "Unknown"} saved by {gk.BaseData.FullName}!", gk);

            // --- Stat Updates ---
            IncrementStat(state, gk, stats => stats.SavesMade++);
            if (shooter != null) IncrementStat(state, shooter, stats => stats.ShotsOnTarget++);

            // --- State Updates ---
            state.Ball.SetPossession(gk); // GK gains possession
            HandlePossessionChange(state, gk.TeamSimId);

             // Reset involved players
             ResetPlayerActionState(gk, result.Outcome);
             if (shooter != null) ResetPlayerActionState(shooter, result.Outcome);
            // Réinitialiser LastShooter en réappliquant la possession au gardien
            // SetPossession réinitialise LastShooter à null selon l'implémentation de SimBall
        }
        /// <summary>Handles a shot blocked by a field player.</summary>
        public void HandleBlock(ActionResult result, MatchState state)
        {
            SimPlayer blocker = result.PrimaryPlayer;
            SimPlayer shooter = result.SecondaryPlayer;
            Vector2 impactPos = result.ImpactPosition ?? blocker?.Position ?? state.Ball.Position; // Best guess impact

            // Conditional jump logic for block
            if (blocker != null && state != null && JumpDecisionUtils.ShouldJumpForBlock(blocker, state))
            {
                // Calculate vertical velocity based on Jumping attribute
                float jumpingValue = Mathf.Clamp(blocker.BaseData.Jumping, 0f, 100f);
                float verticalVelocity = Mathf.Lerp(
                    Core.SimConstants.MIN_JUMP_VERTICAL_VELOCITY,
                    Core.SimConstants.MAX_JUMP_VERTICAL_VELOCITY,
                    jumpingValue / 100f
                );
                // StartJump method doesn't take parameters - it uses the player reference from the constructor
                // and calculates velocity internally based on player attributes
                _jumpSimulator.StartJump();
            }

            Log($"Shot by {shooter?.BaseData?.FullName ?? "Unknown"} blocked by {blocker.BaseData.FullName}!", blocker);

            // --- Stat Updates ---
            IncrementStat(state, blocker, stats => stats.BlocksMade++);
    IncrementStat(state, blocker, stats => stats.BlocksMade++);
            // ShotTaken already counted on release. Blocked shot is NOT ShotOnTarget.

            // --- State Updates ---
             // Rebound direction away from shooter, with randomness
             Vector2 reboundDir = (impactPos - (shooter?.Position ?? impactPos)).normalized; // Direction away from shooter
             if(reboundDir == Vector2.zero) reboundDir = (Vector2.right * (blocker.TeamSimId == 0 ? -1f : 1f)); // Default rebound dir if overlapping
             Vector2 randomOffset = new Vector2((float)state.RandomGenerator.NextDouble() - 0.5f, (float)state.RandomGenerator.NextDouble() - 0.5f) * 0.8f;
             Vector2 finalReboundDir = (reboundDir + randomOffset).normalized;
             float reboundSpeed = ActionResolverConstants.SHOT_BASE_SPEED * (float)state.RandomGenerator.NextDouble() * (BLOCK_REBOUND_MAX_SPEED_FACTOR - BLOCK_REBOUND_MIN_SPEED_FACTOR) + BLOCK_REBOUND_MIN_SPEED_FACTOR;

             state.Ball.MakeLoose(impactPos, finalReboundDir * reboundSpeed, blocker.TeamSimId, blocker);
             HandlePossessionChange(state, -1, true); // Ball becomes contested

             // Reset players
             ResetPlayerActionState(blocker, result.Outcome);
             if (shooter != null) ResetPlayerActionState(shooter, result.Outcome);
             // Keep LastShooter for rebound potential? EventHandler decides based on outcome type?
             // For Block, maybe clear LastShooter to prevent immediate second shot logic? --> Cleared by MakeLoose implicitly now.
        }

        /// <summary>Handles a successful goal, checking 3D position.</summary>
        private void HandleGoalScored(ActionResult result, MatchState state)
        {
             SimPlayer scorer = result.PrimaryPlayer;
             if (scorer?.BaseData == null) { // Check BaseData too
                 Debug.LogError("[MatchEventHandler] Goal registered but scorer or scorer.BaseData is unknown! Score awarded generically.");
                // Award score based on ball crossing info if possible, otherwise guess?
                // This path indicates a deeper simulation issue. Forcing finish might be safer.
                LogEvent(state, "Goal registered with invalid scorer data. Simulation may be unstable."); // Use local LogEvent method
                state.CurrentPhase = GamePhase.Finished; // Abort simulation on critical error
                 return;
             }

             // Goal is confirmed by CheckGoalLineCrossing which now checks height.
             // No extra height check needed here unless adding goal-line technology logic.

             int scoringTeamId = scorer.TeamSimId;
             bool wasPenalty = (state.CurrentPhase == GamePhase.HomePenalty || state.CurrentPhase == GamePhase.AwayPenalty);

             // Update Score
             if (scoringTeamId == HOME_TEAM_ID) state.HomeScore++; else state.AwayScore++;

             // Stats Updates (same as before)
             IncrementStat(state, scorer, stats => stats.GoalsScored++);
             IncrementStat(state, scorer, stats => stats.ShotsOnTarget++);
             if (wasPenalty) { IncrementStat(state, scorer, stats => stats.PenaltiesScored++); }

             Log($"GOAL! Scored by {scorer.BaseData.FullName}. Score: {state.HomeTeamData?.Name ?? "Home"} {state.HomeScore} - {state.AwayScore} {state.AwayTeamData?.Name ?? "Away"}", scorer);

             // State Updates for Kickoff
             state.Ball.Stop();
             state.Ball.Position = _geometry.Center; // Use 3D center
             int kickoffTeamId = (scoringTeamId == HOME_TEAM_ID) ? AWAY_TEAM_ID : HOME_TEAM_ID;
             state.PossessionTeamId = kickoffTeamId;
             TransitionToPhase(state, GamePhase.PreKickOff);

             foreach(var p in state.PlayersOnCourt.ToList()) {
                 if (p != null && !p.IsSuspended()) ResetPlayerActionState(p, result.Outcome);
             }
        }

         /// <summary>Handles a shot that misses the goal frame (may lead to OutOfBounds).</summary>
         private void HandleMiss(ActionResult result, MatchState state)
         {
             SimPlayer shooter = result.PrimaryPlayer;
             // ShotTaken already counted. Miss is NOT ShotOnTarget.
             if (shooter?.BaseData != null) { // Null check
                  Log($"Shot by {shooter.BaseData.FullName} missed target.", shooter);
                  ResetPlayerActionState(shooter, result.Outcome);
             } else { Log($"Shot missed target."); }

             state.Ball.SetLastShooter(null); // Clear shooter context
             // The ball continues moving until CheckPassiveEvents detects OutOfBounds.
             // If it hits post and stays in, CheckReactiveEvents(CheckForLooseBallPickup) handles it.
         }

         /// <summary>Handles a foul committed by a player.</summary>
        private void HandleFoul(ActionResult result, MatchState state)
        {
             SimPlayer committer = result.PrimaryPlayer;
             SimPlayer victim = result.SecondaryPlayer;
             FoulSeverity severity = result.FoulSeverity;

             // Validate involved players
             if (committer?.BaseData == null || victim?.BaseData == null) {
                 Debug.LogError($"[MatchEventHandler] Foul event missing valid committer ({committer?.GetPlayerId()}) or victim ({victim?.GetPlayerId()})!");
                 // Fallback: Contested ball at foul location? Or abort?
                 state.Ball.MakeLoose(result.ImpactPosition ?? state.Ball.Position, Vector2.zero, -1);
                 HandlePossessionChange(state, -1, true);
                 return;
             }

             Log($"Foul by {committer.BaseData.FullName} on {victim.BaseData.FullName} ({severity}).", committer);

             // --- Stat Updates ---
             IncrementStat(state, committer, stats => stats.FoulsCommitted++);

             // --- Apply Penalties (Suspension/Card) ---
             bool applySuspension = severity == FoulSeverity.TwoMinuteSuspension || severity == FoulSeverity.RedCard;
             if (applySuspension)
             {
                 // Increment suspension/card stats
                 if (severity == FoulSeverity.TwoMinuteSuspension) IncrementStat(state, committer, stats => stats.TwoMinuteSuspensions++);
                 if (severity == FoulSeverity.RedCard) IncrementStat(state, committer, stats => stats.RedCards++);

                 // Apply suspension state to player
                 committer.SuspensionTimer = (severity == FoulSeverity.RedCard) ? RED_CARD_SUSPENSION_TIME : DEFAULT_SUSPENSION_TIME; // Use constants

                 committer.CurrentAction = PlayerAction.Idle;
                 committer.IsOnCourt = false; // Remove from court visually/logically
                 // Remove player from the correct on-court list
                 var teamList = state.GetTeamOnCourt(committer.TeamSimId);
                 if (teamList != null && teamList.Contains(committer)) {
                     teamList.Remove(committer);
                 } else {
                      Debug.LogWarning($"[MatchEventHandler] Suspended player {committer.GetPlayerId()} not found in team's on-court list.");
                 }
                 committer.Position = new Vector2(-100, -100); // Move off pitch
                 committer.Velocity = Vector2.zero;
                 if(committer.HasBall) committer.HasBall = false; // Drop ball if held
                 Log($"{committer.BaseData.FullName} receives {severity}.", committer);
             }

             // --- Ball State ---
             // Ball becomes dead at foul location
             state.Ball.Stop();
             if (state.Ball.Holder != null) {
                 if(state.Ball.Holder.HasBall) state.Ball.Holder.HasBall = false; // Ensure holder flag reset
                 state.Ball.SetHolder(null);
             }
             Vector2 foulLocation = result.ImpactPosition ?? victim.Position; // Use impact pos if available
             state.Ball.Position = foulLocation;

             // --- Possession & Restart Phase ---
             int victimTeamId = victim.TeamSimId;
             HandlePossessionChange(state, victimTeamId); // Award possession conceptually

             // Determine restart type (Penalty or Free Throw)
             bool isPenaltyAreaFoul = _geometry.IsInGoalArea(foulLocation, victimTeamId == 1); // Is foul in defending goal area?
             bool deniedClearChance = severity == FoulSeverity.RedCard || severity == FoulSeverity.TwoMinuteSuspension; // Criteria for potential penalty upgrade

             if (isPenaltyAreaFoul && deniedClearChance && severity != FoulSeverity.OffensiveFoul) // Defensive foul in area denying chance = Penalty
             {
                 IncrementStat(state, victimTeamId, stats => stats.PenaltiesAwarded++); // Award penalty stat
                 GamePhase nextPhase = (victimTeamId == HOME_TEAM_ID) ? GamePhase.HomePenalty : GamePhase.AwayPenalty;
                 Log($"7m Penalty awarded to Team {victimTeamId}.", teamId: victimTeamId);
                 TransitionToPhase(state, nextPhase); // Set penalty phase (triggers setup)
             } else {
                 // Free Throw: Adjust position if inside 9m line
                 Vector2 opponentGoalCenter = victimTeamId == HOME_TEAM_ID ? _geometry.AwayGoalCenter3D : _geometry.HomeGoalCenter3D;
                 if (Vector2.Distance(foulLocation, opponentGoalCenter) < _geometry.FreeThrowLineRadius) {
                     Vector2 directionFromGoal = (foulLocation - opponentGoalCenter);
                     // Handle case where foul is exactly at goal center
                     if (directionFromGoal.sqrMagnitude < MIN_DISTANCE_CHECK_SQ) directionFromGoal = Vector2.right * (victimTeamId == 0 ? -1f : 1f);
                     state.Ball.Position = opponentGoalCenter + directionFromGoal.normalized * _geometry.FreeThrowLineRadius;
                 } // Else: ball position remains at foulLocation

                 GamePhase nextPhase = (victimTeamId == HOME_TEAM_ID) ? GamePhase.HomeSetPiece : GamePhase.AwaySetPiece;
                 Log($"Free throw awarded to Team {victimTeamId}.", teamId: victimTeamId);
                 TransitionToPhase(state, nextPhase); // Set set piece phase (triggers setup)
             }

             // Reset players involved after determining restart
             ResetPlayerActionState(committer, result.Outcome); // May already be suspended
             ResetPlayerActionState(victim, result.Outcome);
        }

        /// <summary>Handles the ball going out of bounds. Now accepts optional 3D intersection point.</summary>
        public override void HandleOutOfBounds(ActionResult result, MatchState state, Vector3? intersectionPoint3D = null)
        {
             if (state?.Ball == null) { return; }

             int lastTouchTeamId = state.Ball.LastTouchedByTeamId;
             int receivingTeamId;

             // Determine receiving team
             if (lastTouchTeamId == HOME_TEAM_ID || lastTouchTeamId == AWAY_TEAM_ID) { receivingTeamId = (lastTouchTeamId == HOME_TEAM_ID) ? AWAY_TEAM_ID : HOME_TEAM_ID; }
             else { receivingTeamId = (state.Ball.Position.x < _geometry.Center.x) ? AWAY_TEAM_ID : HOME_TEAM_ID; Log("Unknown last touch for OOB.", null, null); }

             // Determine restart type and position using the 3D intersection point if available
             Vector3 restartPosition3D = intersectionPoint3D ?? state.Ball.Position; // Use intersection if available
             RestartInfo restart = DetermineRestartTypeAndPosition(state, restartPosition3D, lastTouchTeamId, receivingTeamId);
             receivingTeamId = restart.ReceivingTeamId;

             Log($"Ball out of bounds ({restart.Type}). Possession to Team {receivingTeamId}.", teamId: receivingTeamId);

             // State Updates
             HandlePossessionChange(state, receivingTeamId);
             state.Ball.Stop();
             state.Ball.Position = restart.Position; // Set 3D ball restart position

             SimPlayer thrower = FindThrower(state, receivingTeamId, restart.Position, restart.IsGoalThrow);

             if (thrower != null) {
                state.Ball.SetPossession(thrower); // Handles 3D placement internally
                ResetPlayerActionState(thrower);
                GamePhase nextPhase = restart.IsGoalThrow ? ((receivingTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack) : ((receivingTeamId == 0) ? GamePhase.HomeSetPiece : GamePhase.AwaySetPiece);
                TransitionToPhase(state, nextPhase);
             } else {
                 Debug.LogWarning($"No thrower found for {restart.Type} for Team {receivingTeamId} at {state.Ball.Position}");
                 state.Ball.MakeLoose(state.Ball.Position, Vector2.zero, receivingTeamId);
                 HandlePossessionChange(state, -1, true); // Revert to contested
             }
        }
        

        /// <summary>Helper struct for OOB restart info.</summary>
        private struct RestartInfo { public string Type; public Vector3 Position; public bool IsGoalThrow; public int ReceivingTeamId; }

        /// <summary>Determines the type and position of the restart after OOB.</summary>
        private RestartInfo DetermineRestartTypeAndPosition(MatchState state, Vector3 oobPos3D, int lastTouchTeamId, int initialReceivingTeamId)
        {
            string restartType = "Throw-in"; // Default
            Vector3 restartPos3D = oobPos3D;
            bool isGoalThrow = false;
            int finalReceivingTeamId = initialReceivingTeamId;

            // Extract 2D coordinates for boundary checks (X,Z plane in 3D space)
            float oobPosX = oobPos3D.x;
            float oobPosZ = oobPos3D.z; // Z in 3D corresponds to Y in 2D

            bool wentOutHomeGoalLine = oobPosX <= OOB_RESTART_BUFFER;
            bool wentOutAwayGoalLine = oobPosX >= _geometry.PitchLength - OOB_RESTART_BUFFER;

            if (wentOutHomeGoalLine) { // Out over Home end line (X=0)
                if (lastTouchTeamId == 1) { // Away attacker touched last -> Goal Throw for Home
                    isGoalThrow = true; restartType = "Goal throw"; finalReceivingTeamId = Core.SimConstants.HOME_TEAM_ID;
                } // else if (lastTouchTeamId == 0) { /* Corner Throw logic - not implemented */ }
            } else if (wentOutAwayGoalLine) { // Out over Away end line (X=Length)
                 if (lastTouchTeamId == 0) { // Home attacker touched last -> Goal Throw for Away
                     isGoalThrow = true; restartType = "Goal throw"; finalReceivingTeamId = Core.SimConstants.AWAY_TEAM_ID;
                 } // else if (lastTouchTeamId == 1) { /* Corner Throw logic - not implemented */ }
            }

            // Adjust restart position
            if (isGoalThrow) {
                // Position ball just outside goal area for GK
                SimPlayer gk = state.GetGoalkeeper(finalReceivingTeamId);
                Vector3 goalCenter = (finalReceivingTeamId == 0) ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
                
                // Create direction vector in 3D space (X,Z plane)
                Vector3 dir3D;
                if(gk != null) {
                    // Convert 2D player position to 3D direction vector
                    Vector3 gkPos3D = new(gk.Position.x, Core.SimConstants.BALL_DEFAULT_HEIGHT, gk.Position.y);
                    dir3D = new(gkPos3D.x - goalCenter.x, 0f, gkPos3D.z - goalCenter.z);
                    
                    // Check if direction is near zero
                    if(dir3D.sqrMagnitude < Core.SimConstants.VELOCITY_NEAR_ZERO_SQ) {
                        dir3D = new Vector3((finalReceivingTeamId == Core.SimConstants.HOME_TEAM_ID ? 1f : -1f), 0f, 0f); // Default X direction
                    }
                } else {
                    dir3D = new Vector3((finalReceivingTeamId == Core.SimConstants.HOME_TEAM_ID ? 1f : -1f), 0f, 0f); // Default X direction
                }
                
                // Set restart position in 3D
                restartPos3D = goalCenter + dir3D.normalized * GOAL_THROW_RESTART_DIST;
                // Ensure proper ball height
                restartPos3D.y = Core.SimConstants.BALL_DEFAULT_HEIGHT;
            } else { // Sideline throw-in (or corner placeholder)
                 // Clamp Z position (3D equivalent of 2D Y) to stay within bounds
                 restartPos3D.z = Mathf.Clamp(restartPos3D.z, OOB_RESTART_BUFFER, _geometry.PitchWidth - OOB_RESTART_BUFFER);
                 
                 // Handle sideline boundaries in 3D
                 if (oobPosZ <= OOB_RESTART_BUFFER) restartPos3D.z = OOB_RESTART_BUFFER;
                 else if (oobPosZ >= _geometry.PitchWidth - OOB_RESTART_BUFFER) restartPos3D.z = _geometry.PitchWidth - OOB_RESTART_BUFFER;
                 
                 // Keep original X unless it was an endline cross handled above
                 if (!wentOutHomeGoalLine && !wentOutAwayGoalLine) {
                     // Clamp X to boundary if needed (should already be close)
                     restartPos3D.x = Mathf.Clamp(restartPos3D.x, OOB_RESTART_BUFFER, _geometry.PitchLength - OOB_RESTART_BUFFER);
                 }
                 
                 // Ensure proper ball height for throw-in
                 restartPos3D.y = Core.SimConstants.BALL_DEFAULT_HEIGHT;
            }
            
            // Final clamp just in case (X and Z only, preserve Y height)
            restartPos3D.x = Mathf.Clamp(restartPos3D.x, 0f, _geometry.PitchLength);
            restartPos3D.z = Mathf.Clamp(restartPos3D.z, 0f, _geometry.PitchWidth);
            // Ensure proper ball height is maintained
            restartPos3D.y = Core.SimConstants.BALL_DEFAULT_HEIGHT;

            return new RestartInfo { Type = restartType, Position = restartPos3D, IsGoalThrow = isGoalThrow, ReceivingTeamId = finalReceivingTeamId };
        }

        /// <summary>Finds the appropriate player to take a restart (throw-in, goal throw).</summary>
        private SimPlayer FindThrower(MatchState state, int receivingTeamId, Vector3 restartPos3D, bool isGoalThrow)
        {
             if (isGoalThrow) {
                 return state.GetGoalkeeper(receivingTeamId); // GK always takes goal throw
             } else {
                 // Convert 3D restart position to 2D for player distance comparison
                 Vector2 restartPos2D = new Vector2(restartPos3D.x, restartPos3D.z);
                 
                 // Find closest non-GK field player for throw-in/corner
                 return state.GetTeamOnCourt(receivingTeamId)?
                               .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                               .OrderBy(p => Vector2.Distance(p.Position, restartPos2D))
                               .FirstOrDefault();
             }
        }


        /// <summary>
        /// Central helper to manage possession changes and trigger phase transitions.
        /// Ensures correct phase is set based on whether ball is loose or held.
        /// </summary>
        /// <param name="state">Current match state.</param>
        /// <param name="newPossessionTeamId">The new team in possession (-1 for contested).</param>
        /// <param name="ballIsLoose">Indicates if the ball is currently loose.</param>
        public override void HandlePossessionChange(MatchState state, int newPossessionTeamId, bool ballIsLoose = false)
        {
            if (state == null) return;

            int previousPossessionTeamId = state?.PossessionTeamId ?? -1;
            bool possessionTrulyChanged = previousPossessionTeamId != newPossessionTeamId && previousPossessionTeamId != -1 && newPossessionTeamId != -1;

            state.PossessionTeamId = newPossessionTeamId;

            // --- Determine Appropriate Phase ---
            GamePhase nextPhase = state.CurrentPhase; // Start with current phase

            if (ballIsLoose || newPossessionTeamId == -1) {
                // If ball becomes loose, always transition to ContestedBall
                 nextPhase = GamePhase.ContestedBall;
                 Log("Possession contested (Loose Ball).", (SimPlayer)null);
            } else if (possessionTrulyChanged) {
                 // If possession changed between teams (and ball not loose), start transition
                 nextPhase = (newPossessionTeamId == 0) ? GamePhase.TransitionToHomeAttack : GamePhase.TransitionToAwayAttack;
                 Log($"Possession changes to Team {newPossessionTeamId}.", (SimPlayer)null, newPossessionTeamId);
            } else if (newPossessionTeamId != -1 && previousPossessionTeamId == -1) {
                 // Gained possession from a contested state
                 nextPhase = (newPossessionTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack;
                 Log($"Team {newPossessionTeamId} gained possession.", (SimPlayer)null, newPossessionTeamId);
            }
            // Else: Possession stays with the same team (e.g., after own throw-in), maintain current Attack phase

            // --- Ensure Ball Holder Status Matches ---
            if (ballIsLoose || newPossessionTeamId == -1) {
                 if(state.Ball.Holder != null) { state.Ball.Holder.HasBall = false; state.Ball.SetHolder(null); }
                 // Force contested phase if somehow not set above
                 if (nextPhase != GamePhase.ContestedBall) { nextPhase = GamePhase.ContestedBall; }
            } else if (newPossessionTeamId != -1 && state.Ball.Holder != null) {
                 // If possession gained/kept and ball held, ensure we are in an appropriate attack phase
                 // (This corrects cases where possession might be gained during a transition/contested phase check)
                 if (nextPhase == GamePhase.ContestedBall || nextPhase == GamePhase.TransitionToHomeAttack || nextPhase == GamePhase.TransitionToAwayAttack) {
                      nextPhase = (newPossessionTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack;
                 }
            }

            // Transition to the determined phase
            TransitionToPhase(state, nextPhase);
        }

        /// <summary>Simplified logging using the match state's event system.</summary>
        private void Log(string message, SimPlayer playerContext = null, int? teamId = null)
        {
            // Only log to Unity console; event-list logging is handled by LogEvent(MatchState,...)
            Debug.Log(message);
        }

        /// <summary>Logs an event to the match state's event list.</summary>
        public override void LogEvent(MatchState state, string description, int? teamId = null, int? playerId = null)
        {
            if (state?.MatchEvents == null) return;
            try
            {
                state.MatchEvents.Add(new MatchEvent(state.MatchTimeSeconds, description, teamId, playerId));
                // Optional debug logging
                Debug.Log($"[Sim {(int)state.MatchTimeSeconds}s] {description}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MatchEventHandler] Failed to add MatchEvent: {ex.Message}");
            }
        }

        /// <summary>Helper to transition phase within the event handler context.</summary>
        public override void TransitionToPhase(MatchState state, GamePhase newPhase) {
             // Reuse MatchSimulator's transition logic if accessible, or replicate:
             if (state == null || state.CurrentPhase == newPhase) return;
             state.CurrentPhase = newPhase;
             if (newPhase == GamePhase.PreKickOff || newPhase == GamePhase.HomeSetPiece ||
                 newPhase == GamePhase.AwaySetPiece || newPhase == GamePhase.HomePenalty ||
                 newPhase == GamePhase.AwayPenalty || newPhase == GamePhase.HalfTime) {
                  // Setting _setupPending is tricky here as it's private to MatchSimulator.
                  // This indicates tighter coupling or need for a shared state/flag.
                  // For now, assume MatchSimulator's HandlePhaseTransitions will catch this needed setup.
                  // A better approach might involve returning a flag or using an event bus.
                  // Log("Phase requires setup: " + newPhase); // Optional internal log
             }
        }

        /// <summary>Helper to get team ID from state.</summary>
        private int? GetTeamIdFromSimId(MatchState state, int simId)
        {
            if (state == null) return null;
            // Use class constants for team IDs
            if (simId == HOME_TEAM_ID) return state.HomeTeamData?.TeamID;
            if (simId == AWAY_TEAM_ID) return state.AwayTeamData?.TeamID;
            return null;
        }
    }
}