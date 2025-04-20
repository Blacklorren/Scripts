# Engines

Ce dossier contient tous les moteurs centraux de la simulation, responsables de l’orchestration et de la résolution des actions de match. Il regroupe la logique cœur, les états internes et les interfaces nécessaires à la simulation.

## Responsabilités
- Orchestration du déroulement d’un match (tick, phases, transitions)
- Résolution des actions (passes, tirs, duels, etc.)
- Gestion des états internes du match (score, temps, positions, etc.)
- Fourniture d’interfaces pour l’injection de dépendances
- Définition des constantes et exceptions propres à la simulation

## Exemples de fichiers typiques à placer ici
- **MatchEngine.cs** : Moteur principal de simulation de match
- **ActionResolver.cs** : Résolution d’actions (passes, tirs, duels)
- **ActionResolverConstants.cs** : Constantes pour la résolution des actions
- **MatchSimulationData.cs** / **MatchState.cs** : États internes du moteur
- **EngineServiceBundle.cs** / **IEngineServiceBundle.cs** : Services partagés entre engines
- **IMatchEngine.cs**, **IMatchSimulatorFactory.cs**, etc. : Interfaces exposées par les moteurs
- **SimConstants.cs** : Constantes générales de simulation
- **Exceptions de simulation** : (SimulationException.cs, SetupException.cs, etc.)

> **Note :** Le choix ou la fourniture de tactique relève de l'IA ou des providers externes, et ne doit pas être ici.

## Ce qui ne doit PAS être ici
- Pas de logique d’IA (voir `AI/`)
- Pas de gestion d’événements (voir `Events/`)
- Pas de calculs physiques purs (voir `Physics/`)
- Pas de helpers/utilitaires génériques (voir `Utils/`)

## Règles d’architecture
- Les engines doivent être découplés de l’UI, de l’IA et des systèmes externes.
- Toute dépendance doit être injectée via une interface ou un service (pattern Dependency Injection).
- Les états internes doivent être clairement séparés des données métiers (pas de POCO métiers ici).
- Les exceptions spécifiques à la simulation peuvent être placées ici si elles ne sont pas partagées avec d’autres systèmes.

## Exemple d’ajout
Pour ajouter un nouveau moteur (ex : un simulateur météo pour le match) :
1. Créer le script dans ce dossier.
2. Définir une interface si besoin pour l’injection.
3. Documenter les dépendances et les points d’extension.

---

Pour toute question sur l’organisation ou les responsabilités du dossier, se référer au README général de `Simulation/` ou demander conseil à l’équipe technique.
