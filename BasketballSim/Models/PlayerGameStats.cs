namespace BasketballSim.Models;

public class PlayerGameStats
{
    public required string   Name     { get; init; }
    public required string   Team     { get; init; }
    public required Position Position { get; init; }

    public int    Points       { get; set; }
    public int    Rebounds     { get; set; }
    public int    OffRebounds  { get; set; }
    public int    DefRebounds  { get; set; }
    public int    Assists      { get; set; }
    public int    Steals       { get; set; }
    public int    Blocks       { get; set; }
    public int    Turnovers    { get; set; }
    public int    FGMade       { get; set; }
    public int    FGAttempts   { get; set; }
    public int    ThreeMade    { get; set; }
    public int    ThreeAttempts { get; set; }
    public int    FTMade       { get; set; }
    public int    FTAttempts   { get; set; }
    public double MinutesPlayed { get; set; }
    public int    PlusMinus    { get; set; }

    public string FGDisplay    => $"{FGMade}/{FGAttempts}";
    public string ThreeDisplay => $"{ThreeMade}/{ThreeAttempts}";
    public string FTDisplay    => $"{FTMade}/{FTAttempts}";
    public double FGPct        => FGAttempts > 0 ? (double)FGMade / FGAttempts : 0;
    public double ThreePct     => ThreeAttempts > 0 ? (double)ThreeMade / ThreeAttempts : 0;
}
