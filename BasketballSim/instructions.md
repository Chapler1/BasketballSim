# Basketball Sim — Implementation Instructions
## Scope: Game Engine Rewrite + Live Box Score UI

> **Read this entire document before writing any code.** These instructions replace the current `GameEngine.cs`, `PossessionResult.cs`, and `GameSim.razor` with a full player-driven possession pipeline and live box score viewer.

---

## Project Structure — Files to Create or Replace

```
BasketballSim/
├── Models/
│   ├── Player.cs               ← NEW
│   ├── Team.cs                 ← NEW
│   ├── PossessionResult.cs     ← REPLACE (expand existing)
│   └── PlayerGameStats.cs      ← NEW
├── Data/
│   └── RosterData.cs           ← NEW (hardcoded Knicks + Thunder)
├── Simulation/
│   └── GameEngine.cs           ← REPLACE (full rewrite)
└── Components/Pages/
    └── GameSim.razor           ← REPLACE (add box score panel)
```

---

## Step 1 — Player.cs

Create `Models/Player.cs`. This is the full attribute system.

**Physical attributes** are visible to the GM. **Skill/shooting attributes are hidden** (internal — never serialized to UI). All attributes use the Basketball GM 0–100 scale.

```csharp
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
    // Scale: 0–100
    public int Height     { get; init; }   // in inches (e.g. 79 = 6'7")
    public int Strength   { get; init; }
    public int Speed      { get; init; }
    public int Jumping    { get; init; }
    public int Endurance  { get; init; }   // affects fatigue rate

    // ── Shooting Attributes (HIDDEN) ──────────────────────────────
    // These are the TRUE attributes driving simulation math.
    // NEVER expose these to any UI component or API response.
    internal int Attr_Inside      { get; init; }  // layups, dunks, post finishing
    internal int Attr_Dunks       { get; init; }  // dunk tendency + success rate modifier
    internal int Attr_FreeThrow   { get; init; }
    internal int Attr_MidRange    { get; init; }
    internal int Attr_ThreePoint  { get; init; }

    // ── Skill Attributes (HIDDEN) ──────────────────────────────────
    internal int Attr_BasketballIQ     { get; init; }  // reduces TO, improves shot selection
    internal int Attr_Dribbling        { get; init; }  // affects steal chance against, ball handling
    internal int Attr_Passing          { get; init; }  // drives assist probability
    internal int Attr_Rebounding_Off   { get; init; }  // offensive rebound probability
    internal int Attr_Rebounding_Def   { get; init; }  // defensive rebound probability

    // ── Defensive Attributes (HIDDEN) ─────────────────────────────
    internal int Attr_PerimeterDefense { get; init; }  // contests threes + mid-range
    internal int Attr_InteriorDefense  { get; init; }  // contests inside shots, blocks

    // ── Derived Tendency Properties ───────────────────────────────
    // These are computed from attributes. Used by the engine.
    // Still internal — GM cannot see these directly.

    /// Probability this player is selected as ball handler (0–1)
    internal double USG_Weight =>
        (Attr_BasketballIQ + Attr_Dribbling + Attr_MidRange + Attr_ThreePoint) / 400.0;

    /// How often this player defers vs. creates (0 = pure creator, 1 = pure role player)
    internal double DeferralTendency =>
        1.0 - (Attr_Dribbling + Attr_BasketballIQ) / 200.0;

    /// Tendency to drive to the rim (0–1)
    internal double DriveGravity =>
        (Speed + Attr_Dribbling + Attr_Inside) / 300.0;

    /// Three-point threat level (0–1) — affects spacing calculation
    internal double PerimeterGravity =>
        Attr_ThreePoint / 100.0;

    /// How aggressively this player attacks early in shot clock (0–1)
    internal double ShotClockAggressiveness =>
        (Speed + Attr_BasketballIQ) / 200.0;

    /// Tendency to attempt cuts toward the basket (0–1)
    internal double CutTendency =>
        (Speed + Jumping + Attr_BasketballIQ) / 300.0;

    /// Tendency to attempt alley-oops (0–1)
    internal double AlleyOopTendency =>
        (Jumping + Attr_Dunks + Speed) / 300.0;

    /// Fatigue multiplier — starts at 0, increases with minutes
    public double Fatigue { get; set; } = 0.0;

    // ── Make% Converters ──────────────────────────────────────────
    // Converts 0–100 attribute to a realistic NBA shooting percentage.
    // These are the ONLY way the engine accesses shooting probability.

    /// Converts Inside attribute (0–100) to a realistic make% (0.45–0.72)
    internal double InsideMakePct =>
        0.45 + (Attr_Inside / 100.0) * 0.27;

    /// Converts MidRange attribute (0–100) to a realistic make% (0.30–0.52)
    internal double MidRangeMakePct =>
        0.30 + (Attr_MidRange / 100.0) * 0.22;

    /// Converts ThreePoint attribute (0–100) to a realistic make% (0.28–0.45)
    internal double ThreeMakePct =>
        0.28 + (Attr_ThreePoint / 100.0) * 0.17;

    /// Converts FreeThrow attribute (0–100) to a realistic make% (0.55–0.95)
    internal double FTMakePct =>
        0.55 + (Attr_FreeThrow / 100.0) * 0.40;

    /// Block chance modifier from InteriorDefense (0–0.08)
    internal double BlockMod =>
        (Attr_InteriorDefense / 100.0) * 0.08;

    /// Steal modifier from PerimeterDefense + Speed (0–0.03)
    internal double StealMod =>
        ((Attr_PerimeterDefense + Speed) / 200.0) * 0.03;

    /// Turnover rate — lower IQ and dribbling = more turnovers (0.06–0.18)
    internal double TurnoverRate =>
        0.18 - ((Attr_BasketballIQ + Attr_Dribbling) / 200.0) * 0.12;

    /// Offensive rebound probability weight
    internal double ORebWeight =>
        (Attr_Rebounding_Off + Jumping) / 200.0;

    /// Defensive rebound probability weight
    internal double DRebWeight =>
        (Attr_Rebounding_Def + Height) / 200.0;

    /// Pass assist probability weight
    internal double AssistWeight =>
        (Attr_Passing + Attr_BasketballIQ) / 200.0;

    /// Contest penalty applied to shooter's make% (0–0.09)
    internal double ContestPenalty(ShotType shot) => shot switch
    {
        ShotType.Inside or ShotType.Dunk =>
            (Attr_InteriorDefense / 100.0) * 0.09,
        ShotType.MidRange =>
            (Attr_InteriorDefense * 0.4 + Attr_PerimeterDefense * 0.6) / 100.0 * 0.07,
        ShotType.ThreePointer =>
            (Attr_PerimeterDefense / 100.0) * 0.06,
        _ => 0.04
    };
}
```

---

## Step 2 — Team.cs

Create `Models/Team.cs`.

```csharp
namespace BasketballSim.Models;

public class Team
{
    public required string       Name         { get; init; }
    public required string       Abbreviation { get; init; }
    public required string       PrimaryColor { get; init; }   // hex e.g. "#006BB6"
    public required string       SecondaryColor { get; init; }
    public required List<Player> Roster       { get; init; }   // full 12-man roster

    /// Possessions per 48 minutes (affects game pace)
    public double Pace { get; init; } = 100.0;

    /// Returns the 5-man starting lineup
    public List<Player> Starters => Roster.Take(5).ToList();

    /// Returns the bench unit
    public List<Player> Bench => Roster.Skip(5).ToList();

    /// Spacing level: count of credible three-point threats on the floor (0–5)
    public int SpacingLevel(List<Player> lineup) =>
        lineup.Count(p => p.Attr_ThreePoint >= 55);
}
```

---

## Step 3 — PlayerGameStats.cs

Create `Models/PlayerGameStats.cs`. This is the **only stats object that gets sent to the UI**.

```csharp
namespace BasketballSim.Models;

public class PlayerGameStats
{
    public required string Name     { get; init; }
    public required string Team     { get; init; }
    public required Position Position { get; init; }

    // Box score counters — all mutable during simulation
    public int    Points     { get; set; }
    public int    Rebounds   { get; set; }
    public int    OffRebounds { get; set; }
    public int    DefRebounds { get; set; }
    public int    Assists    { get; set; }
    public int    Steals     { get; set; }
    public int    Blocks     { get; set; }
    public int    Turnovers  { get; set; }
    public int    FGMade     { get; set; }
    public int    FGAttempts { get; set; }
    public int    ThreeMade  { get; set; }
    public int    ThreeAttempts { get; set; }
    public int    FTMade     { get; set; }
    public int    FTAttempts { get; set; }
    public double MinutesPlayed { get; set; }
    public int    PlusMinus  { get; set; }

    // Computed display properties
    public string FGDisplay  => $"{FGMade}/{FGAttempts}";
    public string ThreeDisplay => $"{ThreeMade}/{ThreeAttempts}";
    public string FTDisplay  => $"{FTMade}/{FTAttempts}";
    public double FGPct      => FGAttempts > 0 ? (double)FGMade / FGAttempts : 0;
    public double ThreePct   => ThreeAttempts > 0 ? (double)ThreeMade / ThreeAttempts : 0;
}
```

---

## Step 4 — PossessionResult.cs

**Replace** the existing `PossessionResult.cs` entirely.

```csharp
namespace BasketballSim.Models;

public enum ShotType    { Inside, Dunk, MidRange, ThreePointer }

public enum ShotContext
{
    // Inside / Dunk
    DrivingLayup, PickAndRollRoll, CutLayup, FastBreakLayup, PostMove, FloaterLayup,
    AlleyOop, CutDunk, TransitionDunk, PickAndRollDunk,
    // Mid Range
    PullUpMidRange, IsolationMidRange, PickAndRollMidRange, FadeawayMidRange,
    PostMoveMidRange, LateClockMidRange,
    // Three
    CatchAndShootCorner, CatchAndShootWing, PullUpThree, StepBackThree,
    PickAndRollThree, TransitionThree,
    // Secondary
    Putback, TipIn,
    // None (turnovers/FT)
    None
}

public enum PossessionEvent
{
    ShotMade, ShotMissed, Blocked, TurnoverStolen, TurnoverDeadBall,
    FreeThrows, OffensiveRebound, DefensiveRebound, GameWinnerMade, GameWinnerMissed
}

public class PossessionResult
{
    public required string          Team          { get; init; }
    public required string          Narrative     { get; init; }
    public          int             HomeScore     { get; init; }
    public          int             AwayScore     { get; init; }
    public          int             Quarter       { get; init; }
    public          int             ClockSeconds  { get; init; }
    public          int             PointsScored  { get; init; }
    public          PossessionEvent Event         { get; init; }

    // Player attribution (nullable — not all possessions have all actors)
    public string? Scorer    { get; init; }
    public string? Assister  { get; init; }
    public string? Blocker   { get; init; }
    public string? Rebounder { get; init; }
    public string? Stealer   { get; init; }

    // Shot detail
    public ShotType?    Shot    { get; init; }
    public ShotContext? Context { get; init; }
    public bool         IsThree { get; init; }
}
```

---

## Step 5 — RosterData.cs

Create `Data/RosterData.cs`. Hardcode the **2024–25 New York Knicks** and **Oklahoma City Thunder** starting rosters plus key bench players (8 players per team minimum). Use real-life approximate stats converted to the 0–100 Basketball GM scale.

### Attribute Conversion Guide
Use these benchmarks when hardcoding. Real NBA percentages → 0–100 scale:

| Real Stat             | 0–100 Formula                           |
|-----------------------|-----------------------------------------|
| At-rim FG% (inside)   | `(pct - 0.45) / 0.27 * 100`, clamp 0–100 |
| Mid-range FG%         | `(pct - 0.30) / 0.22 * 100`, clamp 0–100 |
| Three-point FG%       | `(pct - 0.28) / 0.17 * 100`, clamp 0–100 |
| Free throw %          | `(pct - 0.55) / 0.40 * 100`, clamp 0–100 |

**Physical scale reference:**
- Height: actual inches (72 = 6'0", 84 = 7'0")
- Speed/Jumping/Strength/Endurance: subjective 0–100 based on known player athleticism

### Knicks Roster (hardcode these players)

```
Starters (indices 0–4):
  Jalen Brunson      PG  #11  Age 27
  OG Anunoby         SF  #8   Age 26
  Mikal Bridges      SG  #25  Age 27
  Julius Randle      PF  #30  Age 29
  Karl-Anthony Towns C   #32  Age 28

Bench (indices 5–11):
  Donte DiVincenzo   SG  #0   Age 27
  Josh Hart          SF  #3   Age 29
  Isaiah Hartenstein C   #55  Age 26
  Miles McBride      PG  #2   Age 23
  Precious Achiuwa   PF  #5   Age 24
  Landry Shamet      SG  #14  Age 26
  Deuce McBride      PG  #8   Age 22
```

### Thunder Roster (hardcode these players)

```
Starters (indices 0–4):
  Shai Gilgeous-Alexander  PG  #2   Age 25
  Luguentz Dort            SG  #5   Age 25
  Jalen Williams           SF  #8   Age 22
  Chet Holmgren            PF  #7   Age 22
  Isaiah Hartenstein        C  #55  Age 26

Bench (indices 5–11):
  Aaron Wiggins            SG  #21  Age 24
  Kenrich Williams         SF  #34  Age 28
  Ousmane Dieng            SF  #13  Age 21
  Josh Giddey              PG  #3   Age 21
  Jaylin Williams          C   #6   Age 22
  Isaiah Joe               SG  #11  Age 24
  Tre Mann                 PG  #23  Age 23
```

### Attribute Values to Hardcode

Use the following **exact values** (already converted to 0–100 scale from 2024–25 real stats):

#### NEW YORK KNICKS

```csharp
// Jalen Brunson — elite scorer, high IQ, limited athleticism
new Player {
    Name="Jalen Brunson", Team="Knicks", JerseyNumber=11, Position=Position.PG, Age=27,
    Height=74, Strength=68, Speed=72, Jumping=55, Endurance=88,
    Attr_Inside=72, Attr_Dunks=30, Attr_FreeThrow=87, Attr_MidRange=88, Attr_ThreePoint=74,
    Attr_BasketballIQ=92, Attr_Dribbling=88, Attr_Passing=78,
    Attr_Rebounding_Off=22, Attr_Rebounding_Def=28,
    Attr_PerimeterDefense=52, Attr_InteriorDefense=30
}

// OG Anunoby — elite defender, developing offense
new Player {
    Name="OG Anunoby", Team="Knicks", JerseyNumber=8, Position=Position.SF, Age=26,
    Height=79, Strength=78, Speed=80, Jumping=74, Endurance=85,
    Attr_Inside=68, Attr_Dunks=58, Attr_FreeThrow=72, Attr_MidRange=62, Attr_ThreePoint=71,
    Attr_BasketballIQ=75, Attr_Dribbling=65, Attr_Passing=55,
    Attr_Rebounding_Off=38, Attr_Rebounding_Def=52,
    Attr_PerimeterDefense=92, Attr_InteriorDefense=70
}

// Mikal Bridges — 3-and-D, solid all-around
new Player {
    Name="Mikal Bridges", Team="Knicks", JerseyNumber=25, Position=Position.SG, Age=27,
    Height=78, Strength=72, Speed=78, Jumping=68, Endurance=90,
    Attr_Inside=65, Attr_Dunks=48, Attr_FreeThrow=78, Attr_MidRange=70, Attr_ThreePoint=68,
    Attr_BasketballIQ=80, Attr_Dribbling=70, Attr_Passing=60,
    Attr_Rebounding_Off=32, Attr_Rebounding_Def=48,
    Attr_PerimeterDefense=88, Attr_InteriorDefense=55
}

// Julius Randle — post scorer, high usage PF
new Player {
    Name="Julius Randle", Team="Knicks", JerseyNumber=30, Position=Position.PF, Age=29,
    Height=81, Strength=88, Speed=66, Jumping=65, Endurance=82,
    Attr_Inside=80, Attr_Dunks=62, Attr_FreeThrow=75, Attr_MidRange=78, Attr_ThreePoint=58,
    Attr_BasketballIQ=74, Attr_Dribbling=72, Attr_Passing=68,
    Attr_Rebounding_Off=58, Attr_Rebounding_Def=70,
    Attr_PerimeterDefense=45, Attr_InteriorDefense=62
}

// Karl-Anthony Towns — shooting big, high inside + three
new Player {
    Name="Karl-Anthony Towns", Team="Knicks", JerseyNumber=32, Position=Position.C, Age=28,
    Height=84, Strength=82, Speed=60, Jumping=68, Endurance=78,
    Attr_Inside=85, Attr_Dunks=70, Attr_FreeThrow=86, Attr_MidRange=75, Attr_ThreePoint=82,
    Attr_BasketballIQ=78, Attr_Dribbling=55, Attr_Passing=60,
    Attr_Rebounding_Off=62, Attr_Rebounding_Def=75,
    Attr_PerimeterDefense=38, Attr_InteriorDefense=68
}

// Donte DiVincenzo — shooter, energetic bench scorer
new Player {
    Name="Donte DiVincenzo", Team="Knicks", JerseyNumber=0, Position=Position.SG, Age=27,
    Height=76, Strength=65, Speed=76, Jumping=66, Endurance=82,
    Attr_Inside=58, Attr_Dunks=38, Attr_FreeThrow=80, Attr_MidRange=64, Attr_ThreePoint=78,
    Attr_BasketballIQ=72, Attr_Dribbling=65, Attr_Passing=58,
    Attr_Rebounding_Off=35, Attr_Rebounding_Def=42,
    Attr_PerimeterDefense=70, Attr_InteriorDefense=38
}

// Josh Hart — hustle player, rebounding guard
new Player {
    Name="Josh Hart", Team="Knicks", JerseyNumber=3, Position=Position.SF, Age=29,
    Height=77, Strength=75, Speed=74, Jumping=65, Endurance=92,
    Attr_Inside=65, Attr_Dunks=45, Attr_FreeThrow=65, Attr_MidRange=55, Attr_ThreePoint=55,
    Attr_BasketballIQ=70, Attr_Dribbling=60, Attr_Passing=58,
    Attr_Rebounding_Off=62, Attr_Rebounding_Def=68,
    Attr_PerimeterDefense=72, Attr_InteriorDefense=50
}

// Isaiah Hartenstein — defensive center, passing big
new Player {
    Name="Isaiah Hartenstein", Team="Knicks", JerseyNumber=55, Position=Position.C, Age=26,
    Height=84, Strength=80, Speed=55, Jumping=60, Endurance=80,
    Attr_Inside=72, Attr_Dunks=58, Attr_FreeThrow=68, Attr_MidRange=48, Attr_ThreePoint=20,
    Attr_BasketballIQ=78, Attr_Dribbling=40, Attr_Passing=72,
    Attr_Rebounding_Off=65, Attr_Rebounding_Def=78,
    Attr_PerimeterDefense=42, Attr_InteriorDefense=80
}
```

#### OKLAHOMA CITY THUNDER

```csharp
// Shai Gilgeous-Alexander — superstar, elite scorer + handles
new Player {
    Name="Shai Gilgeous-Alexander", Team="Thunder", JerseyNumber=2, Position=Position.PG, Age=25,
    Height=79, Strength=70, Speed=85, Jumping=72, Endurance=92,
    Attr_Inside=88, Attr_Dunks=55, Attr_FreeThrow=88, Attr_MidRange=90, Attr_ThreePoint=72,
    Attr_BasketballIQ=94, Attr_Dribbling=95, Attr_Passing=80,
    Attr_Rebounding_Off=30, Attr_Rebounding_Def=40,
    Attr_PerimeterDefense=80, Attr_InteriorDefense=42
}

// Luguentz Dort — lockdown defender, developing offense
new Player {
    Name="Luguentz Dort", Team="Thunder", JerseyNumber=5, Position=Position.SG, Age=25,
    Height=76, Strength=82, Speed=78, Jumping=70, Endurance=88,
    Attr_Inside=62, Attr_Dunks=50, Attr_FreeThrow=68, Attr_MidRange=60, Attr_ThreePoint=66,
    Attr_BasketballIQ=72, Attr_Dribbling=65, Attr_Passing=48,
    Attr_Rebounding_Off=35, Attr_Rebounding_Def=45,
    Attr_PerimeterDefense=95, Attr_InteriorDefense=60
}

// Jalen Williams — rising star, versatile scorer
new Player {
    Name="Jalen Williams", Team="Thunder", JerseyNumber=8, Position=Position.SF, Age=22,
    Height=78, Strength=72, Speed=80, Jumping=70, Endurance=86,
    Attr_Inside=78, Attr_Dunks=58, Attr_FreeThrow=84, Attr_MidRange=80, Attr_ThreePoint=70,
    Attr_BasketballIQ=85, Attr_Dribbling=80, Attr_Passing=68,
    Attr_Rebounding_Off=35, Attr_Rebounding_Def=42,
    Attr_PerimeterDefense=68, Attr_InteriorDefense=45
}

// Chet Holmgren — mobile shot-blocking big, shooting stretch 5
new Player {
    Name="Chet Holmgren", Team="Thunder", JerseyNumber=7, Position=Position.PF, Age=22,
    Height=84, Strength=55, Speed=68, Jumping=78, Endurance=75,
    Attr_Inside=70, Attr_Dunks=65, Attr_FreeThrow=80, Attr_MidRange=72, Attr_ThreePoint=76,
    Attr_BasketballIQ=82, Attr_Dribbling=50, Attr_Passing=62,
    Attr_Rebounding_Off=48, Attr_Rebounding_Def=68,
    Attr_PerimeterDefense=55, Attr_InteriorDefense=88
}

// Isaiah Hartenstein (Thunder) — duplicate but on different team
new Player {
    Name="Isaiah Hartenstein", Team="Thunder", JerseyNumber=55, Position=Position.C, Age=26,
    Height=84, Strength=80, Speed=55, Jumping=60, Endurance=80,
    Attr_Inside=72, Attr_Dunks=58, Attr_FreeThrow=68, Attr_MidRange=48, Attr_ThreePoint=20,
    Attr_BasketballIQ=78, Attr_Dribbling=40, Attr_Passing=72,
    Attr_Rebounding_Off=65, Attr_Rebounding_Def=78,
    Attr_PerimeterDefense=42, Attr_InteriorDefense=80
}

// Aaron Wiggins — reliable 3-and-D bench wing
new Player {
    Name="Aaron Wiggins", Team="Thunder", JerseyNumber=21, Position=Position.SG, Age=24,
    Height=77, Strength=68, Speed=76, Jumping=66, Endurance=82,
    Attr_Inside=60, Attr_Dunks=42, Attr_FreeThrow=74, Attr_MidRange=62, Attr_ThreePoint=72,
    Attr_BasketballIQ=70, Attr_Dribbling=62, Attr_Passing=52,
    Attr_Rebounding_Off=30, Attr_Rebounding_Def=42,
    Attr_PerimeterDefense=74, Attr_InteriorDefense=42
}

// Kenrich Williams — versatile energy player
new Player {
    Name="Kenrich Williams", Team="Thunder", JerseyNumber=34, Position=Position.SF, Age=28,
    Height=79, Strength=74, Speed=70, Jumping=62, Endurance=85,
    Attr_Inside=60, Attr_Dunks=40, Attr_FreeThrow=70, Attr_MidRange=58, Attr_ThreePoint=60,
    Attr_BasketballIQ=75, Attr_Dribbling=58, Attr_Passing=60,
    Attr_Rebounding_Off=45, Attr_Rebounding_Def=55,
    Attr_PerimeterDefense=76, Attr_InteriorDefense=52
}

// Josh Giddey — playmaking big guard
new Player {
    Name="Josh Giddey", Team="Thunder", JerseyNumber=3, Position=Position.PG, Age=21,
    Height=81, Strength=68, Speed=70, Jumping=58, Endurance=80,
    Attr_Inside=62, Attr_Dunks=38, Attr_FreeThrow=65, Attr_MidRange=58, Attr_ThreePoint=50,
    Attr_BasketballIQ=80, Attr_Dribbling=70, Attr_Passing=82,
    Attr_Rebounding_Off=50, Attr_Rebounding_Def=58,
    Attr_PerimeterDefense=55, Attr_InteriorDefense=40
}
```

The `RosterData` class should expose two static properties:

```csharp
public static class RosterData
{
    public static Team Knicks { get; } = new Team {
        Name = "New York Knicks",
        Abbreviation = "NYK",
        PrimaryColor = "#006BB6",
        SecondaryColor = "#F58426",
        Pace = 97.4,
        Roster = [ /* all 8 Knicks players above */ ]
    };

    public static Team Thunder { get; } = new Team {
        Name = "Oklahoma City Thunder",
        Abbreviation = "OKC",
        PrimaryColor = "#007AC1",
        SecondaryColor = "#EF3B24",
        Pace = 100.2,
        Roster = [ /* all 8 Thunder players above */ ]
    };
}
```

---

## Step 6 — GameEngine.cs (Full Rewrite)

**Replace** `GameEngine.cs` entirely. Implement the possession pipeline described below. Do **not** preserve any logic from the old file.

### 6.1 — Enums and Helper Types

At the top of `GameEngine.cs`, define:

```csharp
public enum ShotClockPhase { Early, Mid, Late, BuzzerBeater }
public enum PossessionContext { Regular, MustScore, PotentialGameWinner, IntentionalFoul }
public enum TurnoverType { Stolen, DeadBall }
```

### 6.2 — GameState struct

```csharp
public record GameState(
    int HomeScore, int AwayScore,
    int Quarter, int ClockSeconds,
    bool IsHomePossession)
{
    public int ScoreDiff => IsHomePossession
        ? HomeScore - AwayScore
        : AwayScore - HomeScore;

    public bool IsClutch => Quarter >= 4
        && ClockSeconds <= 300
        && Math.Abs(HomeScore - AwayScore) <= 5;
}
```

### 6.3 — SimulateGame method

```csharp
public List<PossessionResult> SimulateGame(Team homeTeam, Team awayTeam)
```

- Use `homeTeam.Pace` and `awayTeam.Pace` to derive possession count per quarter:
  `int possessionsPerQuarter = (int)((homeTeam.Pace + awayTeam.Pace) / 2 / 4)`
- Simulate 4 quarters. Track `homeScore`, `awayScore`, `clockSeconds` (starts at 720 per quarter).
- Clock consumption per possession: roll between 10–22 seconds based on action player's `ShotClockAggressiveness`.
- Each possession calls `SimulatePossessionChain(...)`.
- After Q4, if tied: simulate 5-minute OT periods (300 seconds, same logic).

### 6.4 — The Possession Pipeline

Implement as a private method `SimulatePossessionChain`. Follow these stages **in order**:

#### Stage 1 — Possession Context
```
if (ClockSeconds <= 5 && Math.Abs(scoreDiff) <= 3) → PotentialGameWinner
else if (trailing by 8+ with under 2 minutes in Q4) → IntentionalFoul (bypass pipeline, log foul, return)
else if (trailing, Q4, under 45 seconds) → MustScore
else → Regular
```

#### Stage 2 — Shot Clock Phase
```
Roll against actionPlayer.ShotClockAggressiveness:
  r < aggressiveness        → Early   (18–24s used)
  r < aggressiveness + 0.55 → Mid     (10–17s used)
  else                      → Late    (1–9s used)
  PotentialGameWinner       → BuzzerBeater (always)
```

#### Stage 3 — Select Action Player
Weight each player in the 5-man lineup by `USG_Weight * (1 - Fatigue * 0.25)`.
- If this is an offensive rebound continuation, give the rebounder `3.5x` weight multiplier.
- Late clock: raise weights to power of 1.4 (highest-usage player dominates more).

Use a weighted random selection helper method.

#### Stage 4 — Turnover Check
```
baseTO = actionPlayer.TurnoverRate
if (phase == Late) baseTO *= 1.3

if (roll < baseTO):
    // Determine stolen vs. dead ball
    totalStealThreat = sum of all defenders' StealMod
    stealShare = totalStealThreat / (totalStealThreat + 0.15)
    if (roll < stealShare):
        stealer = WeightedRandom(defenders, d => d.StealMod)
        → Log TurnoverStolen, attribute stealer, return
    else:
        → Log TurnoverDeadBall, return
```

#### Stage 5 — Shot Type Selection
Weight the 4 shot types using player tendencies + context:

```
Base weights:
  Inside      = actionPlayer.DriveGravity * 100
  Dunk        = actionPlayer.Attr_Dunks * (actionPlayer.Jumping / 100.0)
  MidRange    = actionPlayer.Attr_MidRange * 0.6
  ThreePointer= actionPlayer.Attr_ThreePoint * 0.8

Context modifiers:
  isOffensiveRebound:
    Inside   *= 2.5
    Dunk     *= 2.0
    Three    *= 0.1
  phase == Early:
    Inside   *= 1.4
    Dunk     *= 1.3
    MidRange *= 0.7
  phase == Late:
    Three    *= 1.3
    MidRange *= 1.4
    Dunk     *= 0.3
  position == C or PF:
    MidRange *= 1.2  // post players use mid-range slot for post moves
    Three    *= 0.7  // unless they have high ThreePoint attr
```

#### Stage 6 — Shot Context Selection
Given shot type, roll for context using weighted lists. All weights below are **multipliers** — combine with player attribute values as described.

**Inside shot contexts:**
```
DrivingLayup:       DriveGravity * 2.0
PickAndRollRoll:    DriveGravity * spacingLevel * 0.25
CutLayup:           CutTendency
FastBreakLayup:     phase == Early ? 1.5 : 0.1
PostMove:           position is (PF or C) ? Attr_Inside / 100.0 * 1.5 : 0.1
FloaterLayup:       Speed / 100.0 * Attr_Dribbling / 100.0
```

**Dunk contexts:**
```
AlleyOop:           AlleyOopTendency
CutDunk:            CutTendency * 0.8
TransitionDunk:     phase == Early ? 2.0 : 0.15
PickAndRollDunk:    DriveGravity * 0.5
```

**MidRange contexts:**
```
PullUpMidRange:     DriveGravity * 0.8
IsolationMidRange:  USG_Weight * (1 - DeferralTendency)
PickAndRollMidRange:DriveGravity * 0.5
FadeawayMidRange:   Strength / 100.0 * 0.6
PostMoveMidRange:   position is (PF or C) ? Attr_MidRange / 100.0 * 1.2 : 0.05
LateClockMidRange:  phase == Late ? 2.0 : 0.0
```

**Three contexts:**
```
CatchAndShootCorner: PerimeterGravity * spacingLevel * 0.5
CatchAndShootWing:   PerimeterGravity * 1.2
PullUpThree:         DriveGravity * Attr_ThreePoint / 100.0
StepBackThree:       Attr_Dribbling / 100.0 * Attr_ThreePoint / 100.0
PickAndRollThree:    DriveGravity * 0.6
TransitionThree:     ShotClockAggressiveness * 0.5
```

**Putback/TipIn (only after offensive rebound):**
```
Putback: 0.6
TipIn:   0.4
```

#### Stage 7 — Assist Roll
Every shot context has a base assist probability. If the roll fires, select assister from teammates weighted by `AssistWeight`.

```
Assist probability by context:
  CatchAndShootCorner    → 0.92
  CatchAndShootWing      → 0.88
  AlleyOop               → 1.00
  CutLayup / CutDunk     → 0.90
  PickAndRollRoll        → 0.75
  PickAndRollDunk        → 0.72
  PickAndRollThree       → 0.68
  PickAndRollMidRange    → 0.60
  FastBreakLayup         → 0.55
  TransitionDunk         → 0.50
  TransitionThree        → 0.40
  PostMove               → 0.22
  PostMoveMidRange       → 0.20
  FadeawayMidRange       → 0.12
  IsolationMidRange      → 0.08
  StepBackThree          → 0.10
  PullUpThree            → 0.15
  PullUpMidRange         → 0.18
  DrivingLayup           → 0.30
  FloaterLayup           → 0.22
  LateClockMidRange      → 0.05
  Putback / TipIn        → 0.00
```

#### Stage 8 — Defense Response
Assign a primary defender via `GetMatchup(shooter, defenders)`:
- Match by position first, then find the defender with the highest relevant defense attribute.
- Calculate contest modifier:
  ```
  CatchAndShootCorner/Wing: contestMod = 0.6
  CutLayup/CutDunk/AlleyOop: contestMod = 0.3
  BuzzerBeater: contestMod = 0.5
  All others: contestMod = 1.0
  ```
- `contestPenalty = defender.ContestPenalty(shotType) * contestMod`
- `blockChance = defender.BlockMod * ShotBlockability(shotType) * contestMod`

```
ShotBlockability:
  Inside      → 1.0
  Dunk        → 0.4
  MidRange    → 0.85
  ThreePointer→ 0.25
```

#### Stage 9 — Outcome Roll
```csharp
double baseMake = shotType switch {
    ShotType.Inside      => shooter.InsideMakePct,
    ShotType.Dunk        => shooter.InsideMakePct * 1.08,
    ShotType.MidRange    => shooter.MidRangeMakePct,
    ShotType.ThreePointer=> shooter.ThreeMakePct
};

// PostMove and PostMoveMidRange use the same make% but from the relevant attribute:
// PostMove → InsideMakePct
// PostMoveMidRange → MidRangeMakePct

// Context make% modifier:
baseMake *= ShotContextMakeModifier(context);
// Modifiers:
//   CatchAndShootCorner  → 1.08
//   CatchAndShootWing    → 1.04
//   FastBreakLayup       → 1.10
//   AlleyOop             → 1.12
//   IsolationMidRange    → 0.94
//   StepBackThree        → 0.92
//   LateClockMidRange    → 0.88
//   FadeawayMidRange     → 0.90
//   PostMove             → 0.97
//   BuzzerBeater         → 0.20
//   All others           → 1.0

// Apply fatigue and contest:
double adjMake = baseMake * (1 - shooter.Fatigue * 0.12) - contestPenalty;

// Clutch modifier (applied when gameState.IsClutch):
if (gameState.IsClutch)
    adjMake *= 1.0 + (shooter.Attr_BasketballIQ - 70) / 100.0 * 0.15;

adjMake = Math.Clamp(adjMake, 0.05, 0.95);

double r = _rng.NextDouble();
if (r < blockChance)              → Blocked
else if (r < blockChance + adjMake) → Made
else                               → Missed
```

#### Stage 10 — Free Throw Check (on made driving shot or post move)
If shot was made AND context is `DrivingLayup`, `PickAndRollRoll`, `PostMove`, `PostMoveMidRange`:
```
foulDrawChance = 0.20 * (shooter.Attr_Inside / 100.0)
if roll < foulDrawChance:
    award 1 bonus free throw
    roll shooter.FTMakePct
```

If shot was **missed** AND context is the same set:
```
foulDrawChance = 0.12
if roll < foulDrawChance:
    void the missed shot → award 2 free throws
    roll each at shooter.FTMakePct
```

#### Stage 11 — Secondary Events (Rebound)
On **missed or blocked** shot:
```
// Each player contributes to the rebound race
offRebWeight = sum of offensive lineup's ORebWeight
defRebWeight = sum of defensive lineup's DRebWeight

offRebPct = offRebWeight / (offRebWeight + defRebWeight)
// Clamp between 0.18 and 0.38 (realistic NBA range)
offRebPct = Math.Clamp(offRebPct, 0.18, 0.38)

if (roll < offRebPct && orbCount < 4):
    rebounder = WeightedRandom(offensiveLineup, p => p.ORebWeight)
    orbCount++
    → Log OffensiveRebound, re-enter pipeline at Stage 3 with prevRebounder = rebounder
else:
    rebounder = WeightedRandom(defensiveLineup, p => p.DRebWeight)
    → Log DefensiveRebound, end possession
```

### 6.5 — Stats Tracking

Throughout the pipeline, maintain a `Dictionary<string, PlayerGameStats>` keyed by player name. Update it at each event:

```
ShotMade (Inside/Dunk/MidRange) → scorer: FGMade++, FGAttempts++, Points += 2
ShotMade (Three)                → scorer: FGMade++, FGAttempts++, ThreeMade++, ThreeAttempts++, Points += 3
ShotMissed/Blocked              → shooter: FGAttempts++ (and ThreeAttempts++ if three)
Assisted shot                   → assister: Assists++
Blocked                         → blocker: Blocks++
Stolen                          → stealer: Steals++
TurnoverDeadBall/Stolen         → action player: Turnovers++
OffensiveRebound                → rebounder: OffRebounds++, Rebounds++
DefensiveRebound                → rebounder: DefRebounds++, Rebounds++
FreeThrow made                  → shooter: FTMade++, FTAttempts++, Points++
FreeThrow missed                → shooter: FTAttempts++
```

Return both `List<PossessionResult>` and `Dictionary<string, PlayerGameStats>` from `SimulateGame`. Wrap them in a result object:

```csharp
public record GameResult(
    List<PossessionResult> Possessions,
    Dictionary<string, PlayerGameStats> Stats,
    Team HomeTeam,
    Team AwayTeam,
    int FinalHomeScore,
    int FinalAwayScore
);
```

### 6.6 — Narrative Generation

Create a `NarrativeBuilder` helper class or region. Generate narrative strings by combining player name + shot type + context:

```csharp
// Examples of the pattern — build out all combinations:
(ShotType.Inside, ShotContext.DrivingLayup, made:true)
  → "{scorer} drives — lays it in!"
  → "{scorer} attacks the rim — GOOD!"

(ShotType.Dunk, ShotContext.AlleyOop, made:true)
  → "LOB to {scorer} — THROWS IT DOWN! (Assist: {assister})"

(ShotType.ThreePointer, ShotContext.CatchAndShootCorner, made:true)
  → "{scorer} catch-and-shoot from the corner — BANG! (Assist: {assister})"

(ShotType.ThreePointer, ShotContext.StepBackThree, made:false)
  → "{scorer} step-back three — rattles out"

(ShotType.MidRange, ShotContext.PostMoveMidRange, made:true)
  → "{scorer} with the post move — mid-range GOOD!"

(ShotType.Inside, ShotContext.PostMove, made:true)
  → "{scorer} backs down in the post — lays it in!"

// Turnovers
TurnoverStolen   → "{team} — {actionPlayer} stolen by {stealer}!"
TurnoverDeadBall → "{team} — {actionPlayer} turnover — out of bounds"

// Rebounds
OffensiveRebound → "{rebounder} with the offensive rebound — second chance!"
DefensiveRebound → "{rebounder} with the defensive rebound"
TipIn            → "{scorer} with the TIP-IN!"
Putback          → "{scorer} putback — GOOD!"
```

Build 2–3 variations for each combination and randomly pick between them, same as the existing `Pick()` helper.

---

## Step 7 — GameSim.razor (UI Rewrite)

**Replace** `GameSim.razor` with the following layout. Keep all existing animation/timer/pause logic. Add a box score panel.

### Layout

```
┌─────────────────────────────────────────────────────────┐
│               SCOREBOARD (existing, keep)               │
│   Knicks 87  |  Q3  8:42  |  Thunder 82                │
├─────────────────────────────────────────────────────────┤
│               PLAY-BY-PLAY LOG (existing, keep)         │
│   scrollable, monospace, 280px height                   │
├─────────────────────────────────────────────────────────┤
│               LIVE BOX SCORE (NEW)                      │
│   [KNICKS tab] [THUNDER tab]                            │
│                                                         │
│   Player       MIN  PTS  REB  AST  STL  BLK  TO  FG    │
│   J. Brunson   28   24   3    7    1    0    2   9/18   │
│   OG Anunoby   26   14   6    1    2    1    0   6/11   │
│   ...                                                   │
├─────────────────────────────────────────────────────────┤
│               SPEED SLIDER + CONTROLS (existing, keep)  │
└─────────────────────────────────────────────────────────┘
```

### Box Score Component Requirements

- Two tabs: home team and away team names
- Table columns: `Player | MIN | PTS | REB | AST | STL | BLK | TO | FG | 3PT | FT`
- Sort rows by PTS descending during live play
- Highlight the last player to score (bold or background color for 2 seconds) — use a `_lastScorer` string field and reset via a `Task.Delay(2000)` after each scoring event
- Show team totals row at the bottom: sum all columns
- FG column shows `made/attempts` format
- Table has a max height of 220px with vertical scroll
- Use the team's `PrimaryColor` for the tab header

### Code-behind changes

```csharp
// Replace string-based team params with Team objects
private readonly Team _homeTeam = RosterData.Knicks;
private readonly Team _awayTeam = RosterData.Thunder;

// New fields
private GameResult? _gameResult;
private Dictionary<string, PlayerGameStats> _liveStats = new();
private string _activeBoxScoreTab = "home";
private string? _lastScorer;

// Update RunReplay to also update _liveStats on each possession
foreach (var possession in _gameResult.Possessions)
{
    // existing score/clock/log update...

    // Update live stats
    if (_gameResult.Stats.TryGetValue(possession.Scorer ?? "", out var s))
        _liveStats[possession.Scorer!] = s;
    // etc — rebuild _liveStats from accumulated stats up to current possession index

    // Highlight last scorer
    if (possession.PointsScored > 0 && possession.Scorer != null)
    {
        _lastScorer = possession.Scorer;
        _ = Task.Delay(2000).ContinueWith(_ => {
            _lastScorer = null;
            InvokeAsync(StateHasChanged);
        });
    }
}
```

### Box Score Table Razor

```razor
<div class="mt-3" style="max-width: 700px; margin: 0 auto;">
    <!-- Tab headers -->
    <div style="display: flex; gap: 4px; margin-bottom: 4px;">
        <button style="background: @(_activeBoxScoreTab=="home" ? _homeTeam.PrimaryColor : "#eee"); 
                       color: @(_activeBoxScoreTab=="home" ? "white" : "#333");
                       border: none; padding: 4px 16px; border-radius: 4px 4px 0 0; cursor: pointer;"
                @onclick='() => _activeBoxScoreTab = "home"'>
            @_homeTeam.Abbreviation
        </button>
        <button style="background: @(_activeBoxScoreTab=="away" ? _awayTeam.PrimaryColor : "#eee");
                       color: @(_activeBoxScoreTab=="away" ? "white" : "#333");
                       border: none; padding: 4px 16px; border-radius: 4px 4px 0 0; cursor: pointer;"
                @onclick='() => _activeBoxScoreTab = "away"'>
            @_awayTeam.Abbreviation
        </button>
    </div>

    <!-- Box score table -->
    <div style="max-height: 220px; overflow-y: auto; border: 1px solid #ddd; border-radius: 0 4px 4px 4px;">
        <table style="width: 100%; font-size: 0.78rem; border-collapse: collapse; font-family: monospace;">
            <thead style="position: sticky; top: 0; background: #f5f5f5;">
                <tr>
                    <th style="text-align:left; padding: 4px 8px;">Player</th>
                    <th>PTS</th><th>REB</th><th>AST</th>
                    <th>STL</th><th>BLK</th><th>TO</th>
                    <th>FG</th><th>3PT</th><th>FT</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var stat in GetActiveBoxScore())
                {
                    bool isScorer = stat.Name == _lastScorer;
                    <tr style="background: @(isScorer ? "#fffde7" : "white"); 
                               border-bottom: 1px solid #f0f0f0;
                               font-weight: @(isScorer ? "700" : "400");">
                        <td style="padding: 3px 8px; text-align:left;">@ShortName(stat.Name)</td>
                        <td style="text-align:center;">@stat.Points</td>
                        <td style="text-align:center;">@stat.Rebounds</td>
                        <td style="text-align:center;">@stat.Assists</td>
                        <td style="text-align:center;">@stat.Steals</td>
                        <td style="text-align:center;">@stat.Blocks</td>
                        <td style="text-align:center;">@stat.Turnovers</td>
                        <td style="text-align:center;">@stat.FGDisplay</td>
                        <td style="text-align:center;">@stat.ThreeDisplay</td>
                        <td style="text-align:center;">@stat.FTDisplay</td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
</div>
```

Helper methods in `@code`:
```csharp
IEnumerable<PlayerGameStats> GetActiveBoxScore()
{
    string teamName = _activeBoxScoreTab == "home" ? _homeTeam.Name : _awayTeam.Name;
    return _liveStats.Values
        .Where(s => s.Team == teamName)
        .OrderByDescending(s => s.Points);
}

static string ShortName(string fullName)
{
    var parts = fullName.Split(' ');
    return parts.Length >= 2 ? $"{parts[0][0]}. {parts[^1]}" : fullName;
}
```

---

## Implementation Notes & Pitfalls

1. **Never expose `internal` attributes to Razor.** Any property accessed in `.razor` files must be `public`. Verify no `Attr_*` or `True_*` properties are referenced in the UI layer.

2. **WeightedRandom helper** — implement as a private generic method on `GameEngine`:
   ```csharp
   private T WeightedRandom<T>(IEnumerable<T> items, Func<T, double> weightFn)
   {
       var list = items.ToList();
       double total = list.Sum(weightFn);
       double r = _rng.NextDouble() * total;
       double cumulative = 0;
       foreach (var item in list)
       {
           cumulative += weightFn(item);
           if (r <= cumulative) return item;
       }
       return list.Last();
   }
   ```

3. **Clock tracking** — decrement clock by the possession's consumed time. When clock reaches 0 mid-possession, complete the possession but start the next quarter at 720.

4. **Fatigue** — increment each player's `Fatigue` by `0.004` per possession they are the action player. Fatigue resets to half at halftime (between Q2 and Q3).

5. **orbCount cap** — maximum 3 consecutive offensive rebounds per possession chain to avoid infinite loops.

6. **Stats snapshot for live box score** — the `_liveStats` dictionary should be updated every time `StateHasChanged()` is called in `RunReplay`. Rebuild it from `_gameResult.Stats` up to the current possession index, or maintain a live running copy updated possession by possession.

7. **Free throw possession** — after a fouled missed shot, void the shot attempt from FGAttempts. Only add FTAttempts/FTMade for the free throws.

8. **PostMove context for Inside vs. MidRange** — when context is `PostMove`, use `InsideMakePct`. When context is `PostMoveMidRange`, use `MidRangeMakePct`. Both can occur for PF/C players.

9. **Minimum weight guard** — in all weighted random calls, clamp minimum weight to 0.01 to avoid division by zero or zero-weight pools.

10. **GameSim.razor method signature** — update `StartGame()` and `Replay()` to call `_engine.SimulateGame(_homeTeam, _awayTeam)` which now returns `GameResult` instead of `List<PossessionResult>`.