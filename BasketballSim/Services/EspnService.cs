using System.Text.Json;
using BasketballSim.Models;

namespace BasketballSim.Services;

public record EspnTeam(string Id, string Abbreviation, string Name, string Location, string PrimaryColor, string SecondaryColor);
public record EspnPlayer(string Name, int JerseyNumber, Position Position, int HeightRating, int Age);

public class EspnService(HttpClient http)
{
    private const string BaseUrl = "https://site.api.espn.com/apis/site/v2/sports/basketball/nba";

    private List<EspnTeam>? _teamsCache;

    public async Task<List<EspnTeam>> GetTeamsAsync()
    {
        if (_teamsCache != null) return _teamsCache;

        try
        {
            var json = await http.GetStringAsync($"{BaseUrl}/teams?limit=40");
            using var doc = JsonDocument.Parse(json);
            var teamsArr = doc.RootElement
                .GetProperty("sports")[0]
                .GetProperty("leagues")[0]
                .GetProperty("teams");

            var teams = new List<EspnTeam>();
            foreach (var item in teamsArr.EnumerateArray())
            {
                var t = item.GetProperty("team");
                teams.Add(new EspnTeam(
                    Id:             t.GetProperty("id").GetString()!,
                    Abbreviation:   t.GetProperty("abbreviation").GetString()!,
                    Name:           t.GetProperty("displayName").GetString()!,
                    Location:       t.GetProperty("location").GetString()!,
                    PrimaryColor:   "#" + t.GetProperty("color").GetString()!,
                    SecondaryColor: "#" + t.GetProperty("alternateColor").GetString()!
                ));
            }

            _teamsCache = [.. teams.OrderBy(t => t.Name)];
        }
        catch
        {
            _teamsCache = [];
        }

        return _teamsCache;
    }

    public async Task<List<EspnPlayer>> GetRosterAsync(string teamId)
    {
        try
        {
            var json = await http.GetStringAsync($"{BaseUrl}/teams/{teamId}/roster");
            using var doc = JsonDocument.Parse(json);
            var athletes = doc.RootElement.GetProperty("athletes");

            var guards   = new List<EspnPlayer>();
            var forwards = new List<EspnPlayer>();
            var centers  = new List<EspnPlayer>();

            foreach (var a in athletes.EnumerateArray())
            {
                var name    = a.GetProperty("displayName").GetString() ?? "Unknown";
                var jersey  = int.TryParse(a.TryGetString("jersey"), out var j) ? j : 0;
                var posAbbr = a.GetProperty("position").GetProperty("abbreviation").GetString() ?? "G";
                var heightIn = a.TryGetDouble("height") ?? 79.0;
                var age     = a.TryGetInt("age") ?? 25;
                int heightRating = Math.Clamp((int)((heightIn - 69) * 5 + 5), 5, 95);

                // placeholder position — will be assigned by slot below
                var player = new EspnPlayer(name, jersey, Position.PG, heightRating, age);

                if (posAbbr == "C")
                    centers.Add(player);
                else if (posAbbr == "F")
                    forwards.Add(player);
                else
                    guards.Add(player);
            }

            // Assign: 1st G → PG, 2nd G → SG, 1st F → SF, 2nd F → PF, 1st C → C
            var result = new List<EspnPlayer>();
            if (guards.Count   > 0) result.Add(guards[0]   with { Position = Position.PG });
            if (guards.Count   > 1) result.Add(guards[1]   with { Position = Position.SG });
            if (forwards.Count > 0) result.Add(forwards[0] with { Position = Position.SF });
            if (forwards.Count > 1) result.Add(forwards[1] with { Position = Position.PF });
            if (centers.Count  > 0) result.Add(centers[0]  with { Position = Position.C  });

            // If we don't have 5 yet, fill from remaining in order
            if (result.Count < 5)
            {
                var used = result.Select(p => p.Name).ToHashSet();
                var remaining = guards.Skip(2).Concat(forwards.Skip(2)).Concat(centers.Skip(1))
                    .Where(p => !used.Contains(p.Name));
                var posSlots = new[] { Position.PG, Position.SG, Position.SF, Position.PF, Position.C };
                foreach (var p in remaining)
                {
                    if (result.Count >= 5) break;
                    result.Add(p with { Position = posSlots[result.Count] });
                }
            }

            return result;
        }
        catch
        {
            return [];
        }
    }
}

// JsonElement extension helpers
file static class JsonElementExtensions
{
    public static string? TryGetString(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static double? TryGetDouble(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    public static int? TryGetInt(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
}
