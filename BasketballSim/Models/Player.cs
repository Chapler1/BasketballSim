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

    // ── Derived Tendency Properties ───────────────────────────────
    internal double DriveGravity =>
        (Speed + Attr_Dribbling + Attr_Inside) / 300.0;

    internal double PerimeterGravity =>
        Attr_ThreePoint / 100.0;

    internal double ShotClockAggressiveness =>
        (Speed + Attr_oBBIQ) / 200.0;

    internal double CutTendency =>
        (Speed + Jumping + Attr_oBBIQ) / 300.0;

    // Compressed hustle weight for loose ball scrambles.
    // At 95: 1.135×, at 50: 1.0×, at 5: 0.865× — best player has ~1.31× edge over worst.
    internal double LooseBallWeight =>
        1.0 + (Attr_Hustle - 50) / 50.0 * 0.15;

    internal double AlleyOopTendency =>
        (Jumping + Attr_Dunks + Speed) / 300.0;

    // Gates how often inside shots become dunks; driven by jumping, height, dunk rating
    internal double DunkTendency =>
        (Attr_Dunks + Jumping + Height) / 300.0;

    // Composite screening ability — drives both screener selection and screen effectiveness.
    // At avg (50,50,50): 100. At elite (95,95,95): 190. At floor (5,5,5): 10.
    internal double ScreenAbility =>
        (Strength + Attr_oBBIQ + Attr_Hustle) / 1.5;

    // ── Fatigue / Energy ──────────────────────────────────────────
    // Energy 0–100. Starts fresh at 100, drains as player runs, recovers at breaks.
    public double Energy { get; set; } = 100.0;

    // Quadratic penalty curve: the last 30 points of energy hurt far more than the first.
    // maxPenalty = maximum attribute reduction when fully exhausted (Energy=0).
    private double FatigueCurve(double maxPenalty) =>
        1.0 - Math.Pow(1.0 - Energy / 100.0, 2.0) * maxPenalty;

    // Physical attributes (Speed, Jumping, Dunks, OReb, DReb, Block, Steal, Contest)
    // At Energy=0: 55% penalty. Tired legs don't lie.
    internal double EnergyFactor_Physical => FatigueCurve(0.55);
    // Shooting mechanics (Inside, Mid, 3PT, FT) — form breaks down under fatigue
    // At Energy=0: 40% penalty.
    internal double EnergyFactor_Shooting => FatigueCurve(0.40);
    // Mental/skill (IQ, Handle) — most resilient but TOs climb when exhausted
    // At Energy=0: 20% penalty.
    internal double EnergyFactor_Mental   => FatigueCurve(0.20);

    // How much energy this player burns each possession they are on the floor.
    // High endurance reduces drain; low endurance accelerates it.
    // enduranceMod: 0.82 at End=95 (drains 18% slower), 1.18 at End=5 (drains 18% faster).
    // Calibrated so an average player (End=50) loses ~30 energy per 12-minute quarter,
    // ending a full game around 24 energy (~32% physical penalty) after breaks.
    internal double EnergyDrain(bool isOffense)
    {
        double enduranceMod = 1.0 + (50.0 - Endurance) / 250.0;
        return (isOffense ? 0.66 : 0.54) * enduranceMod;
    }

    // Apply drain and clamp to [0, 100].
    internal void DrainEnergy(bool isOffense) =>
        Energy = Math.Max(0.0, Energy - EnergyDrain(isOffense));

    // Recover energy at a break. High endurance players recover faster.
    // recovMod: 0.72 at End=5, 0.90 at End=50, 1.08 at End=95.
    internal void RecoverEnergy(double baseAmount)
    {
        double recovMod = 0.70 + Endurance / 250.0;
        Energy = Math.Min(100.0, Energy + baseAmount * recovMod);
    }

    // ── Make% Converters ──────────────────────────────────────────
    // All make% properties apply the appropriate energy factor so fatigue
    // naturally degrades performance as the game progresses.

    internal double InsideMakePct =>
        (0.46 + (Attr_Inside / 100.0) * 0.26) * EnergyFactor_Shooting;

    // Uncontested dunk — very high base, degraded by physical fatigue (legs give out)
    internal double DunkMakePct =>
        Math.Clamp(0.75 + (Attr_Dunks + Jumping) / 200.0 * 0.20, 0.75, 0.95) * EnergyFactor_Physical;

    // Contact dunk — strength helps power through; also physically demanding
    internal double ContactDunkMakePct =>
        (0.65 + (Attr_Dunks + Jumping + Strength) / 300.0 * 0.20) * EnergyFactor_Physical;

    internal double MidRangeMakePct =>
        (0.35 + (Attr_MidRange / 100.0) * 0.20) * EnergyFactor_Shooting;

    internal double ThreeMakePct =>
        (0.285 + (Attr_ThreePoint / 100.0) * 0.20) * EnergyFactor_Shooting;

    internal double FTMakePct
    {
        get
        {
            double x = Attr_FreeThrow / 100.0;
            // FT is mostly muscle memory — use Mental energy factor (less drain)
            double base_ = Math.Clamp(0.36 + 0.88 * x - 0.24 * x * x, 0.22, 0.97);
            return base_ * EnergyFactor_Mental;
        }
    }

    // Defense: tired defenders contest worse and steal less effectively
    // Power-law curves widen the spread between stars and scrubs.
    // Physical bonus: Height + Jumping both contribute to blocking ability.
    // BlockMod is used as a weighting proxy for blocker attribution.
    // Actual block CHANCE is computed per-shot-type in GameEngine.
    internal double BlockMod =>
        (Math.Pow(Attr_InteriorDefense / 100.0, 2.0) * 0.130
         + Math.Max(0.0, (Height + Jumping - 100) / 2800.0))
        * EnergyFactor_Physical;

    // Exponent raised 1.3→1.8: widens elite/poor spread (~4.2× vs ~2.8×).
    // Scalar trimmed 0.12→0.09: offsets the added deflection-steal volume.
    internal double StealMod =>
        Math.Pow((Attr_PerimeterDefense + Speed) / 200.0, 1.8) * 0.09 * EnergyFactor_Physical;

    // Tired players lose handle and decision-making → more turnovers.
    // EnergyFactor_Mental < 1 → dividing raises the rate.
    // Calibrated to NBA target ~13.9 TOV/game using real rosters.
    internal double TurnoverRate =>
        (0.060 - ((Attr_oBBIQ + Attr_Dribbling) / 200.0) * 0.030) / EnergyFactor_Mental;

    // Rebounding: power-law spreads elite rebounders further from average.
    internal double ORebWeight =>
        Math.Pow((Attr_Rebounding_Off + Jumping) / 200.0, 1.3) * EnergyFactor_Physical;

    internal double DRebWeight =>
        Math.Pow((Attr_Rebounding_Def + Height) / 200.0, 1.3) * EnergyFactor_Physical;

    // Elite passers/playmakers attract the ball much more than average.
    internal double AssistWeight =>
        Math.Pow((Attr_Passing + Attr_oBBIQ) / 200.0, 3.0);

    // Tired defenders can't close out or contest as well.
    // Physical attributes (Height) add a bonus to inside/mid contest.
    internal double ContestPenalty(ShotType shot) => shot switch
    {
        ShotType.Inside =>
            ((Attr_InteriorDefense / 100.0) * 0.42
             + (Height - 50) / 900.0)
            * EnergyFactor_Physical,
        ShotType.MidRange =>
            ((Attr_InteriorDefense * 0.4 + Attr_PerimeterDefense * 0.6) / 100.0 * 0.092
             + (Height - 50) / 1000.0)
            * EnergyFactor_Physical,
        ShotType.ThreePointer =>
            (Attr_PerimeterDefense / 100.0) * 0.155 * EnergyFactor_Physical,
        _ => 0.04
    };
}
