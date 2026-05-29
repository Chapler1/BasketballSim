using System.Text.Json;
using BasketballSim.Models;
using BasketballSim.Services;
using BasketballSim.Simulation;

// ── Output setup ────────────────────────────────────────────────────────────
var outputPath = Path.Combine(
    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
    "calibration_output.txt");

using var file = new StreamWriter(outputPath, append: false);

// ── Run all scenarios ────────────────────────────────────────────────────────
W("══════════════════════════════════════════════════════════════════");
W(" NBA 2023-24 TARGETS  (per team per game)");
W("══════════════════════════════════════════════════════════════════");
W(" PTS  114.6  |  FGA 88.7  |  FGM 42.9  |  FG%  48.4%");
W(" 3PA   35.1  |  3PM  13.1 |  3P%  37.3%");
W(" 2PA   53.6  |  2PM  29.8 |  2P%  55.6%");
W(" FTA   21.5  |  FTM  16.7 |  FT%  77.5%");
W(" ORB    9.5  |  DRB  32.6 |  TRB  42.1");
W(" AST   26.5  |  STL   7.4 |  BLK   4.9 |  TOV 13.9");
W(" Dunks: ~6-8 per team per game");
W("══════════════════════════════════════════════════════════════════");

WH("SET 1: BASELINES");
RunScenario("All-50 Baseline (should ≈ NBA avg)",
    MT("Home", "HME", "#4af", 100),
    MT("Away", "AWY", "#f84", 100));
RunScenario("Pace 97",
    MT("Home", "HME", "#4af", 97),
    MT("Away", "AWY", "#f84", 97));
RunScenario("Pace 104",
    MT("Home", "HME", "#4af", 104),
    MT("Away", "AWY", "#f84", 104));

WH("SET 2: THREE-POINT SHOOTING");
RunScenario("High 3PT=80  (should ~40+ 3PA)",
    MT("Home", "HME", "#4af", 100, each: c => c.ThreePoint = 80),
    MT("Away", "AWY", "#f84", 100, each: c => c.ThreePoint = 80));
RunScenario("Low 3PT=20  (should ~15-20 3PA)",
    MT("Home", "HME", "#4af", 100, each: c => c.ThreePoint = 20),
    MT("Away", "AWY", "#f84", 100, each: c => c.ThreePoint = 20));
RunScenario("3PT=85/IQ=75 vs 3PT=25 (big split expected)",
    MT("Home", "3PT", "#4af", 100, each: c => { c.ThreePoint = 85; c.oBBIQ = 75; c.dBBIQ = 75; }),
    MT("Away", "LOW", "#f84", 100, each: c => c.ThreePoint = 25));

WH("SET 3: INTERIOR / DUNKS");
RunScenario("Elite Bigs (Inside=85, Dunks=85, Jump=85, H=80) — ~10-12 dunks",
    MT("Home", "BIG", "#4af", 100,
        each: c => { c.Inside = 85; c.Dunks = 85; c.Jumping = 85; c.Height = 80; c.Strength = 80; }),
    MT("Away", "AVG", "#f84", 100));
RunScenario("Non-Dunkers (Dunks=15, Jump=30, H=35) — ~1-2 dunks",
    MT("Home", "SML", "#4af", 100,
        each: c => { c.Dunks = 15; c.Jumping = 30; c.Height = 35; }),
    MT("Away", "AVG", "#f84", 100));
RunScenario("Contact Dunkers (Dunks=80, Str=90, Jump=70)",
    MT("Home", "PWR", "#4af", 100,
        each: c => { c.Dunks = 80; c.Strength = 90; c.Jumping = 70; c.Height = 75; }),
    MT("Away", "AVG", "#f84", 100));

WH("SET 4: DEFENSE");
RunScenario("Elite PerimDef=85 vs All-50 — opp 3P% should drop",
    MT("Home", "DEF", "#4af", 100, each: c => c.PerimeterDefense = 85),
    MT("Away", "OFF", "#f84", 100));
RunScenario("Elite IntDef=85 vs All-50 — opp inside% should drop",
    MT("Home", "DEF", "#4af", 100, each: c => c.InteriorDefense = 85),
    MT("Away", "OFF", "#f84", 100));
RunScenario("Elite Total D (both=85) vs All-50 — away ~8-10 less pts",
    MT("Home", "DEF", "#4af", 100,
        each: c => { c.PerimeterDefense = 85; c.InteriorDefense = 85; }),
    MT("Away", "OFF", "#f84", 100));

WH("SET 5: SKILLS");
RunScenario("High IQ+Drib=80 — fewer TOs, better decisions",
    MT("Home", "IQ+", "#4af", 100, each: c => { c.oBBIQ = 80; c.dBBIQ = 80; c.Dribbling = 80; }),
    MT("Away", "AVG", "#f84", 100));
RunScenario("Low IQ+Drib=20 — more TOs",
    MT("Home", "IQ-", "#4af", 100, each: c => { c.oBBIQ = 20; c.dBBIQ = 20; c.Dribbling = 20; }),
    MT("Away", "AVG", "#f84", 100));
RunScenario("Elite Rebounders (OReb=DReb=85)",
    MT("Home", "REB", "#4af", 100, each: c => { c.ReboundingOff = 85; c.ReboundingDef = 85; }),
    MT("Away", "AVG", "#f84", 100));

WH("SET 6: FREE THROWS");
RunScenario("High FT drivers (FT=85, Inside=75, Spd=75, Drib=75)",
    MT("Home", "FT+", "#4af", 100,
        each: c => { c.FreeThrow = 85; c.Inside = 75; c.Speed = 75; c.Dribbling = 75; }),
    MT("Away", "AVG", "#f84", 100));

WH("SET 7: COACHING");
RunScenario("Coach OffRat 95 vs 5 — target ~6 pt differential",
    MT("Home", "COA", "#4af", 100, coach: new Coach { Name = "HC Elite", OffensiveRating = 95, DefensiveRating = 60 }),
    MT("Away", "COB", "#f84", 100, coach: new Coach { Name = "HC Weak",  OffensiveRating = 5,  DefensiveRating = 60 }));
RunScenario("Pace&Space coach vs GritAndGrind coach",
    MT("Home", "P&S", "#4af", 100, coach: new Coach { Name = "P&S Coach", OffStyle = OffensiveStyle.PaceAndSpace }),
    MT("Away", "GnG", "#f84", 100, coach: new Coach { Name = "GnG Coach", OffStyle = OffensiveStyle.GritAndGrind }));

WH("SET 8: RATING EXTREMES");
RunScenario("All-95 vs All-50",
    MT("Elite", "ELT", "#4af", 100, all: 95),
    MT("Average", "AVG", "#f84", 100, all: 50));
RunScenario("All-95 vs All-5",
    MT("Elite", "ELT", "#4af", 100, all: 95),
    MT("Awful", "AWF", "#f84", 100, all: 5));

// ── Season Sim with Real NBA Rosters ─────────────────────────────────────────
W("");
W("══════════════════════════════════════════════════════════════════");
W(" SEASON SIM — REAL NBA ROSTERS (1,230 games)");
W("══════════════════════════════════════════════════════════════════");

// Find the BasketballSim data directory relative to the running assembly.
// The calibration binary lives under BasketballSim.Calibration/bin/...,
// and the data files live under BasketballSim/Data/.
var asmDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
var repoRoot = asmDir;
// Walk up until we find the solution root (contains both project folders)
for (int i = 0; i < 8; i++)
{
    if (Directory.Exists(Path.Combine(repoRoot, "BasketballSim", "Data"))) break;
    repoRoot = Path.GetDirectoryName(repoRoot)!;
}
var dataDir      = Path.Combine(repoRoot, "BasketballSim", "Data");
var cacheJsonPath = Path.Combine(dataDir, "nba2k_cache.json");
var rosterJsonPath = Path.Combine(dataDir, "nba_roster.json");

if (!File.Exists(cacheJsonPath) || !File.Exists(rosterJsonPath))
{
    W($"⚠  Data files not found under: {dataDir}");
    W("   Skipping season sim. Run the app and fetch rosters first.");
}
else
{
    W($"   Loading 2K cache: {cacheJsonPath}");
    var players2k = Nba2kCacheService.LoadFromPath(cacheJsonPath);
    var lookup    = players2k.ToDictionary(p => EspnTeamFactory.NormalizeName(p.Name), p => p);
    W($"   Players in cache: {players2k.Count}");

    // Parse nba_roster.json (same structure as NbaRosterService.LoadTeams)
    using var rosterDoc = JsonDocument.Parse(File.ReadAllText(rosterJsonPath));
    var teamsArr = rosterDoc.RootElement.GetProperty("teams");

    var teamsByName = new Dictionary<string, Team>(StringComparer.OrdinalIgnoreCase);

    foreach (var t in teamsArr.EnumerateArray())
    {
        var name      = t.GetString("name")!;
        var abbr      = t.GetString("abbreviation")!;
        var primary   = t.GetString("primaryColor") ?? "#888";
        var secondary = t.GetString("secondaryColor") ?? "#888";
        var division  = t.GetString("division") ?? "";
        var conf      = t.GetString("conference") ?? "";

        var espnPlayers = new List<EspnPlayer>();
        if (t.TryGetProperty("players", out var playersArr))
        {
            foreach (var p in playersArr.EnumerateArray())
            {
                var pName    = p.GetString("name") ?? "Unknown";
                var posGroup = p.GetString("espnPosition") ?? "G";
                var hRating  = p.TryGetProperty("heightRating", out var hr) && hr.TryGetInt32(out int h) ? h : 50;
                var pos = posGroup == "C" ? Position.C : posGroup == "F" ? Position.SF : Position.PG;
                espnPlayers.Add(new EspnPlayer(pName, 0, pos, hRating, 25));
            }
        }

        var configs = EspnTeamFactory.BuildRoster(espnPlayers, lookup, name);
        var team    = EspnTeamFactory.BuildTeam(
            configs, name, abbr, primary,
            coach: CoachFactory.GetCoach(name),
            secondaryColor: secondary, division: division, conference: conf);
        teamsByName[name] = team;
    }

    W($"   Teams loaded: {teamsByName.Count}");
    W("   Simulating 1,230 games...");

    int lastPct = -1;
    var progress = new Progress<(int current, int total)>(p =>
    {
        int pct = p.current * 100 / p.total;
        if (pct / 10 > lastPct / 10)
        {
            lastPct = pct;
            Console.Write($"\r   {p.current}/{p.total} games ({pct}%)   ");
        }
    });

    var schedSvc = new SeasonScheduleService();
    var seasonResult = schedSvc.SimulateSeason(teamsByName, progress);
    Console.WriteLine();

    // ── Print league averages ──────────────────────────────────────────────────
    var ts = seasonResult.TeamStats;
    double nTeams = ts.Count;

    double leaguePpg    = ts.Average(t2 => t2.Ppg);
    double leagueFgaPg  = ts.Average(t2 => t2.Fga);
    double leagueFgmPg  = ts.Average(t2 => t2.Fgm);
    double leagueFgPct  = seasonResult.LeagueFgPct * 100;
    double leagueTpaPg  = ts.Average(t2 => t2.Tpa);
    double leagueTpmPg  = ts.Average(t2 => t2.Tpm);
    double leagueTpPct  = seasonResult.LeagueTpPct * 100;
    double leagueFtaPg  = ts.Average(t2 => t2.Fta);
    double leagueFtmPg  = ts.Average(t2 => t2.Ftm);
    double leagueFtPct  = seasonResult.LeagueFtPct * 100;
    double leagueOrebPg = ts.Average(t2 => t2.Oreb);
    double leagueDrebPg = ts.Average(t2 => t2.Dreb);
    double leagueAstPg  = ts.Average(t2 => t2.Ast);
    double leagueStlPg  = ts.Average(t2 => t2.Stl);
    double leagueBlkPg  = ts.Average(t2 => t2.Blk);
    double leagueTovPg  = ts.Average(t2 => t2.Tov);
    double avg2PA       = leagueFgaPg - leagueTpaPg;
    double avg2PM       = leagueFgmPg - leagueTpmPg;
    double avg2PPct     = avg2PA > 0 ? avg2PM / avg2PA * 100 : 0;

    W("");
    W("  LEAGUE AVG (per team per game)");
    W($"  {"Stat",-8} {"Sim",8} {"Target",8} {"Delta",8}");
    W($"  {"─────────────────────────────────────────"}");
    Stat("PTS",    leaguePpg,   114.6);
    Stat("FGA",    leagueFgaPg,  88.7);
    Stat("FGM",    leagueFgmPg,  42.9);
    StatPct("FG%", leagueFgPct,  48.4);
    Stat("3PA",    leagueTpaPg,  35.1);
    Stat("3PM",    leagueTpmPg,  13.1);
    StatPct("3P%", leagueTpPct,  37.3);
    Stat("2PA",    avg2PA,       53.6);
    Stat("2PM",    avg2PM,       29.8);
    StatPct("2P%", avg2PPct,     55.6);
    Stat("FTA",    leagueFtaPg,  21.5);
    Stat("FTM",    leagueFtmPg,  16.7);
    StatPct("FT%", leagueFtPct,  77.5);
    Stat("ORB",    leagueOrebPg,  9.5);
    Stat("DRB",    leagueDrebPg, 32.6);
    Stat("AST",    leagueAstPg,  26.5);
    Stat("STL",    leagueStlPg,   7.4);
    Stat("BLK",    leagueBlkPg,   4.9);
    Stat("TOV",    leagueTovPg,  13.9);
    W("");
    W($"  Avg possessions/team/game: {seasonResult.AvgPossessionsPerTeam:F1}  (NBA ~100)");
    W($"  Avg possession length:     {seasonResult.AvgPossessionLengthSeconds:F1}s  (NBA ~14s)");
    W($"  Avg passes/possession:     {seasonResult.AvgPassesPerPossession:F1}");

    // Top 10 scorers
    W("");
    W("  TOP 10 SCORERS");
    W($"  {"#",-3} {"Name",-22} {"Team",4} {"GP",4} {"PPG",6} {"FTA",6} {"MPG",6}");
    foreach (var (ps, idx) in seasonResult.PlayerStats.Take(10).Select((p, i) => (p, i)))
        W($"  {idx+1,-3} {ps.Name,-22} {ps.TeamAbbr,4} {ps.GP,4} {ps.Ppg,6:F1} {ps.Fta,6:F1} {ps.Mpg,6:F1}");

    // Top 5 rebounders
    W("");
    W("  TOP 10 REBOUNDERS");
    W($"  {"#",-3} {"Name",-22} {"Team",4} {"RPG",6} {"OPG",6} {"DPG",6}");
    foreach (var (ps, idx) in seasonResult.PlayerStats.OrderByDescending(p => p.Rpg).Take(10).Select((p, i) => (p, i)))
        W($"  {idx+1,-3} {ps.Name,-22} {ps.TeamAbbr,4} {ps.Rpg,6:F1} {ps.Opg,6:F1} {ps.Dpg,6:F1}");

    // Top 5 blockers
    W("");
    W("  TOP 10 BLOCKERS");
    W($"  {"#",-3} {"Name",-22} {"Team",4} {"BPG",6}");
    foreach (var (ps, idx) in seasonResult.PlayerStats.OrderByDescending(p => p.Bpg).Take(10).Select((p, i) => (p, i)))
        W($"  {idx+1,-3} {ps.Name,-22} {ps.TeamAbbr,4} {ps.Bpg,6:F2}");

    // Top 10 assist leaders
    W("");
    W("  TOP 10 ASSIST LEADERS");
    W($"  {"#",-3} {"Name",-22} {"Team",4} {"APG",6} {"PPG",6} {"MPG",6}");
    foreach (var (ps, idx) in seasonResult.PlayerStats.OrderByDescending(p => p.Apg).Take(10).Select((p, i) => (p, i)))
        W($"  {idx+1,-3} {ps.Name,-22} {ps.TeamAbbr,4} {ps.Apg,6:F1} {ps.Ppg,6:F1} {ps.Mpg,6:F1}");

    // Top 10 USG% leaders
    W("");
    W("  TOP 10 USG% LEADERS  (target: peak ~32%, #10 ~22%)");
    W($"  {"#",-3} {"Name",-22} {"Team",4} {"USG%",6} {"FGA",6} {"MPG",6}");
    var usgPlayers = seasonResult.PlayerStats
        .Where(p => p.GP >= 15 && p.Mpg >= 15 && p.UsgPct > 0)
        .OrderByDescending(p => p.UsgPct)
        .Take(15)
        .ToList();
    foreach (var (ps, idx) in usgPlayers.Take(10).Select((p, i) => (p, i)))
        W($"  {idx+1,-3} {ps.Name,-22} {ps.TeamAbbr,4} {ps.UsgPct,5:F1}% {ps.Fga,6:F1} {ps.Mpg,6:F1}");
    if (usgPlayers.Count >= 10)
        W($"  #10 USG%: {usgPlayers[9].UsgPct:F1}%  (target ~22%)");

    // ── League shot-type split ─────────────────────────────────────────────────
    {
        double numTeamGames = ts.Sum(t2 => t2.GP);  // 30 teams × 82 = 2,460
        double lInsideAtt = seasonResult.PlayerStats.Sum(p => p.TotalInsideAtt);
        double lInsideMade= seasonResult.PlayerStats.Sum(p => p.TotalInsideMade);
        double lMidAtt    = seasonResult.PlayerStats.Sum(p => p.TotalMidAtt);
        double lMidMade   = seasonResult.PlayerStats.Sum(p => p.TotalMidMade);
        double lThreeAtt  = ts.Sum(t2 => t2.TotalThreeAtt);
        double lThreeMade = ts.Sum(t2 => t2.TotalThreeMade);
        double lFga       = ts.Sum(t2 => t2.TotalFGA);

        double iApg = lInsideAtt / numTeamGames;
        double mApg = lMidAtt    / numTeamGames;
        double tApg = lThreeAtt  / numTeamGames;
        double iPct = lFga > 0 ? lInsideAtt / lFga * 100 : 0;
        double mPct = lFga > 0 ? lMidAtt    / lFga * 100 : 0;
        double tPct = lFga > 0 ? lThreeAtt  / lFga * 100 : 0;
        double iMk  = lInsideAtt > 0 ? lInsideMade / lInsideAtt * 100 : 0;
        double mMk  = lMidAtt    > 0 ? lMidMade    / lMidAtt    * 100 : 0;
        double tMk  = lThreeAtt  > 0 ? lThreeMade  / lThreeAtt  * 100 : 0;

        W("");
        W("  LEAGUE SHOT TYPE SPLIT (per team per game)");
        W($"  {"Type",-10} {"Att/g",6} {"% FGA",7} {"FG%",7}  {"Target Att",12} {"Target %FGA",13}");
        W($"  {"─────────────────────────────────────────────────────────────"}");
        W($"  {"Inside",-10} {iApg,6:F1} {iPct,6:F1}% {iMk,6:F1}%  {"~32-33",12} {"~37%",13}");
        W($"  {"Mid",-10} {mApg,6:F1} {mPct,6:F1}% {mMk,6:F1}%  {"~20-21",12} {"~23%",13}");
        W($"  {"3PT",-10} {tApg,6:F1} {tPct,6:F1}% {tMk,6:F1}%  {"~35.1",12} {"~39.6%",13}");
    }

    // ── Per-player shot profiles ───────────────────────────────────────────────
    {
        var targets = new[]
        {
            "Mitchell Robinson", "Jalen Brunson", "Stephen Curry",
            "Kevin Durant", "Shai Gilgeous-Alexander", "Kyrie Irving",
            "Joel Embiid", "Nikola Jokic", "Giannis Antetokounmpo", "LeBron James",
            "Karl-Anthony Towns", "Anthony Davis",
        };

        var lookup2 = seasonResult.PlayerStats
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        W("");
        W("  PLAYER SHOT PROFILES");
        W($"  {"Name",-26} {"Tm",4} {"GP",3} {"PPG",5} {"FGA",5} {"FG%",5} | {"InsAtt",6} {"In%",5} {"InFG%",6} | {"MidAtt",6} {"Mid%",5} {"MFG%",6} | {"3PA",5} {"3P%",5} {"3P%FGA",7}");
        W($"  {"─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────"}");
        foreach (var name in targets)
        {
            if (!lookup2.TryGetValue(name, out var p)) { W($"  {name,-26} (not found)"); continue; }
            double fga    = p.TotalFGA;
            double inPct  = fga > 0 ? p.TotalInsideAtt / fga * 100 : 0;
            double midPct = fga > 0 ? p.TotalMidAtt    / fga * 100 : 0;
            double tpPct  = fga > 0 ? p.TotalThreeAtt  / fga * 100 : 0;
            double inFg   = p.TotalInsideAtt > 0 ? (double)p.TotalInsideMade / p.TotalInsideAtt * 100 : 0;
            double midFg  = p.TotalMidAtt    > 0 ? (double)p.TotalMidMade    / p.TotalMidAtt    * 100 : 0;
            double tpFg   = p.TotalThreeAtt  > 0 ? (double)p.TotalThreeMade  / p.TotalThreeAtt  * 100 : 0;
            W($"  {p.Name,-26} {p.TeamAbbr,4} {p.GP,3} {p.Ppg,5:F1} {p.Fga,5:F1} {p.FgPct*100,4:F0}% | {p.InsideApg,6:F1} {inPct,4:F0}% {inFg,5:F0}%  | {p.MidApg,6:F1} {midPct,4:F0}% {midFg,5:F0}%  | {p.Tpa,5:F1} {tpFg,4:F0}% {tpPct,6:F0}%");
        }

        // also print all players sorted by name for full reference
        W("");
        W("  ALL PLAYERS — SHOT SPLIT (sorted by PPG, qualifying ≥ 15 GP, ≥ 15 MPG)");
        W($"  {"Name",-26} {"Tm",4} {"PPG",5} {"FGA",5} | {"In%",5} {"InFG%",6} | {"Mid%",5} {"MFG%",6} | {"3P%FGA",7} {"3P%",5}");
        W($"  {"─────────────────────────────────────────────────────────────────────────────────────────────────────"}");
        foreach (var p in seasonResult.PlayerStats.Where(p => p.GP >= 15 && p.Mpg >= 15).Take(50))
        {
            double fga    = p.TotalFGA;
            double inPct  = fga > 0 ? p.TotalInsideAtt / fga * 100 : 0;
            double midPct = fga > 0 ? p.TotalMidAtt    / fga * 100 : 0;
            double tpPct  = fga > 0 ? p.TotalThreeAtt  / fga * 100 : 0;
            double inFg   = p.TotalInsideAtt > 0 ? (double)p.TotalInsideMade / p.TotalInsideAtt * 100 : 0;
            double midFg  = p.TotalMidAtt    > 0 ? (double)p.TotalMidMade    / p.TotalMidAtt    * 100 : 0;
            double tpFg   = p.TotalThreeAtt  > 0 ? (double)p.TotalThreeMade  / p.TotalThreeAtt  * 100 : 0;
            W($"  {p.Name,-26} {p.TeamAbbr,4} {p.Ppg,5:F1} {p.Fga,5:F1} | {inPct,4:F0}% {inFg,5:F0}%  | {midPct,4:F0}% {midFg,5:F0}%  | {tpPct,6:F0}% {tpFg,4:F0}%");
        }
    }

    // Save season results JSON
    var seasonOutputPath = Path.Combine(
        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
        "season_results.json");
    var serOptions = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(seasonOutputPath, JsonSerializer.Serialize(new
    {
        simulated_at = seasonResult.SimulatedAt,
        league_averages = new
        {
            ppg = leaguePpg,    fga = leagueFgaPg,  fgm = leagueFgmPg,  fg_pct = leagueFgPct,
            tpa = leagueTpaPg,  tpm = leagueTpmPg,  tp_pct = leagueTpPct,
            fta = leagueFtaPg,  ftm = leagueFtmPg,  ft_pct = leagueFtPct,
            orb = leagueOrebPg, drb = leagueDrebPg, ast = leagueAstPg,
            stl = leagueStlPg,  blk = leagueBlkPg,  tov = leagueTovPg,
        },
        team_standings = seasonResult.TeamStats.Select(t2 => new
        {
            t2.TeamName, t2.Abbreviation, t2.Conference, t2.Division,
            t2.Wins, t2.Losses, pct = t2.Pct,
            ppg = t2.Ppg, papg = t2.Papg,
        }),
        top_scorers = seasonResult.PlayerStats.Take(25).Select(p => new
        {
            p.Name, p.Team, p.TeamAbbr, pos = p.Position.ToString(),
            p.GP, ppg = p.Ppg, rpg = p.Rpg, apg = p.Apg, bpg = p.Bpg, spg = p.Spg,
            fta = p.Fta, mpg = p.Mpg,
            fga = p.Fga, fg_pct = Math.Round(p.FgPct * 100, 1),
            inside_apg = Math.Round(p.InsideApg, 1), inside_pct = Math.Round(p.InsidePct * 100, 1),
            mid_apg    = Math.Round(p.MidApg,    1), mid_pct    = Math.Round(p.MidPct    * 100, 1),
            tpa        = Math.Round(p.Tpa,        1), tp_pct    = Math.Round(p.TpPct     * 100, 1),
        }),
        all_player_shots = seasonResult.PlayerStats
            .Where(p => p.GP >= 15 && p.Mpg >= 15)
            .Take(100)
            .Select(p => new
            {
                p.Name, p.TeamAbbr, pos = p.Position.ToString(),
                p.GP, ppg = p.Ppg, fga = p.Fga, fg_pct = Math.Round(p.FgPct * 100, 1),
                inside_apg = Math.Round(p.InsideApg, 1), inside_pct = Math.Round(p.InsidePct * 100, 1),
                mid_apg    = Math.Round(p.MidApg,    1), mid_pct    = Math.Round(p.MidPct    * 100, 1),
                tpa        = Math.Round(p.Tpa,        1), tp_pct    = Math.Round(p.TpPct     * 100, 1),
            }),
    }, serOptions));
    W($"   Season results saved to: {seasonOutputPath}");
}

W("");
W($"Output written to: {outputPath}");
file.Flush();
Console.WriteLine($"\nDone. Results: {outputPath}");

// ── Local functions ──────────────────────────────────────────────────────────
void W(string s = "") { Console.WriteLine(s); file.WriteLine(s); }
void Stat(string label, double sim, double target)
{
    double delta = sim - target;
    string flag  = Math.Abs(delta) / target > 0.10 ? " ⚠" : " ✅";
    W($"  {label,-8} {sim,8:F1} {target,8:F1} {delta,8:+0.0;-0.0}{flag}");
}
void StatPct(string label, double sim, double target)
{
    double delta = sim - target;
    string flag  = Math.Abs(delta) > 1.5 ? " ⚠" : " ✅";
    W($"  {label,-8} {sim,7:F1}% {target,7:F1}% {delta,8:+0.0;-0.0}{flag}");
}
void WH(string label) { W(""); W($"{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}"); W($" {label}"); }

Team MT(string name, string abbr, string color, double pace,
    int all = 50, Coach? coach = null,
    Action<PlayerConfig>? each = null)
{
    var positions = new[] { Position.PG, Position.SG, Position.SF, Position.PF, Position.C };
    var roster = positions.Select((pos, i) =>
    {
        var cfg = new PlayerConfig
        {
            Name = $"{pos}{i + 1}{abbr}", Team = name, Position = pos,
            Height = all, Strength = all, Speed = all, Jumping = all, Endurance = all,
            Inside = all, Dunks = all, FreeThrow = all, MidRange = all, ThreePoint = all,
            oBBIQ = all, dBBIQ = all, Dribbling = all, Passing = all,
            ReboundingOff = all, ReboundingDef = all,
            PerimeterDefense = all, InteriorDefense = all
        };
        each?.Invoke(cfg);
        return cfg.ToPlayer();
    }).ToList();
    return new Team
    {
        Name = name, Abbreviation = abbr,
        PrimaryColor = color, SecondaryColor = "#888",
        Pace = pace, Coach = coach ?? new Coach { Name = "Staff Coach" },
        Roster = roster
    };
}

void RunScenario(string title, Team home, Team away, int games = 500)
{
    W("");
    W($"── {title}");
    var engine = new GameEngine();
    var ha = new Accum(); var aa = new Accum();
    int homeWins = 0;
    for (int i = 0; i < games; i++)
    {
        var r = engine.SimulateGame(home, away);
        ha.Add(r, home.Name);
        aa.Add(r, away.Name);
        if (r.FinalHomeScore > r.FinalAwayScore) homeWins++;
    }
    W($"   {games} games | Home wins: {homeWins} ({homeWins * 100.0 / games:F1}%)  Avg score: {ha.Pts/(double)games:F1} - {aa.Pts/(double)games:F1}  (diff: {(ha.Pts-aa.Pts)/(double)games:+0.0;-0.0})");
    W($"   HOME: "); ha.Print(s => W("     " + s));
    W($"   AWAY: "); aa.Print(s => W("     " + s));
}

// ── JsonElement helper (mirrors NbaRosterService's file-scoped extension) ─────
static class CalibJsonExt
{
    public static string? GetString(this System.Text.Json.JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString() : null;
}

// ── Accum class (must be after top-level statements) ─────────────────────────
class Accum
{
    public int    Games;
    public double Pts, Fga, Fgm, Pa3, Pm3, Fta, Ftm;
    public double Orb, Drb, Ast, Stl, Blk, Tov;
    public Dictionary<ShotContext, (double Made, double Att)> Ctx = new();

    public void Add(GameResult r, string teamName)
    {
        Games++;
        foreach (var s in r.Stats.Values.Where(s => s.Team == teamName))
        {
            Pts += s.Points;    Fga += s.FGAttempts; Fgm += s.FGMade;
            Pa3 += s.ThreeAttempts; Pm3 += s.ThreeMade;
            Fta += s.FTAttempts;    Ftm += s.FTMade;
            Orb += s.OffRebounds;   Drb += s.DefRebounds;
            Ast += s.Assists; Stl += s.Steals; Blk += s.Blocks; Tov += s.Turnovers;
        }
        foreach (var p in r.Possessions)
        {
            if (p.Team != teamName || p.Context is not { } ctx ||
                p.Event is not (PossessionEvent.ShotMade or PossessionEvent.ShotMissed
                             or PossessionEvent.Blocked  or PossessionEvent.GameWinnerMade))
                continue;
            bool made = p.Event is PossessionEvent.ShotMade or PossessionEvent.GameWinnerMade;
            var cur = Ctx.GetValueOrDefault(ctx);
            Ctx[ctx] = (cur.Made + (made ? 1 : 0), cur.Att + 1);
        }
    }

    static readonly ShotContext[] DunkCtxs =
    [
        ShotContext.AlleyOop, ShotContext.CutDunk,
        ShotContext.TransitionDunk, ShotContext.PickAndRollDunk, ShotContext.ContactDunk,
        ShotContext.Putback
    ];
    static readonly ShotContext[] LayupCtxs =
    [
        ShotContext.DrivingLayup, ShotContext.PickAndRollRoll, ShotContext.CutLayup,
        ShotContext.FastBreakLayup, ShotContext.PostMove, ShotContext.FloaterLayup,
        ShotContext.TipIn
    ];
    static readonly ShotContext[] MidCtxs =
    [
        ShotContext.PullUpMidRange, ShotContext.IsolationMidRange,
        ShotContext.PickAndRollMidRange, ShotContext.FadeawayMidRange,
        ShotContext.PostMoveMidRange, ShotContext.LateClockMidRange
    ];

    public void Print(Action<string> w)
    {
        double g = Math.Max(Games, 1);
        double twoA = Fga - Pa3, twoM = Fgm - Pm3;
        w($"PTS {Pts/g,6:F1} | FGA {Fga/g,5:F1} FGM {Fgm/g,4:F1} FG% {(Fga>0?Fgm/Fga*100:0),4:F1}%");
        w($"3PA {Pa3/g,6:F1} | 3PM {Pm3/g,4:F1}  3P% {(Pa3>0?Pm3/Pa3*100:0),4:F1}%");
        w($"2PA {twoA/g,6:F1} | 2PM {twoM/g,4:F1}  2P% {(twoA>0?twoM/twoA*100:0),4:F1}%");
        w($"FTA {Fta/g,6:F1} | FTM {Ftm/g,4:F1}  FT% {(Fta>0?Ftm/Fta*100:0),4:F1}%");
        w($"ORB {Orb/g,6:F1} | DRB {Drb/g,4:F1}  AST {Ast/g,4:F1}  STL {Stl/g,3:F1}  BLK {Blk/g,3:F1}  TOV {Tov/g,4:F1}");

        double dA = DunkCtxs.Sum(c => Ctx.GetValueOrDefault(c).Att) / g;
        double dM = DunkCtxs.Sum(c => Ctx.GetValueOrDefault(c).Made) / g;
        double lA = LayupCtxs.Sum(c => Ctx.GetValueOrDefault(c).Att) / g;
        double mA = MidCtxs.Sum(c => Ctx.GetValueOrDefault(c).Att) / g;
        w($"SPLIT: Layup {lA:F1} | Dunk {dA:F1} ({(dA>0?dM/dA*100:0):F1}% make) | Mid {mA:F1} | 3PT {Pa3/g:F1}");
        foreach (var ctx in DunkCtxs)
        {
            if (Ctx.TryGetValue(ctx, out var cs) && cs.Att > 0)
                w($"  {ctx,-20} {cs.Att/g,4:F1} att  {cs.Made/cs.Att*100,5:F1}%");
        }
    }
}
