using UnityEngine;
using HandballManager.Simulation.Utils;
using System;
using System.Linq;
using HandballManager.Core;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Events.Interfaces;

namespace HandballManager.Simulation.Events.Detectors
{
    public class DefaultEventDetector : IEventDetector
    {
        private readonly IGeometryProvider _geometry;

        // Constants moved from MatchSimulator or SimConstants
        private const float INTERCEPTION_RADIUS = ActionResolverConstants.INTERCEPTION_RADIUS;
        private const float BLOCK_RADIUS = ActionResolverConstants.BLOCK_RADIUS;
        private const float SAVE_REACH_BUFFER = ActionResolverConstants.SAVE_REACH_BUFFER;
        private const float LOOSE_BALL_PICKUP_RADIUS = ActionResolverConstants.LOOSE_BALL_PICKUP_RADIUS;
        private const float PHYSICS_STEP_SIZE = 0.02f;
        private const float LOOSE_BALL_PICKUP_RADIUS_SQ = LOOSE_BALL_PICKUP_RADIUS * LOOSE_BALL_PICKUP_RADIUS;
        private const float OOB_RESTART_BUFFER = 0.1f;

        public DefaultEventDetector(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        public void CheckReactiveEvents(MatchState state, IActionResolver actionResolver, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
            if (state == null || state.CurrentPhase == GamePhase.Finished) return;
            CheckForInterceptions(state, actionResolver, eventHandler); if (state.CurrentPhase == GamePhase.Finished) return;
            CheckForBlocks(state, actionResolver, eventHandler); if (state.CurrentPhase == GamePhase.Finished) return;

            CheckForLooseBallPickup(state, eventHandler);
        }

        public void CheckPassiveEvents(MatchState state, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
             if (state == null || state.Ball == null || eventHandler == null || state.CurrentPhase == GamePhase.Finished) return;
             try {
                 if (!CheckGoalLineCrossing(state, eventHandler)) {
                      CheckSideLineCrossing(state, eventHandler);
                 }
             } catch (Exception ex) { Debug.LogError($"Error checking passive events: {ex.Message}"); }
        }

        // --- Private Event Check Methods (Copied & Adapted) ---

        private void CheckForInterceptions(MatchState state, IActionResolver actionResolver, IMatchEventHandler eventHandler)
        {
             // --- Logic copied from MatchSimulator ---
             if (state?.Ball?.Passer == null || !state.Ball.IsInFlight || state.Ball.IntendedTarget == null) return;

             var potentialInterceptors = state.GetOpposingTeamOnCourt(state.Ball.Passer.TeamSimId)?.ToList();
             if (potentialInterceptors == null) return;

             foreach (var defender in potentialInterceptors) {
                 if (defender == null || defender.IsSuspended()) continue;
                 try {
                     // Use injected ActionResolver
                     float interceptChance = actionResolver.CalculateInterceptionChance(defender, state.Ball, state);
                     if (interceptChance > 0f && state.RandomGenerator.NextDouble() < interceptChance) {
                         ActionResult result = new ActionResult { Outcome = ActionResultOutcome.Intercepted, PrimaryPlayer = defender, SecondaryPlayer = state.Ball.Passer, ImpactPosition = defender.Position };
                         eventHandler.HandleActionResult(result, state); // Use injected handler
                         return;
                     }
                      if (defender.CurrentAction == PlayerAction.Intercepting) {
                          eventHandler.ResetPlayerActionState(defender, ActionResultOutcome.Failure); // Use handler
                     }
                 } catch (Exception ex) { Debug.LogError($"Error checking interception for defender {defender.GetPlayerId()}: {ex.Message}"); }
             }
        }

        private void CheckForBlocks(MatchState state, IActionResolver actionResolver, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
            if (state?.Ball?.LastShooter == null || !state.Ball.IsInFlight) return;

            var potentialBlockers = state.GetOpposingTeamOnCourt(state.Ball.LastShooter.TeamSimId)?.ToList();
            if (potentialBlockers == null) return;

            // Use geometry provider for goal center
            Vector2 targetGoal = _geometry.GetOpponentGoalCenter(state.Ball.LastShooter.TeamSimId);
            Vector2 ballPos2D = CoordinateUtils.To2DGround(state.Ball.Position);

            foreach (var defender in potentialBlockers) {
                if (defender == null || defender.IsSuspended() || defender.IsGoalkeeper()) continue;
                try {
                    float distToBall = Vector2.Distance(defender.Position, ballPos2D);
                    if (distToBall < BLOCK_RADIUS * 1.5f) {
                        Vector2 shooterPos = state.Ball.LastShooter.Position;
                        float distToLine = SimulationUtils.CalculateDistanceToLine(defender.Position, shooterPos, targetGoal);

                        if (distToLine < BLOCK_RADIUS) {
                            // Simplified block chance calculation (ActionResolver doesn't have one currently)
                             float blockChance = 0.2f * Mathf.Lerp(0.5f, 1.5f, (defender.BaseData?.Blocking ?? 50f) / 75f)
                                                  * Mathf.Lerp(0.8f, 1.2f, (defender.BaseData?.Anticipation ?? 50f) / 100f)
                                                  * (1.0f - Mathf.Clamp01(distToLine / BLOCK_RADIUS));

                            if (state.RandomGenerator.NextDouble() < Mathf.Clamp01(blockChance)) {
                                ActionResult result = new ActionResult { Outcome = ActionResultOutcome.Blocked, PrimaryPlayer = defender, SecondaryPlayer = state.Ball.LastShooter, ImpactPosition = defender.Position };
                                eventHandler.HandleActionResult(result, state); // Use handler
                                return;
                            }
                        }
                    }
                } catch (Exception ex) { Debug.LogError($"Error checking block for defender {defender.GetPlayerId()}: {ex.Message}"); }
            }
        }

        private void CheckForLooseBallPickup(MatchState state, IMatchEventHandler eventHandler)
        {
            // --- Logic copied from MatchSimulator ---
            if (state?.Ball == null || !state.Ball.IsLoose) return;
            SimPlayer potentialPicker = null; float minPickDistanceSq = LOOSE_BALL_PICKUP_RADIUS_SQ;
            var players = state.PlayersOnCourt?.ToList();
            if (players == null) return;

            Vector2 ballPos2D = CoordinateUtils.To2DGround(state.Ball.Position);

            try {
                 // Prioritize players actively moving to the ball position
                 potentialPicker = players
                    .Where(p => p != null && !p.IsSuspended() && p.CurrentAction == PlayerAction.MovingToPosition)
                    .OrderBy(p => (p.Position - ballPos2D).sqrMagnitude)
                    .FirstOrDefault(p => (p.Position - ballPos2D).sqrMagnitude < minPickDistanceSq);

                 // If no chaser is close enough, check others
                 if (potentialPicker == null)
                 {
                    potentialPicker = players
                        .Where(p => p != null && !p.IsSuspended() && p.CurrentAction != PlayerAction.Landing && p.CurrentAction != PlayerAction.MovingToPosition)
                        .OrderBy(p => (p.Position - ballPos2D).sqrMagnitude)
                        .FirstOrDefault(p => (p.Position - ballPos2D).sqrMagnitude < minPickDistanceSq && (p.BaseData?.Technique ?? 0) > ActionResolverConstants.MIN_PICKUP_TECHNIQUE
                  && (p.BaseData?.Anticipation ?? 0) > ActionResolverConstants.MIN_PICKUP_ANTICIPATION);
                 }


                if (potentialPicker != null) {
                    ActionResult pickupResult = new ActionResult { Outcome = ActionResultOutcome.Success, PrimaryPlayer = potentialPicker, ImpactPosition = ballPos2D, Reason = "Picked up loose ball" };
                    eventHandler.HandlePossessionChange(state, potentialPicker.TeamSimId); // Use handler
                    state.Ball.SetPossession(potentialPicker);
                    eventHandler.ResetPlayerActionState(potentialPicker, pickupResult.Outcome); // Use handler
                }
            } catch (Exception ex) { Debug.LogError($"Error checking loose ball pickup: {ex.Message}"); }
        }

         // --- Passive Event Check Methods ---

         private bool CheckGoalLineCrossing(MatchState state, IMatchEventHandler eventHandler)
         {
            // --- Logic copied from MatchSimulator ---
             Vector3 currentBallPos = state.Ball.Position;
             Vector3 prevBallPos = currentBallPos - state.Ball.Velocity * PHYSICS_STEP_SIZE;

             var crossInfo = DidCrossGoalLinePlane(prevBallPos, currentBallPos);
             if (!crossInfo.DidCross) return false;

             if (Mathf.Abs(currentBallPos.x - prevBallPos.x) < SimConstants.FLOAT_EPSILON) return false;
             float intersectT = Mathf.Clamp01((crossInfo.GoalLineX - prevBallPos.x) / (currentBallPos.x - prevBallPos.x));
             Vector3 intersectPoint = prevBallPos + (currentBallPos - prevBallPos) * intersectT;

             if (IsWithinGoalBounds(intersectPoint, crossInfo.GoalLineX))
             {
                 if (IsValidGoalAttempt(state.Ball, crossInfo.GoalLineX == 0f))
                 {
                     ActionResult goalResult = new ActionResult { Outcome = ActionResultOutcome.Goal, PrimaryPlayer = state.Ball.LastShooter, ImpactPosition = CoordinateUtils.To2DGround(intersectPoint) };
                     eventHandler.HandleActionResult(goalResult, state); // Use handler
                     return true;
                 }
             }

              ActionResult oobResult = new ActionResult { Outcome = ActionResultOutcome.OutOfBounds, ImpactPosition = CoordinateUtils.To2DGround(intersectPoint), PrimaryPlayer = state.Ball.LastTouchedByPlayer };
              eventHandler.HandleOutOfBounds(oobResult, state, intersectPoint); // Use handler
             return true;
         }

         private bool CheckSideLineCrossing(MatchState state, IMatchEventHandler eventHandler)
         {
             // --- Logic copied from MatchSimulator ---
             Vector3 currentBallPos = state.Ball.Position;
             Vector3 prevBallPos = currentBallPos - state.Ball.Velocity * PHYSICS_STEP_SIZE;

             bool crossedBottomLine = (prevBallPos.z >= 0f && currentBallPos.z < 0f) || (prevBallPos.z < 0f && currentBallPos.z >= 0f);
             bool crossedTopLine = (prevBallPos.z <= _geometry.PitchWidth && currentBallPos.z > _geometry.PitchWidth) ||
                                   (prevBallPos.z > _geometry.PitchWidth && currentBallPos.z <= _geometry.PitchWidth);

             if (crossedBottomLine || crossedTopLine) {
                 if (state.Ball.IsLoose || state.Ball.IsInFlight) {
                     if (Mathf.Abs(currentBallPos.z - prevBallPos.z) < SimConstants.FLOAT_EPSILON) return false;
                     float sidelineZ = crossedBottomLine ? 0f : _geometry.PitchWidth;
                     float intersectT = Mathf.Clamp01((sidelineZ - prevBallPos.z) / (currentBallPos.z - prevBallPos.z));
                     Vector3 intersectPoint = prevBallPos + (currentBallPos - prevBallPos) * intersectT;

                     ActionResult oobResult = new ActionResult {
                         Outcome = ActionResultOutcome.OutOfBounds,
                         ImpactPosition = CoordinateUtils.To2DGround(intersectPoint),
                         PrimaryPlayer = state.Ball.LastTouchedByPlayer
                     };
                     eventHandler.HandleOutOfBounds(oobResult, state, intersectPoint); // Use handler
                     return true;
                 }
             }
             return false;
         }

          // --- Helper Methods ---

         private struct GoalLineCrossInfo { public bool DidCross; public float GoalLineX; }

         private GoalLineCrossInfo DidCrossGoalLinePlane(Vector3 prevPos, Vector3 currentPos)
         {
            // --- Logic copied from MatchSimulator ---
             if ((prevPos.x >= 0f && currentPos.x < 0f) || (prevPos.x < 0f && currentPos.x >= 0f)) {
                 return new GoalLineCrossInfo { DidCross = true, GoalLineX = 0f };
             }
             if ((prevPos.x <= _geometry.PitchLength && currentPos.x > _geometry.PitchLength) || (prevPos.x > _geometry.PitchLength && currentPos.x <= _geometry.PitchLength)) {
                 return new GoalLineCrossInfo { DidCross = true, GoalLineX = _geometry.PitchLength };
             }
             return new GoalLineCrossInfo { DidCross = false, GoalLineX = 0f };
         }

         private bool IsWithinGoalBounds(Vector3 position, float goalLineX)
         {
            // --- Logic copied from MatchSimulator ---
             Vector3 goalCenter = Mathf.Approximately(goalLineX, 0f) ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
             bool withinWidth = Mathf.Abs(position.z - goalCenter.z) <= _geometry.GoalWidth / 2f;
             bool underCrossbar = position.y <= _geometry.GoalHeight + SimConstants.BALL_RADIUS;
             bool aboveGround = position.y >= SimConstants.BALL_RADIUS;
             return withinWidth && underCrossbar && aboveGround;
         }

         private bool IsValidGoalAttempt(SimBall ball, bool crossingHomeLine)
         {
            // --- Logic copied from MatchSimulator ---
             return ball.IsInFlight && ball.LastShooter != null &&
                    ((crossingHomeLine && ball.LastShooter.TeamSimId == 1) ||
                     (!crossingHomeLine && ball.LastShooter.TeamSimId == 0));
         }

        // --- Non-linear Utility Functions ---
        /// <summary>
        /// Sigmoid function: returns value between 0 and 1. Use for S-curve scaling.
        /// </summary>
        private static float Sigmoid(float x)
        {
            return 1f / (1f + Mathf.Exp(-x));
        }

        /// <summary>
        /// Power curve: raises input (0..1) to the given power. Use for gentle/harsh curve.
        /// </summary>
        private static float PowerCurve(float t, float power)
        {
            return Mathf.Pow(Mathf.Clamp01(t), power);
        }
    }
}