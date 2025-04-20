# Config

Ce dossier contient les ScriptableObjects et fichiers de configuration éditables dans l’éditeur Unity.

### Fichiers typiques à placer ici :
- **SimulationConfig.asset** : Paramètres de simulation
- **GameSettings.asset** : Paramètres globaux du jeu
- **DifficultySettings.asset** : Réglages de difficulté
- **Tout ScriptableObject de configuration**

## Règles
- Utiliser des ScriptableObjects pour toutes les configurations modifiables à chaud ou éditables par un designer.
- Ne pas stocker de données runtime ici.
