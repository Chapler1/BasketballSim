using BasketballSim.Models;

namespace BasketballSim.Simulation;

public enum ShotClockPhase { Early, Mid, Late, BuzzerBeater }
public enum PossessionContext { Regular, PotentialGameWinner, IntentionalFoul, Heave }
public enum TurnoverType { Stolen, DeadBall }
public enum PossessionState { HalfCourt, Transition, FastBreak, SecondChance }

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
    int FinalAwayScore,
    IReadOnlyDictionary<string, int> PlayerFouls
);

public class GameEngine
{
    private readonly Random _rng = new();
    private Dictionary<string, PlayerGameStats> _stats = new();

    public bool DebugMode { get; set; }
    private PossessionState _possessionState = PossessionState.HalfCourt;
    private int _lastChainPasses;

    // ── Foul tracking ─────────────────────────────────────────────────────
    private int _homeFoulsQ;
    private int _awayFoulsQ;
    private Dictionary<string, int> _playerFouls = new();

    // ── Rotation / substitution state ─────────────────────────────────────
    private List<Player> _homeLineup = [];
    private List<Player> _awayLineup = [];
    private Dictionary<string, double> _homeTargetMin = new();
    private Dictionary<string, double> _awayTargetMin = new();
    private Dictionary<string, double> _homeGameMin = new();
    private Dictionary<string, double> _awayGameMin = new();
    private Dictionary<string, double> _homeStintMin = new();
    private Dictionary<string, double> _awayStintMin = new();
    private Dictionary<string, double> _homeEligibleAt = new();
    private Dictionary<string, double> _awayEligibleAt = new();
    private double _totalGameMinutes;

    // ── Nested MenuEntry type ─────────────────────────────────────────────
    private sealed class MenuEntry
    {
        public ShotContext Context;
        public ShotType    ShotType;
        public bool        IsDunk;
        public double      ActualPPS;
        public double      PerceivedPPS;
        public double      BaseMake;
        public double      ContestMod;
        public double      StyleMult;
        public double      ContextThreshold;
        public double      Weight;        // availWeight × styleMult — used for weighted selection
        public bool        IsPassOption;  // true when this entry represents passing to a teammate
        public Player?     Receiver;      // the teammate who would receive the pass (non-null when IsPassOption)
        public double      ContestRoll1 = 1.0; // first of two contest dice — rolled at menu generation, visible to player
    }

    public GameResult SimulateGame(Team homeTeam, Team awayTeam)
    {
        _stats = new Dictionary<string, PlayerGameStats>();
        _possessionState = PossessionState.HalfCourt;

        foreach (var p in homeTeam.Roster.Concat(awayTeam.Roster))
        {
            _stats[p.Name] = new PlayerGameStats
            {
                Name = p.Name,
                Team = p.Team,
                Position = p.Position
            };
        }

        foreach (var p in homeTeam.Roster.Concat(awayTeam.Roster))
            p.Energy = 100.0;
        _playerFouls = new Dictionary<string, int>();

        InitRotation(homeTeam, awayTeam);

        var results = new List<PossessionResult>(300);
        int homeScore = 0, awayScore = 0;
        int possessionsPerQuarter = (int)((homeTeam.Pace + awayTeam.Pace) / 2.0 / 4.0 * 1.09);

        for (int quarter = 1; quarter <= 4; quarter++)
        {
            _homeFoulsQ = 0;
            _awayFoulsQ = 0;

            if (quarter == 3)
            {
                foreach (var pl in homeTeam.Roster.Concat(awayTeam.Roster))
                    pl.RecoverEnergy(25.0);
                ResetQuarterBreakEligibility(homeTeam, awayTeam);
            }
            else if (quarter > 1)
            {
                foreach (var pl in homeTeam.Roster.Concat(awayTeam.Roster))
                    pl.RecoverEnergy(12.0);
                ResetQuarterBreakEligibility(homeTeam, awayTeam);
            }

            int clockSeconds = 720;
            bool homeFirst = quarter % 2 == 1;

            for (int p = 0; p < possessionsPerQuarter * 2; p++)
            {
                if (clockSeconds <= 0) break;

                bool isHome = (p % 2 == 0) == homeFirst;
                var state = new GameState(homeScore, awayScore, quarter, clockSeconds, isHome);
                var offTeam = isHome ? homeTeam : awayTeam;
                var defTeam = isHome ? awayTeam : homeTeam;

                _homeLineup = CheckSubstitutions(homeTeam, _homeLineup, quarter, clockSeconds,
                    homeScore - awayScore, _homeTargetMin, _homeGameMin, _homeStintMin, _homeEligibleAt);
                _awayLineup = CheckSubstitutions(awayTeam, _awayLineup, quarter, clockSeconds,
                    awayScore - homeScore, _awayTargetMin, _awayGameMin, _awayStintMin, _awayEligibleAt);

                var lineup    = isHome ? _homeLineup : _awayLineup;
                var defLineup = isHome ? _awayLineup : _homeLineup;

                int clockBefore   = clockSeconds;
                int resultsBefore = results.Count;
                int homeBefore    = homeScore, awayBefore = awayScore;
                SimulatePossessionChain(offTeam, defTeam, lineup, defLineup, state,
                    ref homeScore, ref awayScore, ref clockSeconds, results, ref _possessionState);

                int homePts = homeScore - homeBefore, awayPts = awayScore - awayBefore;
                foreach (var pl in _homeLineup) EnsureStats(pl).PlusMinus += homePts - awayPts;
                foreach (var pl in _awayLineup) EnsureStats(pl).PlusMinus += awayPts - homePts;

                int secondsElapsed = clockBefore - clockSeconds;
                if (results.Count > resultsBefore)
                {
                    var r = results[resultsBefore];
                    r.HomeLineup     = _homeLineup.Select(p => p.Name).ToList();
                    r.AwayLineup     = _awayLineup.Select(p => p.Name).ToList();
                    r.SecondsElapsed = secondsElapsed;
                    r.PassCount      = _lastChainPasses;
                }

                double minElapsed = secondsElapsed / 60.0;
                _totalGameMinutes += minElapsed;
                UpdateMinutesPlayed(_homeLineup, minElapsed, _homeGameMin, _homeStintMin);
                UpdateMinutesPlayed(_awayLineup, minElapsed, _awayGameMin, _awayStintMin);
            }
        }

        int otPeriod = 0;
        while (homeScore == awayScore)
        {
            otPeriod++;
            foreach (var pl in homeTeam.Roster.Concat(awayTeam.Roster))
                pl.RecoverEnergy(8.0);
            _homeFoulsQ = 0;
            _awayFoulsQ = 0;

            int clockSeconds = 300;
            for (int p = 0; p < 20 && clockSeconds > 0; p++)
            {
                bool isHome = p % 2 == 0;
                var state = new GameState(homeScore, awayScore, 4 + otPeriod, clockSeconds, isHome);
                var offTeam = isHome ? homeTeam : awayTeam;
                var defTeam = isHome ? awayTeam : homeTeam;

                _homeLineup = CheckSubstitutions(homeTeam, _homeLineup, 4 + otPeriod, clockSeconds,
                    homeScore - awayScore, _homeTargetMin, _homeGameMin, _homeStintMin, _homeEligibleAt);
                _awayLineup = CheckSubstitutions(awayTeam, _awayLineup, 4 + otPeriod, clockSeconds,
                    awayScore - homeScore, _awayTargetMin, _awayGameMin, _awayStintMin, _awayEligibleAt);

                var lineup    = isHome ? _homeLineup : _awayLineup;
                var defLineup = isHome ? _awayLineup : _homeLineup;

                int clockBefore   = clockSeconds;
                int resultsBefore = results.Count;
                int homeBefore    = homeScore, awayBefore = awayScore;
                SimulatePossessionChain(offTeam, defTeam, lineup, defLineup, state,
                    ref homeScore, ref awayScore, ref clockSeconds, results, ref _possessionState);

                int homePts = homeScore - homeBefore, awayPts = awayScore - awayBefore;
                foreach (var pl in _homeLineup) EnsureStats(pl).PlusMinus += homePts - awayPts;
                foreach (var pl in _awayLineup) EnsureStats(pl).PlusMinus += awayPts - homePts;

                int secondsElapsed = clockBefore - clockSeconds;
                if (results.Count > resultsBefore)
                {
                    var r = results[resultsBefore];
                    r.HomeLineup     = _homeLineup.Select(p => p.Name).ToList();
                    r.AwayLineup     = _awayLineup.Select(p => p.Name).ToList();
                    r.SecondsElapsed = secondsElapsed;
                    r.PassCount      = _lastChainPasses;
                }

                double minElapsed = secondsElapsed / 60.0;
                _totalGameMinutes += minElapsed;
                UpdateMinutesPlayed(_homeLineup, minElapsed, _homeGameMin, _homeStintMin);
                UpdateMinutesPlayed(_awayLineup, minElapsed, _awayGameMin, _awayStintMin);
            }
            if (otPeriod > 5) break;
        }

        return new GameResult(results, _stats, homeTeam, awayTeam, homeScore, awayScore, _playerFouls);
    }

    // ── Rotation helpers ──────────────────────────────────────────────────────

    private void InitRotation(Team homeTeam, Team awayTeam)
    {
        _homeLineup    = homeTeam.Starters.ToList();
        _awayLineup    = awayTeam.Starters.ToList();
        _homeTargetMin = RotationManager.ComputeTargetMinutes(homeTeam);
        _awayTargetMin = RotationManager.ComputeTargetMinutes(awayTeam);
        _homeGameMin   = homeTeam.Roster.ToDictionary(p => p.Name, _ => 0.0);
        _awayGameMin   = awayTeam.Roster.ToDictionary(p => p.Name, _ => 0.0);
        _homeStintMin  = homeTeam.Roster.ToDictionary(p => p.Name, _ => 0.0);
        _awayStintMin  = awayTeam.Roster.ToDictionary(p => p.Name, _ => 0.0);
        _homeEligibleAt = homeTeam.Roster.ToDictionary(p => p.Name, _ => 0.0);
        _awayEligibleAt = awayTeam.Roster.ToDictionary(p => p.Name, _ => 0.0);
        _totalGameMinutes = 0.0;
    }

    private void ResetQuarterBreakEligibility(Team homeTeam, Team awayTeam)
    {
        foreach (var p in homeTeam.Roster) _homeEligibleAt[p.Name] = 0.0;
        foreach (var p in awayTeam.Roster) _awayEligibleAt[p.Name] = 0.0;
        foreach (var p in _homeLineup) _homeStintMin[p.Name] = 0.0;
        foreach (var p in _awayLineup) _awayStintMin[p.Name] = 0.0;
    }

    private void UpdateMinutesPlayed(List<Player> lineup, double minutes,
        Dictionary<string, double> gameMin, Dictionary<string, double> stintMin)
    {
        foreach (var p in lineup)
        {
            gameMin[p.Name]  += minutes;
            stintMin[p.Name] += minutes;
            if (_stats.TryGetValue(p.Name, out var gs))
                gs.MinutesPlayed += minutes;
        }
    }

    private List<Player> CheckSubstitutions(
        Team team, List<Player> currentLineup, int quarter, int clockSeconds, int scoreDiff,
        Dictionary<string, double> targetMin, Dictionary<string, double> gameMin,
        Dictionary<string, double> stintMin, Dictionary<string, double> eligibleAt)
    {
        if (team.RotationDepth <= 5) return currentLineup;

        var newLineup = currentLineup.ToList();
        int depth = Math.Clamp(team.RotationDepth, 5, team.Roster.Count);

        var available = team.Roster
            .Take(depth)
            .Where(p => !newLineup.Contains(p) &&
                        _totalGameMinutes >= eligibleAt.GetValueOrDefault(p.Name))
            .OrderByDescending(p => RotationManager.ComputeOverall(p))
            .ToList();

        if (available.Count == 0) return newLineup;

        bool isClutch  = quarter >= 4 && clockSeconds <= 300 && Math.Abs(scoreDiff) <= 5;
        bool isGarbage = quarter >= 4 && clockSeconds <= 240 && Math.Abs(scoreDiff) > 15;

        for (int slot = 0; slot < 5; slot++)
        {
            if (available.Count == 0) break;

            var current = newLineup[slot];
            string name = current.Name;

            bool fouledOut = _playerFouls.GetValueOrDefault(name) >= 6;

            bool foulTrouble = !isClutch && quarter switch
            {
                1 => _playerFouls.GetValueOrDefault(name) >= 2,
                2 => _playerFouls.GetValueOrDefault(name) >= 3,
                3 => _playerFouls.GetValueOrDefault(name) >= 4,
                _ => false
            };

            double tgtMin   = targetMin.GetValueOrDefault(name, 24.0);
            double tgtStint = RotationManager.TargetStintMinutes(current, tgtMin);
            bool stintExpired = !isClutch && stintMin.GetValueOrDefault(name) >= tgtStint;
            bool exhausted    = !isClutch && gameMin.GetValueOrDefault(name)  >= tgtMin;

            bool garbageSub = isGarbage && slot >= 2 &&
                              available.Any(p => team.Roster.IndexOf(p) >= 7);

            bool needsSub = fouledOut || foulTrouble || stintExpired || exhausted || garbageSub;

            if (!needsSub) continue;

            Player? sub;
            if (fouledOut || foulTrouble)
            {
                sub = RotationManager.FindDesignatedBackup(current, available)
                      ?? available.FirstOrDefault();
            }
            else if (isClutch)
            {
                sub = available.OrderByDescending(p => RotationManager.ComputeOverall(p))
                               .FirstOrDefault();
            }
            else if (garbageSub)
            {
                sub = available.Where(p => team.Roster.IndexOf(p) >= 7)
                               .OrderByDescending(p => RotationManager.ComputeOverall(p))
                               .FirstOrDefault()
                      ?? available.FirstOrDefault();
            }
            else
            {
                sub = RotationManager.FindDesignatedBackup(current, available)
                      ?? available.FirstOrDefault();
            }

            if (sub == null) continue;

            newLineup[slot] = sub;
            available.Remove(sub);
            available.Add(current);

            stintMin[sub.Name] = 0.0;

            double restNeeded = Math.Clamp(tgtStint * 0.6, 3.0, 6.0);
            eligibleAt[name] = _totalGameMinutes + restNeeded;
        }

        return newLineup;
    }

    private void SimulatePossessionChain(
        Team offTeam, Team defTeam,
        List<Player> lineup, List<Player> defLineup,
        GameState state,
        ref int homeScore, ref int awayScore,
        ref int clockSeconds,
        List<PossessionResult> results,
        ref PossessionState possState)
    {
        int orbCount = 0;
        Player? prevRebounder = null;
        _lastChainPasses = 0;

        while (true)
        {
            foreach (var pl in lineup)    pl.DrainEnergy(isOffense: true);
            foreach (var pl in defLineup) pl.DrainEnergy(isOffense: false);

            // ── Stage 1: Possession Context ──────────────────────────

            // Heave: possession starts with under 2 seconds on the quarter clock
            if (clockSeconds <= 2)
            {
                bool isRealShot = clockSeconds == 2 && _rng.NextDouble() < 0.25;

                var heaveShooter  = WeightedRandom(lineup, p => p.Tendencies.Touches / 50.0);
                RecordBallTouch(heaveShooter, lineup);
                var heaveMatchups = GetMatchups(lineup, defLineup);
                var heaveDefender = heaveMatchups.TryGetValue(heaveShooter, out var hd) ? hd : defLineup[0];

                if (isRealShot)
                {
                    bool isThreeAttempt = heaveShooter.Attr_ThreePoint >= heaveShooter.Attr_MidRange;
                    double makePct = (isThreeAttempt ? heaveShooter.ThreeMakePct : heaveShooter.MidRangeMakePct)
                                     * 0.55 - heaveDefender.ContestPenalty(isThreeAttempt ? ShotType.ThreePointer : ShotType.MidRange);
                    makePct = Math.Clamp(makePct, 0.03, 0.40);

                    bool made = _rng.NextDouble() < makePct;
                    int pts   = made ? (isThreeAttempt ? 3 : 2) : 0;

                    if (made) { if (state.IsHomePossession) homeScore += pts; else awayScore += pts; }
                    if (made) EnsureStats(heaveShooter).FGMade++;
                    EnsureStats(heaveShooter).FGAttempts++;
                    EnsureStats(heaveShooter).ShotAttempts++;
                    RecordTeamFGA(lineup);
                    if (isThreeAttempt) { if (made) EnsureStats(heaveShooter).ThreeMade++; EnsureStats(heaveShooter).ThreeAttempts++; }
                    if (made) EnsureStats(heaveShooter).Points += pts;

                    var ctx = isThreeAttempt ? ShotContext.CatchAndShootWing : ShotContext.LateClockMidRange;
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = made
                            ? $"{heaveShooter.Name} gets it off in time — BANG! ({pts} pts)"
                            : $"{heaveShooter.Name} rushed shot at the buzzer — no good",
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = 0,
                        PointsScored = pts,
                        Event        = made ? PossessionEvent.ShotMade : PossessionEvent.ShotMissed,
                        Scorer       = heaveShooter.Name,
                        Shot         = isThreeAttempt ? ShotType.ThreePointer : ShotType.MidRange,
                        Context      = ctx,
                        IsThree      = isThreeAttempt
                    });
                }
                else
                {
                    double heaveMakePct = Math.Clamp(heaveShooter.ThreeMakePct * 0.18, 0.01, 0.08);
                    bool made = _rng.NextDouble() < heaveMakePct;
                    int pts   = made ? 3 : 0;

                    if (made) { if (state.IsHomePossession) homeScore += pts; else awayScore += pts; }
                    EnsureStats(heaveShooter).FGAttempts++;
                    EnsureStats(heaveShooter).ShotAttempts++;
                    RecordTeamFGA(lineup);
                    EnsureStats(heaveShooter).ThreeAttempts++;
                    if (made) { EnsureStats(heaveShooter).FGMade++; EnsureStats(heaveShooter).ThreeMade++; EnsureStats(heaveShooter).Points += pts; }

                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = made
                            ? $"{heaveShooter.Name} HEAVE FROM HALFCOURT — IT'S GOOD!!! ({pts} pts)"
                            : $"{heaveShooter.Name} heave at the buzzer — not even close",
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = 0,
                        PointsScored = pts,
                        Event        = made ? PossessionEvent.ShotMade : PossessionEvent.ShotMissed,
                        Scorer       = heaveShooter.Name,
                        Shot         = ShotType.ThreePointer,
                        Context      = ShotContext.None,
                        IsThree      = true
                    });
                }

                clockSeconds = 0;
                return;
            }

            int scoreDiff = state.IsHomePossession
                ? state.HomeScore - state.AwayScore
                : state.AwayScore - state.HomeScore;

            PossessionContext posContext;

            if (state.Quarter == 4 && clockSeconds <= 24 && Math.Abs(state.HomeScore - state.AwayScore) <= 3)
            {
                posContext = PossessionContext.PotentialGameWinner;
            }
            else if (
                (Math.Abs(state.HomeScore - state.AwayScore) is >= 4 and <= 6 && clockSeconds <= 48 && state.Quarter == 4) ||
                (Math.Abs(state.HomeScore - state.AwayScore) is >= 1 and <= 3 && clockSeconds <= 24 && state.Quarter == 4))
            {
                bool offenseIsAhead = scoreDiff > 0;
                posContext = offenseIsAhead ? PossessionContext.IntentionalFoul : PossessionContext.Regular;
            }
            else
            {
                posContext = PossessionContext.Regular;
            }

            // ── IntentionalFoul early-return ─────────────────────────
            if (posContext == PossessionContext.IntentionalFoul)
            {
                var ftShooter = WeightedRandom(lineup, p => p.Tendencies.Touches / 50.0);
                var intFouler = PickFouler(defLineup, null);
                CommitFoul(intFouler, !state.IsHomePossession);

                int pts = 0;
                for (int i = 0; i < 2; i++)
                    if (_rng.NextDouble() < ftShooter.FTMakePct) pts++;

                if (state.IsHomePossession) homeScore += pts;
                else awayScore += pts;

                EnsureStats(ftShooter).FTAttempts += 2;
                EnsureStats(ftShooter).FTMade     += pts;
                EnsureStats(ftShooter).Points     += pts;

                clockSeconds = Math.Max(0, clockSeconds - 3);

                results.Add(new PossessionResult
                {
                    Team         = offTeam.Name,
                    Narrative    = $"{intFouler.Name} fouls intentionally{FoulTag(intFouler)} — {ftShooter.Name} at the line, {pts}/2",
                    HomeScore    = homeScore, AwayScore = awayScore,
                    Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                    PointsScored = pts,
                    Event        = PossessionEvent.FreeThrows,
                    Scorer       = ftShooter.Name, Fouler = intFouler.Name,
                    Context      = ShotContext.None,
                    FTAttempts   = 2
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
                var tempAction = WeightedRandom(lineup, p => Math.Pow(p.Tendencies.Touches / 50.0, 1.2) * (0.75 + p.Energy / 400.0));
                double agg = tempAction.ShotClockAggressiveness;
                double r = _rng.NextDouble();
                if (r < agg)
                    phase = ShotClockPhase.Early;
                else if (r < agg + 0.55)
                    phase = ShotClockPhase.Mid;
                else
                    phase = ShotClockPhase.Late;
            }

            // timeUsed = pre-decision setup time (inbound, positioning, initial screens).
            // Shot execution time is tracked separately as execTime after the decision loop.
            int timeUsed = phase switch
            {
                ShotClockPhase.Early        => _rng.Next(4, 9),   // avg 6.5s (was 8–14)
                ShotClockPhase.Mid          => _rng.Next(3, 7),   // avg 5s   (was 5–10)
                ShotClockPhase.Late         => _rng.Next(1, 5),   // avg 3s   (was 2–8)
                ShotClockPhase.BuzzerBeater => _rng.Next(1, 4),
                _                           => 5
            };
            clockSeconds = Math.Max(0, clockSeconds - timeUsed);

            // Determine possession state first so shot clock starting value can reflect it.
            PossessionState curState = orbCount > 0 ? PossessionState.SecondChance : possState;

            // Shot clock remaining: varies by possession type with noise.
            // FastBreak ≈ 22–24 (full clock, running), HalfCourt ≈ 15–21, SecondChance ≈ 7–13.
            int shotClockRemaining = curState switch
            {
                PossessionState.FastBreak    => Math.Min(24, 23 + _rng.Next(-1, 2)),
                PossessionState.Transition   => Math.Min(24, 21 + _rng.Next(-2, 3)),
                PossessionState.SecondChance => Math.Max(4,  10 + _rng.Next(-3, 4)),
                _                            => 18 + _rng.Next(-3, 4)   // HalfCourt
            };

            // ── Stage 3: Select Action Player ────────────────────────
            Player actionPlayer;
            if (prevRebounder != null && orbCount > 0)
            {
                // Rebounder always has the ball — only they can putback/tip-in
                actionPlayer = prevRebounder;
            }
            else
            {
                var primaryBallhandler = lineup.MaxBy(p =>
                {
                    double score = (p.Attr_Dribbling + p.Attr_Passing) / 2.0;
                    double posMult = p.Position switch
                    {
                        Position.PG => 1.25,
                        Position.SG => 1.05,
                        _           => 0.88,
                    };
                    return score * posMult;
                });
                Func<Player, double> bhWeight = p =>
                {
                    double w = Math.Pow(p.Tendencies.Touches / 50.0, 1.2) * (0.75 + p.Energy / 400.0);
                    if (phase == ShotClockPhase.Late) w = Math.Pow(w, 1.4);
                    return Math.Max(w, 0.01);
                };
                // Primary BH gets ~60% of possession starts: othersSum * 1.5 / (othersSum * 1.5 + othersSum) = 0.60
                double othersSum = lineup.Where(p => p != primaryBallhandler).Sum(bhWeight);
                actionPlayer = WeightedRandom(lineup, p =>
                    p == primaryBallhandler ? othersSum * 1.5 : bhWeight(p));
            }

            actionPlayer.Energy = Math.Max(0.0, actionPlayer.Energy - 0.05);

            // ── Heliocentric override ─────────────────────────────────
            if (offTeam.Coach.OffStyle == OffensiveStyle.Heliocentric && _rng.NextDouble() < 0.80)
            {
                var star = lineup.MaxBy(p => p.Tendencies.Touches);
                if (star != null) actionPlayer = star;
            }

            RecordBallTouch(actionPlayer, lineup);

            // ── Stage 4a: Offensive foul (charge) ────────────────────
            double offFoulRate = 0.008 + actionPlayer.Attr_FoulTendency / 100.0 * 0.014;
            if (_rng.NextDouble() < offFoulRate)
            {
                CommitOffensiveFoul(actionPlayer);
                EnsureStats(actionPlayer).Turnovers++;
                results.Add(new PossessionResult
                {
                    Team         = offTeam.Name,
                    Narrative    = $"{actionPlayer.Name} called for the charge!{FoulTag(actionPlayer)}",
                    HomeScore    = homeScore, AwayScore = awayScore,
                    Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                    PointsScored = 0,
                    Event        = PossessionEvent.OffensiveFoul,
                    Scorer       = actionPlayer.Name, Fouler = actionPlayer.Name,
                    Context      = ShotContext.None
                });
                return;
            }

            // ── Stage 4: Turnover Check ───────────────────────────────
            double baseTO = actionPlayer.TurnoverRate;
            if (phase == ShotClockPhase.Late) baseTO *= 1.3;
            baseTO *= 1.0 - (offTeam.Coach.OffensiveRating - 50.0) / 50.0 * 0.12;

            if (_rng.NextDouble() < baseTO)
            {
                double totalStealThreat = defLineup.Sum(d => d.StealMod * (d.Tendencies.Steal / 50.0));
                double stealShare = totalStealThreat / (totalStealThreat + 0.12);

                EnsureStats(actionPlayer).Turnovers++;

                if (_rng.NextDouble() < stealShare)
                {
                    var stealer = WeightedRandom(defLineup, d => Math.Max(d.StealMod * (d.Tendencies.Steal / 50.0), 0.01));
                    EnsureStats(stealer).Steals++;
                    int clock2 = clockSeconds;
                    possState = PossessionState.FastBreak;
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = $"{offTeam.Abbreviation} — {actionPlayer.Name} stolen by {stealer.Name}!",
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = clock2,
                        PointsScored = 0,
                        Event        = PossessionEvent.TurnoverStolen,
                        Scorer       = actionPlayer.Name,
                        Stealer      = stealer.Name,
                        Context      = ShotContext.None
                    });
                }
                else
                {
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = $"{offTeam.Abbreviation} — {actionPlayer.Name} turnover — out of bounds",
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                        PointsScored = 0,
                        Event        = PossessionEvent.TurnoverDeadBall,
                        Scorer       = actionPlayer.Name,
                        Context      = ShotContext.None
                    });
                }
                return;
            }

            // ── Stage 4.5: Non-shooting defensive foul ────────────────
            if (_rng.NextDouble() < 0.075)
            {
                bool isHomeFouling  = !state.IsHomePossession;
                bool offenseInBonus = state.IsHomePossession ? _awayFoulsQ >= 5 : _homeFoulsQ >= 5;

                var fouler = PickFouler(defLineup, null);
                CommitFoul(fouler, isHomeFouling);

                int defTeamFouls = isHomeFouling ? _homeFoulsQ : _awayFoulsQ;

                if (offenseInBonus)
                {
                    int pts = 0;
                    for (int i = 0; i < 2; i++)
                        if (_rng.NextDouble() < actionPlayer.FTMakePct) pts++;

                    if (state.IsHomePossession) homeScore += pts;
                    else awayScore += pts;

                    EnsureStats(actionPlayer).FTAttempts += 2;
                    EnsureStats(actionPlayer).FTMade     += pts;
                    EnsureStats(actionPlayer).Points     += pts;

                    string bonusNarr = pts switch
                    {
                        2 => $"{fouler.Name} fouls{FoulTag(fouler)} — {actionPlayer.Name} in the bonus, makes both",
                        1 => $"{fouler.Name} fouls{FoulTag(fouler)} — {actionPlayer.Name} in the bonus, 1 of 2",
                        _ => $"{fouler.Name} fouls{FoulTag(fouler)} — {actionPlayer.Name} in the bonus, misses both"
                    };
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = bonusNarr,
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                        PointsScored = pts,
                        Event        = PossessionEvent.FreeThrows,
                        Scorer       = actionPlayer.Name, Fouler = fouler.Name,
                        Context      = ShotContext.None, FTAttempts = 2
                    });
                    return;
                }
                else
                {
                    string bonusWarn = defTeamFouls == 4
                        ? $" — {defTeam.Name} one foul from the bonus!"
                        : "";
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = $"{fouler.Name} fouls{FoulTag(fouler)} — ball retained{bonusWarn}",
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                        PointsScored = 0,
                        Event        = PossessionEvent.NonShootingFoul,
                        Scorer       = actionPlayer.Name, Fouler = fouler.Name,
                        Context      = ShotContext.None
                    });
                    orbCount      = 0;
                    prevRebounder = null;
                    continue;
                }
            }

            // ── Pre-menu setup ────────────────────────────────────────
            int spacingLevel = offTeam.SpacingLevel(lineup);

            double netPerimPressure = lineup.Average(p =>
                (p.Attr_ThreePoint * 0.6 + p.Attr_MidRange * 0.4 - 50.0) / 50.0);

            double teamSag     = Math.Max(0.0, -netPerimPressure);
            double teamGravity = Math.Max(0.0,  netPerimPressure);

            // ── Stage 5-6: PPS Menu + Threshold Pass Loop ─────────────────────
            double clockDecay = Math.Clamp(0.75 + (shotClockRemaining / 22.0) * 0.25, 0.75, 1.00);

            // PotentialGameWinner: bias clock to near end
            if (posContext == PossessionContext.PotentialGameWinner)
            {
                int deficit = -scoreDiff;
                // We'll override shot type via menu later; just update clock
                timeUsed = _rng.Next(Math.Max(1, clockSeconds - 4), clockSeconds + 1);
                clockSeconds = Math.Max(0, clockSeconds - timeUsed);
                shotClockRemaining = Math.Min(shotClockRemaining, Math.Max(0, clockSeconds));
                clockDecay   = Math.Clamp(0.75 + (shotClockRemaining / 22.0) * 0.25, 0.75, 1.00);
            }

            Player ballHandler = actionPlayer;
            string? assisterName = null;
            int passesThisPossession = 0;

            // ── Possession Snapshot: generate menus for all 5 players ─────────────
            var allMatchups = GetMatchups(lineup, defLineup);
            int snapshotClock     = clockSeconds;       // capture — ref params can't be used in lambdas
            int snapshotShotClock = shotClockRemaining; // capture local copy for lambda
            Player? snapshotRebounder = prevRebounder;  // capture ref param for lambda
            var playerMenus = lineup.ToDictionary(
                p => p,
                p =>
                {
                    // On a second-chance possession only the rebounder has the ball —
                    // everyone else gets an empty menu so they can't "tip in" a rebound they didn't grab.
                    if (curState == PossessionState.SecondChance
                        && snapshotRebounder != null
                        && p.Name != snapshotRebounder.Name)
                        return new List<MenuEntry>();

                    return GenerateMenu(p, lineup, defLineup, allMatchups, curState, offTeam, defTeam,
                                        spacingLevel, teamSag, teamGravity, clockDecay, phase,
                                        snapshotClock, snapshotShotClock);
                });

            ShotContext shotContext;
            ShotType    shotType;
            bool        isDunkContext;
            double      knownContestRoll = 1.0; // ContestRoll1 of the chosen shot — set when player decides to shoot

            // Debug info for the whole decision chain (populated when DebugMode == true)
            DecisionDebugInfo? dbg = DebugMode
                ? new DecisionDebugInfo { ClockSeconds = clockSeconds, PossessionState = (BasketballSim.Models.PossessionState)curState }
                : null;

            // ── Vision-Driven Decision Loop ───────────────────────────────────────
            while (true)
            {
                // Noise floor of 0.08 ensures even elite IQ players occasionally misread a shot;
                // low-IQ players have much wider error (~0.35 stddev ≈ can confuse .70 for 1.05).
                double bbiqStddev = 0.08 + 0.27 * (1.0 - ballHandler.Attr_BasketballIQ / 100.0);
                if (offTeam.Coach.OffStyle == OffensiveStyle.MotionFlow) bbiqStddev *= 0.6;

                // Own shots — 100% visible, apply BBIQ noise
                var combinedMenu = new List<MenuEntry>();
                foreach (var e in playerMenus[ballHandler])
                {
                    e.PerceivedPPS = e.ActualPPS + NextGaussian() * bbiqStddev;
                    combinedMenu.Add(e);
                }

                // Pass-option discovery: per-entry roll for each teammate's shots.
                // Blocked on second-chance possessions — putback/tip-in must be taken immediately.
                // Linear base up to Passing=80; elite passers (80+) get an accelerating bonus
                // so vision scales meaningfully: 5→~1%, 50→~15%, 80→~28%, 95→~50%.
                double discoveryRate   = Math.Clamp(
                    ballHandler.Attr_Passing * 0.0055 - 0.090
                    + Math.Max(0, (ballHandler.Attr_Passing - 80) * 0.013),
                    0.01, 0.55);
                double actualPassTO    = PassTurnoverRate(ballHandler);
                double perceivedPassTO = Math.Clamp(
                    actualPassTO + NextGaussian() * bbiqStddev * 0.5, 0.01, 0.30);

                bool canDiscover = curState != PossessionState.SecondChance;
                foreach (var teammate in lineup.Where(p => p != ballHandler && canDiscover))
                {
                    foreach (var entry in playerMenus[teammate])
                    {
                        if (_rng.NextDouble() >= discoveryRate) continue;

                        combinedMenu.Add(new MenuEntry
                        {
                            Context          = entry.Context,
                            ShotType         = entry.ShotType,
                            IsDunk           = entry.IsDunk,
                            ActualPPS        = entry.ActualPPS * (1.0 - actualPassTO),
                            PerceivedPPS     = entry.ActualPPS * (1.0 - perceivedPassTO)
                                               + NextGaussian() * bbiqStddev,
                            BaseMake         = entry.BaseMake,
                            ContestMod       = entry.ContestMod,
                            StyleMult        = entry.StyleMult,
                            ContextThreshold = entry.ContextThreshold,
                            Weight           = entry.Weight,
                            IsPassOption     = true,
                            Receiver         = teammate
                        });
                    }
                }

                bool desperateClock = clockSeconds < 4;

                // First touch in half-court: raise threshold so the primary ball-handler
                // almost always initiates (dribble-up, survey, pass) rather than shooting off the catch.
                // Fast break and transition possessions are excluded — those demand quick decisions.
                double firstTouchMult = (passesThisPossession == 0 && curState == PossessionState.HalfCourt)
                    ? 1.45
                    : 1.0;

                // Filter to qualifying options (above context threshold, or desperate clock)
                var qualifying = desperateClock
                    ? combinedMenu
                    : combinedMenu.Where(e => e.PerceivedPPS >= e.ContextThreshold * firstTouchMult).ToList();

                // Build debug step (populated after decision below)
                DecisionStep? dbgStep = dbg != null
                    ? new DecisionStep { BallHandler = ballHandler.Name, ClockDecay = clockDecay }
                    : null;
                if (dbg != null) dbg.Steps.Add(dbgStep!);

                // PotentialGameWinner: prefer three-pointers
                if (posContext == PossessionContext.PotentialGameWinner && -scoreDiff >= 3)
                {
                    // First try: own three-pointers above threshold
                    var ownThrees = qualifying.Where(e => e.ShotType == ShotType.ThreePointer && !e.IsPassOption).ToList();
                    if (ownThrees.Count > 0)
                    {
                        qualifying = ownThrees;
                    }
                    else
                    {
                        // Include pass-options to three-pointers too
                        var allThrees = qualifying.Where(e => e.ShotType == ShotType.ThreePointer).ToList();
                        if (allThrees.Count > 0) qualifying = allThrees;
                        else qualifying = combinedMenu.Where(e => e.ShotType == ShotType.ThreePointer).ToList();
                        if (qualifying.Count == 0) qualifying = combinedMenu;
                    }
                }

                // Best perceived option
                var best = qualifying.Count > 0 ? qualifying.MaxBy(e => e.PerceivedPPS) : null;

                // ── Decision: Pass / Shoot / Blind Pass ───────────────────────────

                bool doPass        = best != null && best.IsPassOption && !desperateClock;
                bool doShoot       = best != null && (!best.IsPassOption || desperateClock);
                Player passReceiver = doPass ? best!.Receiver! : lineup.First(); // temp; set properly below

                if (doShoot)
                {
                    // ── Take the shot ──────────────────────────────────────────────
                    var chosen = best!.IsPassOption ? best : best; // already self-shot
                    // If desperate and best is a pass option, take own best self-shot instead
                    if (best.IsPassOption)
                        chosen = combinedMenu.Where(e => !e.IsPassOption).MaxBy(e => e.PerceivedPPS)
                                 ?? combinedMenu.MaxBy(e => e.PerceivedPPS)!;

                    shotContext      = chosen.Context;
                    shotType        = chosen.ShotType;
                    isDunkContext   = chosen.IsDunk;
                    knownContestRoll = chosen.ContestRoll1;

                    if (dbgStep != null)
                    {
                        foreach (var e in combinedMenu.OrderByDescending(e => e.PerceivedPPS))
                            dbgStep.Menu.Add(new MenuDebugEntry
                            {
                                Context = e.Context, ShotType = e.ShotType,
                                ActualPPS = e.ActualPPS, PerceivedPPS = e.PerceivedPPS,
                                Threshold = e.ContextThreshold, BaseMake = e.BaseMake,
                                ContestMod = e.ContestMod, StyleMult = e.StyleMult,
                                WasChosen = e == chosen,
                                IsPassOption = e.IsPassOption, ReceiverName = e.Receiver?.Name
                            });
                        dbgStep.Action = $"Shot: {chosen.Context}";
                    }

                    if (assisterName != null)
                    {
                        var assisterPlayer = lineup.FirstOrDefault(p => p.Name == assisterName);
                        double passerMult  = assisterPlayer != null
                            ? Math.Clamp(1.0 + (assisterPlayer.Attr_Passing - 50) / 50.0 * 0.30, 0.70, 1.30)
                            : 1.0;
                        if (_rng.NextDouble() >= AssistProbability(shotContext) * passerMult)
                            assisterName = null;
                    }
                    break;
                }

                // ── Execute a pass (smart or blind) ───────────────────────────────
                bool isBlindPass = !doPass;
                if (!doPass)
                {
                    // Blind pass: no qualifying options — pick by usage weight
                    var candidates = lineup.Where(p => p != ballHandler).ToList();
                    passReceiver = WeightedRandom(candidates,
                        p => Math.Max(Math.Pow(p.Tendencies.Touches / 50.0, 1.2)
                                      * (0.75 + p.Energy / 400.0), 0.01));
                }
                else
                {
                    passReceiver = best!.Receiver!;
                }

                // Capture debug menu for this decision step (pass or blind pass)
                if (dbgStep != null)
                {
                    foreach (var e in combinedMenu.OrderByDescending(e => e.PerceivedPPS))
                        dbgStep.Menu.Add(new MenuDebugEntry
                        {
                            Context = e.Context, ShotType = e.ShotType,
                            ActualPPS = e.ActualPPS, PerceivedPPS = e.PerceivedPPS,
                            Threshold = e.ContextThreshold, BaseMake = e.BaseMake,
                            ContestMod = e.ContestMod, StyleMult = e.StyleMult,
                            WasChosen = !isBlindPass && e == best,
                            IsPassOption = e.IsPassOption, ReceiverName = e.Receiver?.Name
                        });
                    dbgStep.Action = isBlindPass
                        ? $"Blind Pass → {passReceiver.Name}"
                        : $"Pass → {passReceiver.Name}";
                }

                // 1. Drain clock for pass
                int passTime = Math.Max(1, (int)Math.Round(NextGaussian(1.8, 0.6)));
                clockSeconds       = Math.Max(0, clockSeconds - passTime);
                shotClockRemaining = Math.Max(0, shotClockRemaining - passTime);
                clockDecay         = Math.Clamp(0.75 + (shotClockRemaining / 22.0) * 0.25, 0.75, 1.00);

                if (clockSeconds <= 0)
                {
                    EnsureStats(ballHandler).Turnovers++;
                    if (dbgStep != null) dbgStep.Action = "TO: shot clock violation";
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = $"{offTeam.Abbreviation} — shot clock violation",
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = 0,
                        PointsScored = 0,
                        Event        = PossessionEvent.TurnoverDeadBall,
                        Scorer       = ballHandler.Name, Context = ShotContext.None,
                        Debug        = dbg
                    });
                    return;
                }

                // 2. MotionFlow interception check (applies to all passes for MotionFlow teams)
                if (offTeam.Coach.OffStyle == OffensiveStyle.MotionFlow)
                {
                    double totalStealThreatPass = defLineup.Sum(d => d.StealMod * (d.Tendencies.Steal / 50.0));
                    double passStealChance = totalStealThreatPass / (totalStealThreatPass + 0.20) * 0.35;
                    if (_rng.NextDouble() < passStealChance)
                    {
                        var stealer = WeightedRandom(defLineup,
                            d => Math.Max(d.StealMod * (d.Tendencies.Steal / 50.0), 0.01));
                        EnsureStats(ballHandler).Turnovers++;
                        EnsureStats(stealer).Steals++;
                        possState = PossessionState.FastBreak;
                        if (dbgStep != null) dbgStep.Action = $"TO: intercepted by {stealer.Name}";
                        results.Add(new PossessionResult
                        {
                            Team         = offTeam.Name,
                            Narrative    = $"{offTeam.Abbreviation} — {ballHandler.Name} pass intercepted by {stealer.Name}!",
                            HomeScore    = homeScore, AwayScore = awayScore,
                            Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                            PointsScored = 0,
                            Event        = PossessionEvent.TurnoverStolen,
                            Scorer       = ballHandler.Name, Stealer = stealer.Name,
                            Context      = ShotContext.None,
                            Debug        = dbg
                        });
                        return;
                    }
                }

                // 3. Universal pass turnover check (2–8% based on Passing attribute)
                if (_rng.NextDouble() < actualPassTO)
                {
                    // Determine stolen vs dead ball using defender positions
                    var recipientDef = allMatchups.TryGetValue(passReceiver, out var rd) ? rd : defLineup[0];
                    var passerDef    = allMatchups.TryGetValue(ballHandler,  out var pd) ? pd : defLineup[0];

                    double recipientDefThreat = recipientDef.StealMod * (recipientDef.Tendencies.Steal / 50.0) * 2.0;
                    double passerDefThreat    = passerDef.StealMod    * (passerDef.Tendencies.Steal    / 50.0) * 1.0;
                    double otherThreat        = defLineup
                        .Where(d => d != recipientDef && d != passerDef)
                        .Sum(d => d.StealMod * (d.Tendencies.Steal / 50.0) * 0.3);

                    double totalPassThreat = recipientDefThreat + passerDefThreat + otherThreat;
                    double stealShare      = totalPassThreat / (totalPassThreat + 0.12);

                    EnsureStats(ballHandler).Turnovers++;

                    if (_rng.NextDouble() < stealShare)
                    {
                        // Stolen — weighted by individual threat
                        var stealWeights = defLineup.ToDictionary(d => d, d =>
                        {
                            double threat = d.StealMod * (d.Tendencies.Steal / 50.0);
                            if (d == recipientDef) threat *= 2.0;
                            else if (d != passerDef) threat *= 0.3;
                            return Math.Max(threat, 0.001);
                        });
                        var stealer = WeightedRandom(defLineup, d => stealWeights[d]);
                        EnsureStats(stealer).Steals++;
                        possState = PossessionState.FastBreak;
                        if (dbgStep != null) dbgStep.Action = $"TO: stolen by {stealer.Name}";
                        results.Add(new PossessionResult
                        {
                            Team         = offTeam.Name,
                            Narrative    = $"{offTeam.Abbreviation} — {ballHandler.Name} pass stolen by {stealer.Name}!",
                            HomeScore    = homeScore, AwayScore = awayScore,
                            Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                            PointsScored = 0,
                            Event        = PossessionEvent.TurnoverStolen,
                            Scorer       = ballHandler.Name, Stealer = stealer.Name,
                            Context      = ShotContext.None,
                            Debug        = dbg
                        });
                    }
                    else
                    {
                        if (dbgStep != null) dbgStep.Action = "TO: errant pass — out of bounds";
                        results.Add(new PossessionResult
                        {
                            Team         = offTeam.Name,
                            Narrative    = $"{offTeam.Abbreviation} — {ballHandler.Name} errant pass — out of bounds",
                            HomeScore    = homeScore, AwayScore = awayScore,
                            Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                            PointsScored = 0,
                            Event        = PossessionEvent.TurnoverDeadBall,
                            Scorer       = ballHandler.Name,
                            Context      = ShotContext.None,
                            Debug        = dbg
                        });
                    }
                    return;
                }

                // 4. Pass successful — update state
                _lastChainPasses++;
                passesThisPossession++;
                assisterName = ballHandler.Name;

                // Regenerate menus for all players except the receiver (defense rotated)
                clockDecay = Math.Clamp(0.75 + (shotClockRemaining / 22.0) * 0.25, 0.75, 1.00);
                foreach (var p in lineup.Where(p => p != passReceiver))
                {
                    playerMenus[p] = GenerateMenu(p, lineup, defLineup, allMatchups, curState,
                        offTeam, defTeam, spacingLevel, teamSag, teamGravity,
                        clockDecay, phase, clockSeconds, shotClockRemaining);
                }

                ballHandler = passReceiver;
                RecordBallTouch(ballHandler, lineup);
            }

            // actionPlayer is now the shooter
            actionPlayer = ballHandler;

            // ── Shot execution time ────────────────────────────────────
            // After the decision the play still takes live clock: a drive
            // to the rim, setting feet on a pull-up, or posting up all
            // consume game-clock seconds that weren't captured in pass time.
            int execTime = shotContext switch
            {
                // Quick release — catch-and-shoot, fastbreak, open cut
                ShotContext.FastBreakLayup or ShotContext.TransitionDunk
                    or ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing
                    or ShotContext.CutLayup or ShotContext.CutDunk
                    or ShotContext.AlleyOop                               => _rng.Next(1, 3),  // 1–2 s
                // Post/isolation — deliberate setup, multiple fakes
                ShotContext.PostMove or ShotContext.PostMoveMidRange
                    or ShotContext.FadeawayMidRange
                    or ShotContext.IsolationMidRange                      => _rng.Next(3, 6),  // 3–5 s
                // Everything else — drive, pull-up, P&R, step-back
                _                                                         => _rng.Next(2, 4)   // 2–3 s
            };
            clockSeconds = Math.Max(0, clockSeconds - execTime);

            // ── Stage 7: Assist Attribution ───────────────────────────
            if (assisterName == null)  // no tracked passer; try organic assist roll
            {
                var teammates = lineup.Where(p => p.Name != actionPlayer.Name).ToList();
                if (teammates.Count > 0)
                {
                    double assistProb = AssistProbability(shotContext);
                    // Scale by best passer's ability so that Passing=95→~45%, 50→~20%, 5→~5%
                    double bestPassing = teammates.Max(p => p.Attr_Passing);
                    assistProb *= Math.Clamp(0.18 + 0.68 * Math.Pow(bestPassing / 50.0, 1.5), 0.05, 2.5);
                    if (_rng.NextDouble() < assistProb)
                    {
                        var assister = WeightedRandom(teammates, p => Math.Max(p.AssistWeight, 0.01));
                        assisterName = assister.Name;
                    }
                }
            }
            // else: assisterName is set from the tracked pass — already filtered in the loop

            // ── Stage 8: Defense Response ─────────────────────────────
            var defender = allMatchups.TryGetValue(actionPlayer, out var am) ? am : defLineup[0];

            double contestMod = shotContext switch
            {
                // Very open inside shots — defender is trailing or out of position
                ShotContext.FastBreakLayup or ShotContext.CutLayup or ShotContext.CutDunk
                    or ShotContext.AlleyOop or ShotContext.TransitionDunk                => 0.15,
                // Hard-contested inside shots — defender is set and in the way
                ShotContext.DrivingLayup or ShotContext.PostMove
                    or ShotContext.FloaterLayup or ShotContext.PickAndRollRoll            => 1.15,
                // P&R roll — somewhat open, help has to rotate
                ShotContext.PickAndRollDunk                                              => 0.45,
                // Barreling through contact
                ShotContext.ContactDunk                                                  => 0.85,
                // Catch-and-shoot — defender closing out, not set
                ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing         => 0.6,
                // Late-clock desperation
                _ when phase == ShotClockPhase.BuzzerBeater                             => 0.5,
                _                                                                        => 1.0
            };

            double blockTendFactor = Math.Clamp(defender.Tendencies.Block / 50.0, 0.4, 2.0);
            // Second contest die — rolled after the decision is made (what the defender actually does).
            // Final intensity = average of the pre-decision read (roll1) and this post-decision roll.
            double contestRoll2   = Math.Clamp(NextGaussian(1.0, 0.30), 0.15, 1.85);
            double contestPenalty  = defender.ContestPenalty(shotType) * contestMod
                * Math.Clamp(1.5 - blockTendFactor * 0.5, 0.6, 1.2)
                * (knownContestRoll + contestRoll2) / 2.0;

            isDunkContext = shotContext is ShotContext.AlleyOop or ShotContext.CutDunk
                                       or ShotContext.TransitionDunk or ShotContext.PickAndRollDunk
                                       or ShotContext.ContactDunk or ShotContext.Putback;
            double blockability = shotType switch
            {
                ShotType.Inside       => isDunkContext ? 0.35 : 1.0,
                ShotType.MidRange     => 0.85,
                ShotType.ThreePointer => 0.25,
                _                     => 0.5
            };

            // On-ball block: shot-type specific attributes.
            // Inside = IntDef + Height/Jumping; Mid = blended; Three = PerimDef.
            double onBallBlockBase = shotType switch
            {
                ShotType.Inside =>
                    Math.Pow(defender.Attr_InteriorDefense / 100.0, 2.2) * 0.250
                    + Math.Max(0.0, (defender.Height + defender.Jumping - 100) / 1600.0),
                ShotType.MidRange =>
                    Math.Pow((defender.Attr_InteriorDefense * 0.6 + defender.Attr_PerimeterDefense * 0.4) / 100.0, 2.2) * 0.103,
                ShotType.ThreePointer =>
                    Math.Pow(defender.Attr_PerimeterDefense / 100.0, 2.2) * 0.046,
                _ => 0.0
            };
            double blockChance = onBallBlockBase * blockability * contestMod * blockTendFactor
                                 * defender.EnergyFactor_Physical;

            // ── Stage 9: Outcome Roll ─────────────────────────────────
            double baseMake = shotType switch
            {
                ShotType.Inside       => actionPlayer.InsideMakePct,
                ShotType.MidRange     => actionPlayer.MidRangeMakePct,
                ShotType.ThreePointer => actionPlayer.ThreeMakePct,
                _                     => 0.45
            };

            if (shotContext == ShotContext.PostMove)         baseMake = actionPlayer.InsideMakePct;
            if (shotContext == ShotContext.PostMoveMidRange)  baseMake = actionPlayer.MidRangeMakePct;
            if (isDunkContext && shotContext != ShotContext.ContactDunk)
                baseMake = actionPlayer.DunkMakePct;
            if (shotContext == ShotContext.ContactDunk)
                baseMake = actionPlayer.ContactDunkMakePct;

            baseMake *= ShotContextMakeModifier(shotContext, phase);

            // Sag/gravity: perimeter shooting affects how open inside shots are
            if (shotType == ShotType.Inside)
            {
                if (teamSag     > 0) baseMake -= teamSag     * 0.42;
                if (teamGravity > 0) baseMake += teamGravity * 0.16;
            }

            double adjMake = baseMake - contestPenalty;

            if (state.IsClutch)
                adjMake *= 1.0 + (actionPlayer.Attr_BasketballIQ - 70) / 100.0 * 0.15;

            adjMake += (offTeam.Coach.OffensiveRating - 50.0) / 50.0 * 0.013;

            double defCoachMod = 0.80 + defTeam.Coach.DefensiveRating / 100.0 * 0.40;
            adjMake -= contestPenalty * (defCoachMod - 1.0);

            adjMake = Math.Clamp(adjMake, 0.05, 0.95);

            // ── Pre-shot Shooting Foul (contest-based) ────────────────────────
            // Hard contact check before the shot resolves — inside-weighted as in real NBA.
            // Probability scales with how tightly the defender contests the shot.
            double preShotFoulBase = shotType switch
            {
                ShotType.Inside   => 0.005 + defender.ContestPenalty(ShotType.Inside) * 0.030,
                ShotType.MidRange => 0.001 + defender.ContestPenalty(ShotType.MidRange) * 0.012,
                _                 => 0.0
            };
            double preShotFoulChance = preShotFoulBase * (shotContext switch
            {
                ShotContext.DrivingLayup or ShotContext.PickAndRollRoll or ShotContext.FloaterLayup => 1.6,
                ShotContext.PostMove or ShotContext.PostMoveMidRange                               => 1.4,
                ShotContext.ContactDunk                                                            => 1.8,
                ShotContext.CutLayup or ShotContext.FastBreakLayup                                => 0.5,
                ShotContext.AlleyOop or ShotContext.TransitionDunk
                    or ShotContext.PickAndRollDunk or ShotContext.CutDunk                         => 0.2,
                ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing                  => 0.3,
                _ => 1.0
            });

            if (preShotFoulChance > 0 && _rng.NextDouble() < preShotFoulChance)
            {
                var preFouler = PickFouler(defLineup, shotType);
                CommitFoul(preFouler, !state.IsHomePossession);

                var psStats = EnsureStats(actionPlayer);
                psStats.ShotAttempts++;
                int numFTs  = 2;
                int ftPts   = 0;
                for (int i = 0; i < numFTs; i++)
                    if (_rng.NextDouble() < actionPlayer.FTMakePct) ftPts++;

                if (state.IsHomePossession) homeScore += ftPts;
                else awayScore += ftPts;

                psStats.FTAttempts += numFTs;
                psStats.FTMade     += ftPts;
                psStats.Points     += ftPts;

                string preFoulNarr = ftPts switch
                {
                    2 => $"{preFouler.Name} fouls{FoulTag(preFouler)} — {actionPlayer.Name} makes both",
                    1 => $"{preFouler.Name} fouls{FoulTag(preFouler)} — {actionPlayer.Name} 1 of 2",
                    _ => $"{preFouler.Name} fouls{FoulTag(preFouler)} — {actionPlayer.Name} misses both"
                };
                results.Add(new PossessionResult
                {
                    Team         = offTeam.Name,
                    Narrative    = preFoulNarr,
                    HomeScore    = homeScore, AwayScore = awayScore,
                    Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                    PointsScored = ftPts,
                    Event        = PossessionEvent.FreeThrows,
                    Scorer       = actionPlayer.Name, Fouler = preFouler.Name,
                    Context      = ShotContext.None, FTAttempts = numFTs
                });
                return;
            }

            double r2 = _rng.NextDouble();

            bool isBlocked = r2 < blockChance;
            bool isMade    = !isBlocked && r2 < blockChance + adjMake;
            bool isThree   = shotType == ShotType.ThreePointer;

            // Off-ball help blocks — each non-matched defender gets a tiny independent roll.
            // Uses shot-type-specific attributes: IntDef+Height for inside, PerimDef for three.
            Player? helpBlocker = null;
            if (!isBlocked)
            {
                foreach (var helpDef in defLineup.Where(d => d != defender))
                {
                    double helpBase = shotType switch
                    {
                        ShotType.Inside =>
                            Math.Pow(helpDef.Attr_InteriorDefense / 100.0, 2.5) * 0.035
                            + Math.Max(0.0, (helpDef.Height + helpDef.Jumping - 100) / 1760.0),
                        ShotType.MidRange =>
                            Math.Pow((helpDef.Attr_InteriorDefense * 0.6 + helpDef.Attr_PerimeterDefense * 0.4) / 100.0, 2.5) * 0.013,
                        ShotType.ThreePointer =>
                            Math.Pow(helpDef.Attr_PerimeterDefense / 100.0, 2.5) * 0.005,
                        _ => 0.0
                    };
                    double helpChance = helpBase * blockability * contestMod
                        * Math.Clamp(helpDef.Tendencies.Block / 50.0, 0.4, 2.0)
                        * helpDef.EnergyFactor_Physical;
                    if (_rng.NextDouble() < helpChance)
                    {
                        isBlocked   = true;
                        isMade      = false;
                        helpBlocker = helpDef;
                        break;
                    }
                }
            }

            var shooterStats = EnsureStats(actionPlayer);

            if (isBlocked)
            {
                shooterStats.FGAttempts++;
                shooterStats.ShotAttempts++;
                RecordTeamFGA(lineup);
                if (isThree) shooterStats.ThreeAttempts++;
                if      (shotType == ShotType.Inside)    shooterStats.InsideAtt++;
                else if (shotType == ShotType.MidRange)  shooterStats.MidRangeAtt++;
                EnsureStats(defender).DefFGAttempts++;

                // On-ball block: weighted by BlockMod. Off-ball: already identified.
                var blocker = helpBlocker ?? WeightedRandom(defLineup, d => Math.Max(d.BlockMod, 0.01));
                EnsureStats(blocker).Blocks++;

                string narrative = BlockedNarrative(actionPlayer.Name, shotContext, blocker.Name);
                results.Add(new PossessionResult
                {
                    Team         = offTeam.Name,
                    Narrative    = narrative,
                    HomeScore    = homeScore, AwayScore = awayScore,
                    Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                    PointsScored = 0,
                    Event        = PossessionEvent.Blocked,
                    Scorer       = actionPlayer.Name, Blocker = blocker.Name,
                    Shot         = shotType, Context = shotContext, IsThree = isThree,
                    Debug        = dbg
                });

                if (!HandleRebound(offTeam, defTeam, lineup, defLineup, state,
                        ref homeScore, ref awayScore, ref orbCount, ref prevRebounder,
                        results, clockSeconds, shotType, ref possState))
                    return;
            }
            else if (isMade)
            {
                shooterStats.FGMade++;
                shooterStats.FGAttempts++;
                shooterStats.ShotAttempts++;
                RecordTeamFGA(lineup);
                int pts = isThree ? 3 : 2;
                if (isThree) { shooterStats.ThreeMade++; shooterStats.ThreeAttempts++; }
                if      (shotType == ShotType.Inside)   { shooterStats.InsideMade++;   shooterStats.InsideAtt++; }
                else if (shotType == ShotType.MidRange) { shooterStats.MidRangeMade++; shooterStats.MidRangeAtt++; }
                EnsureStats(defender).DefFGMade++;
                EnsureStats(defender).DefFGAttempts++;
                shooterStats.Points += pts;

                if (assisterName != null) EnsureStats(assisterName).Assists++;

                if (state.IsHomePossession) homeScore += pts;
                else awayScore += pts;

                bool isGameWinner = posContext == PossessionContext.PotentialGameWinner;
                string narrative = MadeNarrative(actionPlayer.Name, shotContext, isThree, assisterName);
                results.Add(new PossessionResult
                {
                    Team         = offTeam.Name,
                    Narrative    = narrative,
                    HomeScore    = homeScore, AwayScore = awayScore,
                    Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                    PointsScored = pts,
                    Event        = isGameWinner ? PossessionEvent.GameWinnerMade : PossessionEvent.ShotMade,
                    Scorer       = actionPlayer.Name, Assister = assisterName,
                    Shot         = shotType, Context = shotContext, IsThree = isThree,
                    Debug        = dbg
                });

                // ── Stage 10: Free Throw Check (made contact shot) ────
                if (shotContext is ShotContext.DrivingLayup or ShotContext.PickAndRollRoll
                    or ShotContext.PostMove or ShotContext.PostMoveMidRange
                    or ShotContext.CutLayup or ShotContext.FastBreakLayup)
                {
                    double foulChance = 0.18 + (actionPlayer.Attr_Inside - 50) / 50.0 * 0.08;
                    if (_rng.NextDouble() < foulChance)
                    {
                        var and1Fouler = PickFouler(defLineup, ShotType.Inside);
                        CommitFoul(and1Fouler, !state.IsHomePossession);

                        int ftPts = _rng.NextDouble() < actionPlayer.FTMakePct ? 1 : 0;
                        if (state.IsHomePossession) homeScore += ftPts;
                        else awayScore += ftPts;
                        shooterStats.FTAttempts++;
                        shooterStats.FTMade   += ftPts;
                        shooterStats.Points   += ftPts;
                        results.Add(new PossessionResult
                        {
                            Team         = offTeam.Name,
                            Narrative    = $"{actionPlayer.Name} and-one!{FoulTag(and1Fouler)} — {(ftPts == 1 ? "GOOD!" : "no good")}",
                            HomeScore    = homeScore, AwayScore = awayScore,
                            Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                            PointsScored = ftPts,
                            Event        = PossessionEvent.FreeThrows,
                            Scorer       = actionPlayer.Name, Fouler = and1Fouler.Name,
                            Context      = ShotContext.None, FTAttempts = 1
                        });
                    }
                }

                // ── Inbound time ──────────────────────────────────────────
                // After a made basket the defense inbounds; the clock runs until
                // the ball is touched in-bounds. In the last 2 minutes of Q4 and
                // all OT periods the NBA stops the clock on inbounds, so we skip
                // the drain there.
                bool lateGameClock = state.Quarter == 4 && clockSeconds <= 120
                                  || state.Quarter >= 5;
                if (!lateGameClock)
                {
                    double u = _rng.NextDouble();
                    int inboundTime = u < 0.80 ? 1 : u < 0.95 ? 2 : u < 0.99 ? 3 : 4;
                    clockSeconds = Math.Max(0, clockSeconds - inboundTime);
                }

                possState = PossessionState.HalfCourt;
                return;
            }
            else
            {
                // Missed
                shooterStats.FGAttempts++;
                shooterStats.ShotAttempts++;
                if (isThree) shooterStats.ThreeAttempts++;
                if      (shotType == ShotType.Inside)    shooterStats.InsideAtt++;
                else if (shotType == ShotType.MidRange)  shooterStats.MidRangeAtt++;
                EnsureStats(defender).DefFGAttempts++;

                // ── Stage 10: Free Throw Check (missed contact shot) ──
                bool grantedFT = false;

                if (shotContext is ShotContext.DrivingLayup or ShotContext.PickAndRollRoll
                    or ShotContext.PostMove or ShotContext.PostMoveMidRange
                    or ShotContext.CutLayup or ShotContext.FastBreakLayup or ShotContext.FloaterLayup)
                {
                    if (_rng.NextDouble() < 0.38)
                    {
                        grantedFT = true;
                        shooterStats.FGAttempts--;
                        if (isThree) shooterStats.ThreeAttempts--;
                        if      (shotType == ShotType.Inside)    shooterStats.InsideAtt--;
                        else if (shotType == ShotType.MidRange)  shooterStats.MidRangeAtt--;
                        EnsureStats(defender).DefFGAttempts--;

                        var inFouler = PickFouler(defLineup, ShotType.Inside);
                        CommitFoul(inFouler, !state.IsHomePossession);

                        int pts = 0;
                        for (int i = 0; i < 2; i++)
                            if (_rng.NextDouble() < actionPlayer.FTMakePct) pts++;

                        if (state.IsHomePossession) homeScore += pts;
                        else awayScore += pts;

                        shooterStats.FTAttempts += 2;
                        shooterStats.FTMade     += pts;
                        shooterStats.Points     += pts;

                        string ftNarr = pts switch
                        {
                            2 => $"{inFouler.Name} fouls{FoulTag(inFouler)} — {actionPlayer.Name} makes both",
                            1 => $"{inFouler.Name} fouls{FoulTag(inFouler)} — {actionPlayer.Name} 1 of 2",
                            _ => $"{inFouler.Name} fouls{FoulTag(inFouler)} — {actionPlayer.Name} misses both"
                        };
                        results.Add(new PossessionResult
                        {
                            Team         = offTeam.Name,
                            Narrative    = ftNarr,
                            HomeScore    = homeScore, AwayScore = awayScore,
                            Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                            PointsScored = pts,
                            Event        = PossessionEvent.FreeThrows,
                            Scorer       = actionPlayer.Name, Fouler = inFouler.Name,
                            Context      = ShotContext.None, FTAttempts = 2
                        });
                        return;
                    }
                }
                else if (isThree && _rng.NextDouble() < 0.09)
                {
                    grantedFT = true;
                    shooterStats.FGAttempts--;
                    shooterStats.ThreeAttempts--;
                    EnsureStats(defender).DefFGAttempts--;

                    var threeFouler = PickFouler(defLineup, ShotType.ThreePointer);
                    CommitFoul(threeFouler, !state.IsHomePossession);

                    int pts = 0;
                    for (int i = 0; i < 3; i++)
                        if (_rng.NextDouble() < actionPlayer.FTMakePct) pts++;

                    if (state.IsHomePossession) homeScore += pts;
                    else awayScore += pts;

                    shooterStats.FTAttempts += 3;
                    shooterStats.FTMade     += pts;
                    shooterStats.Points     += pts;

                    string ftNarr3 = pts switch
                    {
                        3 => $"{threeFouler.Name} fouls on the three{FoulTag(threeFouler)} — {actionPlayer.Name} makes all three!",
                        2 => $"{threeFouler.Name} fouls on the three{FoulTag(threeFouler)} — {actionPlayer.Name} 2 of 3",
                        1 => $"{threeFouler.Name} fouls on the three{FoulTag(threeFouler)} — {actionPlayer.Name} 1 of 3",
                        _ => $"{threeFouler.Name} fouls on the three{FoulTag(threeFouler)} — {actionPlayer.Name} misses all"
                    };
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = ftNarr3,
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                        PointsScored = pts,
                        Event        = PossessionEvent.FreeThrows,
                        Scorer       = actionPlayer.Name, Fouler = threeFouler.Name,
                        Context      = ShotContext.None, FTAttempts = 3
                    });
                    return;
                }
                else if (!isThree && shotType == ShotType.MidRange && _rng.NextDouble() < 0.06)
                {
                    grantedFT = true;
                    shooterStats.FGAttempts--;
                    shooterStats.MidRangeAtt--;
                    EnsureStats(defender).DefFGAttempts--;

                    var midFouler = PickFouler(defLineup, ShotType.MidRange);
                    CommitFoul(midFouler, !state.IsHomePossession);

                    int pts = 0;
                    for (int i = 0; i < 2; i++)
                        if (_rng.NextDouble() < actionPlayer.FTMakePct) pts++;

                    if (state.IsHomePossession) homeScore += pts;
                    else awayScore += pts;

                    shooterStats.FTAttempts += 2;
                    shooterStats.FTMade     += pts;
                    shooterStats.Points     += pts;

                    string ftNarrMid = pts switch
                    {
                        2 => $"{midFouler.Name} fouls on the mid-range{FoulTag(midFouler)} — {actionPlayer.Name} makes both",
                        1 => $"{midFouler.Name} fouls on the mid-range{FoulTag(midFouler)} — {actionPlayer.Name} 1 of 2",
                        _ => $"{midFouler.Name} fouls on the mid-range{FoulTag(midFouler)} — {actionPlayer.Name} misses both"
                    };
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = ftNarrMid,
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                        PointsScored = pts,
                        Event        = PossessionEvent.FreeThrows,
                        Scorer       = actionPlayer.Name, Fouler = midFouler.Name,
                        Context      = ShotContext.None, FTAttempts = 2
                    });
                    return;
                }

                if (!grantedFT)
                {
                    RecordTeamFGA(lineup);
                    string narrative = MissedNarrative(actionPlayer.Name, shotContext, isThree);
                    results.Add(new PossessionResult
                    {
                        Team         = offTeam.Name,
                        Narrative    = narrative,
                        HomeScore    = homeScore, AwayScore = awayScore,
                        Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                        PointsScored = 0,
                        Event        = PossessionEvent.ShotMissed,
                        Scorer       = actionPlayer.Name,
                        Shot         = shotType, Context = shotContext, IsThree = isThree,
                        Debug        = dbg
                    });

                    // ── Stage 11: Rebound ─────────────────────────────
                    if (!HandleRebound(offTeam, defTeam, lineup, defLineup, state,
                            ref homeScore, ref awayScore, ref orbCount, ref prevRebounder,
                            results, clockSeconds, shotType, ref possState))
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
        int clockSeconds,
        ShotType lastShotType,
        ref PossessionState possState)
    {
        // GritAndGrind OReb bonus; PaceAndSpace fewer crashes
        double offRebBias = offTeam.Coach.OffStyle == OffensiveStyle.GritAndGrind ? 1.4 :
                            offTeam.Coach.OffStyle == OffensiveStyle.PaceAndSpace  ? 0.75 : 1.0;
        double offRebWeight = lineup.Sum(p => p.ORebWeight * (p.Tendencies.OffRebound / 50.0)) * offRebBias;
        double defRebWeight = defLineup.Sum(p => p.DRebWeight);

        double offRebPct = Math.Clamp(offRebWeight / (offRebWeight + defRebWeight * 4.0), 0.09, 0.30);

        if (_rng.NextDouble() < offRebPct && orbCount < 3)
        {
            // Position-aware offensive rebounder selection
            var rebounder = WeightedRandom(lineup, p =>
            {
                double w = p.ORebWeight * (p.Tendencies.OffRebound / 50.0);
                if (lastShotType == ShotType.Inside)
                    w *= p.Position is Position.C or Position.PF ? 1.3 : 0.8;
                else if (lastShotType == ShotType.ThreePointer)
                    w *= p.Position is Position.PG or Position.SG or Position.SF ? 1.2 : 0.9;
                return Math.Max(w, 0.01);
            });
            orbCount++;
            prevRebounder = rebounder;
            EnsureStats(rebounder).OffRebounds++;
            EnsureStats(rebounder).Rebounds++;

            results.Add(new PossessionResult
            {
                Team         = offTeam.Name,
                Narrative    = $"{rebounder.Name} with the offensive rebound — second chance!",
                HomeScore    = homeScore, AwayScore = awayScore,
                Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                PointsScored = 0,
                Event        = PossessionEvent.OffensiveRebound,
                Rebounder    = rebounder.Name, Context = ShotContext.None
            });
            return true;
        }
        else
        {
            // Position-aware defensive rebounder selection
            var rebounder = WeightedRandom(defLineup, p =>
            {
                double w = p.DRebWeight;
                if (lastShotType == ShotType.Inside)
                    w *= p.Position is Position.C or Position.PF ? 1.3 : 0.8;
                else if (lastShotType == ShotType.ThreePointer)
                    w *= p.Position is Position.PG or Position.SG or Position.SF ? 1.2 : 0.9;
                return Math.Max(w, 0.01);
            });
            EnsureStats(rebounder).DefRebounds++;
            EnsureStats(rebounder).Rebounds++;

            results.Add(new PossessionResult
            {
                Team         = defTeam.Name,
                Narrative    = $"{rebounder.Name} with the defensive rebound",
                HomeScore    = homeScore, AwayScore = awayScore,
                Quarter      = state.Quarter, ClockSeconds = clockSeconds,
                PointsScored = 0,
                Event        = PossessionEvent.DefensiveRebound,
                Rebounder    = rebounder.Name, Context = ShotContext.None
            });
            possState = PossessionState.Transition;
            return false;
        }
    }

    // ── New Helper Methods ────────────────────────────────────────────────────

    private double NextGaussian(double mean = 0.0, double stddev = 1.0)
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        double z  = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stddev * z;
    }

    private static double GetStyleMultiplier(ShotContext ctx, OffensiveStyle style, PossessionState state)
    {
        return (style, ctx) switch
        {
            // PACE & SPACE
            (OffensiveStyle.PaceAndSpace, ShotContext.AlleyOop or ShotContext.CutDunk
                or ShotContext.FastBreakLayup or ShotContext.TransitionDunk or ShotContext.PickAndRollDunk) => 1.4,
            (OffensiveStyle.PaceAndSpace, ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing
                or ShotContext.TransitionThree or ShotContext.PickAndRollThree) => 1.5,
            (OffensiveStyle.PaceAndSpace, ShotContext.IsolationMidRange or ShotContext.PullUpMidRange
                or ShotContext.FadeawayMidRange or ShotContext.LateClockMidRange) => 0.4,
            (OffensiveStyle.PaceAndSpace, ShotContext.PostMove or ShotContext.PostMoveMidRange) => 0.3,
            (OffensiveStyle.PaceAndSpace, ShotContext.DrivingLayup or ShotContext.PickAndRollRoll) => 1.2,

            // HELIOCENTRIC
            (OffensiveStyle.Heliocentric, ShotContext.IsolationMidRange or ShotContext.StepBackThree
                or ShotContext.PullUpThree or ShotContext.PullUpMidRange) => 1.6,
            (OffensiveStyle.Heliocentric, ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing
                or ShotContext.CutLayup or ShotContext.AlleyOop) => 0.5,

            // MOTION / FLOW
            (OffensiveStyle.MotionFlow, ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing) => 1.8,
            (OffensiveStyle.MotionFlow, ShotContext.CutLayup or ShotContext.CutDunk or ShotContext.AlleyOop) => 1.5,
            (OffensiveStyle.MotionFlow, ShotContext.IsolationMidRange or ShotContext.StepBackThree
                or ShotContext.PullUpThree) => 0.4,

            // GRIT & GRIND
            (OffensiveStyle.GritAndGrind, ShotContext.PostMove or ShotContext.PostMoveMidRange
                or ShotContext.ContactDunk or ShotContext.PickAndRollRoll) => 1.8,
            (OffensiveStyle.GritAndGrind, ShotContext.DrivingLayup or ShotContext.ContactDunk) => 1.3,
            (OffensiveStyle.GritAndGrind, ShotContext.FloaterLayup) => 0.6,
            (OffensiveStyle.GritAndGrind, ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing
                or ShotContext.PullUpThree or ShotContext.StepBackThree) => 0.5,

            // STATE-BASED (only FastBreak for high-tempo dunks; Transition gets moderate boost)
            _ when state == PossessionState.FastBreak && (ctx is ShotContext.FastBreakLayup
                or ShotContext.TransitionDunk or ShotContext.TransitionThree) => 1.6,
            _ when state == PossessionState.Transition && ctx == ShotContext.TransitionThree => 1.2,
            _ when state == PossessionState.SecondChance && (ctx is ShotContext.Putback
                or ShotContext.TipIn) => 2.0,

            _ => 1.0
        };
    }

    private static double GetPhysicalMod(Player attacker, Player defender, ShotContext ctx)
    {
        return ctx switch
        {
            ShotContext.DrivingLayup or ShotContext.PickAndRollRoll or ShotContext.PullUpMidRange =>
                Math.Clamp((defender.Speed - attacker.Speed) / 100.0 * 0.15, 0.0, 0.15),
            ShotContext.PostMove or ShotContext.PostMoveMidRange or ShotContext.ContactDunk =>
                Math.Clamp((defender.Strength - attacker.Strength) / 100.0 * 0.18, 0.0, 0.18)
                + Math.Clamp((defender.Height  - attacker.Height)  / 100.0 * 0.06, 0.0, 0.06),
            _ => 0.0
        };
    }

    private static double ComputeContextThreshold(
        ShotContext ctx, ShotType shotType, Player player, int clockSeconds, CoachingProfile coach,
        int shotClockRemaining)
    {
        // Base threshold decays as shot clock winds down: fresh possession → very selective,
        // last few seconds → take almost anything.
        double T = shotClockRemaining switch
        {
            > 20 => 1.32,
            > 15 => 1.09,
            > 10 => 0.82,
            >  5 => 0.54,
            >  2 => 0.32,
            _    => 0.00
        };

        // Heliocentric offenses operate at slightly lower selectivity (ISO-heavy)
        if (coach.OffStyle == OffensiveStyle.Heliocentric) T *= 0.88;

        // Context/tendency modifiers — player's comfort-zone shots need less convincing
        T *= ctx switch
        {
            ShotContext.IsolationMidRange or ShotContext.PickAndRollMidRange
                or ShotContext.FadeawayMidRange =>
                1.0 - (player.Tendencies.MidRange - 50) / 400.0,
            ShotContext.CatchAndShootCorner or ShotContext.CatchAndShootWing
                or ShotContext.StepBackThree or ShotContext.PickAndRollThree or ShotContext.TransitionThree =>
                1.0 - (player.Tendencies.ThreePt - 50) / 400.0,
            ShotContext.DrivingLayup or ShotContext.PickAndRollRoll or ShotContext.FastBreakLayup =>
                1.0 - (player.Tendencies.Drive - 50) / 400.0,
            ShotContext.PostMove or ShotContext.PostMoveMidRange =>
                1.0 - (player.Tendencies.PostUp - 50) / 400.0,
            ShotContext.PullUpMidRange or ShotContext.PullUpThree =>
                1.0 - (player.Tendencies.PullUp - 50) / 400.0,
            ShotContext.ContactDunk =>
                1.0 - (player.Strength - 50) / 150.0,
            _ => 1.0
        };

        // Quarter-clock pressure (end-of-quarter crunch)
        if (clockSeconds < 4)       T = 0.0;
        else if (clockSeconds < 8)  T *= 0.70;
        else if (clockSeconds < 12) T *= 0.85;

        return Math.Max(T, 0.0);
    }

    private List<MenuEntry> GenerateMenu(
        Player player, List<Player> lineup, List<Player> defLineup,
        Dictionary<Player, Player> matchups,
        PossessionState possState, Team offTeam, Team defTeam,
        int spacingLevel, double teamSag, double teamGravity,
        double clockDecay, ShotClockPhase phase,
        int clockSeconds, int shotClockRemaining)
    {
        var coach    = offTeam.Coach;
        var style    = coach.OffStyle;
        double cm    = coach.OffensiveRating / 100.0;
        var defender = matchups[player];
        double defCoachMod  = 0.80 + defTeam.Coach.DefensiveRating / 100.0 * 0.40;
        double coachQualMod = 0.90 + cm * 0.10;

        // Defensive suppression of shot availability
        double intDefSup   = Math.Clamp(1.0 - (defender.Attr_InteriorDefense - 50) / 100.0 * 0.35, 0.3, 1.3);
        double perimDefSup = Math.Clamp(1.0 - (defender.Attr_PerimeterDefense - 50) / 100.0 * 0.35, 0.3, 1.3);
        double driveCont   = 1.0 - Math.Clamp((defender.Speed - player.Speed) / 100.0, 0, 0.20);
        double postCont    = Math.Clamp(1.0 - Math.Clamp((defender.Strength - player.Strength) / 100.0, 0, 0.25)
                                              - Math.Clamp((defender.Height  - player.Height)  / 100.0, 0, 0.10), 0.3, 1.3);
        // Defensive style opens opposite zone
        double opensPerim  = defTeam.Coach.DefStyle == DefensiveStyle.ProtectThePaint ? 1.30 : 1.0;
        double opensInt    = defTeam.Coach.DefStyle == DefensiveStyle.StopTheThree    ? 1.20 : 1.0;

        var entries = new List<MenuEntry>();

        // TryAdd: rolls a dice against appearanceProb. Only adds if the opportunity presents itself.
        void TryAdd(ShotContext ctx, ShotType st, double appearanceProb, bool isDunk = false)
        {
            double styleMult = GetStyleMultiplier(ctx, style, possState);
            double p = Math.Clamp(appearanceProb * styleMult, 0.0, 0.99);
            if (p < 0.001 || _rng.NextDouble() >= p) return;

            // Base make%
            double baseMake;
            if (isDunk && ctx != ShotContext.ContactDunk)
                baseMake = player.DunkMakePct;
            else if (ctx == ShotContext.ContactDunk)
                baseMake = player.ContactDunkMakePct;
            else
                baseMake = st switch
                {
                    ShotType.Inside       => player.InsideMakePct,
                    ShotType.MidRange     => player.MidRangeMakePct,
                    ShotType.ThreePointer => player.ThreeMakePct,
                    _                     => 0.45
                };

            // Physical bonuses to base make
            if (ctx == ShotContext.FastBreakLayup)
                baseMake += (player.Jumping - 50) / 500.0;
            if (ctx == ShotContext.FloaterLayup)
                baseMake += (player.Jumping - 50) / 500.0;
            if (ctx == ShotContext.PostMove)
                baseMake += (player.Height - 50) / 600.0;

            if (ctx == ShotContext.PostMove || ctx == ShotContext.PostMoveMidRange)
                baseMake *= 0.7 + player.Strength / 333.0;

            baseMake = Math.Clamp(baseMake, 0.05, 0.95);

            double ctxMod   = ShotContextMakeModifier(ctx, phase);
            double contest  = defender.ContestPenalty(st);
            double physMod  = GetPhysicalMod(player, defender, ctx);

            double sagMod = 1.0;
            if (st == ShotType.Inside)
            {
                if (teamSag     > 0) sagMod -= teamSag     * 0.35;
                if (teamGravity > 0) sagMod += teamGravity * 0.14;
                sagMod = Math.Max(sagMod, 0.5);
            }

            double defStyleMod = (defTeam.Coach.DefStyle, st) switch
            {
                (DefensiveStyle.ProtectThePaint, ShotType.Inside)        => 0.80,
                (DefensiveStyle.ProtectThePaint, ShotType.ThreePointer)  => 1.10,
                (DefensiveStyle.StopTheThree,    ShotType.ThreePointer)  => 0.80,
                (DefensiveStyle.StopTheThree,    ShotType.Inside)        => 1.10,
                _ => 1.0
            };

            // First of two contest dice — player sees this roll when deciding whether to shoot.
            // Simulates the "read": is the defender in position or closing out late?
            double contestRoll1 = Math.Clamp(NextGaussian(1.0, 0.30), 0.15, 1.85);
            double contestMod = Math.Clamp(1.0 - contest * contestRoll1 * defCoachMod - physMod, 0.30, 1.0);
            int    pts        = st == ShotType.ThreePointer ? 3 : 2;

            double actualPPS = baseMake * ctxMod * contestMod * coachQualMod
                             * clockDecay * sagMod * defStyleMod * pts;

            // Compress PPS spread toward the baseline reference so star players' shots
            // don't dominate the decision menu as heavily. Preserves relative shot quality
            // rankings but shrinks the gap between elite and average options.
            actualPPS = 0.95 + (actualPPS - 0.95) * 0.52;

            actualPPS = Math.Max(actualPPS, 0.01);

            double ctxThreshold = ComputeContextThreshold(ctx, st, player, clockSeconds, coach, shotClockRemaining);

            // Selection weight: raw shot value × style preference.
            // Dunks (baseMake~0.88) are strongly preferred; layups and 3PT roughly equal by EV;
            // mid-range slightly deprioritized. Appearance probability already controls frequency.
            double selectionWeight = baseMake * pts * styleMult;

            entries.Add(new MenuEntry
            {
                Context          = ctx,
                ShotType         = st,
                IsDunk           = isDunk,
                ActualPPS        = actualPPS,
                PerceivedPPS     = actualPPS,
                BaseMake         = baseMake,
                ContestMod       = contestMod,
                StyleMult        = styleMult,
                ContextThreshold = ctxThreshold,
                Weight           = selectionWeight,
                ContestRoll1     = contestRoll1
            });
        }

        double dunkTend = player.DunkTendency;
        bool isOffReb   = possState == PossessionState.SecondChance;

        if (isOffReb)
        {
            // Second chance: always have putback/tip-in available, no dice roll
            entries.Add(new MenuEntry { Context = ShotContext.Putback, ShotType = ShotType.Inside, IsDunk = false,
                ActualPPS = player.InsideMakePct * 2.0, PerceivedPPS = player.InsideMakePct * 2.0,
                BaseMake = player.InsideMakePct, ContestMod = 0.8, StyleMult = 1.0, ContextThreshold = 0.0, Weight = 0.6 });
            entries.Add(new MenuEntry { Context = ShotContext.TipIn, ShotType = ShotType.Inside, IsDunk = false,
                ActualPPS = player.InsideMakePct * 1.8, PerceivedPPS = player.InsideMakePct * 1.8,
                BaseMake = player.InsideMakePct, ContestMod = 0.75, StyleMult = 1.0, ContextThreshold = 0.0, Weight = 0.4 });
            return entries;
        }

        // ── Dunks (gated by DunkTendency; selection weight baseMake~0.88 makes them strongly preferred) ─
        TryAdd(ShotContext.AlleyOop,        ShotType.Inside,
            0.006 * (dunkTend / 0.5) * (player.AlleyOopTendency / 0.5) * (player.Tendencies.Cut / 50.0) * (1.0 + cm * 0.4),
            isDunk: true);
        TryAdd(ShotContext.CutDunk,         ShotType.Inside,
            0.004 * (dunkTend / 0.5) * (player.CutTendency / 0.5) * (player.Tendencies.Cut / 50.0),
            isDunk: true);
        TryAdd(ShotContext.TransitionDunk,  ShotType.Inside,
            possState == PossessionState.FastBreak
                ? 0.006 * (dunkTend / 0.5)
                : 0.002 * (dunkTend / 0.5),
            isDunk: true);
        TryAdd(ShotContext.PickAndRollDunk, ShotType.Inside,
            0.004 * (dunkTend / 0.5) * (player.DriveGravity / 0.5) * Math.Max(spacingLevel / 3.0, 0.5),
            isDunk: true);
        TryAdd(ShotContext.ContactDunk,     ShotType.Inside,
            0.003 * (dunkTend / 0.5) * (player.Strength / 50.0),
            isDunk: true);

        // ── Layups (frequency driven by DriveGravity, defense, spacing) ─────────
        TryAdd(ShotContext.DrivingLayup,    ShotType.Inside,
            0.085 * (player.DriveGravity / 0.5) * intDefSup * driveCont * opensInt);
        TryAdd(ShotContext.CutLayup,        ShotType.Inside,
            0.043 * (player.CutTendency / 0.5) * (player.Tendencies.Cut / 50.0) * (1.0 + cm * 0.5));
        TryAdd(ShotContext.FastBreakLayup,  ShotType.Inside,
            possState == PossessionState.FastBreak
                ? 0.05 * (player.Speed / 50.0) : 0.012);
        TryAdd(ShotContext.PickAndRollRoll, ShotType.Inside,
            0.043 * (player.DriveGravity / 0.5) * Math.Max(spacingLevel / 3.0, 0.5) * (1.0 + cm * 0.3));
        TryAdd(ShotContext.PostMove,        ShotType.Inside,
            player.Position is Position.PF or Position.C
                ? 0.051 * (player.Attr_Inside / 50.0) * (player.Tendencies.PostUp / 50.0) * postCont
                : 0.003);
        TryAdd(ShotContext.FloaterLayup,    ShotType.Inside,
            0.026 * (player.Speed / 50.0) * (player.Attr_Dribbling / 50.0));

        // ── Mid-Range (attribute-driven; specialists get many looks) ─────────────
        TryAdd(ShotContext.PullUpMidRange,      ShotType.MidRange,
            0.077 * (player.Attr_MidRange / 50.0) * (player.Tendencies.PullUp / 50.0) * (player.DriveGravity / 0.5) * perimDefSup);
        TryAdd(ShotContext.IsolationMidRange,   ShotType.MidRange,
            0.051 * (player.Attr_MidRange / 50.0) * (player.Tendencies.Iso / 50.0) * (1.0 - cm * 0.3));
        TryAdd(ShotContext.PickAndRollMidRange, ShotType.MidRange,
            0.068 * (player.Attr_MidRange / 50.0) * (player.DriveGravity / 0.5) * (1.0 + cm * 0.2));
        TryAdd(ShotContext.FadeawayMidRange,    ShotType.MidRange,
            0.034 * (player.Attr_MidRange / 50.0) * (player.Strength / 50.0));
        TryAdd(ShotContext.PostMoveMidRange,    ShotType.MidRange,
            player.Position is Position.PF or Position.C
                ? 0.051 * (player.Attr_MidRange / 50.0) * (player.Tendencies.PostUp / 50.0)
                : 0.003);
        TryAdd(ShotContext.LateClockMidRange,   ShotType.MidRange,
            shotClockRemaining < 6 ? 0.09 : 0.001);

        // ── Three-Point (frequency driven by 3PT attribute + spacing + defense) ──
        TryAdd(ShotContext.CatchAndShootCorner, ShotType.ThreePointer,
            0.050 * (player.Attr_ThreePoint / 50.0) * (1.0 + spacingLevel * 0.06) * (1.0 + cm * 0.3) * perimDefSup * opensPerim);
        TryAdd(ShotContext.CatchAndShootWing,   ShotType.ThreePointer,
            0.056 * (player.Attr_ThreePoint / 50.0) * (1.0 + cm * 0.2) * perimDefSup * opensPerim);
        TryAdd(ShotContext.PullUpThree,         ShotType.ThreePointer,
            0.030 * (player.Attr_ThreePoint / 50.0) * (player.Tendencies.PullUp / 50.0) * (player.DriveGravity / 0.5));
        TryAdd(ShotContext.StepBackThree,       ShotType.ThreePointer,
            0.018 * (player.Attr_ThreePoint / 50.0) * (player.Tendencies.PullUp / 50.0) * (player.Attr_Dribbling / 50.0));
        TryAdd(ShotContext.TransitionThree,     ShotType.ThreePointer,
            possState is PossessionState.FastBreak or PossessionState.Transition
                ? 0.042 * (player.Attr_ThreePoint / 50.0)
                : 0.018 * (player.Attr_ThreePoint / 50.0));
        TryAdd(ShotContext.PickAndRollThree,    ShotType.ThreePointer,
            0.042 * (player.Attr_ThreePoint / 50.0) * (player.DriveGravity / 0.5));

        return entries;
    }

    // ── Old context-selection methods (kept for reference, unused) ────────────

    private ShotContext SelectInsideContext(Player p, ShotClockPhase phase, int spacingLevel, CoachingProfile coach)
    {
        double cm        = coach.OffensiveRating / 100.0;
        double dunkTend  = p.DunkTendency;

        var weights = new Dictionary<ShotContext, double>
        {
            [ShotContext.DrivingLayup]    = Math.Max(p.DriveGravity * 2.0, 0.01),
            [ShotContext.PickAndRollRoll] = Math.Max(p.DriveGravity * spacingLevel * 0.25 * (1.0 + cm * 0.5), 0.01),
            [ShotContext.CutLayup]        = Math.Max(p.CutTendency * (p.Tendencies.Cut / 50.0) * (1.0 + cm * 0.6), 0.01),
            [ShotContext.FastBreakLayup]  = phase == ShotClockPhase.Early
                                            ? 1.5 * (1.0 + cm * 0.5) : 0.1,
            [ShotContext.PostMove]        = p.Position is Position.PF or Position.C
                                            ? Math.Max(p.Attr_Inside / 100.0 * 1.5 * (p.Tendencies.PostUp / 50.0), 0.01) : 0.1,
            [ShotContext.FloaterLayup]    = Math.Max(p.Speed / 100.0 * p.Attr_Dribbling / 100.0 * (1.0 - cm * 0.2), 0.01),
            [ShotContext.AlleyOop]        = Math.Max(dunkTend * p.AlleyOopTendency * (p.Tendencies.Cut / 50.0) * 0.45 * (1.0 + cm * 0.4), 0.01),
            [ShotContext.CutDunk]         = Math.Max(dunkTend * p.CutTendency * (p.Tendencies.Cut / 50.0) * 0.35 * (1.0 + cm * 0.4), 0.01),
            [ShotContext.TransitionDunk]  = phase == ShotClockPhase.Early
                                            ? dunkTend * 0.9 * (1.0 + cm * 0.3)
                                            : Math.Max(dunkTend * 0.07, 0.01),
            [ShotContext.PickAndRollDunk] = Math.Max(dunkTend * p.DriveGravity * 0.22 * (1.0 + cm * 0.3), 0.01),
            [ShotContext.ContactDunk]     = Math.Max(dunkTend * (p.Strength / 100.0) * 0.22, 0.01),
        };
        return WeightedRandom(weights.Keys, k => weights[k]);
    }

    private ShotContext SelectMidRangeContext(Player p, ShotClockPhase phase, CoachingProfile coach)
    {
        double cm = coach.OffensiveRating / 100.0;

        var weights = new Dictionary<ShotContext, double>
        {
            [ShotContext.PullUpMidRange]      = Math.Max(p.DriveGravity * (p.Tendencies.PullUp / 50.0) * 0.8, 0.01),
            [ShotContext.IsolationMidRange]   = Math.Max((p.Tendencies.Touches / 50.0) * (p.Tendencies.Iso / 50.0) * (1.0 - cm * 0.35), 0.01),
            [ShotContext.PickAndRollMidRange] = Math.Max(p.DriveGravity * 0.5 * (1.0 + cm * 0.3), 0.01),
            [ShotContext.FadeawayMidRange]    = Math.Max(p.Strength / 100.0 * 0.6, 0.01),
            [ShotContext.PostMoveMidRange]    = p.Position is Position.PF or Position.C
                                               ? Math.Max(p.Attr_MidRange / 100.0 * 1.2 * (p.Tendencies.PostUp / 50.0), 0.01) : 0.05,
            [ShotContext.LateClockMidRange]   = phase == ShotClockPhase.Late ? 2.0 : 0.01,
        };
        return WeightedRandom(weights.Keys, k => weights[k]);
    }

    private ShotContext SelectThreeContext(Player p, ShotClockPhase phase, int spacingLevel, CoachingProfile coach)
    {
        double cm = coach.OffensiveRating / 100.0;

        var weights = new Dictionary<ShotContext, double>
        {
            [ShotContext.CatchAndShootCorner] = Math.Max(
                p.PerimeterGravity * (0.8 + spacingLevel * 0.15) * (1.0 + cm * 0.8), 0.01),
            [ShotContext.CatchAndShootWing]   = Math.Max(
                p.PerimeterGravity * 1.2 * (1.0 + cm * 0.4), 0.01),
            [ShotContext.PullUpThree]         = Math.Max(
                p.DriveGravity * p.Attr_ThreePoint / 100.0 * (p.Tendencies.PullUp / 50.0) * (1.0 - cm * 0.3), 0.01),
            [ShotContext.StepBackThree]       = Math.Max(
                p.Attr_Dribbling / 100.0 * p.Attr_ThreePoint / 100.0 * (p.Tendencies.PullUp / 50.0) * (1.0 - cm * 0.4), 0.01),
            [ShotContext.TransitionThree]     = Math.Max(
                p.ShotClockAggressiveness * 0.5 * (1.0 + cm * 0.5), 0.01),
            [ShotContext.PickAndRollThree]    = Math.Max(p.DriveGravity * 0.6, 0.01),
        };
        return WeightedRandom(weights.Keys, k => weights[k]);
    }

    private static double AssistProbability(ShotContext context) => context switch
    {
        ShotContext.AlleyOop             => 0.98,
        ShotContext.CutDunk              => 0.82,
        ShotContext.CutLayup             => 0.79,
        ShotContext.PickAndRollDunk      => 0.78,
        ShotContext.CatchAndShootCorner  => 0.77,
        ShotContext.PickAndRollThree     => 0.75,
        ShotContext.CatchAndShootWing    => 0.72,
        ShotContext.PickAndRollMidRange  => 0.66,
        ShotContext.PickAndRollRoll      => 0.63,
        ShotContext.FastBreakLayup       => 0.52,
        ShotContext.TransitionDunk       => 0.44,
        ShotContext.TransitionThree      => 0.42,
        ShotContext.DrivingLayup         => 0.30,
        ShotContext.FloaterLayup         => 0.26,
        ShotContext.ContactDunk          => 0.23,
        ShotContext.PostMove             => 0.21,
        ShotContext.PostMoveMidRange     => 0.17,
        ShotContext.PullUpThree          => 0.17,
        ShotContext.PullUpMidRange       => 0.16,
        ShotContext.FadeawayMidRange     => 0.10,
        ShotContext.StepBackThree        => 0.10,
        ShotContext.IsolationMidRange    => 0.05,
        ShotContext.LateClockMidRange    => 0.04,
        ShotContext.Putback              => 0.00,
        ShotContext.TipIn                => 0.00,
        _                               => 0.16
    };

    private static double ShotContextMakeModifier(ShotContext context, ShotClockPhase phase) => context switch
    {
        ShotContext.CatchAndShootCorner => 1.10,
        ShotContext.CatchAndShootWing   => 1.04,
        ShotContext.FastBreakLayup      => 1.10,
        ShotContext.AlleyOop            => 1.14,
        ShotContext.IsolationMidRange   => 0.94,
        ShotContext.StepBackThree       => 0.92,
        ShotContext.LateClockMidRange   => 0.88,
        ShotContext.FadeawayMidRange    => 0.90,
        ShotContext.PostMove            => 0.97,
        _ when phase == ShotClockPhase.BuzzerBeater => 0.20,
        _ => 1.0
    };

    private static double AttributeCurve(double attr, double iq)
    {
        double normalized = Math.Clamp(attr / 100.0, 0.0, 1.0);
        double exponent   = 1.0 + iq * 1.0;
        return Math.Pow(normalized, exponent);
    }

    // Returns a bijective defensive assignment for all 5 offensive players.
    // Starts with best positional matchups, then randomly applies switches.
    private Dictionary<Player, Player> GetMatchups(List<Player> offense, List<Player> defense)
    {
        var matchups  = new Dictionary<Player, Player>(5);
        var unassigned = defense.ToList();

        // Best positional assignment for each offensive player
        foreach (var off in offense)
        {
            bool isPerimeter = off.Position is Position.PG or Position.SG or Position.SF;
            var  samePos     = unassigned.Where(d => d.Position == off.Position).ToList();
            var  pool        = samePos.Count > 0 ? samePos : unassigned;

            var defender = isPerimeter
                ? pool.MaxBy(d => d.Attr_PerimeterDefense)!
                : pool.MaxBy(d => d.Attr_InteriorDefense)!;

            matchups[off] = defender;
            unassigned.Remove(defender);
            if (unassigned.Count == 0) unassigned = defense.ToList(); // safety fallback
        }

        // Apply switch chance: randomly swap ~22% of assignments
        var offList = offense.ToList();
        for (int i = 0; i < offList.Count; i++)
        {
            if (_rng.NextDouble() < 0.22)
            {
                int j = _rng.Next(offList.Count);
                if (i != j)
                {
                    (matchups[offList[i]], matchups[offList[j]]) = (matchups[offList[j]], matchups[offList[i]]);
                }
            }
        }

        return matchups;
    }

    // Pass turnover rate: 95 Passing → 1.2%, 50 Passing → 2.6%, 5 Passing → 4.0%
    private static double PassTurnoverRate(Player passer) =>
        0.012 + (95.0 - passer.Attr_Passing) / 90.0 * 0.028;

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

    private void RecordBallTouch(Player handler, List<Player> lineup)
    {
        EnsureStats(handler).TouchesTotal++;
        foreach (var p in lineup)
            EnsureStats(p).TeamTouchesOnCourt++;
    }

    private void RecordTeamFGA(List<Player> lineup)
    {
        foreach (var p in lineup)
            EnsureStats(p).TeamFGAOnCourt++;
    }

    // ── Foul helpers ───────────────────────────────────────────────────────

    private void CommitFoul(Player fouler, bool isHomeFouling)
    {
        _playerFouls[fouler.Name] = _playerFouls.GetValueOrDefault(fouler.Name) + 1;
        if (isHomeFouling) _homeFoulsQ++;
        else               _awayFoulsQ++;
        EnsureStats(fouler).Fouls++;
    }

    private void CommitOffensiveFoul(Player fouler)
    {
        _playerFouls[fouler.Name] = _playerFouls.GetValueOrDefault(fouler.Name) + 1;
        EnsureStats(fouler).Fouls++;
    }

    private Player PickFouler(List<Player> defenders, ShotType? shotType)
    {
        return WeightedRandom(defenders, d =>
        {
            double defAttr = shotType == ShotType.Inside
                ? d.Attr_InteriorDefense / 100.0
                : d.Attr_PerimeterDefense / 100.0;
            double w = (d.Attr_FoulTendency / 100.0) * 0.50
                     + (1.0 - defAttr)               * 0.30
                     + (1.0 - d.Energy / 100.0)      * 0.20;
            return Math.Max(w, 0.01);
        });
    }

    private string FoulTag(Player p)
    {
        int f = _playerFouls.GetValueOrDefault(p.Name);
        return f >= 6 ? " [FOULED OUT]" : f == 5 ? $" ({f} fouls — in serious foul trouble)" : f >= 4 ? $" ({f} fouls)" : "";
    }

    // ── Narrative Builders ─────────────────────────────────────────────────

    private string MadeNarrative(string scorer, ShotContext ctx, bool isThree, string? assister)
    {
        // When there's an assister, lead with passer-first narrative for high-assist contexts
        if (assister != null)
        {
            string? passerFirst = ctx switch
            {
                ShotContext.CatchAndShootCorner => Pick(
                    $"{assister} finds {scorer} in the corner — BANG!",
                    $"{assister} kicks it to {scorer} — corner three!"),
                ShotContext.CatchAndShootWing => Pick(
                    $"{assister} swings it to {scorer} on the wing — GOOD!",
                    $"{assister} finds {scorer} from the wing — three!"),
                ShotContext.AlleyOop =>
                    $"{assister} lobs it to {scorer} — THROWS IT DOWN!",
                ShotContext.CutLayup => Pick(
                    $"{assister} feeds {scorer} on the cut — lays it in!",
                    $"{assister} finds {scorer} cutting backdoor — score!"),
                ShotContext.CutDunk =>
                    $"{assister} finds {scorer} on the cut — DUNKS IT!",
                ShotContext.PickAndRollRoll => Pick(
                    $"{assister} finds {scorer} rolling to the rim — finishes!",
                    $"{assister} to {scorer} off the roll — GOOD!"),
                ShotContext.PickAndRollDunk =>
                    $"{assister} lobs to {scorer} off the roll — DUNK!",
                ShotContext.PickAndRollThree => Pick(
                    $"{assister} kicks it out to {scorer} for the three!",
                    $"{assister} finds {scorer} off the pick — BANG!"),
                ShotContext.PickAndRollMidRange =>
                    $"{assister} finds {scorer} off the PnR — mid-range GOOD!",
                ShotContext.FastBreakLayup => Pick(
                    $"{assister} pushes ahead to {scorer} — easy layup!",
                    $"{assister} finds {scorer} in transition — GOOD!"),
                ShotContext.TransitionDunk =>
                    $"{assister} throws ahead to {scorer} — SLAM!",
                ShotContext.TransitionThree => Pick(
                    $"{assister} finds {scorer} ahead of the defense — three!",
                    $"{assister} ahead to {scorer} — BANG!"),
                ShotContext.DrivingLayup =>
                    $"{assister} finds {scorer} driving — lays it in!",
                ShotContext.PostMove =>
                    $"{assister} feeds {scorer} in the post — finishes!",
                ShotContext.ContactDunk =>
                    $"{assister} throws it to {scorer} — POWERS THROUGH — DUNK!",
                _ when isThree => Pick(
                    $"{assister} finds {scorer} from deep — GOOD!",
                    $"{assister} to {scorer} — three!"),
                _ => null   // fall through to standard narrative
            };
            if (passerFirst != null) return passerFirst;
        }

        // Standard (no assister, or context without a passer-first template)
        string assist = assister != null ? $" (Assist: {assister})" : "";
        string s = ctx switch
        {
            ShotContext.DrivingLayup       => Pick($"{scorer} drives — lays it in!{assist}", $"{scorer} attacks the rim — GOOD!{assist}"),
            ShotContext.PickAndRollRoll    => Pick($"{scorer} rolls to the rim — finishes!{assist}", $"{scorer} off the pick-and-roll — GOOD!{assist}"),
            ShotContext.CutLayup           => Pick($"{scorer} backdoor cut — lays it in!{assist}", $"{scorer} cuts to the rim — score!{assist}"),
            ShotContext.FastBreakLayup     => Pick($"{scorer} in transition — easy basket!{assist}", $"{scorer} beats everyone downcourt — lays it up!{assist}"),
            ShotContext.PostMove           => Pick($"{scorer} backs down in the post — lays it in!{assist}", $"{scorer} post move — good!{assist}"),
            ShotContext.FloaterLayup       => Pick($"{scorer} floater over the defense — GOOD!{assist}", $"{scorer} teardrop — splashes through!{assist}"),
            ShotContext.AlleyOop           => $"LOB to {scorer} — THROWS IT DOWN!{assist}",
            ShotContext.CutDunk            => Pick($"{scorer} cuts and DUNKS!{assist}", $"{scorer} jam on the cut!{assist}"),
            ShotContext.TransitionDunk     => Pick($"{scorer} SLAMS it in transition!{assist}", $"{scorer} throws it down on the break!{assist}"),
            ShotContext.PickAndRollDunk    => Pick($"{scorer} catches the lob off the roll — DUNK!{assist}", $"{scorer} oop off the pick — GOOD!{assist}"),
            ShotContext.ContactDunk        => Pick($"{scorer} powers through — CONTACT DUNK!{assist}", $"{scorer} throws it down through contact!{assist}"),
            ShotContext.PullUpMidRange     => Pick($"{scorer} pull-up mid-range — GOOD!", $"{scorer} stops and pops — hits it!"),
            ShotContext.IsolationMidRange  => Pick($"{scorer} isolation — mid-range — GOOD!", $"{scorer} in the iso — knocks it down!"),
            ShotContext.PickAndRollMidRange=> Pick($"{scorer} mid-range off the pick — GOOD!{assist}", $"{scorer} pulls up off the PnR!{assist}"),
            ShotContext.FadeawayMidRange   => Pick($"{scorer} fadeaway — hits it!", $"{scorer} leans back — mid-range GOOD!"),
            ShotContext.PostMoveMidRange   => Pick($"{scorer} with the post move — mid-range GOOD!", $"{scorer} mid-post — drops it in!"),
            ShotContext.LateClockMidRange  => Pick($"{scorer} pulls up late clock — GOOD!", $"{scorer} beats the shot clock — hits!"),
            ShotContext.CatchAndShootCorner=> Pick($"{scorer} catch-and-shoot from the corner — BANG!{assist}", $"{scorer} corner three — GOOD!{assist}"),
            ShotContext.CatchAndShootWing  => Pick($"{scorer} catch-and-shoot from the wing — GOOD!{assist}", $"{scorer} wing three — BANG!{assist}"),
            ShotContext.PullUpThree        => Pick($"{scorer} pull-up three — GOOD!", $"{scorer} off the dribble from deep — knocks it!"),
            ShotContext.StepBackThree      => Pick($"{scorer} step-back three — SPLASH!", $"{scorer} creates space and drains the three!"),
            ShotContext.PickAndRollThree   => Pick($"{scorer} three off the PnR — GOOD!{assist}", $"{scorer} kicks back, three — BANG!{assist}"),
            ShotContext.TransitionThree    => Pick($"{scorer} fires in transition — three!{assist}", $"{scorer} ahead-of-the-defense three — GOOD!{assist}"),
            ShotContext.Putback            => $"{scorer} putback — GOOD!",
            ShotContext.TipIn              => $"{scorer} with the TIP-IN!",
            _                             => $"{scorer} — GOOD!{assist}"
        };
        return s;
    }

    private string MissedNarrative(string shooter, ShotContext ctx, bool isThree) => ctx switch
    {
        ShotContext.DrivingLayup        => Pick($"{shooter} drives — misses the layup", $"{shooter} at the rim — no good"),
        ShotContext.PickAndRollRoll     => $"{shooter} rolls to the rim — misses",
        ShotContext.CutLayup            => $"{shooter} cuts — layup rattles out",
        ShotContext.FastBreakLayup      => $"{shooter} in transition — misses the easy look",
        ShotContext.PostMove            => Pick($"{shooter} post move — off the backboard", $"{shooter} in the post — no good"),
        ShotContext.FloaterLayup        => $"{shooter} floater — no good",
        ShotContext.AlleyOop            => $"{shooter} can't finish the alley-oop — misses",
        ShotContext.CutDunk             => $"{shooter} goes up for the dunk — can't finish",
        ShotContext.TransitionDunk      => $"{shooter} misses the dunk in transition — rare!",
        ShotContext.PickAndRollDunk     => $"{shooter} can't finish the PnR dunk",
        ShotContext.ContactDunk         => $"{shooter} powers up through contact — no good",
        ShotContext.PullUpMidRange      => Pick($"{shooter} pull-up mid-range — off the iron", $"{shooter} pull-up — no good"),
        ShotContext.IsolationMidRange   => Pick($"{shooter} isolation mid-range — no good", $"{shooter} iso — rattles out"),
        ShotContext.PickAndRollMidRange => $"{shooter} mid-range off the PnR — no good",
        ShotContext.FadeawayMidRange    => $"{shooter} fadeaway — off the back rim",
        ShotContext.PostMoveMidRange    => $"{shooter} post-up mid-range — misses",
        ShotContext.LateClockMidRange   => $"{shooter} late clock shot — no good",
        ShotContext.CatchAndShootCorner => Pick($"{shooter} corner three — no good", $"{shooter} misses from the corner"),
        ShotContext.CatchAndShootWing   => Pick($"{shooter} wing three — rattles out", $"{shooter} misses from the wing"),
        ShotContext.PullUpThree         => Pick($"{shooter} step-back three — rattles out", $"{shooter} pull-up three — no good"),
        ShotContext.StepBackThree       => Pick($"{shooter} step-back three — rattles out", $"{shooter} step-back — no good"),
        ShotContext.PickAndRollThree    => $"{shooter} three off the PnR — no good",
        ShotContext.TransitionThree     => $"{shooter} transition three — misses",
        _                              => $"{shooter} — no good"
    };

    private string BlockedNarrative(string shooter, ShotContext ctx, string blocker) => ctx switch
    {
        ShotContext.DrivingLayup or ShotContext.CutLayup or ShotContext.FastBreakLayup
            => Pick($"BLOCKED! {blocker} rejects {shooter} at the rim!", $"{blocker} stuffs {shooter}!"),
        ShotContext.AlleyOop
            => $"{blocker} SWATS the alley-oop attempt by {shooter}!",
        ShotContext.CutDunk or ShotContext.TransitionDunk or ShotContext.PickAndRollDunk
            or ShotContext.ContactDunk
            => $"{blocker} rejects the dunk by {shooter}!",
        ShotContext.FloaterLayup
            => $"{blocker} swats the floater by {shooter}!",
        _   => $"{shooter}'s shot blocked by {blocker}!"
    };

    private string Pick(params string[] options) =>
        options[_rng.Next(options.Length)];
}
