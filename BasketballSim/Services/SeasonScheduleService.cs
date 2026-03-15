using BasketballSim.Models;
using BasketballSim.Simulation;

namespace BasketballSim.Services;

public class SeasonScheduleService
{
    // ── Team metadata — keyword-based lookup ──────────────────────────────────
    // Keys are unique nickname keywords found in every NBA team's displayName.
    // These never change regardless of what abbreviation ESPN happens to return.
    private static readonly (string Keyword, string Division, string Conference)[] _nicknameMap =
    [
        ("Celtics",      "Atlantic",  "Eastern"), ("Brooklyn",   "Atlantic",  "Eastern"),
        ("Knicks",       "Atlantic",  "Eastern"), ("76ers",      "Atlantic",  "Eastern"),
        ("Raptors",      "Atlantic",  "Eastern"),
        ("Bulls",        "Central",   "Eastern"), ("Cavaliers",  "Central",   "Eastern"),
        ("Pistons",      "Central",   "Eastern"), ("Pacers",     "Central",   "Eastern"),
        ("Bucks",        "Central",   "Eastern"),
        ("Hawks",        "Southeast", "Eastern"), ("Hornets",    "Southeast", "Eastern"),
        ("Heat",         "Southeast", "Eastern"), ("Magic",      "Southeast", "Eastern"),
        ("Wizards",      "Southeast", "Eastern"),
        ("Nuggets",      "Northwest", "Western"), ("Timberwolves","Northwest","Western"),
        ("Thunder",      "Northwest", "Western"), ("Trail Blazers","Northwest","Western"),
        ("Jazz",         "Northwest", "Western"),
        ("Warriors",     "Pacific",   "Western"), ("Clippers",   "Pacific",   "Western"),
        ("Lakers",       "Pacific",   "Western"), ("Suns",       "Pacific",   "Western"),
        ("Kings",        "Pacific",   "Western"),
        ("Mavericks",    "Southwest", "Western"), ("Rockets",    "Southwest", "Western"),
        ("Grizzlies",    "Southwest", "Western"), ("Pelicans",   "Southwest", "Western"),
        ("Spurs",        "Southwest", "Western"),
    ];

    public static (string Division, string Conference) GetDivision(string teamName)
    {
        foreach (var (kw, div, conf) in _nicknameMap)
            if (teamName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return (div, conf);
        return ("", "");
    }

    // Division order matters for the circulant schedule pattern.
    private static readonly string[] EasternDivisionOrder = ["Atlantic", "Central", "Southeast"];
    private static readonly string[] WesternDivisionOrder = ["Northwest", "Pacific", "Southwest"];

    // ── Schedule generation ───────────────────────────────────────────────────
    /// <summary>
    /// Groups the loaded teams by division (via their abbreviation) and generates
    /// the 1,230-game schedule using the actual team names — no hardcoded names.
    /// Verified: 82 games / 41H / 41A per team.
    /// </summary>
    public (List<(string Home, string Away)> Games, List<string> Warnings)
        GenerateSchedule(IReadOnlyDictionary<string, Team> teamsByName, int? randomSeed = null)
    {
        // Group teams into conference → division → [name, name, ...]
        var buckets = new Dictionary<string, Dictionary<string, List<string>>>();
        foreach (var (name, team) in teamsByName)
        {
            var (div, conf) = GetDivision(name);
            if (div == "") continue;
            if (!buckets.TryGetValue(conf, out var divMap))
                buckets[conf] = divMap = new();
            if (!divMap.TryGetValue(div, out var list))
                divMap[div] = list = [];
            list.Add(name);
        }

        var warnings = new List<string>();

        string[][] GetDivArrays(string conf, string[] divOrder)
        {
            var arrays = new string[3][];
            for (int d = 0; d < 3; d++)
            {
                var divName = divOrder[d];
                var teams = buckets.TryGetValue(conf, out var dm) && dm.TryGetValue(divName, out var t) ? t : [];
                // Take exactly 5 — ESPN sometimes returns extra stubs beyond the 30 NBA teams
                arrays[d] = [.. teams.Take(5)];
            }
            return arrays;
        }

        var east = GetDivArrays("Eastern", EasternDivisionOrder);
        var west = GetDivArrays("Western", WesternDivisionOrder);

        var games = BuildGames(east, west);

        var rng = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        for (int i = games.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (games[i], games[j]) = (games[j], games[i]);
        }

        return (games, warnings);
    }

    private static List<(string Home, string Away)> BuildGames(string[][] east, string[][] west)
    {
        var games = new List<(string Home, string Away)>(1230);

        // 1. Intra-division: 4 games per pair (2H + 2A)
        foreach (var div in east.Concat(west))
        {
            if (div.Length < 2) continue;
            for (int i = 0; i < div.Length; i++)
            for (int j = i + 1; j < div.Length; j++)
            {
                games.Add((div[i], div[j])); games.Add((div[i], div[j]));
                games.Add((div[j], div[i])); games.Add((div[j], div[i]));
            }
        }

        // 2. In-conference out-of-division (circulant pattern, verified 41H/41A)
        foreach (var confDivs in new[] { east, west })
        {
            for (int d1 = 0; d1 < confDivs.Length; d1++)
            for (int d2 = d1 + 1; d2 < confDivs.Length; d2++)
            {
                var A = confDivs[d1];
                var B = confDivs[d2];
                int n = Math.Min(A.Length, B.Length);

                for (int i = 0; i < n; i++)
                {
                    for (int k = 0; k <= 2; k++)       // 4-game: 2H + 2A
                    {
                        int j = (i + k) % n;
                        games.Add((A[i], B[j])); games.Add((A[i], B[j]));
                        games.Add((B[j], A[i])); games.Add((B[j], A[i]));
                    }
                    {   int j = (i + 3) % n;            // 3-game: A gets 2H
                        games.Add((A[i], B[j])); games.Add((A[i], B[j]));
                        games.Add((B[j], A[i]));
                    }
                    {   int j = (i + 4) % n;            // 3-game: B gets 2H
                        games.Add((B[j], A[i])); games.Add((B[j], A[i]));
                        games.Add((A[i], B[j]));
                    }
                }
            }
        }

        // 3. Inter-conference: 1H + 1A per pair
        foreach (var eDiv in east)
        foreach (var eName in eDiv)
        foreach (var wDiv in west)
        foreach (var wName in wDiv)
        {
            games.Add((eName, wName));
            games.Add((wName, eName));
        }

        return games;
    }

    // ── Season simulation ─────────────────────────────────────────────────────
    public SeasonResult SimulateSeason(
        IReadOnlyDictionary<string, Team> teamsByName,
        IProgress<(int current, int total)>? progress = null)
    {
        var (schedule, _) = GenerateSchedule(teamsByName);
        var engine = new GameEngine();

        var standings = new Dictionary<string, TeamSeasonStats>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, team) in teamsByName)
        {
            standings[name] = new TeamSeasonStats
            {
                TeamName     = name,
                Abbreviation = team.Abbreviation,
                Division     = team.Division,
                Conference   = team.Conference,
                PrimaryColor = team.PrimaryColor,
            };
        }

        var playerAgg  = new Dictionary<string, PlayerSeasonStats>(StringComparer.OrdinalIgnoreCase);
        var gameRecords = new List<SeasonGameRecord>(1230);
        long totalPossessions       = 0;
        long totalPossessionSeconds = 0;
        long totalPasses            = 0;
        int total = schedule.Count;
        int count = 0;

        foreach (var (homeName, awayName) in schedule)
        {
            if (!teamsByName.TryGetValue(homeName, out var homeTeam) ||
                !teamsByName.TryGetValue(awayName, out var awayTeam))
                continue;

            var result    = engine.SimulateGame(homeTeam, awayTeam);
            int homeScore = result.FinalHomeScore;
            int awayScore = result.FinalAwayScore;
            bool homeWon  = homeScore > awayScore;

            gameRecords.Add(new SeasonGameRecord(
                homeName, awayName,
                homeTeam.Abbreviation, awayTeam.Abbreviation,
                homeScore, awayScore));

            // Count possession chains: SecondsElapsed > 0 marks the first event of each chain.
            // ORBs extend the chain (continue the while loop in SimulatePossessionChain),
            // so they don't produce a new chain entry — this correctly treats ORBs as
            // continuing the same possession.
            foreach (var poss in result.Possessions)
            {
                if (poss.SecondsElapsed > 0)
                {
                    totalPossessions++;
                    totalPossessionSeconds += poss.SecondsElapsed;
                    totalPasses            += poss.PassCount;

                    // Per-team possession tracking (for aPER pace adjustment)
                    if (standings.TryGetValue(poss.Team, out var possTeamSt))
                        possTeamSt.TotalPossessions++;
                }
            }

            if (!standings.TryGetValue(homeName, out var homeSt) ||
                !standings.TryGetValue(awayName, out var awaySt))
                continue;

            homeSt.Wins += homeWon ? 1 : 0; homeSt.Losses += homeWon ? 0 : 1;
            homeSt.HomeWins += homeWon ? 1 : 0; homeSt.HomeLosses += homeWon ? 0 : 1;
            awaySt.Wins += homeWon ? 0 : 1; awaySt.Losses += homeWon ? 1 : 0;
            awaySt.AwayWins += homeWon ? 0 : 1; awaySt.AwayLosses += homeWon ? 1 : 0;

            if (homeSt.Conference == awaySt.Conference)
            {
                homeSt.ConfWins += homeWon ? 1 : 0; homeSt.ConfLosses += homeWon ? 0 : 1;
                awaySt.ConfWins += homeWon ? 0 : 1; awaySt.ConfLosses += homeWon ? 1 : 0;
                if (homeSt.Division == awaySt.Division)
                {
                    homeSt.DivWins += homeWon ? 1 : 0; homeSt.DivLosses += homeWon ? 0 : 1;
                    awaySt.DivWins += homeWon ? 0 : 1; awaySt.DivLosses += homeWon ? 1 : 0;
                }
            }

            homeSt.TotalPtsFor += homeScore; homeSt.TotalPtsAgainst += awayScore;
            awaySt.TotalPtsFor += awayScore; awaySt.TotalPtsAgainst += homeScore;

            foreach (var ps in result.Stats.Values)
            {
                TeamSeasonStats? st = null;
                if (ps.Team == homeTeam.Name)      st = homeSt;
                else if (ps.Team == awayTeam.Name) st = awaySt;
                if (st is null) continue;

                st.TotalFGM += ps.FGMade;          st.TotalFGA += ps.FGAttempts;
                st.TotalThreeMade += ps.ThreeMade; st.TotalThreeAtt += ps.ThreeAttempts;
                st.TotalFTM += ps.FTMade;          st.TotalFTA += ps.FTAttempts;
                st.TotalOREB += ps.OffRebounds;    st.TotalDREB += ps.DefRebounds;
                st.TotalAST += ps.Assists;         st.TotalSTL += ps.Steals;
                st.TotalBLK += ps.Blocks;          st.TotalTOV += ps.Turnovers;

                // Player aggregation
                var key = $"{ps.Name}|{ps.Team}";
                if (!playerAgg.TryGetValue(key, out var pagg))
                    playerAgg[key] = pagg = new PlayerSeasonStats
                    {
                        Name = ps.Name, Team = ps.Team,
                        TeamAbbr = st.Abbreviation, Position = ps.Position,
                    };
                pagg.GP++;
                pagg.TotalPTS     += ps.Points;
                pagg.TotalOREB    += ps.OffRebounds;
                pagg.TotalDREB    += ps.DefRebounds;
                pagg.TotalAST     += ps.Assists;
                pagg.TotalSTL     += ps.Steals;
                pagg.TotalBLK     += ps.Blocks;
                pagg.TotalTOV     += ps.Turnovers;
                pagg.TotalFGM     += ps.FGMade;
                pagg.TotalFGA     += ps.FGAttempts;
                pagg.TotalThreeMade += ps.ThreeMade;
                pagg.TotalThreeAtt  += ps.ThreeAttempts;
                pagg.TotalFTM     += ps.FTMade;
                pagg.TotalFTA     += ps.FTAttempts;
                pagg.TotalMIN     += ps.MinutesPlayed;
                pagg.TotalPlusMinus          += ps.PlusMinus;
                pagg.TotalTouchesTotal       += ps.TouchesTotal;
                pagg.TotalShotAttempts       += ps.ShotAttempts;
                pagg.TotalTeamTouchesOnCourt += ps.TeamTouchesOnCourt;
                pagg.TotalTeamFGAOnCourt     += ps.TeamFGAOnCourt;
                pagg.TotalInsideMade         += ps.InsideMade;
                pagg.TotalInsideAtt          += ps.InsideAtt;
                pagg.TotalMidMade            += ps.MidRangeMade;
                pagg.TotalMidAtt             += ps.MidRangeAtt;
                pagg.TotalDefFGM             += ps.DefFGMade;
                pagg.TotalDefFGA             += ps.DefFGAttempts;
            }

            count++;
            if (count % 50 == 0 || count == total)
                progress?.Report((count, total));
        }

        var seasonResult = new SeasonResult
        {
            TeamStats              = [.. standings.Values.OrderByDescending(s => s.Wins)],
            PlayerStats            = [.. playerAgg.Values.OrderByDescending(p => p.Ppg)],
            Games                  = gameRecords,
            SimulatedAt            = DateTime.Now,
            TotalPossessions       = totalPossessions,
            TotalPossessionSeconds = totalPossessionSeconds,
            TotalPasses            = totalPasses,
        };

        ComputeAdvancedStats(seasonResult);
        return seasonResult;
    }

    private static void ComputeAdvancedStats(SeasonResult result)
    {
        var teamMap = result.TeamStats.ToDictionary(t => t.TeamName, StringComparer.OrdinalIgnoreCase);
        var players = result.PlayerStats.Where(p => p.TotalMIN > 0 && p.GP > 0).ToList();
        if (players.Count == 0 || teamMap.Count == 0) return;

        // ── League averages (per team per game) ───────────────────────────────
        double lgPPG  = result.LeaguePpg;
        double lgPapg = result.LeaguePapg;
        double lgFGM  = result.LeagueFgmPg;
        double lgFGA  = result.LeagueFgaPg;
        double lgFTM  = result.LeagueFtmPg;
        double lgFTA  = result.LeagueFtaPg;
        double lgORB  = result.LeagueOrbPg;
        double lgDRB  = result.LeagueDrbPg;
        double lgAST  = result.LeagueAstPg;
        double lgTOV  = result.LeagueTovPg;
        double lgTRB  = lgORB + lgDRB;

        // Value of a possession (pts scored per possession attempted)
        double lgPoss  = lgFGA - lgORB + lgTOV + 0.44 * lgFTA;
        double VOP     = lgPoss > 0 ? lgPPG / lgPoss : 1.0;

        // Defensive rebound rate (fraction of misses recovered by defense)
        double DRBpct  = lgTRB > 0 ? lgDRB / lgTRB : 0.74;

        // PER factor — controls how much of a FGM is attributed to the scorer vs. the assister
        double factor  = (lgFGM > 0 && lgFTM > 0)
            ? (2.0/3.0) - (0.5 * (lgAST / lgFGM)) / (2.0 * (lgFGM / lgFTM))
            : 0.0;

        // Marginal points needed to earn one additional win
        double MPW     = 0.32 * lgPPG;

        // Defensive rebound stop factor (used in stops formula)
        double drbStop = 1.0 - 1.07 * DRBpct;

        // ── Pass 1: aPER + OWS ───────────────────────────────────────────────
        // aPER = pace-adjusted PER: uPER × (lgPace / tmPace), then scaled to avg=15.
        double lgPace = result.AvgPossessionsPerTeam;  // league-wide avg possessions per team per game
        var uPERMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in players)
        {
            if (!teamMap.TryGetValue(p.Team, out var team)) continue;

            double tmASTrat = team.TotalFGM > 0
                ? (double)team.TotalAST / team.TotalFGM
                : lgAST / lgFGM;

            // ── uPER (per-minute, before pace adjustment) ─────────────────────
            // Standard NBA formula: weighted sum of positive contributions minus
            // the cost of misses and turnovers, divided by minutes played.
            double uPER = p.Mpg > 0
                ? (1.0 / p.Mpg) * (
                      p.Tpm
                    + (2.0/3.0)  * p.Apg
                    + (2.0 - factor * tmASTrat) * p.Fgm
                    + p.Ftm * 0.5 * (1.0 + (1.0 - tmASTrat) + (2.0/3.0) * tmASTrat)
                    - VOP          * p.Topg
                    - VOP * DRBpct * (p.Fga - p.Fgm)
                    - VOP * 0.44   * (0.44 + 0.56 * DRBpct) * (p.Fta - p.Ftm)
                    + VOP * (1.0 - DRBpct) * p.Dpg
                    + VOP * DRBpct * p.Opg
                    + VOP          * p.Spg
                    + VOP * DRBpct * p.Bpg)
                : 0;

            // Pace adjustment: players on fast teams get fewer possessions per minute,
            // so their raw per-minute production understates efficiency — and vice versa.
            double tmPace  = team.Pace > 0 ? team.Pace : lgPace;
            double paceAdj = tmPace  > 0   ? lgPace / tmPace : 1.0;
            uPERMap[p.Name + "|" + p.Team] = uPER * paceAdj;

            // ── Offensive Win Shares ───────────────────────────────────────────
            // Scoring contribution = points generated via scoring, assists, ORBs minus TOV cost.
            // Each AST generates ~2/3 of a point of value above baseline.
            // Each ORB extends a possession, worth VOP × (1 - DRBpct).
            double scoringContrib =
                  p.Ppg
                + (2.0/3.0) * p.Apg
                + VOP * (1.0 - DRBpct) * p.Opg
                - VOP * p.Topg;

            // Possessions used per game (FGA + 0.44×FTA + TOV, minus ORB recycling)
            double posUsed = p.Fga + 0.44 * p.Fta + p.Topg - (1.0 - DRBpct) * p.Opg;

            // Marginal offense = contribution above what a replacement player would produce
            // using those same possessions (replacement ≈ 92% of league average efficiency)
            double margOff = scoringContrib - 0.92 * VOP * posUsed;

            p.OWS = margOff * p.GP / MPW;
        }

        // ── Scale PER to league average = 15 ─────────────────────────────────
        double totalMin     = players.Sum(p => p.TotalMIN);
        double weightedUPER = players.Sum(p =>
            uPERMap.GetValueOrDefault(p.Name + "|" + p.Team, 0) * p.TotalMIN);
        double lgUPER   = totalMin > 0 ? weightedUPER / totalMin : 1.0;
        double perScale = lgUPER > 0 ? 15.0 / lgUPER : 1.0;

        foreach (var p in players)
            p.PER = Math.Clamp(uPERMap.GetValueOrDefault(p.Name + "|" + p.Team, 0) * perScale, -5.0, 40.0);

        // ── Pass 2: DWS (proportional per team) ──────────────────────────────
        // Team's defensive win shares budget is based on wins and how far below league
        // average the team's points-allowed is. Distributed to players weighted by
        // (defensive stops × minutes played).
        foreach (var grp in players.GroupBy(p => p.Team, StringComparer.OrdinalIgnoreCase))
        {
            if (!teamMap.TryGetValue(grp.Key, out var team)) continue;

            // Team defensive budget: baseline half of wins, adjusted for defensive quality
            double tmDefAdj      = MPW > 0 ? (lgPapg - team.Papg) / MPW : 0;
            double teamDWSBudget = Math.Max(1.0, team.Wins * 0.45 + tmDefAdj * team.GP * 0.5);

            // Defensive stops per game: STL are clean stops; BLK and DRB contribute partially
            // based on how often they convert into actual defensive possessions
            double DefWeight(PlayerSeasonStats p) =>
                (p.Spg + p.Bpg * DRBpct * drbStop + p.Dpg * drbStop) * p.TotalMIN;

            double totalDefWeight = grp.Sum(DefWeight);
            foreach (var p in grp)
                p.DWS = totalDefWeight > 0
                    ? Math.Max(0, teamDWSBudget * DefWeight(p) / totalDefWeight)
                    : 0;
        }
    }
}
