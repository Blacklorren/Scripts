using UnityEngine;
using HandballManager.Data;
using HandballManager.Simulation.Utils;
using System;
using System.Linq;
using HandballManager.Core;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Events.Interfaces;

namespace HandballManager.Simulation.Events.Handlers
{
    public partial class DefaultMatchEventHandler : IMatchEventHandler
    {
        private readonly IGeometryProvider _geometry;
        private IPhaseManager _phaseManager; // Needs PhaseManager for transitions
        private PassivePlayManager _passivePlayManager;

        // Constants
        private const float DEFAULT_SUSPENSION_TIME = 120f;
        private const float RED_CARD_SUSPENSION_TIME = float.MaxValue;
        private const float BLOCK_REBOUND_MIN_SPEED_FACTOR = 0.2f;
        private const float BLOCK_REBOUND_MAX_SPEED_FACTOR = 0.5f;
        private const float OOB_RESTART_BUFFER = 0.1f;
        private const float GOAL_THROW_RESTART_DIST = 3.0f; // 3 meters for handball goal throw restart
        private const float MIN_DISTANCE_CHECK_SQ = 0.01f;

        public DefaultMatchEventHandler(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            // PhaseManager will be injected via setter to avoid circular dependency in constructor
        }

        // Setter injection for PhaseManager
        public void SetPhaseManager(IPhaseManager phaseManager)
        {
            _phaseManager = phaseManager ?? throw new ArgumentNullException(nameof(phaseManager));
        }

        // Setter injection for PassivePlayManager
        public void SetPassivePlayManager(PassivePlayManager passivePlayManager)
        {
            _passivePlayManager = passivePlayManager;
        }


        public virtual void HandleActionResult(ActionResult result, MatchState state)
        {
            // Passive play turnovers are now handled via the SimBall.OnPassCompletedBetweenTeammates event in MatchSimulator.
            // No direct check needed here.

            // --- Logic copied from MatchSimulator ---
             if (state == null) { Debug.LogError("[DefaultMatchEventHandler] HandleActionResult called with null MatchState."); return; }
             if (_phaseManager == null) { Debug.LogError("[DefaultMatchEventHandler] PhaseManager is not set."); return; } // Check if injected

             switch (result.Outcome) {
                 case ActionResultOutcome.Success: HandleActionSuccess(result, state); break;
                 case ActionResultOutcome.Failure: HandleActionFailure(result, state); break;
                 case ActionResultOutcome.Intercepted: HandleInterception(result, state); break;
                 case ActionResultOutcome.Saved: HandleSave(result, state); break;
                 case ActionResultOutcome.Blocked: HandleBlock(result, state); break;
                 case ActionResultOutcome.BlockedAndCaught:
                 case ActionResultOutcome.BlockedToTeammate:
                 case ActionResultOutcome.BlockedOutOfBounds:
                 case ActionResultOutcome.Deflected:
                     HandleEnhancedBlock(result, state); break;
                 case ActionResultOutcome.Goal: HandleGoalScored(result, state); break;
                 case ActionResultOutcome.Miss: HandleMiss(result, state); break;
                 case ActionResultOutcome.FoulCommitted: HandleFoul(result, state); break;
                 case ActionResultOutcome.OutOfBounds: HandleOutOfBounds(result, state); break;
                 case ActionResultOutcome.Turnover: HandleTurnover(result, state); break;
                 default: Debug.LogWarning($"[DefaultMatchEventHandler] Unhandled ActionResult Outcome: {result.Outcome}"); break;
             }
        }

        public void ResetPlayerActionState(SimPlayer player, ActionResultOutcome outcomeContext = ActionResultOutcome.Success)
        {
             // --- Logic copied from MatchSimulator ---
              if (player == null) return;
              if (player.IsSuspended()) return;

              // Persist receiving state unless pass was intercepted or outcome dictates reset
              // This logic might be better handled by AI re-evaluating
              // if (player.CurrentAction == PlayerAction.ReceivingPass && outcomeContext != ActionResultOutcome.Intercepted) { }

              player.CurrentAction = PlayerAction.Idle;
              player.ActionTimer = 0f;
              player.TargetPlayer = null;
              // Optionally reset TargetPosition: player.TargetPosition = player.Position;
        }

        public virtual void HandlePossessionChange(MatchState state, int newPossessionTeamId, bool ballIsLoose = false)
        {
            if (state == null)
            {
                Debug.LogError("[DefaultMatchEventHandler] HandlePossessionChange called with null MatchState.");
                return;
            }
            if (_phaseManager == null)
            {
                Debug.LogError("[DefaultMatchEventHandler] PhaseManager is not set for HandlePossessionChange.");
                return;
            }

            // Reset pass flags and passive play
            foreach (var player in state.AllPlayers.Values)
            {
                if (player != null)
                    player.ReceivedPassRecently = false;
            }
            _passivePlayManager?.ResetPassivePlay();

            // Contest: loose ball or no possession
            if (ballIsLoose || newPossessionTeamId == -1)
            {
                state.PossessionTeamId = -1;
                if (state.Ball?.Holder != null)
                {
                    state.Ball.Holder.HasBall = false;
                    state.Ball.SetHolder(null);
                }
                TransitionToPhase(state, GamePhase.ContestedBall);
                return;
            }

            int previous = state.PossessionTeamId;
            state.PossessionTeamId = newPossessionTeamId;

            // Gained from contested
            if (previous == -1)
            {
                var nextPhase = (newPossessionTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack;
                TransitionToPhase(state, nextPhase);
                return;
            }

            // Changed possession during play
            if (previous != newPossessionTeamId)
            {
                var transitionPhase = (newPossessionTeamId == 0) ? GamePhase.TransitionToHomeAttack : GamePhase.TransitionToAwayAttack;
                TransitionToPhase(state, transitionPhase);
                return;
            }

            // No phase change if possession remains the same
        }

        public virtual void LogEvent(MatchState state, string description, int? teamId = null, int? playerId = null)
        {
            // --- Logic copied from MatchSimulator ---
            if (state?.MatchEvents == null) return;
            try {
                // Optional: Add timestamp formatting if needed, MatchEvent constructor handles time
                state.MatchEvents.Add(new MatchEvent(state.MatchTimeSeconds, description, teamId, playerId));
                // Debug.Log($"[Sim {(int)state.MatchTimeSeconds}s] {description}"); // Optional console log
            } catch (Exception ex) { Debug.LogWarning($"[DefaultMatchEventHandler] Failed to add MatchEvent: {ex.Message}"); }
        }

        public void HandleStepError(MatchState state, string stepName, Exception ex)
        {
            // --- Logic copied from MatchSimulator ---
             float currentTime = state?.MatchTimeSeconds ?? -1f;
             Debug.LogError($"[DefaultMatchEventHandler] Error during '{stepName}' at Time {currentTime:F1}s: {ex?.Message ?? "Unknown Error"}\n{ex?.StackTrace ?? "No stack trace"}");
             if (state != null) {
                 // Force the simulation to end immediately
                 TransitionToPhase(state, GamePhase.Finished); // Use PhaseManager
             }
             LogEvent(state, $"CRITICAL ERROR during {stepName} - Simulation aborted. Error: {ex?.Message ?? "Unknown Error"}");
        }

        // --- Specific Event Handler Implementations (Copied & Adapted) ---

        private void HandleActionSuccess(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer primary = result.PrimaryPlayer;
            SimPlayer secondary = result.SecondaryPlayer;
            switch (result.Reason) {
                case "Pass Released":
            if (primary != null)
            {
                LogEvent(state, $"Pass released from {primary.BaseData?.FullName ?? "Unknown"} towards {secondary?.BaseData?.FullName ?? "target"}.", primary.GetTeamId(), primary.GetPlayerId());
                ResetPlayerActionState(primary, result.Outcome);
                // Informer PassivePlayManager d'une passe
                _passivePlayManager?.OnPassMade(state.PossessionTeamId);
                // Set ReceivedPassRecently for secondary (recipient), reset for all others on the team
                if (secondary != null)
                {
                    foreach (var player in state.GetTeamOnCourt(primary.TeamSimId))
                    {
                        if (player != null)
                            player.ReceivedPassRecently = (player == secondary);
                    }
                }
            }
            break;
                case "Tackle Won Ball": case "Tackle Successful (No Ball)": if (primary != null && secondary != null) { LogEvent(state, $"Tackle by {primary.BaseData?.FullName ?? "Unknown"} on {secondary.BaseData?.FullName ?? "Unknown"} successful!", primary.GetTeamId(), primary.GetPlayerId()); HandlePossessionChange(state, -1, true); ResetPlayerActionState(primary, result.Outcome); ResetPlayerActionState(secondary, result.Outcome); } break;
                case "Shot Taken": if (primary != null) { LogEvent(state, $"Shot taken by {primary.BaseData?.FullName ?? "Unknown"}.", primary.GetTeamId(), primary.GetPlayerId()); IncrementStat(state, primary, stats => stats.ShotsTaken++); ResetPlayerActionState(primary, result.Outcome); } break;
                case "Picked up loose ball": if (primary != null) { LogEvent(state, $"Player {primary.BaseData?.FullName ?? "Unknown"} picked up loose ball.", primary.GetTeamId(), primary.GetPlayerId()); } break; // State handled elsewhere
                case "Penalty Shot Taken": if (primary != null) { LogEvent(state, $"Penalty shot taken by {primary.BaseData?.FullName ?? "Unknown"}.", primary.GetTeamId(), primary.GetPlayerId()); IncrementStat(state, primary, stats => stats.ShotsTaken++); ResetPlayerActionState(primary, result.Outcome); } break;
                default: if (primary != null) LogEvent(state, $"Action successful for {primary.BaseData?.FullName ?? "Unknown"}.", primary.GetTeamId(), primary.GetPlayerId()); else LogEvent(state, $"Action successful."); if (primary != null) ResetPlayerActionState(primary, result.Outcome); break;
            }
        }

        private void HandleActionFailure(ActionResult result, MatchState state)
        {
            // --- Logic copied from MatchSimulator ---
            SimPlayer primary = result.PrimaryPlayer;
            SimPlayer secondary = result.SecondaryPlayer;
            string reason = result.Reason ?? "Unknown Reason";
            if (reason == "Tackle Evaded" && primary != null) { LogEvent(state, $"Tackle by {primary.BaseData?.FullName ?? "Unknown"} evaded by {secondary?.BaseData?.FullName ?? "opponent"}.", primary.GetTeamId(), primary.GetPlayerId()); ResetPlayerActionState(primary, result.Outcome); }
            else if (primary != null) { LogEvent(state, $"{primary.BaseData?.FullName ?? "Unknown"}'s action failed: {reason}.", primary.GetTeamId(), primary.GetPlayerId()); ResetPlayerActionState(primary, result.Outcome); if (reason.Contains("Pass Target") || reason.Contains("Pass Inaccurate")) { IncrementStat(state, primary, stats => stats.Turnovers++); HandlePossessionChange(state, -1, true); } }
            else { LogEvent(state, $"Action failed: {reason}."); HandlePossessionChange(state, -1, true); }
        }

        public virtual void HandleTurnover(ActionResult result, MatchState state)
        {
            // --- Suivi des stats individuelles ---
            if (result.PrimaryPlayer != null)
            {
                int turnoverId = result.PrimaryPlayer.GetPlayerId();
                if (!state.PlayerStats.ContainsKey(turnoverId))
                    state.PlayerStats[turnoverId] = new PlayerMatchStats();
                state.PlayerStats[turnoverId].Turnovers++;
                state.PlayerStats[turnoverId].Participated = true;
            }
            // Réinitialiser le jeu passif sur turnover explicite
            _passivePlayManager?.ResetPassivePlay();

            // --- Logic copied from MatchSimulator ---
            SimPlayer player = result.PrimaryPlayer;
            string reason = result.Reason ?? "Unknown";
            if (player != null) { LogEvent(state, $"Turnover by {player.BaseData?.FullName ?? "Unknown"}. Reason: {reason}", player.GetTeamId(), player.GetPlayerId()); IncrementStat(state, player, stats => stats.Turnovers++); HandlePossessionChange(state, -1, true); ResetPlayerActionState(player, result.Outcome); }
            else { LogEvent(state, $"Turnover occurred. Reason: {reason}"); HandlePossessionChange(state, -1, true); }
        }

        private void HandleInterception(ActionResult result, MatchState state)
        {
            // --- Suivi des stats individuelles ---
            if (result.SecondaryPlayer != null)
            {
                int interceptorId = result.SecondaryPlayer.GetPlayerId();
                if (!state.PlayerStats.ContainsKey(interceptorId))
                    state.PlayerStats[interceptorId] = new PlayerMatchStats();
                state.PlayerStats[interceptorId].Interceptions++;
                state.PlayerStats[interceptorId].Participated = true;
            }
            // --- Logic copied from MatchSimulator ---
             SimPlayer interceptor = result.PrimaryPlayer; SimPlayer passer = result.SecondaryPlayer;
             if (interceptor?.BaseData == null) { Debug.LogError("[DefaultMatchEventHandler] Interception handled with null interceptor."); HandlePossessionChange(state, -1, true); return; }
             LogEvent(state, $"Pass from {passer?.BaseData?.FullName ?? "Unknown"} intercepted by {interceptor.BaseData.FullName}!", interceptor.GetTeamId(), interceptor.GetPlayerId());
             if (passer != null) IncrementStat(state, passer, stats => stats.Turnovers++);
             IncrementStat(state, interceptor, stats => stats.Interceptions++);
             state.Ball.SetPossession(interceptor);
             HandlePossessionChange(state, interceptor.TeamSimId);
             ResetPlayerActionState(interceptor, result.Outcome);
             if (passer != null) ResetPlayerActionState(passer, result.Outcome);
             if (state.Ball.IntendedTarget != null) ResetPlayerActionState(state.Ball.IntendedTarget, result.Outcome);
             state.Ball.ResetPassContext();
        }

        private void HandleSave(ActionResult result, MatchState state)
        {
            // --- Suivi des stats individuelles ---
            if (result.SecondaryPlayer != null)
            {
                int gkId = result.SecondaryPlayer.GetPlayerId();
                if (!state.PlayerStats.ContainsKey(gkId))
                    state.PlayerStats[gkId] = new PlayerMatchStats();
                state.PlayerStats[gkId].SavesMade++;
                state.PlayerStats[gkId].Participated = true;
            }
            if (result.PrimaryPlayer != null)
            {
                int shooterId = result.PrimaryPlayer.GetPlayerId();
                if (!state.PlayerStats.ContainsKey(shooterId))
                    state.PlayerStats[shooterId] = new PlayerMatchStats();
                state.PlayerStats[shooterId].ShotsTaken++;
                state.PlayerStats[shooterId].ShotsOnTarget++;
                state.PlayerStats[shooterId].Participated = true;
            }
            // --- Logic copied from MatchSimulator ---
            SimPlayer gk = result.PrimaryPlayer; SimPlayer shooter = result.SecondaryPlayer;
            if (gk?.BaseData == null) { Debug.LogError("[DefaultMatchEventHandler] Save handled with null Goalkeeper."); HandlePossessionChange(state, -1, true); if (shooter != null) ResetPlayerActionState(shooter, result.Outcome); return; }
            LogEvent(state, $"Shot by {shooter?.BaseData?.FullName ?? "Unknown"} saved by {gk.BaseData.FullName}!", gk.GetTeamId(), gk.GetPlayerId());
            IncrementStat(state, gk, stats => stats.SavesMade++);
            if (shooter != null) IncrementStat(state, shooter, stats => stats.ShotsOnTarget++);
            state.Ball.SetPossession(gk);
            // Ball is not loose after a GK save if the GK controls it
            state.Ball.IsLooseBallSituation = false;
            HandlePossessionChange(state, gk.TeamSimId);
            ResetPlayerActionState(gk, result.Outcome);
            if (shooter != null) ResetPlayerActionState(shooter, result.Outcome);
            state.Ball.SetLastShooter(null);
        }

        private void HandleBlock(ActionResult result, MatchState state)
        {
            // --- Suivi des stats individuelles ---
            if (result.SecondaryPlayer != null)
            {
                int blockerId = result.SecondaryPlayer.GetPlayerId();
                if (!state.PlayerStats.ContainsKey(blockerId))
                    state.PlayerStats[blockerId] = new PlayerMatchStats();
                state.PlayerStats[blockerId].BlocksMade++;
                state.PlayerStats[blockerId].Participated = true;
            }
            // --- Logic copied from MatchSimulator ---
            SimPlayer blocker = result.PrimaryPlayer; SimPlayer shooter = result.SecondaryPlayer;
            Vector2 impactPos = result.ImpactPosition ?? blocker?.Position ?? CoordinateUtils.To2DGround(state.Ball.Position);
            if (blocker?.BaseData == null) { Debug.LogError("[DefaultMatchEventHandler] Block handled with null Blocker."); state.Ball.MakeLoose(CoordinateUtils.To3DGround(impactPos), Vector3.zero, -1); HandlePossessionChange(state, -1, true); if (shooter != null) ResetPlayerActionState(shooter, result.Outcome); return; }
            LogEvent(state, $"Shot by {shooter?.BaseData?.FullName ?? "Unknown"} blocked by {blocker.BaseData.FullName}!", blocker.GetTeamId(), blocker.GetPlayerId());
            IncrementStat(state, blocker, stats => stats.BlocksMade++);
            Vector2 reboundDir = (impactPos - (shooter?.Position ?? impactPos)).normalized;
            if (reboundDir == Vector2.zero) reboundDir = (Vector2.right * (blocker.TeamSimId == 0 ? -1f : 1f));
            Vector2 randomOffset = new Vector2((float)state.RandomGenerator.NextDouble() - 0.5f, (float)state.RandomGenerator.NextDouble() - 0.5f) * 0.8f;
            Vector2 finalReboundDir = (reboundDir + randomOffset).normalized;
            float reboundSpeed = SimConstants.BALL_DEFAULT_HEIGHT * (float)state.RandomGenerator.NextDouble() * (BLOCK_REBOUND_MAX_SPEED_FACTOR - BLOCK_REBOUND_MIN_SPEED_FACTOR) + BLOCK_REBOUND_MIN_SPEED_FACTOR; // ERROR: Was using SHOT_BASE_SPEED, likely meant something smaller
            reboundSpeed = Mathf.Clamp(reboundSpeed, BLOCK_REBOUND_MIN_SPEED_FACTOR, BLOCK_REBOUND_MAX_SPEED_FACTOR); // Clamp rebound speed reasonably
            state.Ball.MakeLoose(CoordinateUtils.To3DGround(impactPos), CoordinateUtils.To3DGround(finalReboundDir * reboundSpeed, 0f) , blocker.TeamSimId, blocker); // Convert vel to 3D
            // Mark the ball as loose for AI/gameplay purposes after a block
            state.Ball.IsLooseBallSituation = true;
            HandlePossessionChange(state, -1, true);
            ResetPlayerActionState(blocker, result.Outcome);
            if (shooter != null) ResetPlayerActionState(shooter, result.Outcome);
        }

        // --- Enhanced block outcomes handler ---
        private void HandleEnhancedBlock(ActionResult result, MatchState state)
        {
            SimPlayer shooter = result.PrimaryPlayer;
            SimPlayer blocker = result.SecondaryPlayer;
            SimPlayer possessionPlayer = result.PossessionPlayer;
            string reason = result.Reason;
            // Log event with detailed outcome
            LogEvent(state, reason, blocker?.GetTeamId(), blocker?.GetPlayerId());
            IncrementStat(state, blocker, stats => stats.BlocksMade++);
            // Handle each outcome type
            switch (result.Outcome)
            {
                case ActionResultOutcome.BlockedAndCaught:
                case ActionResultOutcome.BlockedToTeammate:
                    // Possession goes to the defender or their teammate
                    if (possessionPlayer != null)
                    {
                        state.Ball.SetPossession(possessionPlayer);
                        HandlePossessionChange(state, possessionPlayer.TeamSimId, false);
                    }
                    break;
                case ActionResultOutcome.BlockedOutOfBounds:
                    // Ball is out of bounds, trigger OOB logic
                    state.Ball.MakeLoose(state.Ball.Position, Vector3.zero, -1);
                    HandlePossessionChange(state, -1, true);
                    break;
                case ActionResultOutcome.Deflected:
                    // Ball is loose after deflection
                    state.Ball.MakeLoose(state.Ball.Position, Vector3.zero, -1);
                    HandlePossessionChange(state, -1, true);
                    break;
            }
            if (blocker != null) ResetPlayerActionState(blocker, result.Outcome);
            if (shooter != null) ResetPlayerActionState(shooter, result.Outcome);
        }

        private void HandleGoalScored(ActionResult result, MatchState state)
        {
            // --- Suivi des stats individuelles ---
            if (result.PrimaryPlayer != null)
            {
                int playerId = result.PrimaryPlayer.GetPlayerId();
                if (!state.PlayerStats.ContainsKey(playerId))
                    state.PlayerStats[playerId] = new PlayerMatchStats();
                state.PlayerStats[playerId].GoalsScored++;
                state.PlayerStats[playerId].ShotsTaken++;
                state.PlayerStats[playerId].ShotsOnTarget++;
                state.PlayerStats[playerId].Participated = true;
            }
            if (result.SecondaryPlayer != null && result.SecondaryPlayer != result.PrimaryPlayer)
            {
                int assistId = result.SecondaryPlayer.GetPlayerId();
                if (!state.PlayerStats.ContainsKey(assistId))
                    state.PlayerStats[assistId] = new PlayerMatchStats();
                state.PlayerStats[assistId].Assists++;
                state.PlayerStats[assistId].Participated = true;
            }
            // --- Logic copied from MatchSimulator ---
            SimPlayer scorer = result.PrimaryPlayer;
            if (scorer?.BaseData == null) { LogEvent(state, "Goal registered with invalid scorer data!"); TransitionToPhase(state, GamePhase.Finished); return; } // Use PhaseManager
            int scoringTeamId = scorer.TeamSimId;
            bool wasPenalty = state.CurrentPhase == GamePhase.HomePenalty || state.CurrentPhase == GamePhase.AwayPenalty;
            if (scoringTeamId == 0) state.HomeScore++; else state.AwayScore++;
            IncrementStat(state, scorer, stats => stats.GoalsScored++);
            IncrementStat(state, scorer, stats => stats.ShotsOnTarget++);
            if (wasPenalty) { IncrementStat(state, scorer, stats => stats.PenaltiesScored++); }
            LogEvent(state, $"GOAL! Scored by {scorer.BaseData.FullName}. Score: {state.HomeTeamData?.Name ?? "Home"} {state.HomeScore} - {state.AwayScore} {state.AwayTeamData?.Name ?? "Away"}", scorer.GetTeamId(), scorer.GetPlayerId());
            state.Ball.Stop(); state.Ball.Position = _geometry.Center; // Use Geometry
            int kickoffTeamId = 1 - scoringTeamId;
            HandlePossessionChange(state, kickoffTeamId); // Ensure possession is set correctly BEFORE phase transition
            TransitionToPhase(state, GamePhase.PreKickOff); // Use PhaseManager
             foreach(var p in state.PlayersOnCourt.ToList()) { if (p != null && !p.IsSuspended()) ResetPlayerActionState(p, result.Outcome); }
        }

        private void HandleMiss(ActionResult result, MatchState state)
        {
            // --- Suivi des stats individuelles ---
            if (result.PrimaryPlayer != null)
            {
                int shooterId = result.PrimaryPlayer.GetPlayerId();
                if (!state.PlayerStats.ContainsKey(shooterId))
                    state.PlayerStats[shooterId] = new PlayerMatchStats();
                state.PlayerStats[shooterId].ShotsTaken++;
                state.PlayerStats[shooterId].Participated = true;
            }
            // --- Logic copied from MatchSimulator ---
            SimPlayer shooter = result.PrimaryPlayer;
            if (shooter?.BaseData != null) { LogEvent(state, $"Shot by {shooter.BaseData.FullName} missed target.", shooter.GetTeamId(), shooter.GetPlayerId()); ResetPlayerActionState(shooter, result.Outcome); }
            else { LogEvent(state, $"Shot missed target."); }
            state.Ball.SetLastShooter(null);
        }

        private void HandleFoul(ActionResult result, MatchState state)
        {
            SimPlayer foulingPlayer = result.PrimaryPlayer;
            SimPlayer fouledPlayer = result.SecondaryPlayer;
            FoulSeverity severity = result.FoulSeverity;

            if (foulingPlayer == null || state == null)
            {
                Debug.LogError("[DefaultMatchEventHandler] HandleFoul called with null player or state.");
                return;
            }

            // Check for specific foul reasons FIRST, as they might override severity
            if (result.Reason == "Defender entered 6m zone")
            {
                severity = FoulSeverity.PenaltyThrow;
                LogEvent(state, "Foul reason updated to PenaltyThrow: Defender entered 6m zone.", foulingPlayer.GetTeamId(), foulingPlayer.GetPlayerId());
            }

            string foulDesc = $"Foul by {foulingPlayer.BaseData?.FullName ?? "Unknown"}";
            if (fouledPlayer != null) foulDesc += $" on {fouledPlayer.BaseData?.FullName ?? "Unknown"}";
            foulDesc += $" ({severity})";

            LogEvent(state, foulDesc, foulingPlayer.GetTeamId(), foulingPlayer.GetPlayerId());
            IncrementStat(state, foulingPlayer, p => p.FoulsCommitted++);

            // --- Suspension/Red Card Escalation Logic ---
            FoulSeverity finalSeverity = severity; // Use this for subsequent logic

            if (finalSeverity == FoulSeverity.TwoMinuteSuspension)
            {
                foulingPlayer.TwoMinuteSuspensionCount++;
                LogEvent(state, $"{foulingPlayer.BaseData?.FullName ?? "Unknown"} receives 2-minute suspension ({foulingPlayer.TwoMinuteSuspensionCount} total).", foulingPlayer.GetTeamId(), foulingPlayer.GetPlayerId());

                if (foulingPlayer.TwoMinuteSuspensionCount >= 3)
                {
                    LogEvent(state, $"Third 2-minute suspension results in RED CARD for {foulingPlayer.BaseData?.FullName ?? "Unknown"}!", foulingPlayer.GetTeamId(), foulingPlayer.GetPlayerId());
                    finalSeverity = FoulSeverity.RedCard; // Escalate to Red Card
                }
            }

            // --- Apply Disciplinary Sanctions based on FINAL severity ---
            if (finalSeverity == FoulSeverity.TwoMinuteSuspension)
            {
                // Apply 2-minute suspension
                foulingPlayer.SuspensionTimer = DEFAULT_SUSPENSION_TIME;
                foulingPlayer.IsOnCourt = false; // Remove from court immediately

foulingPlayer.CurrentAction = PlayerAction.Idle;
                var teamOnCourt = state.GetTeamOnCourt(foulingPlayer.TeamSimId);
                if (teamOnCourt != null && teamOnCourt.Contains(foulingPlayer))
                {
                    teamOnCourt.Remove(foulingPlayer);
                }
                IncrementStat(state, foulingPlayer, p => p.TwoMinuteSuspensions++);
                _passivePlayManager?.ResetAttackTimer(); // Reset passive play timer
            }
            else if (finalSeverity == FoulSeverity.RedCard)
            {
                // Apply Red Card penalty
                LogEvent(state, $"RED CARD for {foulingPlayer.BaseData?.FullName ?? "Unknown"}! Player permanently removed.", foulingPlayer.GetTeamId(), foulingPlayer.GetPlayerId());
                foulingPlayer.SuspensionTimer = RED_CARD_SUSPENSION_TIME; // Effectively permanent
                foulingPlayer.CurrentAction = PlayerAction.Idle; 
                var teamOnCourt = state.GetTeamOnCourt(foulingPlayer.TeamSimId);
                if (teamOnCourt != null && teamOnCourt.Contains(foulingPlayer))
                {
                    teamOnCourt.Remove(foulingPlayer);
                }
                var teamBench = foulingPlayer.TeamSimId == 0 ? state.HomeBench : state.AwayBench;
                if (teamBench != null && teamBench.Contains(foulingPlayer))
                {
                    teamBench.Remove(foulingPlayer);
                }
                IncrementStat(state, foulingPlayer, p => p.RedCards++);
                state.TeamPenaltyTimer[foulingPlayer.TeamSimId] = DEFAULT_SUSPENSION_TIME;
                LogEvent(state, $"Team {foulingPlayer.TeamSimId} plays shorthanded for 2 minutes due to red card.", foulingPlayer.GetTeamId());
            }
            else if (finalSeverity != FoulSeverity.OffensiveFoul && (finalSeverity == FoulSeverity.FreeThrow || finalSeverity == FoulSeverity.PenaltyThrow))
            { 
                // Check for Yellow Card (only if not already suspended/sent off and no previous yellow)
                // Note: This logic might need refinement based on specific rules (e.g., progressive punishments)
                // For now, a simple check: give yellow for 'significant' fouls if not already carded/suspended.
                if (foulingPlayer.YellowCardCount == 0 && foulingPlayer.SuspensionTimer <= 0)
                { 
                    foulingPlayer.YellowCardCount++;
                    LogEvent(state, $"Yellow card for {foulingPlayer.BaseData?.FullName ?? "Unknown"}.", foulingPlayer.GetTeamId(), foulingPlayer.GetPlayerId());
                    _passivePlayManager?.ResetAttackTimer(); // Reset passive play timer after yellow card
                }
            }

            // --- Determine Restart --- 
            if (fouledPlayer == null) {
                Debug.LogWarning("[DefaultMatchEventHandler] Fouled player is null, cannot determine restart accurately.");
                // Default to possession change if possible, but phase transition might be wrong
                HandlePossessionChange(state, 1 - foulingPlayer.TeamSimId);
                ResetPlayerActionState(foulingPlayer, result.Outcome);
                return;
            }

            int victimTeamId = fouledPlayer.TeamSimId;
            HandlePossessionChange(state, victimTeamId); // Give possession to fouled team

            Vector2 foulLocation = result.ImpactPosition ?? fouledPlayer.Position; // Use impact or victim position
            bool isPenaltyAreaFoul = _geometry.IsInGoalArea(foulLocation, victimTeamId == 1); // Is foul inside the defending goal area?
            bool deniedClearChance = finalSeverity == FoulSeverity.RedCard || finalSeverity == FoulSeverity.TwoMinuteSuspension || finalSeverity == FoulSeverity.PenaltyThrow;

            GamePhase nextPhase;
            if ((isPenaltyAreaFoul || finalSeverity == FoulSeverity.PenaltyThrow) && finalSeverity != FoulSeverity.OffensiveFoul) // Penalty awarded
            {
                IncrementStat(state, victimTeamId, stats => stats.PenaltiesAwarded++);
                if (fouledPlayer != null)
{
    int id = fouledPlayer.GetPlayerId();
    if (!state.PlayerStats.ContainsKey(id))
        state.PlayerStats[id] = new PlayerMatchStats();
    state.PlayerStats[id].PenaltiesWon++;
    state.PlayerStats[id].Participated = true;
}
                nextPhase = (victimTeamId == 0) ? GamePhase.HomePenalty : GamePhase.AwayPenalty;
                LogEvent(state, $"7m Penalty awarded to Team {victimTeamId}.", victimTeamId);
            }
            else // Free throw awarded
            {
                Vector2 opponentGoalCenter = _geometry.GetOpponentGoalCenter(victimTeamId);
                Vector2 freeThrowLinePos = opponentGoalCenter + (foulLocation - opponentGoalCenter).normalized * _geometry.FreeThrowLineRadius;

                // If foul was between 6m and 9m lines, restart is from the 9m line directly outwards from goal
                if (!_geometry.IsInGoalArea(foulLocation, victimTeamId == 1) && Vector2.Distance(foulLocation, opponentGoalCenter) < _geometry.FreeThrowLineRadius)
                {
                    foulLocation = freeThrowLinePos;
                }
                // Else, restart from the place of the foul (unless offensive foul near own goal? Needs check)
                // For now, clamp position just to be safe
                foulLocation.x = Mathf.Clamp(foulLocation.x, 0f, _geometry.PitchLength);
                foulLocation.y = Mathf.Clamp(foulLocation.y, 0f, _geometry.PitchWidth);

                state.Ball.Position = CoordinateUtils.To3DGround(foulLocation); // Set ball position for restart
                state.Ball.Stop(); // Stop ball momentum
                nextPhase = (victimTeamId == 0) ? GamePhase.HomeSetPiece : GamePhase.AwaySetPiece;
                LogEvent(state, $"Free throw awarded to Team {victimTeamId}.", victimTeamId);
            }

            TransitionToPhase(state, nextPhase); // Transition to the determined phase

            // Reset actions for involved players
            ResetPlayerActionState(foulingPlayer, result.Outcome);
            ResetPlayerActionState(fouledPlayer, result.Outcome);
        }

        public virtual void HandleOutOfBounds(ActionResult result, MatchState state, Vector3? intersectionPoint3D = null)
        {
             // --- Logic copied from MatchSimulator ---
             if (state?.Ball == null) { return; }
             int lastTouchTeamId = state.Ball.LastTouchedByTeamId;
             int receivingTeamId;
             if (lastTouchTeamId == 0 || lastTouchTeamId == 1) { receivingTeamId = 1 - lastTouchTeamId; }
             else { receivingTeamId = (state.Ball.Position.x < _geometry.Center.x) ? 1 : 0; LogEvent(state, "Unknown last touch for OOB."); }

             Vector3 restartPosition3D = intersectionPoint3D ?? state.Ball.Position;
             RestartInfo restart = DetermineRestartTypeAndPosition(state, restartPosition3D, lastTouchTeamId, receivingTeamId);
             receivingTeamId = restart.ReceivingTeamId;

             LogEvent(state, $"Ball out of bounds ({restart.Type}). Possession to Team {receivingTeamId}.", receivingTeamId);

             HandlePossessionChange(state, receivingTeamId);
             state.Ball.Stop();
             state.Ball.Position = restart.Position;

             SimPlayer thrower = FindThrower(state, receivingTeamId, restart.Position, restart.IsGoalThrow);

             if (thrower != null) {
                 state.Ball.SetPossession(thrower);
                 // Ball is not loose after a throw-in if a player controls it
                 state.Ball.IsLooseBallSituation = false;
                 ResetPlayerActionState(thrower);
                 GamePhase nextPhase = restart.IsGoalThrow ? ((receivingTeamId == 0) ? GamePhase.HomeAttack : GamePhase.AwayAttack) : ((receivingTeamId == 0) ? GamePhase.HomeSetPiece : GamePhase.AwaySetPiece);
                 LogEvent(state, $"Throw-in by {thrower?.BaseData?.FullName ?? "Unknown"} for Team {receivingTeamId}.", receivingTeamId, thrower?.GetPlayerId());
                 TransitionToPhase(state, nextPhase);
             } else {
                 // No thrower found: ball remains loose on the field
                 state.Ball.IsLooseBallSituation = true;
                 Debug.LogWarning($"No thrower found for {restart.Type} for Team {receivingTeamId} at {state.Ball.Position}");
                 state.Ball.MakeLoose(state.Ball.Position, Vector3.zero, receivingTeamId);
                 HandlePossessionChange(state, -1, true);
             }
        }

        // --- Helper Methods (Copied & Adapted) ---

        public void IncrementStat(MatchState state, SimPlayer player, Action<TeamMatchStats> updateAction)
{
    if (player?.BaseData == null || state == null || updateAction == null)
        return;

    TeamMatchStats stats = (player.TeamSimId == 0) ? state.CurrentHomeStats : state.CurrentAwayStats;
    if (stats != null)
    {
        updateAction(stats);
    }
    else
    {
        Debug.LogWarning($"[DefaultMatchEventHandler] Could not find stats object for player {player.BaseData.FullName ?? "Unknown"} (TeamSimId: {player.TeamSimId}) to increment stat.");
    }
}

protected void IncrementStat(MatchState state, int teamSimId, Action<TeamMatchStats> updateAction)
{
    if (state == null || updateAction == null || (teamSimId != 0 && teamSimId != 1))
        return;

    TeamMatchStats stats = (teamSimId == 0) ? state.CurrentHomeStats : state.CurrentAwayStats;
    if (stats != null)
    {
        updateAction(stats);
    }
    else
    {
        Debug.LogWarning($"[DefaultMatchEventHandler] Could not find stats object for TeamSimId {teamSimId} to increment stat.");
    }
}

         private struct RestartInfo { public string Type; public Vector3 Position; public bool IsGoalThrow; public int ReceivingTeamId; }

         private RestartInfo DetermineRestartTypeAndPosition(MatchState state, Vector3 oobPos3D, int lastTouchTeamId, int initialReceivingTeamId)
         {
             // --- Logic copied from MatchSimulator ---
             // Use _geometry provider
             string restartType = "Throw-in"; Vector3 restartPos3D = oobPos3D; bool isGoalThrow = false; int finalReceivingTeamId = initialReceivingTeamId;
             float oobPosX = oobPos3D.x; float oobPosZ = oobPos3D.z;
             bool wentOutHomeGoalLine = oobPosX <= OOB_RESTART_BUFFER; bool wentOutAwayGoalLine = oobPosX >= _geometry.PitchLength - OOB_RESTART_BUFFER;

             if (wentOutHomeGoalLine) { if (lastTouchTeamId == 1) { isGoalThrow = true; restartType = "Goal throw"; finalReceivingTeamId = 0; } }
             else if (wentOutAwayGoalLine) { if (lastTouchTeamId == 0) { isGoalThrow = true; restartType = "Goal throw"; finalReceivingTeamId = 1; } }

             if (isGoalThrow) {
                 SimPlayer gk = state.GetGoalkeeper(finalReceivingTeamId);
                 Vector3 goalCenter = (finalReceivingTeamId == 0) ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
                 Vector3 dir3D = Vector3.zero;
                 if(gk != null) { dir3D = new Vector3(gk.Position.x - goalCenter.x, 0f, gk.Position.y - goalCenter.z); } // Use Y from 2D for Z in 3D
                 if(dir3D.sqrMagnitude < SimConstants.VELOCITY_NEAR_ZERO_SQ) { dir3D = new Vector3((finalReceivingTeamId == 0 ? 1f : -1f), 0f, 0f); }
                 restartPos3D = goalCenter + dir3D.normalized * GOAL_THROW_RESTART_DIST;
                 restartPos3D.y = SimConstants.BALL_DEFAULT_HEIGHT;
             } else {
                  restartPos3D.z = Mathf.Clamp(restartPos3D.z, OOB_RESTART_BUFFER, _geometry.PitchWidth - OOB_RESTART_BUFFER);
                  if (oobPosZ <= OOB_RESTART_BUFFER) restartPos3D.z = OOB_RESTART_BUFFER;
                  else if (oobPosZ >= _geometry.PitchWidth - OOB_RESTART_BUFFER) restartPos3D.z = _geometry.PitchWidth - OOB_RESTART_BUFFER;
                  if (!wentOutHomeGoalLine && !wentOutAwayGoalLine) { restartPos3D.x = Mathf.Clamp(restartPos3D.x, OOB_RESTART_BUFFER, _geometry.PitchLength - OOB_RESTART_BUFFER); }
                  restartPos3D.y = SimConstants.BALL_DEFAULT_HEIGHT;
             }
             restartPos3D.x = Mathf.Clamp(restartPos3D.x, 0f, _geometry.PitchLength); restartPos3D.z = Mathf.Clamp(restartPos3D.z, 0f, _geometry.PitchWidth);
             restartPos3D.y = SimConstants.BALL_DEFAULT_HEIGHT;
             return new RestartInfo { Type = restartType, Position = restartPos3D, IsGoalThrow = isGoalThrow, ReceivingTeamId = finalReceivingTeamId };
         }

         private SimPlayer FindThrower(MatchState state, int receivingTeamId, Vector3 restartPos3D, bool isGoalThrow)
         {
             // --- Logic copied from MatchSimulator ---
              if (isGoalThrow) { return state.GetGoalkeeper(receivingTeamId); }
              else {
                  Vector2 restartPos2D = CoordinateUtils.To2DGround(restartPos3D);
                  return state.GetTeamOnCourt(receivingTeamId)?
                                .Where(p => p != null && p.IsOnCourt && !p.IsSuspended() && !p.IsGoalkeeper())
                                .OrderBy(p => Vector2.Distance(p.Position, restartPos2D))
                                .FirstOrDefault();
              }
         }

         private int? GetTeamIdFromSimId(MatchState state, int simId)
         {
              if (state == null) return null;
              if (simId == 0) return state.HomeTeamData?.TeamID;
              if (simId == 1) return state.AwayTeamData?.TeamID;
              return null;
         }
        /// <summary>
        /// Transitions match to specified game phase
        /// </summary>
        /// <param name="state">Current match state</param>
        /// <param name="newPhase">Target phase to transition to</param>
        public virtual void TransitionToPhase(MatchState state, GamePhase newPhase)
        {
            // Validation checks
            if (state == null)
            {
                Debug.LogError("[DefaultMatchEventHandler] TransitionToPhase called with null MatchState.");
                return;
            }

            if (_phaseManager == null)
            {
                Debug.LogError("[DefaultMatchEventHandler] PhaseManager is not set for TransitionToPhase.");
                return;
            }

            // Log phase transition
            LogEvent(state, $"Match transitioning from {state.CurrentPhase} to {newPhase}.");

            // Delegate the actual transition to the PhaseManager
            // This avoids duplicating logic and maintains separation of concerns
            _phaseManager.TransitionToPhase(state, newPhase);
        }
    }
}