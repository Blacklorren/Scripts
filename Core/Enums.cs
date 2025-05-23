namespace HandballManager.Core
{
    /// <summary>
    /// Defines the primary states the game can be in.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        Loading,
        InSeason, // Main gameplay loop, advancing time week by week/day by day
        SimulatingMatch, // State while a match simulation is actively running
        MatchReport, // Displaying the results of a match
        ManagingTeam, // Player is interacting with team screens (tactics, roster, etc.)
        TransferWindow, // Specific period, might overlap with InSeason or OffSeason
        OffSeason, // Period between seasons for specific activities
        Paused // Generic paused state, perhaps showing a menu
    }

    /// <summary>
    /// Standard player positions in Handball.
    /// </summary>
    public enum PlayerPosition
    {
        None, // Represents no specific position assigned
        Goalkeeper,
        LeftWing,
        LeftBack,
        CentreBack, // Often referred to as Playmaker
        Pivot, // Also known as Line Player
        RightBack,
        RightWing
    }

    /// <summary>
    /// Different roles staff members can have.
    /// </summary>
    public enum StaffRole
    {
        HeadCoach,
        AssistantCoach,
        Physiotherapist,
        Scout,
        YouthCoach,
        Chairman // Added for potential interactions
    }

    /// <summary>
    /// Basic tactical pace options.
    /// </summary>
    public enum TacticPace
    {
        VerySlow,
        Slow,
        Normal,
        Fast,
        VeryFast
    }

    /// <summary>
    /// Common defensive systems in Handball.
    /// </summary>
    public enum DefensiveSystem
    {
        SixZero,    // 6-0
        FiveOne,    // 5-1
        ThreeTwoOne // 3-2-1
        // Add others like 4-2, 3-3 if needed
    }

     /// <summary>
    /// Where the offensive play should primarily be focused.
    /// </summary>
    public enum OffensiveFocusPlay
    {
        Balanced,
        Wings,
        Backs,
        Pivot
    }

    /// <summary>
    /// Status of a player's injury.
    /// </summary>
    public enum InjuryStatus
    {
        Healthy,
        MinorInjury, // Few days out (e.g., knock, slight strain)
        ModerateInjury, // Weeks out (e.g., muscle tear, sprain)
        MajorInjury, // Months out (e.g., ligament rupture, fracture)
        CareerThreatening // Long term, potential stat reduction
    }

    /// <summary>
    /// Represents different training focuses the manager can set.
    /// </summary>
    public enum TrainingFocus
    {
        General,
        Fitness,
        AttackingMovement,
        DefensiveShape,
        ShootingPractice,
        Goalkeeping,
        SetPieces,
        YouthDevelopment
    }

    // Add other simple enums as needed, e.g., PlayerPersonalityTrait, TransferStatus etc.

    /// <summary>
    /// Defines intensity levels for training or simulation.
    /// </summary>
    public enum Intensity
    {
        Low,
        Normal,
        High
    }

    /// <summary>
    /// Defines personality traits that influence player behavior during matches.
    /// </summary>
    public enum PlayerPersonalityTrait
    {
        Balanced,       // Default - no strong tendencies
        Determined,     // Takes more shots, tackles more, less hesitation
        Professional,   // Makes safer decisions, less risky passes/shots
        Ambitious,      // Takes more risks, shoots more
        Loyal,          // Passes more, team-oriented
        Volatile,       // More risky actions, less predictable
        Aggressive,     // More tackles, higher foul risk
        Leader,         // Inspires and organizes teammates, higher influence
        Lazy            // Less defensive work rate
    }

    /// <summary>
    /// Defines the transfer availability status of a player.
    /// </summary>
    public enum TransferStatus
    {
        Unavailable,         // Not available for transfer
        AvailableForLoan,    // Available for loan to another club
        AvailableForTransfer, // Available for permanent transfer
        ListedByClub,        // Actively being offered by the club
        RequestedTransfer    // Player has requested to leave
    }

    /// <summary>
    /// Defines the different phases of a handball match simulation.
    /// </summary>
    public enum GamePhase
    {
        PreKickOff,             // Initial setup before match starts
        KickOff,                // Match kickoff phase
        HomeAttack,             // Home team in possession and attacking
        AwayAttack,             // Away team in possession and attacking
        TransitionToHomeAttack, // Ball possession changing to home team
        TransitionToAwayAttack, // Ball possession changing to away team
        HomeSetPiece,           // Home team taking a set piece (throw-in, free throw)
        AwaySetPiece,           // Away team taking a set piece
        HomePenalty,            // Home team taking a penalty
        AwayPenalty,            // Away team taking a penalty
        ContestedBall,          // Ball is loose/contested
        HalfTime,               // Half-time break
        Timeout,                // Team or referee timeout
        Finished                // Match has concluded
    }

    /// <summary>
    /// Defines specific game situations that require special movement handling.
    /// </summary>
    public enum GameSituationType
    {
        /// <summary>Normal gameplay, no special situation.</summary>
        Normal,
        /// <summary>Free throw situation.</summary>
        FreeThrow,
        /// <summary>Seven-meter throw (penalty) situation.</summary>
        Penalty,
        /// <summary>Kick-off at center court.</summary>
        KickOff,
        /// <summary>Throw-in from sideline.</summary>
        ThrowIn,
        /// <summary>Goal throw by goalkeeper.</summary>
        GoalThrow,
        /// <summary>During an active timeout.</summary>
        Timeout,
        /// <summary>During half-time break.</summary>
        HalfTime
    }
}