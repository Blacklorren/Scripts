using System;
using System.Collections.Generic;
using System.Linq;
using HandballManager.Core;
using HandballManager.Data;
using HandballManager.Simulation.Engines;
using UnityEngine;

namespace HandballManager.Simulation.Factories
{
    /// <summary>
    /// Responsible for creating PlayerData and SimPlayer objects from database, random generation (youth), or user customization.
    /// Centralizes all player instantiation logic for consistency and maintainability.
    /// </summary>
    public class PlayerFactory
    {
        private readonly System.Random _random;
        private readonly PlayerDevelopment _playerDevelopment;

        /// <summary>
        /// Constructeur principal de PlayerFactory.
        /// </summary>
        /// <param name="random">Générateur aléatoire (optionnel)</param>
        /// <param name="playerDevelopment">Instance pour le développement des joueurs (optionnel)</param>
        public PlayerFactory(System.Random random = null, PlayerDevelopment playerDevelopment = null)
        {
            _random = random ?? new System.Random();
            _playerDevelopment = playerDevelopment;
        }

        /// <summary>
        /// Creates a PlayerData object from a database entry (or DTO).
        /// </summary>
        public PlayerData CreateFromDatabase(PlayerDatabaseEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            // Create PlayerData directly, assigning all properties from entry
            var player = new PlayerData()
            {
                // --- Identifiers & Basic Info --- (Moved from old PlayerData initializer)
                PlayerID = entry.PlayerID,
                FirstName = entry.FirstName,
                LastName = entry.LastName,
                Age = entry.Age,
                DateOfBirth = entry.DateOfBirth,
                Nationality = entry.Nationality,
                Reaction = entry.Reaction,

                // --- Contract/Status --- (Moved from old PlayerData initializer)
                CurrentTeamID = entry.CurrentTeamID,
                ContractExpiryDate = entry.ContractExpiryDate,
                Wage = entry.Wage,
                Morale = entry.Morale,
                Condition = entry.Condition,
                CurrentInjuryStatus = entry.CurrentInjuryStatus,
                InjuryReturnDate = entry.InjuryReturnDate,
                InjuryDescription = entry.InjuryDescription,
                TransferStatus = entry.TransferStatus,

                // --- Position & Handedness --- (Moved from old PlayerData initializer)
                PrimaryPosition = entry.PrimaryPosition,
                PreferredHand = entry.ShootingHand, // Assign to the correct PlayerData property 'PreferredHand'
                PositionalFamiliarity = entry.PositionalFamiliarity ?? new Dictionary<PlayerPosition, float>(),

                // --- Traits & Personality --- (Moved from old PlayerData initializer)
                Traits = entry.Traits ?? new List<string>(),
                Personality = entry.Personality,

                // --- Technical Attributes --- (From old BaseData initializer)
                ShootingPower = entry.ShootingPower,
                ShootingAccuracy = entry.ShootingAccuracy,
                Passing = entry.Passing,
                Dribbling = entry.Dribbling,
                Technique = entry.Technique,
                Tackling = entry.Tackling,
                Blocking = entry.Blocking,

                // --- Physical Attributes --- (From old BaseData initializer)
                Speed = entry.Speed,
                Agility = entry.Agility,
                Strength = entry.Strength,
                Jumping = entry.Jumping,
                Stamina = entry.Stamina,
                NaturalFitness = entry.NaturalFitness,
                Resilience = entry.Resilience,
                Height = entry.Height,
                Weight = entry.Weight,

                // --- Mental Attributes --- (From old BaseData initializer)
                Aggression = entry.Aggression,
                Bravery = entry.Bravery,
                Composure = entry.Composure,
                Anticipation = entry.Anticipation,
                Concentration = entry.Concentration,
                Vision = entry.Vision,
                Creativity = entry.Creativity,
                DecisionMaking = entry.DecisionMaking,
                Teamwork = entry.Teamwork,
                WorkRate = entry.WorkRate,
                Leadership = entry.Leadership,
                Positioning = entry.Positioning,
                Determination = entry.Determination,

                // --- Goalkeeping Attributes --- (From old BaseData initializer)
                Reflexes = entry.Reflexes,
                Handling = entry.Handling,
                PositioningGK = entry.PositioningGK,
                OneOnOnes = entry.OneOnOnes,
                PenaltySaving = entry.PenaltySaving,
                Throwing = entry.Throwing,
                Communication = entry.Communication,

                // --- Potential & Hidden Attributes --- (From old BaseData initializer)
                PotentialAbility = entry.PotentialAbility
            };

            player.GeneratePersonality();
            player.PositionalFamiliarity = GenerateInitialFamiliarity(player.PrimaryPosition, player);
            return player;
        }

        /// <summary>
        /// Creates a new youth PlayerData object with randomized attributes and unique ID.
        /// </summary>
        public PlayerData CreateRandomYouth(string nationality, PlayerPosition? forcedPosition = null, int minAge = 15, int maxAge = 18)
        {
            int age = _random.Next(minAge, maxAge + 1);
            string firstName = GenerateRandomFirstName(nationality);
            string lastName = GenerateRandomLastName(nationality);
            string fullName = string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ? GenerateRandomName(nationality) : $"{firstName} {lastName}";

            // Assign handedness (80% right, 18% left, 2% both)
            Handedness handedness = GenerateRandomHandedness();

            // Determine eligible positions based on handedness
            List<PlayerPosition> eligiblePositions = GetEligiblePrimaryPositions(handedness);
            PlayerPosition primaryPosition = forcedPosition ?? eligiblePositions[_random.Next(eligiblePositions.Count)];

            // Generate core abilities
            int potential = RandomAttribute(70, 100);
            int current = RandomAttribute(30, 60);

            // Calculate wage based on age, potential, and current ability
            float wage = CalculateYouthWage(age, potential, current);

            var baseData = GenerateRandomBaseData(potential, primaryPosition);

            var player = new PlayerData(baseData)
            {
                // --- Identifiers ---
                PlayerID = PlayerData.GetNextUniqueID(),
                FirstName = firstName,
                LastName = lastName,
                Age = age,
                DateOfBirth = GenerateBirthDate(age),
                Nationality = nationality,

                // --- Contract/Status ---
                CurrentTeamID = null,
                ContractExpiryDate = DateTime.Now.AddYears(2), // Example: 2-year contract
                Wage = wage,
                Morale = 0.7f,
                Condition = 1.0f,
                CurrentInjuryStatus = InjuryStatus.Healthy,
                InjuryReturnDate = DateTime.MinValue,
                InjuryDescription = string.Empty,
                TransferStatus = TransferStatus.Unavailable,

                // --- Position ---
                PrimaryPosition = primaryPosition,
                PreferredHand = handedness, 

                // --- Skills ---
                Traits = new List<string>(), // Les traits seront générés juste après

            };
            player.Traits = GenerateTraits(player);
            player.GeneratePersonality();

            // Bias numeric personality attributes according to trait
            switch (player.Personality)
            {
                case PlayerPersonalityTrait.Ambitious:
                    player.Ambition = Math.Max(player.Ambition, 80);
                    break;
                case PlayerPersonalityTrait.Determined:
                    player.Determination = Math.Max(player.Determination, 80);
                    break;
                case PlayerPersonalityTrait.Professional:
                    player.Professionalism = Math.Max(player.Professionalism, 80);
                    player.Composure = Math.Max(player.Composure, 80);
                    break;
                case PlayerPersonalityTrait.Aggressive:
                    player.Aggression = Math.Max(player.Aggression, 80);
                    break;
                case PlayerPersonalityTrait.Leader:
                    player.Leadership = Math.Max(player.Leadership, 80);
                    break;
                case PlayerPersonalityTrait.Loyal:
                    player.Loyalty = Math.Max(player.Loyalty, 80);
                    player.Teamwork = Math.Max(player.Teamwork, 80);
                    break;
                // Add more cases as needed
            }
            player.PositionalFamiliarity = GenerateInitialFamiliarity(player.PrimaryPosition, player);
            ValidatePlayer(player);
            return player;
        }

        private BaseData GenerateRandomBaseData(int potential, PlayerPosition? position = null)
        {
            var baseData = new BaseData
            {
                ShootingPower = RandomAttribute(40, 80),
                ShootingAccuracy = RandomAttribute(40, 80),
                Passing = RandomAttribute(40, 80),
                Dribbling = RandomAttribute(40, 80),
                Technique = RandomAttribute(40, 80),
                Tackling = RandomAttribute(40, 80),
                Blocking = RandomAttribute(40, 80),
                Speed = RandomAttribute(40, 80),
                Agility = RandomAttribute(40, 80),
                Strength = RandomAttribute(40, 80),
                Jumping = RandomAttribute(40, 80),
                Stamina = RandomAttribute(40, 80),
                NaturalFitness = RandomAttribute(40, 80),
                Resilience = RandomAttribute(40, 80),
                Aggression = RandomAttribute(40, 80),
                Bravery = RandomAttribute(40, 80),
                Composure = RandomAttribute(40, 80),
                Anticipation = _random.Next(30, 80),
                Concentration = _random.Next(30, 80),
                Vision = _random.Next(30, 80),
                DecisionMaking = _random.Next(30, 80),
                Teamwork = _random.Next(30, 80),
                WorkRate = _random.Next(30, 80),
                Leadership = _random.Next(30, 80),
                Positioning = _random.Next(30, 80),
                Determination = _random.Next(30, 80),
                TacticalAwareness = _random.Next(30, 80),
                Reflexes = RandomAttribute(40, 80),
                Handling = RandomAttribute(20, 60),
                PositioningGK = RandomAttribute(20, 60),
                OneOnOnes = RandomAttribute(20, 60),
                PenaltySaving = RandomAttribute(20, 60),
                Throwing = RandomAttribute(20, 60),
                Communication = RandomAttribute(20, 60),
                PotentialAbility = potential
            };

            // Ajustements selon le poste
            if (position.HasValue)
            {
                switch (position.Value)
                {
                    case PlayerPosition.Goalkeeper:
                        // Gardien : réflexes, handling, penalty saving très élevés
                        baseData.Reflexes = RandomAttribute(70, 99);
                        baseData.Handling = RandomAttribute(70, 99);
                        baseData.PenaltySaving = RandomAttribute(70, 99);
                        baseData.PositioningGK = RandomAttribute(70, 99);
                        baseData.OneOnOnes = RandomAttribute(60, 95);
                        baseData.Throwing = RandomAttribute(60, 95);
                        // Faiblesse sur shooting, dribbling, etc.
                        baseData.ShootingPower = RandomAttribute(20, 40);
                        baseData.ShootingAccuracy = RandomAttribute(20, 40);
                        baseData.Dribbling = RandomAttribute(20, 40);
                        break;
                    case PlayerPosition.LeftWing:
                    case PlayerPosition.RightWing:
                        // Ailier : vitesse, agilité, saut élevés
                        baseData.Speed = RandomAttribute(75, 99);
                        baseData.Agility = RandomAttribute(75, 99);
                        baseData.Jumping = RandomAttribute(65, 95);
                        baseData.ShootingAccuracy = RandomAttribute(60, 90);
                        break;
                    case PlayerPosition.Pivot:
                        // Pivot : force, équilibre, technique
                        baseData.Strength = RandomAttribute(75, 99);
                        baseData.Resilience = RandomAttribute(70, 95);
                        baseData.Technique = RandomAttribute(60, 90);
                        baseData.Positioning = RandomAttribute(65, 95);
                        break;
                    case PlayerPosition.LeftBack:
                    case PlayerPosition.RightBack:
                        // Arrière : puissance de tir, vision, taille
                        baseData.ShootingPower = RandomAttribute(75, 99);
                        baseData.Vision = RandomAttribute(65, 95);
                        baseData.Technique = RandomAttribute(60, 90);
                        baseData.Agility = RandomAttribute(60, 90);
                        break;
                    case PlayerPosition.CentreBack:
                        // Demi-centre : vision, décision, technique, leadership
                        baseData.Vision = RandomAttribute(75, 99);
                        baseData.DecisionMaking = RandomAttribute(75, 99);
                        baseData.Technique = RandomAttribute(65, 90);
                        baseData.Leadership = RandomAttribute(60, 90);
                        break;
                    // Ajoute d'autres cas si besoin
                }
            }
            return baseData;
        }

        // Génère une liste de traits cohérents en fonction du poste, du potentiel, etc.
        private List<string> GenerateTraits(PlayerData player)
        {
            var traits = new List<string>();
            if (player == null)
                return traits;

            // Exemples de logique simple
            switch (player.PrimaryPosition)
            {
                case PlayerPosition.Goalkeeper:
                    traits.Add("Goalkeeper Specialist");
                    if (player.BaseData.Reflexes > 85) traits.Add("Quick Reflexes");
                    if (player.BaseData.PenaltySaving > 85) traits.Add("Penalty Saver");
                    break;
                case PlayerPosition.LeftWing:
                case PlayerPosition.RightWing:
                    traits.Add("Fast Runner");
                    if (player.BaseData.Agility > 85) traits.Add("Acrobat");
                    if (player.BaseData.ShootingAccuracy > 80) traits.Add("Sharp Shooter");
                    break;
                case PlayerPosition.Pivot:
                    traits.Add("Target Man");
                    if (player.BaseData.Strength > 85) traits.Add("Strong Body");
                    if (player.BaseData.Positioning > 80) traits.Add("Space Finder");
                    break;
                case PlayerPosition.LeftBack:
                case PlayerPosition.RightBack:
                    traits.Add("Power Shooter");
                    if (player.BaseData.ShootingPower > 85) traits.Add("Long Shot");
                    break;
                case PlayerPosition.CentreBack:
                    traits.Add("Playmaker");
                    if (player.BaseData.DecisionMaking > 85) traits.Add("Tactician");
                    if (player.BaseData.Leadership > 80) traits.Add("Leader");
                    break;
                // Ajoute d'autres cas si besoin
            }
            // Traits génériques selon le potentiel
            if (player.BaseData.PotentialAbility > 90) traits.Add("Wonderkid");
            else if (player.BaseData.PotentialAbility > 80) traits.Add("High Potential");
            // Un trait athlétique si la moyenne physique est élevée
            var avgPhysical = (player.BaseData.Speed + player.BaseData.Agility + player.BaseData.Strength + player.BaseData.Jumping + player.BaseData.Stamina + player.BaseData.NaturalFitness + player.BaseData.Resilience) / 7.0;
            if (avgPhysical > 80) traits.Add("Athletic");

            // --- Personality-based traits ---
            if (player.Ambition > 80) traits.Add("Highly Ambitious");
            if (player.Determination > 80) traits.Add("Relentless");
            if (player.Professionalism > 80) traits.Add("Model Professional");
            if (player.Loyalty > 80) traits.Add("One Club Player");
            if (player.Aggression > 80) traits.Add("Hot-Headed");
            if (player.Volatility > 80) traits.Add("Mercurial");
            if (player.Composure > 80) traits.Add("Ice Cold");
            if (player.Leadership > 80) traits.Add("Locker Room Leader");
            if (player.Teamwork > 80) traits.Add("Team Player");
            if (player.Ambition < 40) traits.Add("Low Ambition");
            if (player.Determination < 40) traits.Add("Inconsistent");
            if (player.Professionalism < 40) traits.Add("Unprofessional");
            if (player.Loyalty < 40) traits.Add("Journeyman");
            if (player.Aggression < 40) traits.Add("Calm");
            if (player.Composure < 40) traits.Add("Nervous");
            if (player.Teamwork < 40) traits.Add("Individualist");

            return traits;
        }


        // Génère une date de naissance réaliste à partir d'un âge (jour/mois aléatoire)
        private DateTime GenerateBirthDate(int age)
        {
            // Année de naissance
            int year = DateTime.Now.Year - age;
            // Mois aléatoire
            int month = _random.Next(1, 12);
            // Jour aléatoire (en fonction du mois)
            int day = _random.Next(1, DateTime.DaysInMonth(year, month) + 1);
            return new DateTime(year, month, day);
        }

        // Vérifie la validité des bornes et la cohérence des attributs principaux
        private void ValidatePlayer(PlayerData player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            // Vérifie les bornes des attributs principaux (1 à 99)
            foreach (var prop in typeof(BaseData).GetFields())
            {
                if (prop.FieldType == typeof(int))
                {
                    int value = (int)prop.GetValue(player.BaseData);
                    if (value < 1 || value > 99)
                        throw new Exception($"Attribut {prop.Name} hors bornes ({value}) pour le joueur {player.FirstName} {player.LastName}");
                }
            }
            // Vérifie les bornes des attributs de personnalité (1 à 99)
            // Vérifie les bornes des attributs de personnalité (1 à 99)
            var personalityProps = new[] { "Ambition", "Determination", "Aggression", "Volatility", "Professionalism", "Loyalty", "Composure", "Leadership", "Teamwork" };
            foreach (var pname in personalityProps)
            {
                var prop = typeof(PlayerData).GetProperty(pname);
                if (prop != null)
                {
                    int value = (int)prop.GetValue(player);
                    if (value < 1 || value > 99)
                        throw new Exception($"Attribut personnalité {pname} hors bornes ({value}) pour le joueur {player.FirstName} {player.LastName}");
                }
            }
            // Optionally: enforce trait/attribute consistency
            if (player.Personality == PlayerPersonalityTrait.Ambitious && player.Ambition < 70)
                throw new Exception($"Joueur {player.FirstName} {player.LastName} a le trait Ambitious mais une ambition trop basse ({player.Ambition})");
            if (player.Personality == PlayerPersonalityTrait.Determined && player.Determination < 70)
                throw new Exception($"Joueur {player.FirstName} {player.LastName} a le trait Determined mais une determination trop basse ({player.Determination})");
            if (player.Personality == PlayerPersonalityTrait.Professional && player.Professionalism < 70)
                throw new Exception($"Joueur {player.FirstName} {player.LastName} a le trait Professional mais un professionnalisme trop bas ({player.Professionalism})");
            if (player.Personality == PlayerPersonalityTrait.Loyal && player.Loyalty < 70)
                throw new Exception($"Joueur {player.FirstName} {player.LastName} a le trait Loyal mais une loyauté trop basse ({player.Loyalty})");

            // Cohérence métier selon le poste
            switch (player.PrimaryPosition)
            {
                case PlayerPosition.Goalkeeper:
                    if (player.BaseData.Reflexes < 60)
                        throw new Exception("Gardien avec réflexes trop faibles !");
                    if (player.BaseData.Handling < 60)
                        throw new Exception("Gardien avec handling trop faible !");
                    break;
                case PlayerPosition.LeftWing:
                case PlayerPosition.RightWing:
                    if (player.BaseData.Speed < 60)
                        throw new Exception("Ailier trop lent !");
                    break;
                // Ajoute d'autres règles si besoin
            }
        }

        // Helper for wage calculation
        private float CalculateYouthWage(int age, int potential, int currentAbility)
        {
            // Example formula: base + (potential * factor1) + (currentAbility * factor2) - (age * factor3)
            float baseWage = 500f;
            float wage = baseWage + (potential * 10f) + (currentAbility * 5f) - (age * 15f);
            return Mathf.Max(wage, 200f); // Ensure a minimum wage
        }

        // Generate initial positional familiarity using position similarity and attribute logic
        private Dictionary<PlayerPosition, float> GenerateInitialFamiliarity(PlayerPosition primary, PlayerData player)
        {
            // Handedness is enforced for both primary and plausible secondary positions

            var familiarity = new Dictionary<PlayerPosition, float>();
            // Set all to low baseline
            foreach (PlayerPosition pos in Enum.GetValues(typeof(PlayerPosition)))
                familiarity[pos] = 0.1f;
            // Primary position natural
            familiarity[primary] = 1.0f;

            // Determine plausible secondaries by similarity and attributes
            var plausible = GetPlausibleSecondaryPositions(primary, player);
            foreach (var sec in plausible)
                familiarity[sec] = 0.6f + (float)_random.NextDouble() * 0.2f; // 0.6-0.8 moderate familiarity
            return familiarity;
        }

        // Example logic for plausible secondary positions
        private List<PlayerPosition> GetPlausibleSecondaryPositions(PlayerPosition primary, PlayerData player)
        {
            // Weighted scoring system: For each position, calculate a fit score based on a weighted sum of relevant attributes,
            // normalized by the player's average in those attributes. If the score is above threshold, the position is plausible.
            // Handedness is always enforced for wings/backs.

            var secondaries = new List<PlayerPosition>();
            bool isRight = player.PreferredHand == Handedness.Right || player.PreferredHand == Handedness.Both;
            bool isLeft = player.PreferredHand == Handedness.Left || player.PreferredHand == Handedness.Both;
            float threshold = 0.85f; // Moderate fit threshold

            // Helper to compute weighted fit (relative to player's own ability)
            float Fit(Dictionary<string, float> weights) {
                float weightedSum = 0f, totalWeight = 0f, playerSum = 0f;
                foreach (var kv in weights) {
                    float val = (float)typeof(PlayerData).GetProperty(kv.Key).GetValue(player);
                    weightedSum += val * kv.Value;
                    totalWeight += kv.Value;
                    playerSum += val;
                }
                float avg = playerSum / weights.Count;
                return (weightedSum / totalWeight) / avg;
            }

            // Define attribute weights for each position
            var positionWeights = new Dictionary<PlayerPosition, Dictionary<string, float>> {
                { PlayerPosition.LeftWing, new Dictionary<string, float> {
                    { "Speed", 0.25f }, { "Agility", 0.2f }, { "Dribbling", 0.15f }, { "ShootingAccuracy", 0.15f }, { "Technique", 0.1f }, { "Stamina", 0.15f }
                }},
                { PlayerPosition.RightWing, new Dictionary<string, float> {
                    { "Speed", 0.25f }, { "Agility", 0.2f }, { "Dribbling", 0.15f }, { "ShootingAccuracy", 0.15f }, { "Technique", 0.1f }, { "Stamina", 0.15f }
                }},
                { PlayerPosition.LeftBack, new Dictionary<string, float> {
                    { "ShootingPower", 0.2f }, { "Strength", 0.2f }, { "Passing", 0.15f }, { "Technique", 0.15f }, { "DecisionMaking", 0.1f }, { "Height", 0.2f }
                }},
                { PlayerPosition.RightBack, new Dictionary<string, float> {
                    { "ShootingPower", 0.2f }, { "Strength", 0.2f }, { "Passing", 0.15f }, { "Technique", 0.15f }, { "DecisionMaking", 0.1f }, { "Height", 0.2f }
                }},
                { PlayerPosition.CentreBack, new Dictionary<string, float> {
                    { "Passing", 0.25f }, { "DecisionMaking", 0.2f }, { "Anticipation", 0.15f }, { "Technique", 0.15f }, { "Teamwork", 0.15f }, { "Composure", 0.1f }
                }},
                { PlayerPosition.Pivot, new Dictionary<string, float> {
                    { "Strength", 0.2f }, { "Positioning", 0.2f }, { "Bravery", 0.15f }, { "Aggression", 0.1f }, { "Technique", 0.15f }, { "Height", 0.2f }
                }},
                { PlayerPosition.Goalkeeper, new Dictionary<string, float> {
                    { "Reflexes", 0.3f }, { "Handling", 0.2f }, { "PositioningGK", 0.15f }, { "Communication", 0.15f }, { "Agility", 0.1f }, { "OneOnOnes", 0.1f }
                }}
            };

            foreach (var kv in positionWeights) {
                var pos = kv.Key;
                if (pos == primary) continue; // skip current primary
                // Enforce handedness for wings/backs
                if ((pos == PlayerPosition.LeftWing || pos == PlayerPosition.LeftBack) && !isRight) continue;
                if ((pos == PlayerPosition.RightWing || pos == PlayerPosition.RightBack) && !isLeft) continue;
                float fit = Fit(kv.Value);
                if (fit >= threshold) secondaries.Add(pos);
            }
            return secondaries.Take(2).ToList();
        }

        // Randomly generate handedness with 80% right, 18% left, 2% both
        private Handedness GenerateRandomHandedness()
        {
            double roll = _random.NextDouble();
            if (roll < 0.8) return Handedness.Right;
            if (roll < 0.98) return Handedness.Left;
            return Handedness.Both;
        }

        // Get eligible primary positions based on handedness
        private List<PlayerPosition> GetEligiblePrimaryPositions(Handedness hand)
        {
            var positions = new List<PlayerPosition>();
            switch (hand)
            {
                case Handedness.Right:
                    positions.AddRange(new[] { PlayerPosition.LeftWing, PlayerPosition.LeftBack, PlayerPosition.CentreBack, PlayerPosition.Pivot, PlayerPosition.Goalkeeper });
                    break;
                case Handedness.Left:
                    positions.AddRange(new[] { PlayerPosition.RightWing, PlayerPosition.RightBack, PlayerPosition.CentreBack, PlayerPosition.Pivot, PlayerPosition.Goalkeeper });
                    break;
                case Handedness.Both:
                    positions.AddRange(Enum.GetValues(typeof(PlayerPosition)).Cast<PlayerPosition>());
                    break;
            }
            return positions;
        }

        // Placeholder random name generators
        private string GenerateRandomFirstName(string nationality)
        {
            // TODO: Implement based on nationality
            return "Youth";
        }
        private string GenerateRandomLastName(string nationality)
        {
            // TODO: Implement based on nationality
            return "Player";
        }
                
        /// <summary>
        /// Generates a random player name (stub, replace with localization or DB lookup as needed).
        /// </summary>
        private string GenerateRandomName(string nationality)
        {
            // TODO: Replace with real name generator or localization
            string[] firstNames = { "Alex", "Max", "Lucas", "Julien", "Nicolas" };
            string[] lastNames = { "Dupont", "Martin", "Bernard", "Petit", "Robert" };
            return firstNames[_random.Next(firstNames.Length)] + " " + lastNames[_random.Next(lastNames.Length)];
        }

        /// <summary>
        /// Helper to randomize an attribute value.
        /// </summary>
        private int RandomAttribute(int min, int max)
        {
            return _random.Next(min, max + 1);
        }

        /// <summary>
        /// Crée un SimPlayer à partir d'un PlayerData et d'un teamSimId.
        /// Si applyDevelopment est true et que PlayerDevelopment est fourni, applique le développement annuel.
        /// </summary>
        /// <param name="baseData">Le PlayerData de base</param>
        /// <param name="teamSimId">L'identifiant de l'équipe simulée (0=domicile, 1=extérieur)</param>
        /// <param name="applyDevelopment">Si true, applique ProcessAnnualDevelopment avant la création</param>
        /// <returns>Une nouvelle instance de SimPlayer</returns>
        public SimPlayer CreateSimPlayer(PlayerData baseData, int teamSimId, bool applyDevelopment = false)
        {
            if (baseData == null) throw new ArgumentNullException(nameof(baseData));
            if (applyDevelopment && _playerDevelopment != null)
            {
                _playerDevelopment.ProcessAnnualDevelopment(baseData);
            }
            return new SimPlayer(baseData, teamSimId);
        }
    }

    /// <summary>
    /// Example DTO for database player entry. Replace or expand as needed.
    /// </summary>
    public class PlayerDatabaseEntry
    {
        // --- Identifiers ---
        public int Reaction; // Add this if not present
        public int PlayerID;
        public string FirstName;
        public string LastName;
        public int Age;
        public DateTime DateOfBirth;
        public string Nationality;

        // --- Physical Attributes ---
        public int Height;
        public int Weight;

        // --- Contract/Status ---
        public int? CurrentTeamID;
        public DateTime ContractExpiryDate;
        public float Wage;
        public float Morale;
        public float Condition;
        public InjuryStatus CurrentInjuryStatus;
        public DateTime InjuryReturnDate;
        public string InjuryDescription;
        public TransferStatus TransferStatus;

        // --- Position ---
        public PlayerPosition PrimaryPosition;
        public Handedness ShootingHand;
        public PlayerPosition Position; // Used for GenerateInitialFamiliarity
        public Dictionary<PlayerPosition, float> PositionalFamiliarity;

        // --- Technical Skills ---
        public int ShootingPower;
        public int ShootingAccuracy;
        public int Passing;
        public int Dribbling;
        public int Technique;
        public int Tackling;
        public int Blocking;

        // --- Physical Attributes ---
        public int Speed;
        public int Agility;
        public int Strength;
        public int Jumping;
        public int Stamina;
        public int NaturalFitness;
        public int Resilience;

        // --- Mental Attributes ---
        public int Aggression;
        public int Bravery;
        public int Composure;
        public int Concentration;
        public int Anticipation;
        public int Vision;
        public int Creativity;
        public int DecisionMaking;
        public int Teamwork;
        public int WorkRate;
        public int Leadership;
        public int Positioning;
        public int Determination;

        // --- Goalkeeping Attributes ---
        public int Reflexes;
        public int Handling;
        public int PositioningGK;
        public int OneOnOnes;
        public int PenaltySaving;
        public int Throwing;
        public int Communication;

        // --- Potential & Hidden ---
        public int PotentialAbility;
        public List<string> Traits;
        public PlayerPersonalityTrait Personality;
        // Add any other attributes from PlayerData here
    }
     
}
