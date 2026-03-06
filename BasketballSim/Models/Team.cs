namespace BasketballSim.Models;

public class Team
{
    public required string       Name           { get; init; }
    public required string       Abbreviation   { get; init; }
    public required string       PrimaryColor   { get; init; }
    public required string       SecondaryColor { get; init; }
    public required List<Player> Roster         { get; init; }

    public double Pace { get; init; } = 100.0;

    public List<Player> Starters => Roster.Take(5).ToList();
    public List<Player> Bench    => Roster.Skip(5).ToList();

    public int SpacingLevel(List<Player> lineup) =>
        lineup.Count(p => p.Attr_ThreePoint >= 55);
}
