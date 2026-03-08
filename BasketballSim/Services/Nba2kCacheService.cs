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
    IReadOnlyDictionary<string, double> Source);   // computed attr values (0-100 scale)

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
        new("BasketballIQ", "Basketball IQ",   "Skill",    r => (r.V("shotIQ")*2 + r.V("passIQ") + r.V("helpDefenseIQ") + r.V("offensiveConsistency") + r.V("defensiveConsistency")) / 6.0),
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

    // ── Load + map ───────────────────────────────────────────────────────────
    private List<PlayerData> Load()
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "nba2k_cache.json");
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

            // ── Height: "6'11\"" → inches → 0-100 scale ─────────────────────
            var heightStr    = p.TryStr("height") ?? "6'7\"";
            var inches       = ParseInches(heightStr);
            var heightRating = Math.Clamp((inches - 70.0) / (91.0 - 70.0) * 100.0, 0, 100);

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

            result.Add(new PlayerData(
                Name:      p.TryStr("name") ?? "Unknown",
                Team:      p.TryStr("team") ?? "Unknown",
                Overall:   overall,
                Positions: positions,
                HeightStr: heightStr,
                Source:    source));
        }

        return [.. result.OrderByDescending(p => p.Overall)];
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
