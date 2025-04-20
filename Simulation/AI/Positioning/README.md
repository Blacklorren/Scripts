# Positioning (AI)

Ce dossier regroupe les composants d’intelligence artificielle dédiés au positionnement des entités dans la simulation.

## Rôle du dossier
- Centraliser la logique de positionnement IA (placement des joueurs, gardiens, lignes, etc.).
- Fournir des interfaces et implémentations pour le calcul des positions optimales selon le contexte de jeu.
- Permettre la spécialisation du positionnement (par rôle, situation, tactique).

## Fichiers présents
- **IGoalkeeperPositioner.cs** : Interface pour la logique de positionnement des gardiens de but.

> Les fichiers `.meta` Unity sont présents pour la gestion des assets et ne doivent pas être modifiés à la main.

## Ce qui doit être placé ici
- Interfaces et implémentations dédiées au positionnement IA (gardiens, joueurs, lignes, etc.).
- Helpers ou stratégies spécifiques de placement.

## Ce qui ne doit PAS être placé ici
- Décisionnaires IA (placer dans `Decision`).
- Évaluateurs IA (placer dans `Evaluation`).
- Contrôleurs IA globaux ou providers de stratégie (placer dans `AI/` ou sous-dossiers appropriés).

## Bonnes pratiques
- Organiser les positionneurs par spécialité ou rôle.
- Utiliser les interfaces pour garantir découplage et testabilité.
- Documenter chaque positionneur (but, contexte d’utilisation, dépendances).

---

**Note :**
Ce dossier structure la logique de positionnement IA pour une simulation plus réaliste et évolutive.
