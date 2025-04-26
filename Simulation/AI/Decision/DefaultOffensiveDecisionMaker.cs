using HandballManager.Core;
using HandballManager.Simulation.Engines;
using UnityEngine;
using HandballManager.Simulation.AI.Evaluation; // Ajout pour accès aux évaluateurs
using HandballManager.Gameplay; // Pour PlayerPosition, Tactic, etc.

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Default implementation of the offensive decision maker interface.
    /// </summary>
    public class DefaultOffensiveDecisionMaker : IOffensiveDecisionMaker
    {
        // Ajout : Evaluateurs contextuels (injection possible ou fallback statique)
        private readonly ITacticalEvaluator _tacticalEvaluator;
        private readonly IPersonalityEvaluator _personalityEvaluator;
        private readonly IGameStateEvaluator _gameStateEvaluator;

        public DefaultOffensiveDecisionMaker(
            ITacticalEvaluator tacticalEvaluator = null,
            IPersonalityEvaluator personalityEvaluator = null,
            IGameStateEvaluator gameStateEvaluator = null)
        {
            _tacticalEvaluator = tacticalEvaluator;
            _personalityEvaluator = personalityEvaluator;
            _gameStateEvaluator = gameStateEvaluator;
        }
        public DecisionResult MakePassDecision(PlayerAIContext context)
        {
            // Raffinement : prise en compte du poste, attributs, évaluateurs
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var teammates = state.GetTeamOnCourt(player.TeamSimId);
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);

            if (!player.HasBall)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            SimPlayer bestTarget = null;
            float bestScore = float.MinValue;
            float decisionMaking = player.BaseData?.DecisionMaking ?? 50f;
            float teamwork = player.BaseData?.Teamwork ?? 50f;
            float anticipation = player.BaseData?.Anticipation ?? 50f;
            float composure = player.BaseData?.Composure ?? 50f;
            float passing = player.BaseData?.Passing ?? 50f;
            float creativity = player.BaseData?.Creativity ?? 50f;
            PlayerPosition role = player.AssignedTacticalRole;

            foreach (var mate in teammates)
            {
                if (mate == null || mate == player || !mate.IsOnCourt || mate.CurrentAction == PlayerAction.Suspended) continue;
                float score = -Vector2.Distance(mate.Position, GetOpponentGoalPosition(player.TeamSimId));
                score += 0.02f * teamwork;
                if ((mate.Velocity.sqrMagnitude > 1.0f)) score += 0.01f * anticipation;
                foreach (var opp in opponents)
                {
                    if (opp == null || !opp.IsOnCourt || opp.CurrentAction == PlayerAction.Suspended) continue;
                    if (Vector2.Distance(mate.Position, opp.Position) < 2.0f) score -= 5.0f;
                }
                // Différenciation par poste :
                if (role == PlayerPosition.LeftWing || role == PlayerPosition.RightWing)
                {
                    // Ailiers : passes rapides vers pivot ou arrière opposé
                    if (mate.AssignedTacticalRole == PlayerPosition.Pivot) score += 2.0f;
                    if (mate.AssignedTacticalRole == PlayerPosition.LeftBack || mate.AssignedTacticalRole == PlayerPosition.RightBack) score += 1.0f;
                }
                else if (role == PlayerPosition.Pivot)
                {
                    // Pivot : passes courtes, sécurité privilégiée
                    score -= Vector2.Distance(player.Position, mate.Position) * 0.5f;
                    if (mate.AssignedTacticalRole == PlayerPosition.CentreBack) score += 2.0f;
                }
                else if (role == PlayerPosition.CentreBack)
                {
                    // Demi-centre : favorise passes créatives et transversales
                    score += 0.02f * creativity;
                }
                // Attribut Passing : plus élevé, plus de passes risquées
                score += 0.01f * passing;
                // Évaluateurs contextuels
                float tacticalRisk = _tacticalEvaluator?.GetRiskModifier(tactic) ?? 1f;
                float personalityPass = _personalityEvaluator?.GetPassingTendencyModifier(player.BaseData) ?? 1f;
                float gameStateRisk = _gameStateEvaluator?.GetAttackRiskModifier(state, player.TeamSimId) ?? 1f;
                score *= tacticalRisk * personalityPass * gameStateRisk;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = mate;
                }
            }
            float passConfidenceMod = Mathf.Lerp(0.85f, 1.15f, (decisionMaking + composure + passing) / 250f);
            // Plus de confiance si le joueur est créatif et la situation l'encourage
            if (bestTarget != null)
            {
                return new DecisionResult { IsSuccessful = true, Confidence = (0.8f + 0.1f * (bestScore / 10.0f)) * passConfidenceMod, Data = bestTarget };
            }
            return new DecisionResult { IsSuccessful = false, Confidence = 0.3f };
        }

        public DecisionResult MakeShootDecision(PlayerAIContext context)
        {
            // Raffinement : différenciation poste, attributs, évaluateurs
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            Vector2 goalPos = GetOpponentGoalPosition(player.TeamSimId);
            float distToGoal = Vector2.Distance(player.Position, goalPos);

            if (!player.HasBall)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            bool inGoalArea = new HandballManager.Simulation.Utils.PitchGeometryProvider().IsInGoalArea(new Vector3(player.Position.x, SimConstants.BALL_RADIUS, player.Position.y), player.TeamSimId == 0);
            bool isOnGround = player.CurrentAction != PlayerAction.Jumping;
            bool allowShot = true;
            if (inGoalArea && isOnGround)
            {
                allowShot = false;
                if (ShouldMake6mZoneError(player, state))
                    allowShot = true;
            }

            PlayerPosition role = player.AssignedTacticalRole;
            float composure = player.BaseData?.Composure ?? 50f;
            float bravery = player.BaseData?.Bravery ?? 50f;
            float aggression = player.BaseData?.Aggression ?? 50f;
            float decisionMaking = player.BaseData?.DecisionMaking ?? 50f;
            float determination = player.BaseData?.Determination ?? 50f;
            float shootingPower = player.BaseData?.ShootingPower ?? 50f;
            float technique = player.BaseData?.Technique ?? 50f;
            float finishing = player.BaseData?.ShootingAccuracy ?? 50f;
            float tacticalRisk = _tacticalEvaluator?.GetRiskModifier(tactic) ?? 1f;
            float personalityShoot = _personalityEvaluator?.GetShootingTendencyModifier(player.BaseData) ?? 1f;
            float gameStateRisk = _gameStateEvaluator?.GetAttackRiskModifier(state, player.TeamSimId) ?? 1f;
            float combinedRisk = tacticalRisk * personalityShoot * gameStateRisk;

            int defendersNearby = 0;
            foreach (var opp in opponents)
            {
                if (opp == null || !opp.IsOnCourt || opp.CurrentAction == PlayerAction.Suspended) continue;
                if (Vector2.Distance(player.Position, opp.Position) < 2.5f) defendersNearby++;
            }

            bool isBackcourt = role == PlayerPosition.LeftBack || role == PlayerPosition.CentreBack || role == PlayerPosition.RightBack;
            bool isWing = role == PlayerPosition.LeftWing || role == PlayerPosition.RightWing;
            bool isPivot = role == PlayerPosition.Pivot;
            bool hasStrongShot = shootingPower > 85 && technique > 75;
            bool isLongRange = distToGoal >= 9.0f && distToGoal <= 12.0f;
            bool isJumping = player.CurrentAction == PlayerAction.Jumping;
            float shotConfidenceMod = Mathf.Lerp(0.8f, 1.2f, (composure + bravery + aggression + decisionMaking + determination + shootingPower + finishing) / 700f);

            // Différenciation par poste :
            if (isBackcourt && hasStrongShot && isLongRange && isJumping && allowShot)
            {
                float confidence = (0.7f - 0.1f * defendersNearby) * shotConfidenceMod * combinedRisk;
                return new DecisionResult { IsSuccessful = true, Confidence = confidence };
            }
            if (isWing && distToGoal < 7.5f && allowShot)
            {
                // Ailier : tirs angle fermé, favorise si peu de défenseurs proches
                float angleBonus = (Mathf.Abs(player.Position.x - goalPos.x) > 10f) ? 0.1f : 0f;
                float confidence = (0.75f + angleBonus - 0.1f * defendersNearby) * shotConfidenceMod * combinedRisk;
                return new DecisionResult { IsSuccessful = true, Confidence = confidence };
            }
            if (isPivot && distToGoal < 6.5f && allowShot)
            {
                // Pivot : tirs à bout portant, favorise si peu de défenseurs
                float confidence = (0.8f - 0.12f * defendersNearby) * shotConfidenceMod * combinedRisk;
                return new DecisionResult { IsSuccessful = true, Confidence = confidence };
            }
            if (distToGoal < 8.0f && defendersNearby <= 1 && allowShot)
            {
                float closeShotConfidenceMod = Mathf.Lerp(0.85f, 1.15f, (composure + bravery + determination + finishing) / 350f);
                return new DecisionResult { IsSuccessful = true, Confidence = (0.9f - 0.1f * defendersNearby) * closeShotConfidenceMod * combinedRisk };
            }
            // Sinon, pas de bonne opportunité
            float fallbackShotConfidenceMod = Mathf.Lerp(0.85f, 1.1f, (composure + bravery + shootingPower) / 250f);
            return new DecisionResult { IsSuccessful = false, Confidence = 0.4f * fallbackShotConfidenceMod * combinedRisk };
        }

        public DecisionResult MakeDribbleDecision(PlayerAIContext context)
        {
            // Raffinement : différenciation poste, attributs, évaluateurs
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);

            if (!player.HasBall)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            Vector2 dribbleTarget = GetOpponentGoalPosition(player.TeamSimId);
            bool isJumpPlanned = player.PlannedAction == PlayerAction.Jumping || player.PlannedAction == PlayerAction.PreparingShot;
            bool willBeInAir = isJumpPlanned || (player.CurrentAction == PlayerAction.Jumping && player.JumpOriginatedOutsideGoalArea);
            var geometry = new Utils.PitchGeometryProvider();
            bool isInGoalArea = geometry.IsInGoalArea(dribbleTarget, player.TeamSimId == 0);
            if (!willBeInAir && isInGoalArea)
            {
                dribbleTarget = Utils.PitchGeometryProvider.CalculatePathAroundGoalArea(player.Position, dribbleTarget, player.TeamSimId, geometry);
            }
            if (!willBeInAir && geometry.WouldCrossGoalArea(player.Position, dribbleTarget, player.TeamSimId))
            {
                dribbleTarget = Utils.PitchGeometryProvider.CalculatePathAroundGoalArea(player.Position, dribbleTarget, player.TeamSimId, geometry);
            }

            int nearbyOpponents = 0;
            foreach (var opp in opponents)
            {
                if (opp == null || !opp.IsOnCourt || opp.CurrentAction == PlayerAction.Suspended) continue;
                if ((player.Position - opp.Position).sqrMagnitude < 9.0f) nearbyOpponents++;
            }
            PlayerPosition role = player.AssignedTacticalRole;
            float determination = player.BaseData?.Determination ?? 50f;
            float composure = player.BaseData?.Composure ?? 50f;
            float aggression = player.BaseData?.Aggression ?? 50f;
            float decisionMaking = player.BaseData?.DecisionMaking ?? 50f;
            float dribbling = player.BaseData?.Dribbling ?? 50f;
            float tacticalRisk = _tacticalEvaluator?.GetRiskModifier(tactic) ?? 1f;
            float personalityDribble = _personalityEvaluator?.GetDribblingTendencyModifier(player.BaseData) ?? 1f;
            float gameStateRisk = _gameStateEvaluator?.GetAttackRiskModifier(state, player.TeamSimId) ?? 1f;
            float combinedRisk = tacticalRisk * personalityDribble * gameStateRisk;
            float dribbleConfidenceMod = Mathf.Lerp(0.85f, 1.15f, (determination + composure + aggression + decisionMaking + dribbling) / 450f);

            // Différenciation par poste :
            if ((role == PlayerPosition.LeftWing || role == PlayerPosition.RightWing) && nearbyOpponents <= 1)
            {
                // Ailier : dribble rapide sur l'aile si peu de pression
                return new DecisionResult { IsSuccessful = true, Confidence = 0.8f * dribbleConfidenceMod * combinedRisk, Data = dribbleTarget };
            }
            if ((role == PlayerPosition.LeftBack || role == PlayerPosition.CentreBack || role == PlayerPosition.RightBack) && nearbyOpponents <= 2)
            {
                // Arrière : dribble pénétration si espace
                return new DecisionResult { IsSuccessful = true, Confidence = 0.7f * dribbleConfidenceMod * combinedRisk, Data = dribbleTarget };
            }
            if (player.BaseData.GetShieldingEffectiveness() > 0.4f)
            {
                return new DecisionResult { IsSuccessful = true, Confidence = 0.85f * dribbleConfidenceMod * combinedRisk, Data = dribbleTarget };
            }
            else if (player.BaseData.GetShieldingEffectiveness() < 0.2f)
            {
                return new DecisionResult { IsSuccessful = false, Confidence = 0.3f * (1.0f - (aggression / 100f)) * combinedRisk };
            }
            if (nearbyOpponents > 0 && nearbyOpponents <= 2)
            {
                float pressureDribbleConfidenceMod = Mathf.Lerp(0.8f, 1.2f, (determination + composure + aggression + dribbling) / 400f);
                return new DecisionResult { IsSuccessful = true, Confidence = (0.7f - 0.1f * nearbyOpponents) * pressureDribbleConfidenceMod * combinedRisk, Data = dribbleTarget };
            }
            return new DecisionResult { IsSuccessful = false, Confidence = 0.3f * combinedRisk };
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
