using HandballManager.Core;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.AI.Decision
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
                if (mate == null || mate == player || !mate.IsOnCourt || mate.CurrentAction == PlayerAction.Suspended) continue;
                // Score based on proximity to opponent goal (assuming goal at y=0 or y=max)
                float score = -Vector2.Distance(mate.Position, GetOpponentGoalPosition(player.TeamSimId));
                // Penalize if closely marked by opponent
                foreach (var opp in opponents)
                {
                    if (opp == null || !opp.IsOnCourt || opp.CurrentAction == PlayerAction.Suspended) continue;
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

        public DecisionResult MakeShootDecision(PlayerAIContext context)
        {
            // Realistic shoot decision logic
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

            // --- 6m Zone Error Simulation ---
            bool inGoalArea = false;
            // Use static or direct instance of PitchGeometryProvider
            inGoalArea = new HandballManager.Simulation.Utils.PitchGeometryProvider().IsInGoalArea(new Vector3(player.Position.x, SimConstants.BALL_RADIUS, player.Position.y), player.TeamSimId == 0);
            bool isOnGround = player.CurrentAction != PlayerAction.Jumping;
            bool allowShot = true;
            if (inGoalArea && isOnGround)
            {
                // Normally, don't allow shot from ground in 6m
                allowShot = false;
                // But allow with error chance if player makes a skill-based mistake
                if (ShouldMake6mZoneError(player, state))
                    allowShot = true;
            }

            // If close to goal and not heavily marked, shoot
            bool isClose = distToGoal < 8.0f; // Example: 8 meters
            int defendersNearby = 0;
            foreach (var opp in opponents)
            {
                if (opp == null || !opp.IsOnCourt || opp.CurrentAction == PlayerAction.Suspended) continue;
                if (Vector2.Distance(player.Position, opp.Position) < 2.5f) defendersNearby++;
            }
            // --- Ajout : Tir longue distance pour arrières puissants ---
            bool isBackcourt = player.AssignedTacticalRole == PlayerPosition.LeftBack
                    || player.AssignedTacticalRole == PlayerPosition.CentreBack
                    || player.AssignedTacticalRole == PlayerPosition.RightBack;
            bool hasStrongShot = player.BaseData != null && player.BaseData.ShootingPower > 85 && player.BaseData.Technique > 75;
            bool isLongRange = distToGoal >= 9.0f && distToGoal <= 12.0f;
            bool isJumping = player.CurrentAction == PlayerAction.Jumping;
            if (isBackcourt && hasStrongShot && isLongRange && isJumping && allowShot)
            {
                float confidence = 0.7f - 0.1f * defendersNearby;
                return new DecisionResult { IsSuccessful = true, Confidence = confidence };
            }
            if (isClose && defendersNearby <= 1 && allowShot)
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

            // Plan dribble target
            Vector2 dribbleTarget = GetOpponentGoalPosition(player.TeamSimId);
            // --- 6m Zone Avoidance for Offensive AI ---
            bool isJumpPlanned = player.PlannedAction == PlayerAction.Jumping || player.PlannedAction == PlayerAction.PreparingShot;
            bool willBeInAir = isJumpPlanned || (player.CurrentAction == PlayerAction.Jumping && player.JumpOriginatedOutsideGoalArea);
            // Use geometry provider to check if dribbleTarget is in the 6m zone
            var geometry = new Utils.PitchGeometryProvider();
            bool isInGoalArea = geometry.IsInGoalArea(dribbleTarget, player.TeamSimId == 0);
            if (!willBeInAir && isInGoalArea)
            {
                // Reroute around the 6m zone if not about to jump
                dribbleTarget = Utils.PitchGeometryProvider.CalculatePathAroundGoalArea(player.Position, dribbleTarget, player.TeamSimId, geometry);
            }
            // If the path to target crosses the 6m zone and not jumping, reroute
            if (!willBeInAir && geometry.WouldCrossGoalArea(player.Position, dribbleTarget, player.TeamSimId))
            {
                dribbleTarget = Utils.PitchGeometryProvider.CalculatePathAroundGoalArea(player.Position, dribbleTarget, player.TeamSimId, geometry);
            }


            // If under moderate pressure but no good pass or shot, dribble
            int nearbyOpponents = 0;
            foreach (var opp in opponents)
            {
                if (opp == null || !opp.IsOnCourt || opp.CurrentAction == PlayerAction.Suspended) continue;
                if ((player.Position - opp.Position).sqrMagnitude < 9.0f) nearbyOpponents++;
            }
            // --- BallProtectionBonus logic ---
            if (player.BaseData.GetShieldingEffectiveness() > 0.4f)
            {
                // Well shielded: boost confidence
                return new DecisionResult { IsSuccessful = true, Confidence = 0.85f, Data = dribbleTarget };
            }
            else if (player.BaseData.GetShieldingEffectiveness() < 0.2f)
            {
                // Poorly shielded: avoid dribbling
                return new DecisionResult { IsSuccessful = false, Confidence = 0.3f };
            }
            if (nearbyOpponents > 0 && nearbyOpponents <= 2)
            {
                return new DecisionResult { IsSuccessful = true, Confidence = 0.7f - 0.1f * nearbyOpponents, Data = dribbleTarget };
            }
            // Otherwise, dribbling is not optimal
            return new DecisionResult { IsSuccessful = false, Confidence = 0.3f };
        }

            // Explicit implementation for interface: MakeShotDecision
            public DecisionResult MakeShotDecision(PlayerAIContext context)
                {
                    // Alias to MakeShootDecision for interface compatibility
                    return MakeShootDecision(context);
                }

            
        // Returns true if the player should make a 6m zone error (shot on ground in zone), based on attributes
        private bool ShouldMake6mZoneError(SimPlayer player, MatchState state)
        {
            // Example: use agility and decision making, normalized 0..1 (add more as needed)
            float agility = player.BaseData?.Agility ?? 0.5f;
            float decision = player.BaseData?.DecisionMaking ?? 0.5f;
            // Future: add e.g. fatigue, pressure, composure, etc.
            float errorChance = Mathf.Lerp(0.01f, 0.20f, 1f - 0.5f * (agility + decision));
            errorChance = Mathf.Clamp(errorChance, 0.01f, 0.25f);
            float rand = (state.RandomGenerator != null) ? (float)state.RandomGenerator.NextDouble() : UnityEngine.Random.value;
            return rand < errorChance;
        }

        // Helper: Get the position of the opponent's goal (simplified, assumes 2D field with y=0 or y=max)
        private Vector2 GetOpponentGoalPosition(int teamSimId)
        {
            // For home team (0), opponent goal is at high y; for away (1), at low y
            // These values should be replaced with actual field dimensions if available
            float fieldLength = 40f;
            return teamSimId == 0 ? new Vector2(20f, fieldLength) : new Vector2(20f, 0f);
        }

        /// <summary>
        /// Evaluates if the player should set a screen for a teammate.
        /// </summary>
        public DecisionResult EvaluateScreenOpportunity(PlayerAIContext context)
        {
            // Refined logic: Pivot should set screens for backcourt players; others rarely set screens
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };
            var player = context.Player;
            var state = context.MatchState;
            var teammates = state.GetTeamOnCourt(player.TeamSimId);
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            bool isPivot = player.AssignedTacticalRole == PlayerPosition.Pivot;
            var tacticPositioner = context.TacticPositioner as Positioning.TacticPositioner;

            if (isPivot && tacticPositioner != null)
            {
                // Pivot: prioritize setting screens for backcourt teammates
                foreach (var mate in teammates)
                {
                    if (mate == null || mate == player || !mate.IsOnCourt || mate.CurrentAction == PlayerAction.Suspended) continue;
                    // Only consider backcourt teammates
                    // Only consider backcourt teammates
                    if (mate.AssignedTacticalRole != PlayerPosition.LeftBack && mate.AssignedTacticalRole != PlayerPosition.CentreBack && mate.AssignedTacticalRole != PlayerPosition.RightBack)
                    continue;
                    float distToMate = Vector2.Distance(player.Position, mate.Position);
                    if (distToMate < 2.5f) // Close enough to set a screen
                    {
                        // Is mate near the 9m line and has a close defender?
                        if (mate.Position.y > 7.0f && mate.Position.y < 10.0f) // Approximate 9m line
                        {
                            foreach (var opp in opponents)
                            {
                                if (opp == null || !opp.IsOnCourt || opp.CurrentAction == PlayerAction.Suspended) continue;
                                float distToOpp = Vector2.Distance(mate.Position, opp.Position);
                                if (distToOpp < 1.5f)
                                {
                                    // Use helpers to determine screen spot and effectiveness
                                    Vector2 screenSpot = tacticPositioner.GetScreenSpotForScreener(player, mate, opp);
                                    float angle = tacticPositioner.GetScreenAngleBetweenDefenderAndTarget(opp, player, mate);
                                    // Package info for downstream use
                                    var screenData = new ScreenDecisionData
                                    {
                                        Screener = player,
                                        User = mate,
                                        Defender = opp,
                                        ScreenSpot = screenSpot,
                                        EffectivenessAngle = angle
                                    };
                                    float confidence = 0.6f + Mathf.Clamp01(angle / 90f) * 0.2f; // More open angle = higher confidence
                                    return new DecisionResult { IsSuccessful = true, Confidence = confidence, Data = screenData };
                                }
                            }
                        }
                    }
                }
            }
            return new DecisionResult { IsSuccessful = false, Confidence = 0.2f };
        }

        // Helper struct for screen data
        public struct ScreenDecisionData
        {
            public SimPlayer Screener;
            public SimPlayer User;
            public SimPlayer Defender;
            public Vector2 ScreenSpot;
            public float EffectivenessAngle;
        }

        /// <summary>
        /// Determines if a player should use a screen set by a teammate.
        /// </summary>
        public DecisionResult CanUseScreen(PlayerAIContext context)
        {
            // Refined logic: Backcourt players use pivot's screens; pivot can use screens to get open for a pass
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };
            var player = context.Player;
            var state = context.MatchState;
            var teammates = state.GetTeamOnCourt(player.TeamSimId);
            bool isPivot = player.AssignedTacticalRole == PlayerPosition.Pivot;
            var tacticPositioner = context.TacticPositioner as Positioning.TacticPositioner;
            Vector2 goalPos = Vector2.zero;
            if (tacticPositioner != null)
                goalPos = player.TeamSimId == 0 ? new Vector2(20f, 40f) : new Vector2(20f, 0f); // Use same logic as GetOpponentGoalPosition

            if (!isPivot && tacticPositioner != null)
            {
                // Backcourt players: use pivot's screens
                foreach (var mate in teammates)
                {
                    if (mate == null || mate == player || !mate.IsOnCourt || mate.CurrentAction == PlayerAction.Suspended) continue;
                    if (mate.AssignedTacticalRole == PlayerPosition.Pivot && mate.CurrentAction == PlayerAction.SettingScreen)
                    {
                        float distToMate = Vector2.Distance(player.Position, mate.Position);
                        if (distToMate < 3.0f)
                        {
                            // Use helpers to determine user spot
                            Vector2 useSpot = tacticPositioner.GetScreenSpotForUser(mate, player, goalPos);
                            float angle = tacticPositioner.GetScreenAngleBetweenDefenderAndTarget(null, mate, player); // Defender unknown here
                            var useData = new ScreenUseData
                            {
                                Screener = mate,
                                User = player,
                                UseSpot = useSpot,
                                EffectivenessAngle = angle
                            };
                            return new DecisionResult { IsSuccessful = true, Confidence = 0.85f, Data = useData };
                        }
                    }
                }
            }
            else if (isPivot && tacticPositioner != null)
            {
                // Pivot: can use a screen from a backcourt player (rare)
                foreach (var mate in teammates)
                {
                    if (mate == null || mate == player || !mate.IsOnCourt || mate.CurrentAction == PlayerAction.Suspended) continue;
                    if ((mate.AssignedTacticalRole == PlayerPosition.LeftBack || mate.AssignedTacticalRole == PlayerPosition.CentreBack || mate.AssignedTacticalRole == PlayerPosition.RightBack)
                        && mate.CurrentAction == PlayerAction.SettingScreen)
                    {
                        float distToMate = Vector2.Distance(player.Position, mate.Position);
                        if (distToMate < 3.0f)
                        {
                            Vector2 useSpot = tacticPositioner.GetScreenSpotForUser(mate, player, goalPos);
                            float angle = tacticPositioner.GetScreenAngleBetweenDefenderAndTarget(null, mate, player);
                            var useData = new ScreenUseData
                            {
                                Screener = mate,
                                User = player,
                                UseSpot = useSpot,
                                EffectivenessAngle = angle
                            };
                            return new DecisionResult { IsSuccessful = true, Confidence = 0.7f, Data = useData };
                        }
                    }
                }
            }
            return new DecisionResult { IsSuccessful = false, Confidence = 0.2f };
        }

        // Helper struct for screen use data
        public struct ScreenUseData
        {
            public SimPlayer Screener;
            public SimPlayer User;
            public Vector2 UseSpot;
            public float EffectivenessAngle;
        }
        
    }
}
