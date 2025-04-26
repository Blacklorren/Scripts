using UnityEngine;
using HandballManager.Core;
using HandballManager.Data;
using HandballManager.Simulation.Events;
using System.Collections.Generic;
using HandballManager.Simulation.Engines;
using System;
using System.Linq;
using HandballManager.Simulation.Physics;
using UnityEngine.LowLevelPhysics;
using HandballManager.Simulation.Utils;

namespace HandballManager.Simulation.Physics
{
    /// <summary>
    /// Handles collision resolution and boundary enforcement for players and the ball.
    /// </summary>
    public class CollisionResolver
    {
        private readonly IGeometryProvider _geometry;
        // --- Spatial Partitioning Fields ---
        private SpatialGrid _spatialGrid;
        private float _spatialGridCellSize = -1f;
        private float _spatialGridWidth = -1f;
        private float _spatialGridHeight = -1f;

        // Collision Constants
        private const float PLAYER_COLLISION_RADIUS = 0.4f;
        private const float PLAYER_COLLISION_DIAMETER = PLAYER_COLLISION_RADIUS * 2f;
        private const float PLAYER_COLLISION_DIAMETER_SQ = PLAYER_COLLISION_DIAMETER * PLAYER_COLLISION_DIAMETER;
        private const float COLLISION_RESPONSE_FACTOR = 0.5f;  // How strongly players push apart
        private const float COLLISION_MIN_DIST_SQ_CHECK = 0.0001f;  // Lower bound for collision distance check
        
        // Boundary and Spacing Constants
        private const float SIDELINE_BUFFER = 0.5f;  // Buffer from sidelines for player and ball positions

        // Team Spacing Constants
        private const float MIN_SPACING_DISTANCE = 2.0f;  // How close teammates can get before spacing push
        private const float MIN_SPACING_DISTANCE_SQ = MIN_SPACING_DISTANCE * MIN_SPACING_DISTANCE;
        private const float SPACING_PUSH_FACTOR = 0.4f;
        private const float SPACING_PROXIMITY_POWER = 2.0f;  // Power for spacing push magnitude (higher = stronger when very close)

        public CollisionResolver(IGeometryProvider geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }
        /// <summary>
        /// Checks for and resolves collisions between players and between players and the ball.
        /// Implements the IMovementSimulator interface method.
        /// </summary>
        /// <param name="state">The current match state containing players and ball data.</param>
        /// <exception cref="ArgumentNullException">Thrown when state is null.</exception>
        public void ResolveCollisions(MatchState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state), "Match state cannot be null when resolving collisions.");
            }

            // Delegate to the existing collision handling logic with a minimal time step
            // This allows reuse of the existing collision logic without duplicating code
            HandleCollisionsAndBoundaries(state);
        }

        /// <summary>
        /// Handles collisions and boundaries with proper single implementation
        /// </summary>
        private void HandleCollisionsAndBoundaries(MatchState state)
        {
            if (state?.PlayersOnCourt == null) return;

            // --- Reset BallProtectionBonus for all players at the start of the step ---
            // BallProtectionBonus property removed; shielding handled via PlayerData.GetShieldingEffectiveness()

            var players = state.PlayersOnCourt.ToList(); // Convertir IEnumerable en List pour permettre l'indexation

            // --- Spatial Partitioning for Player-Player Collisions and Team Spacing ---
            float pitchLength = (_geometry?.PitchLength ?? SimConstants.DEFAULT_PITCH_LENGTH);
            float pitchWidth = (_geometry?.PitchWidth ?? SimConstants.DEFAULT_PITCH_WIDTH);
            float cellSize = SimConstants.PLAYER_COLLISION_RADIUS * 2.5f;
            if (_spatialGrid == null || _spatialGridCellSize != cellSize || _spatialGridWidth != pitchLength || _spatialGridHeight != pitchWidth)
            {
                _spatialGrid = new SpatialGrid(pitchLength, pitchWidth, cellSize);
                _spatialGridCellSize = cellSize;
                _spatialGridWidth = pitchLength;
                _spatialGridHeight = pitchWidth;
            }
            _spatialGrid.Clear();
            foreach (var p in players)
                if (p != null) _spatialGrid.Insert(p);

            HashSet<(int, int)> checkedPairs = new();
            for (int i = 0; i < players.Count; i++)
            {
                var player1 = players[i];
                if (player1 == null) continue;
                var nearbyPlayers = _spatialGrid.GetNearbySimPlayers(player1.Position, SimConstants.PLAYER_COLLISION_RADIUS * 2f);
                foreach (var player2 in nearbyPlayers)
                {
                    if (player2 == null || player1 == player2) continue;
                    int id1 = player1.GetPlayerId();
                    int id2 = player2.GetPlayerId();
                    if (id1 > id2) continue; // Avoid duplicate checks
                    var pair = (id1, id2);
                    if (checkedPairs.Contains(pair)) continue;
                    checkedPairs.Add(pair);

                    Vector2 separation = player1.Position - player2.Position;
                    float distanceSq = separation.sqrMagnitude;

                    // Handle collision
                    if (distanceSq < SimConstants.PLAYER_COLLISION_DIAMETER_SQ && distanceSq > COLLISION_MIN_DIST_SQ_CHECK)
                    {
                        float distance = Mathf.Sqrt(distanceSq);
                        Vector2 separationDir = separation / distance;

                        float overlap = SimConstants.PLAYER_COLLISION_DIAMETER - distance;
                        Vector2 responseVector = COLLISION_RESPONSE_FACTOR * overlap * separationDir;

                        // --- Shielding Mechanics ---
                        bool player1Shielding = player1.CurrentAction == PlayerAction.ShieldingBall && player1.HasBall;
                        bool player2Shielding = player2.CurrentAction == PlayerAction.ShieldingBall && player2.HasBall;
                        float shieldFactor1 = 0f, shieldFactor2 = 0f;
                        if (player1Shielding && player1.BaseData != null)
                            shieldFactor1 = player1.BaseData.GetShieldingEffectiveness();
                        if (player2Shielding && player2.BaseData != null)
                            shieldFactor2 = player2.BaseData.GetShieldingEffectiveness();

                        // Reduce displacement effect for shielding player
                        Vector2 response1 = responseVector;
                        Vector2 response2 = -responseVector;
                        if (player1Shielding)
                            response1 *= (1f - shieldFactor1);
                        if (player2Shielding)
                            response2 *= (1f - shieldFactor2);
                        player1.Position += response1;
                        player2.Position += response2;

                        // Add velocity response (conservation of momentum)
                        Vector2 relativeVelocity = player1.Velocity - player2.Velocity;
                        float velAlongNormal = Vector2.Dot(relativeVelocity, separationDir);

                        if (velAlongNormal > 0) // Only if moving towards each other
                        {
                            // In collision response
                            float p1Mass = 1.0f + ((player1.BaseData?.Strength ?? 50f) / 200f);
                            float p2Mass = 1.0f + ((player2.BaseData?.Strength ?? 50f) / 200f);
                            float impulse = velAlongNormal / (1 / p1Mass + 1 / p2Mass);
                            Vector2 impulseVector = separationDir * impulse;
                            // Reduce impulse for shielding
                            if (player1Shielding)
                                impulseVector *= (1f - shieldFactor1);
                            if (player2Shielding)
                                impulseVector *= (1f - shieldFactor2);
                            player1.Velocity -= impulseVector / p1Mass;
                            player2.Velocity += impulseVector / p2Mass;
                        }
                    }

                    // Handle team spacing
                    if (player1.TeamSimId == player2.TeamSimId && distanceSq < MIN_SPACING_DISTANCE_SQ)
                    {
                        float spacingStrength = 1f - (distanceSq / MIN_SPACING_DISTANCE_SQ);
                        spacingStrength = Mathf.Pow(spacingStrength, SPACING_PROXIMITY_POWER);
                        Vector2 spacingForce = separation.normalized * spacingStrength * SPACING_PUSH_FACTOR;

                        player1.Position += spacingForce;
                        player2.Position -= spacingForce;
                    }
                }
            }

            // --- Player-Ball Deflections ---
            if (state.Ball != null)
            {
                SimBall ball = state.Ball;
                foreach (var player in players)
                {
                    var playerPos3D = new Vector3(player.Position.x, ball.Position.y, player.Position.y);
                    var diff = ball.Position - playerPos3D;
                    float radiusSum = PLAYER_COLLISION_RADIUS + SimConstants.BALL_RADIUS;
                    if (diff.sqrMagnitude <= radiusSum * radiusSum)
                    {
                        var normal = diff.normalized;
                        // Reflect velocity and apply restitution
                        var reflected = Vector3.Reflect(ball.Velocity, normal) * SimConstants.COEFFICIENT_OF_RESTITUTION;
                        // Technique influences deflection strength
                        float techFactor = (player.BaseData?.Technique ?? 50f) / 100f;
                        ball.Velocity = Vector3.Lerp(reflected, reflected.normalized * ball.Velocity.magnitude, techFactor);
                        // Adjust position to avoid penetration
                        ball.Position = playerPos3D + normal * SimConstants.BALL_RADIUS;
                        Debug.Log("Deflection");
                        // Player stumbles upon deflection
                        player.StartStumble(SimConstants.STUMBLE_DURATION);
                        break;
                    }
                }
            }

            // Boundary clamping for players
            foreach (var player in players)
            {
                if (player == null) continue;
                // In boundary clamping
                float maxX = (_geometry?.PitchLength ?? SimConstants.DEFAULT_PITCH_LENGTH) - SIDELINE_BUFFER;
                float maxY = (_geometry?.PitchWidth ?? SimConstants.DEFAULT_PITCH_WIDTH) - SIDELINE_BUFFER;
                player.Position = new Vector2(
                    Mathf.Clamp(player.Position.x, SIDELINE_BUFFER, maxX),
                    Mathf.Clamp(player.Position.y, SIDELINE_BUFFER, maxY)
                );
            }

            // Ball boundary clamping
            if (state.Ball != null)
            {
                SimBall ball = state.Ball;
                float ballMaxX = (_geometry?.PitchLength ?? SimConstants.DEFAULT_PITCH_LENGTH) - SIDELINE_BUFFER;
                float ballMaxZ = (_geometry?.PitchWidth ?? SimConstants.DEFAULT_PITCH_WIDTH) - SIDELINE_BUFFER;
                ball.Position = new Vector3(
                    Mathf.Clamp(ball.Position.x, SIDELINE_BUFFER, ballMaxX),
                    ball.Position.y,
                    Mathf.Clamp(ball.Position.z, SIDELINE_BUFFER, ballMaxZ)
                );
            }
        }
        /// <summary>
        /// Interface implementation with single method for enforcing boundaries
        /// </summary>
        public void EnforceBoundaries(MatchState state)
        {
            if (state == null) return;
            HandleCollisionsAndBoundaries(state);
        }
      
     }
}
