# UI

Contient tous les scripts liés à l'interface utilisateur et à la gestion des écrans.

### Fichiers typiques à placer ici :
- **UIManager.cs** : Contrôle principal de l'UI
- **PanelController.cs** : Gestion des panneaux et écrans
- **UIEventDispatcher.cs** : Communication UI <-> logique
- **Custom UI Components** : Boutons, sliders, etc.

## Règles
- Ne pas inclure de logique métier ici.
- Les scripts UI doivent communiquer avec le reste du jeu via des événements ou des interfaces.
