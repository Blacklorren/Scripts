using UnityEngine;
using HandballManager.Core; // For Enums like PlayerPosition, DefensiveSystem
using HandballManager.Gameplay; // For Tactic class
using System; // For Math
using HandballManager.Simulation.Utils;
using HandballManager.Simulation.Engines;
using System.Linq; // For IGeometryProvider

namespace HandballManager.Simulation.AI.Positioning // Updated from Engines to AI
{
    /// <summary>
    /// Calculates the target tactical position for players based on team tactics,
    /// player role, game phase, ball position, and player attributes.
    /// Provides the "ideal spot" a player should aim for based on the tactical situation.
    /// Note: Current implementation uses hardcoded formations; a future improvement would be data-driven formations.
    /// </summary>
    public class TacticPositioner : ITacticPositioner // Implement the interface
    {
        /// <summary>
        /// Returns a basic screen spot for the screener (e.g., pivot) to set a screen for a target teammate.
        /// This is a simple implementation: places the screener between the defender and the teammate, offset by a small distance.
        /// </summary>
        public Vector2 GetScreenSpotForScreener(SimPlayer screener, SimPlayer targetTeammate, SimPlayer defender, float offset = 0.7f)
        {
            if (screener == null || targetTeammate == null || defender == null)
                return screener?.Position ?? Vector2.zero;
            // Vector from defender to teammate
            Vector2 dir = (targetTeammate.Position - defender.Position).normalized;
            // Place screener just in front of defender, towards teammate
            return defender.Position + dir * offset;
        }

        /// <summary>
        /// Returns a spot for the screen user to use the screen, i.e., the optimal spot to run around the screener.
        /// This is a basic implementation that places the user slightly offset from the screener, towards the goal.
        /// </summary>
        public Vector2 GetScreenSpotForUser(SimPlayer screener, SimPlayer user, Vector2 goalPos, float offset = 1.0f)
        {
            if (screener == null || user == null)
                return user?.Position ?? Vector2.zero;
            // Vector from screener to goal
            Vector2 dir = (goalPos - screener.Position).normalized;
            // Place user just past the screener, towards the goal
            return screener.Position + dir * offset;
        }

        /// <summary>
        /// Returns the angle (in degrees) between defender, screener, and screen user (for evaluating screen effectiveness).
        /// </summary>
        public float GetScreenAngleBetweenDefenderAndTarget(SimPlayer defender, SimPlayer screener, SimPlayer user)
        {
            if (defender == null || screener == null || user == null)
                return 0f;
            Vector2 a = (screener.Position - defender.Position).normalized;
            Vector2 b = (user.Position - screener.Position).normalized;
            float dot = Vector2.Dot(a, b);
            return Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
        }

        private readonly IGeometryProvider _geometry;
        public IGeometryProvider Geometry => _geometry;
        
        // Constructor to inject the geometry provider
        public TacticPositioner(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        // --- Special Situation Positioning ---
        /// <summary>
        /// Positions players for a free throw situation.
        /// </summary>
        public void PositionForFreeThrow(MatchState state)
        {
            if (state == null) return;
            int attackingTeamId = state.PossessionTeamId;
            int defendingTeamId = (attackingTeamId == 0) ? 1 : 0;
            var attackers = state.GetTeamOnCourt(attackingTeamId);
            var defenders = state.GetTeamOnCourt(defendingTeamId);
            if (attackers == null || defenders == null) return;
            Vector2 freeThrowSpot = new(state.Ball.Position.x, state.Ball.Position.z);
            float freeThrowRadius = _geometry.FreeThrowLineRadius;
            float arcBuffer = 0.2f; // Small buffer to ensure just outside arc
            float minDefenderDist = 3.0f;

            // 1. Place the thrower at the free throw spot
            SimPlayer thrower = state.Ball.Holder ?? attackers.FirstOrDefault(p => !p.IsSuspended());
            if (thrower != null)
            {
                thrower.Position = freeThrowSpot;
                thrower.TargetPosition = freeThrowSpot;
            }

            // 2. Place other attackers just outside the 9m arc (not inside), spread evenly
            var otherAttackers = attackers.Where(p => p != thrower && !p.IsSuspended()).ToList();
            for (int i = 0; i < otherAttackers.Count; i++)
            {
                float angle = (2 * Mathf.PI * i) / otherAttackers.Count;
                Vector2 pos = freeThrowSpot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (freeThrowRadius + arcBuffer);
                otherAttackers[i].Position = pos;
                otherAttackers[i].TargetPosition = pos;
            }

            // 3. Place defenders outside both the 9m arc and at least 3m from thrower, spread evenly
            float defenderRadius = freeThrowRadius + minDefenderDist;
            for (int i = 0; i < defenders.Count; i++)
            {
                if (defenders[i] == null || defenders[i].IsSuspended()) continue;
                float angle = (2 * Mathf.PI * i) / defenders.Count + Mathf.PI / defenders.Count;
                Vector2 pos = freeThrowSpot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * defenderRadius;
                defenders[i].Position = pos;
                defenders[i].TargetPosition = pos;
            }
        }

        /// <summary>
        /// Positions players for a penalty situation.
        /// </summary>
        public void PositionForPenalty(MatchState state)
        {
            if (state == null) return;
            int attackingTeamId = state.PossessionTeamId;
            int defendingTeamId = (attackingTeamId == 0) ? 1 : 0;
            var attackers = state.GetTeamOnCourt(attackingTeamId);
            var defenders = state.GetTeamOnCourt(defendingTeamId);
            if (attackers == null || defenders == null) return;
            // Penalty spot
            Vector2 penaltySpot = (attackingTeamId == 0)
                ? new Vector2(_geometry.HomePenaltySpot3D.x, _geometry.HomePenaltySpot3D.z)
                : new Vector2(_geometry.AwayPenaltySpot3D.x, _geometry.AwayPenaltySpot3D.z);
            float nineMeterRadius = _geometry.FreeThrowLineRadius;
            // 1. Shooter (choose ball holder or first non-GK attacker)
            SimPlayer shooter = state.Ball.Holder ?? attackers.FirstOrDefault(p => !p.IsGoalkeeper() && !p.IsSuspended());
            if (shooter != null)
            {
                shooter.Position = penaltySpot;
                shooter.TargetPosition = penaltySpot;
            }
            // 2. Defending goalkeeper on goal line
            SimPlayer gk = state.GetGoalkeeper(defendingTeamId);
            if (gk != null)
            {
                Vector2 goalCenter = _geometry.GetGoalCenter(defendingTeamId);
                gk.Position = goalCenter;
                gk.TargetPosition = goalCenter;
            }
            // 3. All other players outside the 9m line (free throw arc), spread evenly
            var nonShooters = attackers.Where(p => p != shooter && !p.IsSuspended()).Concat(defenders.Where(p => !p.IsGoalkeeper() && !p.IsSuspended())).ToList();
            for (int i = 0; i < nonShooters.Count; i++)
            {
                float angle = (2 * Mathf.PI * i) / nonShooters.Count;
                Vector2 pos = penaltySpot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (nineMeterRadius + 0.5f);
                nonShooters[i].Position = pos;
                nonShooters[i].TargetPosition = pos;
            }
        }

        /// <summary>
        /// Positions players for a kickoff situation.
        /// </summary>
        public void PositionForKickOff(MatchState state)
        {
            if (state == null) return;
            Vector2 center = new(_geometry.Center.x, _geometry.Center.z);
            var home = state.GetTeamOnCourt(0);
            var away = state.GetTeamOnCourt(1);
            if (home == null || away == null) return;
            int kickoffTeamId = state.PossessionTeamId;
            var kickoffTeam = state.GetTeamOnCourt(kickoffTeamId);
            var otherTeam = state.GetTeamOnCourt((kickoffTeamId == 0) ? 1 : 0);
            if (kickoffTeam == null || otherTeam == null) return;

            // Find pivot and center back
            SimPlayer pivot = kickoffTeam.FirstOrDefault(p => p.BaseData?.PrimaryPosition == PlayerPosition.Pivot && !p.IsSuspended());
            SimPlayer centerBack = kickoffTeam.FirstOrDefault(p => p.BaseData?.PrimaryPosition == PlayerPosition.CentreBack && !p.IsSuspended());
            // Fallback: if no pivot, use first available
            pivot ??= kickoffTeam.FirstOrDefault(p => !p.IsSuspended());

            // 1. Place pivot at center
            if (pivot != null)
            {
                pivot.Position = center;
                pivot.TargetPosition = center;
            }
            // 2. Place center back 1-2m from center (random direction)
            if (centerBack != null && centerBack != pivot)
            {
                float dist = UnityEngine.Random.Range(1.0f, 2.0f);
                float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);
                Vector2 cbPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                centerBack.Position = cbPos;
                centerBack.TargetPosition = cbPos;
            }
            // 3. All other kickoff team players: in their own half, not at center
            float pitchHalf = _geometry.PitchLength / 2f;
            foreach (var player in kickoffTeam)
            {
                if (player == pivot || player == centerBack || player.IsSuspended()) continue;
                float x = (kickoffTeamId == 0)
                    ? UnityEngine.Random.Range(1.0f, pitchHalf - 1.0f)
                    : UnityEngine.Random.Range(pitchHalf + 1.0f, _geometry.PitchLength - 1.0f);
                float y = UnityEngine.Random.Range(2.0f, _geometry.PitchWidth - 2.0f);
                player.Position = new Vector2(x, y);
                player.TargetPosition = player.Position;
            }
            // 4. All opposing team players: in their own half, not at center
            foreach (var player in otherTeam)
            {
                if (player.IsSuspended()) continue;
                float x = (kickoffTeamId == 1)
                    ? UnityEngine.Random.Range(1.0f, pitchHalf - 1.0f)
                    : UnityEngine.Random.Range(pitchHalf + 1.0f, _geometry.PitchLength - 1.0f);
                float y = UnityEngine.Random.Range(2.0f, _geometry.PitchWidth - 2.0f);
                player.Position = new Vector2(x, y);
                player.TargetPosition = player.Position;
            }
        }

        /// <summary>
        /// Positions players for a throw-in situation.
        /// </summary>
        public void PositionForThrowIn(MatchState state)
        {
            if (state == null) return;
            // Ball is on the sideline
            Vector2 throwInSpot = new(state.Ball.Position.x, state.Ball.Position.z);
            int attackingTeamId = state.PossessionTeamId;
            int defendingTeamId = (attackingTeamId == 0) ? 1 : 0;
            var attackers = state.GetTeamOnCourt(attackingTeamId);
            var defenders = state.GetTeamOnCourt(defendingTeamId);
            if (attackers == null || defenders == null) return;
            // 1. Thrower: player holding ball or first available
            SimPlayer thrower = state.Ball.Holder ?? attackers.FirstOrDefault(p => !p.IsSuspended());
            if (thrower != null)
            {
                thrower.Position = throwInSpot;
                thrower.TargetPosition = throwInSpot;
            }
            // 2. Other players: must be at least 3m away, attackers spread along sideline, defenders mark zone
            float minDist = 3.0f;
            var otherAttackers = attackers.Where(p => p != thrower && !p.IsSuspended()).ToList();
            for (int i = 0; i < otherAttackers.Count; i++)
            {
                float offset = (i - otherAttackers.Count / 2f) * minDist * 1.5f;
                Vector2 pos = throwInSpot + new Vector2(0, offset);
                otherAttackers[i].Position = pos;
                otherAttackers[i].TargetPosition = pos;
            }
            for (int i = 0; i < defenders.Count; i++)
            {
                if (defenders[i] == null || defenders[i].IsSuspended()) continue;
                float angle = (2 * Mathf.PI * i) / defenders.Count;
                Vector2 pos = throwInSpot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (minDist + 1.0f);
                defenders[i].Position = pos;
                defenders[i].TargetPosition = pos;
            }
        }

        /// <summary>
        /// Positions players for a goal throw situation.
        /// </summary>
        public void PositionForGoalThrow(MatchState state)
        {
            if (state == null) return;
            int defendingTeamId = state.PossessionTeamId;
            int attackingTeamId = (defendingTeamId == 0) ? 1 : 0;
            var defenders = state.GetTeamOnCourt(defendingTeamId);
            var attackers = state.GetTeamOnCourt(attackingTeamId);
            if (defenders == null || attackers == null) return;
            // 1. Goalkeeper in goal area, with ball
            SimPlayer gk = state.GetGoalkeeper(defendingTeamId);
            Vector2 goalCenter = _geometry.GetGoalCenter(defendingTeamId);
            if (gk != null)
            {
                gk.Position = goalCenter;
                gk.TargetPosition = goalCenter;
            }
            // 2. All other defenders outside goal area
            foreach (var player in defenders)
            {
                if (player == gk || player.IsSuspended()) continue;
                float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);
                float r = _geometry.GoalAreaRadius + 1.0f + UnityEngine.Random.Range(0f, 1.5f);
                Vector2 pos = goalCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                player.Position = pos;
                player.TargetPosition = pos;
            }
            // 3. Attackers: outside goal area, spread out
            foreach (var player in attackers)
            {
                if (player.IsSuspended()) continue;
                float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);
                float r = _geometry.GoalAreaRadius + 2.0f + UnityEngine.Random.Range(0f, 2.0f);
                Vector2 pos = goalCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                player.Position = pos;
                player.TargetPosition = pos;
            }
        }

        // Explicit interface implementation for ITacticPositioner
        /// <summary>
        /// Interface-compliant method for ITacticPositioner. Calls the internal implementation with swapped arguments.
        /// </summary>
        public Vector2 GetPlayerTargetPosition(MatchState state, SimPlayer player)
        {
            return GetPlayerTargetPosition(player, state);
        }

        /// <summary>
        /// Updates the tactical positioning for all players in the match.
        /// </summary>
        /// <param name="state">The current match state containing all player data.</param>
        public void UpdateTacticalPositioning(MatchState state)
        {
            if (state == null)
            {
                Debug.LogError("[TacticPositioner] UpdateTacticalPositioning called with null state.");
                return;
            }
            // Example logic: update each player's target position (stub)
            foreach (var player in state.AllPlayers.Values)
            {
                // Compute and assign the target position (implementation placeholder)
                Vector2 targetPos = GetPlayerTargetPosition(player, state);
                // You may want to assign this to a property on the player, e.g. player.TargetPosition = targetPos;
                // For now, just log it for demonstration:
                Debug.Log($"[TacticPositioner] Player {player.GetPlayerId()} target position: {targetPos}");
            }
        }
        
        /// <summary>
        /// Calculates defensive positions for players based on the current match state.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="defendingTeamId">The team ID of the defending team.</param>
        public void PositionDefensivePlayers(MatchState state, int defendingTeamId)
        {
            if (state == null)
            {
                Debug.LogError("[TacticPositioner] PositionDefensivePlayers called with null state.");
                return;
            }
            // Example stub: log all defensive players for the team
            var defenders = state.GetTeamOnCourt(defendingTeamId);
            if (defenders == null)
            {
                Debug.LogWarning($"[TacticPositioner] No defenders found for team {defendingTeamId}.");
                return;
            }
            foreach (var player in defenders)
            {
                if (player == null) continue;
                // Assign the tactical role for defensive logic
                player.AssignedTacticalRole = player.BaseData.PrimaryPosition;
                Vector2 targetPos = GetPlayerTargetPosition(player, state);
                Debug.Log($"[TacticPositioner] Defensive player {player.GetPlayerId()} assigned role: {player.AssignedTacticalRole} target position: {targetPos}");
            }
        }

        /// <summary>
        /// Calculates offensive positions for players based on the current match state.
        /// </summary>
        /// <param name="state">The current match state.</param>
        /// <param name="attackingTeamId">The team ID of the attacking team.</param>
        public void PositionOffensivePlayers(MatchState state, int attackingTeamId)
        {
            if (state == null)
            {
                Debug.LogError("[TacticPositioner] PositionOffensivePlayers called with null state.");
                return;
            }
            // Example stub: log all offensive players for the team
            var attackers = state.GetTeamOnCourt(attackingTeamId);
            if (attackers == null)
            {
                Debug.LogWarning($"[TacticPositioner] No attackers found for team {attackingTeamId}.");
                return;
            }
            foreach (var player in attackers)
            {
                if (player == null) continue;
                // Assign the tactical role for offensive logic
                player.AssignedTacticalRole = player.BaseData.PrimaryPosition;
                Vector2 targetPos = GetPlayerTargetPosition(player, state);
                Debug.Log($"[TacticPositioner] Offensive player {player.GetPlayerId()} assigned role: {player.AssignedTacticalRole} target position: {targetPos}");
            }
        }

        // --- Positional & Spacing Constants ---
        private const float SIDELINE_BUFFER = 1.0f;
        private const float FORMATION_WIDTH_FACTOR_DEF = 0.9f;
        private const float FORMATION_WIDTH_FACTOR_ATT = 0.85f;
        private float PITCH_HALF_X => _geometry.PitchLength / 2f;

        // Defensive Depths (distance from own goal line)
        private const float DEF_DEPTH_6_0_LINE = 7f;
        private const float DEF_DEPTH_5_1_BASE = DEF_DEPTH_6_0_LINE; // Base line for 5-1
        private const float DEF_DEPTH_5_1_POINT = 10f; // Forward player in 5-1
        private const float DEF_DEPTH_321_HIGH = 12f;  // Point player in 3-2-1
        private const float DEF_DEPTH_321_MID = 9f;   // Middle pair in 3-2-1
        private const float DEF_DEPTH_321_DEEP = 6.5f; // Deep line in 3-2-1
        private const float DEF_GK_DEPTH = 0.5f; // Hardcoded value that was in MatchSimulator

        // Attacking Depths (distance from *opponent* goal line)
        private const float ATT_DEPTH_PIVOT = 6.5f;
        private const float ATT_DEPTH_WING = 8.0f;
        private const float ATT_DEPTH_BACK = 10.5f;
        private const float ATT_DEPTH_PLAYMAKER = 10.0f; // Usually CentreBack
        private const float ATT_GK_DEPTH = 8f; // How far GK comes out

        // Defensive Slot Counts (Used for interpolation)
        private const int DEF_SLOTS_6_0 = 6;
        private const int DEF_SLOTS_5_1_LINE = 5;
        private const int DEF_SLOTS_321_DEEP = 3;

        // Attacking Width Factors (Portion of half-width)
        private const float ATT_WIDTH_FACTOR_WING = 1.0f; // Wings use full available width
        private const float ATT_WIDTH_FACTOR_BACK = 0.45f; // Backs positioned relatively narrower
        private const float ATT_WIDTH_FACTOR_MID_PAIR = 0.35f; // For 3-2-1 mid players
        private const float ATT_WIDTH_FACTOR_DEEP_321 = 0.8f; // For 3-2-1 deep players

        // Adjustment Factors
        private const float BALL_POS_SHIFT_FACTOR = 0.4f;
        private const float BALL_POS_DEPTH_FACTOR = 0.1f;
        private const float BALL_POS_DEPTH_MAX_SHIFT = 2.0f; // Max depth adjustment towards ball
        private const float TRANSITION_LERP_FACTOR = 0.6f;
        private const float CONTESTED_LERP_FACTOR = 0.1f; // Default pos during contested phase (closer to defense)

        // Spacing Constants
        private const float MIN_SPACING_DISTANCE = 2.5f;
        private const float MIN_SPACING_DISTANCE_SQ = MIN_SPACING_DISTANCE * MIN_SPACING_DISTANCE;
        private const float SPACING_PUSH_FACTOR = 0.5f;

        // Attribute Influence Factors
        private const float ATTRIB_ADJ_WORKRATE_FACTOR = 0.04f; // Meters per point difference from 50 WorkRate
        private const float ATTRIB_ADJ_POSITIONING_FACTOR = 0.03f; // Max meters deviation per point difference from 50 Positioning
        private const float ATTRIB_ADJ_POS_SKILL_BENCHMARK = 50f; // Benchmark skill level for positioning adjustments
        private const float ATTRIB_ADJ_POS_MAX_DEVIATION_BASE = 1.5f; // Base max deviation at low skill (modified by factor)
        private const float ATTRIB_ADJ_POS_MIN_DEVIATION_CHANCE = 0.1f; // Min chance to apply deviation even if skilled

        /// <summary>
        /// Calculates the target tactical position for a specific player in the current state.
        /// Considers team tactics, game phase, player role, ball position, attributes, and spacing.
        /// </summary>
        /// <param name="player">The simulation player object.</param>
        /// <param name="state">The current match state.</param>
        /// <returns>The calculated target position on the pitch.</returns>
        public Vector2 GetPlayerTargetPosition(SimPlayer player, MatchState state)
        {
            // --- Input Validation ---
            if (player?.BaseData == null || state == null)
            {
                Debug.LogError("[TacticPositioner] GetPlayerTargetPosition called with null player, BaseData, or state.");
                // Return current position or a default safe position if possible
                return player?.Position ?? Vector2.zero;
            }
            // Get tactic safely
            Tactic tactic = (player.TeamSimId == 0) ? state.HomeTactic : state.AwayTactic;
            if (tactic == null) {
                Debug.LogError($"[TacticPositioner] Null tactic found for player {player.GetPlayerId()} (TeamSimId: {player.TeamSimId}). Using default position.");
                return player.Position; // Fallback to current position
            }

            // 1. Handle Goalkeeper Separately
            if (player.IsGoalkeeper())
            {
                return GetGoalkeeperPosition(player, state);
            }

            // 2. Determine Base Attacking and Defensive Positions based on role and tactic
            // Note: These methods now contain the core hardcoded formation logic.
            Vector2 defensivePos = GetDefensivePosition(player, tactic);
            Vector2 attackingPos = GetAttackingPosition(player, tactic);

            // 3. Determine Target Position based on Game Phase (Interpolate during Transitions)
            Vector2 basePosition = DetermineBasePositionForPhase(player.TeamSimId, state.CurrentPhase, state.PossessionTeamId, defensivePos, attackingPos);

            // 4. Adjust for Ball Position (Shift formation laterally and slightly depth-wise)
            basePosition = AdjustForBallPosition(basePosition, state.Ball.Position);

            // 5. Apply Player-Specific Attribute Adjustments (Work Rate, Positioning)
            basePosition = ApplyAttributeAdjustments(player, basePosition, IsPlayerTeamAttacking(player.TeamSimId, state.PossessionTeamId), state.RandomGenerator);

            // 6. Apply Spacing Logic (Prevent Clustering with Teammates)
            basePosition = ApplySpacing(player, basePosition, state);

            // 7. Final Clamping to Pitch Boundaries (with buffer)
            basePosition.x = Mathf.Clamp(basePosition.x, SIDELINE_BUFFER, _geometry.PitchLength - SIDELINE_BUFFER);
            basePosition.y = Mathf.Clamp(basePosition.y, SIDELINE_BUFFER, _geometry.PitchWidth - SIDELINE_BUFFER);

            return basePosition;
        }

        /// <summary>Calculates the target position for a goalkeeper based on game state.</summary>
        private Vector2 GetGoalkeeperPosition(SimPlayer gk, MatchState state)
        {
            // Added null check for safety, though already checked in main method
            if (gk?.BaseData == null || state == null) return Vector2.zero;

            bool isOwnTeamPossession = gk.TeamSimId == state.PossessionTeamId && state.PossessionTeamId != -1;
            Vector2 goalCenter = gk.TeamSimId == 0 ? _geometry.HomeGoalCenter3D : _geometry.AwayGoalCenter3D;
            float baseGoalLineX = goalCenter.x;

            Vector2 position;

            if (isOwnTeamPossession)
            {
                // Attack: Positioned further out
                float depth = ATT_GK_DEPTH;
                float goalX = baseGoalLineX + (gk.TeamSimId == 0 ? depth : -depth);
                position = new Vector2(goalX, _geometry.Center.y); // Central Y
            }
            else // Defending or Contested
            {
                // Defense: On/near goal line, reacting to ball Y
                float depth = DEF_GK_DEPTH;
                float goalX = baseGoalLineX + (gk.TeamSimId == 0 ? depth : -depth);
                position = new Vector2(goalX, _geometry.Center.y);

                // Adjust Y based on ball position and GK Positioning skill
                float positioningSkill = gk.BaseData?.PositioningGK ?? ATTRIB_ADJ_POS_SKILL_BENCHMARK; // Use benchmark if null
                float ballInfluence = Mathf.Lerp(0.3f, 0.8f, positioningSkill / 100f);
                position.y = Mathf.Lerp(position.y, state.Ball.Position.y, ballInfluence);
                // Clamp Y position to within goal posts (+ slight buffer)
                float goalBuffer = _geometry.GoalWidth * 0.1f; // 10% buffer outside post
                position.y = Mathf.Clamp(position.y, goalCenter.y - (_geometry.GoalWidth / 2f) - goalBuffer,
                                                   goalCenter.y + (_geometry.GoalWidth / 2f) + goalBuffer);
            }

            // Clamp X to prevent going too far behind line or excessively far out
            position.x = Mathf.Clamp(position.x, baseGoalLineX - 1f, baseGoalLineX + ATT_GK_DEPTH + 1f);

            return position;
        }

        /// <summary>Determines the base position based on the current game phase.</summary>
        private Vector2 DetermineBasePositionForPhase(int playerTeamId, GamePhase phase, int possessionTeamId, Vector2 defensivePos, Vector2 attackingPos)
        {
            bool isAttackingPhase = IsPlayerTeamAttacking(playerTeamId, possessionTeamId);
            bool isDefendingPhase = IsPlayerTeamDefending(playerTeamId, possessionTeamId);
            bool isInTransition = phase == GamePhase.TransitionToHomeAttack || phase == GamePhase.TransitionToAwayAttack;

            if (isAttackingPhase) {
                 return attackingPos;
            } else if (isDefendingPhase) {
                 return defensivePos;
            } else if (isInTransition) {
                 bool transitioningToAttack = (phase == GamePhase.TransitionToHomeAttack && playerTeamId == 0) ||
                                              (phase == GamePhase.TransitionToAwayAttack && playerTeamId == 1);
                 float lerpFactor = transitioningToAttack ? TRANSITION_LERP_FACTOR : (1.0f - TRANSITION_LERP_FACTOR);
                 return Vector2.Lerp(defensivePos, attackingPos, lerpFactor);
            } else { // Contested Ball, Kickoff phases - Default closer to defensive shape
                 return Vector2.Lerp(defensivePos, attackingPos, CONTESTED_LERP_FACTOR);
            }
        }

        /// <summary>Helper to check if the player's team is considered attacking.</summary>
        private bool IsPlayerTeamAttacking(int playerTeamId, int possessionTeamId) {
            return playerTeamId == possessionTeamId && possessionTeamId != -1;
        }
        /// <summary>Helper to check if the player's team is considered defending.</summary>
        private bool IsPlayerTeamDefending(int playerTeamId, int possessionTeamId) {
             return playerTeamId != possessionTeamId && possessionTeamId != -1;
        }


        /// <summary>Calculates the base defensive position based on role and tactical system.</summary>
        /// <remarks>Now data-driven using FormationData.</remarks>
        private Vector2 GetDefensivePosition(SimPlayer player, Tactic tactic)
        {
            if (player?.BaseData == null) return Vector2.zero;
            var formation = tactic.DefensiveFormationData;
            var slot = formation.Slots.FirstOrDefault(s => s.PositionRole == player.BaseData.PrimaryPosition);
            if (slot == null)
            {
                Debug.LogWarning($"[TacticPositioner] No defensive slot for position {player.BaseData.PrimaryPosition} in formation {formation.Name}");
                return Vector2.zero;
            }
            float relX = slot.RelativePosition.x;
            float relY = slot.RelativePosition.y;
            float x = (player.TeamSimId == 0) ? relX * _geometry.PitchLength : (1 - relX) * _geometry.PitchLength;
            float y = relY * _geometry.PitchWidth;
            return new Vector2(x, y);
        }

        /// <summary>Calculates the base attacking position based on role.</summary>
        /// <remarks>Now data-driven using FormationData.</remarks>
        private Vector2 GetAttackingPosition(SimPlayer player, Tactic tactic)
        {
            if (player?.BaseData == null) return Vector2.zero;
            var formation = tactic.OffensiveFormationData;
            var slot = formation.Slots.FirstOrDefault(s => s.PositionRole == player.BaseData.PrimaryPosition);
            if (slot == null)
            {
                Debug.LogWarning($"[TacticPositioner] No attacking slot for position {player.BaseData.PrimaryPosition} in formation {formation.Name}");
                return Vector2.zero;
            }
            float relX = slot.RelativePosition.x;
            float relY = slot.RelativePosition.y;
            float x = (player.TeamSimId == 0) ? relX * _geometry.PitchLength : (1 - relX) * _geometry.PitchLength;
            float y = relY * _geometry.PitchWidth;
            return new Vector2(x, y);
        }

        // --- Adjustments ---

        /// <summary>
        /// Shifts the base position laterally and slightly depth-wise towards the ball's position.
        /// </summary>
        private Vector2 AdjustForBallPosition(Vector2 currentPosition, Vector2 ballPosition)
        {
             // Lateral Shift (Y-axis): Move formation towards ball Y
             float ballYRatio = Mathf.Clamp01(ballPosition.y / _geometry.PitchWidth);
             float targetY = Mathf.Lerp(currentPosition.y, ballYRatio * _geometry.PitchWidth, BALL_POS_SHIFT_FACTOR);

             // Depth Shift (X-axis): Subtle push towards ball X
             float xDiff = ballPosition.x - currentPosition.x;
             // Apply shift factor and clamp the maximum adjustment
             float depthAdjustment = Mathf.Clamp(xDiff * BALL_POS_DEPTH_FACTOR, -BALL_POS_DEPTH_MAX_SHIFT, BALL_POS_DEPTH_MAX_SHIFT);

             // Apply adjustments
             currentPosition.y = targetY;
             currentPosition.x += depthAdjustment;

             return currentPosition;
        }

        /// <summary>
        /// Applies subtle adjustments to the target position based on player attributes like Work Rate and Positioning.
        /// Includes a random element influenced by Positioning skill.
        /// </summary>
        private Vector2 ApplyAttributeAdjustments(SimPlayer player, Vector2 position, bool isAttackingPhase, System.Random random)
        {
             // Safety check for player data and random generator
             if (player?.BaseData == null || random == null) return position;

             // --- Work Rate Adjustment --- Affects willingness to push forward/track back
             float workRateDiff = player.BaseData.WorkRate - ATTRIB_ADJ_POS_SKILL_BENCHMARK; // Diff from benchmark 50
             // Positive direction is towards opponent goal (Negative X for Home, Positive X for Away)
             float attackDirectionX = (player.TeamSimId == 0) ? -1f : 1f;
             // If attacking, higher WR pushes further forward. If defending, higher WR tracks back more (further from opponent goal).
             float depthShiftDirection = isAttackingPhase ? attackDirectionX : -attackDirectionX;
             float depthShift = workRateDiff * ATTRIB_ADJ_WORKRATE_FACTOR * depthShiftDirection;
             position.x += depthShift;

             // --- Positioning Adjustment --- Affects deviation from calculated spot
             // Use the actual Positioning attribute. Lower skill = higher potential deviation.
             float positioningSkill = player.BaseData.Positioning; // Assuming Positioning attribute exists now
             // Calculate max potential deviation based on skill difference from benchmark
             float deviationPotential = Mathf.Clamp((100f - positioningSkill) * ATTRIB_ADJ_POSITIONING_FACTOR, 0f, ATTRIB_ADJ_POS_MAX_DEVIATION_BASE);

             // Apply deviation only if potential is significant OR there's a minimum random chance
             if (deviationPotential > 0.1f || random.NextDouble() < ATTRIB_ADJ_POS_MIN_DEVIATION_CHANCE)
             {
                  // Generate random direction and magnitude (scaled by potential)
                  float angle = (float)random.NextDouble() * 2f * Mathf.PI; // Random angle in radians
                  Vector2 randomDir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                  float randomMagnitude = (float)random.NextDouble() * deviationPotential; // Random magnitude up to potential
                  // Apply the random offset
                  position += randomDir * randomMagnitude;
             }

             return position;
        }

        /// <summary>
        /// Adjusts the target position slightly to maintain minimum spacing from teammates, preventing clustering.
        /// Note: This is an O(N^2) operation within the team, acceptable for small team sizes.
        /// </summary>
        private Vector2 ApplySpacing(SimPlayer player, Vector2 targetPosition, MatchState state)
        {
            // Safety checks
            if (player == null || state == null) return targetPosition;

            Vector2 cumulativePush = Vector2.zero;
            int closeNeighbors = 0;
            var teammates = state.GetTeamOnCourt(player.TeamSimId);
            if (teammates == null) return targetPosition; // Check if teammate list is valid

            // Iterate through teammates currently on court
            foreach (var teammate in teammates)
            {
                // Skip self, nulls, or suspended players
                if (teammate == null || teammate == player || !teammate.IsOnCourt || teammate.IsSuspended()) continue;

                // Calculate vector and distance based on *current* positions for reaction
                Vector2 vectorToTeammate = player.Position - teammate.Position;
                float distSq = vectorToTeammate.sqrMagnitude;

                // If teammate is too close (within minimum spacing squared distance)
                if (distSq < MIN_SPACING_DISTANCE_SQ && distSq > 0.01f) // Use constant, add epsilon check
                {
                    closeNeighbors++;
                    float distance = Mathf.Sqrt(distSq);
                    // Calculate push magnitude based on how much overlap there is
                    float pushMagnitude = (MIN_SPACING_DISTANCE - distance) * SPACING_PUSH_FACTOR; // Use constant
                    // Add push vector (away from the teammate) to the cumulative push
                    // Normalize safely in case distSq was extremely small but passed check
                    Vector2 pushDir = (distSq > 0.0001f) ? vectorToTeammate.normalized : Vector2.right; // Default push direction if overlapping
                    cumulativePush += pushDir * pushMagnitude;
                }
            }

            // Apply the total calculated push vector to the target position
            if (closeNeighbors > 0)
            {
                // Optional: Clamp the maximum push distance from spacing?
                // float maxPush = 1.0f;
                // cumulativePush = Vector2.ClampMagnitude(cumulativePush, maxPush);
                targetPosition += cumulativePush;
            }

            return targetPosition;
        }

    }
}