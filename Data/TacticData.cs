using System;

namespace HandballManager.Data
{
    /// <summary>
    /// Structure légère représentant une tactique, indépendante de la logique Gameplay.
    /// </summary>
    [Serializable]
    public class TacticData
    {
        public Guid TacticID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // Ex: "Offensive", "Défensive", etc.
        public string Description { get; set; }
        // Ajouter ici d'autres propriétés purement données nécessaires à la sauvegarde/chargement
    }
}
