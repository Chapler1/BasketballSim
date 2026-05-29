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
    internal int Attr_oBBIQ            { get; init; }  // Offensive Basketball IQ
    internal int Attr_dBBIQ            { get; init; }  // Defensive Basketball IQ
    internal int Attr_Hustle           { get; init; }
    internal int Attr_Dribbling        { get; init; }
    internal int Attr_Passing          { get; init; }
    internal int Attr_Rebounding_Off   { get; init; }
    internal int Attr_Rebounding_Def   { get; init; }

    // ── Defensive Attributes (HIDDEN) ─────────────────────────────
    internal int Attr_PerimeterDefense { get; init; }
    internal int Attr_InteriorDefense  { get; init; }
    // 5 = very clean (Tyus Jones ≈ 1.2 foul/36), 95 = very foul-prone (Valanciunas ≈ 5.6 foul/36)
    internal int Attr_FoulTendency     { get; init; } = 50;

    // ── Tendencies ────────────────────────────────────────────────
    internal PlayerTendencies Tendencies { get; init; } = new();

    // ── Injury System ─────────────────────────────────────────────
    public DominantHand DominantHand { get; init; } = DominantHand.Right;

    // 40 body-part injury ratings (1–99; default 70). Higher = healthier/less fragile.
    // Mutable dict so SeasonScheduleService can degrade ratings after injuries.
    public Dictionary<string, int> InjuryRatings { get; init; } = new();

    // Active in-game/season injury. null = fully healthy.
    public ActiveInjury? CurrentInjury { get; set; }

    // Permanent attribute loss accumulated over career (key = attr name, value = total pts lost).
    public Dictionary<string, int> PermanentInjuryPenalties { get; init; } = new();

    // Injury debuff multipliers — read from EffectiveDebuff so dominant-hand scaling is included.
    // 1.0 = no effect. Grade 3 players should not be on court.
    internal double InjuryFactor_Shooting =>
        CurrentInjury is { IsPlaying: true } ? CurrentInjury.EffectiveDebuff.Shooting : 1.0;
    internal double InjuryFactor_Physical =>
        CurrentInjury is { IsPlaying: true } ? CurrentInjury.EffectiveDebuff.Physical : 1.0;
    internal double InjuryFactor_Speed =>
        CurrentInjury is { IsPlaying: true } ? CurrentInjury.EffectiveDebuff.Speed    : 1.0;
    internal double InjuryFactor_Jump =>
        CurrentInjury is { IsPlaying: true } ? CurrentInjury.EffectiveDebuff.Jump     : 1.0;

    // ── Permanent Penalty Helpers ─────────────────────────────────
    // Subtract accumulated career injury loss from raw attribute; floor at 5.
    private int EffAttr(string key, int raw) =>
        Math.Max(5, raw - PermanentInjuryPenalties.GetValueOrDefault(key, 0));

    private int EffSpeed      => EffAttr("Speed",      Speed);
    private int EffJumping    => EffAttr("Jumping",    Jumping);
    private int EffStrength   => EffAttr("Strength",   Strength);
    private int EffEndurance  => EffAttr("Endurance",  Endurance);
    private int EffInside     => EffAttr("Inside",     Attr_Inside);
    private int EffMidRange   => EffAttr("MidRange",   Attr_MidRange);
    private int EffThreePoint => EffAttr("ThreePoint", Attr_ThreePoint);
    private int EffDribbling  => EffAttr("Dribbling",  Attr_Dribbling);
    private int EffPassing    => EffAttr("Passing",    Attr_Passing);
    private int EffIntDef     => EffAttr("IntDef",     Attr_InteriorDefense);
    private int EffPerimDef   => EffAttr("PerimDef",   Attr_PerimeterDefense);

    // ── Derived Tendency Properties ───────────────────────────────
    internal double DriveGravity =>
        (EffSpeed + Attr_Dribbling + Attr_Inside) / 300.0 * InjuryFactor_Speed;

    internal double PerimeterGravity =>
        Attr_ThreePoint / 100.0;

    internal double ShotClockAggressiveness =>
        (EffSpeed + Attr_oBBIQ) / 200.0 * InjuryFactor_Speed;

    internal double CutTendency =>
        (EffSpeed + EffJumping + Attr_oBBIQ) / 300.0 * InjuryFactor_Speed * InjuryFactor_Jump;

    // Compressed hustle weight for loose ball scrambles.
    // At 95: 1.135×, at 50: 1.0×, at 5: 0.865×
    internal double LooseBallWeight =>
        1.0 + (Attr_Hustle - 50) / 50.0 * 0.15;

    internal double AlleyOopTendency =>
        (EffJumping + Attr_Dunks + EffSpeed) / 300.0 * InjuryFactor_Jump * InjuryFactor_Speed;

    // Gates how often inside shots become dunks; driven by jumping, height, dunk rating
    internal double DunkTendency =>
        (Attr_Dunks + EffJumping + Height) / 300.0 * InjuryFactor_Jump;

    // Composite screening ability — drives screener selection and screen effectiveness.
    internal double ScreenAbility =>
        (EffStrength + Attr_oBBIQ + Attr_Hustle) / 1.5 * InjuryFactor_Physical;

    // ── Fatigue / Energy ──────────────────────────────────────────
    // Energy 0–100. Starts fresh at 100, drains as player runs, recovers at breaks.
    public double Energy { get; set; } = 100.0;

    // Quadratic penalty curve: the last 30 points of energy hurt far more than the first.
    private double FatigueCurve(double maxPenalty) =>
        1.0 - Math.Pow(1.0 - Energy / 100.0, 2.0) * maxPenalty;

    internal double EnergyFactor_Physical => FatigueCurve(0.55);
    internal double EnergyFactor_Shooting => FatigueCurve(0.40);
    internal double EnergyFactor_Mental   => FatigueCurve(0.20);

    // enduranceMod: 0.82 at End=95 (drains 18% slower), 1.18 at End=5 (drains 18% faster).
    internal double EnergyDrain(bool isOffense)
    {
        double enduranceMod = 1.0 + (50.0 - EffEndurance) / 250.0;
        return (isOffense ? 0.66 : 0.54) * enduranceMod;
    }

    internal void DrainEnergy(bool isOffense) =>
        Energy = Math.Max(0.0, Energy - EnergyDrain(isOffense));

    // recovMod: 0.72 at End=5, 0.90 at End=50, 1.08 at End=95.
    internal void RecoverEnergy(double baseAmount)
    {
        double recovMod = 0.70 + EffEndurance / 250.0;
        Energy = Math.Min(100.0, Energy + baseAmount * recovMod);
    }

    // ── Make% Converters ──────────────────────────────────────────
    // All make% properties apply energy and injury factors.

    internal double InsideMakePct =>
        (0.59 + (EffInside / 100.0) * 0.22) * EnergyFactor_Shooting * InjuryFactor_Shooting;

    // Uncontested dunk — degraded by physical/jump fatigue and injury
    internal double DunkMakePct =>
        Math.Clamp(0.75 + (Attr_Dunks + EffJumping) / 200.0 * 0.20, 0.75, 0.95)
        * EnergyFactor_Physical * InjuryFactor_Physical * InjuryFactor_Jump;

    // Contact dunk — strength helps power through
    internal double ContactDunkMakePct =>
        (0.65 + (Attr_Dunks + EffJumping + EffStrength) / 300.0 * 0.20)
        * EnergyFactor_Physical * InjuryFactor_Physical * InjuryFactor_Jump;

    internal double MidRangeMakePct =>
        (0.43 + (EffMidRange / 100.0) * 0.17) * EnergyFactor_Shooting * InjuryFactor_Shooting;

    internal double ThreeMakePct =>
        (0.315 + (EffThreePoint / 100.0) * 0.20) * EnergyFactor_Shooting * InjuryFactor_Shooting;

    internal double FTMakePct
    {
        get
        {
            double x = Attr_FreeThrow / 100.0;
            // FT is mostly muscle memory — use Mental energy factor
            double base_ = Math.Clamp(0.36 + 0.88 * x - 0.24 * x * x, 0.22, 0.97);
            return base_ * EnergyFactor_Mental * InjuryFactor_Shooting;
        }
    }

    // BlockMod: weighting proxy for blocker attribution.
    internal double BlockMod =>
        (Math.Pow(EffIntDef / 100.0, 2.0) * 0.130
         + Math.Max(0.0, (Height + EffJumping - 100) / 2800.0))
        * EnergyFactor_Physical * InjuryFactor_Physical
        * (1.0 + (Attr_Hustle - 50) / 100.0 * 0.08);

    internal double StealMod =>
        Math.Pow((EffPerimDef + EffSpeed) / 200.0, 1.8) * 0.09
        * EnergyFactor_Physical * InjuryFactor_Physical
        * (1.0 + (Attr_Hustle - 50) / 100.0 * 0.08);

    // Tired/injured players lose handle and decision-making → more turnovers.
    internal double TurnoverRate =>
        (0.045 - ((Attr_oBBIQ + EffDribbling) / 200.0) * 0.025) / EnergyFactor_Mental;

    internal double ORebWeight =>
        Math.Pow((Attr_Rebounding_Off + EffJumping) / 200.0, 1.3)
        * EnergyFactor_Physical * InjuryFactor_Physical * InjuryFactor_Jump;

    internal double DRebWeight =>
        Math.Pow((Attr_Rebounding_Def + Height) / 200.0, 1.3)
        * EnergyFactor_Physical * InjuryFactor_Physical;

    internal double AssistWeight =>
        Math.Pow((EffPassing * 1.5 + Attr_oBBIQ * 0.5) / 200.0, 6.0);

    internal double ContestPenalty(ShotType shot) => shot switch
    {
        // Centered at IntDef/PerimDef=50 so the league-average penalty is unchanged
        // while elite defenders contest far harder and poor defenders barely contest at all.
        ShotType.Inside =>
            (Math.Max(0.0, 0.25 + (EffIntDef - 50) / 100.0 * 0.85)
             + (Height - 50) / 900.0)
            * EnergyFactor_Physical * InjuryFactor_Physical,
        ShotType.MidRange =>
            (Math.Max(0.0, 0.046 + (EffIntDef * 0.4 + EffPerimDef * 0.6 - 50) / 100.0 * 0.13)
             + (Height - 50) / 1000.0)
            * EnergyFactor_Physical * InjuryFactor_Physical,
        ShotType.ThreePointer =>
            Math.Max(0.0, 0.09 + (EffPerimDef - 50) / 100.0 * 0.34)
            * EnergyFactor_Physical * InjuryFactor_Physical,
        _ => 0.04
    };
}
