using UnityEngine;
using System;
using System.Collections.Generic; // For List
using System.Linq;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Events.Interfaces; // For ToList() extension method

namespace HandballManager.Simulation.Utils // Changed from Services to Utils
{
    public class DefaultSimulationTimer : ISimulationTimer
    {
        public void UpdateTimers(MatchState state, float deltaTime, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
            if (state == null) return;

            // --- Timeout Timer --- (Handled differently - only runs if state IS Timeout)
            // This logic belongs *outside* the timer service, in the main loop's check
            // if (state.CurrentPhase == GamePhase.Timeout) { ... return; } -> This check is in MatchSimulator loop

            // --- Team Penalty Timers (Red Card Replacement) ---
            for (int teamSimId = 0; teamSimId < state.TeamPenaltyTimer.Length; teamSimId++)
            {
                if (state.TeamPenaltyTimer[teamSimId] > 0f)
                {
                    state.TeamPenaltyTimer[teamSimId] -= deltaTime;
                    if (state.TeamPenaltyTimer[teamSimId] <= 0f)
                    {
                        state.TeamPenaltyTimer[teamSimId] = 0f;

                        List<SimPlayer> teamOnCourt = state.GetTeamOnCourt(teamSimId);
                        List<SimPlayer> teamBench = (teamSimId == 0) ? state.HomeBench : state.AwayBench;

                        if (teamOnCourt != null && teamBench != null && teamOnCourt.Count < 7 && teamBench.Count > 0)
                        {
                            // Team is shorthanded and has bench players available
                            SimPlayer replacementPlayer = teamBench.FirstOrDefault(p => p != null && !p.IsOnCourt); // Find first available player on bench

                            if (replacementPlayer != null)
                            {
                                // Add replacement to court
                                teamOnCourt.Add(replacementPlayer);
                                replacementPlayer.IsOnCourt = true;
                                replacementPlayer.Position = replacementPlayer.TeamSimId == 0 ? new Vector2(2f, 1f) : new Vector2(38f, 1f); // Near bench
                                replacementPlayer.TargetPosition = replacementPlayer.Position;
                                replacementPlayer.CurrentAction = PlayerAction.Idle; // Reset action

                                // Remove from bench
                                teamBench.Remove(replacementPlayer);

                                eventHandler?.LogEvent(state, $"Team {teamSimId} replaces red-carded player with {replacementPlayer.BaseData?.FullName ?? "Unknown"} from bench.",
                                                      teamSimId, replacementPlayer.GetPlayerId());
                            }
                            else
                            {
                                eventHandler?.LogEvent(state, $"Team {teamSimId} penalty expired, but no suitable replacement player found on bench.", teamSimId);
                            }
                        }
                        else
                        {
                            // Penalty expired, but team is full or no bench players
                            eventHandler?.LogEvent(state, $"Team {teamSimId} penalty expired, but team is already full or no players on bench.", teamSimId);
                        }
                    }
                }
            }

            // --- Player Timers (Suspension, Action Prep) ---
            // We need to iterate over a copy of the players collection because the re-entry logic
            // modifies team court lists (adding/removing players) which could affect iteration safety.
            // ToList() creates a safe copy to iterate over while the original collection may change.
            var playersToUpdate = state.AllPlayers.Values.ToList(); // Creating a copy for safe iteration

            foreach (var player in playersToUpdate) {
                if (player == null) continue;
                try {
                    // --- Suspension Timer & Re-entry Logic ---
                    if (player.IsSuspended()) {
                        player.SuspensionTimer -= deltaTime;
                        if (player.SuspensionTimer <= 0f) {
                            player.SuspensionTimer = 0f;
                            List<SimPlayer> teamOnCourt = state.GetTeamOnCourt(player.TeamSimId);
                            bool canReEnter = teamOnCourt != null && teamOnCourt.Count < 7;

                            // Update IsOnCourt status based on re-entry possibility
                            player.IsOnCourt = canReEnter;

                            if (canReEnter) {
                                // Player re-enters - Add to court list IF NOT ALREADY THERE (safety check)
                                if (!teamOnCourt.Contains(player)) {
                                    teamOnCourt.Add(player);
                                }
                                player.Position = player.TeamSimId == 0 ? new Vector2(2f, 1f) : new Vector2(38f, 1f); // Near bench
                                player.TargetPosition = player.Position;
                                player.CurrentAction = PlayerAction.Idle; // Reset action
                                eventHandler?.LogEvent(state, $"Player {player.BaseData?.FullName ?? "Unknown"} re-enters after suspension.", player.GetTeamId(), player.GetPlayerId());
                            } else {
                                // Player suspension ended, but stays off court
                                if (teamOnCourt != null && teamOnCourt.Contains(player)) {
                                    teamOnCourt.Remove(player); // Ensure removed if somehow still in list
                                }
                                player.Position = new Vector2(-100, -100); // Keep off pitch
                                player.CurrentAction = PlayerAction.Idle; // Reset action
                                eventHandler?.LogEvent(state, $"Player {player.BaseData?.FullName ?? "Unknown"} suspension ended, but team is full.", player.GetTeamId(), player.GetPlayerId());
                            }
                        }
                    }
                    // --- Action Preparation Timer ---
                    // Only tick down if player is actually on court and preparing something
                    if (player.IsOnCourt && !player.IsSuspended() && player.ActionTimer > 0) {
                        player.ActionTimer -= deltaTime;
                        if (player.ActionTimer < 0f) player.ActionTimer = 0f; // Clamp to zero
                    }
                } catch (Exception ex) { Debug.LogError($"Error updating timers for player {player.GetPlayerId()}: {ex.Message}"); }
            }
        }
    }
}