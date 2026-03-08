namespace BasketballSim.Models;

public enum Position { PG, SG, SF, PF, C }

public class Player
{
    // ── Identity (always visible) ──────────────────────────────────
    public required string   Name          { get; init; }
    public required string   Team          { get; init; }
    public          int      JerseyNumber  { get; init; }
    public required Position Position      { get; init; }
    public          int      Age           { get; init; }

    // ── Physical Attributes (VISIBLE to GM) ───────────────────────
    public int Height     { get; init; }
    public int Strength   { get; init; }
    public int Speed      { get; init; }
    public int Jumping    { get; init; }
    public int Endurance  { get; init; }

    // ── Shooting Attributes (HIDDEN) ──────────────────────────────
    internal int Attr_Inside      { get; init; }
    internal int Attr_Dunks       { get; init; }
    internal int Attr_FreeThrow   { get; init; }
    internal int Attr_MidRange    { get; init; }
    internal int Attr_ThreePoint  { get; init; }

    // ── Skill Attributes (HIDDEN) ──────────────────────────────────
    internal int Attr_BasketballIQ     { get; init; }
    internal int Attr_Dribbling        { get; init; }
    internal int Attr_Passing          { get; init; }
    internal int Attr_Rebounding_Off   { get; init; }
    internal int Attr_Rebounding_Def   { get; init; }

    // ── Defensive Attributes (HIDDEN) ─────────────────────────────
    internal int Attr_PerimeterDefense { get; init; }
    internal int Attr_InteriorDefense  { get; init; }

    // ── Derived Tendency Properties ───────────────────────────────
    internal double USG_Weight =>
        (Attr_BasketballIQ + Attr_Dribbling + Attr_MidRange + Attr_ThreePoint) / 400.0;

    internal double DeferralTendency =>
        1.0 - (Attr_Dribbling + Attr_BasketballIQ) / 200.0;

    internal double DriveGravity =>
        (Speed + Attr_Dribbling + Attr_Inside) / 300.0;

    internal double PerimeterGravity =>
        Attr_ThreePoint / 100.0;

    internal double ShotClockAggressiveness =>
        (Speed + Attr_BasketballIQ) / 200.0;

    internal double CutTendency =>
        (Speed + Jumping + Attr_BasketballIQ) / 300.0;

    internal double AlleyOopTendency =>
        (Jumping + Attr_Dunks + Speed) / 300.0;

    // Gates how often inside shots become dunks; driven by jumping, height, dunk rating
    internal double DunkTendency =>
        (Attr_Dunks + Jumping + Height) / 300.0;

    public double Fatigue { get; set; } = 0.0;

    // ── Make% Converters ──────────────────────────────────────────
    internal double InsideMakePct =>
        0.36 + (Attr_Inside / 100.0) * 0.34;

    // Uncontested dunk (alley-oop, cut, transition) — very high make%
    internal double DunkMakePct =>
        Math.Clamp(0.75 + (Attr_Dunks + Jumping) / 200.0 * 0.20, 0.75, 0.95);

    // Contact dunk — strength helps power through defenders
    internal double ContactDunkMakePct =>
        0.65 + (Attr_Dunks + Jumping + Strength) / 300.0 * 0.20;

    internal double MidRangeMakePct =>
        0.28 + (Attr_MidRange / 100.0) * 0.26;

    internal double ThreeMakePct =>
        0.25 + (Attr_ThreePoint / 100.0) * 0.24;

    internal double FTMakePct
    {
        get
        {
            double x = Attr_FreeThrow / 100.0;
            return Math.Clamp(0.37 + 1.04 * x - 0.47 * x * x, 0.20, 0.98);
        }
    }

    internal double BlockMod =>
        (Attr_InteriorDefense / 100.0) * 0.185;

    internal double StealMod =>
        ((Attr_PerimeterDefense + Speed) / 200.0) * 0.057;

    internal double TurnoverRate =>
        0.21 - ((Attr_BasketballIQ + Attr_Dribbling) / 200.0) * 0.16;

    internal double ORebWeight =>
        (Attr_Rebounding_Off + Jumping) / 200.0;

    internal double DRebWeight =>
        (Attr_Rebounding_Def + Height) / 200.0;

    internal double AssistWeight =>
        (Attr_Passing + Attr_BasketballIQ) / 200.0;

    internal double ContestPenalty(ShotType shot) => shot switch
    {
        ShotType.Inside =>
            (Attr_InteriorDefense / 100.0) * 0.09,
        ShotType.MidRange =>
            (Attr_InteriorDefense * 0.4 + Attr_PerimeterDefense * 0.6) / 100.0 * 0.07,
        ShotType.ThreePointer =>
            (Attr_PerimeterDefense / 100.0) * 0.06,
        _ => 0.04
    };
}
