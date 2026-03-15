namespace BasketballSim.Models;

public class PlayerTendencies
{
    // ── Offensive ─────────────────────────────────────────────────────────
    // How often this player touches and initiates — composite of handling, scoring threat, athleticism.
    // High = ball-dominant creator; low = off-ball specialist.
    public int Touches   { get; set; } = 50;

    // Shot type selection pulls — each multiplies the attribute-derived weight.
    public int Drive     { get; set; } = 50;  // attacks the basket off the dribble
    public int MidRange  { get; set; } = 50;  // pull-up and post mid-range looks
    public int ThreePt   { get; set; } = 50;  // three-point attempts

    // Play-type pulls
    public int PostUp    { get; set; } = 50;  // initiates from the post
    public int Iso       { get; set; } = 50;  // clears out and goes 1-on-1
    public int PullUp    { get; set; } = 50;  // shoots off the dribble vs. catch-and-shoot
    public int Cut       { get; set; } = 50;  // cuts to the basket off-ball

    // Rebounding
    public int OffRebound { get; set; } = 50; // crashes the offensive glass

    // ── Defensive ─────────────────────────────────────────────────────────
    // High steal tendency = more attempts (more steals) but also gambles more.
    public int Steal     { get; set; } = 50;

    // High block tendency = more blocks, but weaker positional contest when they don't get it.
    public int Block     { get; set; } = 50;
}
