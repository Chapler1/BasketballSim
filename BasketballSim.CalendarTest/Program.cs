using BasketballSim.Models;
using BasketballSim.Services;

// ── Calendar verification ─────────────────────────────────────────────────────
Console.WriteLine("=== CALENDAR DATES (startYear=2025) ===");
Console.WriteLine($"Season Start  : {NbaCalendarService.GetSeasonStart(2025):ddd MMM d yyyy}");
Console.WriteLine($"Season End    : {NbaCalendarService.GetSeasonEnd(2025):ddd MMM d yyyy}");
var (asS, asE) = NbaCalendarService.GetAllStarBreak(2025);
Console.WriteLine($"All-Star Break: {asS:ddd MMM d yyyy} – {asE:ddd MMM d yyyy}");
Console.WriteLine();

// ── Build a BALANCED 30-team, 1230-game NBA-style matchup set ─────────────────
// Each team plays exactly 82 games; each pair plays at most 4 games.
// Mirrors the actual SeasonScheduleService intra-div/in-conf/inter-conf structure.
string[] teams = [
    "Celtics","Brooklyn","Knicks","76ers","Raptors",       // Atlantic
    "Bulls","Cavaliers","Pistons","Pacers","Bucks",         // Central
    "Hawks","Hornets","Heat","Magic","Wizards",             // Southeast
    "Nuggets","Timberwolves","Thunder","Trail Blazers","Jazz",    // Northwest
    "Warriors","Clippers","Lakers","Suns","Kings",          // Pacific
    "Mavericks","Rockets","Grizzlies","Pelicans","Spurs"    // Southwest
];

// Division membership (indices into teams[])
int[][] divs = [
    [0,1,2,3,4], [5,6,7,8,9], [10,11,12,13,14],   // East
    [15,16,17,18,19], [20,21,22,23,24], [25,26,27,28,29]  // West
];

var matchups = new List<(string, string)>(1230);
void Add(int h, int a) { matchups.Add((teams[h], teams[a])); }

// 1. Intra-division: 4 games per pair (2H + 2A) → 16 games per team
foreach (var div in divs)
    for (int i = 0; i < div.Length; i++)
    for (int j = i + 1; j < div.Length; j++)
    { Add(div[i], div[j]); Add(div[i], div[j]); Add(div[j], div[i]); Add(div[j], div[i]); }

// 2. In-conference out-of-division — circulant pattern → ~30 games per team
// East divisions: Atlantic(0-4), Central(5-9), Southeast(10-14) pairs
// West divisions: Northwest(15-19), Pacific(20-24), Southwest(25-29) pairs
foreach (var confDivs in new[] { divs[..3], divs[3..] })
{
    for (int d1 = 0; d1 < confDivs.Length; d1++)
    for (int d2 = d1 + 1; d2 < confDivs.Length; d2++)
    {
        var A = confDivs[d1]; var B = confDivs[d2];
        int n = Math.Min(A.Length, B.Length);
        for (int i = 0; i < n; i++)
        {
            for (int k = 0; k <= 2; k++) { int j=(i+k)%n; Add(A[i],B[j]); Add(A[i],B[j]); Add(B[j],A[i]); Add(B[j],A[i]); }
            { int j=(i+3)%n; Add(A[i],B[j]); Add(A[i],B[j]); Add(B[j],A[i]); }
            { int j=(i+4)%n; Add(B[j],A[i]); Add(B[j],A[i]); Add(A[i],B[j]); }
        }
    }
}

// 3. Inter-conference: 1H + 1A per pair → 30 games per team
for (int e = 0; e < 15; e++)
for (int w = 15; w < 30; w++)
{ Add(e, w); Add(w, e); }

Console.WriteLine($"Generated {matchups.Count} matchups ({(matchups.Count == 1230 ? "correct" : "WRONG")})");
// Verify 82 games per team
var perTeam = new int[30];
foreach (var (h,a) in matchups) { perTeam[Array.IndexOf(teams,h)]++; perTeam[Array.IndexOf(teams,a)]++; }
bool balanced = perTeam.All(c => c == 82);
Console.WriteLine($"Games per team: min={perTeam.Min()} max={perTeam.Max()} ({(balanced?"balanced":"UNBALANCED")})");
Console.WriteLine();

// ── Run 50 seeds, validate constraints ────────────────────────────────────────
Console.WriteLine("=== CONSTRAINT VALIDATION (50 seeds, balanced schedule) ===");
int fails = 0;
for (int seed = 0; seed < 50; seed++)
{
    var games = NbaCalendarService.GenerateSchedule(matchups, 2025, seed);

    var byTeam = teams.ToDictionary(t => t, _ => new List<DateTime>());
    int breakViol = 0;
    foreach (var g in games)
    {
        byTeam[g.HomeTeamId].Add(g.Date);
        byTeam[g.AwayTeamId].Add(g.Date);
        if (g.Date >= asS && g.Date <= asE) breakViol++;
    }

    int v3 = 0, v4 = 0; var b2bs = new List<int>();
    foreach (var t in teams)
    {
        var days = byTeam[t].Order().ToList();
        int b2b = 0;
        for (int i = 1; i < days.Count; i++) if ((days[i]-days[i-1]).Days==1) b2b++;
        b2bs.Add(b2b);
        for (int i = 2; i < days.Count; i++)
            if ((days[i]-days[i-1]).Days==1 && (days[i-1]-days[i-2]).Days==1) v3++;
        for (int i = 0; i < days.Count; i++)
            if (days.Count(d => d >= days[i] && d <= days[i].AddDays(5)) > 4) v4++;
    }

    bool ok = v3==0 && v4==0 && breakViol==0 && games.Count==1230;
    if (!ok) fails++;
    if (!ok || seed < 10)
        Console.WriteLine($"Seed {seed,2}: {games.Count} games | 3in3={v3} 4in6={v4} Break={breakViol} " +
                          $"B2B avg={b2bs.Average():F1} [{b2bs.Min()},{b2bs.Max()}]  {(ok?"OK":"FAIL")}");
}
Console.WriteLine(fails==0 ? "ALL 50 PASSED\n" : $"{fails}/50 SEEDS FAILED\n");

// ── Deep analysis for seed=42 ─────────────────────────────────────────────────
Console.WriteLine("=== DISTRIBUTION ANALYSIS (seed=42, balanced) ===");
{
    var games = NbaCalendarService.GenerateSchedule(matchups, 2025, 42);
    Console.WriteLine($"First game : {games.First().Date:MMM d yyyy}");
    Console.WriteLine($"Last game  : {games.Last().Date:MMM d yyyy}");

    Console.WriteLine("\nLeague games per month:");
    foreach (var g in games.GroupBy(g => g.Date.ToString("yyyy-MM")).OrderBy(g => g.Key))
        Console.WriteLine($"  {g.Key}: {g.Count(),4} games");

    var byTeam = teams.ToDictionary(t => t, _ => new List<DateTime>());
    foreach (var g in games) { byTeam[g.HomeTeamId].Add(g.Date); byTeam[g.AwayTeamId].Add(g.Date); }

    Console.WriteLine("\nPer-team games per week (30-team avg ± deviation):");
    var seasonStart = NbaCalendarService.GetSeasonStart(2025);
    var seasonEnd   = NbaCalendarService.GetSeasonEnd(2025);
    int totalWeeks  = (int)Math.Ceiling((seasonEnd - seasonStart).TotalDays / 7);
    for (int w = 0; w < totalWeeks; w++)
    {
        var wStart = seasonStart.AddDays(w * 7);
        var wEnd   = wStart.AddDays(6);
        bool isBreak = wStart <= asE && wEnd >= asS;
        var counts = teams.Select(t => byTeam[t].Count(d => d >= wStart && d <= wEnd)).ToList();
        Console.WriteLine($"  Week {w+1,2} ({wStart:MMM d}-{wEnd:MMM d}): avg={counts.Average():F1} dev={counts.Max()-counts.Min()}{(isBreak?" [ASB]":"")}");
    }

    var b2bs = teams.Select(t => {
        var d = byTeam[t].Order().ToList();
        return Enumerable.Range(1,d.Count-1).Count(i => (d[i]-d[i-1]).Days==1);
    }).ToList();
    Console.WriteLine($"\nB2B per team: avg={b2bs.Average():F1}  min={b2bs.Min()}  max={b2bs.Max()}");
    Console.WriteLine($"Target: 15–18 B2Bs per team");
}
