using UnityEngine;
using HandballManager.Simulation.Engines;

/// <summary>
/// Handles simulation of player jumps, including arc calculation and gravity integration.
/// </summary>
public class JumpSimulator
{
    private float gravity;
    public JumpSimulator(float gravityValue)
    {
        gravity = gravityValue;
    }

    /// <summary>
    /// Starts a jump for the player by initializing jump parameters.
    /// </summary>
    public void StartJump(SimPlayer player, Vector2 initialVelocity)
    {
        player.JumpOrigin = player.Position;
        player.JumpStartVelocity = initialVelocity;
        player.JumpTimer = 0f;
        player.IsJumping = true;
        player.JumpActive = true;
        player.JumpInitialHeight = player.VerticalPosition;
    }

    /// <summary>
    /// Computes the horizontal position at time t based on initial velocity.
    /// </summary>
    public Vector2 ComputeHorizontalPosition(SimPlayer player, float t)
    {
        if (!player.JumpOrigin.HasValue)
    return player.Position + player.JumpStartVelocity * t; // fallback: use current position
return (Vector2)player.JumpOrigin + player.JumpStartVelocity * t;
    }

    /// <summary>
    /// Computes the vertical position at time t using kinematic equation.
    /// </summary>
    public float ComputeVerticalPosition(SimPlayer player, float t)
    {
        return player.JumpInitialHeight + player.JumpStartVelocity.y * t - 0.5f * gravity * t * t;
    }

    /// <summary>
    /// Steps the jump simulation for the player, updating position and state.
    /// </summary>
    public void Step(SimPlayer player, float deltaTime)
    {
        if (!player.JumpActive) return;
        player.JumpTimer += deltaTime;
        float t = player.JumpTimer;
        float newY = ComputeVerticalPosition(player, t);
        if (newY <= 0f)
        {
            player.VerticalPosition = 0f;
            player.JumpActive = false;
            player.IsJumping = false;
            return;
        }
        player.VerticalPosition = newY;
        // Optionally update horizontal position if needed
        // player.Position = ComputeHorizontalPosition(player, t);
    }
}
