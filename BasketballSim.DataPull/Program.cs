using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

// ── Config / API key ─────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .AddUserSecrets("basketballsim-datapull")
    .AddEnvironmentVariables()
    .Build();

var apiKey = config["NBA2K_API_KEY"] ?? config["Nba2kApi:Key"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("No API key found.");
    Console.Error.WriteLine("Run:  dotnet user-secrets --project BasketballSim.DataPull set \"Nba2kApi:Key\" \"YOUR_KEY\"");
    return 1;
}

const string BaseUrl = "https://api.nba2kapi.com/api";
using var http = new HttpClient();
http.DefaultRequestHeaders.Add("X-API-Key", apiKey);

var outputPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BasketballSim", "Data", "nba2k_cache.json"));

// ── Fetch teams ───────────────────────────────────────────────────────────────
Console.WriteLine("Fetching teams...");
var teamsJson   = await http.GetStringAsync($"{BaseUrl}/teams?teamType=curr");
var teamsResult = JsonNode.Parse(teamsJson)!;
var teamsArray  = teamsResult["data"]!.AsArray();
Console.WriteLine($"  Got {teamsArray.Count} teams.");

// ── Fetch players (cursor-based pagination) ───────────────────────────────────
Console.WriteLine("Fetching players...");
var playersArray = new JsonArray();
const int PageSize = 100;
string? cursor = null;
int pageNum = 1;

while (true)
{
    var url = cursor == null
        ? $"{BaseUrl}/players?teamType=curr&limit={PageSize}"
        : $"{BaseUrl}/players?teamType=curr&limit={PageSize}&cursor={Uri.EscapeDataString(cursor)}";

    var resp = await http.GetAsync(url);
    if (!resp.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"  HTTP {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        return 1;
    }

    var pageJson   = await resp.Content.ReadAsStringAsync();
    var pageResult = JsonNode.Parse(pageJson)!;
    var pageData   = pageResult["data"]!.AsArray();

    foreach (var p in pageData)
        if (p != null) playersArray.Add(JsonNode.Parse(p.ToJsonString()));

    Console.WriteLine($"  Page {pageNum}: {pageData.Count} players (total so far: {playersArray.Count})");

    cursor = pageResult["meta"]?["pagination"]?["nextCursor"]?.GetValue<string>();
    var hasMore = pageResult["meta"]?["pagination"]?["hasMore"]?.GetValue<bool>() ?? false;
    if (!hasMore || string.IsNullOrEmpty(cursor)) break;
    pageNum++;
}

Console.WriteLine($"  Done. {playersArray.Count} total players.");

// ── Attach players to teams ───────────────────────────────────────────────────
var teamPlayers = new Dictionary<string, List<JsonNode>>(StringComparer.OrdinalIgnoreCase);
foreach (var player in playersArray)
{
    if (player == null) continue;
    var teamName =
        player["team"]?.GetValue<string>() ??
        player["teamName"]?.GetValue<string>() ??
        player["currentTeam"]?.GetValue<string>() ??
        "Unknown";

    if (!teamPlayers.ContainsKey(teamName))
        teamPlayers[teamName] = [];
    teamPlayers[teamName].Add(player);
}

// ── Build merged output ───────────────────────────────────────────────────────
var merged = new JsonArray();
foreach (var team in teamsArray)
{
    if (team == null) continue;
    var teamName = team["teamName"]?.GetValue<string>() ?? "";
    var teamCopy = JsonNode.Parse(team.ToJsonString())!.AsObject();

    var players = new JsonArray();
    if (teamPlayers.TryGetValue(teamName, out var plist))
        foreach (var p in plist.OrderBy(p => p?["name"]?.GetValue<string>() ?? ""))
            players.Add(JsonNode.Parse(p!.ToJsonString()));

    teamCopy["players"] = players;
    merged.Add(teamCopy);
}

// ── Write output ──────────────────────────────────────────────────────────────
var output = new JsonObject
{
    ["pulledAt"]    = DateTime.UtcNow.ToString("O"),
    ["teamCount"]   = teamsArray.Count,
    ["playerCount"] = playersArray.Count,
    ["teams"]       = merged,
    ["players"]     = JsonNode.Parse(playersArray.ToJsonString()), // flat list for easy searching
};

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
await File.WriteAllTextAsync(outputPath, output.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine();
Console.WriteLine($"Written to: {outputPath}");
Console.WriteLine($"  {teamsArray.Count} teams, {playersArray.Count} players");

// ── Download team logos ───────────────────────────────────────────────────────
var logosDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BasketballSim", "wwwroot", "logos", "teams"));
Directory.CreateDirectory(logosDir);

Console.WriteLine();
Console.WriteLine("Downloading team logos...");
using var logoHttp = new HttpClient();
logoHttp.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

int logoOk = 0, logoSkip = 0, logoFail = 0;
foreach (var team in teamsArray)
{
    if (team == null) continue;
    var teamName = team["teamName"]?.GetValue<string>() ?? "unknown";
    var logoUrl  = team["logo"]?.GetValue<string>();
    if (string.IsNullOrEmpty(logoUrl)) { logoSkip++; continue; }

    // e.g. "Atlanta Hawks" → "atlanta-hawks.svg"
    var fileName = teamName.ToLowerInvariant().Replace(' ', '-') + ".svg";
    var filePath = Path.Combine(logosDir, fileName);

    if (File.Exists(filePath)) { logoSkip++; continue; }

    try
    {
        var bytes = await logoHttp.GetByteArrayAsync(logoUrl);
        await File.WriteAllBytesAsync(filePath, bytes);
        Console.WriteLine($"  ✓ {fileName}");
        logoOk++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ {fileName}: {ex.Message}");
        logoFail++;
    }
}
Console.WriteLine($"  Done: {logoOk} downloaded, {logoSkip} skipped, {logoFail} failed");
Console.WriteLine($"  Saved to: {logosDir}");

// ── Print sample field names ──────────────────────────────────────────────────
var first = playersArray.FirstOrDefault();
if (first != null)
{
    Console.WriteLine();
    Console.WriteLine("Sample player top-level fields:");
    foreach (var prop in first.AsObject())
        if (prop.Key != "attributes" && prop.Key != "badges")
            Console.WriteLine($"  {prop.Key}: {prop.Value?.ToJsonString()?.Truncate(80)}");
    Console.WriteLine("  attributes keys: " +
        string.Join(", ", first["attributes"]!.AsObject().Select(p => p.Key)));
}

return 0;

file static class Ext
{
    public static string Truncate(this string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
