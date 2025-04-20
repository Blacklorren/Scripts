# Utils (Simulation)

Ce dossier contient les outils, helpers et utilitaires partagés spécifiques à la simulation.

## Fichiers présents dans ce dossier
- **DefaultPhaseManager.cs** : Implémentation par défaut du gestionnaire de phases de simulation.
- **DefaultPlayerSetupHandler.cs** : Handler utilitaire pour l'initialisation des joueurs en simulation.
- **DefaultSimulationTimer.cs** : Gestion du temps et des timers de simulation.
- **IGeometryProvider.cs** : Interface pour la fourniture de géométrie du terrain.
- **IPhaseManager.cs** : Interface pour la gestion des phases de simulation.
- **IPlayerSetupHandler.cs** : Interface pour l'initialisation/configuration des joueurs.
- **ISimulationTimer.cs** : Interface pour la gestion du temps de simulation.
- **JumpDecisionUtils.cs** : Outils statiques pour la logique de saut des joueurs.
- **PitchGeometryProvider.cs** : Fournisseur de géométrie du terrain.
- **PlayerPositionHelper.cs** : Méthodes utilitaires pour manipuler les positions des joueurs.
- **SimulationUtils.cs** : Fonctions utilitaires diverses pour la simulation.
- **README.md** : Ce fichier de documentation.

> Les fichiers `.meta` Unity sont présents pour la gestion des assets et ne doivent pas être modifiés à la main.

## Ce qui doit être placé ici
- Helpers, outils statiques, extensions ou utilitaires partagés entre plusieurs sous-systèmes de la simulation.
- Interfaces et implémentations utilitaires qui ne contiennent pas de logique métier ou de dépendance forte à d'autres modules.

## Ce qui ne doit PAS être placé ici
- Logique métier (gameplay, règles de match, IA, etc.).
- Services globaux ou transverses (à placer dans `Services`).
- Toute classe dépendant fortement d'un sous-système spécifique (à placer dans le dossier correspondant).

## Bonnes pratiques
- Les helpers/utilitaires doivent être **stateless** (sans état) et découplés.
- Bien documenter chaque méthode et classe utilitaire.
- Ajouter des tests unitaires pour garantir la robustesse des helpers.

---

**Note :**
Ce dossier est destiné à centraliser les fonctions utilitaires réutilisables dans la simulation, afin d’éviter la duplication de code et de faciliter la maintenance.
