using HandballManager.Core;
using HandballManager.Simulation.Engines;
using UnityEngine;
using HandballManager.Simulation.AI.Evaluation; // Ajout pour accès aux évaluateurs
using System.Collections.Generic;
using HandballManager.Gameplay; // Pour PlayerPosition, Tactic, etc.
using HandballManager.Simulation.Utils;

namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Default implementation of the offensive decision maker interface.
    /// </summary>
    public class DefaultOffensiveDecisionMaker : IOffensiveDecisionMaker, IShootingDecisionMaker, IPassingDecisionMaker, IDribblingDecisionMaker
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
            // Handball-specific: Role-based pass logic refinement
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var teammates = state.GetTeamOnCourt(player.TeamSimId);
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            var pitchGeometry = new HandballManager.Simulation.Utils.PitchGeometryProvider();
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
                if (mate == null || mate == player || !mate.IsOnCourt || mate.IsSuspended()) continue;
                float score = -Vector2.Distance(mate.Position, GetOpponentGoalPosition(player.TeamSimId));
                score += 0.02f * teamwork;
                if ((mate.Velocity.sqrMagnitude > 1.0f)) score += 0.01f * anticipation;
                foreach (var opp in opponents)
                {
                    if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                    if (Vector2.Distance(mate.Position, opp.Position) < 2.0f) score -= 5.0f;
                }
                // Handball-specific role-based logic
                if (PlayerPositionHelper.IsWing(role))
                {
                    // Wings: prefer passes back to backcourt or to Pivot, penalize risky cross passes
                    if (PlayerPositionHelper.IsBack(mate.AssignedTacticalRole)) score += 2.0f;
                    if (mate.AssignedTacticalRole == PlayerPosition.Pivot)
                    {
                        // Increase if Pivot is near 6m
                        if (pitchGeometry.IsNearSixMeterLine(mate.Position, mate.TeamSimId)) score += 2.5f;
                        else score += 1.0f;
                    }
                    // Penalize passes to opposite wing unless open
                    if (PlayerPositionHelper.IsWing(mate.AssignedTacticalRole) && mate.AssignedTacticalRole != role)
                    {
                        bool open = true;
                        foreach (var opp in opponents)
                        {
                            if (opp == null || !opp.IsOnCourt) continue;
                            if (Vector2.Distance(mate.Position, opp.Position) < 2.5f) { open = false; break; }
                        }
                        if (!open) score -= 2.0f;
                    }
                }
                else if (PlayerPositionHelper.IsBack(role))
                {
                    // Backs: prioritize passes to open wings or Pivot
                    if (PlayerPositionHelper.IsWing(mate.AssignedTacticalRole))
                    {
                        bool open = true;
                        foreach (var opp in opponents)
                        {
                            if (opp == null || !opp.IsOnCourt) continue;
                            if (Vector2.Distance(mate.Position, opp.Position) < 2.5f) { open = false; break; }
                        }
                        if (open) score += 2.0f;
                    }
                    if (mate.AssignedTacticalRole == PlayerPosition.Pivot && pitchGeometry.IsNearSixMeterLine(mate.Position, mate.TeamSimId)) score += 2.5f;
                    if (role == PlayerPosition.CentreBack) score += 1.0f; // Playmaker: higher base confidence
                }
                else if (role == PlayerPosition.Pivot)
                {
                    // Pivot: favor short, safe passes, penalize long/risky
                    float dist = Vector2.Distance(player.Position, mate.Position);
                    score -= dist * 0.7f;
                    if (PlayerPositionHelper.IsBack(mate.AssignedTacticalRole)) score += 1.0f;
                    if (mate.AssignedTacticalRole == PlayerPosition.CentreBack) score += 1.5f;
                    // Increase for passes received near 6m
                    if (pitchGeometry.IsNearSixMeterLine(player.Position, player.TeamSimId)) score += 2.0f;
                }
                // Passing attribute: more risky passes if higher
                score += 0.01f * passing;
                // Contextual evaluators
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
            // Playmaker (CentreBack) gets a base confidence boost
            if (role == PlayerPosition.CentreBack) passConfidenceMod *= 1.08f;
            if (bestTarget != null)
            {
                // Determine if the pass is complex (e.g., distance > 9m)
                float passDistance = Vector2.Distance(player.Position, bestTarget.Position);
                bool isComplex = passDistance > 9.0f;
                var passOption = new PassOption {
                    Player = bestTarget,
                    Score = bestScore,
                    IsSafe = false, // Could be refined
                    IsComplex = isComplex
                };
                return new DecisionResult { IsSuccessful = true, Confidence = (0.8f + 0.1f * (bestScore / 10.0f)) * passConfidenceMod, Data = passOption };
            }
            return new DecisionResult { IsSuccessful = false, Confidence = 0.3f * passConfidenceMod };
        }

        public DecisionResult MakeShootDecision(PlayerAIContext context)
        {
            // Handball-specific: Role-based shooting logic refinement
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            var pitchGeometry = new HandballManager.Simulation.Utils.PitchGeometryProvider();
            Vector2 goalPos = GetOpponentGoalPosition(player.TeamSimId);
            float distToGoal = Vector2.Distance(player.Position, goalPos);

            if (!player.HasBall)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            bool inGoalArea = pitchGeometry.IsInGoalArea(new Vector3(player.Position.x, SimConstants.BALL_RADIUS, player.Position.y), player.TeamSimId == 0);
            bool isOnGround = player.CurrentAction != PlayerAction.JumpingForShot;
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
                if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                if (Vector2.Distance(player.Position, opp.Position) < 2.5f) defendersNearby++;
            }

            bool isBackcourt = PlayerPositionHelper.IsBack(role);
            bool isWing = PlayerPositionHelper.IsWing(role);
            bool isPivot = role == PlayerPosition.Pivot;
            bool hasStrongShot = shootingPower > 85 && technique > 75;
            bool isLongRange = distToGoal >= 9.0f && distToGoal <= 12.0f;
            bool isJumping = player.CurrentAction == PlayerAction.JumpingForShot;
            float shotConfidenceMod = Mathf.Lerp(0.8f, 1.2f, (composure + bravery + aggression + decisionMaking + determination + shootingPower + finishing) / 700f);

            // Handball-specific role-based logic
            if (isWing && allowShot)
            {
                // Wings: check if at wide angle near goal area
                float jumping = player.BaseData?.Jumping ?? 50f;
        bool wideAngle = pitchGeometry.IsWideWingAngleNearGoal(player.Position, player.TeamSimId, jumping);
                float baseConf = 0.55f;
                if (wideAngle && distToGoal < 7.5f)
                {
                    baseConf = 0.85f;
                }
                else if (!wideAngle)
                {
                    baseConf = 0.4f; // Significantly decrease if not at wide angle
                }
                float angleBonus = (Mathf.Abs(player.Position.x - goalPos.x) > 10f) ? 0.1f : 0f;
                float confidence = (baseConf + angleBonus - 0.12f * defendersNearby) * shotConfidenceMod * combinedRisk;
                return new DecisionResult { IsSuccessful = confidence > 0.6f, Confidence = confidence };
            }
            if (isBackcourt && allowShot)
            {
                // Backs: prioritize shooting if >7m and moderately open
                if (distToGoal > 7.0f && defendersNearby <= 2)
                {
                    float rangeBonus = (distToGoal > 9.0f && hasStrongShot) ? 0.1f : 0.0f;
                    float baseConf = (role == PlayerPosition.CentreBack) ? 0.75f : 0.7f;
                    float confidence = (baseConf + rangeBonus - 0.1f * defendersNearby) * shotConfidenceMod * combinedRisk;
                    return new DecisionResult { IsSuccessful = confidence > 0.65f, Confidence = confidence };
                }
            }
            if (isPivot && allowShot)
            {
                // Pivot: increase confidence only if very close to goal (<7m) and in 'receiving pass' context
                bool near6m = distToGoal < 7.0f && pitchGeometry.IsNearSixMeterLine(player.Position, player.TeamSimId);
                bool justReceived = context.Player != null && context.Player.ReceivedPassRecently;
                if (near6m && justReceived)
                {
                    float confidence = (0.88f - 0.13f * defendersNearby) * shotConfidenceMod * combinedRisk;
                    return new DecisionResult { IsSuccessful = confidence > 0.7f, Confidence = confidence };
                }
                else
                {
                    // Discourage shooting otherwise
                    float confidence = 0.3f * shotConfidenceMod * combinedRisk;
                    // Encourage setting screens or finding space
                    if (EvaluateScreenOpportunity(context)?.ShouldSetScreen == true)
                        confidence += 0.1f;
                    return new DecisionResult { IsSuccessful = false, Confidence = confidence };
                }
            }
            // General fallback: close shot if open
            if (distToGoal < 8.0f && defendersNearby <= 1 && allowShot)
            {
                float closeShotConfidenceMod = Mathf.Lerp(0.85f, 1.15f, (composure + bravery + determination + finishing) / 350f);
                return new DecisionResult { IsSuccessful = true, Confidence = (0.9f - 0.1f * defendersNearby) * closeShotConfidenceMod * combinedRisk };
            }
            // Otherwise, not a good opportunity
            float fallbackShotConfidenceMod = Mathf.Lerp(0.85f, 1.1f, (composure + bravery + shootingPower) / 250f);
            return new DecisionResult { IsSuccessful = false, Confidence = 0.4f * fallbackShotConfidenceMod * combinedRisk };
        }

        public DecisionResult MakeDribbleDecision(PlayerAIContext context)
        {
            // Handball-specific: Role-based dribble logic refinement
            if (context == null || context.Player == null || context.MatchState == null)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            var player = context.Player;
            var state = context.MatchState;
            var tactic = context.Tactics;
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            var pitchGeometry = new HandballManager.Simulation.Utils.PitchGeometryProvider();
            if (!player.HasBall)
                return new DecisionResult { IsSuccessful = false, Confidence = 0.0f };

            Vector2 dribbleTarget = GetOpponentGoalPosition(player.TeamSimId);
            bool isJumpPlanned = player.PlannedAction == PlayerAction.JumpingForShot || player.PlannedAction == PlayerAction.PreparingShot;
            bool willBeInAir = isJumpPlanned || (player.CurrentAction == PlayerAction.JumpingForShot && player.JumpOriginatedOutsideGoalArea);
            bool isInGoalArea = pitchGeometry.IsInGoalArea(dribbleTarget, player.TeamSimId == 0);
            if (!willBeInAir && isInGoalArea)
            {
                dribbleTarget = HandballManager.Simulation.Utils.PitchGeometryProvider.CalculatePathAroundGoalArea(player.Position, dribbleTarget, player.TeamSimId, pitchGeometry);
            }
            if (!willBeInAir && pitchGeometry.WouldCrossGoalArea(player.Position, dribbleTarget, player.TeamSimId))
            {
                dribbleTarget = HandballManager.Simulation.Utils.PitchGeometryProvider.CalculatePathAroundGoalArea(player.Position, dribbleTarget, player.TeamSimId, pitchGeometry);
            }

            int nearbyOpponents = 0;
            foreach (var opp in opponents)
            {
                if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
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

            // Handball-specific role-based logic
            if (PlayerPositionHelper.IsWing(role))
            {
                // Wings: prefer dribbling along the sideline if space ahead
                bool nearSideline = pitchGeometry.IsNearSideline(player.Position);
                bool spaceAhead = true;
                foreach (var opp in opponents)
                {
                    if (opp == null || !opp.IsOnCourt) continue;
                    if ((opp.Position - player.Position).sqrMagnitude < 12.0f && Mathf.Abs(opp.Position.x - player.Position.x) < 5.0f)
                    {
                        spaceAhead = false;
                        break;
                    }
                }
                if (nearSideline && spaceAhead && nearbyOpponents <= 1)
                {
                    return new DecisionResult { IsSuccessful = true, Confidence = 0.85f * dribbleConfidenceMod * combinedRisk, Data = dribbleTarget };
                }
                // Otherwise, only dribble if little pressure
                if (nearbyOpponents <= 1)
                {
                    return new DecisionResult { IsSuccessful = true, Confidence = 0.7f * dribbleConfidenceMod * combinedRisk, Data = dribbleTarget };
                }
                // Discourage dribbling under pressure
                return new DecisionResult { IsSuccessful = false, Confidence = 0.2f * dribbleConfidenceMod * combinedRisk };
            }
            else if (PlayerPositionHelper.IsBack(role))
            {
                // Backs: allow penetration dribbles if space, penalize if defenders are close
                if (nearbyOpponents <= 2)
                {
                    return new DecisionResult { IsSuccessful = true, Confidence = 0.7f * dribbleConfidenceMod * combinedRisk, Data = dribbleTarget };
                }
                else
                {
                    return new DecisionResult { IsSuccessful = false, Confidence = 0.3f * dribbleConfidenceMod * combinedRisk };
                }
            }
            else if (role == PlayerPosition.Pivot)
            {
                // Pivot: drastically decrease dribble confidence in open play
                bool near6m = pitchGeometry.IsNearSixMeterLine(player.Position, player.TeamSimId);
                if (near6m && nearbyOpponents <= 1)
                {
                    // Only dribble if very close to goal and little pressure (rare)
                    return new DecisionResult { IsSuccessful = true, Confidence = 0.4f * dribbleConfidenceMod * combinedRisk, Data = dribbleTarget };
                }
                // Prefer to set screens or reposition
                if (EvaluateScreenOpportunity(context)?.ShouldSetScreen == true)
                {
                    return new DecisionResult { IsSuccessful = false, Confidence = 0.25f * dribbleConfidenceMod * combinedRisk };
                }
                return new DecisionResult { IsSuccessful = false, Confidence = 0.1f * dribbleConfidenceMod * combinedRisk };
            }
            // General fallback: attribute-based
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

        /// <summary>
        /// Calculates a score representing how desirable taking a shot is in the current situation.
        /// </summary>
        /// <param name="shooter">The player considering the shot.</param>
        /// <param name="state">The current match state.</param>
        /// <param name="tactic">The team's current tactic.</param>
        /// <returns>A score between 0 and 1 (higher is more desirable).</returns>
        public float EvaluateShootScore(SimPlayer shooter, MatchState state, Tactic tactic)
        {
            // Defensive: return 0 if any input is null
            if (shooter == null || state == null || tactic == null)
                return 0f;

            // Reuse logic from MakeShootDecision, but only compute the confidence value
            var opponents = state.GetOpposingTeamOnCourt(shooter.TeamSimId);
            var pitchGeometry = new HandballManager.Simulation.Utils.PitchGeometryProvider();
            Vector2 goalPos = GetOpponentGoalPosition(shooter.TeamSimId);
            float distToGoal = Vector2.Distance(shooter.Position, goalPos);

            if (!shooter.HasBall)
                return 0f;

            bool inGoalArea = pitchGeometry.IsInGoalArea(new Vector3(shooter.Position.x, SimConstants.BALL_RADIUS, shooter.Position.y), shooter.TeamSimId == 0);
            bool isOnGround = shooter.CurrentAction != PlayerAction.JumpingForShot;
            bool allowShot = true;
            if (inGoalArea && isOnGround)
            {
                allowShot = false;
                // If you want to allow rare 6m zone errors, you could call ShouldMake6mZoneError here if accessible
            }

            PlayerPosition role = shooter.AssignedTacticalRole;
            float composure = shooter.BaseData?.Composure ?? 50f;
            float bravery = shooter.BaseData?.Bravery ?? 50f;
            float aggression = shooter.BaseData?.Aggression ?? 50f;
            float decisionMaking = shooter.BaseData?.DecisionMaking ?? 50f;
            float determination = shooter.BaseData?.Determination ?? 50f;
            float shootingPower = shooter.BaseData?.ShootingPower ?? 50f;
            float technique = shooter.BaseData?.Technique ?? 50f;
            float finishing = shooter.BaseData?.ShootingAccuracy ?? 50f;
            float tacticalRisk = _tacticalEvaluator?.GetRiskModifier(tactic) ?? 1f;
            float personalityShoot = _personalityEvaluator?.GetShootingTendencyModifier(shooter.BaseData) ?? 1f;
            float gameStateRisk = _gameStateEvaluator?.GetAttackRiskModifier(state, shooter.TeamSimId) ?? 1f;
            float combinedRisk = tacticalRisk * personalityShoot * gameStateRisk;

            int defendersNearby = 0;
            foreach (var opp in opponents)
            {
                if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                if (Vector2.Distance(shooter.Position, opp.Position) < 2.5f) defendersNearby++;
            }

            bool isBackcourt = PlayerPositionHelper.IsBack(role);
            bool isWing = PlayerPositionHelper.IsWing(role);
            bool isPivot = role == PlayerPosition.Pivot;
            bool hasStrongShot = shootingPower > 85 && technique > 75;
            bool isLongRange = distToGoal >= 9.0f && distToGoal <= 12.0f;
            bool isJumping = shooter.CurrentAction == PlayerAction.JumpingForShot;
            float shotConfidenceMod = Mathf.Lerp(0.8f, 1.2f, (composure + bravery + aggression + decisionMaking + determination + shootingPower + finishing) / 700f);

            float confidence = 0f;
            if (isWing && allowShot)
            {
                float jumping = shooter.BaseData?.Jumping ?? 50f;
                bool wideAngle = pitchGeometry.IsWideWingAngleNearGoal(shooter.Position, shooter.TeamSimId, jumping);
                float baseConf = 0.55f;
                if (wideAngle && distToGoal < 7.5f)
                {
                    baseConf = 0.85f;
                }
                else if (!wideAngle)
                {
                    baseConf = 0.4f;
                }
                float angleBonus = (Mathf.Abs(shooter.Position.x - goalPos.x) > 10f) ? 0.1f : 0f;
                confidence = (baseConf + angleBonus - 0.12f * defendersNearby) * shotConfidenceMod * combinedRisk;
                return Mathf.Clamp01(confidence);
            }
            if (isBackcourt && allowShot)
            {
                if (distToGoal > 7.0f && defendersNearby <= 2)
                {
                    float rangeBonus = (distToGoal > 9.0f && hasStrongShot) ? 0.1f : 0.0f;
                    float baseConf = (role == PlayerPosition.CentreBack) ? 0.75f : 0.7f;
                    confidence = (baseConf + rangeBonus - 0.1f * defendersNearby) * shotConfidenceMod * combinedRisk;
                    return Mathf.Clamp01(confidence);
                }
            }
            if (isPivot && allowShot)
            {
                bool near6m = distToGoal < 7.0f && pitchGeometry.IsNearSixMeterLine(shooter.Position, shooter.TeamSimId);
                bool justReceived = shooter.ReceivedPassRecently;
                if (near6m && justReceived)
                {
                    confidence = (0.88f - 0.13f * defendersNearby) * shotConfidenceMod * combinedRisk;
                    return Mathf.Clamp01(confidence);
                }
                else
                {
                    confidence = 0.4f * shotConfidenceMod * combinedRisk;
                    return Mathf.Clamp01(confidence);
                }
            }
            // Default: discourage shooting
            confidence = 0.2f * shotConfidenceMod * combinedRisk;
            return Mathf.Clamp01(confidence);
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
    /// Evaluates if the player should set a screen for a teammate. Matches IOffensiveDecisionMaker interface.
    /// </summary>
    public ScreenDecisionData? EvaluateScreenOpportunity(PlayerAIContext context) // Changed return type
    {
        // Refined logic: Pivot should set screens for backcourt players; others rarely set screens
        if (context == null || context.Player == null || context.MatchState == null)
            return null; // Return null if context is invalid (Changed from DecisionResult)

        var player = context.Player;
        var state = context.MatchState;
        var teammates = state.GetTeamOnCourt(player.TeamSimId);
        var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
        bool isPivot = player.AssignedTacticalRole == PlayerPosition.Pivot;
        // Ensure TacticPositioner is correctly obtained from context or dependency injection
        var tacticPositioner = context.TacticPositioner;

        if (isPivot && tacticPositioner != null)
        {
            // Pivot: prioritize setting screens for backcourt teammates
            foreach (var mate in teammates)
            {
                if (mate == null || mate == player || !mate.IsOnCourt || mate.IsSuspended()) continue;
                // Only consider backcourt teammates
                if (mate.AssignedTacticalRole != PlayerPosition.LeftBack && mate.AssignedTacticalRole != PlayerPosition.CentreBack && mate.AssignedTacticalRole != PlayerPosition.RightBack)
                    continue;

                float distToMate = Vector2.Distance(player.Position, mate.Position);
                if (distToMate < 2.5f) // Close enough to set a screen
                {
                    // Is mate near the 9m line and has a close defender?
                    // Adjust Y-check based on actual pitch coordinates if needed
                    if (mate.Position.y > 7.0f && mate.Position.y < 10.0f) // Approximate 9m line
                    {
                        foreach (var opp in opponents)
                        {
                            if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
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
                                    EffectivenessAngle = angle,
                                    ShouldSetScreen = true // Explicitly set flag
                                };
                                // Return the data struct directly (Changed from DecisionResult)
                                return screenData;
                            }
                        }
                    }
                }
            }
        }
        // Return null if no suitable screen opportunity found (Changed from DecisionResult)
        return null;
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
                    if (mate == null || mate == player || !mate.IsOnCourt || mate.IsSuspended()) continue;
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
                    if (mate == null || mate == player || !mate.IsOnCourt || mate.IsSuspended()) continue;
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

        #region IShootingDecisionMaker Explicit Implementation
        // Existing explicit implementation for EvaluateShootScore
        float IShootingDecisionMaker.EvaluateShootScore(SimPlayer shooter, MatchState state, Tactic tactic)
        {
            return EvaluateShootScore(shooter, state, tactic);
        }

        // Explicit implementation for DecideAction
        float IShootingDecisionMaker.DecideAction(SimPlayer shooter, MatchState state)
        {
            // Determine the correct tactic based on the shooter's team
            // Assuming MatchState provides access to team tactics. Adjust if necessary.
            Tactic currentTactic = (shooter.TeamId == 0) ? state.HomeTactic : state.AwayTactic;

            if (currentTactic == null)
            {
                // Handle cases where the tactic might not be set yet or is invalid
                Debug.LogWarning($"[{nameof(DefaultOffensiveDecisionMaker)}] Tactic not found for team {shooter.TeamId} in DecideAction. Returning default score 0.");
                return 0f; // Default score if tactic is missing
            }

            // Reuse the existing EvaluateShootScore logic
            return EvaluateShootScore(shooter, state, currentTactic);
        }
        #endregion

        #region IPassingDecisionMaker Explicit Implementation
        
        List<PassOption> IPassingDecisionMaker.EvaluatePassOptions(SimPlayer passer, MatchState state, Tactic tactic, bool safeOnly)
        {
            return EvaluatePassOptionsImpl(passer, state, tactic, safeOnly);
        }
        private List<PassOption> EvaluatePassOptionsImpl(SimPlayer passer, MatchState state, Tactic tactic, bool safeOnly)
        {
            var passOptions = new List<PassOption>();
            if (passer == null || state == null || tactic == null) return passOptions;
            var teammates = state.GetTeamOnCourt(passer.TeamSimId);
            var opponents = state.GetOpposingTeamOnCourt(passer.TeamSimId);
            var pitchGeometry = new HandballManager.Simulation.Utils.PitchGeometryProvider();
            float teamwork = passer.BaseData?.Teamwork ?? 50f;
            float anticipation = passer.BaseData?.Anticipation ?? 50f;
            float passing = passer.BaseData?.Passing ?? 50f;
            float composure = passer.BaseData?.Composure ?? 50f;
            float creativity = passer.BaseData?.Creativity ?? 50f;
            PlayerPosition role = passer.AssignedTacticalRole;

            foreach (var mate in teammates)
            {
                if (mate == null || mate == passer || !mate.IsOnCourt || mate.IsSuspended()) continue;
                float score = -Vector2.Distance(mate.Position, GetOpponentGoalPosition(passer.TeamSimId));
                score += 0.02f * teamwork;
                if ((mate.Velocity.sqrMagnitude > 1.0f)) score += 0.01f * anticipation;
                bool isSafe = true;
                foreach (var opp in opponents)
                {
                    if (opp == null || !opp.IsOnCourt || opp.IsSuspended()) continue;
                    if (Vector2.Distance(mate.Position, opp.Position) < 2.0f) {
                        score -= 5.0f;
                        isSafe = false;
                    }
                }
                // Handball-specific role-based logic
                if (PlayerPositionHelper.IsWing(role))
                {
                    if (PlayerPositionHelper.IsBack(mate.AssignedTacticalRole)) score += 2.0f;
                    if (mate.AssignedTacticalRole == PlayerPosition.Pivot)
                    {
                        if (pitchGeometry.IsNearSixMeterLine(mate.Position, mate.TeamSimId)) score += 2.5f;
                        else score += 1.0f;
                    }
                    if (PlayerPositionHelper.IsWing(mate.AssignedTacticalRole) && mate.AssignedTacticalRole != role)
                    {
                        bool open = true;
                        foreach (var opp in opponents)
                        {
                            if (opp == null || !opp.IsOnCourt) continue;
                            if (Vector2.Distance(mate.Position, opp.Position) < 2.5f) { open = false; break; }
                        }
                        if (!open) score -= 2.0f;
                    }
                }
                else if (PlayerPositionHelper.IsBack(role))
                {
                    if (PlayerPositionHelper.IsWing(mate.AssignedTacticalRole))
                    {
                        bool open = true;
                        foreach (var opp in opponents)
                        {
                            if (opp == null || !opp.IsOnCourt) continue;
                            if (Vector2.Distance(mate.Position, opp.Position) < 2.5f) { open = false; break; }
                        }
                        if (open) score += 2.0f;
                    }
                    if (mate.AssignedTacticalRole == PlayerPosition.Pivot && pitchGeometry.IsNearSixMeterLine(mate.Position, mate.TeamSimId)) score += 2.5f;
                    if (role == PlayerPosition.CentreBack) score += 1.0f;
                }
                else if (role == PlayerPosition.Pivot)
                {
                    float dist = Vector2.Distance(passer.Position, mate.Position);
                    score -= dist * 0.7f;
                    if (PlayerPositionHelper.IsBack(mate.AssignedTacticalRole)) score += 1.0f;
                    if (mate.AssignedTacticalRole == PlayerPosition.CentreBack) score += 1.5f;
                    if (pitchGeometry.IsNearSixMeterLine(passer.Position, passer.TeamSimId)) score += 2.0f;
                }
                score += 0.01f * passing;
                float tacticalRisk = _tacticalEvaluator?.GetRiskModifier(tactic) ?? 1f;
                float personalityPass = _personalityEvaluator?.GetPassingTendencyModifier(passer.BaseData) ?? 1f;
                float gameStateRisk = _gameStateEvaluator?.GetAttackRiskModifier(state, passer.TeamSimId) ?? 1f;
                score *= tacticalRisk * personalityPass * gameStateRisk;
                // Normalize score to 0-1 for PassOption
                float normScore = Mathf.InverseLerp(-15f, 15f, score); // Adjust bounds as needed
                // Heuristic: consider a pass complex if distance is > 12m or not safe
                bool isComplex = Vector2.Distance(passer.Position, mate.Position) > 12f || !isSafe;
                // Filter if safeOnly required
                if (!safeOnly || isSafe)
                {
                    passOptions.Add(new PassOption {
                        Player = mate,
                        Score = normScore,
                        IsSafe = isSafe,
                        IsComplex = isComplex
                    });
                }
            }
            // Sort by score descending
            passOptions.Sort((a, b) => b.Score.CompareTo(a.Score));
            return passOptions;
        }
        
        public PassOption GetBestPassOption(SimPlayer passer, MatchState state, Tactic tactic)
        {
            var options = EvaluatePassOptionsImpl(passer, state, tactic, false);
            return options.Count > 0 ? options[0] : null;
        }

        public float DecideAction(SimPlayer passer, MatchState state)
        {
            // Retrieve the correct tactic for the passer's team
            Tactic currentTactic = (passer.TeamId == 0) ? state.HomeTactic : state.AwayTactic;
            if (currentTactic == null)
            {
                // Handle cases where the tactic might not be set yet or is invalid
                Debug.LogWarning($"[{nameof(DefaultOffensiveDecisionMaker)}] Tactic not found for team {passer.TeamId} in DecideAction. Returning default score 0.");
                return 0f; // Default score if tactic is missing
            }

            // Reuse the existing EvaluatePassOptions logic
            var options = EvaluatePassOptionsImpl(passer, state, currentTactic, false);
            return (options.Count > 0) ? options[0].Score : 0f;
        }

        public float EvaluateDribbleScore(SimPlayer dribbler, MatchState state, Tactic tactic)
        {
            var context = new PlayerAIContext {
                Player = dribbler,
                MatchState = state,
                Tactics = tactic
            };
            var result = MakeDribbleDecision(context);
            return Mathf.Clamp01(result.Confidence);
        }
        #endregion
    }
 }