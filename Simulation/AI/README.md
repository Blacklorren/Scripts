# AI (Simulation)

Ce dossier contient l’intelligence artificielle liée à la simulation : contrôleurs IA, décisionnaires, évaluateurs, providers de stratégie/tactique, et services IA partagés.

## Fichiers et sous-dossiers présents
- **AIUpdateScheduler.cs** : Gère la fréquence et le moment des mises à jour de l'IA pour optimiser les performances.
- **AIServiceBundle.cs** : Service d’agrégation des composants IA (pour injection ou accès centralisé).
- **DefaultTacticProvider.cs** : Provider de tactique par défaut (stratégie d’équipe IA).
- **IAIServiceBundle.cs** : Interface pour l’agrégateur de services IA.
- **IPlayerAIController.cs** : Interface pour le contrôleur IA des joueurs.
- **ITacticPositioner.cs** : Interface pour le positionnement tactique IA.
- **PlayerAIController.cs** : Contrôleur IA principal pour les joueurs, orchestrant les décisions individuelles.
- **TacticPositioner.cs** : Implémentation du positionnement tactique IA.
- **TeamAIManager.cs** : Gère l'IA au niveau de l'équipe (tactiques, timeouts, substitutions).
- **Decision/** : Décisionnaires spécialisés (logique de prise de décision IA) et structures de données associées (e.g., `PlayerAIContext`, `DecisionResult`).
- **Evaluation/** : Évaluateurs de situations et de contextes pour l’IA.
- **Positioning/** : Logique de positionnement tactique IA et helpers associés.
- **README.md** : Ce fichier de documentation.

> Les fichiers `.meta` Unity sont présents pour la gestion des assets et ne doivent pas être modifiés à la main.

## Ce qui doit être placé ici
- Contrôleurs IA de joueurs ou d’équipes, providers de stratégie/tactique, services IA globaux.
- Décisionnaires, évaluateurs, comportements, helpers IA partagés.
- Interfaces et implémentations IA découplées des moteurs et événements.

## Ce qui ne doit PAS être placé ici
- Logique de simulation moteur, gestion d’événements, ou services non liés à l’IA (placer dans `Engines`, `Events`, ou `Services`).
- Logique métier ou gameplay non IA.

## Bonnes pratiques
- L’IA doit être **découplée des moteurs et événements** : interagir via interfaces.
- Organiser la logique IA par type : décision, évaluation, comportement, providers.
- Documenter chaque classe et méthode IA.
- Préférer l’injection de dépendances pour la flexibilité et les tests.

---

**Note :**
Ce dossier structure toute la logique IA de la simulation, pour une séparation claire entre intelligence artificielle, moteurs de simulation, et logique métier.
