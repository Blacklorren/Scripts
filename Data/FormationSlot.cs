using UnityEngine;
using HandballManager.Core; // Contains PlayerPosition enum

namespace HandballManager.Gameplay // Changed namespace slightly for broader scope initially
{
    [System.Serializable]
    public class FormationSlot
    {
        [Tooltip("Descriptive name for this slot (e.g., 'Left Back Attacking', 'Central Defender')")]
        public string RoleName = "Default Role";

        [Tooltip("The general player position associated with this slot.")]
        public PlayerPosition AssociatedPosition = PlayerPosition.CentreBack; // Default value

        [Tooltip("Base coordinates relative to a formation anchor point (e.g., center of the court half). X = horizontal, Y = vertical.")]
        public Vector2 BasePositionOffset = Vector2.zero;

        // Optional: Add default instructions or behaviors later if needed
        // public string[] DefaultInstructions;
    }
}
