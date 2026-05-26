using System.Text.Json;
using BasketballSim.Models;

namespace BasketballSim.Services;

/// <summary>
/// Persists all 30 NBA team rosters to Data/nba_roster.json so ESPN is only
/// called once (via FetchAndSaveAsync). Subsequent loads read the file and
/// build Team objects using the 2K attribute cache.
/// </summary>
public class NbaRosterService(IWebHostEnvironment env, EspnService espnSvc, Nba2kCacheService nba2kSvc, PlayerDataService playerSvc)
{
    private string RosterPath => Path.Combine(env.ContentRootPath, "Data", "nba_roster.json");

    // HasData: true when players.json exists (the source of truth for sim attributes)
    public bool HasData => playerSvc.HasData || File.Exists(RosterPath);

    // ── Load — always reads from players.json (via PlayerDataService) ─────────
    public Dictionary<string, Team> LoadTeams() => LoadFromPlayerDb();

    private Dictionary<string, Team> LoadFromPlayerDb()
    {
        var db    = playerSvc.GetDb();
        var teams = new Dictionary<string, Team>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in db.Teams)
        {
            var teamPlayers = db.Players
                .Where(p => p.Team.Equals(meta.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var configs = EspnTeamFactory.BuildRoster(teamPlayers, maxBench: 15);
            var team    = EspnTeamFactory.BuildTeam(
                configs, meta.Name, meta.Abbr, meta.PrimaryColor,
                coach: CoachFactory.GetCoach(meta.Name),
                secondaryColor: meta.SecondaryColor,
                division: meta.Division,
                conference: meta.Conference);
            teams[meta.Name] = team;
        }
        return teams;
    }

    // ── Fetch + Save (call once from admin UI) ────────────────────────────────
    public async Task FetchAndSaveAsync(IProgress<(int done, int total)>? progress = null)
    {
        var espnTeams = await espnSvc.GetTeamsAsync();

        // Only keep the known 30 NBA teams
        var nbaTeams = espnTeams
            .Where(t => SeasonScheduleService.GetDivision(t.Name).Division != "")
            .ToList();

        int done = 0;

        var tasks = nbaTeams.Select(async et =>
        {
            var roster   = await espnSvc.GetRosterAsync(et.Id);
            var (div, conf) = SeasonScheduleService.GetDivision(et.Name);

            var players = roster.Select(p => new
            {
                name         = p.Name,
                espnPosition = PositionGroup(p.Position), // "G" / "F" / "C"
                heightRating = p.HeightRating,
            }).ToArray();

            Interlocked.Increment(ref done);
            progress?.Report((done, nbaTeams.Count));

            return new
            {
                name          = et.Name,
                abbreviation  = et.Abbreviation,
                primaryColor  = et.PrimaryColor,
                secondaryColor = et.SecondaryColor,
                division      = div,
                conference    = conf,
                players,
            };
        });

        var results = await Task.WhenAll(tasks);

        var payload = new
        {
            fetched_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            teams      = results.OrderBy(t => t.conference).ThenBy(t => t.division).ThenBy(t => t.name),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(RosterPath)!);
        await File.WriteAllTextAsync(RosterPath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string PositionGroup(Position p) => p switch
    {
        Position.C                  => "C",
        Position.PF or Position.SF  => "F",
        _                           => "G",
    };
}

// ── JsonElement helper ────────────────────────────────────────────────────────
file static class JsonExt
{
    public static string? GetString(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
