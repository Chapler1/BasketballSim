using BasketballSim.Models;

namespace BasketballSim.Services;

/// <summary>
/// Shared helper for building PlayerConfig rosters from ESPN + 2K data.
/// Used by SimLab and GameSim so they share identical team-building logic.
/// </summary>
public static class EspnTeamFactory
{
    // ── Name / position helpers ───────────────────────────────────────────────
    public static string NormalizeName(string name) =>
        name.ToLowerInvariant().Trim()
            .Replace(".", "").Replace("'", "").Replace("-", " ");

    public static Position? ParsePosition(string s) => s switch
    {
        "PG" => Position.PG, "SG" => Position.SG,
        "SF" => Position.SF, "PF" => Position.PF,
        "C"  => Position.C,  _    => (Position?)null,
    };

    // ── Permutation table (computed once at startup) ──────────────────────────
    private static readonly int[][] _perms = GeneratePermutations5();
    private static int[][] GeneratePermutations5()
    {
        var result = new List<int[]>();
        void Permute(int[] arr, int start)
        {
            if (start == arr.Length) { result.Add((int[])arr.Clone()); return; }
            for (int i = start; i < arr.Length; i++)
            {
                (arr[start], arr[i]) = (arr[i], arr[start]);
                Permute(arr, start + 1);
                (arr[start], arr[i]) = (arr[i], arr[start]);
            }
        }
        Permute([0, 1, 2, 3, 4], 0);
        return [.. result];
    }

    // ── Main entry point ─────────────────────────────────────────────────────
    /// <summary>
    /// Build an ordered roster of PlayerConfigs from ESPN + 2K data.
    /// Starters (indices 0–4) are the optimal 5-man lineup by total sim overall.
    /// Bench (indices 5+) are remaining pool players ordered by sim overall.
    /// </summary>
    public static List<PlayerConfig> BuildRoster(
        IReadOnlyList<EspnPlayer> espnPlayers,
        IReadOnlyDictionary<string, PlayerData> lookup,
        string slot,
        int maxBench = 7)
    {
        var allSlots = new[] { Position.PG, Position.SG, Position.SF, Position.PF, Position.C };

        // Build pool sorted by sim overall
        var pool = espnPlayers
            .Select(ep =>
            {
                lookup.TryGetValue(NormalizeName(ep.Name), out var d);
                var positions = d?.Positions
                    .Select(ParsePosition).Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList()
                    ?? [ep.Position];
                return (espn: ep, data2k: d, simOvr: d?.SimOverall ?? 45.0, positions);
            })
            .OrderByDescending(x => x.simOvr)
            .ToList();

        // Optimal lineup search (all C(n,5) combos × 5! permutations)
        int n = Math.Min(pool.Count, 12);
        double bestScore   = -1;
        int    bestPrimary = -1;
        var bestAssignment = new Dictionary<Position, (EspnPlayer espn, PlayerData? data2k)>();

        for (int a = 0; a < n - 4; a++)
        for (int b = a + 1; b < n - 3; b++)
        for (int c = b + 1; c < n - 2; c++)
        for (int d2 = c + 1; d2 < n - 1; d2++)
        for (int e2 = d2 + 1; e2 < n; e2++)
        {
            var combo = new[] { pool[a], pool[b], pool[c], pool[d2], pool[e2] };
            double score = combo.Sum(x => x.simOvr);
            if (score <= bestScore && bestAssignment.Count == 5) continue;

            foreach (var perm in _perms)
            {
                bool valid = true; int primaryCount = 0;
                for (int i = 0; i < 5; i++)
                {
                    var player = combo[perm[i]];
                    if (!player.positions.Contains(allSlots[i])) { valid = false; break; }
                    if (player.positions.Count > 0 && player.positions[0] == allSlots[i]) primaryCount++;
                }
                if (!valid) continue;
                if (score > bestScore || (score == bestScore && primaryCount > bestPrimary))
                {
                    bestScore    = score;
                    bestPrimary  = primaryCount;
                    bestAssignment = Enumerable.Range(0, 5)
                        .ToDictionary(i => allSlots[i], i => (combo[perm[i]].espn, combo[perm[i]].data2k));
                }
            }
        }

        // Fallback greedy fill (tiny roster / no valid 5-man lineup found)
        if (bestAssignment.Count < 5)
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (ep, d, _, positions) in pool)
            {
                if (usedNames.Contains(ep.Name)) continue;
                foreach (var pos in positions)
                {
                    if (!bestAssignment.ContainsKey(pos))
                    { bestAssignment[pos] = (ep, d); usedNames.Add(ep.Name); break; }
                }
                if (bestAssignment.Count == 5) break;
            }
            foreach (var s2 in allSlots)
            {
                if (bestAssignment.ContainsKey(s2)) continue;
                var fb = pool.FirstOrDefault(p => !bestAssignment.Values.Any(v => v.espn.Name == p.espn.Name));
                if (fb.espn is not null) bestAssignment[s2] = (fb.espn, fb.data2k);
            }
        }

        // Build ordered starter configs
        var configs = allSlots
            .Select(s => bestAssignment.TryGetValue(s, out var ent)
                ? BuildPlayerConfig(ent.espn, ent.data2k, slot, s)
                : new PlayerConfig { Name = s.ToString(), Team = slot, Position = s })
            .ToList();

        // Append bench players
        var usedStarters = new HashSet<string>(bestAssignment.Values.Select(v => v.espn.Name), StringComparer.OrdinalIgnoreCase);
        int benchSlots = maxBench;
        foreach (var (ep, d2k, _, positions) in pool)
        {
            if (benchSlots <= 0) break;
            if (usedStarters.Contains(ep.Name)) continue;
            var benchPos = positions.Count > 0 ? positions[0] : Position.SF;
            configs.Add(BuildPlayerConfig(ep, d2k, slot, benchPos));
            usedStarters.Add(ep.Name);
            benchSlots--;
        }

        return configs;
    }

    // ── PlayerConfig builder ──────────────────────────────────────────────────
    public static PlayerConfig BuildPlayerConfig(EspnPlayer espn, PlayerData? data2k, string team, Position pos)
    {
        var cfg = new PlayerConfig { Name = espn.Name, Team = team, Position = pos, Height = espn.HeightRating };
        if (data2k is null) return cfg;

        var m = data2k.Mapped;
        cfg.Inside           = Sim(m, "Inside");
        cfg.Dunks            = Sim(m, "Dunks");
        cfg.FreeThrow        = Sim(m, "FreeThrow");
        cfg.MidRange         = Sim(m, "MidRange");
        cfg.ThreePoint       = Sim(m, "ThreePoint");
        cfg.BasketballIQ     = Sim(m, "BasketballIQ");
        cfg.Dribbling        = Sim(m, "Dribbling");
        cfg.Passing          = Sim(m, "Passing");
        cfg.ReboundingOff    = Sim(m, "RebOff");
        cfg.ReboundingDef    = Sim(m, "RebDef");
        cfg.PerimeterDefense = Sim(m, "PerimDef");
        cfg.InteriorDefense  = Sim(m, "IntDef");
        cfg.FoulTendency     = Sim(m, "FoulTend");
        cfg.Speed            = Sim(m, "Speed");
        cfg.Strength         = Sim(m, "Strength");
        cfg.Jumping          = Sim(m, "Jumping");
        cfg.Endurance        = Sim(m, "Endurance");

        var t = data2k.Tendencies;
        cfg.Tend_Touches  = t.TryGetValue("Touches",  out int tu)  ? tu  : 50;
        cfg.Tend_Drive    = t.TryGetValue("Drive",   out int td)  ? td  : 50;
        cfg.Tend_ThreePt  = t.TryGetValue("ThreePt", out int tt3) ? tt3 : 50;
        cfg.Tend_MidRange = t.TryGetValue("MidRange",out int tm)  ? tm  : 50;
        cfg.Tend_PostUp   = t.TryGetValue("PostUp",  out int tpo) ? tpo : 50;
        cfg.Tend_Iso      = t.TryGetValue("Iso",     out int ti)  ? ti  : 50;
        cfg.Tend_PullUp   = t.TryGetValue("PullUp",  out int tpu) ? tpu : 50;
        cfg.Tend_Cut      = t.TryGetValue("Cut",     out int tc)  ? tc  : 50;
        cfg.Tend_OffReb   = t.TryGetValue("OffReb",  out int tor) ? tor : 50;
        cfg.Tend_Steal    = t.TryGetValue("Steal",   out int ts)  ? ts  : 50;
        cfg.Tend_Block    = t.TryGetValue("Block",   out int tb)  ? tb  : 50;

        return cfg;

        static int Sim(IReadOnlyDictionary<string, double> mapped, string key) =>
            mapped.TryGetValue(key, out double v) ? Math.Clamp((int)Math.Round(v), 5, 95) : 50;
    }

    // ── PlayerRecord-based overloads ──────────────────────────────────────────

    public static List<PlayerConfig> BuildRoster(
        IEnumerable<PlayerRecord> teamPlayers, int maxBench = 7)
    {
        var allSlots = new[] { Position.PG, Position.SG, Position.SF, Position.PF, Position.C };

        var pool = teamPlayers
            .Select(r =>
            {
                var positions = r.Positions
                    .Select(ParsePosition)
                    .Where(p => p.HasValue)
                    .Select(p => p!.Value)
                    .Distinct()
                    .ToList();
                if (positions.Count == 0 && ParsePosition(r.PrimaryPosition) is Position pp)
                    positions.Add(pp);
                // Compute sim overall from the player's actual attributes (players.json values)
                var attrDoubles = r.Attrs.ToDictionary(kv => kv.Key, kv => (double)kv.Value);
                double simOvr = AttributeMapper.ComputeOverall(attrDoubles);
                return (record: r, simOvr, positions);
            })
            .OrderByDescending(x => x.simOvr)
            .ToList();

        int n = Math.Min(pool.Count, 12);
        double bestScore   = -1;
        int    bestPrimary = -1;
        var bestAssignment = new Dictionary<Position, PlayerRecord>();

        for (int a = 0; a < n - 4; a++)
        for (int b = a + 1; b < n - 3; b++)
        for (int c = b + 1; c < n - 2; c++)
        for (int d = c + 1; d < n - 1; d++)
        for (int e = d + 1; e < n; e++)
        {
            var combo = new[] { pool[a], pool[b], pool[c], pool[d], pool[e] };
            double score = combo.Sum(x => x.simOvr);
            if (score <= bestScore && bestAssignment.Count == 5) continue;

            foreach (var perm in _perms)
            {
                bool valid = true; int primaryCount = 0;
                for (int i = 0; i < 5; i++)
                {
                    var player = combo[perm[i]];
                    if (!player.positions.Contains(allSlots[i])) { valid = false; break; }
                    if (player.positions.Count > 0 && player.positions[0] == allSlots[i]) primaryCount++;
                }
                if (!valid) continue;
                if (score > bestScore || (score == bestScore && primaryCount > bestPrimary))
                {
                    bestScore   = score;
                    bestPrimary = primaryCount;
                    bestAssignment = Enumerable.Range(0, 5)
                        .ToDictionary(i => allSlots[i], i => combo[perm[i]].record);
                }
            }
        }

        // Fallback greedy fill
        if (bestAssignment.Count < 5)
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (r, _, positions) in pool)
            {
                if (usedNames.Contains(r.Name)) continue;
                foreach (var pos in positions)
                {
                    if (!bestAssignment.ContainsKey(pos))
                    { bestAssignment[pos] = r; usedNames.Add(r.Name); break; }
                }
                if (bestAssignment.Count == 5) break;
            }
            foreach (var s in allSlots)
            {
                if (bestAssignment.ContainsKey(s)) continue;
                var fb = pool.FirstOrDefault(p => !bestAssignment.Values.Any(v => v.Name == p.record.Name));
                if (fb.record is not null) bestAssignment[s] = fb.record;
            }
        }

        var configs = allSlots
            .Select(s => bestAssignment.TryGetValue(s, out var r)
                ? BuildPlayerConfig(r, s)
                : new PlayerConfig { Name = s.ToString(), Team = "", Position = s })
            .ToList();

        var usedStarters = new HashSet<string>(bestAssignment.Values.Select(v => v.Name), StringComparer.OrdinalIgnoreCase);
        int benchSlots   = maxBench;
        foreach (var (r, _, positions) in pool)
        {
            if (benchSlots <= 0) break;
            if (usedStarters.Contains(r.Name)) continue;
            var benchPos = positions.Count > 0 ? positions[0] : Position.SF;
            configs.Add(BuildPlayerConfig(r, benchPos));
            usedStarters.Add(r.Name);
            benchSlots--;
        }

        return configs;
    }

    public static PlayerConfig BuildPlayerConfig(PlayerRecord r, Position pos)
    {
        static int A(Dictionary<string, int> d, string k) =>
            d.TryGetValue(k, out int v) ? Math.Clamp(v, 5, 95) : 50;

        return new PlayerConfig
        {
            Name = r.Name, Team = r.Team, Position = pos,
            Height           = A(r.Attrs, "Height"),
            Strength         = A(r.Attrs, "Strength"),
            Speed            = A(r.Attrs, "Speed"),
            Jumping          = A(r.Attrs, "Jumping"),
            Endurance        = A(r.Attrs, "Endurance"),
            Inside           = A(r.Attrs, "Inside"),
            Dunks            = A(r.Attrs, "Dunks"),
            FreeThrow        = A(r.Attrs, "FreeThrow"),
            MidRange         = A(r.Attrs, "MidRange"),
            ThreePoint       = A(r.Attrs, "ThreePoint"),
            BasketballIQ     = A(r.Attrs, "BasketballIQ"),
            Dribbling        = A(r.Attrs, "Dribbling"),
            Passing          = A(r.Attrs, "Passing"),
            ReboundingOff    = A(r.Attrs, "RebOff"),
            ReboundingDef    = A(r.Attrs, "RebDef"),
            PerimeterDefense = A(r.Attrs, "PerimDef"),
            InteriorDefense  = A(r.Attrs, "IntDef"),
            FoulTendency     = A(r.Attrs, "FoulTend"),
            Tend_Touches  = A(r.Tends, "Touches"),
            Tend_Drive    = A(r.Tends, "Drive"),
            Tend_ThreePt  = A(r.Tends, "ThreePt"),
            Tend_MidRange = A(r.Tends, "MidRange"),
            Tend_PostUp   = A(r.Tends, "PostUp"),
            Tend_Iso      = A(r.Tends, "Iso"),
            Tend_PullUp   = A(r.Tends, "PullUp"),
            Tend_Cut      = A(r.Tends, "Cut"),
            Tend_OffReb   = A(r.Tends, "OffReb"),
            Tend_Steal    = A(r.Tends, "Steal"),
            Tend_Block    = A(r.Tends, "Block"),
        };
    }

    // ── Team builder ──────────────────────────────────────────────────────────
    public static Team BuildTeam(List<PlayerConfig> configs, string name, string abbr,
                                  string color, double pace = 100, CoachingProfile? coach = null,
                                  int rotationDepth = 9, int starterBias = 80,
                                  string division = "", string conference = "",
                                  string secondaryColor = "#888888") => new()
    {
        Name           = name,
        Abbreviation   = abbr,
        PrimaryColor   = color,
        SecondaryColor = secondaryColor,
        Pace           = pace,
        Coach          = coach ?? CoachingProfiles.Balanced,
        RotationDepth  = Math.Clamp(rotationDepth, 5, configs.Count),
        StarterBias    = Math.Clamp(starterBias, 0, 100),
        Roster         = configs.Select(c => c.ToPlayer()).ToList(),
        Division       = division,
        Conference     = conference,
    };
}
