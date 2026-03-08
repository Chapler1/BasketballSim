namespace BasketballSim.Models;

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
    FreeThrows, OffensiveRebound, DefensiveRebound, GameWinnerMade, GameWinnerMissed
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

    public ShotType?    Shot       { get; init; }
    public ShotContext? Context    { get; init; }
    public bool         IsThree   { get; init; }
    public int          FTAttempts { get; init; }  // only set when Event == FreeThrows
}
