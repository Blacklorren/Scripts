using HandballManager.Core;
using HandballManager.Simulation.Engines;
using HandballManager.Simulation.Utils;
using UnityEngine;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Provides utility methods shared across action calculators.
    /// </summary>
    public static class ActionCalculatorUtils
    {
        private const float MAX_ANGLE_FOR_BEHIND_TACKLE = 75f;
        private const float MIN_TACKLE_APPROACH_ANGLE = 0.5f;
        private const float CLEAR_CHANCE_DEFENDER_DIST = 2.5f;
        /// <summary>
        /// Calculates the pressure on a player from nearby opponents.
        /// </summary>
        public static float CalculatePressureOnPlayer(SimPlayer player, MatchState state)
        {
            float pressure = 0f;
            if (player == null || state == null) return 0f;
            // Corrected property access from TeamSimId to TeamId
            var opponents = state?.GetOpposingTeamOnCourt(player?.TeamId ?? -1);
            if (opponents == null) return 0f;

            foreach(var opponent in opponents) {
                if (opponent == null || opponent.IsSuspended()) continue;
                float dist = Vector2.Distance(player.Position, opponent.Position);
                if (dist < ActionResolverConstants.MAX_PRESSURE_DIST) {
                    // Pressure increases more sharply when very close
                    pressure += Mathf.Pow(1.0f - (dist / ActionResolverConstants.MAX_PRESSURE_DIST), 2);
                }
            }
            return Mathf.Clamp01(pressure); // Max pressure = 1.0
        }

        /// <summary>
        /// Converts a player's 2D position to 3D at standard ball height.
        /// </summary>
        public static Vector3 GetPosition3D(SimPlayer player)
        {
             if (player?.Position == null) return new Vector3(0, SimConstants.BALL_DEFAULT_HEIGHT, 0);
             // Use SimConstants for ball height
             return new Vector3(player.Position.x, SimConstants.BALL_DEFAULT_HEIGHT, player.Position.y);
        }

        /// <summary>
        /// Checks if a tackle attempt is coming significantly from behind a moving target.
        /// </summary>
        public static bool IsTackleFromBehind(SimPlayer tackler, SimPlayer target) {
             if (tackler == null || target == null) return false;
             // Check if target is moving by checking the magnitude of CurrentMovementDirection
             if (target.CurrentMovementDirection.sqrMagnitude < 0.0001f) return false; // Target needs to be moving

             Vector2 targetMovementDir = target.CurrentMovementDirection;
             Vector2 approachDir = (target.Position - tackler.Position);
             if(approachDir.sqrMagnitude < ActionResolverConstants.MIN_DISTANCE_CHECK_SQ) return false; // Avoid issues if overlapping
             approachDir.Normalize();

             // Angle between approach vector and *opposite* of target movement
             float angle = Vector2.Angle(approachDir, -targetMovementDir);
             return angle < MAX_ANGLE_FOR_BEHIND_TACKLE; // Considered 'behind' if approach is within 75 degrees of the opposite direction
         }

         /// <summary>
         /// Calculates the closing speed between two players along the axis connecting them.
         /// Positive value means they are getting closer.
         /// </summary>
         public static float CalculateClosingSpeed(SimPlayer playerA, SimPlayer playerB) {
              if (playerA == null || playerB == null) return 0f;
              Vector2 relativeVelocity = playerA.CurrentMovementDirection - playerB.CurrentMovementDirection;
              Vector2 axisToTarget = (playerB.Position - playerA.Position);
              if (axisToTarget.sqrMagnitude < ActionResolverConstants.MIN_DISTANCE_CHECK_SQ) return 0f;

              // Project relative velocity onto the *opposite* of the axis connecting them
              return Vector2.Dot(relativeVelocity, -axisToTarget.normalized);
         }

         /// <summary>
         /// Determines if the target player is currently in a clear scoring chance situation.
         /// </summary>
         /// <param name="target">The player being evaluated</param>
         /// <param name="state">Current match state</param>
         /// <returns>True if considered a clear scoring opportunity</returns>
         public static bool IsClearScoringChance(SimPlayer target, MatchState state) {
             // Assumes _geometry is accessible via state or injected if needed, using constants for now
             if (target is not { HasBall: true } || state is null) return false;

             Vector2 opponentGoal = (target.TeamSimId == 0)
                 ? new Vector2(ActionResolverConstants.PITCH_LENGTH, ActionResolverConstants.PITCH_CENTER_Y) // Use constants
                 : new Vector2(0f, ActionResolverConstants.PITCH_CENTER_Y);
             float distToGoal = Vector2.Distance(target.Position, opponentGoal);

             // Check 1: Within scoring range? (e.g., inside 12m)
             if (distToGoal > ActionResolverConstants.FREE_THROW_LINE_RADIUS + 3f) return false;

             // Check 2: Reasonable angle/central position?
             float maxAngleOffset = Mathf.Lerp(6f, 9f, Mathf.Clamp01(distToGoal / (ActionResolverConstants.FREE_THROW_LINE_RADIUS + 3f)));
             if (Mathf.Abs(target.Position.y - ActionResolverConstants.PITCH_CENTER_Y) > maxAngleOffset) return false;

             // Check 3: Number of defenders significantly obstructing the path
             int defendersBlocking = 0;
             var opponents = state.GetOpposingTeamOnCourt(target.TeamSimId);
             if (opponents == null) return true; // If no opponents, it's clear

             Vector2 targetToGoalVec = opponentGoal - target.Position;
             if (targetToGoalVec.sqrMagnitude < ActionResolverConstants.MIN_DISTANCE_CHECK_SQ) return false;

             foreach(var opp in opponents) {
                 if (opp == null || opp.IsGoalkeeper() || opp.IsSuspended()) continue;
                 Vector2 targetToOppVec = opp.Position - target.Position;
                 float oppDistToGoal = Vector2.Distance(opp.Position, opponentGoal);

                 float dot = Vector2.Dot(targetToOppVec.normalized, targetToGoalVec.normalized);
                 if (dot > 0.5f && oppDistToGoal < distToGoal * 1.1f)
                 {
                       // Use Simulation.Utils for distance to line
                       float distToLine = SimulationUtils.CalculateDistanceToLine(opp.Position, target.Position, opponentGoal);
                       if (distToLine < CLEAR_CHANCE_DEFENDER_DIST) // Wider cone check
                       {
                            defendersBlocking++;
                       }
                 }
             }
             return defendersBlocking <= 1; // Clear chance if 0 or 1 field defender is potentially obstructing
         }

        /// <summary>
        /// Calculates how "open" a player is (not marked by defenders).
        /// Returns a value from 0.0 (heavily marked) to 1.0 (completely open).
        /// </summary>
        /// <param name="player">The player whose openness is being evaluated.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>Openness score between 0.0 (not open) and 1.0 (completely open).</returns>
        public static float CalculatePlayerOpenness(SimPlayer player, MatchState state)
        {
            if (player == null || state == null)
            {
                Debug.LogWarning("CalculatePlayerOpenness called with null player or state.");
                return 0f;
            }
            var opponents = state.GetOpposingTeamOnCourt(player.TeamSimId);
            if (opponents == null)
            {
                Debug.LogWarning("CalculatePlayerOpenness: No opponents found for player " + player.TeamSimId);
                return 1f; // No defenders, fully open
            }
            float minDistSqr = float.MaxValue;
            int defendersInRadius = 0;
            foreach (var defender in opponents)
            {
                if (defender == null || defender.IsSuspended() || defender.IsGoalkeeper()) continue;
                float distSqr = (player.Position - defender.Position).sqrMagnitude;
                if (distSqr < ActionResolverConstants.OPENNESS_DEFENDER_RADIUS * ActionResolverConstants.OPENNESS_DEFENDER_RADIUS)
                {
                    defendersInRadius++;
                    if (distSqr < minDistSqr)
                        minDistSqr = distSqr;
                }
            }
            // If no defenders in radius, fully open
            if (defendersInRadius == 0)
                return 1f;
            // Openness decreases with more/closer defenders
            float distFactor = Mathf.Clamp01(Mathf.Sqrt(minDistSqr) / ActionResolverConstants.OPENNESS_DEFENDER_RADIUS);
            float defenderFactor = 1f / (defendersInRadius + 1f); // +1 to avoid div by zero
            // Optionally: factor in position relative to goal/ball here
            float openness = distFactor * defenderFactor;
            openness = Mathf.Clamp01(openness);
            if (openness < 0.01f)
                Debug.Log($"Player {player.GetPlayerId()} is heavily marked (openness={openness})");
            return openness;
        }
    }
}