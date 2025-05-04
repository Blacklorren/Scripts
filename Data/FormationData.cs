using UnityEngine;
using System.Collections.Generic;
using HandballManager.Core;

namespace HandballManager.Gameplay // Changed namespace slightly for broader scope initially
{
    [CreateAssetMenu(fileName = "NewFormation", menuName = "Handball Manager/Formation Data")] // Simplified menu path
    public class FormationData : ScriptableObject
    {
        [Tooltip("Unique identifier name for this formation (e.g., '3-2-1 Attack', '6-0 Defense')")]
        public string FormationName = "New Formation";

        [Tooltip("List of slots defining the positions in this formation.")]
        public List<FormationSlot> Slots = new List<FormationSlot>();

        // Optional: Could add properties like formation type (Offensive/Defensive) if needed
        // public FormationType Type = FormationType.Offensive;

        /// <summary>
        /// Finds a formation slot by its role name.
        /// </summary>
        /// <param name="roleName">The name of the role to find.</param>
        /// <returns>The FormationSlot if found, otherwise null.</returns>
        public FormationSlot GetSlotByRoleName(string roleName)
        {
            return Slots.Find(slot => slot.RoleName == roleName);
        }

         /// <summary>
        /// Finds a formation slot by its associated PlayerPosition.
        /// Note: Might return the first match if multiple slots share the same position.
        /// Consider using RoleName for unique identification if needed.
        /// </summary>
        /// <param name="position">The PlayerPosition to find.</param>
        /// <returns>The FormationSlot if found, otherwise null.</returns>
        public FormationSlot GetSlotByPosition(PlayerPosition position)
        {
            return Slots.Find(slot => slot.AssociatedPosition == position);
        }
    }
}
