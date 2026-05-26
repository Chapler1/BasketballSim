namespace BasketballSim.Models;

public record ScheduledGame(
    Guid     GameId,
    DateTime Date,
    string   HomeTeamId,
    string   AwayTeamId,
    int      HomeRestDays,
    int      AwayRestDays);
