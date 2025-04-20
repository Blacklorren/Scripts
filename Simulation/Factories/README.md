# Factories

Ce dossier regroupe toutes les fabriques (factories) responsables de la création d’objets complexes au sein de la simulation. L’utilisation du pattern Factory permet d’isoler la logique d’instanciation et d’injection de dépendances, facilitant la maintenance, les tests, et la configuration flexible des composants.

## Responsabilités
- Centraliser la création d’objets complexes (ex : joueurs, équipes, simulateurs de match).
- Gérer l’injection des dépendances requises pour chaque objet créé.
- Fournir des points d’extension pour la configuration ou la personnalisation des objets instanciés.
- Garantir la cohérence et l’encapsulation de la logique de construction.

## Exemples de fichiers typiques à placer ici
- **PlayerFactory.cs** : Création d’instances de joueurs avec paramètres personnalisés.
- **TeamFactory.cs** : Construction d’équipes à partir de données ou de templates.
- **MatchSimulatorFactory.cs** : Création et configuration complète d’un simulateur de match (voir exemple ci-dessous).
- **Tout script utilisant le pattern Factory pour la simulation**

## Exemple : MatchSimulatorFactory
La classe `MatchSimulatorFactory` illustre le rôle d’une fabrique avancée :
- Elle reçoit via son constructeur toutes les dépendances nécessaires (gestionnaire de phases, IA, gestionnaire d’événements, etc.).
- Elle expose une méthode `Create` qui instancie un `MatchSimulator` préconfiguré, prêt à simuler un match avec tous les services injectés.
- Elle ne contient aucune logique métier propre à la simulation du match (cette logique reste dans le simulateur ou les engines).

## Règles d’architecture
- **Utiliser systématiquement le pattern Factory** pour isoler la création d’objets complexes ou fortement couplés.
- **Aucune logique métier** ne doit être présente dans les factories : elles se limitent à l’instanciation, la configuration et l’injection de dépendances.
- **Favoriser l’injection de dépendances** via le constructeur ou des méthodes dédiées.
- **Documenter** les paramètres attendus et les points d’extension pour chaque factory.

## Ajouter une nouvelle factory
1. Créer un nouveau script dans ce dossier, en respectant le nommage explicite (ex : `CustomBallFactory.cs`).
2. Définir les dépendances requises dans le constructeur.
3. Exposer une ou plusieurs méthodes de création (`Create`, `Build`, etc.) qui retournent l’objet construit.
4. Documenter les usages et les paramètres dans le README et dans le code.

---

Pour toute question sur l’organisation ou les responsabilités du dossier, se référer au README général de `Simulation/` ou demander conseil à l’équipe technique.
