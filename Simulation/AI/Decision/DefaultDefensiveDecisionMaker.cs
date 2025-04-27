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
            // --- System-aware defensive logic ---
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

            // --- Defensive system awareness ---
            var formation = tactic?.DefensiveFormationData;
            string system = (formation?.Name ?? "").Trim().ToUpperInvariant();
            // DefensiveFormationData peut maintenant être utilisé pour une logique avancée basée sur la structure de la formation

            // Helper function: find closest threat near a reference position
            SimPlayer FindClosestThreat(Vector2 refPos, float maxDist = 100f, bool mustHaveBall = false)
            {
                SimPlayer threat = null;
                float minDist = float.MaxValue;
                foreach (var opp in opponents)
                {
                    if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                    if (mustHaveBall && (ballHolder == null || opp != ballHolder)) continue;
                    // Threat: has ball OR is in scoring position (near 6m or open)
                    bool isThreat = (opp == ballHolder) || (opp.Position.y < 7.0f && !opp.IsGoalkeeper());
                    if (!isThreat) continue;
                    float dist = Vector2.Distance(refPos, opp.Position);
                    if (dist < minDist && dist < maxDist)
                    {
                        minDist = dist;
                        threat = opp;
                    }
                }
                return threat;
            }

            // Helper function: get all defenders on court for this team
            var defenders = state.GetTeamOnCourt(player.TeamSimId);

            // --- SYSTEM LOGIC ---
            if (system == "6-0")
            {
                // All defenders hold the line near 6m, mark closest threat, conservative tackling
                SimPlayer threat = FindClosestThreat(player.Position, 7.0f);
                if (threat != null)
                {
                    action.Action = PlayerAction.MarkingPlayer;
                    action.TargetPlayer = threat;
                    action.TargetPosition = threat.Position;
                    // Only attempt tackle if opponent tries to break through line
                    if (opponentHasBall && threat == ballHolder && Vector2.Distance(player.Position, ballHolder.Position) < 2.0f && ballHolder.Position.y < 7.0f)
                    {
                        // More conservative: only tackle if aggression is high
                        float agg = player.BaseData?.Aggression ?? 50f;
                        if (agg > 65f)
                        {
                            action.Action = PlayerAction.AttemptingTackle;
                        }
                    }
                    return action;
                }
            }
            else if (system == "5-1")
            {
                // Identify the 'point' defender (CentreBack or designated)
                bool isPoint = role == PlayerPosition.CentreBack;
                if (!isPoint && tactic?.DefensiveFormationData != null)
                {
                    // Try to match slot name for point (if formation data supports it)
                    var slot = tactic.DefensiveFormationData.Slots?.Find(s => s.PositionRole.ToString().ToLower().Contains("point"));
                    if (slot != null && slot.PositionRole == player.AssignedTacticalRole) isPoint = true;
                }
                if (isPoint)
                {
                    // Point defender: position higher, aggressive on ball carrier, attempt interceptions
                    if (opponentHasBall && ballHolder != null && ballHolder.Position.y > 9.0f)
                    {
                        action.Action = PlayerAction.AttemptingTackle;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                    // Otherwise, mark closest backcourt threat
                    SimPlayer threat = FindClosestThreat(player.Position, 20.0f);
                    if (threat != null)
                    {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = threat;
                        action.TargetPosition = threat.Position;
                        return action;
                    }
                }
                else
                {
                    // Other 5 defenders: behave like 6-0 line
                    SimPlayer threat = FindClosestThreat(player.Position, 7.0f);
                    if (threat != null)
                    {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = threat;
                        action.TargetPosition = threat.Position;
                        // Tackle only if opponent tries to break line
                        if (opponentHasBall && threat == ballHolder && Vector2.Distance(player.Position, ballHolder.Position) < 2.0f && ballHolder.Position.y < 7.0f)
                        {
                            float agg = player.BaseData?.Aggression ?? 50f;
                            if (agg > 65f)
                            {
                                action.Action = PlayerAction.AttemptingTackle;
                            }
                        }
                        return action;
                    }
                }
            }
            else if (system == "3-2-1")
            {
                // Assign lines by role (or by slot if formation data is available)
                // 1 high: CentreBack or designated; 2 mid: LeftBack/RightBack; 3 deep: Pivot/Wings
                bool isHigh = role == PlayerPosition.CentreBack;
                bool isMid = role == PlayerPosition.LeftBack || role == PlayerPosition.RightBack;
                bool isDeep = role == PlayerPosition.Pivot || role == PlayerPosition.LeftWing || role == PlayerPosition.RightWing;
                // If formation data available, prefer slot assignment
                if (tactic?.DefensiveFormationData != null)
                {
                    var slot = tactic.DefensiveFormationData.Slots?.Find(s => s.PositionRole == player.AssignedTacticalRole);
                    if (slot != null)
                    {
                        var name = slot.PositionRole.ToString().ToLower();
                        isHigh = name.Contains("high") || name.Contains("point");
                        isMid = name.Contains("mid") || name.Contains("half");
                        isDeep = name.Contains("deep") || name.Contains("wing") || name.Contains("pivot");
                    }
                }
                if (isHigh)
                {
                    // High defender: aggressive, challenge ball carrier high, attempt interceptions
                    if (opponentHasBall && ballHolder != null && ballHolder.Position.y > 10.0f)
                    {
                        action.Action = PlayerAction.AttemptingTackle;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                    // Otherwise, mark closest backcourt threat
                    SimPlayer threat = FindClosestThreat(player.Position, 20.0f);
                    if (threat != null)
                    {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = threat;
                        action.TargetPosition = threat.Position;
                        return action;
                    }
                }
                else if (isMid)
                {
                    // Mid defenders: cover half-spaces, challenge backcourt players
                    if (opponentHasBall && ballHolder != null && ballHolder.Position.y > 9.0f && Vector2.Distance(player.Position, ballHolder.Position) < 3.5f)
                    {
                        action.Action = PlayerAction.AttemptingTackle;
                        action.TargetPlayer = ballHolder;
                        action.TargetPosition = ballHolder.Position;
                        return action;
                    }
                    // Otherwise, mark closest backcourt threat
                    SimPlayer threat = FindClosestThreat(player.Position, 12.0f);
                    if (threat != null)
                    {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = threat;
                        action.TargetPosition = threat.Position;
                        return action;
                    }
                }
                else if (isDeep)
                {
                    // Deep defenders: cover wings and pivot, less aggressive
                    SimPlayer threat = FindClosestThreat(player.Position, 7.0f);
                    if (threat != null)
                    {
                        action.Action = PlayerAction.MarkingPlayer;
                        action.TargetPlayer = threat;
                        action.TargetPosition = threat.Position;
                        // Tackle only if opponent tries to break line
                        if (opponentHasBall && threat == ballHolder && Vector2.Distance(player.Position, ballHolder.Position) < 2.0f && ballHolder.Position.y < 7.0f)
                        {
                            float agg = player.BaseData?.Aggression ?? 50f;
                            if (agg > 70f)
                            {
                                action.Action = PlayerAction.AttemptingTackle;
                            }
                        }
                        return action;
                    }
                }
            }
            // --- fallback to existing role-based logic below if no system match ---

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
