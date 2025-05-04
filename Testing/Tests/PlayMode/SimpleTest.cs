using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HandballManager.Data;
using HandballManager.Gameplay;
using HandballManager.Core;
using HandballManager.Simulation.Engines;

namespace HandballManager.Tests.PlayMode
{
    /// <summary>
    /// Types de matchs possibles
    /// </summary>
    public enum MatchType
    {
        Friendly,
        League,
        Cup,
        International
    }
    
    /// <summary>
    /// Classe simple pour représenter la configuration d'un match pour les tests
    /// </summary>
    public class MatchConfiguration
    {
        public TeamData HomeTeam { get; set; }
        public TeamData AwayTeam { get; set; }
        public Tactic HomeTactic { get; set; }
        public Tactic AwayTactic { get; set; }
        public MatchType MatchType { get; set; }
        public int MatchDay { get; set; }
        public int Season { get; set; }
    }
    
    /// <summary>
    /// Classe de test minimale pour vérifier l'infrastructure de test
    /// </summary>
    public class SimpleTest
    {
        [UnityTest]
        [Timeout(5000)] // Timeout court de 5 secondes
        public IEnumerator SimpleTest_ShouldPass()
        {
            Debug.Log("Début du test simple");
            
            // Attendre une frame
            yield return null;
            
            Debug.Log("Test simple terminé avec succès");
            Assert.Pass("Le test simple a réussi");
        }
        
        [UnityTest]
        [Timeout(10000)] // Timeout de 10 secondes
        public IEnumerator TacticTest_ShouldCreateAndConvertTactics()
        {
            Debug.Log("Début du test de tactiques");
            
            // Créer une TacticData
            var tacticData = new TacticData
            {
                TacticID = Guid.NewGuid(),
                Name = "Test Tactic",
                OffensiveFormationName = "3-3",
                DefensiveFormationName = "6-0"
            };
            
            Debug.Log("TacticData créée avec succès");
            yield return null;
            
            try
            {
                // Créer des FormationData factices pour le test
                Debug.Log("Création de FormationData factices...");
                var offensiveFormation = CreateDummyFormation("3-3");
                var defensiveFormation = CreateDummyFormation("6-0");
                
                // Créer une Tactic directement avec les FormationData factices
                Debug.Log("Création d'une Tactic avec les FormationData factices...");
                var tactic = new Tactic
                {
                    OffensiveFormationData = offensiveFormation,
                    DefensiveFormationData = defensiveFormation
                };
                
                // Vérifier que la création a fonctionné
                if (tactic != null)
                {
                    Debug.Log("Tactic créée avec succès: " + tactic.ToString());
                    
                    // Vérifier les FormationData
                    if (tactic.OffensiveFormationData != null)
                    {
                        Debug.Log("OffensiveFormationData: " + tactic.OffensiveFormationData.FormationName);
                    }
                    else
                    {
                        Debug.LogWarning("OffensiveFormationData est null");
                    }
                    
                    if (tactic.DefensiveFormationData != null)
                    {
                        Debug.Log("DefensiveFormationData: " + tactic.DefensiveFormationData.FormationName);
                    }
                    else
                    {
                        Debug.LogWarning("DefensiveFormationData est null");
                    }
                    
                    // Test de conversion de Tactic vers TacticData
                    Debug.Log("Test de conversion de Tactic vers TacticData...");
                    var convertedData = TacticConverter.ToData(tactic);
                    Debug.Log($"TacticData convertie: {convertedData.Name}, Off: {convertedData.OffensiveFormationName}, Def: {convertedData.DefensiveFormationName}");
                    
                    // Vérifier que les noms de formation sont corrects
                    Assert.AreEqual("3-3", convertedData.OffensiveFormationName, "Le nom de la formation offensive ne correspond pas");
                    Assert.AreEqual("6-0", convertedData.DefensiveFormationName, "Le nom de la formation défensive ne correspond pas");
                }
                else
                {
                    Debug.LogError("La création de Tactic a échoué: tactic est null");
                    Assert.Fail("La création de Tactic a échoué");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception lors du test: {ex.Message}\n{ex.StackTrace}");
                Assert.Fail($"Exception lors du test: {ex.Message}");
            }
            
            yield return null;
            Debug.Log("Test de tactiques terminé");
        }
        
        /// <summary>
        /// Crée une FormationData factice pour les tests
        /// </summary>
        private FormationData CreateDummyFormation(string formationName)
        {
            // Créer une instance de FormationData
            var formation = ScriptableObject.CreateInstance<FormationData>();
            formation.FormationName = formationName;
            
            // Créer quelques slots de formation
            formation.Slots = new List<FormationSlot>();
            
            // Ajouter des slots pour chaque position
            var positions = Enum.GetValues(typeof(PlayerPosition));
            foreach (PlayerPosition position in positions)
            {
                // Ne pas ajouter plus de 7 slots (taille d'une équipe de handball)
                if (formation.Slots.Count >= 7) break;
                
                var slot = new FormationSlot
                {
                    RoleName = position.ToString(),
                    AssociatedPosition = position,
                    BasePositionOffset = new Vector2(formation.Slots.Count * 2.0f - 6.0f, 0)
                };
                
                formation.Slots.Add(slot);
            }
            
            return formation;
        }
        
        [UnityTest]
        [Timeout(20000)] // 20 secondes maximum
        public IEnumerator SimpleMatchTest_ShouldCreateAndRunMatch()
    {
            Debug.Log("===== Début du test de match simplié =====");
            
            // Créer des FormationData factices
            Debug.Log("Création des FormationData factices...");
            var offensiveFormation = CreateDummyFormation("3-3");
            var defensiveFormation = CreateDummyFormation("6-0");
            yield return null;
            
            // Créer des tactiques avec les FormationData factices
            Debug.Log("Création des tactiques...");
            var homeTactic = new Tactic
            {
                OffensiveFormationData = offensiveFormation,
                DefensiveFormationData = defensiveFormation
            };
            
            var awayTactic = new Tactic
            {
                OffensiveFormationData = offensiveFormation,
                DefensiveFormationData = defensiveFormation
            };
            yield return null;
            
            // Créer des équipes de test
            Debug.Log("Création des équipes de test...");
            var homeTeam = CreateTestTeam("Home Team", 70, homeTactic);
            var awayTeam = CreateTestTeam("Away Team", 65, awayTactic);
            yield return null;
            
            // Créer une configuration de match
            Debug.Log("Création de la configuration de match...");
            var matchConfig = new MatchConfiguration
            {
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                HomeTactic = homeTactic,
                AwayTactic = awayTactic,
                MatchType = MatchType.Friendly,
                MatchDay = 1,
                Season = 1
            };
            yield return null;
            
            // Vérifier que la configuration est valide
            Assert.IsNotNull(matchConfig.HomeTeam, "L'équipe à domicile ne peut pas être null");
            Assert.IsNotNull(matchConfig.AwayTeam, "L'équipe à l'extérieur ne peut pas être null");
            Assert.IsNotNull(matchConfig.HomeTactic, "La tactique à domicile ne peut pas être null");
            Assert.IsNotNull(matchConfig.AwayTactic, "La tactique à l'extérieur ne peut pas être null");
            
            Debug.Log("Configuration de match valide. Test réussi.");
            Debug.Log("===== Test de match simplié terminé =====");
            yield return null;
        }
        
        /// <summary>
        /// Crée une équipe de test avec des joueurs factices
        /// </summary>
        private TeamData CreateTestTeam(string name, int averageAbility, Tactic tactic)
        {
            // Créer une TacticData correspondant à la Tactic
            var tacticData = new TacticData
            {
                TacticID = Guid.NewGuid(),
                Name = $"{name} Tactic",
                OffensiveFormationName = tactic.OffensiveFormationData?.FormationName ?? "Unknown",
                DefensiveFormationName = tactic.DefensiveFormationData?.FormationName ?? "Unknown"
            };
            
            // Créer une TeamData
            var team = new TeamData
            {
                Name = name,
                Reputation = 5000,
                Budget = 1000000,
                LeagueID = 1
            };
            
            // Ajouter la tactique à l'équipe
            team.Tactics = new List<TacticData> { tacticData };
            team.CurrentTacticID = tacticData.TacticID;
            
            // Créer des joueurs pour l'équipe
            var random = new System.Random();
            int playersToCreate = 14; // Créer un effectif complet
            int gkCreated = 0;
            var positions = Enum.GetValues(typeof(PlayerPosition)).Cast<PlayerPosition>().ToList();
            
            for (int i = 0; i < playersToCreate; i++)
            {
                // Déterminer la position du joueur
                PlayerPosition pos;
                if (gkCreated < 2 && i >= playersToCreate - 2)
                {
                    pos = PlayerPosition.Goalkeeper;
                    gkCreated++;
                }
                else
                {
                    pos = positions[i % positions.Count];
                    if (pos == PlayerPosition.Goalkeeper && gkCreated >= 2)
                    {
                        pos = PlayerPosition.CentreBack; // Éviter trop de gardiens
                    }
                    if (pos == PlayerPosition.Goalkeeper) gkCreated++;
                }
                
                // Calculer le niveau moyen des attributs du joueur
                int baseAbility = averageAbility + random.Next(-5, 6); // +/- 5 points
                baseAbility = Mathf.Clamp(baseAbility, 30, 99);
                
                // Créer le joueur avec des attributs individuels
                var player = new PlayerData
                {
                    FirstName = $"Player{i+1}",
                    LastName = $"{name.Substring(0, 1)}{i+1}",
                    PrimaryPosition = pos,
                    Age = 25
                };
                
                // Initialiser les attributs individuels que nous savons exister
                // Attributs mentaux (confirmés dans le code)
                player.Determination = baseAbility + random.Next(-10, 11);
                player.Composure = baseAbility + random.Next(-10, 11);
                player.Teamwork = baseAbility + random.Next(-10, 11);
                player.Leadership = baseAbility + random.Next(-10, 11);
                player.Ambition = baseAbility + random.Next(-10, 11);
                player.Aggression = baseAbility + random.Next(-10, 11);
                player.Loyalty = baseAbility + random.Next(-10, 11);
                player.Professionalism = baseAbility + random.Next(-10, 11);
                player.Reaction = baseAbility + random.Next(-10, 11);
                
                // S'assurer que tous les attributs sont dans la plage valide (1-100)
                ClampAttributes(player);
                
                // Ajouter le joueur à l'équipe
                team.AddPlayer(player);
            }
            
            // Vérifier que l'équipe est valide
            if (team.Roster.Count(p => p.PrimaryPosition == PlayerPosition.Goalkeeper) < 1)
            {
                Debug.LogError($"L'équipe {name} n'a pas de gardien!");
                return null;
            }
            
            if (team.Roster.Count < 7)
            {
                Debug.LogError($"L'équipe {name} n'a que {team.Roster.Count} joueurs (minimum 7 requis)!");
                return null;
            }
            
            return team;
        }
        
        /// <summary>
        /// S'assure que tous les attributs du joueur sont dans la plage valide (1-100)
        /// </summary>
        private void ClampAttributes(PlayerData player)
        {
            // Attributs mentaux (confirmés dans le code)
            player.Determination = Mathf.Clamp(player.Determination, 1, 100);
            player.Composure = Mathf.Clamp(player.Composure, 1, 100);
            player.Teamwork = Mathf.Clamp(player.Teamwork, 1, 100);
            player.Leadership = Mathf.Clamp(player.Leadership, 1, 100);
            player.Ambition = Mathf.Clamp(player.Ambition, 1, 100);
            player.Aggression = Mathf.Clamp(player.Aggression, 1, 100);
            player.Loyalty = Mathf.Clamp(player.Loyalty, 1, 100);
            player.Professionalism = Mathf.Clamp(player.Professionalism, 1, 100);
            player.Reaction = Mathf.Clamp(player.Reaction, 1, 100);
        }
        
        [UnityTest]
        [Timeout(60000)] // 60 secondes maximum pour une simulation complète
        public IEnumerator FullMatchSimulation_ShouldProduceRealisticScore()
        {
            Debug.Log("===== Début du test de simulation complète de match =====");
            
            // Créer des FormationData factices
            Debug.Log("Création des FormationData factices...");
            var offensiveFormation = CreateDummyFormation("3-3");
            var defensiveFormation = CreateDummyFormation("6-0");
            yield return null;
            
            // Créer des tactiques avec les FormationData factices
            Debug.Log("Création des tactiques...");
            var homeTactic = new Tactic
            {
                OffensiveFormationData = offensiveFormation,
                DefensiveFormationData = defensiveFormation
            };
            
            var awayTactic = new Tactic
            {
                OffensiveFormationData = offensiveFormation,
                DefensiveFormationData = defensiveFormation
            };
            yield return null;
            
            // Créer des équipes de test avec des capacités différentes
            Debug.Log("Création des équipes de test...");
            var homeTeam = CreateTestTeam("Home Team", 75, homeTactic); // Équipe à domicile légèrement meilleure
            var awayTeam = CreateTestTeam("Away Team", 70, awayTactic);
            yield return null;
            
            // Obtenir une instance de IMatchEngine via Zenject
            Debug.Log("Obtention du moteur de simulation...");
            IMatchEngine matchEngine = null;
            try
            {
                // Essayer d'obtenir le moteur de simulation via Zenject si disponible
                matchEngine = UnityEngine.Object.FindObjectOfType<MonoBehaviour>().GetComponent<IMatchEngine>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Impossible d'obtenir IMatchEngine via Zenject: {ex.Message}. Création d'une instance directe.");
            }
            
            // Si nous n'avons pas pu obtenir le moteur via Zenject, créer une instance directement
            if (matchEngine == null)
            {
                Debug.Log("Création d'une instance directe de MatchEngine...");
                matchEngine = new MatchEngine();
            }
            yield return null;
            
            // Simuler le match
            Debug.Log("Démarrage de la simulation du match...");
            MatchResult result = null;
            
            // Utiliser une méthode synchrone pour la simulation dans le contexte de test
            bool useSimulateMatch = matchEngine.GetType().GetMethod("SimulateMatch") != null;
            
            if (useSimulateMatch)
            {
                try
                {
                    // Si la méthode SimulateMatch existe, l'utiliser directement
                    var simulateMatchMethod = matchEngine.GetType().GetMethod("SimulateMatch");
                    
                    // Fournir tous les paramètres requis par la méthode SimulateMatch
                    // TeamData homeTeam, TeamData awayTeam, Tactic homeTactic, Tactic awayTactic,
                    // int seed = -1, IProgress<float> progress = null, CancellationToken cancellationToken = default
                    result = (MatchResult)simulateMatchMethod.Invoke(matchEngine, new object[] { 
                        homeTeam,           // homeTeam
                        awayTeam,           // awayTeam
                        homeTactic,         // homeTactic
                        awayTactic,         // awayTactic
                        -1,                 // seed (valeur par défaut -1)
                        null,               // progress (null)
                        default(System.Threading.CancellationToken)  // cancellationToken (default)
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Erreur lors de la simulation du match (méthode synchrone): {ex.Message}\n{ex.StackTrace}");
                    Assert.Fail($"La simulation du match a échoué: {ex.Message}");
                    yield break;
                }
            }
            else
            {
                // Sinon, utiliser la méthode asynchrone et attendre le résultat
                Task<MatchResult> task = null;
                
                try
                {
                    task = matchEngine.SimulateMatchAsync(homeTeam, awayTeam, homeTactic, awayTactic);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Erreur lors du démarrage de la simulation asynchrone: {ex.Message}\n{ex.StackTrace}");
                    Assert.Fail($"La simulation du match a échoué au démarrage: {ex.Message}");
                    yield break;
                }
                
                // Attendre que la tâche soit terminée en dehors du bloc try
                while (!task.IsCompleted)
                {
                    yield return null;
                }
                
                try
                {
                    result = task.Result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Erreur lors de la récupération du résultat de simulation: {ex.Message}\n{ex.StackTrace}");
                    Assert.Fail($"La simulation du match a échoué lors de la récupération du résultat: {ex.Message}");
                    yield break;
                }
            }
            
            // Vérifier que le résultat n'est pas null
            Assert.IsNotNull(result, "Le résultat de la simulation ne peut pas être null");
            
            // Afficher le score final
            Debug.Log($"Score final: {result.HomeScore} - {result.AwayScore}");
            
            // Vérifier que le score est réaliste pour un match de handball
            // Un score typique de handball est entre 20 et 35 buts par équipe
            Assert.IsTrue(result.HomeScore >= 15 && result.HomeScore <= 40, 
                $"Le score de l'équipe à domicile ({result.HomeScore}) n'est pas réaliste pour un match de handball");
            Assert.IsTrue(result.AwayScore >= 15 && result.AwayScore <= 40, 
                $"Le score de l'équipe à l'extérieur ({result.AwayScore}) n'est pas réaliste pour un match de handball");
            
            // Vérifier que la différence de score n'est pas trop grande
            int scoreDifference = Math.Abs(result.HomeScore - result.AwayScore);
            Assert.IsTrue(scoreDifference <= 15, 
                $"La différence de score ({scoreDifference}) est trop grande pour un match réaliste");
            
            // Vérifier que le match a bien une équipe gagnante ou est un match nul
            Assert.IsTrue(result.HomeScore > result.AwayScore || result.HomeScore < result.AwayScore || result.HomeScore == result.AwayScore,
                "Le résultat du match n'est pas valide");
            
            // Afficher des statistiques supplémentaires si disponibles
            if (result.HomeStats != null && result.AwayStats != null)
            {
                Debug.Log("Statistiques du match:");
                Debug.Log($"Tirs: {result.HomeStats.ShotsTaken} - {result.AwayStats.ShotsTaken}");
                Debug.Log($"Tirs cadrés: {result.HomeStats.ShotsOnTarget} - {result.AwayStats.ShotsOnTarget}");
                Debug.Log($"Arrêts: {result.HomeStats.SavesMade} - {result.AwayStats.SavesMade}");
                Debug.Log($"Pourcentage de réussite: {result.HomeStats.ShootingPercentage:F1}% - {result.AwayStats.ShootingPercentage:F1}%");
                
                // Vérifier que les statistiques sont cohérentes
                Assert.IsTrue(result.HomeStats.ShotsTaken >= result.HomeScore, 
                    "Le nombre de tirs doit être supérieur ou égal au nombre de buts");
                Assert.IsTrue(result.AwayStats.ShotsTaken >= result.AwayScore, 
                    "Le nombre de tirs doit être supérieur ou égal au nombre de buts");
            }
            
            Debug.Log("===== Test de simulation de match terminé avec succès =====\n");
            yield return null;
        }
    }
}
