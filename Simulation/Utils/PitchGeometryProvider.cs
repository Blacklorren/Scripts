using HandballManager.Core;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.Utils 
{
    public class PitchGeometryProvider : IGeometryProvider
    {
        // Implement all properties and methods from IGeometryProvider
        // by copying the static PitchGeometry class content here.
        // Replace hardcoded values with references if moved to a config class.

        public float PitchWidth => 20f;
        public float PitchLength => 40f;
        public float GoalWidth => 3f;
        public float GoalHeight => 2f;
        public float GoalAreaRadius => 6f;
        public float FreeThrowLineRadius => 9f;
        public float SidelineBuffer => 1f; // Missing property from interface
        
        // Center property required by interface
        public Vector3 Center => new Vector3(PitchLength / 2f, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        
        // Renamed to match interface
        public Vector3 HomeGoalCenter3D => new Vector3(0f, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        public Vector3 AwayGoalCenter3D => new Vector3(PitchLength, SimConstants.BALL_RADIUS, PitchWidth / 2f);
        
        private float SevenMeterMarkX => 7f;
        public Vector3 HomePenaltySpot3D => new Vector3(SevenMeterMarkX, SimConstants.BALL_RADIUS, Center.z);
        public Vector3 AwayPenaltySpot3D => new Vector3(PitchLength - SevenMeterMarkX, SimConstants.BALL_RADIUS, Center.z);

        public Vector2 GetGoalCenter(int teamSimId)
        {
            Vector3 goalCenter3D = teamSimId == 0 ? HomeGoalCenter3D : AwayGoalCenter3D;
            return new Vector2(goalCenter3D.x, goalCenter3D.z);
        }

        public Vector2 GetOpponentGoalCenter(int teamSimId)
        {
            return GetGoalCenter(teamSimId == 0 ? 1 : 0);
        }

        /// <summary>
        /// Checks if a 2D position is inside the goal area (6m zone) for the specified goal.
        /// </summary>
        /// <param name="position">The 2D position to check (x = field length, y = field width).</param>
        /// <param name="checkHomeGoalArea">True to check the home goal area, false for away.</param>
        /// <returns>True if the position is inside the goal area, false otherwise.</returns>
        public bool IsInGoalArea(Vector2 position, bool checkHomeGoalArea)
        {
            Vector3 pos3D = new Vector3(position.x, SimConstants.BALL_RADIUS, position.y);
            return IsInGoalArea(pos3D, checkHomeGoalArea);
        }

        /// <summary>
        /// Checks if a 3D position is inside the goal area (6m zone) for the specified goal.
        /// </summary>
        /// <param name="position">The 3D position to check (x = field length, z = field width).</param>
        /// <param name="checkHomeGoalArea">True to check the home goal area, false for away.</param>
        /// <returns>True if the position is inside the goal area, false otherwise.</returns>
        public bool IsInGoalArea(Vector3 position, bool checkHomeGoalArea)
        {
            Vector3 goalCenter = checkHomeGoalArea ? HomeGoalCenter3D : AwayGoalCenter3D;
            float distSqXZ = (position.x - goalCenter.x) * (position.x - goalCenter.x) +
                             (position.z - goalCenter.z) * (position.z - goalCenter.z);
            return distSqXZ <= GoalAreaRadius * GoalAreaRadius;
        }
        /// <summary>
        /// Calculates a rerouted target position that avoids the 6m goal area if the direct path crosses it.
        /// </summary>
        /// <param name="from">Player's current position</param>
        /// <param name="to">Intended target position</param>
        /// <param name="teamSimId">Team ID (0=home, 1=away)</param>
        /// <returns>Adjusted target position that avoids the goal area if necessary</returns>
        public static Vector2 CalculatePathAroundGoalArea(Vector2 from, Vector2 to, int teamSimId, PitchGeometryProvider pitchGeometry)
        {
            if (pitchGeometry == null)
                return to;
            // Check if the direct path crosses the goal area
            bool fromInArea = pitchGeometry.IsInGoalArea(from, teamSimId == 0);
            bool toInArea = pitchGeometry.IsInGoalArea(to, teamSimId == 0);
            if (!fromInArea && !toInArea)
            {
                // If the line crosses the area, reroute
                Vector3 goalCenter = teamSimId == 0 ? pitchGeometry.HomeGoalCenter3D : pitchGeometry.AwayGoalCenter3D;
                float r = pitchGeometry.GoalAreaRadius + 0.2f; // Small buffer
                // Direction from goal center to 'to'
                Vector2 dir = (to - new Vector2(goalCenter.x, goalCenter.z)).normalized;
                // Place target just outside the 6m area
                Vector2 reroute = new Vector2(goalCenter.x, goalCenter.z) + dir * r;
                // If the reroute is closer to 'from' than 'to', use it
                if ((reroute - from).sqrMagnitude < (to - from).sqrMagnitude)
                    return reroute;
            }
            // Otherwise, return original target
            return to;
        }
        /// <summary>
        /// Checks if the segment from 'from' to 'to' crosses the 6m goal area for the given team.
        /// </summary>
        /// <param name="from">Start position of the segment.</param>
        /// <param name="to">End position of the segment.</param>
        /// <param name="teamSimId">Team ID (0=home, 1=away).</param>
        /// <returns>True if the segment crosses the goal area, false otherwise.</returns>
        public bool WouldCrossGoalArea(Vector2 from, Vector2 to, int teamSimId)
        {
            Vector3 goalCenter3D = teamSimId == 0 ? HomeGoalCenter3D : AwayGoalCenter3D;
            Vector2 center = new Vector2(goalCenter3D.x, goalCenter3D.z);
            float r = GoalAreaRadius + 0.05f; // Small tolerance
            // Vector math for segment-circle intersection
            Vector2 d = to - from;
            Vector2 f = from - center;
            float a = Vector2.Dot(d, d);
            float b = 2 * Vector2.Dot(f, d);
            float c = Vector2.Dot(f, f) - r * r;
            float discriminant = b * b - 4 * a * c;
            if (discriminant < 0)
                return false; // No intersection
            discriminant = Mathf.Sqrt(discriminant);
            float t1 = (-b - discriminant) / (2 * a);
            float t2 = (-b + discriminant) / (2 * a);
            // Check if intersection occurs within the segment
            return (t1 >= 0 && t1 <= 1) || (t2 >= 0 && t2 <= 1);
        }
    }
}