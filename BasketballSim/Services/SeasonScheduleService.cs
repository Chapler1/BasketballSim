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

    // ── Fatigue constants ─────────────────────────────────────────────────────
    private const double FatigueDrainCoeff    = 0.42;   // per minute played (reduced: equilibrium at SF~85)
    private const double FatigueRecoveryCoeff = 0.45;   // base recovery rate per rest day
    private const double FatigueB2bRecovery   = 3.0;    // flat recovery on B2B (0 rest days)
    // No DNP threshold — fatigue now continuously reduces minutes targets instead of hard-benching

    private static double DrainAmount(double minutesPlayed, int endurance) =>
        minutesPlayed * FatigueDrainCoeff * Math.Max(0.05, 1.5 - endurance / 100.0);

    private static double ApplyRecovery(double sf, int restDays, int endurance)
    {
        if (restDays == 0) return Math.Min(100.0, sf + FatigueB2bRecovery);
        double dailyRate = FatigueRecoveryCoeff * (0.6 + endurance / 100.0 * 0.8);
        return 100.0 - (100.0 - sf) * Math.Pow(1.0 - dailyRate, restDays);
    }

    // ── Season simulation ─────────────────────────────────────────────────────
    public SeasonResult SimulateSeason(
        IReadOnlyDictionary<string, Team> teamsByName,
        IProgress<(int current, int total)>? progress = null,
        int? seed = null,
        bool disableInjuries = false)
    {
        // Build matchup list then assign calendar dates via the greedy scheduler
        var (matchups, _) = GenerateSchedule(teamsByName, randomSeed: seed);
        var scheduledGames = NbaCalendarService.GenerateSchedule(matchups, startYear: 2025);

        var engine = new GameEngine { DisableInjuries = disableInjuries };

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

        // ── Season fatigue tracker (player name → current fatigue 0–100) ──────
        var fatigue = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var enduranceByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in teamsByName.Values)
        foreach (var p in team.Roster)
        {
            fatigue[p.Name]         = 100.0;
            enduranceByName[p.Name] = p.Endurance;
        }

        // ── Season injury RNG (offset from schedule seed to avoid correlation) ─
        var injRng = seed.HasValue ? new Random(seed.Value ^ 0x1A2B3C4D) : new Random();

        var pendingInjuries   = new Dictionary<string, PendingInjury>(StringComparer.OrdinalIgnoreCase);
        var finalizedInjuries = new Dictionary<string, List<InjuryRecord>>(StringComparer.OrdinalIgnoreCase);

        var playerAgg   = new Dictionary<string, PlayerSeasonStats>(StringComparer.OrdinalIgnoreCase);
        var gameRecords = new List<SeasonGameRecord>(1230);
        long totalPossessions       = 0;
        long totalPossessionSeconds = 0;
        long totalPasses            = 0;
        int total = scheduledGames.Count;
        int count = 0;

        foreach (var sg in scheduledGames)
        {
            var homeName = sg.HomeTeamId;
            var awayName = sg.AwayTeamId;

            if (!teamsByName.TryGetValue(homeName, out var homeTeam) ||
                !teamsByName.TryGetValue(awayName, out var awayTeam))
                continue;

            // ── Apply rest-day recovery before game ──────────────────────────
            foreach (var p in homeTeam.Roster)
                fatigue[p.Name] = Math.Clamp(ApplyRecovery(fatigue.GetValueOrDefault(p.Name, 100.0), sg.HomeRestDays, p.Endurance), 0.0, 100.0);
            foreach (var p in awayTeam.Roster)
                fatigue[p.Name] = Math.Clamp(ApplyRecovery(fatigue.GetValueOrDefault(p.Name, 100.0), sg.AwayRestDays, p.Endurance), 0.0, 100.0);

            // ── Tick injury recovery by calendar rest days ────────────────────
            if (!disableInjuries)
            {
                foreach (var p in homeTeam.Roster)
                {
                    if (p.CurrentInjury is null) continue;
                    if (InjuryService.TickRecovery(p.CurrentInjury, sg.HomeRestDays))
                    {
                        FinalizeInjuryRecord(p.Name, DateOnly.FromDateTime(sg.Date), pendingInjuries, finalizedInjuries);
                        p.CurrentInjury = null;
                    }
                }
                foreach (var p in awayTeam.Roster)
                {
                    if (p.CurrentInjury is null) continue;
                    if (InjuryService.TickRecovery(p.CurrentInjury, sg.AwayRestDays))
                    {
                        FinalizeInjuryRecord(p.Name, DateOnly.FromDateTime(sg.Date), pendingInjuries, finalizedInjuries);
                        p.CurrentInjury = null;
                    }
                }
            }

            // ── Determine DNP players ─────────────────────────────────────────
            var dnpThisGame = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // No fatigue-based DNP — fatigue reduces minutes targets instead (RotationManager).
            // Injury-based DNP (skipped when injuries disabled)
            if (!disableInjuries)
            {
                foreach (var (teamId, roster) in new[] { (homeName, homeTeam), (awayName, awayTeam) })
                {
                    var sortedByOverall = roster.Roster
                        .OrderByDescending(RotationManager.ComputeOverall)
                        .ToList();

                    foreach (var p in roster.Roster)
                    {
                        if (p.CurrentInjury is null) continue;

                        if (p.CurrentInjury.Definition.Grade >= 2)
                        {
                            dnpThisGame.Add(p.Name);
                            p.CurrentInjury.IsPlaying = false;
                        }
                        else // Grade 1: threshold-based play-through decision
                        {
                            int rank = sortedByOverall.IndexOf(p) + 1;
                            bool plays = InjuryService.ShouldPlayThroughG1(
                                p, rank, p.CurrentInjury, gameImportance: 0.25, injRng);
                            p.CurrentInjury.IsPlaying = plays;
                            if (!plays) dnpThisGame.Add(p.Name);
                        }
                    }
                }
            }

            // ── Record pre-game fatigue for stats (played games only) ─────────
            var preGameFatigue = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in homeTeam.Roster.Concat(awayTeam.Roster))
                preGameFatigue[p.Name] = fatigue.GetValueOrDefault(p.Name, 100.0);

            var result = engine.SimulateGame(homeTeam, awayTeam,
                startingFatigue: fatigue,
                dnpPlayers: dnpThisGame.Count > 0 ? dnpThisGame : null);
            int homeScore = result.FinalHomeScore;
            int awayScore = result.FinalAwayScore;
            bool homeWon  = homeScore > awayScore;

            gameRecords.Add(new SeasonGameRecord(
                homeName, awayName,
                homeTeam.Abbreviation, awayTeam.Abbreviation,
                homeScore, awayScore,
                sg.Date, sg.HomeRestDays, sg.AwayRestDays));

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
                bool played = ps.MinutesPlayed > 0;
                if (played) pagg.GP++;
                if (played && preGameFatigue.TryGetValue(ps.Name, out double pgf))
                    pagg.TotalFatigueIn += pgf;

                // Injury game tracking
                var rosterPlayer = homeTeam.Roster.Concat(awayTeam.Roster)
                    .FirstOrDefault(p => string.Equals(p.Name, ps.Name, StringComparison.OrdinalIgnoreCase));
                if (rosterPlayer?.CurrentInjury is not null)
                {
                    if (!played && dnpThisGame.Contains(ps.Name))
                    {
                        pagg.GamesInjured++;
                        if (pendingInjuries.TryGetValue(ps.Name, out var pi))
                            pi.GamesMissed++;
                    }
                    else if (played && rosterPlayer.CurrentInjury.IsPlaying)
                        pagg.GamesPlayedThrough++;
                }
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
                pagg.TotalTeamFGMOnCourt     += ps.TeamFGMOnCourt;
                pagg.TotalTeamORebOnCourt    += ps.TeamORebOnCourt;
                pagg.TotalTeamDRebOnCourt    += ps.TeamDRebOnCourt;
                pagg.TotalOppORebOnCourt     += ps.OppORebOnCourt;
                pagg.TotalOppDRebOnCourt     += ps.OppDRebOnCourt;
                pagg.TotalTeamPtsOnCourt     += ps.TeamPtsOnCourt;
                pagg.TotalOppPtsOnCourt      += ps.OppPtsOnCourt;
                pagg.TotalPossessionsOnCourt += ps.PossessionsOnCourt;
            }

            // ── Track GamesRested for DNP players ────────────────────────────
            foreach (var dnpName in dnpThisGame)
            {
                // Find which team this player belongs to
                TeamSeasonStats? dnpSt = null;
                if (homeTeam.Roster.Any(p => p.Name == dnpName)) dnpSt = standings.GetValueOrDefault(homeName);
                else if (awayTeam.Roster.Any(p => p.Name == dnpName)) dnpSt = standings.GetValueOrDefault(awayName);

                if (dnpSt == null) continue;
                var dnpKey = playerAgg.Keys.FirstOrDefault(k => k.StartsWith(dnpName + "|", StringComparison.OrdinalIgnoreCase));
                if (dnpKey != null) playerAgg[dnpKey].GamesRested++;
            }

            // ── Post-game: process new in-game injuries ───────────────────────
            foreach (var ev in result.InjuryEvents)
            {
                var allRosters = homeTeam.Roster.Concat(awayTeam.Roster);
                var injPlayer = allRosters.FirstOrDefault(p => string.Equals(p.Name, ev.PlayerName, StringComparison.OrdinalIgnoreCase));
                if (injPlayer is null) continue;

                // Degrade body-part rating (makes future injuries to this part more likely)
                int curRating = injPlayer.InjuryRatings.GetValueOrDefault(ev.BodyPartKey, 99);
                injPlayer.InjuryRatings[ev.BodyPartKey] =
                    Math.Max(1, curRating - InjuryTables.RatingDegradation(ev.Grade));

                // Roll permanent debuff at moment of injury (Grade 2/3 only)
                Dictionary<string, int>? perm = null;
                if (injPlayer.CurrentInjury is not null)
                {
                    perm = InjuryService.RollPermanentDebuff(injPlayer, injPlayer.CurrentInjury, injRng);
                    if (perm is not null)
                        foreach (var (attrKey, loss) in perm)
                        {
                            injPlayer.PermanentInjuryPenalties.TryGetValue(attrKey, out int existing);
                            injPlayer.PermanentInjuryPenalties[attrKey] = existing + loss;
                        }

                    // Record this injury for end-of-season history
                    pendingInjuries[ev.PlayerName] = new PendingInjury
                    {
                        InjuryName    = ev.InjuryName,
                        BodyPart      = ev.BodyPartDisplay,
                        Grade         = ev.Grade,
                        InjuredDate   = DateOnly.FromDateTime(sg.Date),
                        EstimatedDays = injPlayer.CurrentInjury.Definition.ExpectedDays,
                        ActualDays    = injPlayer.CurrentInjury.DaysRemaining,
                        PermanentPenalties = perm is not null && perm.Count > 0
                            ? new Dictionary<string, int>(perm) : null
                    };
                }
            }

            // ── Drain season fatigue based on minutes played ──────────────────
            foreach (var ps in result.Stats.Values)
            {
                if (ps.MinutesPlayed <= 0) continue;
                int end = enduranceByName.GetValueOrDefault(ps.Name, 50);
                double drain = DrainAmount(ps.MinutesPlayed, end);
                fatigue[ps.Name] = Math.Clamp(fatigue.GetValueOrDefault(ps.Name, 100.0) - drain, 0.0, 100.0);
            }

            count++;
            if (count % 50 == 0 || count == total)
                progress?.Report((count, total));
        }

        // ── Populate InjuryHistory on player season stats ────────────────────
        foreach (var (name, records) in finalizedInjuries)
        {
            var key = playerAgg.Keys.FirstOrDefault(k =>
                k.StartsWith(name + "|", StringComparison.OrdinalIgnoreCase));
            if (key is not null) playerAgg[key].InjuryHistory.AddRange(records);
        }
        foreach (var (name, pi) in pendingInjuries)
        {
            var key = playerAgg.Keys.FirstOrDefault(k =>
                k.StartsWith(name + "|", StringComparison.OrdinalIgnoreCase));
            if (key is null) continue;
            playerAgg[key].InjuryHistory.Add(new InjuryRecord(
                pi.InjuryName, pi.BodyPart, pi.Grade, pi.InjuredDate, null,
                pi.EstimatedDays, pi.ActualDays, pi.GamesMissed,
                pi.PermanentPenalties is not null
                    ? new Dictionary<string, int>(pi.PermanentPenalties) : null));
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

        // Populate TeamPossEventsPg for USG% formula: TmFGA + 0.44*TmFTA + TmTOV per game
        foreach (var p in players)
            if (teamMap.TryGetValue(p.Team, out var tm))
                p.TeamPossEventsPg = tm.Fga + 0.44 * tm.Fta + tm.Tov;

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

    private static void FinalizeInjuryRecord(
        string playerName,
        DateOnly returnedDate,
        Dictionary<string, PendingInjury>  pending,
        Dictionary<string, List<InjuryRecord>> finalized)
    {
        if (!pending.TryGetValue(playerName, out var pi)) return;
        var rec = new InjuryRecord(
            pi.InjuryName, pi.BodyPart, pi.Grade,
            pi.InjuredDate, returnedDate,
            pi.EstimatedDays, pi.ActualDays, pi.GamesMissed,
            pi.PermanentPenalties is not null
                ? new Dictionary<string, int>(pi.PermanentPenalties) : null);
        if (!finalized.TryGetValue(playerName, out var list))
            finalized[playerName] = list = [];
        list.Add(rec);
        pending.Remove(playerName);
    }

    internal sealed class PendingInjury
    {
        public required string   InjuryName           { get; init; }
        public required string   BodyPart              { get; init; }
        public required int      Grade                 { get; init; }
        public required DateOnly InjuredDate           { get; init; }
        public required int      EstimatedDays         { get; init; }
        public required int      ActualDays            { get; init; }
        public          int      GamesMissed           { get; set;  }
        public Dictionary<string, int>? PermanentPenalties { get; set; }
    }

    // ── Playoff simulation ────────────────────────────────────────────────────

    internal record PlayoffTeam(string Name, string Abbr, int Seed);

    internal sealed class PlayoffState
    {
        public required IReadOnlyDictionary<string, Team>      Teams     { get; init; }
        public required GameEngine                              Engine    { get; init; }
        public required Dictionary<string, double>              Fatigue   { get; init; }
        public required Dictionary<string, int>                Endurance { get; init; }
        public required Random                                  InjRng    { get; init; }
        public required Dictionary<string, PendingInjury>      Pending   { get; init; }
        public required Dictionary<string, List<InjuryRecord>> Finalized { get; init; }
        public required Dictionary<string, PlayerSeasonStats>  PlayerAgg { get; init; }
    }

    /// <summary>
    /// Simulates the NBA play-in tournament and full bracket (best-of-7 series).
    /// Requires a completed SeasonResult for standings. Team injury state and
    /// permanent rating degradation carry over from the regular season.
    /// </summary>
    public PlayoffResult SimulatePlayoffs(
        IReadOnlyDictionary<string, Team> teamsByName,
        SeasonResult regularSeason,
        IProgress<(int done, int total)>? progress = null,
        int? seed = null)
    {
        var result   = new PlayoffResult();
        var state    = new PlayoffState
        {
            Teams     = teamsByName,
            Engine    = new GameEngine(),
            Fatigue   = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            Endurance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            InjRng    = seed.HasValue ? new Random(seed.Value ^ unchecked((int)0xBAADF00D)) : new Random(),
            Pending   = new Dictionary<string, PendingInjury>(StringComparer.OrdinalIgnoreCase),
            Finalized = new Dictionary<string, List<InjuryRecord>>(StringComparer.OrdinalIgnoreCase),
            PlayerAgg = new Dictionary<string, PlayerSeasonStats>(StringComparer.OrdinalIgnoreCase),
        };

        foreach (var team in teamsByName.Values)
        foreach (var p in team.Roster)
        {
            state.Fatigue[p.Name]   = 90.0;
            state.Endurance[p.Name] = p.Endurance;
        }

        // Seed top 10 per conference (W → Pct tiebreak)
        var eastTop10 = regularSeason.TeamStats
            .Where(t => t.Conference == "Eastern")
            .OrderByDescending(t => t.Wins).ThenByDescending(t => t.Pct)
            .Take(10).ToList();
        var westTop10 = regularSeason.TeamStats
            .Where(t => t.Conference == "Western")
            .OrderByDescending(t => t.Wins).ThenByDescending(t => t.Pct)
            .Take(10).ToList();

        int doneGames = 0;
        const int totalEst = 83; // typical bracket (6 play-in + ~77 bracket games avg)

        void Report() { doneGames++; progress?.Report((doneGames, totalEst)); }

        // ── Play-in (3 games per conference) ─────────────────────────────────
        PlayoffTeam RunPlayIn(
            List<TeamSeasonStats> conf10,
            List<SeasonGameRecord> playInGames,
            out PlayoffTeam seed7, out PlayoffTeam seed8)
        {
            // Handle degenerate case (< 8 teams)
            if (conf10.Count < 8)
            {
                seed7 = new(conf10.ElementAtOrDefault(6)?.TeamName ?? "", conf10.ElementAtOrDefault(6)?.Abbreviation ?? "", 7);
                seed8 = new(conf10.ElementAtOrDefault(7)?.TeamName ?? "", conf10.ElementAtOrDefault(7)?.Abbreviation ?? "", 8);
                return seed7;
            }

            var t7  = new PlayoffTeam(conf10[6].TeamName, conf10[6].Abbreviation, 7);
            var t8  = new PlayoffTeam(conf10[7].TeamName, conf10[7].Abbreviation, 8);
            var t9  = conf10.Count > 8 ? new PlayoffTeam(conf10[8].TeamName, conf10[8].Abbreviation, 9)  : t8;
            var t10 = conf10.Count > 9 ? new PlayoffTeam(conf10[9].TeamName, conf10[9].Abbreviation, 10) : t9;

            // Game 1: 7 vs 8 (7 hosts)
            bool g1HomeWon = PlayOneGame(t7, t8, 0.55, state, playInGames, 3);
            Report();
            var w1 = g1HomeWon ? t7 : t8;
            var l1 = g1HomeWon ? t8 : t7;

            // Game 2: 9 vs 10 (9 hosts)
            bool g2HomeWon = PlayOneGame(t9, t10, 0.55, state, playInGames, 3);
            Report();
            var w2 = g2HomeWon ? t9 : t10;

            // Game 3: loser of 7/8 hosts winner of 9/10
            bool g3HomeWon = PlayOneGame(l1, w2, 0.70, state, playInGames, 3);
            Report();
            var w3 = g3HomeWon ? l1 : w2;

            seed7 = new(w1.Name, w1.Abbr, 7);
            seed8 = new(w3.Name, w3.Abbr, 8);
            return seed7;
        }

        PlayoffTeam e7, e8, w7, w8;
        RunPlayIn(eastTop10, result.PlayInGames, out e7, out e8);
        RunPlayIn(westTop10, result.PlayInGames, out w7, out w8);

        // Build 8-team bracket per conference
        PlayoffTeam EastSeed(int i) => i < 6
            ? new(eastTop10[i].TeamName, eastTop10[i].Abbreviation, i + 1)
            : (i == 6 ? e7 : e8);
        PlayoffTeam WestSeed(int i) => i < 6
            ? new(westTop10[i].TeamName, westTop10[i].Abbreviation, i + 1)
            : (i == 6 ? w7 : w8);

        var eastBracket = Enumerable.Range(0, 8).Select(EastSeed).ToArray();
        var westBracket = Enumerable.Range(0, 8).Select(WestSeed).ToArray();

        // Store seedings for display
        result.EastSeeds = eastBracket.Select(t => (t.Name, t.Abbr, t.Seed)).ToList();
        result.WestSeeds = westBracket.Select(t => (t.Name, t.Abbr, t.Seed)).ToList();

        // ── Conference rounds ─────────────────────────────────────────────────
        PlayoffTeam SimConference(
            PlayoffTeam[] bracket,
            List<PlayoffSeriesRecord> r1Out,
            List<PlayoffSeriesRecord> r2Out,
            ref PlayoffSeriesRecord? cfOut)
        {
            // R1: 1v8, 2v7, 3v6, 4v5
            var s1v8 = SimulateSeries(bracket[0], bracket[7], 1, state, ref doneGames, totalEst, progress);
            var s2v7 = SimulateSeries(bracket[1], bracket[6], 1, state, ref doneGames, totalEst, progress);
            var s3v6 = SimulateSeries(bracket[2], bracket[5], 1, state, ref doneGames, totalEst, progress);
            var s4v5 = SimulateSeries(bracket[3], bracket[4], 1, state, ref doneGames, totalEst, progress);
            r1Out.AddRange([s1v8, s2v7, s3v6, s4v5]);

            // R2: W(1/8) vs W(4/5),  W(2/7) vs W(3/6)
            var w1v8 = GetWinner(s1v8); var w2v7 = GetWinner(s2v7);
            var w3v6 = GetWinner(s3v6); var w4v5 = GetWinner(s4v5);

            var sa = OrderByHomeCourt(w1v8, w4v5);
            var sb = OrderByHomeCourt(w2v7, w3v6);
            var r2a = SimulateSeries(sa.high, sa.low, 2, state, ref doneGames, totalEst, progress);
            var r2b = SimulateSeries(sb.high, sb.low, 2, state, ref doneGames, totalEst, progress);
            r2Out.AddRange([r2a, r2b]);

            // CF
            var wa = GetWinner(r2a); var wb = GetWinner(r2b);
            var cf = OrderByHomeCourt(wa, wb);
            var cfSeries = SimulateSeries(cf.high, cf.low, 3, state, ref doneGames, totalEst, progress);
            cfOut = cfSeries;

            return GetWinner(cfSeries);
        }

        PlayoffSeriesRecord? eastCF = null, westCF = null;
        var eastFinalist = SimConference(eastBracket, result.EastFirstRound, result.EastSecondRound, ref eastCF);
        var westFinalist = SimConference(westBracket, result.WestFirstRound, result.WestSecondRound, ref westCF);
        result.EastConfFinals = eastCF;
        result.WestConfFinals = westCF;

        // ── NBA Finals ────────────────────────────────────────────────────────
        var finalsOrder = OrderByRegularSeasonWins(eastFinalist, westFinalist, regularSeason);
        result.Finals   = SimulateSeries(finalsOrder.high, finalsOrder.low, 4, state, ref doneGames, totalEst, progress);
        result.Champion     = result.Finals.Winner;
        result.ChampionAbbr = result.Finals.WinnerAbbr;

        // ── Finalize player stats ─────────────────────────────────────────────
        result.PlayerStats = [.. state.PlayerAgg.Values.Where(p => p.GP > 0).OrderByDescending(p => p.Ppg)];

        return result;
    }

    // ── Awards ────────────────────────────────────────────────────────────────

    public static SeasonAwards ComputeAwards(SeasonResult result)
    {
        var awards  = new SeasonAwards();
        var teamMap = result.TeamStats.ToDictionary(t => t.TeamName, StringComparer.OrdinalIgnoreCase);
        var players = result.PlayerStats.Where(p => p.GP >= 40 && p.TotalMIN > 0).ToList();
        if (players.Count == 0) return awards;

        // MVP: weighted PER + WS; team must have 40+ regular-season wins
        var mvpScore = (PlayerSeasonStats p) => p.PER * 0.55 + p.WS * 0.45;
        var mvp = players
            .Where(p => teamMap.TryGetValue(p.Team, out var tm) && tm.Wins >= 40)
            .OrderByDescending(mvpScore).FirstOrDefault();
        if (mvp is not null)
            awards.MVP = new(mvp.Name, mvp.Team, mvp.TeamAbbr, mvpScore(mvp), mvp.Position);

        // DPOY: defensive win shares + blocks + steals + opponent FG% suppression
        var dpoyScore = (PlayerSeasonStats p) => p.DWS * 0.45 + p.Bpg * 2.0 + p.Spg * 1.5 + Math.Max(0, 0.50 - p.OppFgPct) * 30;
        var dpoy = players.OrderByDescending(dpoyScore).FirstOrDefault();
        if (dpoy is not null)
            awards.DPOY = new(dpoy.Name, dpoy.Team, dpoy.TeamAbbr, dpoyScore(dpoy), dpoy.Position);

        // 6th Man of Year: high PER, low minutes (bench role)
        var sixmoy = players.Where(p => p.Mpg is >= 12 and < 28)
            .OrderByDescending(p => p.PER).FirstOrDefault();
        if (sixmoy is not null)
            awards.SixMOY = new(sixmoy.Name, sixmoy.Team, sixmoy.TeamAbbr, sixmoy.PER, sixmoy.Position);

        // All-NBA: 2 guards + 2 forwards + 1 center per team, 3 teams
        var guards   = players.Where(p => p.Position is Position.PG or Position.SG)
                              .OrderByDescending(p => p.PER).Take(6).ToList();
        var forwards = players.Where(p => p.Position is Position.SF or Position.PF)
                              .OrderByDescending(p => p.PER).Take(6).ToList();
        var centers  = players.Where(p => p.Position == Position.C)
                              .OrderByDescending(p => p.PER).Take(3).ToList();

        static AwardWinner ToW(PlayerSeasonStats p, double s) => new(p.Name, p.Team, p.TeamAbbr, s, p.Position);
        if (guards.Count >= 2 && forwards.Count >= 2 && centers.Count >= 1)
            awards.AllNba1 = [ToW(guards[0], guards[0].PER), ToW(guards[1], guards[1].PER),
                              ToW(forwards[0], forwards[0].PER), ToW(forwards[1], forwards[1].PER),
                              ToW(centers[0], centers[0].PER)];
        if (guards.Count >= 4 && forwards.Count >= 4 && centers.Count >= 2)
            awards.AllNba2 = [ToW(guards[2], guards[2].PER), ToW(guards[3], guards[3].PER),
                              ToW(forwards[2], forwards[2].PER), ToW(forwards[3], forwards[3].PER),
                              ToW(centers[1], centers[1].PER)];
        if (guards.Count >= 6 && forwards.Count >= 6 && centers.Count >= 3)
            awards.AllNba3 = [ToW(guards[4], guards[4].PER), ToW(guards[5], guards[5].PER),
                              ToW(forwards[4], forwards[4].PER), ToW(forwards[5], forwards[5].PER),
                              ToW(centers[2], centers[2].PER)];

        // All-Defense: 2G + 2F + 1C × 2 teams
        var defScore = (PlayerSeasonStats p) => p.DWS * 0.5 + p.Bpg * 1.5 + p.Spg * 1.5 + Math.Max(0, 0.50 - p.OppFgPct) * 20;
        var dGuards  = players.Where(p => p.Position is Position.PG or Position.SG).OrderByDescending(defScore).Take(4).ToList();
        var dForward = players.Where(p => p.Position is Position.SF or Position.PF).OrderByDescending(defScore).Take(4).ToList();
        var dCenter  = players.Where(p => p.Position == Position.C).OrderByDescending(defScore).Take(2).ToList();
        if (dGuards.Count >= 2 && dForward.Count >= 2 && dCenter.Count >= 1)
            awards.AllDef1 = [ToW(dGuards[0], defScore(dGuards[0])), ToW(dGuards[1], defScore(dGuards[1])),
                              ToW(dForward[0], defScore(dForward[0])), ToW(dForward[1], defScore(dForward[1])),
                              ToW(dCenter[0], defScore(dCenter[0]))];
        if (dGuards.Count >= 4 && dForward.Count >= 4 && dCenter.Count >= 2)
            awards.AllDef2 = [ToW(dGuards[2], defScore(dGuards[2])), ToW(dGuards[3], defScore(dGuards[3])),
                              ToW(dForward[2], defScore(dForward[2])), ToW(dForward[3], defScore(dForward[3])),
                              ToW(dCenter[1], defScore(dCenter[1]))];

        return awards;
    }

    // ── Playoff helpers ───────────────────────────────────────────────────────

    private static PlayoffTeam GetWinner(PlayoffSeriesRecord s) =>
        new(s.Winner, s.WinnerAbbr, s.WinnerSeed);

    private static (PlayoffTeam high, PlayoffTeam low) OrderByHomeCourt(PlayoffTeam a, PlayoffTeam b) =>
        a.Seed <= b.Seed ? (a, b) : (b, a);

    private static (PlayoffTeam high, PlayoffTeam low) OrderByRegularSeasonWins(
        PlayoffTeam east, PlayoffTeam west, SeasonResult reg)
    {
        var ew = reg.TeamStats.FirstOrDefault(t => t.TeamName == east.Name)?.Wins ?? 0;
        var ww = reg.TeamStats.FirstOrDefault(t => t.TeamName == west.Name)?.Wins ?? 0;
        return ew >= ww ? (east, west) : (west, east);
    }

    private static double PlayoffImportance(int round, bool isElimination) => (round, isElimination) switch
    {
        (1, false) => 0.55,
        (1, true)  => 0.70,
        (2, false) => 0.65,
        (2, true)  => 0.80,
        (3, false) => 0.75,
        (3, true)  => 0.90,
        (4, false) => 0.85,
        _          => 1.00,
    };

    private static bool IsHighSeedHome(int gameNum) => gameNum is 1 or 2 or 5 or 7;

    private static HashSet<string> BuildPlayoffDnp(
        Team homeTeam, Team awayTeam,
        Dictionary<string, double> fatigue,
        Random injRng,
        double gameImportance)
    {
        var dnp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // No fatigue-based DNP — fatigue reduces minutes targets continuously instead.

        foreach (var team in new[] { homeTeam, awayTeam })
        {
            var sorted = team.Roster.OrderByDescending(RotationManager.ComputeOverall).ToList();
            foreach (var p in team.Roster)
            {
                if (p.CurrentInjury is null) continue;
                if (p.CurrentInjury.Definition.Grade >= 2)
                {
                    dnp.Add(p.Name);
                    p.CurrentInjury.IsPlaying = false;
                }
                else
                {
                    int rank = sorted.IndexOf(p) + 1;
                    bool plays = InjuryService.ShouldPlayThroughG1(p, rank, p.CurrentInjury, gameImportance, injRng);
                    p.CurrentInjury.IsPlaying = plays;
                    if (!plays) dnp.Add(p.Name);
                }
            }
        }
        return dnp;
    }

    // Returns true if home team won.
    private static bool PlayOneGame(
        PlayoffTeam homeSeed, PlayoffTeam awaySeed,
        double gameImportance,
        PlayoffState state,
        List<SeasonGameRecord>? gamesOut,
        int restDays = 2)
    {
        if (!state.Teams.TryGetValue(homeSeed.Name, out var homeTeam) ||
            !state.Teams.TryGetValue(awaySeed.Name, out var awayTeam))
            return false;

        // Fatigue recovery
        foreach (var p in homeTeam.Roster)
            state.Fatigue[p.Name] = Math.Clamp(ApplyRecovery(state.Fatigue.GetValueOrDefault(p.Name, 90.0), restDays, p.Endurance), 0.0, 100.0);
        foreach (var p in awayTeam.Roster)
            state.Fatigue[p.Name] = Math.Clamp(ApplyRecovery(state.Fatigue.GetValueOrDefault(p.Name, 90.0), restDays, p.Endurance), 0.0, 100.0);

        // Tick injury recovery
        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var p in homeTeam.Roster.Concat(awayTeam.Roster))
        {
            if (p.CurrentInjury is null) continue;
            if (InjuryService.TickRecovery(p.CurrentInjury, restDays))
            {
                FinalizeInjuryRecord(p.Name, today, state.Pending, state.Finalized);
                p.CurrentInjury = null;
            }
        }

        var dnp    = BuildPlayoffDnp(homeTeam, awayTeam, state.Fatigue, state.InjRng, gameImportance);
        var result = state.Engine.SimulateGame(homeTeam, awayTeam,
            startingFatigue: state.Fatigue, dnpPlayers: dnp.Count > 0 ? dnp : null);
        bool homeWon = result.FinalHomeScore > result.FinalAwayScore;

        // Process in-game injuries
        foreach (var ev in result.InjuryEvents)
        {
            var injPlayer = homeTeam.Roster.Concat(awayTeam.Roster)
                .FirstOrDefault(p => string.Equals(p.Name, ev.PlayerName, StringComparison.OrdinalIgnoreCase));
            if (injPlayer is null) continue;
            int cur = injPlayer.InjuryRatings.GetValueOrDefault(ev.BodyPartKey, 99);
            injPlayer.InjuryRatings[ev.BodyPartKey] = Math.Max(1, cur - InjuryTables.RatingDegradation(ev.Grade));
            if (injPlayer.CurrentInjury is not null)
            {
                var perm = InjuryService.RollPermanentDebuff(injPlayer, injPlayer.CurrentInjury, state.InjRng);
                if (perm is not null)
                    foreach (var (k, v) in perm)
                    {
                        injPlayer.PermanentInjuryPenalties.TryGetValue(k, out int ex);
                        injPlayer.PermanentInjuryPenalties[k] = ex + v;
                    }
                state.Pending[ev.PlayerName] = new PendingInjury
                {
                    InjuryName = ev.InjuryName, BodyPart = ev.BodyPartDisplay,
                    Grade = ev.Grade, InjuredDate = today,
                    EstimatedDays = injPlayer.CurrentInjury.Definition.ExpectedDays,
                    ActualDays    = injPlayer.CurrentInjury.DaysRemaining,
                };
            }
        }

        // Drain fatigue
        foreach (var ps in result.Stats.Values)
        {
            if (ps.MinutesPlayed <= 0) continue;
            int end = state.Endurance.GetValueOrDefault(ps.Name, 50);
            state.Fatigue[ps.Name] = Math.Clamp(
                state.Fatigue.GetValueOrDefault(ps.Name, 90.0) - DrainAmount(ps.MinutesPlayed, end), 0.0, 100.0);
        }

        // Aggregate player stats
        string homeAbbr = homeTeam.Abbreviation, awayAbbr = awayTeam.Abbreviation;
        foreach (var ps in result.Stats.Values)
        {
            if (ps.MinutesPlayed <= 0) continue;
            var key = $"{ps.Name}|{ps.Team}";
            if (!state.PlayerAgg.TryGetValue(key, out var agg))
                state.PlayerAgg[key] = agg = new PlayerSeasonStats
                {
                    Name = ps.Name, Team = ps.Team,
                    TeamAbbr = ps.Team == homeTeam.Name ? homeAbbr : awayAbbr,
                    Position = ps.Position,
                };
            agg.GP++;
            agg.TotalPTS          += ps.Points;
            agg.TotalOREB         += ps.OffRebounds;
            agg.TotalDREB         += ps.DefRebounds;
            agg.TotalAST          += ps.Assists;
            agg.TotalSTL          += ps.Steals;
            agg.TotalBLK          += ps.Blocks;
            agg.TotalTOV          += ps.Turnovers;
            agg.TotalFGM          += ps.FGMade;
            agg.TotalFGA          += ps.FGAttempts;
            agg.TotalThreeMade    += ps.ThreeMade;
            agg.TotalThreeAtt     += ps.ThreeAttempts;
            agg.TotalFTM          += ps.FTMade;
            agg.TotalFTA          += ps.FTAttempts;
            agg.TotalMIN          += ps.MinutesPlayed;
            agg.TotalPlusMinus    += ps.PlusMinus;
        }

        gamesOut?.Add(new SeasonGameRecord(
            homeSeed.Name, awaySeed.Name, homeAbbr, awayAbbr,
            result.FinalHomeScore, result.FinalAwayScore,
            DateTime.Now, restDays, restDays));

        return homeWon;
    }

    // Same as PlayOneGame but returns the GameResult for watch-game playback.
    private static GameResult? PlayOneGameCapturing(
        PlayoffTeam homeSeed, PlayoffTeam awaySeed,
        double gameImportance, PlayoffState state,
        List<SeasonGameRecord>? gamesOut, int restDays = 2)
    {
        if (!state.Teams.TryGetValue(homeSeed.Name, out var homeTeam) ||
            !state.Teams.TryGetValue(awaySeed.Name, out var awayTeam))
            return null;

        foreach (var p in homeTeam.Roster)
            state.Fatigue[p.Name] = Math.Clamp(ApplyRecovery(state.Fatigue.GetValueOrDefault(p.Name, 90.0), restDays, p.Endurance), 0.0, 100.0);
        foreach (var p in awayTeam.Roster)
            state.Fatigue[p.Name] = Math.Clamp(ApplyRecovery(state.Fatigue.GetValueOrDefault(p.Name, 90.0), restDays, p.Endurance), 0.0, 100.0);

        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var p in homeTeam.Roster.Concat(awayTeam.Roster))
        {
            if (p.CurrentInjury is null) continue;
            if (InjuryService.TickRecovery(p.CurrentInjury, restDays))
            {
                FinalizeInjuryRecord(p.Name, today, state.Pending, state.Finalized);
                p.CurrentInjury = null;
            }
        }

        var dnp    = BuildPlayoffDnp(homeTeam, awayTeam, state.Fatigue, state.InjRng, gameImportance);
        var result = state.Engine.SimulateGame(homeTeam, awayTeam,
            startingFatigue: state.Fatigue, dnpPlayers: dnp.Count > 0 ? dnp : null);

        foreach (var ev in result.InjuryEvents)
        {
            var injPlayer = homeTeam.Roster.Concat(awayTeam.Roster)
                .FirstOrDefault(p => string.Equals(p.Name, ev.PlayerName, StringComparison.OrdinalIgnoreCase));
            if (injPlayer is null) continue;
            int cur = injPlayer.InjuryRatings.GetValueOrDefault(ev.BodyPartKey, 99);
            injPlayer.InjuryRatings[ev.BodyPartKey] = Math.Max(1, cur - InjuryTables.RatingDegradation(ev.Grade));
            if (injPlayer.CurrentInjury is not null)
            {
                var perm = InjuryService.RollPermanentDebuff(injPlayer, injPlayer.CurrentInjury, state.InjRng);
                if (perm is not null)
                    foreach (var (k, v) in perm)
                    {
                        injPlayer.PermanentInjuryPenalties.TryGetValue(k, out int ex);
                        injPlayer.PermanentInjuryPenalties[k] = ex + v;
                    }
                state.Pending[ev.PlayerName] = new PendingInjury
                {
                    InjuryName = ev.InjuryName, BodyPart = ev.BodyPartDisplay,
                    Grade = ev.Grade, InjuredDate = today,
                    EstimatedDays = injPlayer.CurrentInjury.Definition.ExpectedDays,
                    ActualDays    = injPlayer.CurrentInjury.DaysRemaining,
                };
            }
        }

        foreach (var ps in result.Stats.Values)
        {
            if (ps.MinutesPlayed <= 0) continue;
            int end = state.Endurance.GetValueOrDefault(ps.Name, 50);
            state.Fatigue[ps.Name] = Math.Clamp(
                state.Fatigue.GetValueOrDefault(ps.Name, 90.0) - DrainAmount(ps.MinutesPlayed, end), 0.0, 100.0);
        }

        string homeAbbr = homeTeam.Abbreviation, awayAbbr = awayTeam.Abbreviation;
        foreach (var ps in result.Stats.Values)
        {
            if (ps.MinutesPlayed <= 0) continue;
            var key = $"{ps.Name}|{ps.Team}";
            if (!state.PlayerAgg.TryGetValue(key, out var agg))
                state.PlayerAgg[key] = agg = new PlayerSeasonStats
                {
                    Name = ps.Name, Team = ps.Team,
                    TeamAbbr = ps.Team == homeTeam.Name ? homeAbbr : awayAbbr,
                    Position = ps.Position,
                };
            agg.GP++;
            agg.TotalPTS       += ps.Points;
            agg.TotalOREB      += ps.OffRebounds;
            agg.TotalDREB      += ps.DefRebounds;
            agg.TotalAST       += ps.Assists;
            agg.TotalSTL       += ps.Steals;
            agg.TotalBLK       += ps.Blocks;
            agg.TotalTOV       += ps.Turnovers;
            agg.TotalFGM       += ps.FGMade;
            agg.TotalFGA       += ps.FGAttempts;
            agg.TotalThreeMade += ps.ThreeMade;
            agg.TotalThreeAtt  += ps.ThreeAttempts;
            agg.TotalFTM       += ps.FTMade;
            agg.TotalFTA       += ps.FTAttempts;
            agg.TotalMIN       += ps.MinutesPlayed;
            agg.TotalPlusMinus += ps.PlusMinus;
        }

        gamesOut?.Add(new SeasonGameRecord(
            homeSeed.Name, awaySeed.Name, homeAbbr, awayAbbr,
            result.FinalHomeScore, result.FinalAwayScore,
            DateTime.Now, restDays, restDays));

        return result;
    }

    private PlayoffSeriesRecord SimulateSeries(
        PlayoffTeam highSeed, PlayoffTeam lowSeed, int round,
        PlayoffState state,
        ref int doneGames, int totalEst,
        IProgress<(int done, int total)>? progress)
    {
        var series = new PlayoffSeriesRecord
        {
            HighSeed = highSeed.Seed, LowSeed = lowSeed.Seed,
            HighSeedTeam = highSeed.Name, LowSeedTeam = lowSeed.Name,
            HighSeedAbbr = highSeed.Abbr, LowSeedAbbr = lowSeed.Abbr,
        };

        for (int gameNum = 1; !series.Complete; gameNum++)
        {
            bool highHome  = IsHighSeedHome(gameNum);
            var  home      = highHome ? highSeed : lowSeed;
            var  away      = highHome ? lowSeed  : highSeed;
            bool isElim    = series.HighSeedWins == 3 || series.LowSeedWins == 3;
            double importance = PlayoffImportance(round, isElim);

            bool homeWon = PlayOneGame(home, away, importance, state, series.Games);
            doneGames++;
            progress?.Report((doneGames, totalEst));

            if (homeWon) { if (highHome) series.HighSeedWins++; else series.LowSeedWins++; }
            else         { if (highHome) series.LowSeedWins++;  else series.HighSeedWins++; }
        }

        return series;
    }

    // ── Live Season (day-by-day) ──────────────────────────────────────────────

    /// <summary>
    /// Mutable state for a live day-by-day season simulation.
    /// Nested here so it can reference <see cref="PendingInjury"/>.
    /// </summary>
    public sealed class LiveSeasonState
    {
        // Schedule
        internal List<ScheduledGame> Schedule  { get; init; } = [];
        public   List<DateTime>      GameDates { get; init; } = [];

        // Dependencies
        internal IReadOnlyDictionary<string, Team> Teams  { get; init; } = new Dictionary<string, Team>();
        internal GameEngine                        Engine { get; init; } = new();

        // Season fatigue / injury
        internal Dictionary<string, double>             Fatigue      { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, int>                Endurance    { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        internal Random                                 InjRng       { get; init; } = new();
        internal Dictionary<string, PendingInjury>      PendingInj   { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, List<InjuryRecord>> FinalizedInj { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        // Live-updated output
        public Dictionary<string, TeamSeasonStats>   Standings     { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, PlayerSeasonStats> PlayerAgg     { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SeasonGameRecord>                CompletedGames { get; init; } = [];

        // Configuration
        public bool   DisableInjuries { get; init; }
        public string FollowedTeam    { get; init; } = "";

        // Progress
        public int      DateIndex          { get; set; }
        public bool     IsComplete         => DateIndex >= GameDates.Count;
        public DateTime CurrentDate        => DateIndex < GameDates.Count ? GameDates[DateIndex] : DateTime.MaxValue;
        public int      TotalScheduledDays => GameDates.Count;
        public int      GamesSimulated     => CompletedGames.Count;

        // Pace tracking
        public long TotalPossessions       { get; set; }
        public long TotalPossessionSeconds { get; set; }
        public long TotalPasses            { get; set; }

        // Set when IsComplete
        public SeasonResult? Result { get; set; }

        // Upcoming games for a given team (for calendar display)
        public List<ScheduledGame> UpcomingGamesFor(string teamId) =>
            DateIndex < GameDates.Count
                ? Schedule
                    .Where(g => g.Date.Date >= CurrentDate
                             && (g.HomeTeamId.Equals(teamId, StringComparison.OrdinalIgnoreCase)
                              || g.AwayTeamId.Equals(teamId, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(g => g.Date)
                    .Take(20)
                    .ToList()
                : [];

        // Games on a specific date
        public List<ScheduledGame> GamesOn(DateTime date) =>
            Schedule.Where(g => g.Date.Date == date.Date).ToList();

        // Recent completed games (ticker)
        public List<SeasonGameRecord> RecentCompleted(int count = 25) =>
            CompletedGames.Count <= count
                ? CompletedGames
                : CompletedGames.GetRange(CompletedGames.Count - count, count);
    }

    /// <summary>Initialises a live season state — generates schedule upfront, seeds fatigue at 100.</summary>
    public LiveSeasonState InitializeLiveSeason(
        IReadOnlyDictionary<string, Team> teamsByName,
        string followedTeam = "",
        int? seed = null,
        bool disableInjuries = false)
    {
        var (matchups, _) = GenerateSchedule(teamsByName, randomSeed: seed);
        var schedule      = NbaCalendarService.GenerateSchedule(matchups, startYear: 2025);
        var dates         = schedule.Select(g => g.Date.Date).Distinct().OrderBy(d => d).ToList();

        var standings = new Dictionary<string, TeamSeasonStats>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, team) in teamsByName)
            standings[name] = new TeamSeasonStats
            {
                TeamName     = name,
                Abbreviation = team.Abbreviation,
                Division     = team.Division,
                Conference   = team.Conference,
                PrimaryColor = team.PrimaryColor,
            };

        var fatigue   = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var endurance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in teamsByName.Values)
        foreach (var p in team.Roster)
        {
            fatigue[p.Name]   = 100.0;
            endurance[p.Name] = p.Endurance;
        }

        return new LiveSeasonState
        {
            Schedule        = schedule,
            GameDates       = dates,
            Teams           = teamsByName,
            Engine          = new GameEngine { DisableInjuries = disableInjuries },
            Fatigue         = fatigue,
            Endurance       = endurance,
            InjRng          = seed.HasValue ? new Random(seed.Value ^ 0x1A2B3C4D) : new Random(),
            PendingInj      = new Dictionary<string, PendingInjury>(StringComparer.OrdinalIgnoreCase),
            FinalizedInj    = new Dictionary<string, List<InjuryRecord>>(StringComparer.OrdinalIgnoreCase),
            Standings       = standings,
            PlayerAgg       = new Dictionary<string, PlayerSeasonStats>(StringComparer.OrdinalIgnoreCase),
            CompletedGames  = [],
            DisableInjuries = disableInjuries,
            FollowedTeam    = followedTeam,
        };
    }

    /// <summary>Simulates all games on the current calendar date and advances to the next date.</summary>
    public void SimulateDay(LiveSeasonState s)
    {
        if (s.IsComplete) return;

        var today       = s.GameDates[s.DateIndex];
        var todaysGames = s.Schedule.Where(g => g.Date.Date == today).ToList();

        foreach (var sg in todaysGames)
            SimulateOneLiveGame(s, sg);

        s.DateIndex++;
        FinalizeSeasonIfComplete(s);
    }

    /// <summary>Advances the live season through all remaining games instantly (on a background thread).</summary>
    public void SimulateRestOfSeason(LiveSeasonState s)
    {
        while (!s.IsComplete)
            SimulateDay(s);
    }

    internal static GameResult SimulateOneLiveGame(LiveSeasonState s, ScheduledGame sg)
    {
        s.Teams.TryGetValue(sg.HomeTeamId, out var homeTeam);
        s.Teams.TryGetValue(sg.AwayTeamId, out var awayTeam);
        if (homeTeam is null || awayTeam is null)
        {
            // Degenerate — team not found; return a no-op result so the season can continue
            var stub = homeTeam ?? awayTeam!;
            return new GameResult([], [], stub, stub, 0, 0, new Dictionary<string, int>(), []);
        }

        // Fatigue recovery
        foreach (var p in homeTeam.Roster)
            s.Fatigue[p.Name] = Math.Clamp(ApplyRecovery(s.Fatigue.GetValueOrDefault(p.Name, 100.0), sg.HomeRestDays, p.Endurance), 0.0, 100.0);
        foreach (var p in awayTeam.Roster)
            s.Fatigue[p.Name] = Math.Clamp(ApplyRecovery(s.Fatigue.GetValueOrDefault(p.Name, 100.0), sg.AwayRestDays, p.Endurance), 0.0, 100.0);

        // Injury tick
        if (!s.DisableInjuries)
        {
            foreach (var p in homeTeam.Roster)
            {
                if (p.CurrentInjury is null) continue;
                if (InjuryService.TickRecovery(p.CurrentInjury, sg.HomeRestDays))
                { FinalizeInjuryRecord(p.Name, DateOnly.FromDateTime(sg.Date), s.PendingInj, s.FinalizedInj); p.CurrentInjury = null; }
            }
            foreach (var p in awayTeam.Roster)
            {
                if (p.CurrentInjury is null) continue;
                if (InjuryService.TickRecovery(p.CurrentInjury, sg.AwayRestDays))
                { FinalizeInjuryRecord(p.Name, DateOnly.FromDateTime(sg.Date), s.PendingInj, s.FinalizedInj); p.CurrentInjury = null; }
            }
        }

        // DNP
        var dnp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!s.DisableInjuries)
        {
            foreach (var team in new[] { homeTeam, awayTeam })
            {
                var sorted = team.Roster.OrderByDescending(RotationManager.ComputeOverall).ToList();
                foreach (var p in team.Roster)
                {
                    if (p.CurrentInjury is null) continue;
                    if (p.CurrentInjury.Definition.Grade >= 2)
                        { dnp.Add(p.Name); p.CurrentInjury.IsPlaying = false; }
                    else
                    {
                        bool plays = InjuryService.ShouldPlayThroughG1(p, sorted.IndexOf(p) + 1, p.CurrentInjury, 0.25, s.InjRng);
                        p.CurrentInjury.IsPlaying = plays;
                        if (!plays) dnp.Add(p.Name);
                    }
                }
            }
        }

        var preGame = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in homeTeam.Roster.Concat(awayTeam.Roster))
            preGame[p.Name] = s.Fatigue.GetValueOrDefault(p.Name, 100.0);

        var result   = s.Engine.SimulateGame(homeTeam, awayTeam,
            startingFatigue: s.Fatigue, dnpPlayers: dnp.Count > 0 ? dnp : null);
        bool homeWon = result.FinalHomeScore > result.FinalAwayScore;

        s.CompletedGames.Add(new SeasonGameRecord(
            sg.HomeTeamId, sg.AwayTeamId,
            homeTeam.Abbreviation, awayTeam.Abbreviation,
            result.FinalHomeScore, result.FinalAwayScore,
            sg.Date, sg.HomeRestDays, sg.AwayRestDays));

        // Pace
        foreach (var poss in result.Possessions)
        {
            if (poss.SecondsElapsed <= 0) continue;
            s.TotalPossessions++;
            s.TotalPossessionSeconds += poss.SecondsElapsed;
            s.TotalPasses            += poss.PassCount;
            if (s.Standings.TryGetValue(poss.Team, out var pt)) pt.TotalPossessions++;
        }

        if (!s.Standings.TryGetValue(sg.HomeTeamId, out var homeSt) ||
            !s.Standings.TryGetValue(sg.AwayTeamId, out var awaySt)) goto DrainFatigue;

        // Standings
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
        homeSt.TotalPtsFor += result.FinalHomeScore; homeSt.TotalPtsAgainst += result.FinalAwayScore;
        awaySt.TotalPtsFor += result.FinalAwayScore; awaySt.TotalPtsAgainst += result.FinalHomeScore;

        // Player aggregation
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

            var key = $"{ps.Name}|{ps.Team}";
            if (!s.PlayerAgg.TryGetValue(key, out var pagg))
                s.PlayerAgg[key] = pagg = new PlayerSeasonStats
                    { Name = ps.Name, Team = ps.Team, TeamAbbr = st.Abbreviation, Position = ps.Position };

            bool played = ps.MinutesPlayed > 0;
            if (played) pagg.GP++;
            if (played && preGame.TryGetValue(ps.Name, out double pgf))
                pagg.TotalFatigueIn += pgf;

            var rosterPlayer = homeTeam.Roster.Concat(awayTeam.Roster)
                .FirstOrDefault(p => string.Equals(p.Name, ps.Name, StringComparison.OrdinalIgnoreCase));
            if (rosterPlayer?.CurrentInjury is not null)
            {
                if (!played && dnp.Contains(ps.Name))
                    { pagg.GamesInjured++; if (s.PendingInj.TryGetValue(ps.Name, out var pi)) pi.GamesMissed++; }
                else if (played && rosterPlayer.CurrentInjury.IsPlaying)
                    pagg.GamesPlayedThrough++;
            }

            pagg.TotalPTS += ps.Points; pagg.TotalOREB += ps.OffRebounds; pagg.TotalDREB += ps.DefRebounds;
            pagg.TotalAST += ps.Assists; pagg.TotalSTL += ps.Steals; pagg.TotalBLK += ps.Blocks;
            pagg.TotalTOV += ps.Turnovers; pagg.TotalFGM += ps.FGMade; pagg.TotalFGA += ps.FGAttempts;
            pagg.TotalThreeMade += ps.ThreeMade; pagg.TotalThreeAtt += ps.ThreeAttempts;
            pagg.TotalFTM += ps.FTMade; pagg.TotalFTA += ps.FTAttempts; pagg.TotalMIN += ps.MinutesPlayed;
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
            pagg.TotalTeamFGMOnCourt     += ps.TeamFGMOnCourt;
            pagg.TotalTeamORebOnCourt    += ps.TeamORebOnCourt;
            pagg.TotalTeamDRebOnCourt    += ps.TeamDRebOnCourt;
            pagg.TotalOppORebOnCourt     += ps.OppORebOnCourt;
            pagg.TotalOppDRebOnCourt     += ps.OppDRebOnCourt;
            pagg.TotalTeamPtsOnCourt     += ps.TeamPtsOnCourt;
            pagg.TotalOppPtsOnCourt      += ps.OppPtsOnCourt;
            pagg.TotalPossessionsOnCourt += ps.PossessionsOnCourt;
        }

        // DNP rested-game tracking
        foreach (var dnpName in dnp)
        {
            var dnpKey = s.PlayerAgg.Keys
                .FirstOrDefault(k => k.StartsWith(dnpName + "|", StringComparison.OrdinalIgnoreCase));
            if (dnpKey != null) s.PlayerAgg[dnpKey].GamesRested++;
        }

        // Post-game injuries
        foreach (var ev in result.InjuryEvents)
        {
            var injPlayer = homeTeam.Roster.Concat(awayTeam.Roster)
                .FirstOrDefault(p => string.Equals(p.Name, ev.PlayerName, StringComparison.OrdinalIgnoreCase));
            if (injPlayer is null) continue;

            int cur = injPlayer.InjuryRatings.GetValueOrDefault(ev.BodyPartKey, 99);
            injPlayer.InjuryRatings[ev.BodyPartKey] = Math.Max(1, cur - InjuryTables.RatingDegradation(ev.Grade));

            if (injPlayer.CurrentInjury is not null)
            {
                var perm = InjuryService.RollPermanentDebuff(injPlayer, injPlayer.CurrentInjury, s.InjRng);
                if (perm is not null)
                    foreach (var (k, v) in perm)
                    {
                        injPlayer.PermanentInjuryPenalties.TryGetValue(k, out int ex);
                        injPlayer.PermanentInjuryPenalties[k] = ex + v;
                    }
                s.PendingInj[ev.PlayerName] = new PendingInjury
                {
                    InjuryName    = ev.InjuryName, BodyPart = ev.BodyPartDisplay,
                    Grade         = ev.Grade,       InjuredDate = DateOnly.FromDateTime(sg.Date),
                    EstimatedDays = injPlayer.CurrentInjury.Definition.ExpectedDays,
                    ActualDays    = injPlayer.CurrentInjury.DaysRemaining,
                    PermanentPenalties = perm is not null && perm.Count > 0 ? new Dictionary<string, int>(perm) : null
                };
            }
        }

        DrainFatigue:
        foreach (var ps in result.Stats.Values)
        {
            if (ps.MinutesPlayed <= 0) continue;
            int end = s.Endurance.GetValueOrDefault(ps.Name, 50);
            s.Fatigue[ps.Name] = Math.Clamp(
                s.Fatigue.GetValueOrDefault(ps.Name, 100.0) - DrainAmount(ps.MinutesPlayed, end), 0.0, 100.0);
        }

        return result;
    }

    // ── Season finalization helper ────────────────────────────────────────────
    private static void FinalizeSeasonIfComplete(LiveSeasonState s)
    {
        if (!s.IsComplete) return;

        foreach (var (name, records) in s.FinalizedInj)
        {
            var key = s.PlayerAgg.Keys.FirstOrDefault(k =>
                k.StartsWith(name + "|", StringComparison.OrdinalIgnoreCase));
            if (key is not null) s.PlayerAgg[key].InjuryHistory.AddRange(records);
        }
        foreach (var (name, pi) in s.PendingInj)
        {
            var key = s.PlayerAgg.Keys.FirstOrDefault(k =>
                k.StartsWith(name + "|", StringComparison.OrdinalIgnoreCase));
            if (key is null) continue;
            s.PlayerAgg[key].InjuryHistory.Add(new InjuryRecord(
                pi.InjuryName, pi.BodyPart, pi.Grade, pi.InjuredDate, null,
                pi.EstimatedDays, pi.ActualDays, pi.GamesMissed,
                pi.PermanentPenalties is not null ? new Dictionary<string, int>(pi.PermanentPenalties) : null));
        }

        var result = new SeasonResult
        {
            TeamStats              = [.. s.Standings.Values.OrderByDescending(t => t.Wins)],
            PlayerStats            = [.. s.PlayerAgg.Values.OrderByDescending(p => p.Ppg)],
            Games                  = s.CompletedGames,
            SimulatedAt            = DateTime.Now,
            TotalPossessions       = s.TotalPossessions,
            TotalPossessionSeconds = s.TotalPossessionSeconds,
            TotalPasses            = s.TotalPasses,
        };
        ComputeAdvancedStats(result);
        s.Result = result;
    }

    /// <summary>
    /// Advances the live season through all days strictly before <paramref name="targetDate"/>.
    /// Call before <see cref="SimulateDayCapturingGame"/> to fast-forward to the right day.
    /// </summary>
    public void SimulateDaysUntil(LiveSeasonState s, DateTime targetDate)
    {
        while (!s.IsComplete && s.CurrentDate.Date < targetDate.Date)
            SimulateDay(s);
    }

    /// <summary>
    /// Simulates all games on the current calendar day.
    /// Returns the <see cref="GameResult"/> for <paramref name="targetGame"/>, or null if not found on today's slate.
    /// Season standings and player aggregates are updated exactly as in a normal day.
    /// </summary>
    public GameResult? SimulateDayCapturingGame(LiveSeasonState s, ScheduledGame targetGame)
    {
        if (s.IsComplete) return null;

        var today       = s.GameDates[s.DateIndex];
        var todaysGames = s.Schedule.Where(g => g.Date.Date == today).ToList();

        GameResult? captured = null;
        foreach (var sg in todaysGames)
        {
            var gr = SimulateOneLiveGame(s, sg);
            if (sg.HomeTeamId.Equals(targetGame.HomeTeamId, StringComparison.OrdinalIgnoreCase) &&
                sg.AwayTeamId.Equals(targetGame.AwayTeamId, StringComparison.OrdinalIgnoreCase) &&
                sg.Date.Date == targetGame.Date.Date)
                captured = gr;
        }

        s.DateIndex++;
        FinalizeSeasonIfComplete(s);
        return captured;
    }

    // ── Live Playoffs ─────────────────────────────────────────────────────────

    public sealed class LivePlayoffState
    {
        public  PlayoffResult  Result        { get; } = new();
        internal PlayoffState  State         { get; init; } = null!;
        public  SeasonResult   RegularSeason { get; init; } = null!;
        public  bool           IsComplete    => Result.Champion is not null;
        public  DateOnly       CurrentDate   { get; set; }
        // Bracket seeds stored for home-court calculation in later rounds
        internal PlayoffTeam[] EastBracket   { get; init; } = [];
        internal PlayoffTeam[] WestBracket   { get; init; } = [];
    }

    /// <summary>
    /// Runs the play-in and sets up the first-round bracket without simulating any series games.
    /// Returns a <see cref="LivePlayoffState"/> ready for interactive game-by-game simulation.
    /// </summary>
    public LivePlayoffState InitializeLivePlayoffs(
        IReadOnlyDictionary<string, Team> teamsByName,
        SeasonResult regularSeason)
    {
        var state = new PlayoffState
        {
            Teams     = teamsByName,
            Engine    = new GameEngine(),
            Fatigue   = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            Endurance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            InjRng    = new Random(),
            Pending   = new Dictionary<string, PendingInjury>(StringComparer.OrdinalIgnoreCase),
            Finalized = new Dictionary<string, List<InjuryRecord>>(StringComparer.OrdinalIgnoreCase),
            PlayerAgg = new Dictionary<string, PlayerSeasonStats>(StringComparer.OrdinalIgnoreCase),
        };
        foreach (var team in teamsByName.Values)
        foreach (var p in team.Roster)
        {
            state.Fatigue[p.Name]   = 90.0;
            state.Endurance[p.Name] = p.Endurance;
        }

        var eastTop10 = regularSeason.TeamStats
            .Where(t => t.Conference == "Eastern")
            .OrderByDescending(t => t.Wins).ThenByDescending(t => t.Pct)
            .Take(10).ToList();
        var westTop10 = regularSeason.TeamStats
            .Where(t => t.Conference == "Western")
            .OrderByDescending(t => t.Wins).ThenByDescending(t => t.Pct)
            .Take(10).ToList();

        var live = new LivePlayoffState { State = state, RegularSeason = regularSeason };

        // Run play-in (6 games, instant — same logic as SimulatePlayoffs)
        PlayoffTeam RunPlayIn(List<TeamSeasonStats> conf10, out PlayoffTeam seed7, out PlayoffTeam seed8)
        {
            if (conf10.Count < 8)
            {
                seed7 = new(conf10.ElementAtOrDefault(6)?.TeamName ?? "", conf10.ElementAtOrDefault(6)?.Abbreviation ?? "", 7);
                seed8 = new(conf10.ElementAtOrDefault(7)?.TeamName ?? "", conf10.ElementAtOrDefault(7)?.Abbreviation ?? "", 8);
                return seed7;
            }
            var t7  = new PlayoffTeam(conf10[6].TeamName, conf10[6].Abbreviation, 7);
            var t8  = new PlayoffTeam(conf10[7].TeamName, conf10[7].Abbreviation, 8);
            var t9  = conf10.Count > 8 ? new PlayoffTeam(conf10[8].TeamName, conf10[8].Abbreviation, 9)  : t8;
            var t10 = conf10.Count > 9 ? new PlayoffTeam(conf10[9].TeamName, conf10[9].Abbreviation, 10) : t9;
            bool g1 = PlayOneGame(t7,  t8,  0.55, state, live.Result.PlayInGames, 3);
            bool g2 = PlayOneGame(t9,  t10, 0.55, state, live.Result.PlayInGames, 3);
            var w1 = g1 ? t7 : t8; var l1 = g1 ? t8 : t7; var w2 = g2 ? t9 : t10;
            bool g3 = PlayOneGame(l1, w2, 0.70, state, live.Result.PlayInGames, 3);
            seed7 = new(w1.Name, w1.Abbr, 7);
            seed8 = new((g3 ? l1 : w2).Name, (g3 ? l1 : w2).Abbr, 8);
            return seed7;
        }

        PlayoffTeam e7, e8, w7, w8;
        RunPlayIn(eastTop10, out e7, out e8);
        RunPlayIn(westTop10, out w7, out w8);

        PlayoffTeam ES(int i) => i < 6 ? new(eastTop10[i].TeamName, eastTop10[i].Abbreviation, i+1) : (i == 6 ? e7 : e8);
        PlayoffTeam WS(int i) => i < 6 ? new(westTop10[i].TeamName, westTop10[i].Abbreviation, i+1) : (i == 6 ? w7 : w8);
        var eastBracket = Enumerable.Range(0, 8).Select(ES).ToArray();
        var westBracket = Enumerable.Range(0, 8).Select(WS).ToArray();

        // Store seedings
        live.Result.EastSeeds = eastBracket.Select(t => (t.Name, t.Abbr, t.Seed)).ToList();
        live.Result.WestSeeds = westBracket.Select(t => (t.Name, t.Abbr, t.Seed)).ToList();

        // Set up R1 matchups (no games played yet)
        PlayoffSeriesRecord MakeSeries(PlayoffTeam high, PlayoffTeam low) =>
            new() { HighSeed=high.Seed, LowSeed=low.Seed, HighSeedTeam=high.Name, LowSeedTeam=low.Name, HighSeedAbbr=high.Abbr, LowSeedAbbr=low.Abbr };

        var r1Start = new DateOnly(2026, 4, 19);
        live.CurrentDate = r1Start;

        void AddR1(List<PlayoffSeriesRecord> list, PlayoffTeam high, PlayoffTeam low)
        {
            var s = MakeSeries(high, low);
            s.StartDate = r1Start;
            list.Add(s);
        }

        AddR1(live.Result.EastFirstRound, eastBracket[0], eastBracket[7]); // 1v8
        AddR1(live.Result.EastFirstRound, eastBracket[1], eastBracket[6]); // 2v7
        AddR1(live.Result.EastFirstRound, eastBracket[2], eastBracket[5]); // 3v6
        AddR1(live.Result.EastFirstRound, eastBracket[3], eastBracket[4]); // 4v5
        AddR1(live.Result.WestFirstRound, westBracket[0], westBracket[7]); // 1v8
        AddR1(live.Result.WestFirstRound, westBracket[1], westBracket[6]); // 2v7
        AddR1(live.Result.WestFirstRound, westBracket[2], westBracket[5]); // 3v6
        AddR1(live.Result.WestFirstRound, westBracket[3], westBracket[4]); // 4v5

        return live;
    }

    /// <summary>Simulates the next game in the series, updates the series record.</summary>
    public void SimPlayoffGame(LivePlayoffState live, PlayoffSeriesRecord series)
    {
        if (series.Complete) return;
        var (high, low) = (new PlayoffTeam(series.HighSeedTeam, series.HighSeedAbbr, series.HighSeed),
                           new PlayoffTeam(series.LowSeedTeam,  series.LowSeedAbbr,  series.LowSeed));
        int gameNum    = series.Games.Count + 1;
        var gameDate   = series.GameDate(gameNum);
        if (gameDate > live.CurrentDate) live.CurrentDate = gameDate;
        bool highHome  = IsHighSeedHome(gameNum);
        bool isElim    = series.HighSeedWins == 3 || series.LowSeedWins == 3;
        int round      = DetermineRound(live, series);
        double imp     = PlayoffImportance(round, isElim);
        bool homeWon   = PlayOneGame(highHome ? high : low, highHome ? low : high, imp, live.State, series.Games);
        if (homeWon) { if (highHome) series.HighSeedWins++; else series.LowSeedWins++; }
        else         { if (highHome) series.LowSeedWins++;  else series.HighSeedWins++; }
    }

    /// <summary>Simulates the next game in the series and returns the GameResult for watch-game.</summary>
    public GameResult? SimPlayoffGameCapturing(LivePlayoffState live, PlayoffSeriesRecord series)
    {
        if (series.Complete) return null;
        var (high, low) = (new PlayoffTeam(series.HighSeedTeam, series.HighSeedAbbr, series.HighSeed),
                           new PlayoffTeam(series.LowSeedTeam,  series.LowSeedAbbr,  series.LowSeed));
        int gameNum    = series.Games.Count + 1;
        var gameDate   = series.GameDate(gameNum);
        if (gameDate > live.CurrentDate) live.CurrentDate = gameDate;
        bool highHome  = IsHighSeedHome(gameNum);
        bool isElim    = series.HighSeedWins == 3 || series.LowSeedWins == 3;
        int round      = DetermineRound(live, series);
        double imp     = PlayoffImportance(round, isElim);
        var gr = PlayOneGameCapturing(highHome ? high : low, highHome ? low : high, imp, live.State, series.Games);
        if (gr is null) return null;
        bool homeWon = gr.FinalHomeScore > gr.FinalAwayScore;
        if (homeWon) { if (highHome) series.HighSeedWins++; else series.LowSeedWins++; }
        else         { if (highHome) series.LowSeedWins++;  else series.HighSeedWins++; }
        return gr;
    }

    /// <summary>Sims all remaining games in the series to completion.</summary>
    public void SimPlayoffSeries(LivePlayoffState live, PlayoffSeriesRecord series)
    {
        while (!series.Complete)
            SimPlayoffGame(live, series);
    }

    /// <summary>
    /// Sims the target series to completion, then sims all concurrent series for any games
    /// whose scheduled date falls on or before the target series' completion date.
    /// </summary>
    public void SimPlayoffSeriesWithConcurrent(LivePlayoffState live, PlayoffSeriesRecord targetSeries)
    {
        while (!targetSeries.Complete)
        {
            SimPlayoffGame(live, targetSeries);
            AdvanceBracket(live);
            if (live.IsComplete) return;
        }
        var endDate = live.CurrentDate;

        bool anyProgress;
        do
        {
            anyProgress = false;
            foreach (var other in GetAllActiveSeries(live).ToList())
            {
                if (other.Complete || ReferenceEquals(other, targetSeries)) continue;
                if (other.StartDate.HasValue && other.NextGameDate <= endDate)
                {
                    SimPlayoffGame(live, other);
                    AdvanceBracket(live);
                    anyProgress = true;
                    if (live.IsComplete) return;
                }
            }
        } while (anyProgress);
    }

    /// <summary>
    /// Sims every series in the given round to completion, advancing in game-date order.
    /// </summary>
    public void SimPlayoffRound(LivePlayoffState live, List<PlayoffSeriesRecord> roundSeries)
    {
        while (roundSeries.Any(s => !s.Complete))
        {
            var next = roundSeries.Where(s => !s.Complete)
                                  .OrderBy(s => s.NextGameDate)
                                  .First();
            SimPlayoffGame(live, next);
            AdvanceBracket(live);
            if (live.IsComplete) return;
        }
    }

    private static List<PlayoffSeriesRecord> GetAllActiveSeries(LivePlayoffState live)
    {
        var r = live.Result;
        var list = new List<PlayoffSeriesRecord>();
        list.AddRange(r.EastFirstRound);
        list.AddRange(r.WestFirstRound);
        list.AddRange(r.EastSecondRound);
        list.AddRange(r.WestSecondRound);
        if (r.EastConfFinals is not null) list.Add(r.EastConfFinals);
        if (r.WestConfFinals is not null) list.Add(r.WestConfFinals);
        if (r.Finals is not null) list.Add(r.Finals);
        return list;
    }

    /// <summary>
    /// Checks if the current round is fully complete and populates the next round's matchups.
    /// Call after every SimPlayoffGame/SimPlayoffSeries. Returns true if bracket advanced.
    /// </summary>
    public bool AdvanceBracket(LivePlayoffState live)
    {
        var r = live.Result;
        bool advanced = false;

        PlayoffSeriesRecord MakeSeries(PlayoffTeam high, PlayoffTeam low) =>
            new() { HighSeed=high.Seed, LowSeed=low.Seed, HighSeedTeam=high.Name, LowSeedTeam=low.Name, HighSeedAbbr=high.Abbr, LowSeedAbbr=low.Abbr };

        // Helper: find latest game date across a set of completed series
        static DateOnly LastGameDate(IEnumerable<PlayoffSeriesRecord> series) =>
            series.Where(s => s.StartDate.HasValue && s.Games.Count > 0)
                  .Select(s => s.GameDate(s.Games.Count))
                  .DefaultIfEmpty(new DateOnly(2026, 4, 19))
                  .Max();

        // East R1 → R2
        if (r.EastFirstRound.Count == 4 && r.EastFirstRound.All(s => s.Complete) && r.EastSecondRound.Count == 0)
        {
            var w1v8 = GetWinner(r.EastFirstRound[0]); var w4v5 = GetWinner(r.EastFirstRound[3]);
            var w2v7 = GetWinner(r.EastFirstRound[1]); var w3v6 = GetWinner(r.EastFirstRound[2]);
            var (ha, la) = OrderByHomeCourt(w1v8, w4v5);
            var (hb, lb) = OrderByHomeCourt(w2v7, w3v6);
            var r2Start = LastGameDate(r.EastFirstRound).AddDays(3);
            var sa = MakeSeries(ha, la); sa.StartDate = r2Start;
            var sb = MakeSeries(hb, lb); sb.StartDate = r2Start;
            r.EastSecondRound.AddRange([sa, sb]);
            advanced = true;
        }
        // West R1 → R2
        if (r.WestFirstRound.Count == 4 && r.WestFirstRound.All(s => s.Complete) && r.WestSecondRound.Count == 0)
        {
            var w1v8 = GetWinner(r.WestFirstRound[0]); var w4v5 = GetWinner(r.WestFirstRound[3]);
            var w2v7 = GetWinner(r.WestFirstRound[1]); var w3v6 = GetWinner(r.WestFirstRound[2]);
            var (ha, la) = OrderByHomeCourt(w1v8, w4v5); var (hb, lb) = OrderByHomeCourt(w2v7, w3v6);
            var r2Start = LastGameDate(r.WestFirstRound).AddDays(3);
            var sc = MakeSeries(ha, la); sc.StartDate = r2Start;
            var sd = MakeSeries(hb, lb); sd.StartDate = r2Start;
            r.WestSecondRound.AddRange([sc, sd]);
            advanced = true;
        }
        // East R2 → CF
        if (r.EastSecondRound.Count == 2 && r.EastSecondRound.All(s => s.Complete) && r.EastConfFinals is null)
        {
            var (h, l) = OrderByHomeCourt(GetWinner(r.EastSecondRound[0]), GetWinner(r.EastSecondRound[1]));
            r.EastConfFinals = MakeSeries(h, l);
            r.EastConfFinals.StartDate = LastGameDate(r.EastSecondRound).AddDays(3);
            advanced = true;
        }
        // West R2 → CF
        if (r.WestSecondRound.Count == 2 && r.WestSecondRound.All(s => s.Complete) && r.WestConfFinals is null)
        {
            var (h, l) = OrderByHomeCourt(GetWinner(r.WestSecondRound[0]), GetWinner(r.WestSecondRound[1]));
            r.WestConfFinals = MakeSeries(h, l);
            r.WestConfFinals.StartDate = LastGameDate(r.WestSecondRound).AddDays(3);
            advanced = true;
        }
        // ECF + WCF → Finals
        if (r.EastConfFinals?.Complete == true && r.WestConfFinals?.Complete == true && r.Finals is null)
        {
            var east = GetWinner(r.EastConfFinals); var west = GetWinner(r.WestConfFinals);
            var (h, l) = OrderByRegularSeasonWins(east, west, live.RegularSeason);
            r.Finals = MakeSeries(h, l);
            r.Finals.StartDate = new[] { r.EastConfFinals, r.WestConfFinals }
                .Select(cf => LastGameDate([cf])).Max().AddDays(3);
            advanced = true;
        }
        // Finals done → Champion
        if (r.Finals?.Complete == true && r.Champion is null)
        {
            r.Champion     = r.Finals.Winner;
            r.ChampionAbbr = r.Finals.WinnerAbbr;
            advanced = true;
        }
        return advanced;
    }

    private static int DetermineRound(LivePlayoffState live, PlayoffSeriesRecord series)
    {
        var r = live.Result;
        if (r.Finals == series) return 4;
        if (r.EastConfFinals == series || r.WestConfFinals == series) return 3;
        if (r.EastSecondRound.Contains(series) || r.WestSecondRound.Contains(series)) return 2;
        return 1;
    }
}
