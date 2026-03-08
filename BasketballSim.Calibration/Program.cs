using BasketballSim.Models;
using BasketballSim.Simulation;
using BasketballSim.Data;

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
    MT("Home", "3PT", "#4af", 100, each: c => { c.ThreePoint = 85; c.BasketballIQ = 75; }),
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
    MT("Home", "IQ+", "#4af", 100, each: c => { c.BasketballIQ = 80; c.Dribbling = 80; }),
    MT("Away", "AVG", "#f84", 100));
RunScenario("Low IQ+Drib=20 — more TOs",
    MT("Home", "IQ-", "#4af", 100, each: c => { c.BasketballIQ = 20; c.Dribbling = 20; }),
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
RunScenario("Coach 95 vs Coach 5 — target ~6 pt differential",
    MT("Home", "COA", "#4af", 100, coach: new CoachingProfile(1.0, 1.0, 1.0, 95)),
    MT("Away", "COB", "#f84", 100, coach: new CoachingProfile(1.0, 1.0, 1.0, 5)));
RunScenario("Pace&Space coach vs PostUp coach",
    MT("Home", "P&S", "#4af", 100, coach: CoachingProfiles.PaceAndSpace),
    MT("Away", "PST", "#f84", 100, coach: CoachingProfiles.PostUp));

WH("SET 8: REAL TEAM DATA");
RunScenario("Knicks (real data) vs All-50 baseline",
    RosterData.Knicks,
    MT("Baseline", "BAS", "#888", 100));

WH("SET 9: RATING EXTREMES");
RunScenario("All-95 vs All-50",
    MT("Elite", "ELT", "#4af", 100, all: 95),
    MT("Average", "AVG", "#f84", 100, all: 50));
RunScenario("All-95 vs All-5",
    MT("Elite", "ELT", "#4af", 100, all: 95),
    MT("Awful", "AWF", "#f84", 100, all: 5));

W("");
W($"Output written to: {outputPath}");
file.Flush();
Console.WriteLine($"\nDone. Results: {outputPath}");

// ── Local functions ──────────────────────────────────────────────────────────
void W(string s = "") { Console.WriteLine(s); file.WriteLine(s); }
void WH(string label) { W(""); W($"{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}{'═',1}"); W($" {label}"); }

Team MT(string name, string abbr, string color, double pace,
    int all = 50, CoachingProfile? coach = null,
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
            BasketballIQ = all, Dribbling = all, Passing = all,
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
        Pace = pace, Coach = coach ?? CoachingProfiles.Balanced,
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
