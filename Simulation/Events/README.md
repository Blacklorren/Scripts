# Events

Ce dossier regroupe tout ce qui concerne la gestion des événements dans la simulation de match. Il centralise la diffusion, la détection, la gestion et le traitement des événements survenant pendant la simulation.

## Responsabilités
- Définir les types d’événements du match (ex : but, faute, passe, interception…)
- Orchestrer la diffusion des événements via un bus central (`EventBus`)
- Gérer les handlers (gestionnaires) pour chaque type d’événement
- Détecter les événements à partir des actions simulées
- Fournir des interfaces pour l’injection et l’extension des handlers et facilitateurs

## Structure du dossier
- **Core/** : Fichiers centraux du système d'événements (ex : `EventBus.cs`, `Events.cs`)
- **Interfaces/** : Interfaces pour injection et extension (`IActionResolver.cs`, `IEventDetector.cs`, etc.)
- **Handlers/** : Gestionnaires d’événements (`MatchEventHandler.cs`, `DefaultMatchEventHandler.cs`, etc.)
- **Detectors/** : Détecteurs d’événements (`DefaultEventDetector.cs`, etc.)
- **Resolvers/** : Résolveurs d’actions (`ActionResolver.cs`, etc.)
- **Finalizers/** : Finalisation des résultats de match (`DefaultMatchFinalizer.cs`, etc.)
- **Calculators/** : Calculateurs spécialisés (faute, interception, tir…)

## Ce qui ne doit PAS être ici
- Pas de logique d’IA (voir `AI/`)
- Pas de logique de moteur central (voir `Engines/`)
- Pas de calculs physiques purs (voir `Physics/`)
- Pas de helpers/utilitaires génériques (voir `Utils/`)

## Règles d’architecture
- Les événements doivent être découplés de l’IA, des engines et de l’UI.
- Utiliser des interfaces pour injecter les handlers et faciliter l’extension.
- Chaque événement doit avoir un type clair et documenté.
- Les handlers, détecteurs, finalizers et résolveurs doivent être testables indépendamment.
- Respecter la séparation des responsabilités via les sous-dossiers.

## Bonnes pratiques et conventions
- Préfixer les interfaces par `I` (ex : `IMatchEventHandler`).
- Placer chaque interface dans le dossier `Interfaces/`.
- Les implémentations par défaut peuvent être préfixées par `Default` (ex : `DefaultMatchFinalizer`).
- Grouper les implémentations spécialisées dans les sous-dossiers correspondants.
- Documenter chaque événement et handler dans le code.

## Exemple d’ajout
Pour ajouter un nouvel événement (ex : “Carton Rouge”) :
1. Définir le type d’événement dans `Events.cs` ou un fichier dédié dans `Core/`.
2. Créer un handler spécifique dans `Handlers/`.
3. Enregistrer ce handler auprès du `EventBus` dans `Core/`.

---

Pour toute question sur l’organisation ou l’extension du système d’événements, se référer au README général de `Simulation/` ou demander conseil à l’équipe technique.

## Règles d’architecture
- Les événements doivent être découplés de l’IA, des engines et de l’UI.
- Utiliser des interfaces pour injecter les handlers et facilitateurs d’extension.
- Chaque événement doit avoir un type clair et documenté.
- Les handlers doivent être testables indépendamment.

## Exemple d’ajout
Pour ajouter un nouvel événement (ex : “Carton Rouge”) :
1. Définir le type d’événement dans `MatchEvent.cs` ou un fichier dédié.
2. Créer un handler spécifique si besoin dans `Handlers/`.
3. Enregistrer ce handler auprès du `EventBus`.

---

Pour toute question sur l’organisation ou l’extension du système d’événements, se référer au README général de `Simulation/` ou demander conseil à l’équipe technique.
