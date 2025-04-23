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
                Type = tactic.OffensiveFormation, // Peut être adapté selon besoin
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
                OffensiveFormation = data.Type, // Peut être adapté selon besoin
                // DefensiveSystem, Pace, etc. peuvent être extraits de Description ou par extension
            };
        }
    }
}
