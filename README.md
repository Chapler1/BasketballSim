# BasketballSim — Full Technical Walkthrough

A possession-by-possession NBA simulator built with Blazor (.NET 10, Interactive Server). Every game plays out one possession at a time, with individual player attributes, tendencies, fatigue, injuries, and coaching style driving every decision. Statistics are calibrated to **real NBA averages**.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Player Attributes](#2-player-attributes)
3. [Player Tendencies](#3-player-tendencies)
4. [Coaching System](#4-coaching-system)
5. [Game Engine — Top Level](#5-game-engine--top-level)
6. [Rotation & Substitution System](#6-rotation--substitution-system)
7. [Fatigue System](#7-fatigue-system)
8. [Possession Chain](#8-possession-chain)
9. [Shot Generation (GenerateMenu)](#9-shot-generation-generatemenu)
10. [Tendency Budget Pool (Normalized)](#10-tendency-budget-pool-normalized)
11. [Shot Threshold & Selection](#11-shot-threshold--selection)
12. [Shot Resolution](#12-shot-resolution)
13. [Shooting Formulas](#13-shooting-formulas)
14. [Contest Penalty](#14-contest-penalty)
15. [Defensive Focus System](#15-defensive-focus-system)
16. [Turnovers, Steals & Deflections](#16-turnovers-steals--deflections)
17. [Rebounds](#17-rebounds)
18. [Fouls & Free Throws](#18-fouls--free-throws)
19. [Assists](#19-assists)
20. [Special Possession States](#20-special-possession-states)
21. [Injury System](#21-injury-system)
22. [Season Simulation](#22-season-simulation)
23. [Schedule Generation](#23-schedule-generation)
24. [Playoffs & Awards](#24-playoffs--awards)
25. [Stat Tracking & Advanced Metrics](#25-stat-tracking--advanced-metrics)
26. [Data Pipeline (ESPN + NBA 2K)](#26-data-pipeline-espn--nba-2k)
27. [Calibration](#27-calibration)

---

## 1. Architecture Overview

| Layer | File(s) | Role |
|-------|---------|------|
| **Pages** | `Pages/SeasonSim.razor`, `Pages/SimLab.razor` | UI — LiveSeason ticker, stats tables, player cards, playoff bracket |
| **Simulation** | `Simulation/GameEngine.cs`, `Simulation/RotationManager.cs` | Core game loop; one possession at a time |
| **Models** | `Models/Player.cs`, `Models/Team.cs`, `Models/Coach.cs`, `Models/Injury.cs` | Data types with derived properties |
| **Services** | `Services/SeasonScheduleService.cs` | Schedule generator, `SimulateDay`, season-level aggregation |
| **Services** | `Services/NbaRosterService.cs`, `Services/EspnTeamFactory.cs` | ESPN roster fetch → `Team`/`Player` objects |
| **Services** | `Services/Nba2kCacheService.cs` | NBA 2K rating → sim attribute mapping |
| **Calibration** | `BasketballSim.Calibration/Program.cs` | Standalone runner — simulates full season and compares to NBA targets |

### Rendering

Blazor Interactive Server — all simulation logic runs server-side; the browser receives rendered HTML via SignalR. No WebAssembly. Components opt in with `@rendermode InteractiveServer`.

### Rating Scale

`5` = worst in NBA · `50` = league average · `95` = best in NBA

Every formula is centered on 50 = average so the league-average player produces average results by construction. There are no hidden global scalars.

---

## 2. Player Attributes

Each player has **19 attributes** on the 5–95 scale. They are sourced from NBA 2K ratings (fetched via `Nba2kCacheService`) and remapped through `AttributeMapper.MapFromSource()`.

### Attribute Groups

#### Physical (publicly visible to GM)
| Attribute | Drives |
|-----------|--------|
| **Height** | ContestPenalty (inside/mid), DRebWeight, BlockMod, dunk gating |
| **Strength** | ContactDunkMakePct, ScreenAbility |
| **Speed** | DriveGravity, CutTendency, AlleyOopTendency, ShotClockAggressiveness |
| **Jumping** | DunkTendency, AlleyOopTendency, ORebWeight, BlockMod |
| **Endurance** | Fatigue drain rate and recovery rate |

#### Shooting (hidden)
| Attribute | Drives |
|-----------|--------|
| **Inside** | InsideMakePct |
| **Dunks** | DunkMakePct, ContactDunkMakePct, DunkTendency |
| **Free Throw** | FTMakePct |
| **Mid-Range** | MidRangeMakePct |
| **Three-Point** | ThreeMakePct, PerimeterGravity |

#### Skill (hidden)
| Attribute | Drives |
|-----------|--------|
| **Offensive IQ (oBBIQ)** | Shot thresholds, ShotClockAggressiveness, TurnoverRate, ScreenAbility |
| **Defensive IQ (dBBIQ)** | HelpDefenseAmount scaling in CoachFactory |
| **Hustle** | LooseBallWeight, BlockMod modifier, StealMod modifier |
| **Dribbling** | TurnoverRate, DriveGravity, Touches tendency weight |
| **Passing** | PassTurnoverRate, assist tracking scale |
| **Off Rebounding** | ORebWeight |
| **Def Rebounding** | DRebWeight |

#### Defense (hidden)
| Attribute | Drives |
|-----------|--------|
| **Perimeter Defense** | ContestPenalty (mid, 3PT), StealMod |
| **Interior Defense** | ContestPenalty (inside), BlockMod, off-ball block base |
| **Foul Tendency** | Pre-shot foul rate (5 = very clean, 95 = very foul-prone) |

### Permanent Injury Penalties

Each player has a `PermanentInjuryPenalties` dictionary that permanently reduces specific attributes (e.g. `"Speed" → -3`). All derived properties use `EffAttr(key, raw)` which subtracts accumulated career damage before computing, floored at 5.

---

## 3. Player Tendencies

Tendencies are derived from raw NBA 2K attributes through `Nba2kCacheService.DeriveTendencies()`. They use piecewise-linear normalization: `50 = league average`, range `5–95`.

### Derived tendency formula
```
T(raw, min, median, max):
  t = raw >= median
    ? (raw - median) / (max - median)
    : -(median - raw) / (median - min)
  return Clamp(Round(50 + Clamp(t, -1, 1) × 45), 5, 95)
```

### Tendency Definitions

| Tendency | Proxy inputs | Role |
|----------|-------------|------|
| **Touches** | Dribbling×0.28 + oBBIQ×0.22 + MidRange×0.14 + ThreePoint×0.10 + Inside×0.08 + Passing×0.08 + Speed×0.07 + Strength×0.03 | Primary ball-handler selection weight |
| **Drive** | DrivingLayup×0.30 + DrivingDunk×0.20 + SpeedWithBall×0.30 + Agility×0.20 | Budget pool: drive shot appearances |
| **Cut** | Agility×0.50 + Speed×0.30 + Hustle×0.20 | Budget pool: off-ball cut appearances |
| **PostUp** | (PostControl + PostHook + PostFade) / 3 | Budget pool: post shot appearances |
| **Iso** | MidRange×0.45 + BallHandle×0.35 + ShotIQ×0.20 | Budget pool: isolation appearances |
| **ThreePt** | ThreePointShot (raw 2K) | Threshold tendency: willingness to pull trigger on 3s |
| **MidRange** | MidRangeShot (raw 2K) | Threshold tendency: willingness to shoot mid-range |
| **PullUp** | (MidRangeShot + BallHandle) / 2 | Threshold tendency: willingness to pull up off the dribble |
| **OffReb** | OffensiveRebound (raw 2K) | Offensive rebound chase probability |
| **Steal** | Steal (raw 2K) | Steal attempt weight |
| **Block** | Block (raw 2K) | Block attempt weight |

---

## 4. Coaching System

Each team has a `Coach` with properties that modify game-engine behavior.

### Offensive Style enum
```
PaceAndSpace   — fast, spread floor, 3PT heavy
MotionOffense  — ball movement, catch-and-shoot emphasis
PickAndRoll    — PnR-heavy, guard/big combos
IsoHeavy       — star isolation, fewer passes
Balanced       — neutral
GritAndGrind   — slow, physical, mid-range/post
Heliocentric   — one ball-dominant star with spacers
```

### How style affects simulation

| Property | Effect |
|----------|--------|
| `PacePref` (0–100) | Blended 35% into team pace: `pace = team.Pace×0.65 + coach.PacePref×0.35` |
| `HelpDefenseAmount` (38–78) | Scales the defensive focus helpFactor; higher = more attention on star threats |
| Dribble isoMult (per style) | Heliocentric=1.4, IsoHeavy=1.2, GritGrind=1.1, Balanced=1.0, PnR=0.9, PaceSpace=0.8, MotionFlow=0.7 |

The `CoachFactory` assigns coaching profiles to all 30 real NBA teams based on their known playstyle.

---

## 5. Game Engine — Top Level

`GameEngine.SimulateGame(homeTeam, awayTeam, startingFatigue?, dnpPlayers?)` is the entry point.

### Initialization
1. Create `PlayerGameStats` for every rostered player.
2. Load `startingFatigue` — each player's `Energy` set to their pre-game fatigue value (clamped 20–100). If no fatigue dict passed, start at 100.
3. `InitRotation` → build starting lineups, compute target-minutes budgets.
4. Zero out foul counts.

### Game structure
- **4 quarters × 720 seconds** each.
- Possessions per quarter = `(homePace + awayPace) / 2 / 4 × 1.09` (rounded).
- Teams alternate possessions; home team takes odd-numbered possessions in Q1/Q3.
- **Overtime**: 5-minute periods until a team is ahead at the end. Max 5 OT periods.

### Quarter-break energy recovery
| Break | Recovery |
|-------|----------|
| Q1 → Q2 | +12 energy |
| Halftime (Q2 → Q3) | +25 energy |
| Q3 → Q4 | +12 energy |
| Each OT break | +8 energy |

Recovery scales with Endurance: `recovMod = 0.70 + Endurance/250`

### Per-possession loop
For each possession:
1. `CheckSubstitutions` for both teams.
2. `ComputeDefensiveFocus` — recompute `focusMult` for each offensive player against the current 5-man defensive lineup.
3. `SimulatePossessionChain` — runs the full possession.
4. Record `PlusMinus` delta for every player on both lineups.
5. Update minutes played for both lineups.

---

## 6. Rotation & Substitution System

### Target minutes

`RotationManager.ComputeTargetMinutes(team)` assigns a per-player target minutes budget for the 48-minute game. DNP players (injured/rested) are excluded upfront; their minutes are redistributed.

Players are ranked by `ComputeOverall` (a weighted sum of all attributes). The top ~8 players split the available minutes roughly proportionally to their overall, capped per role.

### Substitution trigger

`CheckSubstitutions` fires every possession. A player is subbed out when:
- `gameMin[player] ≥ targetMin[player] + overBy`  
- A qualified sub (has remaining target minutes, not already on court, rest period elapsed) is available.

The subbed-out player becomes ineligible for re-entry for `Clamp(stintLength × 0.6, 3, 6)` minutes.

### Starting lineup

`BuildStartingLineup` picks the top-5 players by overall from the eligible roster. The rest are bench.

---

## 7. Fatigue System

### In-game energy drain

Every possession, all 10 players drain energy:

```
drainAmount = (offense ? 0.66 : 0.54) × enduranceMod
enduranceMod = 1.0 + (50 - Endurance) / 250
```

- Offense drains more than defense.  
- At Endurance=95: mod=0.82 (drains 18% slower than avg).  
- At Endurance=5: mod=1.18 (drains 18% faster).  
- At Endurance=50: mod=1.0 exactly.

### Fatigue curves

Energy affects three capability areas through a **quadratic penalty curve**:

```
FatigueCurve(maxPenalty) = 1 - (1 - Energy/100)² × maxPenalty
```

| Factor | maxPenalty | At Energy=0 |
|--------|-----------|-------------|
| `EnergyFactor_Physical` | 0.55 | 45% of normal |
| `EnergyFactor_Shooting` | 0.40 | 60% of normal |
| `EnergyFactor_Mental` | 0.20 | 80% of normal |

The quadratic shape means the last 30 energy points hurt far more than the first 70. A player at 80 energy is barely affected; at 30 energy they are significantly degraded.

### Cross-game fatigue

Between games, the season scheduler tracks each player's fatigue. Recovery per rest day:

```
newFatigue = Clamp(ApplyRecovery(priorFatigue, restDays, Endurance), 0, 100)
```

`ApplyRecovery` applies a per-day recovery rate that scales with Endurance. High-endurance players (Ben Wallace types) recover almost fully on a single rest day; low-endurance players accumulate fatigue over a road trip.

---

## 8. Possession Chain

`SimulatePossessionChain` runs in a loop — it doesn't exit until the possession ends (made shot, turnover, or dead-ball).

### Loop iteration (one "action")

1. **Drain energy** — all 10 players lose energy.
2. **Injury check** — `CheckInjuries` rolls for in-game injuries on both lineups.
3. **Record possession** — on the first action, log the active lineups for tracking stats.
4. **Determine possession context** — Heave / PotentialGameWinner / IntentionalFoul / Regular.
5. **Select ball-handler** — `WeightedRandom(lineup, p => p.Tendencies.Touches / 50.0)`.
6. **Transition / fast-break gates** — if `possessionState == Transition` or `FastBreak`, transition-specific shots are enabled.
7. **Generate shot menu** — `GenerateMenu(ballHandler, lineup, defLineup, state, possState)`.
8. **Attempt shot, pass, or dribble** — pick from menu or fall back.
9. **Handle outcome** — made shot, miss, turnover, foul, offensive rebound, etc.

### Ball-handler selection

The primary ball-handler is selected by `WeightedRandom` weighted on `Tendencies.Touches / 50.0`. A player with Touches=80 is selected 80/50 = 1.6× as often as an average-Touches player. The deterministic 60% formula ensures star guards handle the ball in ~60% of possessions:

```
p(star) = othersSum × 1.5 / (othersSum × 1.5 + othersSum) ≈ 0.60
```

---

## 9. Shot Generation (GenerateMenu)

`GenerateMenu` builds a list of `MenuEntry` objects — one per available shot context for the current ball-handler. Each entry has an `ActualPPS` (expected points per shot) and a `PerceivedPPS` (what the player "sees").

### Shot context categories

#### Inside
| Context | Base prob | Notes |
|---------|-----------|-------|
| DrivingLayup | 0.085 | × DriveGravity × DefSuppression × DriveContestMult |
| FloaterLayup | 0.030 | × DriveGravity modifier |
| AlleyOop | 0.006 | × DunkTend × AlleyOopTend |
| CutDunk | 0.011 | × DunkTend × CutTendency |
| CutLayup | 0.018 | × CutTendency |
| PostMove | 0.045 | × PostProxy |
| PickAndRollRoll | 0.060 | × PnR modifier |
| PickAndRollDunk | 0.022 | × DunkTend |
| TransitionDunk | — | Transition/FastBreak only |
| ContactDunk | — | Drive contact scenario |
| PutbackLayup | — | Second chance only |
| TipIn | — | Second chance only |

#### Mid-Range
| Context | Base prob | Notes |
|---------|-----------|-------|
| IsolationMidRange | 0.040 | × IsoProxy |
| FadeawayMidRange | 0.018 | × FadeProxy |
| PullUpMidRange | 0.030 | |
| PickAndRollMidRange | 0.026 | × PnR modifier |
| PostMoveMidRange | 0.022 | × PostProxy |
| LateClockMidRange | — | <5s on shot clock |

#### Three-Point
| Context | Base prob | Notes |
|---------|-----------|-------|
| CatchAndShootCorner | 0.050 | × ThreePoint/50 |
| CatchAndShootWing | 0.056 | × ThreePoint/50 |
| PullUpThree | 0.030 | × ThreePoint/50 |
| StepBackThree | 0.018 | × ThreePoint/50 |
| PickAndRollThree | 0.042 | × ThreePoint/50 |
| TransitionThree | 0.042 / 0.018 | FastBreak / normal transition |

### Actual PPS

```
ActualPPS = makePct × pointValue - missedPenalty
```

Where `pointValue` is 1 for inside (or 1.5 for dunks resolved as and-1 opportunities), 2 for mid, 3 for 3PT — accounting for foul draw probabilities.

### Perceived PPS (with compression)

The engine applies **PPS compression** so that no single shot completely dominates:

```
perceivedPPS = 0.95 + (actualPPS - 0.95) × 0.52
```

Reference point 0.95, compression factor 0.52. This reduces the distance between a 1.35 3PT and a 1.05 layup so the player doesn't always take the same shot.

---

## 10. Tendency Budget Pool (Normalized)

Appearance tendencies (Drive, Cut, PostUp, Iso) control *which* shot opportunities appear in the menu. They operate as a **normalized budget pool** — tendencies redistribute shot type distribution without changing total shot volume.

### Normalization math

```csharp
// Step 1: compute base game-state probabilities WITHOUT tendency weights
double gsDrive = Σ gameStateProb for Drive contexts   // DrivingLayup, FloaterLayup, PnR contexts
double gsCut   = Σ gameStateProb for Cut contexts     // AlleyOop, CutDunk, CutLayup
double gsPost  = Σ gameStateProb for Post contexts    // PostMove, PostMoveMidRange
double gsIso   = Σ gameStateProb for Iso contexts     // IsolationMidRange, FadeawayMidRange
double baseTotal = gsDrive + gsCut + gsPost + gsIso

// Step 2: tendency weights (50 → 1.0; floor at 0.1 so pool never zeroes)
double wDrive = Max(0.1, player.Tendencies.Drive  / 50.0)
double wCut   = Max(0.1, player.Tendencies.Cut    / 50.0)
double wPost  = Max(0.1, player.Tendencies.PostUp / 50.0)
double wIso   = Max(0.1, player.Tendencies.Iso    / 50.0)

// Step 3: normalization factor
double weightedTotal = gsDrive×wDrive + gsCut×wCut + gsPost×wPost + gsIso×wIso
double normFactor    = weightedTotal > 0 ? baseTotal / weightedTotal : 1.0

// Step 4: each pooled context probability = gameStateProb × tendencyWeight × normFactor
```

**Concrete example — Mitchell Robinson** (Cut≈85, Post≈70, Drive≈30, Iso≈20):
- wCut = 1.70, wPost = 1.40, wDrive = 0.60, wIso = 0.40
- normFactor keeps total probability identical to a 50/50/50/50 neutral player
- Result: ~40% of his menu is cut shots, ~33% post, ~16% drives, ~11% iso

**Shots outside the pool** (CaS, PullUp, StepBack, TransitionShots, FastBreak, LateClockMid) are unaffected by the normalized budget — they respond to game state and threshold tendencies only.

---

## 11. Shot Threshold & Selection

### How thresholds work

Each `MenuEntry` has a `ContextThreshold` — the minimum `PerceivedPPS` needed for the ball-handler to "pull the trigger." If a shot's `PerceivedPPS < threshold`, the player passes or dribbles instead.

### Base threshold by clock

| Shot clock remaining | Threshold |
|---------------------|-----------|
| > 20 seconds | 1.32 |
| > 15 seconds | 1.09 |
| > 10 seconds | 0.87 |
| > 5 seconds | 0.60 |
| > 2 seconds | 0.35 |
| ≤ 2 seconds | 0.00 (always shoot) |

The clock is always started at 24 seconds for half-court possessions. An `earlyClockGate` ramps up from 0 (at clock=24) to 1.0 (at clock≤21) — shots are suppressed on the very first tick of each possession to model play-development time.

```
earlyClockGate = Clamp((24 - shotClockRemaining) / 3.0, 0, 1)
```

### Threshold tendencies (selection modifiers)

These lower the threshold (make a player *more* willing to shoot), applied in `ComputeContextThreshold`:

| Tendency | Applies to | Formula |
|----------|-----------|---------|
| **ThreePt** | CaS Corner/Wing, PnR3, Transition3 | `1.0 - (ThreePt - 50) / 400.0` |
| **MidRange** | IsoMid, FadeawayMid, PnRMid | `1.0 - (MidRange - 50) / 400.0` |
| **PullUp** | PullUpMid, PullUpThree, StepBack3 | `1.0 - (PullUp - 50) / 400.0` |

A tendency of 95 lowers the threshold by `45/400 = 0.1125`. A tendency of 5 raises it by `0.1125` — the player almost never attempts these shots.

### Defensive focus at selection

The `focusMult` (see §15) is applied here too: `threshold × focusMult`. Stars under heavy defensive focus have a higher threshold — fewer shots look good enough to take. This is clamped at ≥ 1.0 (defense never makes shots *easier* to take).

### Menu selection

Once the menu is built, the engine picks `MaxBy(PerceivedPPS)` from only the entries that clear their threshold. If multiple shots tie (rare after compression), the highest actual PPS wins.

If no shot clears the threshold:
- If `passesThisPossession > 0`: attempt dribble action.
- Otherwise: pass to a teammate.

---

## 12. Shot Resolution

After a shot context is selected, the engine resolves it in order:

### Step 1 — Pre-shot foul check

Before the shot is taken, a foul can be committed by the defender:

```
insideFoulChance = 0.005 + ContestPenalty(Inside) × 0.030 × focusMult
midFoulChance    = 0.001 + ContestPenalty(Mid)    × 0.012 × focusMult
```

If a pre-shot foul occurs, the shooter goes to the free-throw line (2 or 3 shots).

### Step 2 — Shot attempt

```
adjustedMake = baseMakePct - ContestPenalty(shotType) × adjustedContest
adjustedMake × (1.0 - (focusMult - 1.0) × 0.30)      // defensive focus make% reduction
```

The contest penalty is scaled by how closely the defender contests (based on their defensive attributes and remaining energy).

### Step 3 — Block check

For missed shots:

**On-ball block** (defender directly matched up):
```
Inside:  (IntDef/100)^2.0 × 0.200 + (Height + Jumping - 100) / 2000
Mid:     above × 0.082
3PT:     above × 0.037
```

**Off-ball block** (any other defender in the lane):
```
Inside:  (IntDef/100)^2.3 × 0.028 + (Height + Jumping - 100) / 2200
Mid:     above × 0.010
3PT:     above × 0.004
```

Block weight also includes: `× (1 + (Hustle - 50) / 100 × 0.08)`.

If blocked, the ball is loose — rebound battle ensues.

### Step 4 — And-1 / foul on made shot

For made inside shots:
```
and1Chance = 0.18 × contactProb × focusMult × FoulTendency/50
```

For made 3PT:
```
and1Chance = 0.09 × ... (rare)
```

### Step 5 — Dunk resolution

If a shot context is flagged `IsDunk`:
- `DunkMakePct` replaces `InsideMakePct`
- Contact dunks use `ContactDunkMakePct`
- Dunk attempts at low energy or with active injury can downgrade to a layup attempt

---

## 13. Shooting Formulas

All formulas apply `EnergyFactor_Shooting × InjuryFactor_Shooting` multiplicatively.

### Field Goal Make%

| Shot Type | Formula | At attr=50 |
|-----------|---------|-----------|
| **Inside** | `0.59 + (Inside/100) × 0.22` | 70.0% |
| **Mid-Range** | `0.43 + (MidRange/100) × 0.17` | 51.5% |
| **Three-Point** | `0.315 + (ThreePoint/100) × 0.20` | 41.5% |
| **Free Throw** | `Clamp(0.36 + 0.88x - 0.24x², 0.22, 0.97)` where x = FreeThrow/100 | 75.8% |
| **Dunk** | `Clamp(0.75 + (Dunks+Jumping)/200 × 0.20, 0.75, 0.95)` | ~83% |
| **Contact Dunk** | `0.65 + (Dunks+Jumping+Strength)/300 × 0.20` | ~72% |

### Attribute range effect

| Shot Type | At attr=5 | At attr=50 | At attr=95 |
|-----------|----------|-----------|-----------|
| Inside | 59.1% | 70.0% | 79.9% |
| Mid-Range | 43.9% | 51.5% | 59.2% |
| Three-Point | 32.5% | 41.5% | 50.5% |

### Free Throw curve

The FT formula is a quadratic — accuracy improves faster at the high end:
- At FT=5 (attr): ~36%
- At FT=50: ~75.8%
- At FT=95: ~95.6%

FT uses `EnergyFactor_Mental (maxPenalty=0.20)` instead of Shooting — it's muscle memory, less affected by physical fatigue.

---

## 14. Contest Penalty

The contest penalty is subtracted from the base make% after the shot is resolved. It comes from the **defender**, not the shooter.

```csharp
// Inside
penalty = Max(0, 0.25 + (IntDef - 50)/100 × 0.85) + (Height - 50)/900
         × EnergyFactor_Physical × InjuryFactor_Physical

// Mid-Range
penalty = Max(0, 0.046 + (IntDef×0.4 + PerimDef×0.6 - 50)/100 × 0.13) + (Height - 50)/1000
         × EnergyFactor_Physical × InjuryFactor_Physical

// Three-Point
penalty = Max(0, 0.09 + (PerimDef - 50)/100 × 0.34)
         × EnergyFactor_Physical × InjuryFactor_Physical
```

**Contest penalty range examples (Inside)**:
| Defender IntDef | Height | Penalty |
|----------------|--------|---------|
| 20 (poor) | 50 | 0.0% (floored) |
| 50 (average) | 50 | 25.0% |
| 80 (good) | 70 | 33.5% |
| 95 (elite) | 85 | 39.5% |

A raw `InsideMakePct` of 70% against an elite interior defender drops to ~30% — making paint shots genuinely contested.

---

## 15. Defensive Focus System

The defensive focus system concentrates defensive attention on the biggest threats in the current lineup. It prevents star players from scoring unrealistic totals (pre-system: 34–38 PPG for top stars vs. real 27–30).

### Focus computation

Computed fresh every possession against the current **5-man offensive lineup**:

```csharp
// Step 1: threat score per player (avg of 5 key offensive attributes)
double threatScore = (ThreePoint + MidRange + Inside + Dribbling + oBBIQ) / 5.0

// Step 2: normalize across the 5 players on court
double avgThreat = lineup.Average(threatScore)
double normThreat = threatScore / avgThreat     // >1 = above lineup avg

// Step 3: apply coach's help defense tendency
double helpFactor = coach.HelpDefenseAmount / 50.0  // 38-78 → 0.76-1.56
double focusRatio = Max(0.25, 1 + (normThreat - 1) × helpFactor)
double focusMult  = Pow(focusRatio, 0.90)
```

**Why this design matters:**
- LeBron with four bench players → normThreat >> 1 → focusMult ≈ 1.45 (everyone is keying on him)
- LeBron with four other All-Stars → normThreat ≈ 1.05 → focusMult ≈ 1.05 (focus spread across threats)

### Three application sites

| Site | Effect | Notes |
|------|--------|-------|
| Menu generation | `contextThreshold × focusMult` | Clamped ≥ 1.0 — defense never makes shots easier |
| Shot execution | `contestPenalty × focusMult` | Harder physical contest on focused players |
| Make% | `adjMake × (1 - (focusMult - 1) × 0.30)` | Multiplicative — proportional reduction, doesn't disproportionately hurt 3PT |

### Jensen's inequality effect

Because `Σ(focusMult) ≤ n` (normalized to the lineup), over-focusing one player mathematically helps others. Doubling focus on a star means teammates get `focusMult < 1.0` — a small bonus. This creates a realistic dead-weight-loss tradeoff.

### Coach HelpDefenseAmount

Set per team in `CoachFactory`. Range: 38–78.
- 38 = very permissive (e.g. pace-heavy teams)
- 78 = extremely focused help defense

---

## 16. Turnovers, Steals & Deflections

### Ball-handler turnovers

Each time the ball-handler holds the ball, a turnover is rolled:

```
turnoverRate = (0.045 - (oBBIQ + Dribbling)/200 × 0.025) / EnergyFactor_Mental
```

At attr=50: ~3.2% per action. At Dribbling=5, oBBIQ=5: ~4.5%. At elite ball-handlers: ~2%.

### Pass turnovers

Each pass has its own turnover check:

```
passTurnoverRate = 0.010 + (95 - Passing)/90 × 0.022
```

At Passing=95: 1.0%. At Passing=5: 3.2%.

The **first half-court pass** of a possession gets a 0.25× reduction (realistic — first pass is almost always safe).

### Deflections

Every pass has a 3.3% deflection chance (the ball tips but stays in play, burning clock). First-touch half-court pass also gets 0.25× reduction here.

A deflected pass becomes a steal if a defender wins the scramble: `stealThreat / (stealThreat + 0.12)`.

### Steal probability

```
StealMod = ((PerimDef + Speed) / 200)^1.8 × 0.09 × EnergyFactor_Physical × InjuryFactor_Physical
           × (1 + (Hustle - 50) / 100 × 0.08)
```

The stealer is selected by `WeightedRandom(defLineup, p => p.StealMod)`.

---

## 17. Rebounds

After any missed field goal attempt, a rebound battle is held.

### Offensive rebound rate

```
OReb% = Clamp(offReb / (offReb + defReb × 4.0), 0.09, 0.30)
```

The 4.0× weight on defensive rebounding reflects that defenders are already positioned in the paint. This produces realistic NBA OReb% of ~23%.

### Individual rebound selection

Offensive rebounder: `WeightedRandom(offLineup, p => p.ORebWeight)`  
Defensive rebounder: `WeightedRandom(defLineup, p => p.DRebWeight)`

```
ORebWeight = ((OffRebounding + Jumping) / 200)^1.3 × EnergyFactor_Physical × InjuryFactor_Physical × InjuryFactor_Jump
DRebWeight = ((DefRebounding + Height) / 200)^1.3 × EnergyFactor_Physical × InjuryFactor_Physical
```

### Putback / tip-in

After an offensive rebound, there is a small chance the rebounder immediately scores:
- `PutbackLayup` if the rebounder is near the basket
- `TipIn` if they tipped it without securing

These second-chance shots have special `PossessionState.SecondChance` context.

---

## 18. Fouls & Free Throws

### Foul tracking

Fouls are tracked both per-player and per-team per-quarter:
- 6 personal fouls → player is fouled out
- 5 team fouls per quarter → bonus free throws for all subsequent fouls

### Pre-shot fouls (before the shot)

```
Inside: 0.005 + ContestPenalty(Inside) × 0.030 × focusMult
Mid:    0.001 + ContestPenalty(Mid)    × 0.012 × focusMult
```

These produce 2-shot (or 3-shot) free throw situations.

### Foul on made field goal (and-1)

```
and1Chance = 0.18 × contactProb × focusMult × (FoulTendency/50)
```

Only rolls when the shot is made. Produces one additional free throw attempt.

### Missed 3PT fouls

```
3PT foul chance = 0.09 × contestActivity × focusMult
```

Three free throws if fouled on a 3PT attempt.

### Free throw resolution

```
FTMakePct = Clamp(0.36 + 0.88x - 0.24x², 0.22, 0.97) × EnergyFactor_Mental × InjuryFactor_Shooting
```

where `x = FreeThrow / 100`. Each attempt is an independent Bernoulli trial.

### Intentional fouls (late-game)

When a team is down 1–6 with under 48 seconds in Q4, the engine switches to `IntentionalFoul` context: the trailing team immediately fouls, and the leading team's highest-Touches player shoots two free throws. 3 seconds of clock is consumed.

---

## 19. Assists

### Per-context assist probability

Each shot context has a hardcoded assist probability reflecting how often that shot type is assisted in real basketball:

| Context | Assist prob |
|---------|------------|
| AlleyOop | 0.98 |
| CutDunk | 0.82 |
| CutLayup | 0.79 |
| PickAndRollDunk | 0.78 |
| CatchAndShootCorner | 0.77 |
| PickAndRollThree | 0.75 |
| CatchAndShootWing | 0.72 |
| PickAndRollMidRange | 0.66 |
| PickAndRollRoll | 0.63 |
| FastBreakLayup | 0.52 |
| TransitionDunk | 0.44 |
| TransitionThree | 0.42 |
| DrivingLayup | 0.30 |
| FloaterLayup | 0.26 |
| PostMove | 0.21 |
| IsolationMidRange | 0.08 |
| PullUpMidRange | 0.07 |
| FadeawayMidRange | 0.05 |
| PutbackLayup / TipIn | 0.00 |

### Assister selection

When a context determines the shot was assisted, the passer is chosen from the lineup:

**Organic assist** (no tracked pass):
```
organicBase = 0.18 + 0.68 × (bestPassing / 50.0)^1.5
```

**Tracked assist** (when an actual pass sequence occurred):
```
passWeight = Clamp(1.0 + (passer.Passing - 50) / 50.0 × 0.30, 0.70, 1.30)
```

The recent pass chain is checked first — if a specific pass led to the shot, the passer gets the assist at `passWeight × contextAssistProb`.

---

## 20. Special Possession States

### Fast Break

Triggered when the defense transitions from offense to defense with fewer than 4 defenders back. Fast break possessions have:
- Higher transition shot probabilities (FastBreakLayup, TransitionDunk)
- Reduced defense (lower contest penalties)
- Shot clock reset to 14 seconds

### Transition (normal)

A softer transition — 4+ defenders back but caught mid-setup. TransitionThree is available at a higher rate than half-court.

### Second Chance

After an offensive rebound, the possession enters `SecondChance` state. Only inside shots and short mid-range shots are available. PutbackLayup and TipIn contexts are added to the menu.

### Shot clock scenarios

| Scenario | Clock starts at |
|----------|----------------|
| Half-court possession | 24 seconds |
| Fast break | 14 seconds |
| After offensive rebound | 14 seconds |
| End-of-quarter heave | Actual remaining seconds |

### End-of-period heaves

If `clockSeconds ≤ 2`:
- 2 seconds exactly: 25% chance it's a real shot (half-court range), 75% it's a full heave
- Full heave: `make% = ThreeMakePct × 0.18`, clamped to 1–8%
- Real shot: `(ThreeMakePct or MidMakePct) × 0.55 - contestPenalty`, clamped to 3–40%

---

## 21. Injury System

### In-game injury checks

Every possession, `CheckInjuries` rolls for each player on both lineups:

```
injuryRoll < baseRate × (1.0 - bodyPartRating/100) × contactMult
```

Where `bodyPartRating` is the player's current health in the at-risk body part (1–99, starts at ~70 for most parts). Contact-heavy plays (inside shots, drives, screens) have higher contact multipliers.

### Injury grades

| Grade | Label | Effect |
|-------|-------|--------|
| **1** | Questionable | Player may continue; `ShouldPlayThroughG1` determines if they play |
| **2** | Out | Immediate DNP for the remainder of the game and future games until recovered |
| **3** | Severe | Extended absence; highest risk of permanent debuffs |

### Play-through (Grade 1)

```
p(plays) = basePlayThrough × (1 - severity × 0.3) × (1 - rosterRank/15)
```

Stars (low roster rank = high overall) are more likely to play through minor injuries. Grade 2+ is always DNP.

### Recovery

`InjuryService.TickRecovery` is called once per rest day between games. `DaysRemaining` decrements each tick. When it hits 0, the injury clears and the player returns.

### Permanent debuffs

When a Grade 2+ injury resolves, `RollPermanentDebuff` fires:
- Rolls a debuff probability based on injury grade and body part
- If triggered, reduces `PermanentInjuryPenalties[attrKey]` by 1–5 points
- These accumulate across the career — a player who re-injures the same knee eventually loses permanent Speed or Jumping

### Body part rating degradation

After each injury, the damaged body part's rating is reduced:
```
InjuryRatings[bodyPartKey] = Max(1, currentRating - RatingDegradation(grade))
```

This makes previously-injured body parts more susceptible to re-injury — realistically modeling chronic injury history.

---

## 22. Season Simulation

`SeasonScheduleService` manages the full season lifecycle.

### LiveSeasonState

The central object holding all mutable season state:
- `Schedule` — all 1,230 `ScheduledGame` entries
- `GameDates` — deduplicated sorted list of game days
- `DateIndex` — current position in the calendar
- `Teams` — all 30 team objects (modified in place by injuries/fatigue)
- `Standings` — `TeamSeasonStats` per team (W/L, pts for/against, etc.)
- `PlayerAgg` — `PlayerSeasonStats` per player, keyed `"Name|Team"`
- `CompletedGames` — `SeasonGameRecord` list for the ticker and game log
- `Fatigue` — per-player energy level carried between games
- `Endurance` — per-player cached endurance rating (for recovery math)
- `PendingInj` / `FinalizedInj` — injury tracking across the season
- `FollowedTeam` — the team the user is tracking (gates ticker filtering)

### SimulateDay

`SimulateDay` processes all games on the current calendar date then advances `DateIndex`:

1. Get today's games from `Schedule` where `Date.Date == GameDates[DateIndex]`
2. For each game: call `SimulateOneLiveGame(s, scheduledGame)`
3. Increment `DateIndex`
4. If season complete: finalize injury history, compute `SeasonResult`

### SimulateOneLiveGame

For each game:
1. **Fatigue recovery**: each player recovers based on rest days since last game
2. **Injury tick**: each player's `CurrentInjury.DaysRemaining` decrements; cleared injuries are finalized
3. **DNP determination**: Grade 2+ → always DNP; Grade 1 → `ShouldPlayThroughG1` check
4. **Game engine**: `s.Engine.SimulateGame(homeTeam, awayTeam, fatigue, dnpPlayers)`
5. **Standings update**: W/L, conf/div splits, pts for/against
6. **Player aggregation**: all box-score stats rolled into `PlayerAgg` per-player season totals
7. **Post-game injury processing**: new in-game injuries get `PendingInj` entries and body-part degradation
8. **Fatigue drain**: each player who played loses fatigue proportional to minutes × Endurance

### Mode comparison

| Mode | Entry point | Speed |
|------|------------|-------|
| **Instant Sim** | `SimulateSeason(teams)` | ~2-3 seconds for 1,230 games |
| **Live Season** | `StartLiveSeason()` → `SimulateDay()` | One day at a time (user-controlled) |
| **Auto-Sim** | `ToggleAutoSim()` | Simulates one day every 500ms; pause at any time |
| **Sim Rest of Season** | `SimulateRestOfSeason()` | Completes all remaining days instantly in background thread |

---

## 23. Schedule Generation

### Algorithm (circulant)

`GenerateSchedule` creates the 1,230-game schedule using a **circulant round-robin** that guarantees:
- Exactly 82 games per team
- Exactly 41 home + 41 away per team
- Division rivals play more frequently (matching NBA format)

### NBA Calendar mapping

`NbaCalendarService.GenerateSchedule` takes the abstract matchup list and assigns real calendar dates:
- Season starts October 22
- Season ends mid-April
- Games distributed across ~180 days
- Rest days are pre-calculated per game (`HomeRestDays`, `AwayRestDays`) and fed directly into fatigue recovery

### Schedule density

Back-to-back games (1 rest day) are distributed realistically — every team has roughly 14–18 back-to-backs over the 82-game season, matching the actual NBA schedule density.

---

## 24. Playoffs & Awards

### Play-In Tournament

Seeds 7–10 in each conference play a mini-tournament:
- 7 vs 8: winner gets 7th seed; loser plays again
- 9 vs 10: loser is eliminated
- Loser of (7v8) vs Winner of (9v10): winner gets 8th seed

### Bracket

16 teams (8 per conference), single-elimination series format (best-of-7 for all rounds):
- First Round: 1v8, 2v7, 3v6, 4v5 per conference
- Conference Semifinals: winners bracket
- Conference Finals: top two survivors per conference
- NBA Finals: East vs West champion

Home court: higher seed hosts games 1, 2, 5, 7.

### Season Awards

Awards are computed from regular-season `PlayerSeasonStats` and `TeamSeasonStats`:

**MVP** — weighted scoring formula:
```
score = PTS×2.0 + AST×1.5 + (OReb+DReb)×1.0 + teamWins×0.8
       + FGPct×50 + PerimDef×0.3 + oBBIQ×0.2 - TOV×1.5
```

**DPOY** — defensive formula:
```
score = STL×4.0 + BLK×3.5 + DefRating×0.3 + (PerimDef+IntDef)×0.25
       - PTS×0.05 (reduces pure scorers)
```

**Sixth Man of the Year** — highest-scoring player with fewer than 60% of games started.

**All-NBA / All-Defensive Teams** — position-balanced selection (2 guards, 2 forwards, 1 center per team) by weighted performance score.

---

## 25. Stat Tracking & Advanced Metrics

### Per-game box score (`PlayerGameStats`)

| Stat | How tracked |
|------|-------------|
| PTS, FGM, FGA, 3PM, 3PA, FTM, FTA | Direct counters per possession |
| ORB, DRB | Rebound resolution |
| AST | Context assist probability × passer tracking |
| STL, BLK | Steal/block resolution outcomes |
| TOV | Turnover resolution |
| MIN | Seconds elapsed per possession × lineup membership |
| +/- | Score delta during each possession while on court |
| InsideMade/Att, MidMade/Att | Shot type counters |
| TouchesTotal | Counted each time player receives a pass or handles the ball |
| TeamTouchesOnCourt, TeamFGAOnCourt | Denominator for USG% and Touch% |
| PossessionsOnCourt | For ON/OFF rating calculation |
| TeamPtsOnCourt, OppPtsOnCourt | For NET rating |

### Advanced metrics (computed in `SeasonResult` / `PlayerSeasonStats`)

| Metric | Formula |
|--------|---------|
| **TS%** | `PTS / (2 × (FGA + 0.44 × FTA))` |
| **eFG%** | `(FGM + 0.5 × 3PM) / FGA` |
| **USG%** | `(FGA + 0.44×FTA + TOV) / TeamPossessionsOnCourt × team's usage share` |
| **AST%** | `AST / TeamFGMOnCourt` |
| **OREB%** | `OReb / TeamORebOnCourt` |
| **DREB%** | `DReb / TeamDRebOnCourt` |
| **OFF RTG** | `TeamPtsOnCourt / PossessionsOnCourt × 100` |
| **DEF RTG** | `OppPtsOnCourt / PossessionsOnCourt × 100` |
| **NET RTG** | `OFF RTG - DEF RTG` |
| **ShotShare%** | `FGA / TeamFGAOnCourt` |
| **Touch%** | `TouchesTotal / TeamTouchesOnCourt` |

### Pace

```
Pace = (TotalPossessions / TotalGames) × 48 / (TotalPossessionSeconds / TotalPossessions / 60)
```

Target: **~103 possessions per team per game**.

---

## 26. Data Pipeline (ESPN + NBA 2K)

### ESPN roster fetch

`EspnTeamFactory` fetches the current NBA roster from the ESPN API for all 30 teams. Each player record includes name, position, age, jersey number, and height.

`NbaRosterService` wraps this into `Team` and `Player` objects, assigning positions as PG/SG/SF/PF/C.

### NBA 2K attribute mapping

`Nba2kCacheService` loads `Data/nba2k_cache.json` — a cached export of NBA 2K player ratings.

Raw 2K attributes (e.g. `drivingLayup`, `threePointShot`, `helpDefenseIQ`) are composited into the 19 sim attributes:

| Sim Attribute | 2K Composite |
|---------------|-------------|
| Speed | avg(speed, speedWithBall, agility) |
| Inside | avg(closeShot, drivingLayup, postHook, postFade, postControl) |
| Passing | avg(passAccuracy, passVision, passIQ) |
| oBBIQ | (shotIQ×2 + passIQ + offensiveConsistency) / 4 |
| dBBIQ | (helpDefenseIQ×2 + defensiveConsistency) / 3 |
| PerimDef | avg(perimeterDefense, steal) |
| IntDef | avg(interiorDefense, block) |

The composite values are then mapped from the 2K scale (typically 25–99) to the sim scale (5–95) via `AttributeMapper.MapFromSource`, centering the median player at 50.

### Height mapping

```
heightRating = Clamp((inches - 70) × 5 + 5, 5, 100)
```

- 5'10" (70 inches) → 5
- 6'7" (79 inches) → 50 (league average center)
- 7'5" (89 inches) → 100

### Team assignment

`EspnRosterSyncService` runs at startup and calls `Nba2kCacheService.SetEspnTeams(assignments)` to patch team assignments from live ESPN data over the cached 2K data.

---

## 27. Calibration

### Target (NBA 2023-24)

| Stat | Target | Sim (latest) |
|------|--------|-------------|
| PTS | 114.6 | 112.9 ✅ |
| FGA | 88.7 | 89.0 ✅ |
| FGM | 42.9 | 42.5 ✅ |
| FG% | 48.4% | 47.7% ✅ |
| 3PA | 35.1 | 32.5 ✅ |
| 3PM | 13.1 | 12.1 ✅ |
| 3P% | 37.3% | 37.3% ✅ |
| 2PA | 53.6 | 56.5 ⚠ |
| 2PM | 29.8 | 30.4 ✅ |
| 2P% | 55.6% | 53.8% ⚠ structural |
| FTA | 21.5 | 21.0 ✅ |
| FTM | 16.7 | 15.8 ✅ |
| FT% | 77.5% | 75.6% ⚠ structural |
| ORB | 9.5 | 8.6 ✅ |
| DRB | 32.6 | 34.7 ⚠ structural |
| AST | 26.5 | 28.3 ✅ |
| STL | 7.4 | 7.6 ✅ |
| BLK | 4.9 | 4.8 ✅ |
| TOV | 13.9 | 14.6 ✅ |

**Pace**: 103.4 possessions / team / game ✅  
**Avg possession length**: 13.8 seconds ✅  
**Passes / possession**: 2.8 ✅

### Known structural gaps

- **2P% and FT%** are slightly low: further ContestPenalty reduction cascades into faster pace. These are accepted as structural limitations until a more granular shot-quality decomposition is built.
- **DRB high**: The sim assigns every missed shot to a rebound. Real NBA has ~5% of missed shots go out of bounds (not modeled). This explains the DRB surplus.

### Running calibration

```bash
cd BasketballSim.Calibration
dotnet run
```

Outputs `calibration_out.txt` and `season_results.json`. The calibration run uses **real NBA rosters** via `Nba2kCacheService.LoadFromPath` — not synthetic all-50 players — so the calibration reflects actual league talent distribution.

### Calibration philosophy

Every constant in the engine is derived from a mechanical argument — no "multiply by 0.87 to fix the number." If a stat is off, the investigation starts with *why* (shot quality too high? rotation pattern wrong? foul rate miscalibrated?) before any constant is changed. This keeps the sim internally consistent.

---

## Running the App

```bash
# HTTP
dotnet run --project BasketballSim/BasketballSim.csproj --launch-profile http
# HTTPS
dotnet run --project BasketballSim/BasketballSim.csproj --launch-profile https
# Watch (hot reload)
dotnet watch --project BasketballSim/BasketballSim.csproj
```

- HTTP: http://localhost:5094  
- HTTPS: https://localhost:7232

---

*Calibrated to NBA 2023-24 | Built with Blazor .NET 10 | Possession-by-possession simulation*
