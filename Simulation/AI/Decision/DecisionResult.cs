namespace HandballManager.Simulation.AI.Decision
{
    /// <summary>
    /// Represents the result of an AI decision.
    /// </summary>
    public class DecisionResult
    {
        /// <summary>
        /// Gets or sets whether the decision was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets the confidence level of the decision (0.0 to 1.0).
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Gets or sets additional data related to the decision.
        /// </summary>
        public object Data { get; set; }
    }
}
