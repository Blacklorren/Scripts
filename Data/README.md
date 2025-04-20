# Data

Ce dossier regroupe toutes les structures de données métiers et persistantes.

### Fichiers typiques à placer ici :
- **PlayerData.cs** : Données des joueurs (POCO ou ScriptableObject)
- **TeamData.cs** : Données des équipes (POCO ou ScriptableObject)
- **MatchResult.cs** : Résultats de matchs
- **CompetitionData.cs** : Données de compétitions
- **Enums spécifiques aux données**

## Règles
- Pas de logique métier ici, uniquement des POCO (Plain Old C# Objects) ou des ScriptableObjects de données.
- Les fichiers ici doivent pouvoir être sérialisés facilement.
