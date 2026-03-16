namespace BasketballSim.Models;

public class HistoricalSeasonStats
{
    public string Season { get; set; } = "";
    public string Team   { get; set; } = "";
    public int    Gp     { get; set; }
    public double Min    { get; set; }
    public double Pts    { get; set; }
    public double Reb    { get; set; }
    public double Ast    { get; set; }
    public double Stl    { get; set; }
    public double Blk    { get; set; }
    public double Tov    { get; set; }
    public double Fgm    { get; set; }
    public double Fga    { get; set; }
    public double FgPct  { get; set; }
    public double Fg3m   { get; set; }
    public double Fg3a   { get; set; }
    public double Fg3Pct { get; set; }
    public double Ftm    { get; set; }
    public double Fta    { get; set; }
    public double FtPct  { get; set; }

    // Advanced stats (from NBA API Advanced measure type)
    public double? TsPct    { get; set; }
    public double? EfgPct   { get; set; }
    public double? UsgPct   { get; set; }
    public double? AstPct   { get; set; }
    public double? OrebPct  { get; set; }
    public double? DrebPct  { get; set; }
    public double? RebPct   { get; set; }
    public double? OffRating { get; set; }
    public double? DefRating { get; set; }
    public double? NetRating { get; set; }
}
