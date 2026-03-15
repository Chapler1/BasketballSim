namespace BasketballSim.Models;

public class Team
{
    public required string       Name           { get; init; }
    public required string       Abbreviation   { get; init; }
    public required string       PrimaryColor   { get; init; }
    public required string       SecondaryColor { get; init; }
    public required List<Player> Roster         { get; init; }

    public string Division   { get; init; } = "";
    public string Conference { get; init; } = "";

    public double Pace { get; init; } = 100.0;
    public CoachingProfile Coach { get; set; } = CoachingProfiles.Balanced;

    // How many players the coach uses in their rotation (5 = starters only, up to full roster).
    public int RotationDepth { get; init; } = 8;

    // Bias toward playing starters over bench (0 = equal minutes for all, 100 = max starter load).
    // Blends between equal-share allocation and gap-based allocation.
    public int StarterBias { get; init; } = 65;

    public List<Player> Starters => Roster.Take(5).ToList();
    public List<Player> Bench    => Roster.Skip(5).ToList();

    public int SpacingLevel(List<Player> lineup) =>
        lineup.Count(p => p.Attr_ThreePoint >= 55);
}
