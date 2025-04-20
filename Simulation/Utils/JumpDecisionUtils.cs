using UnityEngine;
using HandballManager.Simulation.Engines;
using HandballManager.Core; // For PlayerPosition enum

namespace HandballManager.Simulation.Utils
{
    /// <summary>
    /// Utility class for making jump decisions for shots and blocks in handball simulation.
    /// </summary>
    public static class JumpDecisionUtils
    {
        /// <summary>
        /// Determines if a player should jump for a shot based on their data and match state.
        /// </summary>
        public static bool ShouldJumpForShot(SimPlayer playerData, MatchState state, HandballManager.Simulation.AI.Evaluation.IGameStateEvaluator evaluator)
        {
            if (playerData == null || state == null)
                return false;
            if (evaluator == null)
                throw new System.ArgumentNullException(nameof(evaluator), "IGameStateEvaluator must be provided to ShouldJumpForShot.");

            var pos = playerData.BaseData?.PrimaryPosition ?? PlayerPosition.Goalkeeper; // Default to Goalkeeper if null
            bool hasStamina = playerData.Stamina > 0.2f;
            if (playerData.CurrentAction == PlayerAction.Jumping || !hasStamina)
                return false;

            // Detect counter-attack
            bool isCounterAttack = evaluator.IsCounterAttackOpportunity(playerData, state);

            // Use geometry for distance
            PitchGeometryProvider geometryProvider = new PitchGeometryProvider();
            Vector2 goalPosition = geometryProvider.GetOpponentGoalCenter(playerData.TeamSimId);
            float distanceToGoal = Vector2.Distance(playerData.Position, goalPosition);

            // --- 1. Wing (Ailier) ---
            if (PlayerPositionHelper.IsWing(pos))
            {
                return !isCounterAttack; // Always jump unless counter-attack
            }

            // --- 2. Backcourt (Arrière) ---
            if (PlayerPositionHelper.IsBack(pos))
            {
                int defendersBetween = CountDefendersBetweenShooterAndGoal(playerData, state, goalPosition);
                float jumpTendency = (playerData.BaseData?.Jumping ?? 50) + (playerData.BaseData?.DecisionMaking ?? 50);
                // Saut si loin du but (>9m) ou défenseurs présents, pondéré par attributs
                if (distanceToGoal > 9.0f || defendersBetween > 0)
                {
                    return jumpTendency > 80; // Seuil ajustable
                }
                else
                {
                    return false; // Tir en appui sinon
                }
            }

            // --- 3. Autres postes : logique par défaut (ex : pivot, demi-centre, etc.) ---
            return false;
        }

        // Overload for legacy calls (throws for now)
        public static bool ShouldJumpForShot(SimPlayer playerData, MatchState state)
        {
            throw new System.InvalidOperationException("ShouldJumpForShot now requires an IGameStateEvaluator argument. Update all usages to provide the evaluator.");
        }

        /// <summary>
        /// Helper: Counts number of defenders between shooter and goal (simplified, can be improved)
        /// </summary>
        private static int CountDefendersBetweenShooterAndGoal(SimPlayer shooter, MatchState state, Vector2 goalPosition)
        {
            if (state == null || shooter == null) return 0;
            int count = 0;
            foreach (var player in state.GetOpposingTeamOnCourt(shooter.TeamSimId))
            {
                // Simplified: check if player is roughly between shooter and goal (bounding box)
                float minX = Mathf.Min(shooter.Position.x, goalPosition.x);
                float maxX = Mathf.Max(shooter.Position.x, goalPosition.x);
                float minY = Mathf.Min(shooter.Position.y, goalPosition.y);
                float maxY = Mathf.Max(shooter.Position.y, goalPosition.y);
                var pos = player.Position;
                if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Determines if a player should jump to block based on their data and match state.
        /// </summary>
        public static bool ShouldJumpForBlock(SimPlayer playerData, MatchState state)
        {
            if (playerData == null || state == null)
                return false;

            // Example logic: Jump to block if close to the shooter and not already jumping
            SimPlayer shooter = state.Ball.LastShooter;
            if (shooter == null || shooter.BaseData == null)
                return false;

            float distanceToShooterSq = (playerData.Position - shooter.Position).sqrMagnitude;
            bool isInBlockRange = distanceToShooterSq < 4.0f; // 2m block range, squared
            bool hasStamina = playerData.Stamina > 0.1f;
            return isInBlockRange && playerData.CurrentAction != PlayerAction.Jumping && hasStamina;
        }
    }
}
