using System.Text.Json;
using System.Text.RegularExpressions;
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
        "oBBIQ","dBBIQ","Hustle","Dribbling","Passing","RebOff","RebDef",
        "PerimDef","IntDef","FoulTend",
        // Injury system — not sourced from 2K
        "DomHand",
        "InjHead","InjNeck",
        "InjLShoulder","InjRShoulder","InjLUpperArm","InjRUpperArm",
        "InjLElbow","InjRElbow","InjLForearm","InjRForearm",
        "InjLWrist","InjRWrist","InjLHand","InjRHand","InjLFingers","InjRFingers",
        "InjChest","InjUpperBack","InjAbdominals","InjLowerBack","InjLOblique","InjROblique",
        "InjLHip","InjRHip","InjLHamstring","InjRHamstring","InjLQuad","InjRQuad",
        "InjLKnee","InjRKnee","InjLShinCalf","InjRShinCalf",
        "InjLAchilles","InjRAchilles","InjLAnkle","InjRAnkle",
        "InjLFoot","InjRFoot","InjLToes","InjRToes",
    ];

    // Keys not sourced from 2K — defaults applied, never overwritten by Re-map.
    private static readonly HashSet<string> _nonMappedKeys = [
        "DomHand",
        "InjHead","InjNeck",
        "InjLShoulder","InjRShoulder","InjLUpperArm","InjRUpperArm",
        "InjLElbow","InjRElbow","InjLForearm","InjRForearm",
        "InjLWrist","InjRWrist","InjLHand","InjRHand","InjLFingers","InjRFingers",
        "InjChest","InjUpperBack","InjAbdominals","InjLowerBack","InjLOblique","InjROblique",
        "InjLHip","InjRHip","InjLHamstring","InjRHamstring","InjLQuad","InjRQuad",
        "InjLKnee","InjRKnee","InjLShinCalf","InjRShinCalf",
        "InjLAchilles","InjRAchilles","InjLAnkle","InjRAnkle",
        "InjLFoot","InjRFoot","InjLToes","InjRToes",
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

    /// <summary>
    /// Re-maps only the specified attribute keys from 2K source data for every player,
    /// leaving all other manually-edited attributes untouched.
    ///
    /// Normalization per key: piecewise linear so that
    ///   min player    →  5
    ///   median player → 50
    ///   max player    → 95
    /// computed from the matched population, then rounded and clamped.
    /// </summary>
    public async Task<int> RefreshAttrsAsync(string[] keys)
    {
        var db     = GetDb();
        var lookup = BuildMatchLookup();

        // Build (record, PlayerData) pairs for all matched players.
        var matched = db.Players
            .Select(r => (record: r, data: lookup.GetValueOrDefault(EspnTeamFactory.NormalizeName(r.Name))))
            .Where(x => x.data is not null)
            .ToList();

        foreach (var key in keys.Where(k => !_nonMappedKeys.Contains(k)))
        {
            // Collect raw source values for this key across all matched players.
            var rawValues = matched
                .Select(x => x.data!.Source.TryGetValue(key, out double v) ? v : 50.0)
                .ToList();

            var sorted = rawValues.Order().ToList();
            double median = sorted.Count % 2 == 1
                ? sorted[sorted.Count / 2]
                : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;
            double min  = sorted[0];
            double max  = sorted[^1];

            for (int i = 0; i < matched.Count; i++)
            {
                double raw = rawValues[i];
                double mapped = raw <= median
                    ? (median > min  ? 5.0  + (raw - min)    / (median - min)  * 45.0 : 50.0)
                    : (max  > median ? 50.0 + (raw - median) / (max - median)  * 45.0 : 50.0);

                matched[i].record.Attrs[key] = Math.Clamp((int)Math.Round(mapped), 5, 95);
            }
        }

        await SaveAsync();
        return matched.Count;
    }

    /// <summary>
    /// Detects unmatched players via Height == 50 (Height is pulled directly from 2K;
    /// any properly-matched player will have a real height value, not exactly 50).
    ///   - If the player now matches a 2K entry (e.g. apostrophe name fix): remap all attributes.
    ///   - If still unmatched (G-League callup / not in 2K): set all attributes to 10.
    /// Also migrates legacy "BasketballIQ" key → "oBBIQ" for all players.
    /// Returns (remapped, setToTen).
    /// </summary>
    public async Task<(int remapped, int setToTen)> FixDefaultPlayersAsync()
    {
        var db     = GetDb();
        var lookup = BuildMatchLookup();

        // Migrate legacy BasketballIQ key → oBBIQ for all players that still have it
        foreach (var record in db.Players)
        {
            if (record.Attrs.TryGetValue("BasketballIQ", out int biq) && !record.Attrs.ContainsKey("oBBIQ"))
            {
                record.Attrs["oBBIQ"] = biq;
                record.Attrs.Remove("BasketballIQ");
            }
            else if (record.Attrs.ContainsKey("BasketballIQ"))
            {
                record.Attrs.Remove("BasketballIQ"); // oBBIQ already set by Re-map button
            }
            if (!record.Attrs.ContainsKey("dBBIQ"))  record.Attrs["dBBIQ"]  = 50;
            if (!record.Attrs.ContainsKey("Hustle"))  record.Attrs["Hustle"] = 50;
        }

        // Build per-key normalization parameters from all matched players
        var allMatched = db.Players
            .Select(r => (record: r, data: lookup.GetValueOrDefault(EspnTeamFactory.NormalizeName(r.Name))))
            .Where(x => x.data is not null)
            .ToList();

        var normParams = new Dictionary<string, (double min, double median, double max)>();
        foreach (var key in AttrKeys.Where(k => k != "Height" && !_nonMappedKeys.Contains(k)))
        {
            var rawValues = allMatched
                .Select(x => x.data!.Source.TryGetValue(key, out double v) ? v : 50.0)
                .Order().ToList();
            if (rawValues.Count == 0) continue;
            double median = rawValues.Count % 2 == 1
                ? rawValues[rawValues.Count / 2]
                : (rawValues[rawValues.Count / 2 - 1] + rawValues[rawValues.Count / 2]) / 2.0;
            normParams[key] = (rawValues[0], median, rawValues[^1]);
        }

        int remapped = 0, setToTen = 0;

        foreach (var record in db.Players)
        {
            // Height == 50 is the reliable signal for "never matched 2K data"
            if (!record.Attrs.TryGetValue("Height", out int h) || h != 50) continue;

            var key2k = EspnTeamFactory.NormalizeName(record.Name);
            if (lookup.TryGetValue(key2k, out var d))
            {
                // Now matches (e.g. apostrophe fix) — remap all attributes from 2K
                record.Attrs["Height"] = d.Source.TryGetValue("Height", out double hv)
                    ? Math.Clamp((int)Math.Round(hv), 5, 100) : 50;

                foreach (var key in _nonMappedKeys) record.Attrs.TryAdd(key, 50);
                foreach (var key in AttrKeys.Where(k => k != "Height" && !_nonMappedKeys.Contains(k)))
                {
                    if (!normParams.TryGetValue(key, out var np)) continue;
                    double raw    = d.Source.TryGetValue(key, out double rv) ? rv : 50.0;
                    double mapped = raw <= np.median
                        ? (np.median > np.min  ? 5.0  + (raw - np.min)    / (np.median - np.min)  * 45.0 : 50.0)
                        : (np.max  > np.median ? 50.0 + (raw - np.median) / (np.max - np.median)  * 45.0 : 50.0);
                    record.Attrs[key] = Math.Clamp((int)Math.Round(mapped), 5, 95);
                }
                foreach (var (k, v) in d.Tendencies) record.Tends[k] = v;
                foreach (var tk in TendKeys)
                    if (!record.Tends.ContainsKey(tk)) record.Tends[tk] = 50;
                remapped++;
            }
            else
            {
                // Genuinely not in 2K — set to 10 for game attrs, keep defaults for injury ratings
                foreach (var key in AttrKeys.Where(k => !_nonMappedKeys.Contains(k)))
                    record.Attrs[key] = 10;
                foreach (var key in _nonMappedKeys)
                    record.Attrs.TryAdd(key, key == "DomHand" ? 0 : 99);
                foreach (var tk in TendKeys)  record.Tends[tk]  = 25;
                setToTen++;
            }
        }

        await SaveAsync();
        return (remapped, setToTen);
    }

    /// <summary>
    /// Non-destructively syncs players.json with the latest nba_roster.json:
    ///   - Updates Team/TeamAbbr for existing players
    ///   - Adds new players (with 2K attrs if matched, else 50s)
    ///   - Marks departed players as free agents (Team = "")
    /// Returns (updated, added, freed) counts.
    /// </summary>
    public async Task<(int updated, int added, int freed)> SyncRostersAsync()
    {
        if (!File.Exists(RosterPath)) return (0, 0, 0);

        var db = GetDb();
        var lookup2k = BuildMatchLookup();

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(RosterPath));

        // Build current ESPN roster: norm → (originalName, teamName, abbr, espnPos, heightRating)
        var currentByNorm = new Dictionary<string, (string name, string team, string abbr, string espnPos, int height)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var t in doc.RootElement.GetProperty("teams").EnumerateArray())
        {
            var teamName = t.GetProperty("name").GetString()!;
            var abbr     = t.GetProperty("abbreviation").GetString()!;
            foreach (var p in t.GetProperty("players").EnumerateArray())
            {
                var pName   = p.GetProperty("name").GetString()!;
                var espnPos = p.TryGetProperty("espnPosition", out var ep) ? ep.GetString() ?? "G" : "G";
                var height  = p.TryGetProperty("heightRating", out var hr) ? hr.GetInt32() : 50;
                currentByNorm[EspnTeamFactory.NormalizeName(pName)] = (pName, teamName, abbr, espnPos, height);
            }
        }

        // Track existing players by normalized name
        var existingByNorm = db.Players
            .GroupBy(r => EspnTeamFactory.NormalizeName(r.Name))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        int updated = 0, freed = 0;

        // Pass 1: update team assignments for existing players
        foreach (var record in db.Players)
        {
            var norm = EspnTeamFactory.NormalizeName(record.Name);
            if (currentByNorm.TryGetValue(norm, out var cur))
            {
                if (record.Team != cur.team || record.TeamAbbr != cur.abbr)
                {
                    record.Team    = cur.team;
                    record.TeamAbbr = cur.abbr;
                    updated++;
                }
            }
            else if (!string.IsNullOrEmpty(record.Team))
            {
                record.Team    = "";
                record.TeamAbbr = "";
                freed++;
            }
        }

        // Pass 2: add new players not yet in players.json
        int added = 0;
        foreach (var (norm, (pName, teamName, abbr, espnPos, heightRating)) in currentByNorm)
        {
            if (existingByNorm.ContainsKey(norm)) continue;

            lookup2k.TryGetValue(norm, out var d);

            var positions = d?.Positions.ToList()
                ?? espnPos switch
                {
                    "C" => new List<string> { "C" },
                    "F" => new List<string> { "SF", "PF" },
                    _   => new List<string> { "PG", "SG" },
                };

            var record = new PlayerRecord
            {
                Name     = pName,
                Team     = teamName,
                TeamAbbr = abbr,
                Positions = positions,
                Height   = d?.HeightStr ?? "6'7\"",
                Overall  = d?.Overall ?? 75,
            };

            if (d != null)
            {
                foreach (var key in AttrKeys)
                {
                    if (key == "Height")
                        record.Attrs[key] = d.Source.TryGetValue("Height", out double hv)
                            ? Math.Clamp((int)Math.Round(hv), 5, 100) : 50;
                    else if (_nonMappedKeys.Contains(key))
                        record.Attrs.TryAdd(key, key == "DomHand" ? 0 : 99);
                    else
                        record.Attrs[key] = d.Mapped.TryGetValue(key, out double v)
                            ? Math.Clamp((int)Math.Round(v), 5, 95) : 50;
                }
                foreach (var (k, v) in d.Tendencies) record.Tends[k] = v;
                foreach (var tk in TendKeys) record.Tends.TryAdd(tk, 50);
            }
            else
            {
                foreach (var key in AttrKeys)
                    record.Attrs[key] = _nonMappedKeys.Contains(key)
                        ? (key == "DomHand" ? 0 : 99)
                        : (key == "Height" ? heightRating : 50);
                foreach (var tk in TendKeys) record.Tends[tk] = 50;
            }

            // Ensure all body-part injury keys present (defaults to 99)
            foreach (var key in InjuryTables.BodyParts.Keys)
                record.Attrs.TryAdd(key, 99);

            db.Players.Add(record);
            added++;
        }

        await SaveAsync();
        return (updated, added, freed);
    }

    // ── Name matching helpers ─────────────────────────────────────────────────

    private static readonly Regex _suffixRe =
        new(@"\b(jr|sr|ii|iii|iv|v)\b\.?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string StripSuffix(string normalizedName)
    {
        var s = _suffixRe.Replace(normalizedName, "").Trim();
        return Regex.Replace(s, @"\s+", " ");
    }

    // Builds a 2K player lookup that resolves both exact AND suffix-stripped names.
    // Exact keys win (TryAdd): "jimmy butler iii" → ButlerIII data, and also adds
    // "jimmy butler" → ButlerIII data only if no other entry already claimed that key.
    private Dictionary<string, PlayerData> BuildMatchLookup()
    {
        var dict = new Dictionary<string, PlayerData>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in nba2k.GetPlayers())
        {
            var norm = EspnTeamFactory.NormalizeName(p.Name);
            dict.TryAdd(norm, p);
            var stripped = StripSuffix(norm);
            if (stripped != norm) dict.TryAdd(stripped, p);
        }
        return dict;
    }

    public async Task SaveAsync()
    {
        if (_db is null) return;
        _db.SavedAt = DateTime.Now;
        await File.WriteAllTextAsync(DataPath, JsonSerializer.Serialize(_db, _opts));
    }

    private PlayerDb Load()
    {
        PlayerDb db;
        if (File.Exists(DataPath))
            db = JsonSerializer.Deserialize<PlayerDb>(File.ReadAllText(DataPath), _opts) ?? new();
        else if (!File.Exists(RosterPath))
            return new();
        else
            db = GenerateFromSources();

        // Migrate: ensure all players have the new injury body-part ratings (default 70)
        // and remove the legacy 3-bucket injury attributes.
        foreach (var p in db.Players)
        {
            p.Attrs.Remove("InjLower");
            p.Attrs.Remove("InjUpper");
            p.Attrs.Remove("InjCore");
            p.Attrs.TryAdd("DomHand", 0);
            foreach (var key in InjuryTables.BodyParts.Keys)
                p.Attrs.TryAdd(key, 99);
        }

        return db;
    }

    private PlayerDb GenerateFromSources()
    {
        var lookup = BuildMatchLookup();

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
                    Positions = d?.Positions.ToList() ?? Str(p, "espnPosition") switch
                    {
                        "C" => new List<string> { "C" },
                        "F" => new List<string> { "SF", "PF" },
                        _   => new List<string> { "PG", "SG" },
                    },
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
