# Decision (AI)

Ce dossier regroupe les composants de prise de décision pour l’intelligence artificielle de la simulation.

## Rôle du dossier
- Centraliser la logique de décision individuelle ou collective (joueur, équipe, phase de jeu) pour l’IA.
- Permettre d’organiser les différents types de décisionnaires par rôle ou spécialité (attaque, défense, passe, tir, dribble, etc.).
- Favoriser la séparation des responsabilités et la testabilité de la logique IA.

## Fichiers présents
- **DefaultDefensiveDecisionMaker.cs** : Implémentation par défaut du décisionnaire défensif IA.
- **DefaultOffensiveDecisionMaker.cs** : Implémentation par défaut du décisionnaire offensif IA.
- **IDefensiveDecisionMaker.cs** : Interface pour les décisionnaires défensifs.
- **IOffensiveDecisionMaker.cs** : Interface pour les décisionnaires offensifs.
- **IDribblingDecisionMaker.cs** : Interface pour la prise de décision de dribble.
- **IPassingDecisionMaker.cs** : Interface pour la prise de décision de passe.
- **IShootingDecisionMaker.cs** : Interface pour la prise de décision de tir.

> Les fichiers `.meta` Unity sont présents pour la gestion des assets et ne doivent pas être modifiés à la main.

## Ce qui doit être placé ici
- Toute classe ou interface dédiée à la prise de décision IA (individuelle ou collective).
- Implémentations spécialisées (attaque, défense, passes, tirs, etc.).

## Ce qui ne doit PAS être placé ici
- Logique comportementale (placer dans `Behaviors` ou `Evaluation` si concerné).
- Contrôleurs IA globaux, providers de stratégie ou services IA (placer dans `AI/` ou sous-dossiers appropriés).

## Bonnes pratiques
- Organiser les décisionnaires par spécialité ou phase de jeu.
- Utiliser les interfaces pour garantir découplage et testabilité.
- Documenter chaque décisionnaire (but, contexte d’utilisation, dépendances).

---

**Note :**
Ce dossier structure la logique de prise de décision de l’IA, pour une organisation claire et évolutive du code simulation/AI.
