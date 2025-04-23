using System;

namespace HandballManager.Data
{
    /// <summary>
    /// Serializable Data Transfer Object for a single match event.
    /// </summary>
    [Serializable]
    public class MatchEventDto
    {
        public float Time; // Time in seconds since match start
        public string EventType;
        public string Description;
        public int? PlayerId;
        public int? TeamId;
        // Add other simple fields as needed
    }
}
