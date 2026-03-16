using System.Text.Json;

namespace BasketballSim.Services;

// ── Attribute definition ─────────────────────────────────────────────────────
public record AttrDef(
    string Key,
    string Label,
    string Group,
    Func<IReadOnlyDictionary<string, int>, double>? Compute,   // null = height
    bool IsHeight = false);

// ── Per-player data ──────────────────────────────────────────────────────────
public record PlayerData(
    string Name,
    string Team,
    int    Overall,
    string[] Positions,
    string HeightStr,
    IReadOnlyDictionary<string, double> Source,      // raw composite values (0-100 scale)
    IReadOnlyDictionary<string, double> Mapped,      // transformed to sim scale [5, 95]
    double SimOverall,                               // weighted sim overall [5, 95]
    IReadOnlyDictionary<string, int> Tendencies);    // derived tendencies [5, 95], 50=avg

public class Nba2kCacheService(IWebHostEnvironment env)
{
    private List<PlayerData>? _cache;

    // ── Attribute definitions ────────────────────────────────────────────────
    public static readonly AttrDef[] Attributes =
    [
        // Physical
        new("Height",       "Height",          "Physical", null,            IsHeight: true),
        new("Strength",     "Strength",        "Physical", r => r.V("strength")),
        new("Speed",        "Speed",           "Physical", r => Avg(r, "speed","speedWithBall","agility")),
        new("Jumping",      "Jumping",         "Physical", r => r.V("vertical")),
        new("Endurance",    "Endurance",       "Physical", r => r.V("stamina")),
        // Shooting
        new("Inside",       "Inside",          "Shooting", r => Avg(r, "closeShot","drivingLayup","postHook","postFade","postControl")),
        new("Dunks",        "Dunks",           "Shooting", r => Avg(r, "drivingDunk","standingDunk")),
        new("FreeThrow",    "Free Throw",      "Shooting", r => r.V("freeThrow")),
        new("MidRange",     "Mid Range",       "Shooting", r => r.V("midRangeShot")),
        new("ThreePoint",   "Three Point",     "Shooting", r => r.V("threePointShot")),
        // Skill
        new("oBBIQ",        "Offensive IQ",    "Skill",    r => (r.V("shotIQ")*2 + r.V("passIQ") + r.V("offensiveConsistency")) / 4.0),
        new("dBBIQ",        "Defensive IQ",    "Skill",    r => (r.V("helpDefenseIQ")*2 + r.V("defensiveConsistency")) / 3.0),
        new("Hustle",       "Hustle",          "Skill",    r => r.V("hustle")),
        new("Dribbling",    "Dribbling",       "Skill",    r => Avg(r, "ballHandle","speedWithBall")),
        new("Passing",      "Passing",         "Skill",    r => Avg(r, "passAccuracy","passVision","passIQ")),
        new("RebOff",       "Off Rebounding",  "Skill",    r => r.V("offensiveRebound")),
        new("RebDef",       "Def Rebounding",  "Skill",    r => r.V("defensiveRebound")),
        // Defense
        new("PerimDef",     "Perimeter Def",   "Defense",  r => Avg(r, "perimeterDefense","steal")),
        new("IntDef",       "Interior Def",    "Defense",  r => Avg(r, "interiorDefense","block")),
    ];

    private static double Avg(IReadOnlyDictionary<string, int> r, params string[] keys) =>
        keys.Average(k => r.V(k));

    // ── Public API ───────────────────────────────────────────────────────────
    public IReadOnlyList<PlayerData> GetPlayers() => _cache ??= Load();

    /// <summary>Called by EspnRosterSyncService at startup to apply current team assignments.</summary>
    public void SetEspnTeams(IEnumerable<(string playerName, string teamName)> assignments)
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, team) in assignments)
            overrides[name] = team;

        _ = GetPlayers(); // ensure cache built before patching
        if (_cache is null) return;

        _cache = [.. _cache.Select(p =>
            overrides.TryGetValue(p.Name, out var t) ? p with { Team = t } : p)
            .OrderByDescending(p => p.Overall)];
    }

    // ── Load + map ───────────────────────────────────────────────────────────
    /// <summary>Standalone loader — for use outside of DI (e.g. calibration runner).</summary>
    public static IReadOnlyList<PlayerData> LoadFromPath(string jsonPath) =>
        LoadInternal(jsonPath);

    private List<PlayerData> Load() =>
        LoadInternal(Path.Combine(env.ContentRootPath, "Data", "nba2k_cache.json"));

    private static List<PlayerData> LoadInternal(string path)
    {
        if (!File.Exists(path)) return [];

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("players", out var arr)) return [];

        var result = new List<PlayerData>();

        foreach (var p in arr.EnumerateArray())
        {
            // ── Raw 2K attributes ────────────────────────────────────────────
            var raw = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (p.TryGetProperty("attributes", out var attrs))
                foreach (var a in attrs.EnumerateObject())
                    if (a.Value.TryGetInt32(out int v)) raw[a.Name] = v;

            // ── Height: inches → user scale (6'7"=79"→50, each inch = 5 pts) ──
            // 5'10"=70"→5, 6'7"=79"→50, 7'5"=89"→100 — formula: (inches-70)*5+5
            var heightStr = p.TryStr("height") ?? "6'7\"";
            var inches    = ParseInches(heightStr);
            double heightRating = Math.Clamp((inches - 70) * 5 + 5, 5, 100);

            // ── Compute all source values ────────────────────────────────────
            var source = new Dictionary<string, double>(Attributes.Length);
            foreach (var attr in Attributes)
                source[attr.Key] = attr.IsHeight ? heightRating : attr.Compute!(raw);

            // ── Positions ────────────────────────────────────────────────────
            string[] positions = p.TryGetProperty("positions", out var posArr)
                ? [.. posArr.EnumerateArray().Select(x => x.GetString() ?? "?")]
                : ["?"];

            int overall = p.TryGetProperty("overall", out var ov) && ov.TryGetInt32(out int ovv)
                ? ovv : 75;

            var mapped     = AttributeMapper.MapFromSource(source);
            var simOverall = AttributeMapper.ComputeOverall(mapped);
            var tendencies = DeriveTendencies(raw, mapped);

            result.Add(new PlayerData(
                Name:       p.TryStr("name") ?? "Unknown",
                Team:       p.TryStr("team") ?? "Unknown",
                Overall:    overall,
                Positions:  positions,
                HeightStr:  heightStr,
                Source:     source,
                Mapped:     mapped,
                SimOverall: simOverall,
                Tendencies: tendencies));
        }

        return [.. result.OrderByDescending(p => p.Overall)];
    }

    // ── Tendency derivation from raw 2K attributes ────────────────────────────
    // 50 = league average, 5 = lowest, 95 = highest. Piecewise linear normalization.
    private static IReadOnlyDictionary<string, int> DeriveTendencies(
        Dictionary<string, int> r, IReadOnlyDictionary<string, double> mapped)
    {
        static int T(double raw, double min, double median, double max)
        {
            double t = raw >= median
                ? (raw - median) / Math.Max(max - median, 1.0)
                : -(median - raw) / Math.Max(median - min, 1.0);
            return Math.Clamp((int)Math.Round(50.0 + Math.Clamp(t, -1.0, 1.0) * 45.0), 5, 95);
        }

        // Composite proxies
        double driveProxy   = r.V("drivingLayup") * 0.30 + r.V("drivingDunk") * 0.20
                            + r.V("speedWithBall") * 0.30 + r.V("agility") * 0.20;
        double postProxy    = (r.V("postControl") + r.V("postHook") + r.V("postFade")) / 3.0;
        double isoProxy     = r.V("midRangeShot") * 0.45 + r.V("ballHandle") * 0.35
                            + r.V("shotIQ") * 0.20;
        double pullProxy    = (r.V("midRangeShot") + r.V("ballHandle")) / 2.0;
        double cutProxy     = r.V("agility") * 0.50 + r.V("speed") * 0.30 + r.V("hustle") * 0.20;
        // Touches: uses the sim's own mapped attributes (already [5,95] scale) so it reflects
        // our attribute system, not raw 2K values. Weighted average = already in [5,95].
        static double M(IReadOnlyDictionary<string, double> m, string k) =>
            m.TryGetValue(k, out double v) ? v : 50.0;
        double touchesProxy = M(mapped, "Dribbling")    * 0.28
                            + M(mapped, "oBBIQ")        * 0.22
                            + M(mapped, "MidRange")     * 0.14
                            + M(mapped, "ThreePoint")   * 0.10
                            + M(mapped, "Inside")       * 0.08
                            + M(mapped, "Passing")      * 0.08
                            + M(mapped, "Speed")        * 0.07
                            + M(mapped, "Strength")     * 0.03;

        return new Dictionary<string, int>
        {
            // Offensive
            ["Touches"]  = Math.Clamp((int)Math.Round(touchesProxy), 5, 95),
            ["Drive"]    = T(driveProxy,              28, 68, 92),
            ["ThreePt"]  = T(r.V("threePointShot"),  25, 78, 99),
            ["MidRange"] = T(r.V("midRangeShot"),    25, 74, 98),
            ["PostUp"]   = T(postProxy,              25, 55, 90),
            ["Iso"]      = T(isoProxy,               30, 70, 96),
            ["PullUp"]   = T(pullProxy,              25, 72, 97),
            ["Cut"]      = T(cutProxy,               40, 75, 96),
            ["OffReb"]   = T(r.V("offensiveRebound"), 25, 47, 98),
            // Defensive
            ["Steal"]    = T(r.V("steal"),           20, 55, 90),
            ["Block"]    = T(r.V("block"),           25, 55, 99),
        };
    }

    private static int ParseInches(string h)
    {
        h = h.Replace("\"", "").Replace("\u0022", "").Trim();
        var parts = h.Split('\'');
        if (parts.Length < 2 || !int.TryParse(parts[0].Trim(), out int feet)) return 79;
        int.TryParse(parts[1].Trim(), out int inches);
        return feet * 12 + inches;
    }
}

// ── Extension helpers ────────────────────────────────────────────────────────
file static class Ext
{
    public static double V(this IReadOnlyDictionary<string, int> d, string k) =>
        d.TryGetValue(k, out int v) ? v : 50;

    public static string? TryStr(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
