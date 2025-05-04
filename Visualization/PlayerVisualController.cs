using UnityEngine;
using HandballManager.Simulation;
using HandballManager.Simulation.Engines;
using HandballManager.Core; // Assuming SimPlayer might be here or in Simulation

namespace HandballManager.Visualization
{
    /// <summary>
    /// Controls the visual representation of a player in the Unity scene,
    /// synchronizing it with the simulation state (SimPlayer).
    /// </summary>
    public class PlayerVisualController : MonoBehaviour
    {
        [Header("Simulation Link")]
        public SimPlayer LinkedSimPlayer; // Reference to the simulation data

        [Header("Visual Settings")]
        public float PositionInterpolationSpeed = 15.0f; // How quickly the visual snaps to the simulation position
        public float RotationInterpolationSpeed = 10.0f; // How quickly the visual rotates
        public SpriteRenderer PlayerSpriteRenderer; // Assign the player's sprite renderer here
        public Animator PlayerAnimator; // Assign the player's animator here (optional for now)

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;

        void Start()
        {
            if (LinkedSimPlayer == null)
            {
                Debug.LogError($"PlayerVisualController on {gameObject.name} requires a LinkedSimPlayer.", this);
                enabled = false; // Disable if no simulation link
                return;
            }

            // Initialize visual position to simulation position
            _targetPosition = ConvertSimPositionToWorld(LinkedSimPlayer.Position);
            transform.position = _targetPosition;
            _targetRotation = Quaternion.LookRotation(Vector3.forward, LinkedSimPlayer.LookDirection); // Assuming 2D top-down, Z is forward, LookDirection is up/right
            transform.rotation = _targetRotation;

            if (PlayerSpriteRenderer == null)
            {
                PlayerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (PlayerSpriteRenderer == null) Debug.LogWarning($"PlayerVisualController on {gameObject.name} couldn't find a SpriteRenderer.", this);
            }
            if (PlayerAnimator == null)
            {
                PlayerAnimator = GetComponentInChildren<Animator>();
                // No warning if animator is missing, it's optional for the basic run animation
            }
        }

        void Update()
        {
            if (LinkedSimPlayer == null || !enabled)
                return;

            // --- Update Target Position & Rotation from SimPlayer --- 
            _targetPosition = ConvertSimPositionToWorld(LinkedSimPlayer.Position);
            // Use LookDirection for orientation. Assuming Vector2 LookDirection maps to 2D rotation.
            // If LookDirection is (0,0), maintain current rotation.
            if (LinkedSimPlayer.LookDirection.sqrMagnitude > 0.01f)
            {
                 // Convert 2D direction vector to a 2D rotation (angle around Z axis)
                 float angle = Mathf.Atan2(LinkedSimPlayer.LookDirection.y, LinkedSimPlayer.LookDirection.x) * Mathf.Rad2Deg - 90f; // -90 because Unity's forward is Y+ for sprites usually
                 _targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            // Else: Keep the last valid _targetRotation

            // --- Smooth Interpolation --- 
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * PositionInterpolationSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * RotationInterpolationSpeed);

            // --- Placeholder Run Animation --- 
            if (PlayerAnimator != null)
            {
                bool isMoving = LinkedSimPlayer.Velocity.magnitude > 0.1f; // Threshold to consider moving
                // Assuming an Animator parameter named "IsRunning" (boolean)
                PlayerAnimator.SetBool("IsRunning", isMoving);
            }
        }

        /// <summary>
        /// Converts simulation coordinates (Vector2) to Unity world coordinates (Vector3).
        /// Placeholder implementation - adjust based on actual court setup.
        /// </summary>
        private Vector3 ConvertSimPositionToWorld(Vector2 simPosition)
        {
            // Simple 1:1 mapping for now, assuming simulation plane is XY in Unity
            return new Vector3(simPosition.x, simPosition.y, 0); 
            // If your court is oriented differently (e.g., XZ plane), adjust this:
            // return new Vector3(simPosition.x, 0, simPosition.y);
        }

        // Optional: Method to set the SimPlayer link if not done via Inspector
        public void SetSimPlayer(SimPlayer player)
        {
            LinkedSimPlayer = player;
            if (this.isActiveAndEnabled) // If already started, re-initialize position
            {
                 _targetPosition = ConvertSimPositionToWorld(LinkedSimPlayer.Position);
                 transform.position = _targetPosition;
                 if (LinkedSimPlayer.LookDirection.sqrMagnitude > 0.01f)
                 {
                    float angle = Mathf.Atan2(LinkedSimPlayer.LookDirection.y, LinkedSimPlayer.LookDirection.x) * Mathf.Rad2Deg - 90f;
                    _targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    transform.rotation = _targetRotation;
                 }
            }
            enabled = (LinkedSimPlayer != null);
        }
    }
}