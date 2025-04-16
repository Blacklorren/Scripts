using HandballManager.Simulation.AI.Decision;
using HandballManager.Simulation.Core.MatchData;
using UnityEngine;

namespace HandballManager.Simulation.Core.AI
{
    /// <summary>
    /// Default implementation of the offensive decision maker interface.
    /// </summary>
    public class DefaultOffensiveDecisionMaker : IOffensiveDecisionMaker
    {
        public DecisionResult MakePassDecision(PlayerAIContext context)
        {
            // Realistic pass decision logic
            // 1. Validate context
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var teammates = state.GetTeamOnCourt(player.TeamSimId);
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);

            // 2. If player does not have the ball, cannot pass
            if (!player.HasBall)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            // 3. Find best pass option (simplified: pass to open teammate closest to goal)
            SimPlayer bestTarget = null;
            float bestScore = float.MinValue;
            foreach (var mate in teammates)
            {
                if (mate == null || mate == player || !mate.IsOnCourt || mate.IsSuspended()) continue;
                // Score based on proximity to opponent goal (assuming goal at y=0 or y=max)
                float score = -Vector2.Distance(mate.Position, GetOpponentGoalPosition(player.TeamSimId));
                // Penalize if closely marked by opponent
                foreach (var opp in opponents)
                {
                    if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                    if (Vector2.Distance(mate.Position, opp.Position) < 2.0f) score -= 5.0f;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = mate;
                }
            }
            if (bestTarget != null)
            {
                return new DecisionResult { IsSuccessful = true, Confidence = 0.8f + 0.1f * (bestScore / 10.0f), Data = bestTarget };
            }
            // Fallback: no good pass
            return new DecisionResult { IsSuccessful = false, Confidence = 0.3f };
        }

        public DecisionResult MakeShotDecision(PlayerAIContext context)
        {
            // Realistic shot decision logic
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            Vector2 goalPos = GetOpponentGoalPosition(player.TeamSimId);
            float distToGoal = Vector2.Distance(player.Position, goalPos);

            // Only consider shooting if player has the ball
            if (!player.HasBall)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            // If close to goal and not heavily marked, shoot
            bool isClose = distToGoal < 8.0f; // Example: 8 meters
            int defendersNearby = 0;
            foreach (var opp in opponents)
            {
                if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                if (Vector2.Distance(player.Position, opp.Position) < 2.5f) defendersNearby++;
            }
            if (isClose && defendersNearby <= 1)
            {
                // Good shot opportunity
                return new DecisionResult { IsSuccessful = true, Confidence = 0.9f - 0.1f * defendersNearby };
            }
            // Otherwise, not a great shot
            return new DecisionResult { IsSuccessful = false, Confidence = 0.4f };
        }

        public DecisionResult MakeDribbleDecision(PlayerAIContext context)
        {
            // Realistic dribble decision logic
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);

            if (!player.HasBall)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            // If under moderate pressure but no good pass or shot, dribble
            int nearbyOpponents = 0;
            foreach (var opp in opponents)
            {
                if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                if (Vector2.Distance(player.Position, opp.Position) < 3.0f) nearbyOpponents++;
            }
            if (nearbyOpponents > 0 && nearbyOpponents <= 2)
            {
                return new DecisionResult { IsSuccessful = true, Confidence = 0.7f - 0.1f * nearbyOpponents };
            }
            // Otherwise, dribbling is not optimal
            return new DecisionResult { IsSuccessful = false, Confidence = 0.3f };
        }

        // Helper: Get the position of the opponent's goal (simplified, assumes 2D field with y=0 or y=max)
        private Vector2 GetOpponentGoalPosition(int teamSimId)
        {
            // For home team (0), opponent goal is at high y; for away (1), at low y
            // These values should be replaced with actual field dimensions if available
            float fieldLength = 40f;
            return teamSimId == 0 ? new Vector2(20f, fieldLength) : new Vector2(20f, 0f);
        }
    }
}
