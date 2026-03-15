namespace BasketballSim.Models;

public class PlayerSeasonStats
{
    public string   Name     { get; set; } = "";
    public string   Team     { get; set; } = "";
    public string   TeamAbbr { get; set; } = "";
    public Position Position { get; set; }

    public int    GP           { get; set; }
    public int    TotalPTS     { get; set; }
    public int    TotalOREB    { get; set; }
    public int    TotalDREB    { get; set; }
    public int    TotalAST     { get; set; }
    public int    TotalSTL     { get; set; }
    public int    TotalBLK     { get; set; }
    public int    TotalTOV     { get; set; }
    public int    TotalFGM     { get; set; }
    public int    TotalFGA     { get; set; }
    public int    TotalThreeMade { get; set; }
    public int    TotalThreeAtt  { get; set; }
    public int    TotalFTM     { get; set; }
    public int    TotalFTA     { get; set; }
    public double TotalMIN     { get; set; }

    public int    TotalPlusMinus          { get; set; }
    public int    TotalTouchesTotal       { get; set; }
    public int    TotalShotAttempts       { get; set; }
    public int    TotalTeamTouchesOnCourt { get; set; }
    public int    TotalTeamFGAOnCourt     { get; set; }

    // Shot-type breakdown totals
    public int    TotalInsideMade  { get; set; }
    public int    TotalInsideAtt   { get; set; }
    public int    TotalMidMade     { get; set; }
    public int    TotalMidAtt      { get; set; }

    // Defensive FG (shots defended — primary matchup only)
    public int    TotalDefFGM      { get; set; }
    public int    TotalDefFGA      { get; set; }

    public double Ppg  => GP > 0 ? (double)TotalPTS / GP : 0;
    public double Rpg  => GP > 0 ? (double)(TotalOREB + TotalDREB) / GP : 0;
    public double Opg  => GP > 0 ? (double)TotalOREB / GP : 0;
    public double Dpg  => GP > 0 ? (double)TotalDREB / GP : 0;
    public double Apg  => GP > 0 ? (double)TotalAST / GP : 0;
    public double Spg  => GP > 0 ? (double)TotalSTL / GP : 0;
    public double Bpg  => GP > 0 ? (double)TotalBLK / GP : 0;
    public double Topg => GP > 0 ? (double)TotalTOV / GP : 0;
    public double Mpg  => GP > 0 ? TotalMIN / GP : 0;

    public double Fgm  => GP > 0 ? (double)TotalFGM / GP : 0;
    public double Fga  => GP > 0 ? (double)TotalFGA / GP : 0;
    public double Tpm  => GP > 0 ? (double)TotalThreeMade / GP : 0;
    public double Tpa  => GP > 0 ? (double)TotalThreeAtt / GP : 0;
    public double Ftm  => GP > 0 ? (double)TotalFTM / GP : 0;
    public double Fta  => GP > 0 ? (double)TotalFTA / GP : 0;

    public double FgPct => TotalFGA > 0 ? (double)TotalFGM / TotalFGA : 0;
    public double TpPct => TotalThreeAtt > 0 ? (double)TotalThreeMade / TotalThreeAtt : 0;
    public double FtPct => TotalFTA > 0 ? (double)TotalFTM / TotalFTA : 0;

    public double AvgPlusMinus    => GP > 0 ? (double)TotalPlusMinus / GP : 0;
    public double TouchPct        => TotalTeamTouchesOnCourt > 0 ? (double)TotalTouchesTotal / TotalTeamTouchesOnCourt : 0;
    public double ShotSharePct    => TotalTeamFGAOnCourt > 0 ? (double)TotalFGA / TotalTeamFGAOnCourt : 0;
    public double TouchesPg       => GP > 0 ? (double)TotalTouchesTotal / GP : 0;
    public double ShotAttemptRate => TotalTouchesTotal > 0 ? (double)TotalShotAttempts / TotalTouchesTotal : 0;

    // Shot-type per-game + pct
    public double InsidePg  => GP > 0 ? (double)TotalInsideMade / GP : 0;
    public double InsideApg => GP > 0 ? (double)TotalInsideAtt / GP : 0;
    public double MidPg     => GP > 0 ? (double)TotalMidMade / GP : 0;
    public double MidApg    => GP > 0 ? (double)TotalMidAtt / GP : 0;
    public double InsidePct => TotalInsideAtt > 0 ? (double)TotalInsideMade / TotalInsideAtt : 0;
    public double MidPct    => TotalMidAtt > 0 ? (double)TotalMidMade / TotalMidAtt : 0;

    // Opponent FG% (shots defended)
    public double OppFgPct  => TotalDefFGA > 0 ? (double)TotalDefFGM / TotalDefFGA : 0;

    // Advanced stats — computed post-season by SeasonScheduleService.ComputeAdvancedStats()
    public double PER  { get; set; }   // Player Efficiency Rating (league avg = 15)
    public double OWS  { get; set; }   // Offensive Win Shares
    public double DWS  { get; set; }   // Defensive Win Shares
    public double WS   => OWS + DWS;
    public double WS48 => TotalMIN > 0 ? WS / TotalMIN * 48.0 : 0;
}

public class TeamSeasonStats
{
    public string TeamName    { get; set; } = "";
    public string Abbreviation { get; set; } = "";
    public string Division    { get; set; } = "";
    public string Conference  { get; set; } = "";
    public string PrimaryColor { get; set; } = "#888";

    public int Wins   { get; set; }
    public int Losses { get; set; }
    public int HomeWins    { get; set; }
    public int HomeLosses  { get; set; }
    public int AwayWins    { get; set; }
    public int AwayLosses  { get; set; }
    public int ConfWins    { get; set; }
    public int ConfLosses  { get; set; }
    public int DivWins     { get; set; }
    public int DivLosses   { get; set; }

    // Season totals (divide by GP for per-game)
    public double TotalPtsFor     { get; set; }
    public double TotalPtsAgainst { get; set; }
    public int TotalFGM  { get; set; }
    public int TotalFGA  { get; set; }
    public int TotalThreeMade { get; set; }
    public int TotalThreeAtt  { get; set; }
    public int TotalFTM  { get; set; }
    public int TotalFTA  { get; set; }
    public int TotalOREB { get; set; }
    public int TotalDREB { get; set; }
    public int TotalAST  { get; set; }
    public int TotalSTL  { get; set; }
    public int TotalBLK  { get; set; }
    public int TotalTOV  { get; set; }

    public long TotalPossessions { get; set; }  // offensive possessions this team had all season

    public int GP  => Wins + Losses;
    public double Pct  => GP > 0 ? (double)Wins / GP : 0;
    public double Pace => GP > 0 ? (double)TotalPossessions / GP : 0;

    public double Ppg   => GP > 0 ? TotalPtsFor / GP : 0;
    public double Papg  => GP > 0 ? TotalPtsAgainst / GP : 0;
    public double Fgm   => GP > 0 ? (double)TotalFGM / GP : 0;
    public double Fga   => GP > 0 ? (double)TotalFGA / GP : 0;
    public double FgPct => TotalFGA > 0 ? (double)TotalFGM / TotalFGA : 0;
    public double Tpm   => GP > 0 ? (double)TotalThreeMade / GP : 0;
    public double Tpa   => GP > 0 ? (double)TotalThreeAtt / GP : 0;
    public double TpPct => TotalThreeAtt > 0 ? (double)TotalThreeMade / TotalThreeAtt : 0;
    public double Ftm   => GP > 0 ? (double)TotalFTM / GP : 0;
    public double Fta   => GP > 0 ? (double)TotalFTA / GP : 0;
    public double FtPct => TotalFTA > 0 ? (double)TotalFTM / TotalFTA : 0;
    public double Oreb  => GP > 0 ? (double)TotalOREB / GP : 0;
    public double Dreb  => GP > 0 ? (double)TotalDREB / GP : 0;
    public double Reb   => Oreb + Dreb;
    public double Ast   => GP > 0 ? (double)TotalAST / GP : 0;
    public double Stl   => GP > 0 ? (double)TotalSTL / GP : 0;
    public double Blk   => GP > 0 ? (double)TotalBLK / GP : 0;
    public double Tov   => GP > 0 ? (double)TotalTOV / GP : 0;
}

public record SeasonGameRecord(
    string HomeTeam,
    string AwayTeam,
    string HomeAbbr,
    string AwayAbbr,
    int    HomeScore,
    int    AwayScore);

public class SeasonResult
{
    public List<TeamSeasonStats>   TeamStats   { get; set; } = [];
    public List<PlayerSeasonStats> PlayerStats { get; set; } = [];
    public List<SeasonGameRecord>  Games       { get; set; } = [];
    public DateTime SimulatedAt { get; set; } = DateTime.Now;

    // Each call to SimulatePossessionChain = one possession (ORBs extend, not restart).
    // SecondsElapsed > 0 marks the first event of each chain.
    public long TotalPossessions       { get; set; }  // league-wide across all games
    public long TotalPossessionSeconds { get; set; }  // league-wide
    public long TotalPasses            { get; set; }  // successful passes, league-wide

    // NBA convention: possessions per team per game (~100)
    public double AvgPossessionsPerTeam =>
        Games.Count > 0 ? TotalPossessions / (Games.Count * 2.0) : 0;

    public double AvgPossessionLengthSeconds =>
        TotalPossessions > 0 ? (double)TotalPossessionSeconds / TotalPossessions : 0;

    public double AvgPassesPerPossession =>
        TotalPossessions > 0 ? (double)TotalPasses / TotalPossessions : 0;

    // League-wide shooting splits (pooled across all team-games)
    public double LeagueFgPct =>
        TeamStats.Sum(t => t.TotalFGA) > 0
            ? (double)TeamStats.Sum(t => t.TotalFGM) / TeamStats.Sum(t => t.TotalFGA) : 0;
    public double LeagueTpPct =>
        TeamStats.Sum(t => t.TotalThreeAtt) > 0
            ? (double)TeamStats.Sum(t => t.TotalThreeMade) / TeamStats.Sum(t => t.TotalThreeAtt) : 0;
    public double LeagueFtPct =>
        TeamStats.Sum(t => t.TotalFTA) > 0
            ? (double)TeamStats.Sum(t => t.TotalFTM) / TeamStats.Sum(t => t.TotalFTA) : 0;

    // Per-game per-team averages (averaged across the 30-team pool)
    public double LeagueFgmPg  => TeamStats.Count > 0 ? TeamStats.Average(t => t.Fgm)  : 0;
    public double LeagueFgaPg  => TeamStats.Count > 0 ? TeamStats.Average(t => t.Fga)  : 0;
    public double LeagueTpmPg  => TeamStats.Count > 0 ? TeamStats.Average(t => t.Tpm)  : 0;
    public double LeagueTpaPg  => TeamStats.Count > 0 ? TeamStats.Average(t => t.Tpa)  : 0;
    public double LeagueFtmPg  => TeamStats.Count > 0 ? TeamStats.Average(t => t.Ftm)  : 0;
    public double LeagueFtaPg  => TeamStats.Count > 0 ? TeamStats.Average(t => t.Fta)  : 0;

    // Additional league averages used for advanced stat computation
    public double LeaguePpg   => TeamStats.Count > 0 ? TeamStats.Average(t => t.Ppg)  : 0;
    public double LeaguePapg  => TeamStats.Count > 0 ? TeamStats.Average(t => t.Papg) : 0;
    public double LeagueOrbPg => TeamStats.Count > 0 ? TeamStats.Average(t => t.Oreb) : 0;
    public double LeagueDrbPg => TeamStats.Count > 0 ? TeamStats.Average(t => t.Dreb) : 0;
    public double LeagueAstPg => TeamStats.Count > 0 ? TeamStats.Average(t => t.Ast)  : 0;
    public double LeagueTovPg => TeamStats.Count > 0 ? TeamStats.Average(t => t.Tov)  : 0;
}
