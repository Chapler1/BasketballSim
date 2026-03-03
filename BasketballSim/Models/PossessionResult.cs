namespace BasketballSim.Models;

public class PossessionResult
{
    public required string Team { get; init; }
    public required string Narrative { get; init; }
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
    public int Quarter { get; init; }
    public int ClockSeconds { get; init; }
    public int PointsScored { get; init; }
}
