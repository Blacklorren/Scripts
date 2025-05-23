using UnityEngine;

namespace HandballManager.Simulation.Engines // Updated namespace to match new folder structure
{
    /// <summary>
    /// Contains all constants used by the ActionResolver and its calculators for
    /// determining probabilities and outcomes of player actions.
    /// </summary>
    public static class ActionResolverConstants
    {
        // General
        public const float MAX_PRESSURE_DIST = 2.5f; // Max distance opponent exerts pressure from
        public const float MIN_DISTANCE_CHECK_SQ = 0.01f; // Squared distance threshold for near-zero checks
        public const float GOAL_LINE_BUFFER = 0.01f; // 1cm buffer for goal line crossing detection

        // Stamina System Constants (fatigue = 1 - Stamina)
        public const float FATIGUE_ACCUMULATION_RATE = 0.02f;   // How quickly fatigue accumulates with movement (per second at full effort) - tuned for realism
        public const float FATIGUE_RECOVERY_RATE = 0.025f;      // How quickly fatigue recovers when resting (per second) - tuned for realism
        public const float FATIGUE_RECOVERY_THRESHOLD = 0.18f;  // Movement intensity below which recovery occurs (fraction of max speed)

        // Pass Constants
        public const float BASE_PASS_ACCURACY = 0.92f;
        public const float PASS_DISTANCE_FACTOR = 0.03f;   // Accuracy penalty per meter
        public const float PASS_PRESSURE_FACTOR = 0.4f;   // Penalty per pressure unit
        public const float PASS_FATIGUE_FACTOR = 0.3f;    // Penalty per unit fatigue for pass accuracy
        public const float PASS_COMPOSURE_MAX_EFFECT = 0.7f; // Max composure effect on pressure penalty
        public const float PASS_SKILL_WEIGHT_PASSING = 0.6f;
        public const float PASS_SKILL_WEIGHT_DECISION = 0.2f;
        public const float PASS_SKILL_WEIGHT_TECHNIQUE = 0.2f;
        public const float PASS_ACCURACY_SKILL_MIN_MOD = 0.7f; // Min multiplier from skill (at 0 skill)
        public const float PASS_ACCURACY_SKILL_MAX_MOD = 1.15f; // Max multiplier from skill (at 100 skill)
        public const float PASS_ACCURATE_ANGLE_OFFSET_RANGE = 5f; // Degrees +/- for accurate passes
        public const float PASS_INACCURATE_ANGLE_OFFSET_MIN = 15f; // Min degrees offset
        public const float PASS_INACCURATE_ANGLE_OFFSET_MAX = 45f; // Max degrees offset
        public const float PASS_INACCURATE_SPEED_MIN_FACTOR = 0.3f;
        public const float PASS_INACCURATE_SPEED_MAX_FACTOR = 0.6f;
        public const float PASS_BASE_LAUNCH_ANGLE_DEG = 4.0f; // Base upward angle for passes
        public const float PASS_LAUNCH_ANGLE_VARIANCE_DEG = 2.0f; // Variance in launch angle

        // Interception Constants
        public const float INTERCEPTION_BASE_CHANCE = 0.12f;
        public const float INTERCEPTION_ATTRIBUTE_WEIGHT = 0.6f;   // Influence of skills vs position
        public const float INTERCEPTION_POSITION_WEIGHT = 0.4f;    // Influence of position vs skills
        public const float INTERCEPTION_SKILL_WEIGHT_ANTICIPATION = 0.6f;
        public const float INTERCEPTION_SKILL_WEIGHT_AGILITY = 0.2f;
        public const float INTERCEPTION_SKILL_WEIGHT_POSITIONING = 0.2f;
        public const float INTERCEPTION_SKILL_MIN_MOD = 0.5f;
        public const float INTERCEPTION_SKILL_MAX_MOD = 1.5f;
        public const float INTERCEPTION_PASS_PROGRESS_BASE_FACTOR = 0.6f;
        public const float INTERCEPTION_PASS_PROGRESS_MIDPOINT_BONUS = 0.4f; // Bonus applied at midpoint (using Sin)
        public const float INTERCEPTION_PASS_SPEED_MAX_PENALTY = 0.5f; // Max penalty (50%) for fastest passes
        public const float INTERCEPTION_CLOSING_FACTOR_MIN_SCALE = 0.5f; // Min multiplier if moving directly away
        public const float INTERCEPTION_CLOSING_FACTOR_MAX_SCALE = 1.2f; // Max multiplier if moving directly towards
        // Stamina penalty for interception (fatigue = 1 - Stamina, based on real-world handball athlete data)
        // At max fatigue, interception chance is reduced by up to 35% (typical observed technical decline is 20-40%)
        public const float INTERCEPTION_FATIGUE_MIN_EFFECT = 0.0f;  // No penalty when fresh
        public const float INTERCEPTION_FATIGUE_MAX_EFFECT = 0.35f; // 35% penalty at max fatigue
        // Assume MatchSimulator.INTERCEPTION_RADIUS is accessible or move value here
        public const float INTERCEPTION_RADIUS = 1.5f;
        public const float BLOCK_RADIUS = 1.0f; // Block detection radius in meters
        public const float SAVE_REACH_BUFFER = 0.3f; // Extra reach margin for goalkeeper saves (in meters)
        public const float BASE_SAVE_PROBABILITY = 0.3f; // Base probability for goalkeeper saves (average ~30%)
        public const float SAVE_REFLEX_MOD_MIN = 0.7f; // Poor reflexes
        public const float SAVE_REFLEX_MOD_MAX = 1.3f; // Excellent reflexes
        public const float CLOSE_SHOT_DISTANCE = 6f; // 6m or less is a close shot
        public const float LONG_SHOT_DISTANCE = 9f; // 9m or more is a long shot
        public const float CLOSE_SHOT_SAVE_MOD = 0.6f; // Close shot: harder to save (reduce probability)
        public const float LONG_SHOT_SAVE_MOD = 1.4f; // Long shot: easier to save (increase probability)
        public const float INTERCEPTION_RADIUS_EXTENDED_FACTOR = 1.3f;
        public const float LOOSE_BALL_PICKUP_RADIUS = 1.5f; // Distance in meters for ball pickup detection
        public const int MIN_PICKUP_TECHNIQUE = 30; // Minimum technique required to pick up loose ball
        public const int MIN_PICKUP_ANTICIPATION = 30; // Minimum anticipation required to pick up loose ball

        // Pass Constants
        public const float PASS_BASE_SPEED = 13f; // Base speed for a standard pass in m/s
        
        // Shot Constants
        public const float SHOT_ACCURACY_BASE = 100f;            // Maximum potential accuracy roll (used with player accuracy stat)
        public const float SHOT_MAX_ANGLE_OFFSET_DEGREES = 15f;  // Max degrees offset for lowest accuracy shot
        public const float SHOT_PRESSURE_INACCURACY_MOD = 1.5f;  // Pressure multiplier on potential angle offset
        public const float SHOT_COMPOSURE_FACTOR = 0.6f;         // How much high composure reduces pressure effect (0=none, 1=full)
        public const float SHOT_MAX_DEVIATION_CLAMP_FACTOR = 1.5f; // Max multiplier clamp for angle deviation
        public const float SHOT_BASE_LAUNCH_ANGLE_DEG = 12.0f; // Base upward angle for shots
        public const float SHOT_LAUNCH_ANGLE_VARIANCE_DEG = 4.0f; // Variance in launch angle
        public const float SHOT_MAX_SPIN_MAGNITUDE = 80.0f; // Radians per second (Needs Tuning!)
        public const float SHOT_TECHNIQUE_SPIN_FACTOR = 0.7f; // How much technique affects spin magnitude
        public const float SHOT_TYPE_SPIN_FACTOR = 1.0f; // Placeholder for different shot types
        // Assume MatchSimulator.SHOT_BASE_SPEED is accessible or move value here
        public const float SHOT_BASE_SPEED = 22.0f;

        public const float PASS_DISTANCE_MAX_PENALTY_ABS = 0.35f;

        // Tackle Constants
        public const float BASE_TACKLE_SUCCESS = 0.40f;
        public const float BASE_TACKLE_FOUL_CHANCE = 0.25f;
        public const float TACKLE_ATTRIBUTE_SCALING = 0.5f;        // Max bonus/penalty from skill ratio
        public const float MIN_TACKLE_TARGET_SKILL_DENOMINATOR = 10f; // Safety for division
        public const float TACKLE_SUCCESS_SKILL_RANGE_MOD = 1.5f;  // Multiplier for skill effect range on success
        public const float TACKLE_FOUL_SKILL_RANGE_MOD = 0.5f;     // Multiplier for skill effect range on foul chance
        public const float TACKLE_AGGRESSION_FOUL_FACTOR_MIN = 0.7f;
        public const float TACKLE_AGGRESSION_FOUL_FACTOR_MAX = 1.5f;
        public const float TACKLE_FROM_BEHIND_FOUL_MOD = 1.6f;     // Increased penalty
        public const float TACKLE_HIGH_SPEED_FOUL_MOD = 1.4f;      // Max multiplier for high closing speed foul chance
        public const float TACKLE_HIGH_SPEED_THRESHOLD_FACTOR = 0.6f; // % of Max Player Speed to trigger high speed check
        public const float TACKLE_CLEAR_CHANCE_FOUL_MOD = 2.0f;    // Higher foul chance multiplier if denying chance
        public const float TACKLE_SKILL_WEIGHT_TACKLING = 0.5f;
        public const float TACKLE_SKILL_WEIGHT_STRENGTH = 0.3f;
        public const float TACKLE_SKILL_WEIGHT_ANTICIPATION = 0.2f;
        public const float TARGET_SKILL_WEIGHT_DRIBBLING = 0.4f;
        public const float TARGET_SKILL_WEIGHT_AGILITY = 0.3f;
        public const float TARGET_SKILL_WEIGHT_STRENGTH = 0.2f;
        public const float TARGET_SKILL_WEIGHT_COMPOSURE = 0.1f;
        // Assume MatchSimulator.TACKLE_RADIUS is accessible or move value here
        public const float TACKLE_RADIUS = 1.0f;
        public const float TACKLE_RANGE_CHECK_BUFFER = 1.3f;
        // Assume MatchSimulator.MAX_PLAYER_SPEED is accessible or move value here
        public const float MAX_PLAYER_SPEED = 8.0f;

        // Foul Severity Constants
        public const float FOUL_SEVERITY_FROM_BEHIND_BONUS = 0.2f;
        public const float FOUL_SEVERITY_HIGH_SPEED_THRESHOLD_FACTOR = 0.7f; // Speed threshold for severity increase
        public const float FOUL_SEVERITY_HIGH_SPEED_BONUS = 0.15f;
        public const float FOUL_SEVERITY_AGGRESSION_FACTOR = 0.3f; // How much aggression contributes
        public const float FOUL_SEVERITY_DOGSO_BONUS = 0.4f; // Factor increase for denying clear chance
        public const float FOUL_SEVERITY_DOGSO_RED_CARD_BASE_CHANCE = 0.1f;
        public const float FOUL_SEVERITY_DOGSO_RED_CARD_SEVERITY_SCALE = 0.4f;
        public const float FOUL_SEVERITY_DOGSO_RED_CARD_RECKLESS_BONUS = 0.2f;
        public const float FOUL_SEVERITY_RECKLESS_SPEED_THRESHOLD_FACTOR = 0.8f; // Speed threshold for recklessness check
        public const float FOUL_SEVERITY_RECKLESS_AGGRESSION_THRESHOLD = 65;   // Aggression threshold for recklessness check
        public const float FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_BASE = 0.1f;
        public const float FOUL_SEVERITY_TWO_MINUTE_THRESHOLD_SEVERITY_SCALE = 0.6f;
        public const float FOUL_SEVERITY_RED_CARD_THRESHOLD_BASE = 0.01f;
        public const float FOUL_SEVERITY_RED_CARD_THRESHOLD_SEVERITY_SCALE = 0.15f;

        // Pre-pass Interception Constants
        /// <summary>
        /// Base probability that a defender can intercept a pass before it's released.
        /// </summary>
        public const float PRE_PASS_INTERCEPTION_BASE_CHANCE = 0.2f;

        /// <summary>
        /// Maximum distance from the pass line for a defender to be considered as a potential interceptor.
        /// </summary>
        public const float PRE_PASS_INTERCEPTION_RADIUS = 2.5f;

        /// <summary>
        /// Maximum distance from the passer for a defender to be considered for pre-pass interception.
        /// </summary>
        public const float PRE_PASS_PROXIMITY_RADIUS = 5.0f;

        /// <summary>
        /// Minimum skill modifier for pre-pass interception calculation.
        /// </summary>
        public const float PRE_PASS_SKILL_MIN_MOD = 0.5f;

        /// <summary>
        /// Maximum skill modifier for pre-pass interception calculation.
        /// </summary>
        public const float PRE_PASS_SKILL_MAX_MOD = 2.0f;

        /// <summary>
        /// Weight of anticipation skill in pre-pass interception calculation.
        /// </summary>
        public const float PRE_PASS_SKILL_WEIGHT_ANTICIPATION = 0.6f;

        /// <summary>
        /// Weight of positioning skill in pre-pass interception calculation.
        /// </summary>
        public const float PRE_PASS_SKILL_WEIGHT_POSITIONING = 0.3f;

        /// <summary>
        /// Weight of agility skill in pre-pass interception calculation.
        /// </summary>
        public const float PRE_PASS_SKILL_WEIGHT_AGILITY = 0.1f;

        // Assume MatchSimulator.PitchGeometry... constants are accessible or move values here
        public const float PITCH_LENGTH = 40f; // Example value
        public const float PITCH_WIDTH = 20f;  // Example value
        public const float PITCH_CENTER_Y = PITCH_WIDTH / 2f;
        public const float FREE_THROW_LINE_RADIUS = 9f; // Example value
        public const float GOAL_WIDTH = 3f; // Standard handball goal width (3 meters)

        /// <summary>
        /// Radius (in meters) within which defenders are considered for openness calculation.
        /// </summary>
        public const float OPENNESS_DEFENDER_RADIUS = 2.5f;
        /// <summary>
        /// Minimum factor applied to pass accuracy when target is heavily marked (not open).
        /// </summary>
        public const float OPENNESS_MIN_FACTOR = 0.7f;
        /// <summary>
        /// Minimum factor applied to interception chance when defender is not facing the ball.
        /// </summary>
        public const float AWARENESS_MIN_FACTOR = 0.5f;
        /// <summary>
        /// Maximum factor applied to interception chance when defender is fully aware (facing ball).
        /// </summary>
        public const float AWARENESS_MAX_FACTOR = 1.2f;
    }
}
// --- END OF FILE HandballManager/Simulation/Constants/ActionResolverConstants.cs ---