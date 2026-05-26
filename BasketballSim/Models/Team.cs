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
    public Coach  Coach { get; set; } = new Coach { Name = "Staff Coach" };

    // Derived from coach — how many players the rotation uses (7–11)
    public int RotationDepth => Math.Clamp(7 + (int)(Coach.RotationDepthPref / 100.0 * 4), 5, Roster.Count);

    // Derived from coach — bias toward starters (0=equal, 100=max starter load)
    public int StarterBias => Coach.StarterLoadPref;

    public List<Player> Starters => Roster.Take(5).ToList();
    public List<Player> Bench    => Roster.Skip(5).ToList();

    public int SpacingLevel(List<Player> lineup) =>
        lineup.Count(p => p.Attr_ThreePoint >= 55);
}
