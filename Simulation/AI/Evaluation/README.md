# Evaluation (AI)

Ce dossier regroupe les composants d’évaluation utilisés par l’intelligence artificielle de la simulation.

## Rôle du dossier
- Centraliser la logique d’évaluation de l’état du jeu, des situations, ou des entités pour l’IA.
- Permettre à l’IA de quantifier, scorer ou analyser des contextes (état de match, personnalité, tactique…).
- Favoriser la séparation entre l’évaluation, la prise de décision et le comportement.

## Fichiers présents
- **IGameStateEvaluator.cs** : Interface pour l’évaluation de l’état global du jeu (score, momentum, risques…).
- **IPersonalityEvaluator.cs** : Interface pour l’évaluation de la personnalité ou des traits individuels.
- **ITacticalEvaluator.cs** : Interface pour l’évaluation tactique (positionnement, stratégie, opportunités…).

> Les fichiers `.meta` Unity sont présents pour la gestion des assets et ne doivent pas être modifiés à la main.

## Ce qui doit être placé ici
- Interfaces et implémentations dédiées à l’évaluation (scoring, analyse, quantification) pour l’IA.
- Évaluateurs spécialisés (état de jeu, personnalité, tactique…).

## Ce qui ne doit PAS être placé ici
- Décisionnaires IA (placer dans `Decision`).
- Comportements ou actions IA (placer dans `Behaviors` ou le dossier approprié).
- Contrôleurs ou providers IA globaux.

## Bonnes pratiques
- Organiser les évaluateurs par spécialité ou type d’analyse.
- Utiliser les interfaces pour garantir découplage et testabilité.
- Documenter chaque évaluateur (but, contexte d’utilisation, dépendances).

---

**Note :**
Ce dossier structure la logique d’évaluation pour l’IA, permettant une IA plus modulaire et maintenable.
