using System;
using System.Collections.Generic;
using UnityEngine;
using HandballManager.Core;

namespace HandballManager.Gameplay
{
    [Serializable]
    public class FormationSlot
    {
        /// <summary>The player role or position for this slot.</summary>
        public PlayerPosition PositionRole { get; set; }
        /// <summary>Normalized position on the pitch (0-1 range).</summary>
        public Vector2 RelativePosition { get; set; }
        // Optionally add basic instructions, e.g., public string Instruction { get; set; }
    }

    [Serializable]
    public class FormationData
    {
        /// <summary>Name identifier for the formation.</summary>
        public string Name { get; set; }
        /// <summary>List of slots defining relative positions for each role.</summary>
        public List<FormationSlot> Slots { get; set; } = new List<FormationSlot>();
    }
}
