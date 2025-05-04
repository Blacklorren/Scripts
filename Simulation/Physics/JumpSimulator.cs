using UnityEngine;
using HandballManager.Simulation.Engines; 
using HandballManager.Core; 

namespace HandballManager.Simulation.Physics
{
    /// <summary>
    /// Handles simulation of player jumps, including arc calculation and gravity integration.
    /// </summary>
    public class JumpSimulator
    {
        private readonly SimPlayer _player; // Store player reference
        private readonly float _gravityScalar;

        // Properties to expose jump state if needed externally
        public bool IsJumping => _player.IsJumping;
        public bool JumpActive => _player.JumpActive;
        public float CurrentHeight => _player.VerticalPosition;
        public float JumpTimer => _player.JumpTimer;

        // Initial velocity required to reach target jump height under gravity
        private float _jumpInitialVerticalVelocity;

        // Constructor now takes SimPlayer and uses Core.SimConstants
        public JumpSimulator(SimPlayer player)
        {
            _player = player ?? throw new System.ArgumentNullException(nameof(player));
            _gravityScalar = SimConstants.EARTH_GRAVITY; // Use constant from Core
            // Pre-calculate initial velocity based on player's jump attribute
            CalculateInitialJumpVelocity();
        }

        private void CalculateInitialJumpVelocity()
        {
            // Use SimConstants from Core for jump factors and attributes
            float jumpAttribute = _player.BaseData?.Jumping ?? SimConstants.PLAYER_DEFAULT_ATTRIBUTE_VALUE; // Use default if data missing
            float jumpHeightFactor = Mathf.Lerp(SimConstants.JUMP_MIN_FACTOR, SimConstants.JUMP_MAX_FACTOR, jumpAttribute / 100f);
            float targetHeight = SimConstants.BASE_JUMP_HEIGHT * jumpHeightFactor;

            // Formula: v0 = sqrt(2 * g * h)
            _jumpInitialVerticalVelocity = Mathf.Sqrt(2 * _gravityScalar * targetHeight);

            // Optionally clamp based on min/max jump velocities if needed
             _jumpInitialVerticalVelocity = Mathf.Clamp(_jumpInitialVerticalVelocity, SimConstants.MIN_JUMP_VERTICAL_VELOCITY, SimConstants.MAX_JUMP_VERTICAL_VELOCITY);
        }

        /// <summary>
        /// Starts a jump for the player by initializing jump parameters.
        /// </summary>
        public void StartJump()
        {
            if (!_player.IsJumping) // Prevent double jumps
            {
                _player.IsJumping = true;
                _player.JumpActive = true;
                _player.JumpTimer = 0f;
                _player.VerticalVelocity = _jumpInitialVerticalVelocity; // Set initial upward velocity
                _player.JumpInitialHeight = _player.Position.y; // Record starting height
                _player.CurrentAction = PlayerAction.JumpingForShot; // Set player action
                Debug.Log($"Player {_player.PlayerID} started jump with velocity {_jumpInitialVerticalVelocity}");
            }
        }
        
        /// <summary>
        /// Starts a jump for the specified player with a custom velocity.
        /// </summary>
        /// <param name="player">The player who is jumping</param>
        /// <param name="jumpVelocity">The initial jump velocity (vertical component is used)</param>
        public void StartJump(SimPlayer player, Vector2 jumpVelocity)
        {
            if (player == null || player.IsJumping) return; // Prevent null or double jumps
            
            player.IsJumping = true;
            player.JumpActive = true;
            player.JumpTimer = 0f;
            player.VerticalVelocity = jumpVelocity.y; // Use the provided vertical velocity
            player.JumpInitialHeight = player.Position.y; // Record starting height
            player.CurrentAction = PlayerAction.JumpingForShot; // Set player action
            Debug.Log($"Player {player.PlayerID} started jump with custom velocity {jumpVelocity.y}");
        }

        /// <summary>
        /// Computes the horizontal position at time t based on initial velocity.
        /// </summary>
        public Vector2 ComputeHorizontalPosition(float t)
        {
            if (!_player.JumpOrigin.HasValue)
                return _player.Position + _player.JumpStartVelocity * t; // fallback: use current position
            return (Vector2)_player.JumpOrigin + _player.JumpStartVelocity * t;
        }

        /// <summary>
        /// Computes the vertical position at time t using kinematic equation.
        /// </summary>
        public float ComputeVerticalPosition(float t)
        {
            // Equation: y(t) = y0 + v0y*t + 0.5*a*t^2 (where a = -gravity)
            return _player.JumpInitialHeight + _player.JumpStartVelocity.y * t - 0.5f * _gravityScalar * t * t;
        }

        /// <summary>
        /// Updates the jump simulation for the player, including position and state.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_player.JumpActive) return;

            _player.JumpTimer += deltaTime;
            float t = _player.JumpTimer;

            float newY = ComputeVerticalPosition(t);

            if (newY <= 0f && _player.IsJumping) // Landed
            {
                _player.VerticalPosition = 0f;
                _player.JumpActive = false;
                _player.IsJumping = false;
                _player.JumpTimer = 0f; // Reset timer
                // Trigger recovery state in SimPlayer
                _player.StartJumpRecovery(0.2f); // Example recovery duration
                Debug.Log($"Player {_player.PlayerID} landed.");
                return;
            }

            _player.VerticalPosition = Mathf.Max(0f, newY); // Ensure vertical position doesn't go below 0

            // Optionally update horizontal position based on jump trajectory
            // Vector2 newHorizontalPos = ComputeHorizontalPosition(t);
            // _player.Position = newHorizontalPos; // Careful: This overrides physics engine movement
        }

        /// <summary>
        /// Updates the player's vertical position and velocity during a jump.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since the last update.</param>
        public void UpdateJump(float deltaTime)
        {
            if (!_player.IsJumping) return;

            _player.JumpTimer += deltaTime;

            // Update vertical velocity based on gravity
            _player.VerticalVelocity -= _gravityScalar * deltaTime;

            // Update vertical position based on velocity
            // Formula: new_y = old_y + v * dt + 0.5 * a * dt^2 (more accurate)
            // float deltaY = _player.VerticalVelocity * deltaTime - 0.5f * _gravityScalar * deltaTime * deltaTime;
            // Simpler: new_y = old_y + average_velocity * dt (average velocity approach)
            float newVerticalVelocity = _player.VerticalVelocity;
            float averageVelocity = (_player.VerticalVelocity + newVerticalVelocity) / 2.0f; // Technically should use previous velocity for average
            
            // Update the vertical position separately (Position is a Vector2, height is tracked in VerticalPosition)
            _player.VerticalPosition += _player.VerticalVelocity * deltaTime;
            
            // No need to modify Position as it's the 2D ground position (x,z)

            // Check for landing (position returns to or goes below initial height AND vertical velocity is negative)
            if (_player.Position.y <= _player.JumpInitialHeight && _player.VerticalVelocity < 0)
            {
                Land();
            }
        }

        /// <summary>
        /// Handles the player landing.
        /// </summary>
        private void Land()
        {
            _player.IsJumping = false;
            _player.JumpActive = false;
            _player.VerticalVelocity = 0f;
            
            // Reset the vertical position to the initial height
            // Position is a Vector2 representing ground position, VerticalPosition tracks height
            _player.VerticalPosition = _player.JumpInitialHeight;
            
            _player.JumpTimer = 0f;
            _player.CurrentAction = PlayerAction.Idle; // Or Landing state if exists

             // Apply landing impact/recovery based on Core.SimConstants
            // TODO: Implement landing recovery logic (e.g., temporary speed reduction)
             // float landingImpactFactor = Mathf.Lerp(SimConstants.LANDING_MIN_IMPACT, SimConstants.LANDING_MAX_IMPACT, 1f - (_player.BaseData?.Agility ?? 50) / 100f);
             // Apply impact... schedule recovery...

        }

        /// <summary>Stops the jump simulation prematurely if needed.</summary>
        public void CancelJump()
        {
             if (_player.JumpActive)
             {
                 _player.JumpActive = false;
                 _player.IsJumping = false;
                 _player.VerticalPosition = 0f; // Force landing
                 _player.JumpTimer = 0f;
                 Debug.Log($"Player {_player.PlayerID} jump cancelled.");
                 // Optionally trigger recovery immediately
                 _player.StartJumpRecovery(0.1f); // Shorter recovery for cancel?
             }
        }
    }
}
