namespace BasketballSim.Models;

public class PlayerRecord
{
    public string       Name      { get; set; } = "";
    public string       Team      { get; set; } = "";
    public string       TeamAbbr  { get; set; } = "";
    public List<string> Positions { get; set; } = ["PG"];
    public string       Height    { get; set; } = "6'7\"";
    public int          Overall   { get; set; } = 75;
    public Dictionary<string, int> Attrs { get; set; } = new();
    public Dictionary<string, int> Tends { get; set; } = new();

    public string PrimaryPosition => Positions.Count > 0 ? Positions[0] : "PG";
}

public class PlayerDb
{
    public DateTime           SavedAt { get; set; } = DateTime.Now;
    public List<TeamMeta>     Teams   { get; set; } = [];
    public List<PlayerRecord> Players { get; set; } = [];
}

public class TeamMeta
{
    public string Name           { get; set; } = "";
    public string Abbr           { get; set; } = "";
    public string PrimaryColor   { get; set; } = "#888888";
    public string SecondaryColor { get; set; } = "#888888";
    public string Division       { get; set; } = "";
    public string Conference     { get; set; } = "";
}
