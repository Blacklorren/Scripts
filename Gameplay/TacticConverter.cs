using HandballManager.Data;
using System;

namespace HandballManager.Gameplay
{
    /// <summary>
    /// Utilitaire pour convertir entre Tactic (Gameplay) et TacticData (Data).
    /// </summary>
    public static class TacticConverter
    {
        public static TacticData ToData(Tactic tactic)
        {
            if (tactic == null) return null;
            return new TacticData
            {
                TacticID = tactic.TacticID,
                Name = tactic.TacticName,
                Type = tactic.OffensiveFormationName, // Store formation names
                OffensiveFormationName = tactic.OffensiveFormationData.Name,
                DefensiveFormationName = tactic.DefensiveFormationData.Name,
                Description = $"DefSys: {tactic.DefensiveSystem}, Pace: {tactic.Pace}"
                // Ajouter d'autres propriétés si besoin
            };
        }

        public static Tactic FromData(TacticData data)
        {
            if (data == null) return null;
            return new Tactic
            {
                TacticID = data.TacticID,
                TacticName = data.Name,
                OffensiveFormation = data.OffensiveFormationName,
                DefensiveSystem = DefensiveSystem.SixZero, // Default, actual system should be set or parsed
                OffensiveFormationData = LoadFormationData(data.OffensiveFormationName),
                DefensiveFormationData = LoadFormationData(data.DefensiveFormationName)
            };
        }

        private static FormationData LoadFormationData(string name)
        {
            // TODO: implement loading logic from ScriptableObject or JSON
            return new FormationData { Name = name };
        }
    }
}
