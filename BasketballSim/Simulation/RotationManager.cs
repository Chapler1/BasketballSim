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
    // ── Overall rating ────────────────────────────────────────────────────────
    // Composite used only for rotation priority. Does NOT affect play-by-play math.
    internal static double ComputeOverall(Player p) =>
        (p.Attr_Inside + p.Attr_MidRange + p.Attr_ThreePoint +
         p.Attr_BasketballIQ + p.Attr_Passing +
         p.Attr_PerimeterDefense + p.Attr_InteriorDefense +
         p.Attr_Rebounding_Off + p.Attr_Rebounding_Def +
         p.Speed + p.Endurance) / 11.0;

    // ── Target game minutes ───────────────────────────────────────────────────
    /// <summary>
    /// Allocate target game minutes for every player in the rotation (Roster[0..RotationDepth-1]).
    ///
    /// Starter targets:
    ///   base = 26 + qualityGap * 12   (26 min if backup is equal; 38 min if gap is huge)
    ///   + endurance bonus ±4 min
    ///   → clamped [20, 42]
    ///
    /// Bench targets:
    ///   remaining = 240 - Σ starter targets
    ///   distributed proportional to overall rating, clamped [2, 28] per player.
    ///
    /// 240 = 5 positions × 48 minutes.
    /// </summary>
    public static Dictionary<string, double> ComputeTargetMinutes(Team team)
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
            var s      = starters[i];
            var backup = FindDesignatedBackup(s, bench);

            double sOvr  = ComputeOverall(s);
            double bOvr  = backup != null ? ComputeOverall(backup) : sOvr - 30.0;
            // gap: 0 = backup is equal or better; 1 = 25+ point gap (starter is irreplaceable)
            double gap   = Math.Clamp((sOvr - bOvr) / 25.0, 0.0, 1.0);

            // Blend between equal share (bias=0) and gap-based target (bias=100)
            double gapBased = 26.0 + gap * 12.0;
            double baseMin  = equalShare + (gapBased - equalShare) * biasFactor;
            double endBonus = (s.Endurance - 50.0) / 50.0 * 4.0;      // ±4

            starterMin[i]   = Math.Clamp(baseMin + endBonus, 20.0, 42.0);
            targets[s.Name] = starterMin[i];
        }

        // Bench earns the remaining player-minutes
        double benchTotal = 240.0 - starterMin.Sum();
        if (bench.Count > 0 && benchTotal > 0)
        {
            double totalOvr = bench.Sum(ComputeOverall);
            if (totalOvr > 0)
            {
                foreach (var bp in bench)
                {
                    double share     = ComputeOverall(bp) / totalOvr;
                    targets[bp.Name] = Math.Clamp(benchTotal * share, 2.0, 28.0);
                }
            }
            else
            {
                foreach (var bp in bench)
                    targets[bp.Name] = benchTotal / bench.Count;
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
}
