namespace BasketballSim.Models;

public record CoachingProfile(
    double InsideMod,      // multiplier on inside shot weight (e.g. 1.35 = post-up heavy)
    double MidMod,         // multiplier on mid-range shot weight
    double ThreeMod,       // multiplier on three-point shot weight
    double CoachingRating  // 0–100; affects context quality (corner 3s, cuts, transition)
);

public static class CoachingProfiles
{
    public static CoachingProfile Balanced     => new(1.00, 1.00, 1.00, 60);
    public static CoachingProfile PaceAndSpace => new(1.10, 0.65, 1.45, 80);
    public static CoachingProfile PostUp       => new(1.35, 1.20, 0.60, 55);
    public static CoachingProfile IsoHeavy     => new(1.05, 1.15, 0.85, 40);
    public static CoachingProfile DefenseFirst => new(1.00, 0.90, 0.95, 50);
}
