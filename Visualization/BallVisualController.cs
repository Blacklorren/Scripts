using UnityEngine;
using HandballManager.Simulation;
using HandballManager.Simulation.Engines;
using HandballManager.Core; // Assuming SimBall might be here or in Simulation

namespace HandballManager.Visualization
{
    /// <summary>
    /// Controls the visual representation of the ball in the Unity scene,
    /// synchronizing it with the simulation state (SimBall).
    /// </summary>
    public class BallVisualController : MonoBehaviour
    {
        [Header("Simulation Link")]
        public SimBall LinkedSimBall; // Reference to the simulation data

        [Header("Visual Settings")]
        public float PositionInterpolationSpeed = 20.0f; // How quickly the visual snaps to the simulation position
        public float HeightScaleFactor = 0.5f; // How much the Y position affects scale (0 = no effect)
        public float MinScale = 0.8f; // Minimum scale when on the ground
        public float MaxScale = 1.5f; // Maximum scale at peak height
        public SpriteRenderer BallSpriteRenderer; // Assign the ball's sprite renderer here

        private Vector3 _targetPosition;
        private Vector3 _baseScale;

        void Start()
        {
            if (LinkedSimBall == null)
            {
                Debug.LogError($"BallVisualController on {gameObject.name} requires a LinkedSimBall.", this);
                enabled = false; // Disable if no simulation link
                return;
            }

            if (BallSpriteRenderer == null)
            {
                BallSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (BallSpriteRenderer == null) 
                {
                     Debug.LogError($"BallVisualController on {gameObject.name} couldn't find a SpriteRenderer.", this);
                     enabled = false;
                     return;
                }
            }
            _baseScale = transform.localScale;

            // Initialize visual position to simulation position
            _targetPosition = ConvertSimPositionToWorld(LinkedSimBall.Position);
            transform.position = _targetPosition;
            UpdateScale(); // Initial scale update
        }

        void Update()
        {
            if (LinkedSimBall == null || !enabled)
                return;

            // --- Update Target Position from SimBall --- 
            _targetPosition = ConvertSimPositionToWorld(LinkedSimBall.Position);

            // --- Smooth Interpolation --- 
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * PositionInterpolationSpeed);

            // --- Update Scale based on Height (Y position) --- 
            UpdateScale();
        }

        /// <summary>
        /// Updates the visual scale based on the ball's height (Y position in simulation).
        /// </summary>
        private void UpdateScale()
        {
            // Assuming SimBall.Position.y represents height above the ground plane
            float height = LinkedSimBall.Position.y;
            // Normalize height effect (e.g., assume max relevant height is 5 units)
            float normalizedHeight = Mathf.Clamp01(height / 5.0f); 
            float scaleMultiplier = Mathf.Lerp(MinScale, MaxScale, normalizedHeight * HeightScaleFactor);
            transform.localScale = _baseScale * scaleMultiplier;
        }

        /// <summary>
        /// Converts simulation coordinates (Vector3, using XY for position, Y for height) 
        /// to Unity world coordinates (Vector3).
        /// Placeholder implementation - adjust based on actual court setup.
        /// </summary>
        private Vector3 ConvertSimPositionToWorld(Vector3 simPosition)
        {
            // Use X and Z from simPosition for world X and Z, ignore sim Y for world Y (handled by scale)
            return new Vector3(simPosition.x, 0, simPosition.z); 
            // If your court plane is XY in Unity:
            // return new Vector3(simPosition.x, simPosition.z, 0); // Use sim Z for world Y
        }

        // Optional: Method to set the SimBall link if not done via Inspector
        public void SetSimBall(SimBall ball)
        {
            LinkedSimBall = ball;
            if (this.isActiveAndEnabled) // If already started, re-initialize position
            {
                 _targetPosition = ConvertSimPositionToWorld(LinkedSimBall.Position);
                 transform.position = _targetPosition;
                 UpdateScale();
            }
            enabled = (LinkedSimBall != null);
        }
    }
}