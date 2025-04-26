using System;
using System.Collections.Generic;
using UnityEngine;
using HandballManager.Simulation.Engines;
using HandballManager.Core;
using System.Linq;

namespace HandballManager.Simulation.Events.Calculators
{
    /// <summary>
    /// Handles block detection, probability calculation, and outcome resolution for defensive blocks.
    /// </summary>
    public class BlockCalculator
    {
        public enum BlockTimingCategory { Perfect, Early, Late, None }
        public enum BlockOutcomeType { None, DeflectedLoose, CaughtByBlocker, OutOfBounds, ToTeammate }
        public struct BlockResult
        {
            public bool Blocked;
            public bool Partial;
            public float Effectiveness; // 0-100%
            public bool Foul;
            public SimPlayer Blocker;
            public Vector3 DeflectionDirection;
            public BlockTimingCategory Timing;
            public string Reason;
            // Visual feedback triggers
            public bool IsJumpingBlock;
            public bool IsPassiveBlock;
            public readonly bool IsFullBlock => Blocked && !Partial;
            // Extended outcome
            public BlockOutcomeType OutcomeType;
            public SimPlayer PossessionPlayer;
        }

        // Entry point for block logic
        public BlockResult TryBlockShot(SimPlayer shooter, MatchState state, ShotContext shotContext)
        {
            var defenders = state.GetOpposingTeamOnCourt(shooter.TeamSimId);
            if (defenders == null || defenders.Count == 0)
                return new BlockResult { Blocked = false, Partial = false, Effectiveness = 0f, Reason = "No defenders" };

            float shotReleaseTime = shotContext.ReleaseTime;
            Vector3 shotOrigin = shotContext.ShotOrigin;
            Vector3 shotDir = shotContext.ShotDirection.normalized;
            float shotSpeed = shotContext.ShotSpeed;
            float shotDeception = shotContext.ShotDeception;
            float shotHeight = shotContext.ShotHeight;

            // Parameters for tuning realism/playability
            const float BLOCK_RADIUS = 2.0f; // meters for active block
            const float PASSIVE_BLOCK_RADIUS = 1.5f; // meters for passive block
            const float BLOCK_CONE_ANGLE_DEG = 40f; // cone angle for active block
            const float PASSIVE_BLOCK_CONE_ANGLE_DEG = 70f;
            const float TIMING_WINDOW = 0.45f; // seconds, base window for perfect timing
            const float FOUL_BASE_CHANCE = 0.1f;
            const float FOUL_LATE_BONUS = 0.25f;
            const float EFFECTIVENESS_FULL_BLOCK = 0.85f;
            const float RECOVERY_PENALTY = 0.15f; // agility reduction
            const float RECOVERY_DURATION = 1.2f; // seconds
            const float JUMP_HEIGHT_BONUS = 0.2f; // bonus for jumping blocks
            const float VERTICAL_BLOCK_TOLERANCE = 0.4f; // meters
            const float PASSIVE_BLOCK_PROB = 0.10f; // base passive block chance
            const float PASSIVE_BLOCK_EFFECTIVENESS = 0.3f;

            List<(SimPlayer, float, bool, bool, float, BlockTimingCategory, bool)> blockAttempts = new();
            List<(SimPlayer, float)> passiveAttempts = new();

            float now = Time.time;
            // --- Active Blockers (Standing & Jumping) ---
            foreach (var defender in defenders)
            {
                if (defender == null || defender.BaseData == null || defender.BaseData.CurrentInjuryStatus != InjuryStatus.Healthy)
                    continue;
                // Only consider defenders actively attempting a block
                if (defender.CurrentAction != PlayerAction.AttemptingBlock)
                    continue;
                Vector2 defenderPos2D = defender.Position;
                Vector2 shotOrigin2D = new(shotOrigin.x, shotOrigin.z);
                float dist = Vector2.Distance(defenderPos2D, shotOrigin2D);
                // --- Active block: must be within cone and radius ---
                if (dist > BLOCK_RADIUS)
                    continue;
                Vector3 defenderToShot = (shotOrigin - new Vector3(defender.Position.x, shotHeight, defender.Position.y)).normalized;
                float angleCos = Vector3.Dot(defenderToShot, shotDir);
                float coneCos = Mathf.Cos(BLOCK_CONE_ANGLE_DEG * Mathf.Deg2Rad);
                if (angleCos < coneCos)
                    continue;
                // --- Vertical overlap check ---
                float defenderVertical = defender.IsJumping ? defender.VerticalPosition : 0f;
                bool verticalOverlap = Mathf.Abs(defenderVertical - shotHeight) < VERTICAL_BLOCK_TOLERANCE;
                bool isJumpingBlock = defender.IsJumping && verticalOverlap;
                bool isStandingBlock = !defender.IsJumping && Mathf.Abs(shotHeight) < (defender.BaseData.Height / 100f + VERTICAL_BLOCK_TOLERANCE);
                if (!isJumpingBlock && !isStandingBlock)
                    continue;
                // --- Timing window (reaction) ---
                float reaction = defender.BaseData.Reaction > 0 ? defender.BaseData.Reaction : 50f;
                float timingAdj = Mathf.Lerp(0.7f, 1.3f, 1f - (reaction / 100f));
                float defenderAttemptTime = shotReleaseTime + (dist / (shotSpeed + 0.01f)) * timingAdj;
                float timingDelta = Mathf.Abs(now - defenderAttemptTime);
                BlockTimingCategory timingCat = BlockTimingCategory.None;
                if (timingDelta <= TIMING_WINDOW * 0.5f) timingCat = BlockTimingCategory.Perfect;
                else if (now < defenderAttemptTime) timingCat = BlockTimingCategory.Early;
                else if (now > defenderAttemptTime) timingCat = BlockTimingCategory.Late;
                else timingCat = BlockTimingCategory.None;
                // --- Block probability ---
                float blockSkill = defender.BaseData.Blocking; // Block probability modified by BaseData.Blocking
                float height = defender.BaseData.Height;       // Block probability modified by BaseData.Height
                float jumping = defender.BaseData.Jumping;     // Block probability modified by BaseData.Jumping
                float agility = defender.BaseData.Agility;     // Block probability modified by BaseData.Agility
                float bravery = defender.BaseData.Bravery;     // Block probability modified by BaseData.Bravery
                float positioning = defender.BaseData.Positioning; // Block probability modified by BaseData.Positioning
                float reflexes = defender.BaseData.Reflexes;   // Block probability modified by BaseData.Reflexes (especially for GK/last-ditch blocks)
                float workRate = defender.BaseData.WorkRate;   // Block probability modified by BaseData.WorkRate (effort/coverage)
                float determination = defender.BaseData.Determination; // Block probability modified by BaseData.Determination (contest under pressure)

                // Weighted sum: Blocking (30%), Height (10%), Jumping (10%), Agility (10%), Bravery (10%), Positioning (10%), Reflexes (10%), WorkRate (5%), Determination (5%)
                float blockScore =
                    blockSkill * 0.30f +
                    height * 0.10f +
                    jumping * 0.10f +
                    agility * 0.10f +
                    bravery * 0.10f +
                    positioning * 0.10f +
                    reflexes * 0.10f +
                    workRate * 0.05f +
                    determination * 0.05f; // All attributes above contribute to block probability

                float deceptionPenalty = Mathf.Clamp01(shotDeception / 100f) * 0.25f;
                float anglePenalty = 1f - angleCos;
                float distPenalty = Mathf.Clamp01(dist / BLOCK_RADIUS) * 0.2f;
                float baseProb = blockScore / 100f;
                float timingBonus = (timingCat == BlockTimingCategory.Perfect) ? 0.15f : (timingCat == BlockTimingCategory.Early ? -0.07f : (timingCat == BlockTimingCategory.Late ? -0.12f : 0f));
                float jumpBonus = isJumpingBlock ? JUMP_HEIGHT_BONUS : 0f;
                float blockProb = baseProb + timingBonus + jumpBonus - deceptionPenalty - anglePenalty - distPenalty;
                blockProb = Mathf.Clamp01(blockProb);
                // Foul risk
                float foulChance = FOUL_BASE_CHANCE;
                if (timingCat == BlockTimingCategory.Late) foulChance += FOUL_LATE_BONUS;
                if (Mathf.Abs(angleCos) < 0.5f) foulChance += 0.15f;
                bool foul = UnityEngine.Random.value < foulChance * (1f - (bravery / 120f));
                // Recovery penalty
                defender.BaseData.ApplyTemporaryAgilityReduction(RECOVERY_PENALTY, RECOVERY_DURATION);
                blockAttempts.Add((defender, blockProb, isJumpingBlock, foul, angleCos, timingCat, false));
            }

            // --- Passive Blockers ---
            foreach (var defender in defenders)
            {
                if (defender == null || defender.BaseData == null || defender.BaseData.CurrentInjuryStatus != InjuryStatus.Healthy)
                    continue;
                Vector2 defenderPos2D = defender.Position;
                Vector2 shotOrigin2D = new(shotOrigin.x, shotOrigin.z);
                float dist = Vector2.Distance(defenderPos2D, shotOrigin2D);
                if (dist > PASSIVE_BLOCK_RADIUS)
                    continue;
                Vector3 defenderToShot = (shotOrigin - new Vector3(defender.Position.x, shotHeight, defender.Position.y)).normalized;
                float angleCos = Vector3.Dot(defenderToShot, shotDir);
                float coneCos = Mathf.Cos(PASSIVE_BLOCK_CONE_ANGLE_DEG * Mathf.Deg2Rad);
                if (angleCos < coneCos)
                    continue;
                // Exclude defenders already in active list
                bool alreadyActive = blockAttempts.Exists(b => b.Item1 == defender);
                if (alreadyActive)
                    continue;
                passiveAttempts.Add((defender, PASSIVE_BLOCK_PROB));
            }

            // --- Cumulative Probability for Multiple Blockers ---
            float cumulativeProb = 1f;
            foreach (var attempt in blockAttempts)
            {
                cumulativeProb *= (1f - attempt.Item2);
            }
            float finalBlockProb = 1f - cumulativeProb;
            // Add passive blockers
            foreach (var attempt in passiveAttempts)
            {
                finalBlockProb = 1f - (1f - finalBlockProb) * (1f - attempt.Item2);
            }
            // Roll for block
            float roll = UnityEngine.Random.value;
            bool isBlocked = roll < finalBlockProb * EFFECTIVENESS_FULL_BLOCK;
            bool isPartial = !isBlocked && roll < finalBlockProb;
            // Determine which defender succeeded (weighted random by effectiveness)
            BlockResult result = new() { Blocked = false, Partial = false, Effectiveness = 0f, Timing = BlockTimingCategory.None, Reason = "No block attempt" };
            if (isBlocked || isPartial)
            {
                System.Random rng = new();
                // Active block: pick highest effectiveness or weighted
                if (blockAttempts.Count > 0)
                {
                    var sorted = blockAttempts.OrderByDescending(b => b.Item2).ToList();
                    var winner = sorted[0];
                    result.Blocked = isBlocked;
                    result.Partial = isPartial;
                    result.Effectiveness = isBlocked ? 1f : PASSIVE_BLOCK_EFFECTIVENESS;
                    result.Foul = winner.Item4;
                    result.Blocker = winner.Item1;
                    result.DeflectionDirection = isBlocked ? -shotDir : (shotDir + UnityEngine.Random.insideUnitSphere * 0.5f).normalized;
                    result.Timing = winner.Item6;
                    result.IsJumpingBlock = winner.Item3;
                    result.IsPassiveBlock = false;
                    result.Reason = isBlocked ? (winner.Item3 ? "Jumping block" : "Standing block") : "Partial block/deflection";
                    // Determine block outcome and possession
                    if (isBlocked)
                    {
                        float catchChance = 0.3f + (winner.Item2 * 0.5f); // Higher blockProb = more likely to catch
                        float outChance = 0.1f;
                        float toTeammateChance = 0.2f;
                        float roll2 = UnityEngine.Random.value;
                        if (roll2 < catchChance)
                        {
                            result.OutcomeType = BlockOutcomeType.CaughtByBlocker;
                            result.PossessionPlayer = winner.Item1;
                        }
                        else if (roll2 < catchChance + outChance)
                        {
                            result.OutcomeType = BlockOutcomeType.OutOfBounds;
                            result.PossessionPlayer = null;
                        }
                        else if (roll2 < catchChance + outChance + toTeammateChance)
                        {
                            result.OutcomeType = BlockOutcomeType.ToTeammate;
                            // Pick a random teammate (excluding self)
                            var teammates = defenders.Where(d => d != winner.Item1).ToList();
                            if (teammates.Count > 0)
                                result.PossessionPlayer = teammates[rng.Next(teammates.Count)];
                            else
                                result.PossessionPlayer = null;
                        }
                        else
                        {
                            result.OutcomeType = BlockOutcomeType.DeflectedLoose;
                            result.PossessionPlayer = null;
                        }
                    }
                    else // partial
                    {
                        result.OutcomeType = BlockOutcomeType.DeflectedLoose;
                        result.PossessionPlayer = null;
                    }
                }
                else if (passiveAttempts.Count > 0)
                {
                    var winner = passiveAttempts[UnityEngine.Random.Range(0, passiveAttempts.Count)];
                    result.Blocked = false;
                    result.Partial = true;
                    result.Effectiveness = PASSIVE_BLOCK_EFFECTIVENESS;
                    result.Foul = false;
                    result.Blocker = winner.Item1;
                    result.DeflectionDirection = (shotDir + UnityEngine.Random.insideUnitSphere * 0.7f).normalized;
                    result.Timing = BlockTimingCategory.None;
                    result.IsJumpingBlock = false;
                    result.IsPassiveBlock = true;
                    result.Reason = "Passive block (marking proximity)";
                    result.OutcomeType = BlockOutcomeType.DeflectedLoose;
                    result.PossessionPlayer = null;
                }
            }
            else
            {
                result.Reason = "Block missed";
                result.OutcomeType = BlockOutcomeType.None;
                result.PossessionPlayer = null;
            }
            return result;
        }
    }

    // Context for shot, passed to block logic
    public struct ShotContext
    {
        public Vector3 ShotOrigin;
        public Vector3 ShotDirection;
        public float ShotSpeed;
        public float ShotHeight;
        public float ShotAngle;
        public float ShotDeception;
        public float ReleaseTime;
    }
}
