# Installers

Ce dossier regroupe les scripts d’installation et d’injection de dépendances pour la simulation. Les installateurs (installers) servent à centraliser la configuration, l’initialisation et l’injection des services, engines, et composants nécessaires au bon fonctionnement du système de simulation.

## Responsabilités
- Centraliser l’initialisation et la configuration des services et engines de simulation.
- Gérer l’injection de dépendances (via Zenject ou tout autre framework DI).
- Faciliter le bootstrap de la simulation dans différents contextes (jeu, tests, outils).
- Garantir la cohérence et la modularité de l’architecture logicielle.

## Exemples de fichiers typiques à placer ici
- **SimulationInstaller.cs** : Installe et configure tous les services principaux de la simulation (voir exemple ci-dessous).
- **EngineInstaller.cs** : Installation spécifique pour un moteur ou un module particulier.
- **Tout script de bootstrap ou d’initialisation de la simulation**

## Exemple : SimulationInstaller
La classe `SimulationInstaller` illustre le rôle d’un installateur :
- Elle hérite de `MonoInstaller` (Zenject) et définit la méthode `InstallBindings`.
- Elle déclare et configure toutes les dépendances nécessaires (engines, services, AI, providers, etc.).
- Elle permet de personnaliser l’injection (ex : tactiques ou équipes dynamiques).
- Elle ne contient aucune logique métier propre à la simulation.

## Règles d’architecture
- **Centraliser l’initialisation** des services et engines de simulation dans ce dossier.
- **Respecter l’injection de dépendances** pour faciliter les tests, la maintenance et la flexibilité.
- **Ne pas inclure de logique métier** dans les installateurs : ils se limitent à la configuration et à l’injection.
- **Documenter** les paramètres configurables et les points d’extension pour chaque installateur.

## Ajouter un nouvel installer
1. Créer un nouveau script dans ce dossier, héritant du type d’installer approprié (`MonoInstaller`, `ScriptableObjectInstaller`, etc.).
2. Définir les dépendances à injecter et leur cycle de vie (`AsSingle`, `AsTransient`, etc.).
3. Documenter les usages, les paramètres et les points d’extension dans le README et le code.

---

Pour toute question sur l’organisation ou les responsabilités du dossier, se référer au README général de `Simulation/` ou demander conseil à l’équipe technique.
