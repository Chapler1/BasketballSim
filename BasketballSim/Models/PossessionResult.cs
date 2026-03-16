namespace BasketballSim.Models;

public enum PossessionState { HalfCourt, Transition, FastBreak, SecondChance }

/// <summary>Per-context entry shown in the debug decision table.</summary>
public class MenuDebugEntry
{
    public ShotContext Context       { get; set; }
    public ShotType    ShotType      { get; set; }
    public double      ActualPPS     { get; set; }
    public double      PerceivedPPS  { get; set; }
    public double      Threshold     { get; set; }
    public double      BaseMake      { get; set; }
    public double      ContestMod    { get; set; }
    public double      StyleMult     { get; set; }
    public bool        WasChosen     { get; set; }
    public bool        IsPassOption  { get; set; }
    public string?     ReceiverName  { get; set; }
}

/// <summary>One decision moment: ball handler sees combined menu and chooses.</summary>
public class DecisionStep
{
    public string               BallHandler { get; set; } = "";
    public double               ClockDecay  { get; set; }
    public List<MenuDebugEntry> Menu        { get; set; } = [];
    /// <summary>"Shot: CatchAndShootCorner" | "Pass → Curry" | "Blind Pass → James" | "TO: stolen/deadball"</summary>
    public string               Action      { get; set; } = "";
}

/// <summary>Decision debug info attached to PossessionResults when DebugMode is on.</summary>
public class DecisionDebugInfo
{
    public int                ClockSeconds    { get; set; }
    public PossessionState    PossessionState { get; set; }
    public List<DecisionStep> Steps           { get; set; } = [];
}

public enum ShotType    { Inside, MidRange, ThreePointer }

public enum ShotContext
{
    // Inside (layups + dunks)
    DrivingLayup, PickAndRollRoll, CutLayup, FastBreakLayup, PostMove, FloaterLayup,
    AlleyOop, CutDunk, TransitionDunk, PickAndRollDunk, ContactDunk,
    // Mid Range
    PullUpMidRange, IsolationMidRange, PickAndRollMidRange, FadeawayMidRange,
    PostMoveMidRange, LateClockMidRange,
    // Three
    CatchAndShootCorner, CatchAndShootWing, PullUpThree, StepBackThree,
    PickAndRollThree, TransitionThree,
    // Secondary
    Putback, TipIn,
    // None (turnovers/FT)
    None
}

public enum PossessionEvent
{
    ShotMade, ShotMissed, Blocked, TurnoverStolen, TurnoverDeadBall,
    FreeThrows, OffensiveRebound, DefensiveRebound, GameWinnerMade, GameWinnerMissed,
    NonShootingFoul, OffensiveFoul,
    LooseBallRecovered, ShotOutOfBounds
}

public class PossessionResult
{
    public required string          Team         { get; init; }
    public required string          Narrative    { get; init; }
    public          int             HomeScore    { get; init; }
    public          int             AwayScore    { get; init; }
    public          int             Quarter      { get; init; }
    public          int             ClockSeconds { get; init; }
    public          int             PointsScored { get; init; }
    public          PossessionEvent Event        { get; init; }

    public string? Scorer    { get; init; }
    public string? Assister  { get; init; }
    public string? Blocker   { get; init; }
    public string? Rebounder { get; init; }
    public string? Stealer   { get; init; }
    public string? Fouler    { get; init; }

    public ShotType?    Shot       { get; init; }
    public ShotContext? Context    { get; init; }
    public bool         IsThree   { get; init; }
    public int          FTAttempts { get; init; }  // only set when Event == FreeThrows

    // Set once (on the first event of each possession chain) for live replay tracking.
    // HomeLineup/AwayLineup = player names on the floor; SecondsElapsed = clock time consumed.
    public IReadOnlyList<string> HomeLineup     { get; set; } = [];
    public IReadOnlyList<string> AwayLineup     { get; set; } = [];
    public int                   SecondsElapsed { get; set; }
    public int                   PassCount      { get; set; }

    // Populated when GameEngine.DebugMode == true (shot events only).
    public DecisionDebugInfo? Debug { get; set; }
}
