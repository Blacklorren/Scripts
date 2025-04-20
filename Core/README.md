# Core

Ce dossier contient les services transverses et les outils génériques utilisés dans tout le projet.

### Fichiers typiques à placer ici :
- **ServiceContainer.cs** : Conteneur d'injection de dépendances
- **TimeManager.cs** : Gestion du temps global
- **Enums.cs** : Énumérations partagées
- **Logging/** : Outils de log et de suivi (ex : Logger.cs, LogFormatter.cs)
- **Time/** : Gestion du temps et des timers (ex : Timer.cs, Stopwatch.cs)

## Règles
- N'inclure ici que les utilitaires réutilisables et indépendants de la logique métier.
- Aucun code dépendant directement de la simulation, du gameplay ou de l'UI.
