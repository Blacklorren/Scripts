using HandballManager.Gameplay;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.AI.Evaluation;
using UnityEngine;
using HandballManager.Core;

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Default implementation of the defensive decision maker interface.
    /// </summary>
    public class DefaultDefensiveDecisionMaker : IDefensiveDecisionMaker
    {
        private readonly ITacticalEvaluator _tacticalEvaluator;
        private readonly IPersonalityEvaluator _personalityEvaluator;
        private readonly IGameStateEvaluator _gameStateEvaluator;

        public DefaultDefensiveDecisionMaker(
            ITacticalEvaluator tacticalEvaluator = null,
            IPersonalityEvaluator personalityEvaluator = null,
            IGameStateEvaluator gameStateEvaluator = null)
        {
            _tacticalEvaluator = tacticalEvaluator;
            _personalityEvaluator = personalityEvaluator;
            _gameStateEvaluator = gameStateEvaluator;
        }
        /// <summary>
        /// Makes a tackle decision based on player context
        /// </summary>
        public DecisionResult MakeTackleDecision(PlayerAIContext context)
        {
            // Integrate evaluators and role
            var player = context?.Player;
            float aggression = player?.BaseData?.Aggression ?? 50f;
            float bravery = player?.BaseData?.Bravery ?? 50f;
            float determination = player?.BaseData?.Determination ?? 50f;
            float concentration = player?.BaseData?.Concentration ?? 50f;
            float riskMod = _personalityEvaluator?.GetRiskModifier(player?.BaseData) ?? 1.0f;
            float tacticAggression = _tacticalEvaluator?.GetRiskModifier(context?.Tactics) ?? 1.0f;
            float gameAggression = _gameStateEvaluator?.GetAttackRiskModifier(context?.MatchState, player?.TeamSimId ?? 0) ?? 1.0f;
            float tackleConfidence = Mathf.Lerp(0.7f, 1.2f, (aggression + bravery + determination + concentration) / 400f) * riskMod * tacticAggression * gameAggression;
            return new DecisionResult { IsSuccessful = true, Confidence = 0.75f * tackleConfidence };
        }

        /// <summary>
        /// Decides the best defensive action for the player based on the current state and tactics.
        /// </summary>
        public DefensiveAction DecideDefensiveAction(SimPlayer player, MatchState state, Tactic tactic)
        {
            // Role-based and evaluator-driven logic
            var role = player?.AssignedTacticalRole ?? default(PlayerPosition);
            float tacticAggression = _tacticalEvaluator?.GetRiskModifier(tactic) ?? 1.0f;
            float personalityAggression = _personalityEvaluator?.GetRiskModifier(player?.BaseData) ?? 1.0f;
            float gameAggression = _gameStateEvaluator?.GetAttackRiskModifier(state, player?.TeamSimId ?? 0) ?? 1.0f;
            float aggressionMod = tacticAggression * personalityAggression * gameAggression;

            var action = new DefensiveAction();
            if (player == null || !player.IsOnCourt || player.IsSuspended()) {
                action.Action = PlayerAction.Idle;
                return action;
            }

            var ball = state.Ball;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            if (opponents == null || opponents.Count == 0) {
                action.Action = PlayerAction.Idle;
                return action;
            }

            SimPlayer ballHolder = ball?.Holder;
            bool opponentHasBall = ballHolder != null && ballHolder.TeamSimId != player.TeamSimId;

            // Role-specific defensive logic
            switch (role) {
                case PlayerPosition.Goalkeeper:
                    // TODO: goalkeeper logic is still basic; will implement proper methods later
                    // Goalkeeper: prioritize blocking shots, positioning, and quick recovery
                    if (opponentHasBall && Vector2.Distance(player.Position, ballHolder.Position) < 2.5f) {
                        action.Action = PlayerAction.AttemptingBlock;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    } else {
                        action.Action = PlayerAction.GoalkeeperPositioning;
                        action.TargetPlayer = null;
                        action.TargetPosition = player.Position; // Let GK logic handle optimal pos
                        return action;
                    }
                case PlayerPosition.Pivot:
                    // Pivot: focus on blocking passing lanes, physical marking in 6m zone
                    if (opponentHasBall && Vector2.Distance(player.Position, ballHolder.Position) < 3.0f * aggressionMod) {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                    // Mark closest dangerous opponent in the 6m area
                    SimPlayer closeOpp = null;
                    float minDist = float.MaxValue;
                    foreach (var opp in opponents) {
                        float dist = Vector2.Distance(player.Position, opp.Position);
                        if (dist < minDist && opp.Position.y < 7.0f) { // 6m area
                            minDist = dist;
                            closeOpp = opp;
                        }
                    }
                    if (closeOpp != null) {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = closeOpp;
                        action.TargetPosition = closeOpp.Position;
                        return action;
                    }
                    break;
                case PlayerPosition.LeftBack:
                case PlayerPosition.CentreBack:
                case PlayerPosition.RightBack:
                    // Back: intercept passes, aggressive marking, help block shots
                    if (opponentHasBall && Vector2.Distance(player.Position, ballHolder.Position) < 2.5f * aggressionMod) {
                        action.Action = PlayerAction.AttemptingTackle;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                    // Mark closest opponent in backcourt
                    SimPlayer backOpp = null;
                    minDist = float.MaxValue;
                    foreach (var opp in opponents) {
                        float dist = Vector2.Distance(player.Position, opp.Position);
                        if (dist < minDist && opp.Position.y > 7.0f) { // backcourt
                            minDist = dist;
                            backOpp = opp;
                        }
                    }
                    if (backOpp != null) {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = backOpp;
                        action.TargetPosition = backOpp.Position;
                        return action;
                    }
                    break;
                case PlayerPosition.LeftWing:
                case PlayerPosition.RightWing:
                    // Wing: intercept passes to corners, pressure on wings
                    if (opponentHasBall && Vector2.Distance(player.Position, ballHolder.Position) < 2.0f * aggressionMod) {
                        action.Action = PlayerAction.AttemptingTackle;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                    // Mark closest opponent near sideline
                    SimPlayer wingOpp = null;
                    minDist = float.MaxValue;
                    foreach (var opp in opponents) {
                        float dist = Vector2.Distance(player.Position, opp.Position);
                        if (dist < minDist && Mathf.Abs(opp.Position.x) > 7.0f) { // wide
                            minDist = dist;
                            wingOpp = opp;
                        }
                    }
                    if (wingOpp != null) {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = wingOpp;
                        action.TargetPosition = wingOpp.Position;
                        return action;
                    }
                    break;
                default:
                    // Fallback to generic logic
                    break;
            }

            // --- Existing generic fallback logic below ---
            if (opponentHasBall) {
                float aggression = player.BaseData?.Aggression ?? 50f;
                float bravery = player.BaseData?.Bravery ?? 50f;
                float anticipation = player.BaseData?.Anticipation ?? 50f;
                float determination = player.BaseData?.Determination ?? 50f;
                float concentration = player.BaseData?.Concentration ?? 50f;
                float leadership = player.BaseData?.Leadership ?? 50f;
                float workRate = player.BaseData?.WorkRate ?? 50f;
                float distanceToHolder = Vector2.Distance(player.Position, ballHolder.Position);
                float tackleRange = 2.0f + 0.01f * anticipation;
                if (ballHolder.BaseData.GetShieldingEffectiveness() < 0.2f && distanceToHolder <= tackleRange) {
                    if ((aggression + bravery + determination) / 3f > 60f) {
                        action.Action = PlayerAction.AttemptingTackle;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                } else if (ballHolder.BaseData.GetShieldingEffectiveness() > 0.4f) {
                    if ((concentration + leadership) / 2f > 55f) {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                } else if (distanceToHolder <= tackleRange) {
                    if (determination > 45f) {
                        action.Action = PlayerAction.AttemptingTackle;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                } else {
                    if ((workRate + leadership) / 2f > 50f) {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                }
            } else if (ball != null && ball.IsLoose) {
                float workRate = player.BaseData?.WorkRate ?? 50f;
                float determination = player.BaseData?.Determination ?? 50f;
                float anticipation = player.BaseData?.Anticipation ?? 50f;
                if ((workRate + determination + anticipation) / 3f > 45f) {
                    action.Action = PlayerAction.ChasingBall;
                    action.TargetPlayer = null;
                    action.TargetPosition = new Vector2(ball.Position.x, ball.Position.z);
                    return action;
                }
            } else {
                float leadership = player.BaseData?.Leadership ?? 50f;
                float concentration = player.BaseData?.Concentration ?? 50f;
                SimPlayer markTarget = null;
                float minDist = float.MaxValue;
                foreach (var opp in opponents) {
                    if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                    float dist = Vector2.Distance(player.Position, opp.Position);
                    if (dist < minDist) {
                        minDist = dist;
                        markTarget = opp;
                    }
                }
                if (markTarget != null && (leadership + concentration) / 2f > 45f) {
                    action.Action = PlayerAction.MarkingPlayer;
                    action.TargetPlayer = markTarget;
                    action.TargetPosition = markTarget.Position;
                    return action;
                }
            }
            action.Action = PlayerAction.Idle;
            return action;
        }

    }
}
