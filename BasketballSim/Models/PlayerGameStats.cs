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
    public int    Fouls        { get; set; }
    public int    FGMade       { get; set; }
    public int    FGAttempts   { get; set; }
    public int    ThreeMade    { get; set; }
    public int    ThreeAttempts { get; set; }
    public int    FTMade       { get; set; }
    public int    FTAttempts   { get; set; }
    public double MinutesPlayed { get; set; }
    public int    PlusMinus    { get; set; }

    // Player tracking stats
    public int    TouchesTotal       { get; set; }   // times this player had the ball
    public int    ShotAttempts       { get; set; }   // FGA + pre-shot fouls (shots that drew FTs without a FGA)
    public int    TeamTouchesOnCourt { get; set; }   // total touches by any player while this player is on court
    public int    TeamFGAOnCourt     { get; set; }   // total team FGA while this player is on court

    // Advanced on-court tracking
    public int TeamFGMOnCourt      { get; set; }  // team FGM while on court (for AST%)
    public int TeamORebOnCourt     { get; set; }  // team OReb while on court (for OREB%)
    public int TeamDRebOnCourt     { get; set; }  // team DReb while on court (for DREB%)
    public int OppORebOnCourt      { get; set; }  // opp OReb while on court (for DREB% denom)
    public int OppDRebOnCourt      { get; set; }  // opp DReb while on court (for OREB% denom)
    public int TeamPtsOnCourt      { get; set; }  // team pts scored while on court (for OFF_RTG)
    public int OppPtsOnCourt       { get; set; }  // opp pts scored while on court (for DEF_RTG)
    public int PossessionsOnCourt  { get; set; }  // off + def possessions while on court

    // Shot-type breakdown (only final FGA, consistent with FGAttempts after FT conversions)
    public int InsideMade   { get; set; }
    public int InsideAtt    { get; set; }
    public int MidRangeMade { get; set; }
    public int MidRangeAtt  { get; set; }

    // Defensive: shots attempted and made against this player's primary coverage
    public int DefFGMade     { get; set; }
    public int DefFGAttempts { get; set; }

    public string FGDisplay    => $"{FGMade}/{FGAttempts}";
    public string ThreeDisplay => $"{ThreeMade}/{ThreeAttempts}";
    public string FTDisplay    => $"{FTMade}/{FTAttempts}";
    public double FGPct           => FGAttempts > 0 ? (double)FGMade / FGAttempts : 0;
    public double ThreePct        => ThreeAttempts > 0 ? (double)ThreeMade / ThreeAttempts : 0;
    public double InsidePct       => InsideAtt > 0 ? (double)InsideMade / InsideAtt : 0;
    public double MidRangePct     => MidRangeAtt > 0 ? (double)MidRangeMade / MidRangeAtt : 0;
    public double DefFgPct        => DefFGAttempts > 0 ? (double)DefFGMade / DefFGAttempts : 0;
    public double TouchPct        => TeamTouchesOnCourt > 0 ? (double)TouchesTotal / TeamTouchesOnCourt : 0;
    public double ShotSharePct    => TeamFGAOnCourt > 0 ? (double)FGAttempts / TeamFGAOnCourt : 0;
    public double ShotAttemptRate => TouchesTotal > 0 ? (double)ShotAttempts / TouchesTotal : 0;
}
