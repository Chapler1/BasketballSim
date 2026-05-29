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

    // Advanced on-court tracking totals
    public int    TotalTeamFGMOnCourt     { get; set; }
    public int    TotalTeamORebOnCourt    { get; set; }
    public int    TotalTeamDRebOnCourt    { get; set; }
    public int    TotalOppORebOnCourt     { get; set; }
    public int    TotalOppDRebOnCourt     { get; set; }
    public int    TotalTeamPtsOnCourt     { get; set; }
    public int    TotalOppPtsOnCourt      { get; set; }
    public int    TotalPossessionsOnCourt { get; set; }

    // Populated post-season: team's avg (FGA + 0.44*FTA + TOV) per game, used in USG% formula
    public double TeamPossEventsPg { get; set; }

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

    // Advanced derived stats (computed from on-court tracking totals)
    public double TsPct    => (TotalFGA + 0.44 * TotalFTA) > 0
        ? TotalPTS / (2.0 * (TotalFGA + 0.44 * TotalFTA)) : 0;
    public double EfgPct   => TotalFGA > 0
        ? (TotalFGM + 0.5 * TotalThreeMade) / (double)TotalFGA : 0;
    // NBA formula: 100 * ((FGA + 0.44*FTA + TOV) * (TmMP/5)) / (MP * (TmFGA + 0.44*TmFTA + TmTOV))
    // TmMP/5 = 48 (constant). TeamPossEventsPg = TmFGA + 0.44*TmFTA + TmTOV per game.
    public double UsgPct   => TotalMIN > 0 && TeamPossEventsPg > 0
        ? (TotalFGA + 0.44 * TotalFTA + TotalTOV) * 48.0 / (TotalMIN * TeamPossEventsPg) : 0;
    public double AstPct   => (TotalTeamFGMOnCourt - TotalFGM) > 0
        ? (double)TotalAST / (TotalTeamFGMOnCourt - TotalFGM) : 0;
    public double OrebPct  => (TotalTeamORebOnCourt + TotalOppDRebOnCourt) > 0
        ? (double)TotalOREB / (TotalTeamORebOnCourt + TotalOppDRebOnCourt) : 0;
    public double DrebPct  => (TotalTeamDRebOnCourt + TotalOppORebOnCourt) > 0
        ? (double)TotalDREB / (TotalTeamDRebOnCourt + TotalOppORebOnCourt) : 0;
    public double RebPct   => (TotalTeamORebOnCourt + TotalTeamDRebOnCourt + TotalOppORebOnCourt + TotalOppDRebOnCourt) > 0
        ? (double)(TotalOREB + TotalDREB) / (TotalTeamORebOnCourt + TotalTeamDRebOnCourt + TotalOppORebOnCourt + TotalOppDRebOnCourt) : 0;
    public double OffRating => TotalPossessionsOnCourt > 0
        ? 200.0 * TotalTeamPtsOnCourt / TotalPossessionsOnCourt : 0;
    public double DefRating => TotalPossessionsOnCourt > 0
        ? 200.0 * TotalOppPtsOnCourt / TotalPossessionsOnCourt : 0;
    public double NetRating => OffRating - DefRating;

    // Season fatigue tracking
    public int    GamesRested      { get; set; }  // games auto-benched due to fatigue
    public double TotalFatigueIn   { get; set; }  // sum of pre-game fatigue (played games only)
    public double AvgSeasonFatigue => GP > 0 ? TotalFatigueIn / GP : 100.0;

    // Season injury tracking
    public int              GamesInjured     { get; set; }  // games missed due to injury (Grade 3 / Grade 2 DNP)
    public int              GamesPlayedThrough { get; set; }  // games played with Grade 1 or 2 injury
    public List<InjuryRecord> InjuryHistory  { get; set; } = [];

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
    string   HomeTeam,
    string   AwayTeam,
    string   HomeAbbr,
    string   AwayAbbr,
    int      HomeScore,
    int      AwayScore,
    DateTime Date,
    int      HomeRestDays,
    int      AwayRestDays);

public class PlayoffSeriesRecord
{
    public int    HighSeed      { get; set; }
    public int    LowSeed       { get; set; }
    public string HighSeedTeam  { get; set; } = "";
    public string LowSeedTeam   { get; set; } = "";
    public string HighSeedAbbr  { get; set; } = "";
    public string LowSeedAbbr   { get; set; } = "";
    public int    HighSeedWins  { get; set; }
    public int    LowSeedWins   { get; set; }
    public bool   Complete      => HighSeedWins == 4 || LowSeedWins == 4;
    public string Winner        => HighSeedWins == 4 ? HighSeedTeam : (LowSeedWins == 4 ? LowSeedTeam : "");
    public string WinnerAbbr    => HighSeedWins == 4 ? HighSeedAbbr : (LowSeedWins == 4 ? LowSeedAbbr : "");
    public int    WinnerSeed    => HighSeedWins == 4 ? HighSeed : LowSeed;
    public string SeriesScore   => $"{Math.Max(HighSeedWins, LowSeedWins)}-{Math.Min(HighSeedWins, LowSeedWins)}";
    public List<SeasonGameRecord> Games { get; set; } = [];
    public DateOnly? StartDate { get; set; }
    // NBA 2-2-1-1-1 format: 2 days between games (travel built in)
    private static readonly int[] _gameOffsets = { 0, 2, 4, 6, 8, 10, 12 };
    public DateOnly GameDate(int gameNum) =>
        StartDate.HasValue
            ? StartDate.Value.AddDays(_gameOffsets[Math.Clamp(gameNum - 1, 0, 6)])
            : DateOnly.FromDateTime(DateTime.Now);
    public DateOnly NextGameDate => GameDate(Games.Count + 1);
}

public class PlayoffResult
{
    public List<SeasonGameRecord>    PlayInGames      { get; set; } = [];
    public List<PlayoffSeriesRecord> EastFirstRound   { get; set; } = [];
    public List<PlayoffSeriesRecord> WestFirstRound   { get; set; } = [];
    public List<PlayoffSeriesRecord> EastSecondRound  { get; set; } = [];
    public List<PlayoffSeriesRecord> WestSecondRound  { get; set; } = [];
    public PlayoffSeriesRecord?      EastConfFinals   { get; set; }
    public PlayoffSeriesRecord?      WestConfFinals   { get; set; }
    public PlayoffSeriesRecord?      Finals           { get; set; }
    public string?                   Champion         { get; set; }
    public string?                   ChampionAbbr     { get; set; }
    public List<PlayerSeasonStats>   PlayerStats      { get; set; } = [];
    // Seedings after play-in (for display): 8 teams per conf in seed order
    public List<(string Name, string Abbr, int Seed)> EastSeeds { get; set; } = [];
    public List<(string Name, string Abbr, int Seed)> WestSeeds { get; set; } = [];
}

public record AwardWinner(string Name, string Team, string TeamAbbr, double Score, Position Position);

public class SeasonAwards
{
    public AwardWinner?      MVP     { get; set; }
    public AwardWinner?      DPOY    { get; set; }
    public AwardWinner?      SixMOY  { get; set; }
    public List<AwardWinner> AllNba1 { get; set; } = [];
    public List<AwardWinner> AllNba2 { get; set; } = [];
    public List<AwardWinner> AllNba3 { get; set; } = [];
    public List<AwardWinner> AllDef1 { get; set; } = [];
    public List<AwardWinner> AllDef2 { get; set; } = [];
}

public class SeasonResult
{
    public List<TeamSeasonStats>   TeamStats   { get; set; } = [];
    public List<PlayerSeasonStats> PlayerStats { get; set; } = [];
    public List<SeasonGameRecord>  Games       { get; set; } = [];
    public PlayoffResult?          Playoffs    { get; set; }
    public SeasonAwards?           Awards      { get; set; }
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
