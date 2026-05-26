namespace BasketballSim.Models;

public enum InjuryCategory { Upper, Core, Lower }
public enum DominantHand   { Right, Left }

// Multiplicative debuff applied while a player is injured and playing through (Grade 1/2).
// 1.0 = no effect. Values less than 1.0 degrade the corresponding derived stats.
public record InjuryDebuff(
    double Shooting,  // InsideMakePct, MidRangeMakePct, ThreeMakePct, FTMakePct
    double Physical,  // StealMod, BlockMod, DRebWeight, ORebWeight, ContestPenalty, DunkMakePct
    double Speed,     // DriveGravity, CutTendency, movement tendencies
    double Jump       // AlleyOopTendency, DunkTendency, jump component of ORebWeight
)
{
    public static readonly InjuryDebuff None = new(1, 1, 1, 1);
}

public record InjuryDefinition(
    string Name,
    string BodyPartType,      // "Knee", "Ankle", "LowerBack" — body-part type key
    InjuryCategory Category,
    int Grade,                // 1 (day-to-day), 2 (significant), 3 (out)
    int ExpectedDays,         // Actual = Uniform(expected×0.6, expected×1.4)
    InjuryDebuff Debuff,      // Grade 3 always InjuryDebuff.None (player DNPs)
    double BasePermChance,    // 0.0 for Grade 1 — no permanent effects
    string[] PermanentAtRisk  // attribute keys eligible for permanent loss (Grade 2/3 only)
);

public class ActiveInjury
{
    public required InjuryDefinition Definition { get; init; }
    public required string BodyPartKey     { get; init; }  // e.g. "InjLKnee"
    public required string BodyPartDisplay { get; init; }  // e.g. "Left Knee" (UI label)
    public int  DaysRemaining { get; set; }
    public bool IsPlaying     { get; set; }  // false for Grade 3 or DNP Grade 2

    // Shooting debuff scaled by dominant-hand side; other factors same as Definition.Debuff.
    public InjuryDebuff EffectiveDebuff { get; set; } = InjuryDebuff.None;

    public double ReInjuryChancePerPoss => Definition.Grade switch
    {
        1 => 0.0013,   // ~8%/game (65 poss); was 0.0015 (9.4%)
        2 => 0.0032,   // ~19%/game;          was 0.008 (40%) — far too high
        _ => 0.0
    };
}

public record InjuryRecord(
    string   InjuryName,
    string   BodyPart,
    int      Grade,
    DateOnly InjuredDate,
    DateOnly? ReturnedDate,
    int      EstimatedDays,
    int      ActualDays,
    int      GamesMissed,
    IReadOnlyDictionary<string, int>? PermanentPenalties
)
{
    public bool HadPermanentDebuff => PermanentPenalties?.Count > 0;
}

// ── Static injury tables ──────────────────────────────────────────────────────

public static class InjuryTables
{
    // Maps each Attrs body-part key to (displayName, bodyPartType, category)
    public static readonly IReadOnlyDictionary<string, (string Display, string Type, InjuryCategory Cat)> BodyParts =
        new Dictionary<string, (string, string, InjuryCategory)>
        {
            // Upper — single
            { "InjHead",       ("Head/Face",    "Head",       InjuryCategory.Upper) },
            { "InjNeck",       ("Neck",         "Neck",       InjuryCategory.Upper) },
            // Upper — paired
            { "InjLShoulder",  ("Left Shoulder","Shoulder",   InjuryCategory.Upper) },
            { "InjRShoulder",  ("Right Shoulder","Shoulder",  InjuryCategory.Upper) },
            { "InjLUpperArm",  ("Left Upper Arm","UpperArm",  InjuryCategory.Upper) },
            { "InjRUpperArm",  ("Right Upper Arm","UpperArm", InjuryCategory.Upper) },
            { "InjLElbow",     ("Left Elbow",   "Elbow",      InjuryCategory.Upper) },
            { "InjRElbow",     ("Right Elbow",  "Elbow",      InjuryCategory.Upper) },
            { "InjLForearm",   ("Left Forearm", "Forearm",    InjuryCategory.Upper) },
            { "InjRForearm",   ("Right Forearm","Forearm",    InjuryCategory.Upper) },
            { "InjLWrist",     ("Left Wrist",   "Wrist",      InjuryCategory.Upper) },
            { "InjRWrist",     ("Right Wrist",  "Wrist",      InjuryCategory.Upper) },
            { "InjLHand",      ("Left Hand",    "Hand",       InjuryCategory.Upper) },
            { "InjRHand",      ("Right Hand",   "Hand",       InjuryCategory.Upper) },
            { "InjLFingers",   ("Left Fingers", "Fingers",    InjuryCategory.Upper) },
            { "InjRFingers",   ("Right Fingers","Fingers",    InjuryCategory.Upper) },
            // Core — single
            { "InjChest",      ("Chest/Ribs",   "Chest",      InjuryCategory.Core) },
            { "InjUpperBack",  ("Upper Back",   "UpperBack",  InjuryCategory.Core) },
            { "InjAbdominals", ("Abdominals",   "Abdominals", InjuryCategory.Core) },
            { "InjLowerBack",  ("Lower Back",   "LowerBack",  InjuryCategory.Core) },
            // Core — paired
            { "InjLOblique",   ("Left Oblique", "Obliques",   InjuryCategory.Core) },
            { "InjROblique",   ("Right Oblique","Obliques",   InjuryCategory.Core) },
            // Lower — paired
            { "InjLHip",       ("Left Hip/Groin","Hip",       InjuryCategory.Lower) },
            { "InjRHip",       ("Right Hip/Groin","Hip",      InjuryCategory.Lower) },
            { "InjLHamstring", ("Left Hamstring","Hamstring", InjuryCategory.Lower) },
            { "InjRHamstring", ("Right Hamstring","Hamstring",InjuryCategory.Lower) },
            { "InjLQuad",      ("Left Quadriceps","Quad",     InjuryCategory.Lower) },
            { "InjRQuad",      ("Right Quadriceps","Quad",    InjuryCategory.Lower) },
            { "InjLKnee",      ("Left Knee",    "Knee",       InjuryCategory.Lower) },
            { "InjRKnee",      ("Right Knee",   "Knee",       InjuryCategory.Lower) },
            { "InjLShinCalf",  ("Left Shin/Calf","ShinCalf",  InjuryCategory.Lower) },
            { "InjRShinCalf",  ("Right Shin/Calf","ShinCalf", InjuryCategory.Lower) },
            { "InjLAchilles",  ("Left Achilles","Achilles",   InjuryCategory.Lower) },
            { "InjRAchilles",  ("Right Achilles","Achilles",  InjuryCategory.Lower) },
            { "InjLAnkle",     ("Left Ankle",   "Ankle",      InjuryCategory.Lower) },
            { "InjRAnkle",     ("Right Ankle",  "Ankle",      InjuryCategory.Lower) },
            { "InjLFoot",      ("Left Foot",    "Foot",       InjuryCategory.Lower) },
            { "InjRFoot",      ("Right Foot",   "Foot",       InjuryCategory.Lower) },
            { "InjLToes",      ("Left Toes",    "Toes",       InjuryCategory.Lower) },
            { "InjRToes",      ("Right Toes",   "Toes",       InjuryCategory.Lower) },
        };

    // Base injury chance per possession per body part key (at rating=50).
    // Multiplied by susceptibility formula: ((100-rating)/50)^1.0
    public static readonly IReadOnlyDictionary<string, double> BaseRates =
        BodyParts.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Cat switch
            {
                InjuryCategory.Upper => 0.000031,
                InjuryCategory.Core  => 0.000024,
                InjuryCategory.Lower => 0.0000455,
                _                    => 0.0000365,
            });

    // Injury definitions indexed by BodyPartType
    // Lookup: given a body part type (e.g. "Knee"), get all injuries for that type.
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<InjuryDefinition>> ByType =
        BuildByType();

    private static IReadOnlyDictionary<string, IReadOnlyList<InjuryDefinition>> BuildByType()
    {
        var all = BuildAll();
        return all
            .GroupBy(d => d.BodyPartType)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<InjuryDefinition>)g.ToList());
    }

    private static List<InjuryDefinition> BuildAll() =>
    [
        // ── HEAD / FACE ──────────────────────────────────────────────────────────
        new("Facial Cut",        "Head", InjuryCategory.Upper, 1,  2,
            new(0.96, 0.97, 1.0, 1.0), 0.0, []),
        new("Broken Nose",       "Head", InjuryCategory.Upper, 1,  4,
            new(0.93, 0.94, 1.0, 1.0), 0.0, []),
        new("Concussion",        "Head", InjuryCategory.Upper, 2, 14,
            new(0.85, 0.90, 1.0, 1.0), 0.05, ["Passing"]),
        new("Severe Concussion", "Head", InjuryCategory.Upper, 3, 40,
            InjuryDebuff.None, 0.12, ["Passing", "oBBIQ_never"]),  // oBBIQ_never = never actually applied

        // ── NECK ─────────────────────────────────────────────────────────────────
        new("Neck Strain",       "Neck", InjuryCategory.Upper, 1,  7,
            new(0.92, 0.91, 1.0, 1.0), 0.0, []),
        new("Whiplash",          "Neck", InjuryCategory.Upper, 2, 14,
            new(0.88, 0.88, 1.0, 1.0), 0.06, ["PerimDef", "Endurance"]),
        new("Neck Herniation",   "Neck", InjuryCategory.Upper, 3, 60,
            InjuryDebuff.None, 0.20, ["PerimDef", "Endurance"]),

        // ── SHOULDER ─────────────────────────────────────────────────────────────
        new("Shoulder Strain",     "Shoulder", InjuryCategory.Upper, 1, 10,
            new(0.88, 0.88, 1.0, 1.0), 0.0, []),
        new("Shoulder Separation", "Shoulder", InjuryCategory.Upper, 2, 25,
            new(0.72, 0.76, 1.0, 1.0), 0.14, ["Inside", "MidRange", "PerimDef", "IntDef"]),
        new("Rotator Cuff Tear",   "Shoulder", InjuryCategory.Upper, 3, 135,
            InjuryDebuff.None, 0.28, ["Inside", "MidRange", "PerimDef", "IntDef"]),
        new("Labrum Tear",         "Shoulder", InjuryCategory.Upper, 3, 180,
            InjuryDebuff.None, 0.30, ["Inside", "MidRange", "PerimDef", "IntDef"]),

        // ── UPPER ARM ────────────────────────────────────────────────────────────
        new("Bicep Strain",   "UpperArm", InjuryCategory.Upper, 1, 10,
            new(0.90, 0.88, 1.0, 1.0), 0.0, []),
        new("Tricep Strain",  "UpperArm", InjuryCategory.Upper, 2, 21,
            new(0.82, 0.80, 1.0, 1.0), 0.10, ["Strength", "Inside", "Dunks"]),
        new("Bicep Tear",     "UpperArm", InjuryCategory.Upper, 3, 90,
            InjuryDebuff.None, 0.25, ["Strength", "Inside", "Dunks"]),
        new("Tricep Tear",    "UpperArm", InjuryCategory.Upper, 3, 90,
            InjuryDebuff.None, 0.25, ["Strength", "Inside", "Dunks"]),

        // ── ELBOW ────────────────────────────────────────────────────────────────
        new("Elbow Tendinitis",  "Elbow", InjuryCategory.Upper, 1,  7,
            new(0.91, 0.93, 1.0, 1.0), 0.0, []),
        new("Elbow Bursitis",    "Elbow", InjuryCategory.Upper, 1,  6,
            new(0.92, 0.94, 1.0, 1.0), 0.0, []),
        new("Hyperextension",    "Elbow", InjuryCategory.Upper, 2, 14,
            new(0.80, 0.86, 1.0, 1.0), 0.10, ["Inside", "Strength"]),
        new("Elbow Fracture",    "Elbow", InjuryCategory.Upper, 3, 66,
            InjuryDebuff.None, 0.18, ["Inside", "Strength"]),

        // ── FOREARM ──────────────────────────────────────────────────────────────
        new("Forearm Strain",    "Forearm", InjuryCategory.Upper, 1,  7,
            new(0.90, 0.92, 1.0, 1.0), 0.0, []),
        new("Forearm Fracture",  "Forearm", InjuryCategory.Upper, 2, 32,
            new(0.80, 0.83, 1.0, 1.0), 0.12, ["Dribbling", "Passing"]),
        new("Compound Fracture", "Forearm", InjuryCategory.Upper, 3, 75,
            InjuryDebuff.None, 0.22, ["Dribbling", "Passing"]),

        // ── WRIST ────────────────────────────────────────────────────────────────
        new("Wrist Sprain",        "Wrist", InjuryCategory.Upper, 1,  9,
            new(0.88, 0.94, 1.0, 1.0), 0.0, []),
        new("Scaphoid Fracture",   "Wrist", InjuryCategory.Upper, 2, 42,
            new(0.74, 0.88, 1.0, 1.0), 0.16, ["Inside", "MidRange", "ThreePoint", "Dribbling"]),
        new("Wrist Surgery",       "Wrist", InjuryCategory.Upper, 3, 90,
            InjuryDebuff.None, 0.24, ["Inside", "MidRange", "ThreePoint", "Dribbling"]),

        // ── HAND ─────────────────────────────────────────────────────────────────
        new("Finger Jam",          "Hand", InjuryCategory.Upper, 1,  4,
            new(0.92, 0.96, 1.0, 1.0), 0.0, []),
        new("Broken Metacarpal",   "Hand", InjuryCategory.Upper, 2, 32,
            new(0.76, 0.88, 1.0, 1.0), 0.12, ["Inside", "MidRange", "ThreePoint", "Dribbling"]),

        // ── FINGERS ──────────────────────────────────────────────────────────────
        new("Finger Dislocation",  "Fingers", InjuryCategory.Upper, 1,  3,
            new(0.91, 0.95, 1.0, 1.0), 0.0, []),
        new("Finger Fracture",     "Fingers", InjuryCategory.Upper, 2, 19,
            new(0.82, 0.91, 1.0, 1.0), 0.08, ["Inside", "ThreePoint", "Dribbling"]),

        // ── CHEST / RIBS ─────────────────────────────────────────────────────────
        new("Bruised Ribs",  "Chest", InjuryCategory.Core, 1, 10,
            new(0.91, 0.87, 0.93, 0.94), 0.0, []),
        new("Pec Strain",    "Chest", InjuryCategory.Core, 2, 21,
            new(0.84, 0.78, 0.88, 0.90), 0.12, ["Strength", "Endurance"]),
        new("Pec Tear",      "Chest", InjuryCategory.Core, 3, 90,
            InjuryDebuff.None, 0.28, ["Strength", "Endurance"]),

        // ── UPPER BACK ───────────────────────────────────────────────────────────
        new("Thoracic Strain",  "UpperBack", InjuryCategory.Core, 1,  7,
            new(0.91, 0.87, 0.90, 0.86), 0.0, []),
        new("Facet Syndrome",   "UpperBack", InjuryCategory.Core, 2, 25,
            new(0.84, 0.80, 0.83, 0.78), 0.10, ["Jumping", "IntDef"]),

        // ── ABDOMINALS ───────────────────────────────────────────────────────────
        new("Core Strain",       "Abdominals", InjuryCategory.Core, 1, 10,
            new(0.90, 0.86, 0.86, 0.83), 0.0, []),
        new("Hernia",            "Abdominals", InjuryCategory.Core, 2, 32,
            new(0.83, 0.78, 0.78, 0.74), 0.14, ["Speed", "Jumping", "Endurance"]),
        new("Hernia Surgery",    "Abdominals", InjuryCategory.Core, 3, 66,
            InjuryDebuff.None, 0.22, ["Speed", "Jumping", "Endurance"]),

        // ── OBLIQUES ─────────────────────────────────────────────────────────────
        new("Side Strain",      "Obliques", InjuryCategory.Core, 1, 10,
            new(0.90, 0.86, 0.84, 0.90), 0.0, []),
        new("Oblique Tear",     "Obliques", InjuryCategory.Core, 2, 25,
            new(0.83, 0.79, 0.76, 0.84), 0.10, ["Speed", "Jumping"]),

        // ── LOWER BACK ───────────────────────────────────────────────────────────
        new("Back Spasms",      "LowerBack", InjuryCategory.Core, 1,  5,
            new(0.88, 0.84, 0.83, 0.82), 0.0, []),
        new("Disc Herniation",  "LowerBack", InjuryCategory.Core, 2, 37,
            new(0.80, 0.75, 0.74, 0.73), 0.20, ["Speed", "Jumping", "Endurance", "Strength"]),
        new("Disc Surgery",     "LowerBack", InjuryCategory.Core, 3, 135,
            InjuryDebuff.None, 0.35, ["Speed", "Jumping", "Endurance", "Strength"]),

        // ── HIP / GROIN ──────────────────────────────────────────────────────────
        new("Adductor Strain",  "Hip", InjuryCategory.Lower, 1, 14,
            new(0.88, 0.86, 0.78, 0.83), 0.0, []),
        new("Hip Flexor Tear",  "Hip", InjuryCategory.Lower, 2, 32,
            new(0.81, 0.78, 0.68, 0.74), 0.14, ["Speed", "Jumping"]),
        new("Labrum Tear",      "Hip", InjuryCategory.Lower, 3, 90,
            InjuryDebuff.None, 0.24, ["Speed", "Jumping"]),

        // ── HAMSTRING ────────────────────────────────────────────────────────────
        new("Hamstring Strain", "Hamstring", InjuryCategory.Lower, 1, 11,
            new(0.90, 0.87, 0.73, 0.80), 0.0, []),
        new("Hamstring Pull",   "Hamstring", InjuryCategory.Lower, 2, 21,
            new(0.83, 0.80, 0.62, 0.72), 0.14, ["Speed", "Endurance"]),
        new("Hamstring Tear",   "Hamstring", InjuryCategory.Lower, 3, 90,
            InjuryDebuff.None, 0.26, ["Speed", "Endurance"]),

        // ── QUADRICEPS ───────────────────────────────────────────────────────────
        new("Quad Contusion",   "Quad", InjuryCategory.Lower, 1,  5,
            new(0.92, 0.88, 0.82, 0.76), 0.0, []),
        new("Quad Strain",      "Quad", InjuryCategory.Lower, 2, 16,
            new(0.84, 0.80, 0.72, 0.66), 0.12, ["Speed", "Jumping", "Strength"]),
        new("Quad Tear",        "Quad", InjuryCategory.Lower, 3, 75,
            InjuryDebuff.None, 0.24, ["Speed", "Jumping", "Strength"]),

        // ── KNEE ─────────────────────────────────────────────────────────────────
        new("Knee Contusion",   "Knee", InjuryCategory.Lower, 1,  5,
            new(0.90, 0.87, 0.75, 0.74), 0.0, []),
        new("Knee Tendinitis",  "Knee", InjuryCategory.Lower, 1,  8,
            new(0.89, 0.86, 0.74, 0.72), 0.0, []),
        new("Meniscus Sprain",  "Knee", InjuryCategory.Lower, 2, 21,
            new(0.83, 0.79, 0.67, 0.65), 0.16, ["Speed", "Jumping", "Strength"]),
        new("MCL Sprain",       "Knee", InjuryCategory.Lower, 2, 28,
            new(0.82, 0.78, 0.65, 0.63), 0.18, ["Speed", "Jumping", "Strength"]),
        new("Meniscus Surgery", "Knee", InjuryCategory.Lower, 3, 90,
            InjuryDebuff.None, 0.28, ["Speed", "Jumping", "Strength"]),
        new("ACL Tear",         "Knee", InjuryCategory.Lower, 3, 300,
            InjuryDebuff.None, 0.40, ["Speed", "Jumping", "Strength"]),

        // ── SHIN / CALF ───────────────────────────────────────────────────────────
        new("Shin Splints",     "ShinCalf", InjuryCategory.Lower, 1, 10,
            new(0.91, 0.88, 0.78, 0.85), 0.0, []),
        new("Calf Strain",      "ShinCalf", InjuryCategory.Lower, 2, 21,
            new(0.84, 0.81, 0.70, 0.78), 0.12, ["Speed", "Endurance"]),
        new("Stress Fracture",  "ShinCalf", InjuryCategory.Lower, 3, 66,
            InjuryDebuff.None, 0.20, ["Speed", "Endurance"]),

        // ── ACHILLES ─────────────────────────────────────────────────────────────
        new("Achilles Tendinopathy", "Achilles", InjuryCategory.Lower, 1, 14,
            new(0.90, 0.86, 0.68, 0.66), 0.0, []),
        new("Partial Achilles Tear", "Achilles", InjuryCategory.Lower, 2, 44,
            new(0.82, 0.78, 0.56, 0.54), 0.20, ["Speed", "Jumping"]),
        new("Achilles Rupture",      "Achilles", InjuryCategory.Lower, 3, 270,
            InjuryDebuff.None, 0.38, ["Speed", "Jumping"]),

        // ── ANKLE ────────────────────────────────────────────────────────────────
        new("Ankle Roll",          "Ankle", InjuryCategory.Lower, 1,  5,
            new(0.91, 0.85, 0.74, 0.84), 0.0, []),
        new("Ankle Sprain",        "Ankle", InjuryCategory.Lower, 1,  7,
            new(0.90, 0.84, 0.72, 0.83), 0.0, []),
        new("High Ankle Sprain",   "Ankle", InjuryCategory.Lower, 2, 21,
            new(0.83, 0.76, 0.65, 0.76), 0.12, ["Speed", "Jumping"]),
        new("Ankle Fracture",      "Ankle", InjuryCategory.Lower, 3, 42,
            InjuryDebuff.None, 0.20, ["Speed", "Jumping"]),

        // ── FOOT ─────────────────────────────────────────────────────────────────
        new("Turf Toe",            "Foot", InjuryCategory.Lower, 1,  7,
            new(0.90, 0.86, 0.76, 0.90), 0.0, []),
        new("Plantar Fasciitis",   "Foot", InjuryCategory.Lower, 2, 25,
            new(0.83, 0.80, 0.68, 0.86), 0.12, ["Speed"]),
        new("Foot Stress Fracture","Foot", InjuryCategory.Lower, 3, 66,
            InjuryDebuff.None, 0.18, ["Speed"]),

        // ── TOES ─────────────────────────────────────────────────────────────────
        new("Toe Jam/Dislocation", "Toes", InjuryCategory.Lower, 1,  2,
            new(0.91, 0.86, 0.84, 0.87), 0.0, []),
        new("Broken Toe",          "Toes", InjuryCategory.Lower, 2, 19,
            new(0.84, 0.78, 0.76, 0.80), 0.08, ["Speed"]),
    ];

    // Dominant-hand arm body-part keys — used to scale shooting debuff.
    public static readonly HashSet<string> ArmBodyPartKeys =
    [
        "InjLShoulder","InjRShoulder",
        "InjLUpperArm","InjRUpperArm",
        "InjLElbow","InjRElbow",
        "InjLForearm","InjRForearm",
        "InjLWrist","InjRWrist",
        "InjLHand","InjRHand",
        "InjLFingers","InjRFingers",
    ];

    // Grade distribution: G1=71%, G2=26%, G3=3%
    public static readonly double[] GradeThresholds = [0.71, 0.97]; // < 0.71 → G1, < 0.97 → G2, else G3

    // Rating degradation per grade after injury
    public static int RatingDegradation(int grade) => grade switch { 1 => 3, 2 => 8, _ => 18 };
}
