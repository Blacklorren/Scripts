# Simulation

Ce dossier regroupe toute la logique de simulation du jeu (moteurs, IA, événements, états internes, physique, etc.). Il est organisé pour séparer clairement chaque responsabilité et faciliter la navigation.

## Structure du dossier

```
Simulation/
│
├── Engines/      # Moteurs centraux de simulation (MatchEngine, ActionResolver, etc.)
├── AI/           # Intelligence artificielle (contrôleurs, décisions, comportements)
├── Events/       # Système d’événements de match (handlers, bus, types d’événements)
├── Physics/      # Calculs physiques purs (ballon, collisions, trajectoires)
├── Data/         # Etats temporaires de simulation (pas les POCO métiers)
├── Services/     # Services transverses dédiés à la simulation (temps, random, etc.)
├── Utils/        # Helpers, outils, extensions réutilisables
├── Factories/    # Création d’objets complexes de simulation
├── Installers/   # Scripts d’initialisation/d’injection de dépendances
├── MoraleSimulator.cs         # Orchestrateurs principaux (optionnel)
├── PlayerDevelopment.cs       # Orchestrateurs principaux (optionnel)
├── TrainingSimulator.cs       # Orchestrateurs principaux (optionnel)
└── README.md
```

## Rôle de chaque sous-dossier

### `Engines/`
Contient les moteurs centraux de simulation (par exemple : `MatchEngine.cs`, `ActionResolver.cs`, `MatchSimulationData.cs`, `MatchState.cs`, etc.).
- **À placer ici :** Toute logique de simulation de match, résolution d’actions, états internes, constantes.

### `AI/`
Toute l’intelligence artificielle (contrôleurs, prise de décision, comportements, tuning).
- **À placer ici :** `PlayerAIController.cs`, `TeamAIManager.cs`, DecisionMakers, Behaviors, etc.

### `Events/`
Gestion des événements de match (handlers, bus, types d’événements).
- **À placer ici :** `EventBus.cs`, `MatchEvent.cs`, `DefaultMatchEventHandler.cs`, etc.

### `Physics/`
Scripts de calcul physique pur (ballon, collisions, trajectoires).
- **À placer ici :** `SimBall.cs`, `CollisionUtils.cs`, etc.

### `Data/`
Etats temporaires de simulation (pas les POCO métiers qui restent dans le dossier Data racine du projet).
- **À placer ici :** `SimPlayerState.cs`, `SimTeamState.cs`, objets de passage internes à la simulation.

### `Services/`
Services transverses utilisés dans la simulation (gestion du temps, random, etc.).
- **À placer ici :** `SimulationTimeService.cs`, `RandomService.cs`, etc.

### `Utils/`
Helpers, outils, extensions, utilitaires réutilisables dans la simulation.
- **À placer ici :** `SimulationUtils.cs`, `JumpDecisionUtils.cs`, etc.

### `Factories/`
Création d’objets complexes de simulation (pattern Factory).
- **À placer ici :** `PlayerFactory.cs`, `TeamFactory.cs`, `MatchFactory.cs`, etc.

### `Installers/`
Scripts d’initialisation et d’injection de dépendances pour la simulation.
- **À placer ici :** `SimulationInstaller.cs`, `EngineInstaller.cs`, etc.

### Racine de Simulation
Les orchestrateurs principaux (ex : `MoraleSimulator.cs`, `PlayerDevelopment.cs`, `TrainingSimulator.cs`) peuvent rester à la racine s’ils ne rentrent pas dans un sous-dossier spécifique.

## Règles générales
- **Pas de logique métier ou UI** dans Simulation (uniquement la logique de simulation du jeu).
- **Un script = une responsabilité** : placer chaque script dans le sous-dossier correspondant à sa fonction principale.
- **Eviter les sous-dossiers trop profonds ou génériques** : chaque niveau doit avoir un rôle clair.

## Dépendances
- La simulation dépend des données métiers du dossier `Data/` (racine du projet).
- Elle utilise des configurations du dossier `Config/`.

## Exemple d’ajout d’un nouveau moteur de simulation
- Créer le script dans `Engines/`.
- Si besoin d’états internes, les placer aussi dans `Engines/`.
- Si le moteur a des besoins IA, référencer les contrôleurs dans `AI/`.

---

Pour toute question sur l’organisation ou pour ajouter un nouveau type de composant, se référer à ce README ou demander conseil à l’équipe technique.
