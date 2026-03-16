using System.Text.Json;
using BasketballSim.Models;

namespace BasketballSim.Services;

public class HistoricalStatsService(IWebHostEnvironment env)
{
    private string DataPath => Path.Combine(env.ContentRootPath, "Data", "historical_player_stats.json");

    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    // Keyed by player name as it appears in the JSON (original NBA API name).
    // Also built a normalized-name secondary index for fuzzy matching.
    private Dictionary<string, List<HistoricalSeasonStats>>? _byName;
    private Dictionary<string, string>? _normIndex; // normalized → original key

    public bool HasData => File.Exists(DataPath);

    private void EnsureLoaded()
    {
        if (_byName is not null) return;
        if (!File.Exists(DataPath)) { _byName = []; _normIndex = []; return; }

        _byName = JsonSerializer.Deserialize<Dictionary<string, List<HistoricalSeasonStats>>>(
            File.ReadAllText(DataPath), _opts) ?? [];

        _normIndex = _byName.Keys.ToDictionary(
            k => EspnTeamFactory.NormalizeName(k),
            k => k,
            StringComparer.Ordinal);
    }

    public List<HistoricalSeasonStats> GetPlayerHistory(string playerName)
    {
        EnsureLoaded();
        // Exact match first
        if (_byName!.TryGetValue(playerName, out var exact)) return exact;
        // Normalized fallback
        var norm = EspnTeamFactory.NormalizeName(playerName);
        if (_normIndex!.TryGetValue(norm, out var key) && _byName.TryGetValue(key, out var hist))
            return hist;
        return [];
    }
}
