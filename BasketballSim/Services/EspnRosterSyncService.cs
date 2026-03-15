namespace BasketballSim.Services;

/// <summary>
/// Runs at startup: fetches every ESPN team roster, then tells Nba2kCacheService
/// which team each player currently belongs to.  This overwrites the stale team
/// assignments baked into nba2k_cache.json.
/// </summary>
public class EspnRosterSyncService(IServiceScopeFactory scopeFactory, Nba2kCacheService cache)
    : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var espn = scope.ServiceProvider.GetRequiredService<EspnService>();

            var teams = await espn.GetTeamsAsync();

            var assignments = new List<(string playerName, string teamName)>();
            foreach (var team in teams)
            {
                if (ct.IsCancellationRequested) break;
                var roster = await espn.GetRosterAsync(team.Id);
                foreach (var p in roster)
                    assignments.Add((p.Name, team.Name));
            }

            cache.SetEspnTeams(assignments);
        }
        catch
        {
            // Network failure — 2K teams remain, app still works fine
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
