namespace BasketballSim.Models;

public enum DefensiveStyle
{
    Balanced,          // balanced contest of all shot types
    ProtectThePaint,   // hardens interior, surrenders some perimeter
    StopTheThree,      // hardens perimeter, can be attacked inside
}

public enum OffensiveStyle
{
    Balanced,      // no forced weights — roster expression
    PaceAndSpace,  // rim + 3PT only, high pace
    Heliocentric,  // star-driven, minimal passing
    MotionFlow,    // ball movement upgrades shots, high steal risk
    GritAndGrind,  // post/interior focus, OReb emphasis, slower pace
}

public record CoachingProfile(
    double InsideMod,       // multiplier on inside shot weight (0.88–1.15)
    double MidMod,          // multiplier on mid-range shot weight
    double ThreeMod,        // multiplier on three-point shot weight
    double OffensiveRating, // 0–100; context quality & ball security
    double DefensiveRating, // 0–100; scales contest penalty
    DefensiveStyle DefStyle  = DefensiveStyle.Balanced,
    OffensiveStyle OffStyle  = OffensiveStyle.Balanced
);

public static class CoachingProfiles
{
    // Tightened shot-type mods (~0.88–1.15) so style tilts without dominating.
    // OffStyle drives the hard-coded menu multipliers in GenerateMenu.
    public static CoachingProfile Balanced     => new(1.00, 1.00, 1.00, 60, 60);
    public static CoachingProfile PaceAndSpace => new(1.00, 0.88, 1.12, 80, 55, OffStyle: OffensiveStyle.PaceAndSpace);
    public static CoachingProfile PostUp       => new(1.12, 1.08, 0.88, 55, 50, OffStyle: OffensiveStyle.GritAndGrind);
    public static CoachingProfile IsoHeavy     => new(1.04, 1.08, 0.90, 40, 40, OffStyle: OffensiveStyle.Heliocentric);
    public static CoachingProfile MotionFlow   => new(1.00, 0.90, 1.05, 75, 60, OffStyle: OffensiveStyle.MotionFlow);
    public static CoachingProfile DefenseFirst => new(1.00, 0.95, 0.95, 50, 80, DefensiveStyle.ProtectThePaint);
}
