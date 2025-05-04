using HandballManager.Data;
using HandballManager.Core;
using System;
using UnityEngine;

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
                // Use the '.FormationName' property from the ScriptableObject definition
                OffensiveFormationName = tactic.OffensiveFormationData?.FormationName,
                DefensiveFormationName = tactic.DefensiveFormationData?.FormationName,
            };
        }

        public static Tactic FromData(TacticData data)
        {
            if (data == null) return null;
            Tactic tactic = new Tactic
            {
                TacticID = data.TacticID,
                TacticName = data.Name,
                OffensiveFormationData = LoadFormationData(data.OffensiveFormationName),
                DefensiveFormationData = LoadFormationData(data.DefensiveFormationName)
            };

            return tactic;
        }

        private static FormationData LoadFormationData(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("Attempted to load FormationData with null or empty name.");
                return null;
            }

            string resourcePath = "Formations/" + name;
            FormationData loadedData = Resources.Load<FormationData>(resourcePath);

            if (loadedData == null)
            {
                Debug.LogWarning($"Failed to load FormationData from Resources: {resourcePath}. Make sure the asset exists and the path is correct.");
            }

            return loadedData;
        }
    }
}
