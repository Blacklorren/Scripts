using System;
using System.Collections.Generic;
using System.Linq; // Add Linq for Sum() and ToList() methods
using HandballManager.Core; // For Enums like PlayerPosition, InjuryStatus, PlayerPersonalityTrait
using UnityEngine; // For Mathf.Clamp, Range attribute, Debug.Log
using HandballManager.Simulation.Engines; // For MatchState

namespace HandballManager.Data
{
    /// <summary>
    /// Represents the player's dominant shooting hand.
    /// </summary>
    public enum Handedness
    {
        Right,
        Left,
        Both
    }

    public enum SquadStatus
    {
        KeyPlayer,
        FirstTeam,
        Rotation,
        Substitute,
        Youth
    }

    /// <summary>
    /// Represents all data associated with a single player.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        /// <summary>
        /// Player's reaction attribute (0-100). Determines response speed in critical moments.
        /// </summary>
        public int Reaction { get; set; } = 50; // Default value, adjust as needed
        /// <summary>
        /// List of player traits (special skills, tags, etc.)
        /// </summary>
        public List<string> Traits { get; set; } = new List<string>();
    
        /// <summary>
        /// The current MatchState this player is participating in (set during active matches).
        /// </summary>
        public MatchState CurrentMatchState { get; set; }

        /// <summary>
        /// The last time (in seconds since game start) this player committed a step violation.
        /// </summary>
        public float LastStepViolationTime { get; set; }

        /// <summary>
        /// Temporary bonus applied during ball shielding situations (set by physics/collision logic).
        /// Should be reset/managed by simulation step as needed.
        /// </summary>
        public float BallProtectionBonus { get; set; } = 0f;

        // --- Base Attributes ---
        public BaseData BaseData { get; private set; }

        // --- Jumping Mechanics ---
        public bool IsJumping { get; private set; } = false;
        public float JumpHeight { get; private set; } = 0f;
        public float JumpTime { get; private set; } = 0f;
        public float JumpDuration { get; private set; } = 0f;
        public float VerticalPosition { get; private set; } = 0f;

        /// <summary>
        /// Calculates the effectiveness of shielding based on Strength and Balance attributes.
        /// Returns a value between SHIELD_MIN_FACTOR and SHIELD_MAX_FACTOR.
        /// </summary>
        public float GetShieldingEffectiveness()
        {
            // Constants for min/max shielding effectiveness
            const float SHIELD_MIN_FACTOR = 0.10f;
            const float SHIELD_MAX_FACTOR = 0.60f;
            float strength = BaseData?.Strength ?? 50f;
            float agility = BaseData?.Agility ?? 50f;
            // Weighted average: Strength (60%), Balance (40%)
            float attributeScore = (strength * 0.6f + agility * 0.4f) / 100f;
            return Mathf.Lerp(SHIELD_MIN_FACTOR, SHIELD_MAX_FACTOR, attributeScore);
        }

        public void InitiateJump()
        {
            if (!IsJumping)
            {
                IsJumping = true;
                JumpTime = 0f;
                float jumpFactor = Mathf.Lerp(
                    SimConstants.JUMP_MIN_FACTOR,
                    SimConstants.JUMP_MAX_FACTOR,
                    (BaseData?.Jumping ?? SimConstants.PLAYER_DEFAULT_ATTRIBUTE_VALUE) / 100f);
                JumpHeight = SimConstants.BASE_JUMP_HEIGHT * jumpFactor;
                JumpDuration = SimConstants.BASE_JUMP_DURATION;
            }
        }

        public void UpdateJump(float deltaTime)
        {
            if (IsJumping)
            {
                JumpTime += deltaTime;
                float jumpProgress = JumpTime / JumpDuration;
                float verticalPosition = Mathf.Sin(jumpProgress * Mathf.PI) * JumpHeight;
                VerticalPosition = verticalPosition;
                if (JumpTime >= JumpDuration)
                {
                    IsJumping = false;
                    VerticalPosition = 0f;
                    HandleJumpLanding();
                }
            }
        }

        private void HandleJumpLanding()
        {
            float landingStabilityLoss = Mathf.Lerp(
                SimConstants.LANDING_MAX_IMPACT,
                SimConstants.LANDING_MIN_IMPACT,
                (BaseData?.Jumping ?? SimConstants.PLAYER_DEFAULT_ATTRIBUTE_VALUE) / 100f);
            ApplyTemporaryAgilityReduction(landingStabilityLoss, SimConstants.LANDING_RECOVERY_TIME);
        }

        // You must implement this method elsewhere in the class if not present
        public void ApplyTemporaryAgilityReduction(float reduction, float duration)
        {
            // TODO: Implement agility reduction logic
        }

        public SquadStatus SquadStatus { get; set; } = SquadStatus.FirstTeam;

        // --- Identifiers ---
        public int PlayerID { get; set; } // Or use Guid: public Guid PlayerID { get; set; } = Guid.NewGuid();
        public string FirstName { get; set; } = "Default";
        public string LastName { get; set; } = "Player";

        /// <summary>
        /// The player's dominant shooting hand.
        /// </summary>
        public Handedness ShootingHand { get; set; } = Handedness.Right;
        public string KnownAs { get; set; } // Nickname or shorter name
        public string FullName => string.IsNullOrEmpty(KnownAs) ? $"{FirstName} {LastName}" : KnownAs; // Calculated full name
        public int Age { get; set; } = 20;
        public DateTime DateOfBirth { get; set; } = DateTime.Now.AddYears(-20); // Calculate Age from this?
        public string Nationality { get; set; } = "Unknown";

        /// <summary>
        /// Player height in centimeters.
        /// </summary>
        public int Height { get; set; } = 180;

        /// <summary>
        /// Player weight in kilograms.
        /// </summary>
        public int Weight { get; set; } = 75;

        // --- Contract/Status ---
        public int? CurrentTeamID { get; set; } // Nullable if free agent
        public DateTime ContractExpiryDate { get; set; } = DateTime.MinValue;
        public float Wage { get; set; } = 1000f; // Per week/month? Define standard (e.g., weekly).
        /// <summary>Morale scaled 0.0 (Very Unhappy) to 1.0 (Very Happy).</summary>
        [Range(0f, 1f)] public float Morale { get; set; } = 0.7f;
        /// <summary>Physical condition/match fitness scaled 0.0 (Exhausted) to 1.0 (Peak).</summary>
        [Range(0f, 1f)] public float Condition { get; set; } = 1.0f;
        public InjuryStatus CurrentInjuryStatus { get; set; } = InjuryStatus.Healthy;
        public DateTime InjuryReturnDate { get; set; } = DateTime.MinValue; // Date when player returns from injury
        public string InjuryDescription { get; set; } = ""; // e.g., "Sprained Ankle"
        public TransferStatus TransferStatus { get; set; } = TransferStatus.Unavailable; // Player's availability

        // --- Position ---
        public PlayerPosition PrimaryPosition { get; set; } = PlayerPosition.CentreBack;
        /// <summary>Familiarity with each position, scaled 0.0 (Unfamiliar) to 1.0 (Natural).</summary>
        public Dictionary<PlayerPosition, float> PositionalFamiliarity { get; set; } = new Dictionary<PlayerPosition, float>();

        // --- Technical Attributes (Scale 1-100 suggested, could be 1-20) ---
        [Header("Technical")]
        [Range(1, 100)] public int ShootingPower { get; set; } = 50;
        [Range(1, 100)] public int ShootingAccuracy { get; set; } = 50; // Includes placement
        [Range(1, 100)] public int Passing { get; set; } = 50; // Accuracy and weighting of passes
        [Range(1, 100)] public int Dribbling { get; set; } = 50; // Ball handling/control while moving
        [Range(1, 100)] public int Technique { get; set; } = 50; // General ball skill, first touch, feints
        [Range(1, 100)] public int Tackling { get; set; } = 50; // Defensive ability to dispossess opponent
        [Range(1, 100)] public int Blocking { get; set; } = 50; // Ability to block shots/passes defensively

        // --- Physical Attributes (Scale 1-100) ---
        [Header("Physical")]
        [Range(1, 100)] public int Speed { get; set; } = 50; // Top running speed (Acceleration could be separate?)
        [Range(1, 100)] public int Agility { get; set; } = 50; // Ability to change direction quickly
        [Range(1, 100)] public int Strength { get; set; } = 50; // Physical power, resistance in duels
        [Range(1, 100)] public int Jumping { get; set; } = 50; // Vertical leap height
        [Range(1, 100)] public int Stamina { get; set; } = 50; // Ability to maintain physical exertion
        /// <summary>Natural fitness level / ability to recover condition.</summary>
        [Range(1, 100)] public int NaturalFitness { get; set; } = 50;
        /// <summary>Resistance to getting injured. Higher is better.</summary>
        [Range(1, 100)] public int Resilience { get; set; } = 50; // Injury proneness/resistance

        // --- Mental Attributes (Scale 1-100) ---
        [Header("Mental")]
        [Range(1, 100)] public int Aggression { get; set; } = 50; // Willingness to engage physically (defense/attack) - can be good/bad
        [Range(1, 100)] public int Bravery { get; set; } = 50; // Willingness to risk injury (block shots, contest headers)
        [Range(1, 100)] public int Composure { get; set; } = 50; // Ability to perform under pressure
        [Range(1, 100)] public int Concentration { get; set; } = 50; // Maintaining focus throughout match
        [Range(1, 100)] public int Anticipation { get; set; } = 50; // Reading the game, predicting opponent actions
        [Range(1, 100)] public int DecisionMaking { get; set; } = 50; // Choosing the right action (pass, shoot, defend etc.)
        [Range(1, 100)] public int Teamwork { get; set; } = 50; // Playing well with teammates, following tactics
        [Range(1, 100)] public int WorkRate { get; set; } = 50; // Willingness to exert effort off the ball
        [Range(1, 100)] public int Leadership { get; set; } = 50; // Ability to influence teammates positively
        [Range(1, 100)] public int Positioning { get; set; } = 50; // Defensive positioning sense for field players
        [Range(1, 100)] public int Determination { get; set; } = 50; // Added: Mental strength and perseverance

        // --- Goalkeeping Attributes (Scale 1-100) - Only relevant for Goalkeepers ---
        [Header("Goalkeeping")]
        [Range(1, 100)] public int Reflexes { get; set; } = 20; // Reaction speed to shots
        [Range(1, 100)] public int Handling { get; set; } = 20; // Ability to catch/hold the ball securely
        [Range(1, 100)] public int PositioningGK { get; set; } = 20; // Positioning in the goal area, cutting angles
        [Range(1, 100)] public int OneOnOnes { get; set; } = 20; // Ability in one-on-one situations
        [Range(1, 100)] public int PenaltySaving { get; set; } = 20; // Specific skill for saving penalties
        [Range(1, 100)] public int Throwing { get; set; } = 20; // Accuracy and distance of throws (for counter-attacks)
        [Range(1, 100)] public int Communication { get; set; } = 20; // Organizing the defense

        // --- Potential & Hidden ---
        [Header("Potential")]
        /// <summary>Potential Ability rating (Scale 1-100). Represents the theoretical maximum overall skill.</summary>
        [Range(1, 100)] public int PotentialAbility { get; set; } = 60; // Renamed from PotentialRating for clarity
        /// <summary>Current Ability (calculated dynamically or stored) represents overall current skill level.</summary>
        [Range(1, 100)] public int CurrentAbility { get; private set; } // Keep private set, calculated internally

        // --- Personality ---
        /// <summary>The player's personality trait that influences in-match behavior.</summary>
        public PlayerPersonalityTrait Personality { get; set; } = PlayerPersonalityTrait.Balanced;

        // Hidden Attributes (Example - influence personality, development, consistency)
        // public int Professionalism { get; set; } = 50;
        // public int Ambition { get; set; } = 50;
        // public int Consistency { get; set; } = 50;
        // public int BigMatchTemperament { get; set; } = 50;
        // public int InjuryProneness { get; set; } // Could replace Resilience or work with it


        // --- Constructor (Example) ---
        public PlayerData()
        {
            // Initialize positional familiarity dictionary for all positions
            foreach (PlayerPosition pos in Enum.GetValues(typeof(PlayerPosition)))
            {
                PositionalFamiliarity[pos] = 0.1f; // Start with low familiarity everywhere
            }
            // Set primary position familiarity higher
            // Ensure PrimaryPosition has a default value before accessing PositionalFamiliarity with it.
            // The default is CentreBack, which is fine.
            PositionalFamiliarity[PrimaryPosition] = 1.0f;

            // Set a default date of birth if not specified
            if (DateOfBirth == default(DateTime))
            {
                 DateOfBirth = DateTime.Now.AddYears(-Age); // Approximate based on age
            }

            PlayerID = GetNextUniqueID(); // Assign unique ID
            CalculateCurrentAbility(); // Calculate initial CA
            GeneratePersonality(); // FIX: Call personality generation
        }

        // --- Methods (Examples) ---

        /// <summary>
        /// Calculates a weighted Current Ability based on attributes relevant to the primary position.
        /// Needs significant refinement for accurate representation.
        /// </summary>
        /// <returns>A calculated current ability score (1-100).</returns>
        public int CalculateCurrentAbility()
        {
            // TODO: Implement a much more sophisticated weighting system per position.
            // This is a VERY ROUGH placeholder average.
            int sum = 0;
            int count = 0;
            Action<int> AddStat = (stat) => { sum += stat; count++; };

            // Add common mental attributes
            AddStat(Composure); AddStat(Concentration); AddStat(Anticipation); AddStat(DecisionMaking); AddStat(Teamwork); AddStat(WorkRate); AddStat(Determination); // Include Determination

            // Add common physical attributes
            AddStat(Speed); AddStat(Agility); AddStat(Strength); AddStat(Jumping); AddStat(Stamina); AddStat(NaturalFitness);

            // Add position-specific attributes
            switch (PrimaryPosition)
            {
                case PlayerPosition.Goalkeeper:
                    AddStat(Reflexes); AddStat(Handling); AddStat(PositioningGK); AddStat(OneOnOnes); AddStat(PenaltySaving); AddStat(Throwing); AddStat(Communication);
                    // Weight GK stats higher
                    sum += Reflexes + Handling + PositioningGK; count += 3; // Add some key ones again for weight
                    break;

                case PlayerPosition.LeftWing:
                case PlayerPosition.RightWing:
                    AddStat(ShootingAccuracy); AddStat(Dribbling); AddStat(Technique); AddStat(Passing); // Wings need skill & finishing
                    AddStat(Speed); AddStat(Agility); // Speed is crucial
                    sum += Speed + Agility + ShootingAccuracy + Dribbling; count += 4; // Weight key wing attributes
                    break;

                case PlayerPosition.LeftBack:
                case PlayerPosition.RightBack:
                    AddStat(ShootingPower); AddStat(ShootingAccuracy); AddStat(Passing); AddStat(Technique); // Backs shoot and pass
                    AddStat(Strength); AddStat(Tackling); AddStat(Blocking); // Need some defense/physicality
                    sum += ShootingPower + Passing + Strength; count += 3; // Weight key back attributes
                    break;

                case PlayerPosition.CentreBack: // Playmaker role
                    AddStat(Passing); AddStat(Technique); AddStat(DecisionMaking); AddStat(Anticipation); // Core playmaking
                    AddStat(ShootingPower); AddStat(ShootingAccuracy); // Often shoot too
                    sum += Passing + Technique + DecisionMaking + Anticipation; count += 4; // Weight key playmaker attributes
                    break;

                case PlayerPosition.Pivot: // Line player
                    AddStat(Strength); AddStat(Blocking); AddStat(ShootingAccuracy); AddStat(Technique); // Physical presence, finishing close range
                    AddStat(Positioning); AddStat(Aggression); // Getting into position, fighting for space
                    sum += Strength + Blocking + ShootingAccuracy + Positioning; count += 4; // Weight key pivot attributes
                    break;
            }

            // Basic average - replace with proper weights!
            CurrentAbility = (count > 0) ? Mathf.Clamp(sum / count, 1, 100) : 1;
            return CurrentAbility;
        }

        /// <summary>
        /// Checks if the player is currently injured based on status and return date.
        /// </summary>
        public bool IsInjured()
        {
            // Check status first, then ensure return date is in the future
            if (CurrentInjuryStatus != InjuryStatus.Healthy)
            {
                try
                {
                    // Ensure GameManager and TimeManager instances exist before accessing
                    if (Core.GameManager.Instance != null && Core.GameManager.Instance.TimeManager != null)
                    {
                         // Compare dates only
                         return InjuryReturnDate.Date > Core.GameManager.Instance.TimeManager.CurrentDate.Date;
                    }
                    else
                    {
                        // If managers aren't ready (e.g., during initial setup), assume injured if status is not Healthy
                        Debug.LogWarning($"GameManager or TimeManager not ready for injury check for {FullName}. Assuming injured based on status.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    // Handle other potential exceptions during date comparison
                    Debug.LogWarning($"Error checking injury date for {FullName}: {ex.Message}");
                    return true; // Assume injured if we can't check date properly
                }
            }
            return false; // Not injured if status is Healthy
        }


        /// <summary>
        /// Updates the player's injury status if the return date has passed.
        /// Should be called daily (e.g., by GameManager).
        /// </summary>
        public void UpdateInjuryStatus()
        {
            if (CurrentInjuryStatus != InjuryStatus.Healthy)
            {
                 try
                 {
                     // Ensure GameManager and TimeManager instances exist before accessing
                     if (Core.GameManager.Instance != null && Core.GameManager.Instance.TimeManager != null)
                     {
                         if (InjuryReturnDate.Date <= Core.GameManager.Instance.TimeManager.CurrentDate.Date)
                         {
                             Debug.Log($"{FullName} has recovered from {InjuryDescription}.");
                             CurrentInjuryStatus = InjuryStatus.Healthy;
                             InjuryReturnDate = DateTime.MinValue;
                             InjuryDescription = "";
                             // Player returns with reduced condition after injury
                             Condition = Mathf.Clamp(Condition * 0.7f, 0.1f, 0.8f); // Example: return at 70% of previous, max 80% initially
                         }
                     } else {
                          // Cannot update status if managers aren't ready
                          // Debug.LogWarning($"Cannot update injury status for {FullName}: GameManager or TimeManager not ready.");
                     }
                 }
                 catch (Exception ex)
                 {
                     Debug.LogWarning($"Error updating injury status for {FullName}: {ex.Message}");
                     // Don't change status if date check fails
                 }
            }
        }

        /// <summary>
        /// Inflicts an injury on the player.
        /// </summary>
        /// <param name="status">The severity of the injury.</param>
        /// <param name="durationDays">Estimated duration in days.</param>
        /// <param name="description">Description of the injury.</param>
        public void InflictInjury(InjuryStatus status, int durationDays, string description)
        {
            if (status == InjuryStatus.Healthy) return; // Cannot inflict 'healthy'

            CurrentInjuryStatus = status;
            try
            {
                // Ensure GameManager and TimeManager instances exist before accessing
                if (Core.GameManager.Instance != null && Core.GameManager.Instance.TimeManager != null)
                {
                    InjuryReturnDate = Core.GameManager.Instance.TimeManager.CurrentDate.AddDays(durationDays);
                }
                else
                {
                     // Fallback if game time unavailable (e.g., during setup)
                     Debug.LogWarning($"GameManager/TimeManager not ready. Setting injury return date relative to system time for {FullName}.");
                     InjuryReturnDate = DateTime.Now.Date.AddDays(durationDays);
                }
            }
            catch (Exception ex) {
                 Debug.LogError($"Error setting injury return date for {FullName}: {ex.Message}. Using fallback.");
                 InjuryReturnDate = DateTime.Now.Date.AddDays(durationDays);
            }

            InjuryDescription = description;
            Condition = 0.1f; // Drop condition when injured
            Morale = Mathf.Clamp(Morale - 0.2f, 0f, 1f); // Drop morale

            Debug.LogWarning($"{FullName} injured! Status: {status}, Description: {description}, Out until: {InjuryReturnDate.ToShortDateString()} ({durationDays} days)");
        }

        /// <summary>
        /// Generates a random personality trait for the player based on their position and attributes.
        /// Moved inside the class.
        /// </summary>
        public void GeneratePersonality()
        {
            // Create a weighted random selection based on player attributes
            Dictionary<PlayerPersonalityTrait, float> weights = new Dictionary<PlayerPersonalityTrait, float>();

            // Default weight for all traits
            weights[PlayerPersonalityTrait.Balanced] = 30f;
            weights[PlayerPersonalityTrait.Determined] = 15f;
            weights[PlayerPersonalityTrait.Professional] = 15f;
            weights[PlayerPersonalityTrait.Ambitious] = 10f;
            weights[PlayerPersonalityTrait.Loyal] = 10f;
            weights[PlayerPersonalityTrait.Volatile] = 5f;
            weights[PlayerPersonalityTrait.Aggressive] = 10f;
            weights[PlayerPersonalityTrait.Lazy] = 5f;

            // Adjust weights based on attributes
            if (WorkRate > 80) {
                weights[PlayerPersonalityTrait.Determined] += 15f;
                weights[PlayerPersonalityTrait.Lazy] -= 4f;
            }

            if (Teamwork > 80) {
                weights[PlayerPersonalityTrait.Loyal] += 15f;
                weights[PlayerPersonalityTrait.Ambitious] -= 5f;
            }

            if (Aggression > 80) {
                weights[PlayerPersonalityTrait.Aggressive] += 20f;
                weights[PlayerPersonalityTrait.Professional] -= 5f;
            }

            if (Composure > 80) {
                weights[PlayerPersonalityTrait.Professional] += 15f;
                weights[PlayerPersonalityTrait.Volatile] -= 3f;
            }

            // FIX: Determination attribute reference confirmed to exist
            if (Determination > 80) {
                weights[PlayerPersonalityTrait.Determined] += 15f;
                weights[PlayerPersonalityTrait.Lazy] -= 4f;
            }

            // Position-based adjustments
            if (PrimaryPosition == PlayerPosition.Goalkeeper) {
                weights[PlayerPersonalityTrait.Professional] += 10f;
                weights[PlayerPersonalityTrait.Volatile] -= 3f;
            }

            // Ensure no negative weights
            foreach (var key in weights.Keys.ToList()) { // Using System.Linq here
                weights[key] = Mathf.Max(weights[key], 1f);
            }

            // Select personality based on weights
            float totalWeight = weights.Values.Sum(); // Using System.Linq here
             // Check for zero total weight edge case
             if (totalWeight <= 0f) {
                 Personality = PlayerPersonalityTrait.Balanced; // Default if weights are invalid
                 return;
             }

            float randomValue = UnityEngine.Random.Range(0, totalWeight);
            float cumulativeWeight = 0;

            foreach (var pair in weights) {
                cumulativeWeight += pair.Value;
                if (randomValue <= cumulativeWeight) {
                    Personality = pair.Key;
                    break;
                }
            }
            // In case of floating point issues, ensure a value is assigned
            if (Personality == default && weights.Count > 0) {
                 Personality = weights.Keys.First(); // Assign first trait as fallback
            }
        }


        public override string ToString()
        {
            return $"{FullName} ({Age}, {PrimaryPosition}) - CA: {CurrentAbility}, PA: {PotentialAbility}";
        }

        // Basic placeholder for unique IDs - replace with a robust system if needed
        private static int _nextId = 100; // Start player IDs from 100?
        public static int GetNextUniqueID() { return _nextId++; }

        // --- Handball Step Tracking ---
        public int StepCount { get; private set; } = 0;
        public bool HasBall { get; set; } = false;
        public bool IsDribbling { get; set; } = false;
        /// <summary>
        /// True si le joueur a déjà dribblé depuis la prise de possession (pour la règle de reprise de dribble)
        /// </summary>
        public bool HasDribbledSincePossession { get; set; } = false;


        public void IncrementStep()
        {
            if (HasBall && !IsDribbling)
            {
                StepCount++;
            }
        }

        public void ResetSteps()
        {
            StepCount = 0;
            HasDribbledSincePossession = false;
        }

        public void StartPossession()
        {
            ResetSteps();
        }

        public void LosePossession()
        {
            ResetSteps();
        }

        public void StartDribble()
        {
            IsDribbling = true;
            HasDribbledSincePossession = true;
        }

        public void EndDribble()
        {
            IsDribbling = false;
        }

        public bool IsDoubleDribbleViolation()
        {
            return HasDribbledSincePossession;
        }

        public bool ExceededStepLimit()
        {
            return StepCount > 3;
        }
    } // End PlayerData Class
}