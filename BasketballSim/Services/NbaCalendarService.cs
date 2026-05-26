using BasketballSim.Models;

namespace BasketballSim.Services;

/// <summary>
/// Generates NBA-style dated schedules using the Heuristic Greedy Slotting algorithm.
///
/// Calendar (startYear = 2025):
///   Season Start  : 3rd Tuesday of October 2025  → Oct 21 2025
///   All-Star Break: 2nd Wed – 3rd Wed of Feb 2026 → Feb 11 – Feb 18 2026
///   Season End    : 2nd Sunday of April 2026      → Apr 12 2026
///
/// Hard constraints (calendar-date-aware):
///   • Not already playing that day
///   • No 3-in-3 (three consecutive calendar nights)
///   • Max 4 games in any rolling 6-calendar-day window
///
/// Distribution:
///   Each matchup's "target day" is proportional to its shuffled-list position,
///   spreading games evenly from October through April.
///
/// B2B control:
///   Phase 1-2: local window, B2B avoided when team is below cap (greedy)
///   Post-processing: iterative game-moves to reduce B2Bs toward target range (15-18)
/// </summary>
public static class NbaCalendarService
{
    private const int B2bSoftCap = 16;  // greedy cap (affects phase 1-2 avoidance)
    private const int B2bTarget  = 16;  // post-processing reduction target

    // ── Calendar helpers ──────────────────────────────────────────────────────

    public static DateTime GetNthDayOfWeek(int year, int month, DayOfWeek dow, int n)
    {
        var first  = new DateTime(year, month, 1);
        int offset = ((int)dow - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + (n - 1) * 7);
    }

    /// <summary>3rd Tuesday of October of <paramref name="startYear"/>.</summary>
    public static DateTime GetSeasonStart(int startYear) =>
        GetNthDayOfWeek(startYear, 10, DayOfWeek.Tuesday, 3);

    /// <summary>2nd Sunday of April of the following year.</summary>
    public static DateTime GetSeasonEnd(int startYear) =>
        GetNthDayOfWeek(startYear + 1, 4, DayOfWeek.Sunday, 2);

    /// <summary>2nd–3rd Wednesday of February of the following year (inclusive).</summary>
    public static (DateTime Start, DateTime End) GetAllStarBreak(int startYear) =>
        (GetNthDayOfWeek(startYear + 1, 2, DayOfWeek.Wednesday, 2),
         GetNthDayOfWeek(startYear + 1, 2, DayOfWeek.Wednesday, 3));

    // ── Schedule generation ───────────────────────────────────────────────────

    public static List<ScheduledGame> GenerateSchedule(
        List<(string Home, string Away)> matchups,
        int  startYear = 2025,
        int? seed      = null)
    {
        // ── Calendar ──────────────────────────────────────────────────────────
        var seasonStart                = GetSeasonStart(startYear);
        var seasonEnd                  = GetSeasonEnd(startYear);
        var (allStarStart, allStarEnd) = GetAllStarBreak(startYear);

        var vdList = new List<DateTime>();
        for (var d = seasonStart; d <= seasonEnd; d = d.AddDays(1))
            if (d < allStarStart || d > allStarEnd)
                vdList.Add(d);

        DateTime[] vd        = vdList.ToArray();
        int        totalDays = vd.Length;

        // adj[d]    = vd[d] is exactly 1 calendar day after vd[d-1]  (false at break boundary)
        // calDay[d] = calendar days from season start to vd[d]
        bool[] adj    = new bool[totalDays];
        int[]  calDay = new int[totalDays];
        for (int d = 0; d < totalDays; d++)
        {
            calDay[d] = (int)(vd[d] - vd[0]).TotalDays;
            if (d > 0) adj[d] = calDay[d] == calDay[d - 1] + 1;
        }

        // ── Team registry ─────────────────────────────────────────────────────
        var allTeams = matchups
            .SelectMany(m => new[] { m.Home, m.Away })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var teamIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < allTeams.Count; i++) teamIdx[allTeams[i]] = i;
        int T = allTeams.Count;

        // ── Try up to 5 shuffles; pick the first violation-free result ─────────
        var masterRng = seed.HasValue ? new Random(seed.Value) : new Random();
        List<ScheduledGame>? best      = null;
        int                  bestViols = int.MaxValue;

        for (int attempt = 0; attempt < 5 && bestViols > 0; attempt++)
        {
            int thisSeed = attempt == 0 && seed.HasValue
                           ? seed.Value
                           : masterRng.Next();

            var candidate = RunOnePass(matchups, vd, totalDays, adj, calDay, allTeams, teamIdx, T, thisSeed);
            int viols     = CountViolations(candidate, vd, adj, calDay, totalDays);

            if (viols < bestViols) { best = candidate; bestViols = viols; }
        }

        best!.Sort((a, b) => a.Date.CompareTo(b.Date));
        return best;
    }

    // ── One scheduling pass ───────────────────────────────────────────────────

    private static List<ScheduledGame> RunOnePass(
        List<(string Home, string Away)> matchups,
        DateTime[] vd, int totalDays, bool[] adj, int[] calDay,
        List<string> allTeams, Dictionary<string, int> teamIdx, int T,
        int shuffleSeed)
    {
        bool[,] busy    = new bool[T, totalDays];
        int[]   teamB2B = new int[T];

        var rng     = new Random(shuffleSeed);
        var ordered = matchups.ToList();
        for (int i = ordered.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
        }

        int N      = ordered.Count;
        var result = new List<ScheduledGame>(N);

        // ── Greedy placement ──────────────────────────────────────────────────
        for (int idx = 0; idx < N; idx++)
        {
            var (home, away) = ordered[idx];
            if (!teamIdx.TryGetValue(home, out int hi) ||
                !teamIdx.TryGetValue(away, out int ai)) continue;

            int baseTarget = (int)((long)idx * totalDays / N);
            int jitter     = rng.Next(-4, 5);
            int target     = Math.Clamp(baseTarget + jitter, 0, totalDays - 1);

            bool avoidB2Bh  = teamB2B[hi] >= B2bSoftCap;
            bool avoidB2Bai = teamB2B[ai] >= B2bSoftCap;

            int chosen = FindSlot(hi, ai, target, busy, totalDays, adj, calDay, avoidB2Bh, avoidB2Bai);
            if (chosen < 0) continue;

            if (chosen > 0             && adj[chosen]     && busy[hi, chosen - 1]) teamB2B[hi]++;
            if (chosen + 1 < totalDays && adj[chosen + 1] && busy[hi, chosen + 1]) teamB2B[hi]++;
            if (chosen > 0             && adj[chosen]     && busy[ai, chosen - 1]) teamB2B[ai]++;
            if (chosen + 1 < totalDays && adj[chosen + 1] && busy[ai, chosen + 1]) teamB2B[ai]++;

            busy[hi, chosen] = true;
            busy[ai, chosen] = true;

            result.Add(new ScheduledGame(
                Guid.NewGuid(),
                vd[chosen],
                home, away,
                PrevRestDays(hi, chosen, busy, vd),
                PrevRestDays(ai, chosen, busy, vd)));
        }

        // ── Repair: fix residual hard-constraint violations ────────────────────
        for (int pass = 0; pass < 8; pass++)
        {
            bool anyRepaired = false;
            for (int t = 0; t < T; t++)
            {
                int violIdx = FindViolatingDay(t, busy, adj, calDay, totalDays);
                if (violIdx < 0) continue;

                string tName = allTeams[t];
                int gi = result.FindIndex(g =>
                    calDay[violIdx] == (int)(g.Date - vd[0]).TotalDays &&
                    (g.HomeTeamId.Equals(tName, StringComparison.OrdinalIgnoreCase) ||
                     g.AwayTeamId.Equals(tName, StringComparison.OrdinalIgnoreCase)));
                if (gi < 0) continue;

                var game  = result[gi];
                int hi_r  = teamIdx[game.HomeTeamId];
                int ai_r  = teamIdx[game.AwayTeamId];

                busy[hi_r, violIdx] = false;
                busy[ai_r, violIdx] = false;
                result.RemoveAt(gi);

                int newSlot = -1;
                for (int d = 0; d < totalDays; d++)
                    if (PassesHard(hi_r, ai_r, d, busy, totalDays, adj, calDay))
                    { newSlot = d; break; }

                if (newSlot >= 0)
                {
                    busy[hi_r, newSlot] = true;
                    busy[ai_r, newSlot] = true;
                    result.Add(new ScheduledGame(game.GameId, vd[newSlot],
                        game.HomeTeamId, game.AwayTeamId,
                        PrevRestDays(hi_r, newSlot, busy, vd),
                        PrevRestDays(ai_r, newSlot, busy, vd)));
                    anyRepaired = true;
                }
                else
                {
                    busy[hi_r, violIdx] = true;
                    busy[ai_r, violIdx] = true;
                    result.Add(game);
                }
            }
            if (!anyRepaired) break;
        }

        // ── B2B reduction: iteratively move games off B2B nights ──────────────
        // Recount actual B2Bs from the busy matrix (repair may have changed them)
        for (int t = 0; t < T; t++)
        {
            teamB2B[t] = 0;
            for (int d = 1; d < totalDays; d++)
                if (busy[t, d] && adj[d] && busy[t, d - 1]) teamB2B[t]++;
        }

        // Build a fast day-lookup: for each (team, calDay value) → result list index
        // We'll just scan result each time (1230 games is fast enough)
        for (int round = 0; round < 80; round++)
        {
            bool improved = false;

            // Process teams in descending B2B order so we fix the worst first
            var teamOrder = Enumerable.Range(0, T)
                .Where(t => teamB2B[t] > B2bTarget)
                .OrderByDescending(t => teamB2B[t])
                .ToList();

            foreach (int t in teamOrder)
            {
                if (teamB2B[t] <= B2bTarget) continue;

                // Try both nights of each B2B pair; take the one that succeeds
                bool fixedAny = false;
                for (int pass2 = 0; pass2 < 2 && !fixedAny; pass2++)
                {
                    // pass2=0: try the second night (D+1); pass2=1: try the first night (D)
                    int b2bNight = -1;
                    for (int d = 1; d < totalDays; d++)
                    {
                        if (!busy[t, d] || !adj[d] || !busy[t, d - 1]) continue;
                        b2bNight = (pass2 == 0) ? d : d - 1;
                        break;
                    }
                    if (b2bNight < 0) break;

                    string tName = allTeams[t];
                    int gi = result.FindIndex(g =>
                        calDay[b2bNight] == (int)(g.Date - vd[0]).TotalDays &&
                        (g.HomeTeamId.Equals(tName, StringComparison.OrdinalIgnoreCase) ||
                         g.AwayTeamId.Equals(tName, StringComparison.OrdinalIgnoreCase)));
                    if (gi < 0) continue;

                    var game = result[gi];
                    int hi_r = teamIdx[game.HomeTeamId];
                    int ai_r = teamIdx[game.AwayTeamId];

                    busy[hi_r, b2bNight] = false;
                    busy[ai_r, b2bNight] = false;
                    result.RemoveAt(gi);

                    // Full-season outward search for a valid non-B2B slot
                    int newSlot = -1;
                    for (int radius = 2; radius <= totalDays && newSlot < 0; radius++)
                    {
                        int d1 = b2bNight + radius;
                        int d2 = b2bNight - radius;
                        if (d1 < totalDays
                            && PassesHard(hi_r, ai_r, d1, busy, totalDays, adj, calDay)
                            && !CreatesB2B(hi_r, d1, busy, totalDays, adj)
                            && !CreatesB2B(ai_r, d1, busy, totalDays, adj))
                            newSlot = d1;
                        else if (d2 >= 0
                            && PassesHard(hi_r, ai_r, d2, busy, totalDays, adj, calDay)
                            && !CreatesB2B(hi_r, d2, busy, totalDays, adj)
                            && !CreatesB2B(ai_r, d2, busy, totalDays, adj))
                            newSlot = d2;
                    }

                    if (newSlot >= 0)
                    {
                        busy[hi_r, newSlot] = true;
                        busy[ai_r, newSlot] = true;
                        result.Add(new ScheduledGame(game.GameId, vd[newSlot],
                            game.HomeTeamId, game.AwayTeamId,
                            PrevRestDays(hi_r, newSlot, busy, vd),
                            PrevRestDays(ai_r, newSlot, busy, vd)));
                        improved = true;
                        fixedAny = true;
                    }
                    else
                    {
                        busy[hi_r, b2bNight] = true;
                        busy[ai_r, b2bNight] = true;
                        result.Add(game);
                    }
                }
            }

            // Recount B2Bs for next iteration
            for (int t = 0; t < T; t++)
            {
                teamB2B[t] = 0;
                for (int d = 1; d < totalDays; d++)
                    if (busy[t, d] && adj[d] && busy[t, d - 1]) teamB2B[t]++;
            }

            if (!improved) break;
        }

        return result;
    }

    /// <summary>Counts total 3-in-3 + 4-in-6 violations across all teams.</summary>
    private static int CountViolations(
        List<ScheduledGame> games, DateTime[] vd, bool[] adj, int[] calDay, int totalDays)
    {
        var allNames = games.SelectMany(g => new[] { g.HomeTeamId, g.AwayTeamId })
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var idx  = allNames.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i, StringComparer.OrdinalIgnoreCase);
        int tCnt = allNames.Count;
        bool[,] busy = new bool[tCnt, totalDays];
        foreach (var g in games)
        {
            int di   = (int)(g.Date - vd[0]).TotalDays;
            int dIdx = Array.BinarySearch(calDay, di);
            if (dIdx >= 0) { busy[idx[g.HomeTeamId], dIdx] = true; busy[idx[g.AwayTeamId], dIdx] = true; }
        }
        int total = 0;
        for (int t = 0; t < tCnt; t++)
            while (true)
            {
                int vi = FindViolatingDay(t, busy, adj, calDay, totalDays);
                if (vi < 0) break;
                total++;
                busy[t, vi] = false; // clear to find next distinct violation
            }
        return total;
    }

    /// <summary>
    /// Returns the valid-day index of the first constraint violation for team,
    /// or -1 if the team's schedule is clean.
    /// </summary>
    private static int FindViolatingDay(int team, bool[,] busy, bool[] adj, int[] calDay, int totalDays)
    {
        // 3-in-3: three calendar-consecutive game nights
        for (int d = 2; d < totalDays; d++)
        {
            if (!busy[team, d]) continue;
            if (adj[d] && busy[team, d - 1] && adj[d - 1] && busy[team, d - 2])
                return d;
        }
        // 4-in-6: more than 4 games in a 6-calendar-day window
        for (int d = 0; d < totalDays; d++)
        {
            if (!busy[team, d]) continue;
            int count = 1;
            for (int k = d + 1; k <= Math.Min(totalDays - 1, d + 6); k++)
            {
                if (calDay[k] - calDay[d] > 5) break;
                if (busy[team, k]) { count++; if (count == 5) return k; }
            }
        }
        return -1;
    }

    // ── Slot finder ───────────────────────────────────────────────────────────

    private static int FindSlot(
        int hi, int ai, int target,
        bool[,] busy, int totalDays, bool[] adj, int[] calDay,
        bool avoidB2Bh, bool avoidB2Bai)
    {
        int fwdEnd = Math.Min(totalDays - 1, target + 60);
        int bwdEnd = Math.Max(0,             target - 50);

        // Phase 1 & 2: local window, B2B avoidance active for teams at/above cap
        int slot = LocalScan(hi, ai, target,     fwdEnd, +1, busy, totalDays, adj, calDay, avoidB2Bh, avoidB2Bai);
        if (slot >= 0) return slot;
        slot = LocalScan(hi, ai, target - 1, bwdEnd, -1, busy, totalDays, adj, calDay, avoidB2Bh, avoidB2Bai);
        if (slot >= 0) return slot;

        // Phase 3 & 4: local window, B2B allowed
        slot = LocalScan(hi, ai, target,     fwdEnd, +1, busy, totalDays, adj, calDay, false, false);
        if (slot >= 0) return slot;
        slot = LocalScan(hi, ai, target - 1, bwdEnd, -1, busy, totalDays, adj, calDay, false, false);
        if (slot >= 0) return slot;

        // Phase 5: full season scan, B2B allowed
        for (int d = 0; d < totalDays; d++)
            if (PassesHard(hi, ai, d, busy, totalDays, adj, calDay)) return d;

        // Phase 6: absolute fallback — still enforces 3-in-3, skips 4-in-6
        for (int d = 0; d < totalDays; d++)
            if (!busy[hi, d] && !busy[ai, d]
                && !Creates3in3(hi, d, busy, totalDays, adj)
                && !Creates3in3(ai, d, busy, totalDays, adj))
                return d;

        // Phase 7: any free day (should never reach here)
        for (int d = 0; d < totalDays; d++)
            if (!busy[hi, d] && !busy[ai, d]) return d;

        return -1;
    }

    private static int LocalScan(
        int hi, int ai, int from, int to, int step,
        bool[,] busy, int totalDays, bool[] adj, int[] calDay,
        bool avoidB2Bh, bool avoidB2Bai)
    {
        int firstValid = -1, bestDay = -1;
        for (int d = from; step > 0 ? d <= to : d >= to; d += step)
        {
            if (!PassesHard(hi, ai, d, busy, totalDays, adj, calDay)) continue;
            if (avoidB2Bh  && CreatesB2B(hi, d, busy, totalDays, adj)) continue;
            if (avoidB2Bai && CreatesB2B(ai, d, busy, totalDays, adj)) continue;

            if (firstValid < 0) firstValid = d;
            if (RestParityOk(hi, ai, d, busy)) { bestDay = d; break; }
            if (Math.Abs(d - firstValid) >= 20) break;
        }
        return bestDay >= 0 ? bestDay : firstValid;
    }

    // ── Constraint helpers ────────────────────────────────────────────────────

    private static bool PassesHard(int hi, int ai, int d, bool[,] busy, int totalDays, bool[] adj, int[] calDay)
        => !busy[hi, d] && !busy[ai, d]
        && !Creates3in3(hi, d, busy, totalDays, adj) && !Creates3in3(ai, d, busy, totalDays, adj)
        && !Violates4in6(hi, d, busy, totalDays, calDay) && !Violates4in6(ai, d, busy, totalDays, calDay);

    private static bool RestParityOk(int hi, int ai, int d, bool[,] busy)
    {
        static int prev(int t, int dd, bool[,] b)
        { for (int k = dd - 1; k >= 0; k--) if (b[t, k]) return dd - k - 1; return 5; }
        return Math.Abs(prev(hi, d, busy) - prev(ai, d, busy)) <= 2;
    }

    /// <summary>
    /// True if scheduling on d creates 3 consecutive CALENDAR game nights.
    /// Uses adj[] to prevent false positives across the All-Star break gap.
    /// </summary>
    private static bool Creates3in3(int team, int d, bool[,] busy, int totalDays, bool[] adj)
    {
        bool p1 = d >= 1            && adj[d]     && busy[team, d - 1];
        bool p2 = d >= 2            && adj[d - 1] && busy[team, d - 2];
        bool n1 = d + 1 < totalDays && adj[d + 1] && busy[team, d + 1];
        bool n2 = d + 2 < totalDays && adj[d + 2] && busy[team, d + 2];
        return (p2 && p1) || (p1 && n1) || (n1 && n2);
    }

    /// <summary>
    /// True if scheduling on d pushes any 6-calendar-day window containing d past 4 games.
    /// </summary>
    private static bool Violates4in6(int team, int d, bool[,] busy, int totalDays, int[] calDay)
    {
        int cd = calDay[d];
        for (int s = cd - 5; s <= cd; s++)
        {
            int count = 1;
            for (int k = d - 1; k >= Math.Max(0, d - 6); k--)
            {
                int ck = calDay[k];
                if (ck < s) break;
                if (busy[team, k]) count++;
            }
            for (int k = d + 1; k <= Math.Min(totalDays - 1, d + 6); k++)
            {
                int ck = calDay[k];
                if (ck > s + 5) break;
                if (busy[team, k]) count++;
            }
            if (count > 4) return true;
        }
        return false;
    }

    /// <summary>True if scheduling on d creates a calendar back-to-back for the team.</summary>
    private static bool CreatesB2B(int team, int d, bool[,] busy, int totalDays, bool[] adj)
        => (d > 0             && adj[d]     && busy[team, d - 1])
        || (d + 1 < totalDays && adj[d + 1] && busy[team, d + 1]);

    /// <summary>Calendar days of rest before game on valid-day index d. Returns 5 if no prior game.</summary>
    private static int PrevRestDays(int team, int d, bool[,] busy, DateTime[] vd)
    {
        for (int k = d - 1; k >= 0; k--)
            if (busy[team, k]) return (int)(vd[d] - vd[k]).TotalDays - 1;
        return 5;
    }
}
