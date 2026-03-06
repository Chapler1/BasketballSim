using BasketballSim.Models;

namespace BasketballSim.Simulation;

public enum ShotClockPhase { Early, Mid, Late, BuzzerBeater }
public enum PossessionContext { Regular, MustScore, PotentialGameWinner, IntentionalFoul }
public enum TurnoverType { Stolen, DeadBall }

public record GameState(
    int HomeScore, int AwayScore,
    int Quarter, int ClockSeconds,
    bool IsHomePossession)
{
    public int ScoreDiff => IsHomePossession
        ? HomeScore - AwayScore
        : AwayScore - HomeScore;

    public bool IsClutch => Quarter >= 4
        && ClockSeconds <= 300
        && Math.Abs(HomeScore - AwayScore) <= 5;
}

public record GameResult(
    List<PossessionResult> Possessions,
    Dictionary<string, PlayerGameStats> Stats,
    Team HomeTeam,
    Team AwayTeam,
    int FinalHomeScore,
    int FinalAwayScore
);

public class GameEngine
{
    private readonly Random _rng = new();
    private Dictionary<string, PlayerGameStats> _stats = new();

    public GameResult SimulateGame(Team homeTeam, Team awayTeam)
    {
        _stats = new Dictionary<string, PlayerGameStats>();

        // Initialize stats for all players
        foreach (var p in homeTeam.Roster.Concat(awayTeam.Roster))
        {
            _stats[p.Name] = new PlayerGameStats
            {
                Name = p.Name,
                Team = p.Team,
                Position = p.Position
            };
        }

        // Reset fatigue
        foreach (var p in homeTeam.Roster.Concat(awayTeam.Roster))
            p.Fatigue = 0.0;

        var results = new List<PossessionResult>(300);
        int homeScore = 0, awayScore = 0;
        int possessionsPerQuarter = (int)((homeTeam.Pace + awayTeam.Pace) / 2 / 4);

        // Simulate 4 quarters
        for (int quarter = 1; quarter <= 4; quarter++)
        {
            int clockSeconds = 720;
            bool homeFirst = quarter % 2 == 1; // home starts Q1, Q3; away starts Q2, Q4

            for (int p = 0; p < possessionsPerQuarter * 2; p++)
            {
                if (clockSeconds <= 0) break;

                bool isHome = (p % 2 == 0) == homeFirst;
                var state = new GameState(homeScore, awayScore, quarter, clockSeconds, isHome);
                var offTeam = isHome ? homeTeam : awayTeam;
                var defTeam = isHome ? awayTeam : homeTeam;

                var lineup = offTeam.Starters;
                var defLineup = defTeam.Starters;

                SimulatePossessionChain(offTeam, defTeam, lineup, defLineup, state,
                    ref homeScore, ref awayScore, ref clockSeconds, results);

                // Halftime fatigue reset
                if (quarter == 2 && p == possessionsPerQuarter * 2 - 1)
                {
                    foreach (var pl in homeTeam.Roster.Concat(awayTeam.Roster))
                        pl.Fatigue *= 0.5;
                }
            }
        }

        // Overtime if tied
        int otPeriod = 0;
        while (homeScore == awayScore)
        {
            otPeriod++;
            int clockSeconds = 300;
            for (int p = 0; p < 20 && clockSeconds > 0; p++)
            {
                bool isHome = p % 2 == 0;
                var state = new GameState(homeScore, awayScore, 4 + otPeriod, clockSeconds, isHome);
                var offTeam = isHome ? homeTeam : awayTeam;
                var defTeam = isHome ? awayTeam : homeTeam;
                SimulatePossessionChain(offTeam, defTeam, offTeam.Starters, defTeam.Starters,
                    state, ref homeScore, ref awayScore, ref clockSeconds, results);
            }
            if (otPeriod > 5) break; // safety
        }

        return new GameResult(results, _stats, homeTeam, awayTeam, homeScore, awayScore);
    }

    private void SimulatePossessionChain(
        Team offTeam, Team defTeam,
        List<Player> lineup, List<Player> defLineup,
        GameState state,
        ref int homeScore, ref int awayScore,
        ref int clockSeconds,
        List<PossessionResult> results)
    {
        int orbCount = 0;
        Player? prevRebounder = null;

        while (true)
        {
            // ── Stage 1: Possession Context ──────────────────────────
            int scoreDiff = state.IsHomePossession
                ? state.HomeScore - state.AwayScore
                : state.AwayScore - state.HomeScore;

            PossessionContext posContext;
            if (clockSeconds <= 5 && Math.Abs(state.HomeScore - state.AwayScore) <= 3)
                posContext = PossessionContext.PotentialGameWinner;
            else if (scoreDiff <= -8 && state.Quarter == 4 && clockSeconds <= 120)
                posContext = PossessionContext.IntentionalFoul;
            else if (scoreDiff < 0 && state.Quarter == 4 && clockSeconds <= 45)
                posContext = PossessionContext.MustScore;
            else
                posContext = PossessionContext.Regular;

            // IntentionalFoul — just log foul and return
            if (posContext == PossessionContext.IntentionalFoul)
            {
                // Award 2 FT for the trailing team scenario; here offense gets fouled
                var ftShooter = WeightedRandom(lineup, p => p.USG_Weight);
                int pts = 0;
                for (int i = 0; i < 2; i++)
                    if (_rng.NextDouble() < ftShooter.FTMakePct) pts++;
                if (state.IsHomePossession) homeScore += pts;
                else awayScore += pts;

                EnsureStats(ftShooter).FTAttempts += 2;
                EnsureStats(ftShooter).FTMade += pts;
                EnsureStats(ftShooter).Points += pts;

                int clock = Math.Max(0, clockSeconds - 3);
                clockSeconds = clock;
                results.Add(new PossessionResult
                {
                    Team = offTeam.Name,
                    Narrative = $"{ftShooter.Name} at the line — {pts}/2 free throws",
                    HomeScore = homeScore, AwayScore = awayScore,
                    Quarter = state.Quarter, ClockSeconds = clockSeconds,
                    PointsScored = pts,
                    Event = PossessionEvent.FreeThrows,
                    Scorer = ftShooter.Name,
                    Context = ShotContext.None,
                    FTAttempts = 2
                });
                return;
            }

            // ── Stage 2: Shot Clock Phase ─────────────────────────────
            ShotClockPhase phase;
            if (posContext == PossessionContext.PotentialGameWinner)
            {
                phase = ShotClockPhase.BuzzerBeater;
            }
            else
            {
                // Select action player first to use their aggressiveness
                var tempAction = WeightedRandom(lineup, p => p.USG_Weight * (1 - p.Fatigue * 0.25));
                double agg = tempAction.ShotClockAggressiveness;
                double r = _rng.NextDouble();
                if (r < agg)
                    phase = ShotClockPhase.Early;
                else if (r < agg + 0.55)
                    phase = ShotClockPhase.Mid;
                else
                    phase = ShotClockPhase.Late;
            }

            // Clock consumption
            int timeUsed = phase switch
            {
                ShotClockPhase.Early       => _rng.Next(12, 19),
                ShotClockPhase.Mid         => _rng.Next(8, 15),
                ShotClockPhase.Late        => _rng.Next(2, 9),
                ShotClockPhase.BuzzerBeater=> _rng.Next(1, 4),
                _                          => 13
            };
            clockSeconds = Math.Max(0, clockSeconds - timeUsed);

            // ── Stage 3: Select Action Player ────────────────────────
            Player actionPlayer;
            if (prevRebounder != null && orbCount > 0)
            {
                var pr = prevRebounder;
                actionPlayer = WeightedRandom(lineup, p =>
                {
                    double w = p.USG_Weight * (1 - p.Fatigue * 0.25);
                    if (p.Name == pr.Name) w *= 3.5;
                    if (phase == ShotClockPhase.Late) w = Math.Pow(w, 1.4);
                    return Math.Max(w, 0.01);
                });
            }
            else
            {
                actionPlayer = WeightedRandom(lineup, p =>
                {
                    double w = p.USG_Weight * (1 - p.Fatigue * 0.25);
                    if (phase == ShotClockPhase.Late) w = Math.Pow(w, 1.4);
                    return Math.Max(w, 0.01);
                });
            }

            // Fatigue
            actionPlayer.Fatigue = Math.Min(1.0, actionPlayer.Fatigue + 0.004);

            // ── Stage 4: Turnover Check ───────────────────────────────
            double baseTO = actionPlayer.TurnoverRate;
            if (phase == ShotClockPhase.Late) baseTO *= 1.3;

            if (_rng.NextDouble() < baseTO)
            {
                double totalStealThreat = defLineup.Sum(d => d.StealMod);
                double stealShare = totalStealThreat / (totalStealThreat + 0.15);

                EnsureStats(actionPlayer).Turnovers++;

                if (_rng.NextDouble() < stealShare)
                {
                    var stealer = WeightedRandom(defLineup, d => Math.Max(d.StealMod, 0.01));
                    EnsureStats(stealer).Steals++;
                    int clock2 = clockSeconds;
                    results.Add(new PossessionResult
                    {
                        Team = offTeam.Name,
                        Narrative = $"{offTeam.Abbreviation} — {actionPlayer.Name} stolen by {stealer.Name}!",
                        HomeScore = homeScore, AwayScore = awayScore,
                        Quarter = state.Quarter, ClockSeconds = clock2,
                        PointsScored = 0,
                        Event = PossessionEvent.TurnoverStolen,
                        Scorer = actionPlayer.Name,
                        Stealer = stealer.Name,
                        Context = ShotContext.None
                    });
                }
                else
                {
                    results.Add(new PossessionResult
                    {
                        Team = offTeam.Name,
                        Narrative = $"{offTeam.Abbreviation} — {actionPlayer.Name} turnover — out of bounds",
                        HomeScore = homeScore, AwayScore = awayScore,
                        Quarter = state.Quarter, ClockSeconds = clockSeconds,
                        PointsScored = 0,
                        Event = PossessionEvent.TurnoverDeadBall,
                        Scorer = actionPlayer.Name,
                        Context = ShotContext.None
                    });
                }
                return;
            }

            // ── Stage 5: Shot Type Selection ─────────────────────────
            bool isOffReb = orbCount > 0;
            int spacingLevel = offTeam.SpacingLevel(lineup);

            // Per-player perimeter pressure: each player independently frees or occupies their defender.
            // Positive → defender is pinned (shooter), negative → defender can sag and help in the paint.
            // The lineup average is the net team-level paint congestion signal.
            double netPerimPressure = lineup.Average(p =>
                (p.Attr_ThreePoint * 0.6 + p.Attr_MidRange * 0.4 - 50.0) / 50.0);
            // -1 = all non-shooters (max sag), +1 = all elite shooters (max gravity)

            double teamSag     = Math.Max(0.0, -netPerimPressure);
            double teamGravity = Math.Max(0.0,  netPerimPressure);

            double wInside = Math.Max(actionPlayer.DriveGravity * 100, 0.01);
            double wDunk   = Math.Max(actionPlayer.Attr_Dunks * (actionPlayer.Jumping / 100.0) * 0.65, 0.01);
            double wMid    = Math.Max(actionPlayer.Attr_MidRange * 0.96, 0.01);
            double wThree  = Math.Max(actionPlayer.Attr_ThreePoint * 1.75, 0.01);

            if (isOffReb)    { wInside *= 2.5; wDunk *= 2.0; wThree *= 0.1; }
            if (phase == ShotClockPhase.Early) { wInside *= 1.4; wDunk *= 1.3; wMid *= 0.7; }
            if (phase == ShotClockPhase.Late)  { wThree *= 1.3; wMid *= 1.4; wDunk *= 0.3; }
            if (actionPlayer.Position is Position.C or Position.PF)
            {
                wMid *= 1.2;
                if (actionPlayer.Attr_ThreePoint < 60) wThree *= 0.7;
            }

            // Defensive team influence on shot selection
            double defPerimAvg = defLineup.Average(d => (double)d.Attr_PerimeterDefense);
            double defIntAvg   = defLineup.Average(d => (double)d.Attr_InteriorDefense);
            double perimStrength = (defPerimAvg - 50.0) / 50.0;  // -1 to +1
            double intStrength   = (defIntAvg   - 50.0) / 50.0;
            // Good perimeter D → fewer threes, more drives inside
            wThree  *= Math.Max(0.3, 1.0 - perimStrength * 0.25);
            wInside *= Math.Max(0.3, 1.0 + perimStrength * 0.15);
            // Good interior D → fewer inside shots/dunks, more threes
            wInside *= Math.Max(0.3, 1.0 - intStrength * 0.25);
            wDunk   *= Math.Max(0.3, 1.0 - intStrength * 0.30);
            wThree  *= Math.Max(0.3, 1.0 + intStrength * 0.15);

            // Shooter spacing bonus — more shooters on floor → more corner/wing threes
            wThree *= (0.7 + spacingLevel * 0.12);

            // Sag: sagging defenders help in the paint → fewer clean lanes inside
            if (teamSag > 0)
            {
                wInside *= Math.Max(0.35, 1.0 - teamSag * 0.55);
                wDunk   *= Math.Max(0.35, 1.0 - teamSag * 0.45);
            }
            // Gravity: defenders pinned on shooters → more open lanes inside
            if (teamGravity > 0)
            {
                wInside *= (1.0 + teamGravity * 0.35);
                wDunk   *= (1.0 + teamGravity * 0.25);
            }

            var shotType = WeightedRandom(
                new[] { ShotType.Inside, ShotType.Dunk, ShotType.MidRange, ShotType.ThreePointer },
                t => t switch
                {
                    ShotType.Inside      => wInside,
                    ShotType.Dunk        => wDunk,
                    ShotType.MidRange    => wMid,
                    ShotType.ThreePointer=> wThree,
                    _                    => 0.01
                });

            // ── Stage 6: Shot Context Selection ──────────────────────
            ShotContext shotContext;
            if (isOffReb && orbCount > 0)
            {
                // Putback or tip-in — always the rebounder
                actionPlayer = prevRebounder!;
                shotContext = _rng.NextDouble() < 0.6 ? ShotContext.Putback : ShotContext.TipIn;
                shotType = ShotType.Inside;
            }
            else
            {
                shotContext = shotType switch
                {
                    ShotType.Inside      => SelectInsideContext(actionPlayer, phase, spacingLevel),
                    ShotType.Dunk        => SelectDunkContext(actionPlayer, phase),
                    ShotType.MidRange    => SelectMidRangeContext(actionPlayer, phase),
                    ShotType.ThreePointer=> SelectThreeContext(actionPlayer, phase, spacingLevel),
                    _                    => ShotContext.DrivingLayup
                };
            }

            // ── Stage 7: Assist Roll ──────────────────────────────────
            double assistProb = AssistProbability(shotContext);
            string? assisterName = null;
            if (_rng.NextDouble() < assistProb)
            {
                var teammates = lineup.Where(p => p.Name != actionPlayer.Name).ToList();
                if (teammates.Count > 0)
                {
                    var assister = WeightedRandom(teammates, p => Math.Max(p.AssistWeight, 0.01));
                    assisterName = assister.Name;
                }
            }

            // ── Stage 8: Defense Response ─────────────────────────────
            var defender = GetMatchup(actionPlayer, defLineup);

            double contestMod = shotContext switch
            {
                ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing => 0.6,
                ShotContext.CutLayup or ShotContext.CutDunk or ShotContext.AlleyOop => 0.3,
                _ when phase == ShotClockPhase.BuzzerBeater => 0.5,
                _ => 1.0
            };

            double contestPenalty = defender.ContestPenalty(shotType) * contestMod;
            double blockability = shotType switch
            {
                ShotType.Inside      => 1.0,
                ShotType.Dunk        => 0.4,
                ShotType.MidRange    => 0.85,
                ShotType.ThreePointer=> 0.25,
                _                    => 0.5
            };
            double blockChance = defender.BlockMod * blockability * contestMod;

            // ── Stage 9: Outcome Roll ─────────────────────────────────
            double baseMake = shotType switch
            {
                ShotType.Inside      => actionPlayer.InsideMakePct,
                ShotType.Dunk        => actionPlayer.InsideMakePct * 1.08,
                ShotType.MidRange    => actionPlayer.MidRangeMakePct,
                ShotType.ThreePointer=> actionPlayer.ThreeMakePct,
                _                    => 0.45
            };

            // Context-specific overrides
            if (shotContext == ShotContext.PostMove)        baseMake = actionPlayer.InsideMakePct;
            if (shotContext == ShotContext.PostMoveMidRange) baseMake = actionPlayer.MidRangeMakePct;

            baseMake *= ShotContextMakeModifier(shotContext, phase);

            // Sag/gravity: perimeter shooting affects how open inside shots are
            if (shotType is ShotType.Inside or ShotType.Dunk)
            {
                if (teamSag     > 0) baseMake -= teamSag     * 0.30; // crowded paint
                if (teamGravity > 0) baseMake += teamGravity * 0.12; // open lanes
            }

            double adjMake = baseMake * (1 - actionPlayer.Fatigue * 0.12) - contestPenalty;

            if (state.IsClutch)
                adjMake *= 1.0 + (actionPlayer.Attr_BasketballIQ - 70) / 100.0 * 0.15;

            adjMake = Math.Clamp(adjMake, 0.05, 0.95);
            double r2 = _rng.NextDouble();

            bool isBlocked = r2 < blockChance;
            bool isMade    = !isBlocked && r2 < blockChance + adjMake;
            bool isThree   = shotType == ShotType.ThreePointer;

            // ── FG Attempt tracking ───────────────────────────────────
            var shooterStats = EnsureStats(actionPlayer);

            if (isBlocked)
            {
                // Blocked — still counts as attempt
                shooterStats.FGAttempts++;
                if (isThree) shooterStats.ThreeAttempts++;

                var blocker = WeightedRandom(defLineup, d => Math.Max(d.BlockMod, 0.01));
                EnsureStats(blocker).Blocks++;

                string narrative = BlockedNarrative(actionPlayer.Name, shotContext, blocker.Name);
                results.Add(new PossessionResult
                {
                    Team = offTeam.Name,
                    Narrative = narrative,
                    HomeScore = homeScore, AwayScore = awayScore,
                    Quarter = state.Quarter, ClockSeconds = clockSeconds,
                    PointsScored = 0,
                    Event = PossessionEvent.Blocked,
                    Scorer = actionPlayer.Name, Blocker = blocker.Name,
                    Shot = shotType, Context = shotContext, IsThree = isThree
                });

                // Rebound after block
                if (!HandleRebound(offTeam, defTeam, lineup, defLineup, state,
                        ref homeScore, ref awayScore, ref orbCount, ref prevRebounder, results, clockSeconds))
                    return;
            }
            else if (isMade)
            {
                shooterStats.FGMade++;
                shooterStats.FGAttempts++;
                int pts = isThree ? 3 : 2;
                if (isThree) { shooterStats.ThreeMade++; shooterStats.ThreeAttempts++; }
                shooterStats.Points += pts;

                if (assisterName != null) EnsureStats(assisterName).Assists++;

                if (state.IsHomePossession) homeScore += pts;
                else awayScore += pts;

                bool isGameWinner = posContext == PossessionContext.PotentialGameWinner;
                string narrative = MadeNarrative(actionPlayer.Name, shotContext, isThree, assisterName);
                results.Add(new PossessionResult
                {
                    Team = offTeam.Name,
                    Narrative = narrative,
                    HomeScore = homeScore, AwayScore = awayScore,
                    Quarter = state.Quarter, ClockSeconds = clockSeconds,
                    PointsScored = pts,
                    Event = isGameWinner ? PossessionEvent.GameWinnerMade : PossessionEvent.ShotMade,
                    Scorer = actionPlayer.Name, Assister = assisterName,
                    Shot = shotType, Context = shotContext, IsThree = isThree
                });

                // ── Stage 10: Free Throw Check (made contact shot) ────
                if (shotContext is ShotContext.DrivingLayup or ShotContext.PickAndRollRoll
                    or ShotContext.PostMove or ShotContext.PostMoveMidRange)
                {
                    double foulChance = 0.20 * (actionPlayer.Attr_Inside / 100.0);
                    if (_rng.NextDouble() < foulChance)
                    {
                        int ftPts = _rng.NextDouble() < actionPlayer.FTMakePct ? 1 : 0;
                        if (state.IsHomePossession) homeScore += ftPts;
                        else awayScore += ftPts;
                        shooterStats.FTAttempts++;
                        shooterStats.FTMade += ftPts;
                        shooterStats.Points += ftPts;
                        results.Add(new PossessionResult
                        {
                            Team = offTeam.Name,
                            Narrative = $"{actionPlayer.Name} and-one — {(ftPts == 1 ? "GOOD!" : "no good")}",
                            HomeScore = homeScore, AwayScore = awayScore,
                            Quarter = state.Quarter, ClockSeconds = clockSeconds,
                            PointsScored = ftPts,
                            Event = PossessionEvent.FreeThrows,
                            Scorer = actionPlayer.Name, Context = ShotContext.None,
                            FTAttempts = 1
                        });
                    }
                }

                return; // Possession ends
            }
            else
            {
                // Missed
                shooterStats.FGAttempts++;
                if (isThree) shooterStats.ThreeAttempts++;

                // ── Stage 10: Free Throw Check (missed contact shot) ──
                bool grantedFT = false;
                if (shotContext is ShotContext.DrivingLayup or ShotContext.PickAndRollRoll
                    or ShotContext.PostMove or ShotContext.PostMoveMidRange)
                {
                    if (_rng.NextDouble() < 0.12)
                    {
                        grantedFT = true;
                        // Void FG attempt
                        shooterStats.FGAttempts--;
                        if (isThree) shooterStats.ThreeAttempts--;

                        int pts = 0;
                        for (int i = 0; i < 2; i++)
                            if (_rng.NextDouble() < actionPlayer.FTMakePct) pts++;

                        if (state.IsHomePossession) homeScore += pts;
                        else awayScore += pts;

                        shooterStats.FTAttempts += 2;
                        shooterStats.FTMade += pts;
                        shooterStats.Points += pts;

                        string ftNarr = pts switch
                        {
                            2 => $"{actionPlayer.Name} fouled — makes both",
                            1 => $"{actionPlayer.Name} fouled — 1 of 2",
                            _ => $"{actionPlayer.Name} fouled — misses both"
                        };
                        results.Add(new PossessionResult
                        {
                            Team = offTeam.Name,
                            Narrative = ftNarr,
                            HomeScore = homeScore, AwayScore = awayScore,
                            Quarter = state.Quarter, ClockSeconds = clockSeconds,
                            PointsScored = pts,
                            Event = PossessionEvent.FreeThrows,
                            Scorer = actionPlayer.Name, Context = ShotContext.None,
                            FTAttempts = 2
                        });
                        return;
                    }
                }

                if (!grantedFT)
                {
                    string narrative = MissedNarrative(actionPlayer.Name, shotContext, isThree);
                    results.Add(new PossessionResult
                    {
                        Team = offTeam.Name,
                        Narrative = narrative,
                        HomeScore = homeScore, AwayScore = awayScore,
                        Quarter = state.Quarter, ClockSeconds = clockSeconds,
                        PointsScored = 0,
                        Event = PossessionEvent.ShotMissed,
                        Scorer = actionPlayer.Name,
                        Shot = shotType, Context = shotContext, IsThree = isThree
                    });

                    // ── Stage 11: Rebound ─────────────────────────────
                    if (!HandleRebound(offTeam, defTeam, lineup, defLineup, state,
                            ref homeScore, ref awayScore, ref orbCount, ref prevRebounder, results, clockSeconds))
                        return;
                }
            }
        }
    }

    // Returns true if offensive rebound (possession continues), false if defensive rebound (ends)
    private bool HandleRebound(
        Team offTeam, Team defTeam,
        List<Player> lineup, List<Player> defLineup,
        GameState state,
        ref int homeScore, ref int awayScore,
        ref int orbCount, ref Player? prevRebounder,
        List<PossessionResult> results,
        int clockSeconds)
    {
        double offRebWeight = lineup.Sum(p => p.ORebWeight);
        double defRebWeight = defLineup.Sum(p => p.DRebWeight);
        double offRebPct = Math.Clamp(offRebWeight / (offRebWeight + defRebWeight), 0.18, 0.38);

        if (_rng.NextDouble() < offRebPct && orbCount < 3)
        {
            var rebounder = WeightedRandom(lineup, p => Math.Max(p.ORebWeight, 0.01));
            orbCount++;
            prevRebounder = rebounder;
            EnsureStats(rebounder).OffRebounds++;
            EnsureStats(rebounder).Rebounds++;

            results.Add(new PossessionResult
            {
                Team = offTeam.Name,
                Narrative = $"{rebounder.Name} with the offensive rebound — second chance!",
                HomeScore = homeScore, AwayScore = awayScore,
                Quarter = state.Quarter, ClockSeconds = clockSeconds,
                PointsScored = 0,
                Event = PossessionEvent.OffensiveRebound,
                Rebounder = rebounder.Name, Context = ShotContext.None
            });
            return true; // continue possession
        }
        else
        {
            var rebounder = WeightedRandom(defLineup, p => Math.Max(p.DRebWeight, 0.01));
            EnsureStats(rebounder).DefRebounds++;
            EnsureStats(rebounder).Rebounds++;

            results.Add(new PossessionResult
            {
                Team = defTeam.Name,
                Narrative = $"{rebounder.Name} with the defensive rebound",
                HomeScore = homeScore, AwayScore = awayScore,
                Quarter = state.Quarter, ClockSeconds = clockSeconds,
                PointsScored = 0,
                Event = PossessionEvent.DefensiveRebound,
                Rebounder = rebounder.Name, Context = ShotContext.None
            });
            return false; // end possession
        }
    }

    private ShotContext SelectInsideContext(Player p, ShotClockPhase phase, int spacingLevel)
    {
        var weights = new Dictionary<ShotContext, double>
        {
            [ShotContext.DrivingLayup]   = Math.Max(p.DriveGravity * 2.0, 0.01),
            [ShotContext.PickAndRollRoll]= Math.Max(p.DriveGravity * spacingLevel * 0.25, 0.01),
            [ShotContext.CutLayup]       = Math.Max(p.CutTendency, 0.01),
            [ShotContext.FastBreakLayup] = phase == ShotClockPhase.Early ? 1.5 : 0.1,
            [ShotContext.PostMove]       = p.Position is Position.PF or Position.C
                                            ? Math.Max(p.Attr_Inside / 100.0 * 1.5, 0.01) : 0.1,
            [ShotContext.FloaterLayup]   = Math.Max(p.Speed / 100.0 * p.Attr_Dribbling / 100.0, 0.01),
        };
        return WeightedRandom(weights.Keys, k => weights[k]);
    }

    private ShotContext SelectDunkContext(Player p, ShotClockPhase phase)
    {
        var weights = new Dictionary<ShotContext, double>
        {
            [ShotContext.AlleyOop]        = Math.Max(p.AlleyOopTendency, 0.01),
            [ShotContext.CutDunk]         = Math.Max(p.CutTendency * 0.8, 0.01),
            [ShotContext.TransitionDunk]  = phase == ShotClockPhase.Early ? 2.0 : 0.15,
            [ShotContext.PickAndRollDunk] = Math.Max(p.DriveGravity * 0.5, 0.01),
        };
        return WeightedRandom(weights.Keys, k => weights[k]);
    }

    private ShotContext SelectMidRangeContext(Player p, ShotClockPhase phase)
    {
        var weights = new Dictionary<ShotContext, double>
        {
            [ShotContext.PullUpMidRange]     = Math.Max(p.DriveGravity * 0.8, 0.01),
            [ShotContext.IsolationMidRange]  = Math.Max(p.USG_Weight * (1 - p.DeferralTendency), 0.01),
            [ShotContext.PickAndRollMidRange]= Math.Max(p.DriveGravity * 0.5, 0.01),
            [ShotContext.FadeawayMidRange]   = Math.Max(p.Strength / 100.0 * 0.6, 0.01),
            [ShotContext.PostMoveMidRange]   = p.Position is Position.PF or Position.C
                                               ? Math.Max(p.Attr_MidRange / 100.0 * 1.2, 0.01) : 0.05,
            [ShotContext.LateClockMidRange]  = phase == ShotClockPhase.Late ? 2.0 : 0.01,
        };
        return WeightedRandom(weights.Keys, k => weights[k]);
    }

    private ShotContext SelectThreeContext(Player p, ShotClockPhase phase, int spacingLevel)
    {
        var weights = new Dictionary<ShotContext, double>
        {
            // Corner has a base weight independent of spacingLevel so average teams still hit corners (~17-20% of 3PA)
            [ShotContext.CatchAndShootCorner]= Math.Max(p.PerimeterGravity * (0.8 + spacingLevel * 0.15), 0.01),
            [ShotContext.CatchAndShootWing]  = Math.Max(p.PerimeterGravity * 1.2, 0.01),
            [ShotContext.PullUpThree]        = Math.Max(p.DriveGravity * p.Attr_ThreePoint / 100.0, 0.01),
            [ShotContext.StepBackThree]      = Math.Max(p.Attr_Dribbling / 100.0 * p.Attr_ThreePoint / 100.0, 0.01),
            [ShotContext.PickAndRollThree]   = Math.Max(p.DriveGravity * 0.6, 0.01),
            [ShotContext.TransitionThree]    = Math.Max(p.ShotClockAggressiveness * 0.5, 0.01),
        };
        return WeightedRandom(weights.Keys, k => weights[k]);
    }

    private static double AssistProbability(ShotContext context) => context switch
    {
        ShotContext.CatchAndShootCorner  => 0.92,
        ShotContext.CatchAndShootWing    => 0.88,
        ShotContext.AlleyOop             => 1.00,
        ShotContext.CutLayup             => 0.90,
        ShotContext.CutDunk              => 0.90,
        ShotContext.PickAndRollRoll      => 0.75,
        ShotContext.PickAndRollDunk      => 0.72,
        ShotContext.PickAndRollThree     => 0.68,
        ShotContext.PickAndRollMidRange  => 0.60,
        ShotContext.FastBreakLayup       => 0.55,
        ShotContext.TransitionDunk       => 0.50,
        ShotContext.TransitionThree      => 0.40,
        ShotContext.PostMove             => 0.22,
        ShotContext.PostMoveMidRange     => 0.20,
        ShotContext.FadeawayMidRange     => 0.12,
        ShotContext.IsolationMidRange    => 0.08,
        ShotContext.StepBackThree        => 0.10,
        ShotContext.PullUpThree          => 0.15,
        ShotContext.PullUpMidRange       => 0.18,
        ShotContext.DrivingLayup         => 0.30,
        ShotContext.FloaterLayup         => 0.22,
        ShotContext.LateClockMidRange    => 0.05,
        ShotContext.Putback              => 0.00,
        ShotContext.TipIn                => 0.00,
        _                               => 0.15
    };

    private static double ShotContextMakeModifier(ShotContext context, ShotClockPhase phase) => context switch
    {
        ShotContext.CatchAndShootCorner => 1.08,
        ShotContext.CatchAndShootWing   => 1.04,
        ShotContext.FastBreakLayup      => 1.10,
        ShotContext.AlleyOop            => 1.12,
        ShotContext.IsolationMidRange   => 0.94,
        ShotContext.StepBackThree       => 0.92,
        ShotContext.LateClockMidRange   => 0.88,
        ShotContext.FadeawayMidRange    => 0.90,
        ShotContext.PostMove            => 0.97,
        _ when phase == ShotClockPhase.BuzzerBeater => 0.20,
        _ => 1.0
    };

    private Player GetMatchup(Player shooter, List<Player> defenders)
    {
        // Match by position first, then fallback to highest relevant defender
        var samePos = defenders.Where(d => d.Position == shooter.Position).ToList();
        var pool = samePos.Count > 0 ? samePos : defenders;

        bool isPerimeter = shooter.Position is Position.PG or Position.SG or Position.SF;
        return isPerimeter
            ? pool.MaxBy(d => d.Attr_PerimeterDefense)!
            : pool.MaxBy(d => d.Attr_InteriorDefense)!;
    }

    private T WeightedRandom<T>(IEnumerable<T> items, Func<T, double> weightFn)
    {
        var list = items.ToList();
        double total = list.Sum(weightFn);
        double r = _rng.NextDouble() * total;
        double cumulative = 0;
        foreach (var item in list)
        {
            cumulative += weightFn(item);
            if (r <= cumulative) return item;
        }
        return list.Last();
    }

    private PlayerGameStats EnsureStats(Player p) => _stats[p.Name];

    private PlayerGameStats EnsureStats(string name) =>
        _stats.TryGetValue(name, out var s) ? s : throw new KeyNotFoundException(name);

    // ── Narrative Builders ─────────────────────────────────────────────────

    private string MadeNarrative(string scorer, ShotContext ctx, bool isThree, string? assister)
    {
        string assist = assister != null ? $" (Assist: {assister})" : "";
        string s = ctx switch
        {
            ShotContext.DrivingLayup      => Pick($"{scorer} drives — lays it in!{assist}", $"{scorer} attacks the rim — GOOD!{assist}"),
            ShotContext.PickAndRollRoll   => Pick($"{scorer} rolls to the rim — finishes!{assist}", $"{scorer} off the pick-and-roll — GOOD!{assist}"),
            ShotContext.CutLayup          => Pick($"{scorer} backdoor cut — lays it in!{assist}", $"{scorer} cuts to the rim — score!{assist}"),
            ShotContext.FastBreakLayup    => Pick($"{scorer} in transition — easy basket!{assist}", $"{scorer} beats everyone downcourt — lays it up!{assist}"),
            ShotContext.PostMove          => Pick($"{scorer} backs down in the post — lays it in!{assist}", $"{scorer} post move — good!{assist}"),
            ShotContext.FloaterLayup      => Pick($"{scorer} floater over the defense — GOOD!{assist}", $"{scorer} teardrop — splashes through!{assist}"),
            ShotContext.AlleyOop          => $"LOB to {scorer} — THROWS IT DOWN!{assist}",
            ShotContext.CutDunk           => Pick($"{scorer} cuts and DUNKS!{assist}", $"{scorer} jam on the cut!{assist}"),
            ShotContext.TransitionDunk    => Pick($"{scorer} SLAMS it in transition!{assist}", $"{scorer} throws it down on the break!{assist}"),
            ShotContext.PickAndRollDunk   => Pick($"{scorer} catches the lob off the roll — DUNK!{assist}", $"{scorer} oop off the pick — GOOD!{assist}"),
            ShotContext.PullUpMidRange    => Pick($"{scorer} pull-up mid-range — GOOD!", $"{scorer} stops and pops — hits it!"),
            ShotContext.IsolationMidRange => Pick($"{scorer} isolation — mid-range — GOOD!", $"{scorer} in the iso — knocks it down!"),
            ShotContext.PickAndRollMidRange=> Pick($"{scorer} mid-range off the pick — GOOD!{assist}", $"{scorer} pulls up off the PnR!{assist}"),
            ShotContext.FadeawayMidRange  => Pick($"{scorer} fadeaway — hits it!", $"{scorer} leans back — mid-range GOOD!"),
            ShotContext.PostMoveMidRange  => Pick($"{scorer} with the post move — mid-range GOOD!", $"{scorer} mid-post — drops it in!"),
            ShotContext.LateClockMidRange => Pick($"{scorer} pulls up late clock — GOOD!", $"{scorer} beats the shot clock — hits!"),
            ShotContext.CatchAndShootCorner=> Pick($"{scorer} catch-and-shoot from the corner — BANG!{assist}", $"{scorer} corner three — GOOD!{assist}"),
            ShotContext.CatchAndShootWing => Pick($"{scorer} catch-and-shoot from the wing — GOOD!{assist}", $"{scorer} wing three — BANG!{assist}"),
            ShotContext.PullUpThree       => Pick($"{scorer} pull-up three — GOOD!", $"{scorer} off the dribble from deep — knocks it!"),
            ShotContext.StepBackThree     => Pick($"{scorer} step-back three — SPLASH!", $"{scorer} creates space and drains the three!"),
            ShotContext.PickAndRollThree  => Pick($"{scorer} three off the PnR — GOOD!{assist}", $"{scorer} kicks back, three — BANG!{assist}"),
            ShotContext.TransitionThree   => Pick($"{scorer} fires in transition — three!{assist}", $"{scorer} ahead-of-the-defense three — GOOD!{assist}"),
            ShotContext.Putback           => $"{scorer} putback — GOOD!",
            ShotContext.TipIn             => $"{scorer} with the TIP-IN!",
            _                            => $"{scorer} — GOOD!{assist}"
        };
        return s;
    }

    private string MissedNarrative(string shooter, ShotContext ctx, bool isThree) => ctx switch
    {
        ShotContext.DrivingLayup       => Pick($"{shooter} drives — misses the layup", $"{shooter} at the rim — no good"),
        ShotContext.PickAndRollRoll    => $"{shooter} rolls to the rim — misses",
        ShotContext.CutLayup           => $"{shooter} cuts — layup rattles out",
        ShotContext.FastBreakLayup     => $"{shooter} in transition — misses the easy look",
        ShotContext.PostMove           => Pick($"{shooter} post move — off the backboard", $"{shooter} in the post — no good"),
        ShotContext.FloaterLayup       => $"{shooter} floater — no good",
        ShotContext.AlleyOop           => $"{shooter} can't finish the alley-oop — misses",
        ShotContext.CutDunk            => $"{shooter} goes up for the dunk — can't finish",
        ShotContext.TransitionDunk     => $"{shooter} misses the dunk in transition — rare!",
        ShotContext.PickAndRollDunk    => $"{shooter} can't finish the PnR dunk",
        ShotContext.PullUpMidRange     => Pick($"{shooter} pull-up mid-range — off the iron", $"{shooter} pull-up — no good"),
        ShotContext.IsolationMidRange  => Pick($"{shooter} isolation mid-range — no good", $"{shooter} iso — rattles out"),
        ShotContext.PickAndRollMidRange=> $"{shooter} mid-range off the PnR — no good",
        ShotContext.FadeawayMidRange   => $"{shooter} fadeaway — off the back rim",
        ShotContext.PostMoveMidRange   => $"{shooter} post-up mid-range — misses",
        ShotContext.LateClockMidRange  => $"{shooter} late clock shot — no good",
        ShotContext.CatchAndShootCorner=> Pick($"{shooter} corner three — no good", $"{shooter} misses from the corner"),
        ShotContext.CatchAndShootWing  => Pick($"{shooter} wing three — rattles out", $"{shooter} misses from the wing"),
        ShotContext.PullUpThree        => Pick($"{shooter} step-back three — rattles out", $"{shooter} pull-up three — no good"),
        ShotContext.StepBackThree      => Pick($"{shooter} step-back three — rattles out", $"{shooter} step-back — no good"),
        ShotContext.PickAndRollThree   => $"{shooter} three off the PnR — no good",
        ShotContext.TransitionThree    => $"{shooter} transition three — misses",
        _                             => $"{shooter} — no good"
    };

    private string BlockedNarrative(string shooter, ShotContext ctx, string blocker) => ctx switch
    {
        ShotContext.DrivingLayup or ShotContext.CutLayup or ShotContext.FastBreakLayup
            => Pick($"BLOCKED! {blocker} rejects {shooter} at the rim!", $"{blocker} stuffs {shooter}!"),
        ShotContext.AlleyOop
            => $"{blocker} SWATS the alley-oop attempt by {shooter}!",
        ShotContext.CutDunk or ShotContext.TransitionDunk or ShotContext.PickAndRollDunk
            => $"{blocker} rejects the dunk by {shooter}!",
        ShotContext.FloaterLayup
            => $"{blocker} swats the floater by {shooter}!",
        _   => $"{shooter}'s shot blocked by {blocker}!"
    };

    private string Pick(params string[] options) =>
        options[_rng.Next(options.Length)];
}
