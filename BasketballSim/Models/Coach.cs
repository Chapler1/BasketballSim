namespace BasketballSim.Models;

public enum DefensiveStyle
{
    Balanced,         // balanced contest of all shot types
    ProtectThePaint,  // hardens interior, surrenders some perimeter
    StopTheThree,     // hardens perimeter, can be attacked inside
}

public enum OffensiveStyle
{
    Balanced,          // no forced weights — roster expression
    PaceAndSpace,      // rim + 3PT only, high pace
    Heliocentric,      // star-driven, minimal off-ball play
    MotionFlow,        // ball movement upgrades shots, cuts rewarded
    GritAndGrind,      // post/interior focus, OReb emphasis, slower pace
    IsoHeavy,          // star creator, iso-forward but not fully heliocentric
    PickAndRollHeavy,  // system runs through ball screens
}

public class Coach
{
    public required string Name   { get; init; }
    public int Age                { get; set; } = 50;
    public int YearsCoached       { get; set; } = 5;

    public OffensiveStyle OffStyle { get; set; } = OffensiveStyle.Balanced;
    public DefensiveStyle DefStyle { get; set; } = DefensiveStyle.Balanced;

    // Pace preference (85–115); blended 65/35 with the team's roster-driven pace
    public double PacePref        { get; set; } = 100.0;

    // Rotation philosophy (0–100, 50=neutral)
    public int RotationDepthPref  { get; set; } = 50;  // 0=7-man, 100=11-man rotation
    public int VetPreference      { get; set; } = 50;  // >50 favors vets (age≥28), <50 favors youth (≤23)
    public int StarterLoadPref    { get; set; } = 50;  // maps to StarterBias; >50 = starters carry more minutes

    // Quality ratings (visible — observable from reputation/results; centered ~60, range ~40–85)
    public int OffensiveRating    { get; set; } = 60;
    public int DefensiveRating    { get; set; } = 60;

    // Hidden — revealed through scouting, like player skill attributes
    internal int Potential        { get; set; } = 60;  // 0–100; ceiling for rating growth

    // How aggressively this staff game-plans to stop identified offensive threats.
    // 0 = treat all opponents identically; 100 = elite film-room focus on stars.
    // Applied pre-game: high-threat players face harder contests, bench gets easier looks.
    public int HelpDefenseAmount  { get; set; } = 50;  // 0–100; default = balanced
}
