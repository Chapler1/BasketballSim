using System.Text.Json;
using BasketballSim.Models;

namespace BasketballSim.Services;

public class PlayerDataService(IWebHostEnvironment env, Nba2kCacheService nba2k)
{
    private string DataPath   => Path.Combine(env.ContentRootPath, "Data", "players.json");
    private string RosterPath => Path.Combine(env.ContentRootPath, "Data", "nba_roster.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string[] AttrKeys = [
        "Height","Strength","Speed","Jumping","Endurance",
        "Inside","Dunks","FreeThrow","MidRange","ThreePoint",
        "BasketballIQ","Dribbling","Passing","RebOff","RebDef",
        "PerimDef","IntDef","FoulTend",
    ];
    private static readonly string[] TendKeys = [
        "Touches","Drive","ThreePt","MidRange","PostUp",
        "Iso","PullUp","Cut","OffReb","Steal","Block",
    ];

    private PlayerDb? _db;
    public bool HasData => File.Exists(DataPath);
    public PlayerDb GetDb() => _db ??= Load();
    public IReadOnlyList<PlayerRecord> GetPlayers() => GetDb().Players;
    public IReadOnlyList<TeamMeta>     GetTeams()   => GetDb().Teams;
    public void Invalidate() => _db = null;

    public async Task SaveAsync()
    {
        if (_db is null) return;
        _db.SavedAt = DateTime.Now;
        await File.WriteAllTextAsync(DataPath, JsonSerializer.Serialize(_db, _opts));
    }

    private PlayerDb Load()
    {
        if (File.Exists(DataPath))
            return JsonSerializer.Deserialize<PlayerDb>(File.ReadAllText(DataPath), _opts) ?? new();
        if (!File.Exists(RosterPath)) return new();
        return GenerateFromSources();
    }

    private PlayerDb GenerateFromSources()
    {
        var lookup = nba2k.GetPlayers()
            .ToDictionary(p => EspnTeamFactory.NormalizeName(p.Name), p => p);

        using var doc    = JsonDocument.Parse(File.ReadAllText(RosterPath));
        var teamsArr     = doc.RootElement.GetProperty("teams");
        var db           = new PlayerDb();

        foreach (var t in teamsArr.EnumerateArray())
        {
            var teamName  = Str(t, "name")           ?? "";
            var abbr      = Str(t, "abbreviation")   ?? "";
            var primary   = Str(t, "primaryColor")   ?? "#888";
            var secondary = Str(t, "secondaryColor") ?? "#888";
            var div       = Str(t, "division")       ?? "";
            var conf      = Str(t, "conference")     ?? "";

            db.Teams.Add(new TeamMeta
            {
                Name = teamName, Abbr = abbr,
                PrimaryColor = primary, SecondaryColor = secondary,
                Division = div, Conference = conf,
            });

            if (!t.TryGetProperty("players", out var playersArr)) continue;
            foreach (var p in playersArr.EnumerateArray())
            {
                var pName  = Str(p, "name") ?? "Unknown";
                lookup.TryGetValue(EspnTeamFactory.NormalizeName(pName), out var d);

                var record = new PlayerRecord
                {
                    Name     = pName,
                    Team     = teamName,
                    TeamAbbr = abbr,
                    Positions = d?.Positions.ToList() ?? [Str(p, "espnPosition") == "C" ? "C" : "PG"],
                    Height   = d?.HeightStr ?? "6'7\"",
                    Overall  = d?.Overall   ?? 75,
                };

                if (d != null)
                {
                    foreach (var key in AttrKeys)
                    {
                        if (key == "Height")
                            // Use source value directly — already on the 5-100 scale, don't compress
                            record.Attrs[key] = d.Source.TryGetValue("Height", out double hv)
                                ? Math.Clamp((int)Math.Round(hv), 5, 100) : 50;
                        else
                            record.Attrs[key] = d.Mapped.TryGetValue(key, out double v)
                                ? Math.Clamp((int)Math.Round(v), 5, 95) : 50;
                    }
                    foreach (var (k, v) in d.Tendencies)
                        record.Tends[k] = v;
                    // ensure all tend keys present
                    foreach (var key in TendKeys)
                        if (!record.Tends.ContainsKey(key)) record.Tends[key] = 50;
                }
                else
                {
                    foreach (var key in AttrKeys) record.Attrs[key] = 50;
                    foreach (var key in TendKeys) record.Tends[key] = 50;
                }

                db.Players.Add(record);
            }
        }
        return db;
    }

    private static string? Str(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
