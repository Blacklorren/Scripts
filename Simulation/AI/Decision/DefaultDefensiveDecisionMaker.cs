using HandballManager.Gameplay;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Default implementation of the defensive decision maker interface.
    /// </summary>
    public class DefaultDefensiveDecisionMaker : IDefensiveDecisionMaker
    {
        /// <summary>
        /// Makes a tackle decision based on player context
        /// </summary>
        public DecisionResult MakeTackleDecision(PlayerAIContext context)
        {
            return new DecisionResult { IsSuccessful = true, Confidence = 0.75f };
        }

        /// <summary>
        /// Decides the best defensive action for the player based on the current state and tactics.
        /// </summary>
        public DefensiveAction DecideDefensiveAction(SimPlayer player, MatchState state, Tactic tactic)
        {
            // DefensiveAction to return
            var action = new DefensiveAction();

            // 1. Check player status
            if (player == null || !player.IsOnCourt || player.IsSuspended())
            {
                action.Action = PlayerAction.Idle;
                return action;
            }

            // 2. Get ball and opponents
            var ball = state.Ball;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            if (opponents == null || opponents.Count == 0)
            {
                action.Action = PlayerAction.Idle;
                return action;
            }

            // 3. Check if any opponent has the ball
            SimPlayer ballHolder = ball?.Holder;
            bool opponentHasBall = ballHolder != null && ballHolder.TeamSimId != player.TeamSimId;

            // 4. Defensive priorities
            if (opponentHasBall)
            {
                float distanceToHolder = Vector2.Distance(player.Position, ballHolder.Position);
                float tackleRange = 2.0f; // Example value, tweak as needed

                // --- BallProtectionBonus logic ---
                if (ballHolder.BaseData.GetShieldingEffectiveness() < 0.2f && distanceToHolder <= tackleRange)
                {
                    // Poorly shielded: aggressive tackle
                    action.Action = PlayerAction.AttemptingTackle;
                    action.TargetPlayer = ballHolder;
                    action.TargetPosition = ballHolder.Position;
                    return action;
                }
                else if (ballHolder.BaseData.GetShieldingEffectiveness() > 0.4f)
                {
                    // Well shielded: contain instead of tackle
                    action.Action = PlayerAction.MarkingPlayer;
                    action.TargetPlayer = ballHolder;
                    action.TargetPosition = ballHolder.Position;
                    return action;
                }
                else if (distanceToHolder <= tackleRange)
                {
                    // Default: attempt tackle if in range
                    action.Action = PlayerAction.AttemptingTackle;
                    action.TargetPlayer = ballHolder;
                    action.TargetPosition = ballHolder.Position;
                    return action;
                }
                else
                {
                    // Not in range, mark or chase
                    action.Action = PlayerAction.MarkingPlayer;
                    action.TargetPlayer = ballHolder;
                    action.TargetPosition = ballHolder.Position;
                    return action;
                }
            }
            else if (ball != null && ball.IsLoose)
            {
                // Ball is loose, chase it
                action.Action = PlayerAction.ChasingBall;
                action.TargetPlayer = null;
                action.TargetPosition = new Vector2(ball.Position.x, ball.Position.z); // Convert 3D to 2D
                return action;
            }
            else
            {
                // No immediate ball threat, mark the most dangerous opponent (e.g., closest to goal or player)
                SimPlayer markTarget = null;
                float minDist = float.MaxValue;
                foreach (var opp in opponents)
                {
                    if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                    float dist = Vector2.Distance(player.Position, opp.Position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        markTarget = opp;
                    }
                }
                if (markTarget != null)
                {
                    action.Action = PlayerAction.MarkingPlayer;
                    action.TargetPlayer = markTarget;
                    action.TargetPosition = markTarget.Position;
                    return action;
                }
            }

            // Fallback: Idle
            action.Action = PlayerAction.Idle;
            return action;
        }
    }
}
