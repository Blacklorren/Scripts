using UnityEngine;

namespace HandballManager.Core
{
    public static class SimConstants
    {
        // --- Match Timing ---
        public const float HALF_DURATION_SECONDS = 1800f; // 30 minutes per half

        // --- Jumping & Landing ---
        public const float BASE_JUMP_HEIGHT = 0.7f; // meters (average handball jump)
        public const float BASE_JUMP_DURATION = 0.6f; // seconds
        public const float JUMP_MIN_FACTOR = 0.7f; // 70% of base height for low attribute
        public const float JUMP_MAX_FACTOR = 1.3f; // 130% of base height for top attribute
        public const float LANDING_MIN_IMPACT = 0.05f; // minimal agility loss for high jumpers
        public const float LANDING_MAX_IMPACT = 0.20f; // max agility loss for poor jumpers
        public const float LANDING_RECOVERY_TIME = 1.2f; // seconds to recover after landing
        public const float MIN_JUMP_VERTICAL_VELOCITY = 3.0f; // m/s (lowest jump)
        public const float MAX_JUMP_VERTICAL_VELOCITY = 6.0f; // m/s (highest jump)

        // --- Team Identifiers ---
        public const int HOME_TEAM_ID = 0;
        public const int AWAY_TEAM_ID = 1;

        // --- Physics Parameters ---
        public const float EARTH_GRAVITY = 9.81f;
        public const float AIR_DENSITY = 1.225f;
        public const float DRAG_COEFFICIENT = 0.47f;

        // --- Goal Line Impact Prediction ---
        public const float GOAL_PLANE_OFFSET = 0.1f;
        public const float MAX_GOAL_PREDICTION_TIME = 2.0f;
        public const float MIN_GOAL_PREDICTION_TIME = -0.05f;
        public const float GOAL_LINE_X_HOME = 0f;
        public const float VELOCITY_NEAR_ZERO = 0.1f;

        // --- Game Rules ---
        public const float DEFAULT_SUSPENSION_TIME = 120f;
        public const float RED_CARD_SUSPENSION_TIME = float.MaxValue;

        // --- Epsilon ---
        public const float VELOCITY_NEAR_ZERO_SQ = 0.01f;
        public const float FLOAT_EPSILON = 0.0001f;

        // --- Ball Physics ---
        public const float BALL_MASS = 0.425f;
        public const float BALL_RADIUS = 0.095f;
        public const float BALL_CROSS_SECTIONAL_AREA = Mathf.PI * BALL_RADIUS * BALL_RADIUS;
        public const float BALL_DEFAULT_HEIGHT = BALL_RADIUS;
        public static readonly Vector3 GRAVITY = new Vector3(0f, -9.81f, 0f);
        public const float MAGNUS_COEFFICIENT_SIMPLE = 0.0001f;
        public const float SPIN_DECAY_FACTOR = 0.90f;
        public const float COEFFICIENT_OF_RESTITUTION = 0.65f;
        public const float FRICTION_COEFFICIENT_SLIDING = 0.4f;
        public const float FRICTION_COEFFICIENT_ROLLING = 0.015f;
        public const float ROLLING_TRANSITION_VEL_Y_THRESHOLD = 0.2f;
        public const float ROLLING_TRANSITION_VEL_XZ_THRESHOLD = 0.1f;

        // --- Ball State ---
        public const float BALL_OFFSET_FROM_HOLDER = 0.3f;
        public const float BALL_RELEASE_OFFSET = 0.1f;
        public const float BALL_LOOSE_HEIGHT_FACTOR = 1.0f;
        public const float LOOSE_BALL_PICKUP_RADIUS = 1.5f;

        // --- SimPlayer ---
        public const float PLAYER_DEFAULT_MAX_SPEED = 7.0f;
        public const float PLAYER_STAMINA_LOW_THRESHOLD = 0.5f;
        public const float PLAYER_STAMINA_MIN_SPEED_FACTOR = 0.4f;
        public const int PLAYER_DEFAULT_ATTRIBUTE_VALUE = 50;
        public const float PLAYER_DRIBBLING_SPEED_MULTIPLIER = 0.85f; // Speed multiplier when dribbling

        // --- Player Collision ---
        public const float PLAYER_COLLISION_RADIUS = 0.4f;
        public const float PLAYER_COLLISION_DIAMETER = PLAYER_COLLISION_RADIUS * 2f;
        public const float PLAYER_COLLISION_DIAMETER_SQ = PLAYER_COLLISION_DIAMETER * PLAYER_COLLISION_DIAMETER;

        // --- Stamina Constants ---
        public const float BASE_STAMINA_DRAIN_PER_SECOND = 0.002f;
        public const float SPRINT_STAMINA_MULTIPLIER = 2.5f;
        public const float PLAYER_STAMINA_DRAIN_RATE = 0.05f; // Base drain per second at max intensity (from Sim)
        public const float PLAYER_STAMINA_RECOVERY_RATE = 0.08f; // Recovery per second when idle (from Sim)
        public const float PLAYER_STAMINA_POSSESSION_DRAIN_MULTIPLIER = 1.2f; // Multiplier for drain while holding ball (from Sim)

        // --- Pitch Dimensions ---
        public const float DEFAULT_PITCH_LENGTH = 40.0f;
        public const float DEFAULT_PITCH_WIDTH = 20.0f;

        // --- Set Piece Rules ---
        public const float SET_PIECE_DEFENDER_DISTANCE = 3.0f; // Minimum legal distance for defenders on set pieces

        // --- Stumble Mechanics ---
        public const float STUMBLE_DURATION = 0.5f;              // seconds players remain stumbling after a collision
        public const float STUMBLE_ACCELERATION_FACTOR = 0.5f;    // fraction of normal accel when stumbling
        public const float STUMBLE_SPEED_FACTOR = 0.5f;           // fraction of normal speed when stumbling

        // --- Utility Functions ---
        public static float CalculateMaxSpeed(float speedAttribute)
        {
            // Example calculation: Scale base max speed by attribute (e.g., 5-10 m/s range)
            // Ensure speedAttribute is scaled appropriately (e.g., 0-100)
            float minSpeed = 5.0f;
            float maxSpeed = 10.0f;
            // Use PLAYER_DEFAULT_MAX_SPEED? For now, keep the Sim logic.
            return Mathf.Lerp(minSpeed, maxSpeed, Mathf.Clamp01(speedAttribute / 100f));
        }

        // --- AI Decision Modifiers (Moved from OffensiveAIController) ---
        public const float BASE_ACTION_THRESHOLD = 0.35f;
        public const float SHOT_PREP_TIME_BASE = 0.6f;
        public const float SHOT_PREP_TIME_RANDOM_FACTOR = 0.3f;
        public const float PASS_PREP_TIME_BASE = 0.4f;
        public const float PASS_PREP_TIME_RANDOM_FACTOR = 0.2f;
        public const float MIN_ACTION_TIMER = 0.5f;
        // Phase Modifiers
        public const float TRANSITION_DRIBBLE_MODIFIER = 1.3f;
        public const float TRANSITION_PASS_MODIFIER = 1.15f;
        public const float TRANSITION_SHOOT_MODIFIER = 1.15f;
        public const float TRANSITION_COMPLEX_PASS_MODIFIER = 0.8f;
        public const float TRANSITION_SCREEN_MODIFIER = 0.8f;
        public const float TRANSITION_PREP_TIME_FACTOR = 0.85f;
        public const float TRANSITION_ACTION_THRESHOLD_FACTOR = 0.85f;
        public const float POSITIONAL_SCREEN_MODIFIER = 1.15f;
        public const float POSITIONAL_FORMATION_PASS_MODIFIER = 1.1f;
    }
}
