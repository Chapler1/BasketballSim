namespace BasketballSim.Services;

/// <summary>
/// Two-step transformation from raw 2K composite values to the sim's 5–95 scale.
///
/// Step 1 — Piecewise linear normalization: maps the empirical median → 50 exactly,
///           with [min, max] → [5, 95]. This corrects 2K's inflated medians
///           (e.g., ThreePoint median = 78 in 2K → 50 in sim).
///
/// Step 2 — Skew transform: sign-preserving power function around the midpoint.
///           s > 0 → right-skewed (elite rare, more upper separation).
///           s < 0 → left-skewed (floor high, reduce extreme upper stretch).
///           s = 0 → passes through unchanged (normalization result stands).
///
/// At tendency = 50 in either step, behavior is always identity.
/// </summary>
public static class AttributeMapper
{
    private record AttrStats(double Min, double Median, double Max, double SkewParam);

    // Empirical stats from 528 NBA 2K players (composite attributes as computed by Nba2kCacheService).
    // SkewParam chosen per attribute based on basketball distribution reasoning.
    private static readonly Dictionary<string, AttrStats> Stats = new()
    {
        // Inside composite (closeShot+layup+post avg): median inflated by close-shot, lower half stretched
        // after norm → apply +0.3 so elite finishers have more room at top
        ["Inside"]       = new(45.6,  62.4, 95.4, +0.30),

        // Dunks (drivingDunk+standingDunk avg): near-symmetric after norm, want mild right-skew
        ["Dunks"]        = new(25.0,  60.0, 90.0, +0.30),

        // FreeThrow: left-skewed in 2K (median 77), normalization stretches upper already — leave as-is
        ["FreeThrow"]    = new(37.0,  77.0, 97.0,  0.00),

        // MidRange: left-skewed (median 74), normalization already gives good upper separation
        ["MidRange"]     = new(25.0,  74.0, 98.0,  0.00),

        // ThreePoint: VERY left-skewed (median 78), normalization stretches elite shooters dramatically
        ["ThreePoint"]   = new(25.0,  78.0, 99.0,  0.00),

        // BasketballIQ: left-skewed after norm (upper compressed), push slightly right
        ["BasketballIQ"] = new(42.7,  64.0, 95.0, +0.30),

        // Dribbling: left-skewed (median 70.5), normalization handles upper separation well
        ["Dribbling"]    = new(30.0,  70.5, 95.5,  0.00),

        // Passing: slightly left-skewed after norm
        ["Passing"]      = new(31.0,  60.0, 96.0, +0.20),

        // OReb: right-skewed in 2K (median 47), but upper COMPRESSED after norm — correct strongly
        ["RebOff"]       = new(25.0,  47.0, 98.0, +0.70),

        // DReb: slightly left-skewed after norm — correct moderately
        ["RebDef"]       = new(33.0,  58.0, 97.0, +0.40),

        // PerimDef: slightly left-skewed after norm
        ["PerimDef"]     = new(27.5,  57.5, 96.0, +0.20),

        // IntDef: left-skewed after norm — elite shot-blockers/interior defenders should stand out
        ["IntDef"]       = new(25.0,  56.0, 97.5, +0.40),

        // Speed: left-skewed (median 74), normalization already stretches elite speed — leave
        ["Speed"]        = new(30.3,  74.0, 95.7,  0.00),

        // Strength: slightly left-skewed after norm
        ["Strength"]     = new(30.0,  60.0, 96.0, +0.20),

        // Jumping: left-skewed (median 75), normalization handles it
        ["Jumping"]      = new(40.0,  75.0, 99.0,  0.00),

        // Endurance: very narrow upper range (85–99 = 14 pts), normalization over-stretches top end
        // → reduce with negative skew so stamina differences aren't exaggerated
        ["Endurance"]    = new(60.0,  85.0, 99.0, -0.30),

        // Height: piecewise-linear already applied in Nba2kCacheService (6'5"=77" → 50 on 0-100).
        // Identity pass-through here so Mapped["Height"] mirrors Source["Height"].
        ["Height"]       = new( 0.0,  50.0, 100.0,  0.00),
    };

    // Attribute weights for computing sim overall.
    // Balanced so guards/wings/bigs produce similar distribution curves.
    // Reduced guard-heavy attrs (Dribbling, Mid, 3PT slightly), raised big-heavy (RebDef, RebOff, Strength, IntDef).
    private static readonly Dictionary<string, double> Weights = new()
    {
        ["Inside"]       = 1.3,   // was 1.5 — still top but less center-tilted
        ["Dunks"]        = 0.4,   // was 0.5 — cosmetic, not position-defining
        ["FreeThrow"]    = 0.5,   // unchanged
        ["MidRange"]     = 1.0,   // was 1.2 — reduce, guards/wings heavy
        ["ThreePoint"]   = 1.1,   // was 1.2 — slight reduce
        ["BasketballIQ"] = 1.3,   // was 1.5 — still top, more universal now
        ["Dribbling"]    = 0.7,   // was 1.0 — guard-heavy, reduce significantly
        ["Passing"]      = 0.9,   // was 0.8 — slight increase, more universal
        ["RebOff"]       = 0.8,   // was 0.6 — increase, benefits bigs
        ["RebDef"]       = 1.0,   // was 0.8 — increase, benefits bigs
        ["PerimDef"]     = 1.1,   // was 1.2 — slight reduce
        ["IntDef"]       = 1.1,   // was 1.0 — slight increase, benefits bigs
        ["Speed"]        = 0.9,   // was 1.0 — slight reduce, guard-leaning
        ["Strength"]     = 0.7,   // was 0.5 — increase, benefits bigs
        ["Jumping"]      = 0.6,   // was 0.7 — slight reduce
        ["Endurance"]    = 0.4,   // was 0.3 — increase, universal
    };

    private const double SimMin = 5.0, SimMax = 95.0, SimMid = 50.0;

    public static IReadOnlyCollection<string> AttrKeys => Stats.Keys;

    /// <summary>Maps a single raw 2K composite value to [5, 95].</summary>
    public static double MapToSim(double raw, string key)
    {
        if (!Stats.TryGetValue(key, out var st)) return SimMid;

        // Step 1: piecewise linear normalization — empirical median always → 50
        double t;
        if (raw >= st.Median)
            t = (raw - st.Median) / Math.Max(st.Max - st.Median, 1.0);
        else
            t = -(st.Median - raw) / Math.Max(st.Median - st.Min, 1.0);

        t = Math.Clamp(t, -1.0, 1.0);

        // Step 2: sign-preserving power skew — s=0 is identity
        double tSkewed = t;
        if (st.SkewParam != 0.0)
        {
            if (t >= 0)
                tSkewed =  Math.Pow(t,  1.0 / (1.0 + st.SkewParam));
            else
                tSkewed = -Math.Pow(-t, 1.0 + st.SkewParam);
        }

        return Math.Clamp(SimMid + tSkewed * (SimMax - SimMid), SimMin, SimMax);
    }

    /// <summary>Maps all composite attributes for a player from their source dict.</summary>
    public static IReadOnlyDictionary<string, double> MapFromSource(IReadOnlyDictionary<string, double> source)
    {
        var result = new Dictionary<string, double>(Stats.Count);
        foreach (var key in Stats.Keys)
            result[key] = source.TryGetValue(key, out double raw) ? MapToSim(raw, key) : SimMid;
        return result;
    }

    /// <summary>Weighted average of mapped attributes → single sim overall [5, 95].</summary>
    public static double ComputeOverall(IReadOnlyDictionary<string, double> mapped)
    {
        double totalWeight = 0, weightedSum = 0;
        foreach (var (key, weight) in Weights)
        {
            if (mapped.TryGetValue(key, out double val))
            {
                weightedSum += val * weight;
                totalWeight += weight;
            }
        }
        return totalWeight > 0 ? Math.Clamp(weightedSum / totalWeight, SimMin, SimMax) : SimMid;
    }
}
