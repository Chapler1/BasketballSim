using BasketballSim.Models;

namespace BasketballSim.Simulation;

/// <summary>
/// Mirrors real NBA coach rotation logic.
///
/// Pre-game: target minutes are set by the quality gap between a starter and their best
/// available positional backup, adjusted by the starter's endurance. Large gap = starter
/// plays more (scarce resource). Small gap = backup earns more burn time. Bench minutes
/// are distributed proportionally by overall rating within RotationDepth.
///
/// In-game substitution triggers (in priority order):
///   1. Foul-out (6 fouls) — forced replacement.
///   2. Foul trouble — NBA-threshold subs: 2 fouls in Q1, 3 before half, 4 in Q3.
///   3. Stint expired — player has played their continuous target stint; needs rest.
///   4. Game minutes exhausted — player has hit their target for the game.
///   5. Garbage time — blowout in Q4, give deep bench their minutes.
/// Clutch time (Q4 ≤ 5 min, margin ≤ 5) overrides routine rotation — starters close.
/// </summary>
public static class RotationManager
{
    // ── Fatigue minutes multiplier ────────────────────────────────────────────
    // Gentle linear taper: only 25% of the SF deficit is applied as a minutes penalty.
    // SF=100 → 1.00 (full minutes), SF=90 → 0.975, SF=85 → 0.963, SF=80 → 0.950.
    // At drain=0.42 + restDays~2, equilibrium lands at SF≈80 → ~33 min for a 35-min player.
    // Floored at 0.50 so even very tired players still contribute.
    private static double FatigueMinutesMult(double sf) =>
        Math.Clamp(1.0 - (1.0 - sf / 100.0) * 0.25, 0.50, 1.00);

    // ── Overall rating ────────────────────────────────────────────────────────
    // Composite used only for rotation priority. Does NOT affect play-by-play math.
    internal static double ComputeOverall(Player p) =>
        (p.Attr_Inside + p.Attr_MidRange + p.Attr_ThreePoint +
         p.Attr_oBBIQ + p.Attr_dBBIQ + p.Attr_Hustle + p.Attr_Passing +
         p.Attr_PerimeterDefense + p.Attr_InteriorDefense +
         p.Attr_Rebounding_Off + p.Attr_Rebounding_Def +
         p.Speed + p.Endurance) / 13.0;

    // Fatigue-adjusted overall: fatigued players look worse to the rotation algorithm,
    // earning fewer minutes. At SF=100 → mult=1.0; at SF=60 → mult=0.84; at SF=55 → mult=0.82.
    private static double AdjustedOverall(Player p, IReadOnlyDictionary<string, double>? fatigue)
    {
        double mult = fatigue != null && fatigue.TryGetValue(p.Name, out double sf)
            ? 0.60 + sf / 100.0 * 0.40 : 1.0;
        return ComputeOverall(p) * mult;
    }

    // Coach vet-preference adjustment: veterans (age≥28) get up to +15% if coach prefers experience;
    // young players (age≤23) get up to +15% if coach is youth-forward (VetPreference < 50).
    private static double VetAdjustedOverall(Player p, IReadOnlyDictionary<string, double>? fatigue, Coach coach)
    {
        double overall = AdjustedOverall(p, fatigue);
        double vetMult = 1.0;
        if (p.Age >= 28 && coach.VetPreference > 50)
            vetMult = 1.0 + (coach.VetPreference - 50) / 50.0 * 0.15;
        else if (p.Age <= 23 && coach.VetPreference < 50)
            vetMult = 1.0 + (50 - coach.VetPreference) / 50.0 * 0.15;
        return overall * vetMult;
    }

    // ── Target game minutes ───────────────────────────────────────────────────
    /// <summary>
    /// Allocate target game minutes for every player in the rotation (Roster[0..RotationDepth-1]).
    ///
    /// Starter targets:
    ///   base = 24 + qualityGap * 9   (24 min if backup is equal; 33 min if gap is huge)
    ///   blended with equalShare by StarterBias, endurance bonus ±1.5 min
    ///   → clamped [18, 36]
    ///
    /// Bench targets:
    ///   remaining = 240 - Σ starter targets
    ///   distributed proportional to overall rating, clamped [2, 22] per player.
    ///
    /// 240 = 5 positions × 48 minutes.
    /// </summary>
    public static Dictionary<string, double> ComputeTargetMinutes(
        Team team,
        IReadOnlyDictionary<string, double>? fatigue = null,
        IReadOnlyCollection<string>? dnp = null)
    {
        var targets = new Dictionary<string, double>();
        int depth   = Math.Clamp(team.RotationDepth, 5, team.Roster.Count);
        var rotation = team.Roster.Take(depth).ToList();
        var starters = rotation.Take(5).ToList();
        var bench    = rotation.Skip(5).ToList();

        double equalShare  = 240.0 / depth;                      // equal minutes if no bias
        double biasFactor  = Math.Clamp(team.StarterBias / 100.0, 0.0, 1.0);

        double[] starterMin = new double[5];
        for (int i = 0; i < 5; i++)
        {
            var s = starters[i];

            if (dnp != null && dnp.Contains(s.Name))
            {
                targets[s.Name] = 0;
                starterMin[i]   = 0;
                continue;
            }

            var backup = FindDesignatedBackup(s, bench.Where(p => dnp == null || !dnp.Contains(p.Name)));

            double sOvr  = VetAdjustedOverall(s, fatigue, team.Coach);
            double bOvr  = backup != null ? VetAdjustedOverall(backup, fatigue, team.Coach) : sOvr - 30.0;
            // gap: 0 = backup is equal or better; 1 = 25+ point gap (starter is irreplaceable)
            double gap   = Math.Clamp((sOvr - bOvr) / 25.0, 0.0, 1.0);

            // Blend between equal share (bias=0) and gap-based target (bias=100)
            double gapBased = 24.0 + gap * 9.0;
            double baseMin  = equalShare + (gapBased - equalShare) * biasFactor;
            double endBonus = (s.Endurance - 50.0) / 50.0 * 2.5;      // ±2.5 (high endurance earns more mins)

            double sfMult = fatigue != null && fatigue.TryGetValue(s.Name, out double sf)
                ? FatigueMinutesMult(sf) : 1.0;
            starterMin[i]   = Math.Clamp((baseMin + endBonus) * sfMult, 10.0, 36.0);
            targets[s.Name] = starterMin[i];
        }

        // Bench earns the remaining player-minutes
        double benchTotal = 240.0 - starterMin.Sum();
        var activeBench = bench.Where(p => dnp == null || !dnp.Contains(p.Name)).ToList();
        foreach (var bp in bench.Where(p => dnp != null && dnp.Contains(p.Name)))
            targets[bp.Name] = 0;

        if (activeBench.Count > 0 && benchTotal > 0)
        {
            double totalOvr = activeBench.Sum(p => VetAdjustedOverall(p, fatigue, team.Coach));
            if (totalOvr > 0)
            {
                foreach (var bp in activeBench)
                {
                    double share   = VetAdjustedOverall(bp, fatigue, team.Coach) / totalOvr;
                    double bsfMult = fatigue != null && fatigue.TryGetValue(bp.Name, out double bsf)
                        ? FatigueMinutesMult(bsf) : 1.0;
                    targets[bp.Name] = Math.Clamp(benchTotal * share * bsfMult, 1.0, 22.0);
                }
            }
            else
            {
                foreach (var bp in activeBench)
                    targets[bp.Name] = benchTotal / activeBench.Count;
            }
        }

        return targets;
    }

    // ── Target stint length ───────────────────────────────────────────────────
    /// <summary>
    /// How many consecutive minutes a player plays before being pulled for rest.
    /// More target minutes → longer stints (fewer breaks needed).
    /// Higher endurance → extended stints (±2 min).
    /// Result is clamped [2.5, 14] minutes.
    /// </summary>
    public static double TargetStintMinutes(Player p, double gameTargetMinutes)
    {
        // ~4 stints per 32-min starter; bench plays 1-2 shorter stints
        double baseStint  = Math.Clamp(gameTargetMinutes / 4.0, 3.0, 12.0);
        double endBonus   = (p.Endurance - 50.0) / 50.0 * 2.0;   // ±2
        return Math.Clamp(baseStint + endBonus, 2.5, 14.0);
    }

    // ── Positional backup matching ────────────────────────────────────────────
    /// <summary>
    /// Find the best available backup for a starter.
    /// Priority: same position → adjacent position → any available player.
    /// "Best" = highest ComputeOverall.
    /// </summary>
    public static Player? FindDesignatedBackup(Player starter, IEnumerable<Player> bench)
    {
        var list = bench.ToList();
        if (list.Count == 0) return null;

        var same = list.Where(p => p.Position == starter.Position)
                       .OrderByDescending(ComputeOverall).FirstOrDefault();
        if (same != null) return same;

        var adj = list.Where(p => AdjacentPositions(starter.Position).Contains(p.Position))
                      .OrderByDescending(ComputeOverall).FirstOrDefault();
        if (adj != null) return adj;

        return list.OrderByDescending(ComputeOverall).First();
    }

    private static Position[] AdjacentPositions(Position pos) => pos switch
    {
        Position.PG => [Position.SG],
        Position.SG => [Position.PG, Position.SF],
        Position.SF => [Position.SG, Position.PF],
        Position.PF => [Position.SF, Position.C],
        Position.C  => [Position.PF],
        _           => []
    };

    // ── Proportional shortfall ────────────────────────────────────────────
    /// <summary>
    /// Positive = player is behind their proportional target (wants to play).
    /// Negative = player has played more than their share so far (should rest).
    /// </summary>
    public static double ComputeShortfall(double totalGameMin, double targetMin, double gameMin)
        => (totalGameMin / 48.0) * targetMin - gameMin;

    // ── Position-flexible lineup construction ─────────────────────────────
    /// <summary>
    /// Greedy positional assignment from a pre-sorted pool (C → PF → SF → SG → PG,
    /// most-constrained first). Tries primary + adjacent positions first; falls back
    /// to any remaining player if a slot truly can't be covered positionally (e.g.
    /// a guard-heavy bench). Returns null only when fewer than 5 players are in the pool.
    /// </summary>
    public static List<Player>? BuildPositionalLineup(List<Player> pool)
    {
        if (pool.Count < 5) return null;

        var assigned = new List<Player>(5);
        var used     = new HashSet<string>();

        foreach (var pos in s_positionOrder)
        {
            // Prefer positional match (primary or adjacent)
            var candidate = pool.FirstOrDefault(p =>
                !used.Contains(p.Name) &&
                (p.Position == pos || AdjacentPositions(p.Position).Contains(pos)));

            // Fallback: any remaining player (handles guard-heavy benches, etc.)
            candidate ??= pool.FirstOrDefault(p => !used.Contains(p.Name));

            if (candidate == null) return null;
            assigned.Add(candidate);
            used.Add(candidate.Name);
        }

        return assigned;
    }

    private static readonly Position[] s_positionOrder =
        [Position.C, Position.PF, Position.SF, Position.SG, Position.PG];

    /// <summary>
    /// Build the ideal 5-player lineup from the rotation based on proportional shortfall.
    /// Players in <paramref name="unavailable"/> (DNP, fouled out) are excluded.
    ///
    /// Primary pool: players whose shortfall is above the threshold (on or behind schedule).
    /// Fallback: if fewer than 5 qualify, fill remaining slots with the rested players
    /// closest to break-even (least-negative shortfall first) so the rotation always
    /// happens — even on a guard-heavy bench with only 3 eligible players.
    ///
    /// Pool is sorted by shortfall (descending) so bench players who need minutes most
    /// are assigned to positions before starters who just had a rest cut short.
    /// </summary>
    public static List<Player>? BuildDesiredLineup(
        Team   team,
        Dictionary<string, double> targetMin,
        Dictionary<string, double> gameMin,
        Dictionary<string, double> eligibleAt,
        double totalGameMin,
        HashSet<string> unavailable)
    {
        int depth = Math.Clamp(team.RotationDepth, 5, team.Roster.Count);

        const double entryThreshold = -0.5; // don't pull for tiny negative shortfall

        double Shortfall(Player p) => ComputeShortfall(
            totalGameMin, targetMin.GetValueOrDefault(p.Name, 20.0), gameMin.GetValueOrDefault(p.Name));

        var eligible = team.Roster.Take(depth)
            .Where(p =>
                !unavailable.Contains(p.Name) &&
                totalGameMin >= eligibleAt.GetValueOrDefault(p.Name) &&
                gameMin.GetValueOrDefault(p.Name) < targetMin.GetValueOrDefault(p.Name, 20.0) + 1.0)
            .ToList();

        // Primary pool: players who want to play (shortfall above threshold)
        var pool = eligible
            .Where(p => Shortfall(p) > entryThreshold)
            .OrderByDescending(Shortfall)
            .ToList();

        // Fallback: when bench is small, fill remaining slots with rested players
        // sorted closest-to-break-even first (least negative shortfall = smallest debt)
        if (pool.Count < 5)
        {
            var poolNames = pool.Select(p => p.Name).ToHashSet();
            var extras = eligible
                .Where(p => !poolNames.Contains(p.Name))
                .OrderByDescending(Shortfall)
                .Take(5 - pool.Count);
            pool.AddRange(extras);
        }

        if (pool.Count < 5) return null; // truly not enough available players

        return BuildPositionalLineup(pool);
    }
}
