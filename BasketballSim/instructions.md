Technical Specification: Basketball Simulation Engine Refactor
1. Overview

Goal: Transition from a linear "Attribute-Check" shooting model to a State-Based PPS (Points Per Shot) Threshold model.
Objective: Solve the "Mid-Range Poison Pill" (where improving mid-range attributes hurts team efficiency) by making shot selection a tactical decision based on expected value, defensive pressure, and shot-clock desperation.

Relevant files:
- BasketballSim/Simulation/GameEngine.cs — core simulation engine (refactor target)
- BasketballSim/Models/Player.cs — all player attributes and derived stats
- BasketballSim/Models/PlayerTendencies.cs — per-player tendency ints (0–100 scale)
- BasketballSim/Models/CoachingProfile.cs — coaching profile (InsideMod/MidMod/ThreeMod, OffensiveRating, DefensiveRating, DefStyle, OffStyle)
- BasketballSim/Models/PossessionResult.cs — ShotType, ShotContext enums and result record

2. Core System: The State Machine

The possession context is determined by the result of the previous play.

Previous Play Result       | Possession State  | Initial Menu Weighting           | Shot Clock
---------------------------|-------------------|----------------------------------|----------
Made Basket / Dead Ball    | HALF_COURT        | Balanced/Set Plays               | 24s
Defensive Rebound          | TRANSITION        | Bias: Rim, Transition 3          | 24s
Steal / Live Turnover      | FAST_BREAK        | Heavy Bias: Rim, Open Corner 3   | 24s
Offensive Rebound          | SECOND_CHANCE     | Heavy Bias: Putback, Interior    | 14s

Implementation note: Add a new enum `PossessionState { HalfCourt, Transition, FastBreak, SecondChance }` to
GameEngine.cs. The existing `orbCount`/`prevRebounder` tracking already captures SECOND_CHANCE. Steals
(PossessionEvent.TurnoverStolen) become FAST_BREAK; DRebs (PossessionEvent.DefensiveRebound) become TRANSITION.
The existing `ShotClockPhase` (Early/Mid/Late/BuzzerBeater) remains and is set *within* each state.

3. Physical Attribute Impact Map

Physical attributes (Height, Speed, Jumping, Strength, Endurance) must influence both offense and defense
throughout the engine. These are already in Player.cs as public ints (0–100 scale).

A. OFFENSE

  Driving / Blow-by (DrivingLayup, PickAndRollRoll, FastBreakLayup):
    - Speed (primary): scales DriveGravity = (Speed + Attr_Dribbling + Attr_Inside) / 300.
      Higher Speed → DrivingLayup/FastBreakLayup contexts appear more often; higher PPS floor.
    - Jumping: adds a small PPS bonus for FastBreakLayup and TransitionDunk (better lift = easier finish).
      Suggested: baseMake += (Jumping - 50) / 500.0 on FastBreakLayup context.

  Dunks (AlleyOop, CutDunk, TransitionDunk, PickAndRollDunk, ContactDunk):
    - Jumping (primary gating): DunkTendency = (Attr_Dunks + Jumping + Height) / 300.
      Low Jumping → dunk contexts rarely appear on the menu; threshold naturally not met.
    - Height: also contributes to DunkTendency; taller players can dunk with less jump.
    - Strength: gates ContactDunk specifically. ContactDunkMakePct already uses Strength.
      Additionally lower T for ContactDunk: T_contact × (1 − (Strength − 50) / 150).

  Post Game (PostMove, PostMoveMidRange, FadeawayMidRange):
    - Strength (primary): scales PostMove context weight and lowers T_post.
      PostMove weight × (0.7 + Strength / 333.0). At Strength=95: 0.985×; at 20: 0.76×.
    - Height: a taller player is harder to front/seal; add Height bonus to PostMove base PPS.
      basePPS_post += (Height - 50) / 600.0.
    - Jumping: Fadeaway benefits from jump height (harder to contest).
      FadeawayMidRange blockability += (Jumping - 50) / 400.0 (reduces block probability).

  Floater (FloaterLayup):
    - Speed + Jumping: FloaterLayup weight = Speed / 100 × Attr_Dribbling / 100.
      Jumping bonus: baseMake_floater += (Jumping - 50) / 500.0 (higher arc over rim protectors).

  Offensive Rebounds (Putback, TipIn):
    - Jumping (primary): ORebWeight = Pow((Attr_Rebounding_Off + Jumping) / 200, 1.3).
      Already implemented. Jumping directly controls how often SECOND_CHANCE state is entered.
    - Height: contributes to ORebWeight via Attr_Rebounding_Off (which 2K maps partially from height).
    - Strength: allows player to hold position for box-out → small multiplier on ORebWeight.
      ORebWeight × (0.9 + Strength / 1000.0). Subtle, not dominant.

B. DEFENSE

  Contest (all shot types):
    - Height (primary for interior): taller defenders alter release points on Inside shots.
      ContestPenalty(Inside) += (Height - 50) / 600.0 × EnergyFactor_Physical.
      This is additive on top of the existing Attr_InteriorDefense-based penalty.
    - Height for perimeter: modest impact on MidRange only (not 3PT — height doesn't close out faster).
      ContestPenalty(MidRange) += (Height - 50) / 900.0 × EnergyFactor_Physical.
    - Jumping: increases block probability for high-leaping defenders regardless of height.
      BlockMod bonus: += (Jumping - 50) / 600.0 × EnergyFactor_Physical. Additive on base BlockMod.

  Stopping Drives (DrivingLayup, PullUpMidRange, PickAndRollRoll):
    - Speed (primary): fast defenders can cut off driving lanes.
      When defender Speed > attacker Speed: apply a DriveContest modifier that reduces DrivingLayup PPS.
      DriveContest = 1.0 − Clamp((defSpeed − offSpeed) / 100.0, 0, 0.15).
      This is applied as a multiplier on the base PPS of drive-based contexts.
    - Attr_PerimeterDefense: already drives ContestPenalty; also gates whether driving contexts appear
      at "Open" vs "Contested" quality on the menu.

  Stopping Post (PostMove, PostMoveMidRange, ContactDunk):
    - Strength (primary): physical defenders can wall off post entry and disrupt seals.
      PostContest = 1.0 − Clamp((defStrength − offStrength) / 100.0, 0, 0.18).
      Applied to PostMove and PostMoveMidRange Actual_PPS as a multiplier.
    - Height: taller defenders change post angles. Already partially captured by DRebWeight.
      Add: PostContest height bonus += (defHeight − offHeight) / 800.0 (small, stacks with Strength).
    - Attr_InteriorDefense: primary skill driver already in ContestPenalty.

  Blocks (all Inside + some MidRange):
    - Height + Jumping (both matter): BlockMod = Pow(Attr_InteriorDefense / 100.0, 1.3) × 0.26.
      Add physical bonus: BlockMod += (Height + Jumping − 100) / 1200.0 × EnergyFactor_Physical.
      At both 95: +0.075 bonus. At both 50: +0. At both 5: −0.075 (floored at 0).
    - Jumping addendum: if Jumping > 75, increase TransitionDunk/AlleyOop blockability slightly
      (elite leapers can still get a hand on high-flying dunks).

  Rebounding (defensive):
    - Height (primary): DRebWeight = Pow((Attr_Rebounding_Def + Height) / 200, 1.3). Already correct.
    - Jumping: also contributes via Attr_Rebounding_Def proxy from 2K data.
    - Strength: allows holding position against offensive rebounders. Small multiplier on DRebWeight.
      DRebWeight × (0.9 + Strength / 1000.0). Matches the symmetric offensive version.
    - Position modifiers (new): on Inside misses, C/PF get 1.3× DRebWeight; Guards/Wings 0.8×.
      On ThreePointer misses, Guards/Wings get 1.2× DRebWeight; C/PF get 0.9×.

  Stealing (all turnovers):
    - Speed (primary): StealMod = Pow((Attr_PerimeterDefense + Speed) / 200, 1.3) × 0.07.
      Already implemented. Speed directly reflects anticipation and first-step quickness.
    - Jumping: no impact on steals (not a physical steal attribute).

C. ATTRIBUTE INTERACTION SUMMARY TABLE

Physical Attr | Offensive Impact                              | Defensive Impact
--------------|-----------------------------------------------|------------------------------------------
Height        | DunkTendency, PostMove PPS bonus              | ContestPenalty(Inside/Mid) bonus, BlockMod bonus, DRebWeight
Speed         | DriveGravity, FastBreakLayup PPS, ShotClockAggressiveness | DriveContest modifier, StealMod
Jumping       | DunkTendency, ORebWeight, FastBreakLayup/Floater PPS bonus | BlockMod bonus, DRebWeight (via reb attr)
Strength      | ContactDunkMakePct, PostMove weight/threshold, ORebWeight (minor) | PostContest modifier, DRebWeight (minor)
Endurance     | All attributes degrade via EnergyFactor curves | Same — fatigue affects physical defense harder

4. The Menu Generation (Availability Logic)

Instead of a player "choosing" a shot, the engine generates a Menu of available ShotContexts for the current
ball handler. Each ShotContext maps to a ShotType and a base PPS.

A. Influence Hierarchy

    1. Offensive Style Profile (see Section 5): Hard-coded profile boosts/suppresses which ShotContext
       categories appear and with what frequency. Applied first as a multiplier on context weights.

    2. Coaching Quality (CoachingProfile.OffensiveRating):
       High OffensiveRating elevates corner-3 and cut contexts; low suppresses them.
       Applied as a secondary modifier within each context category.

    3. Passer Attribute (Player.Attr_Passing):
       High Attr_Passing "upgrades" shots to Open/assisted versions (higher PPS contexts:
       CatchAndShootCorner, CatchAndShootWing, CutLayup, AlleyOop).
       Low Attr_Passing forces contested/self-created versions (IsolationMidRange, PullUpThree, StepBackThree).

    4. Physical Modifiers (Section 3): Speed affects drive contexts; Jumping affects dunk/OReb contexts;
       Strength affects post contexts; Height affects interior contests and blocks.

    5. Defensive Suppression:
       Defender's Attr_PerimeterDefense suppresses high-PPS perimeter contexts from the menu.
       Defender's Attr_InteriorDefense suppresses At-Rim / Dunk contexts.
       Speed differential applies DriveContest to driving contexts.
       Strength differential applies PostContest to post contexts.

    6. Defensive Stance (CoachingProfile.DefStyle):
       DefensiveStyle.ProtectThePaint — suppresses At-Rim/Dunk PPS; opens corner-3 PPS.
       DefensiveStyle.StopTheThree   — suppresses perimeter PPS; opens paint PPS.
       DefensiveStyle.Balanced       — moderate suppression everywhere.

       Sag signal (existing teamSag/teamGravity from lineup Attr_ThreePoint averages):
       High teamSag  → increases Open Mid / Open Corner 3 availability; decreases At-Rim.
       High teamGravity → increases At-Rim availability; decreases open perimeter.

5. Offensive Style Profiles (Hard-Coded)

Add `OffensiveStyle` enum to CoachingProfile.cs:
  `public enum OffensiveStyle { PaceAndSpace, Heliocentric, MotionFlow, GritAndGrind, Balanced }`

Each profile is a set of multipliers applied during GenerateMenu() to context category weights.
These represent a coach's system, not individual player tendencies — they shape the menu before
player attributes and tendencies are applied.

── PACE & SPACE ─────────────────────────────────────────────────────────────────────────────────

Philosophy: Mathematical Optimization. Aggressively pursues only the two highest-value shot types
(rim and 3-point). Maximizes possessions to exploit the law of large numbers.

Menu multipliers:
  Rim/Dunk contexts (AlleyOop, CutDunk, FastBreakLayup, TransitionDunk, PickAndRollDunk): × 1.4
  3PT contexts (CatchAndShootCorner, CatchAndShootWing, TransitionThree, PickAndRollThree):  × 1.5
  Mid-range contexts (all Isolation/PullUp/Fadeaway/PostMove mid):                           × 0.4
  Post contexts (PostMove, PostMoveMidRange):                                                 × 0.3
  DrivingLayup / PickAndRollRoll:                                                             × 1.2
  Pace bonus: Team.Pace += 5 baseline (faster possessions increase scoring ceiling variance).

Emergent behaviors:
  STRENGTH — Best-fit roster: Elite 3PT shooters + rim-running bigs + fast guards.
    Corner 3s flood the menu; rim rolls get open looks via gravity. PPS ceiling is the highest
    of any style when shots are falling.
  WEAKNESS — High variance: lives and dies by 3PT RNG.
    Poor transition defense (fast pace leaves D scrambling) → more opponent FAST_BREAK states.
    Fewer SECOND_CHANCE states (no offensive board emphasis → OrebWeight multiplier: × 0.75).
    Mid-range specialists become essentially worthless. Teams with poor 3PT shooting will crater.

── HELIOCENTRIC ─────────────────────────────────────────────────────────────────────────────────

Philosophy: Star Power. Guarantees the highest-USG_Weight player initiates every possession loop.
Minimizes passes to minimize turnover dice rolls.

Menu multipliers:
  Primary ball handler (highest USG_Weight player) is locked in as the initiator for all checks.
  Usage selection: instead of weighted random, primary handler is forced as action player 80% of possessions.
  Iso contexts (IsolationMidRange, StepBackThree, PullUpThree, PullUpMidRange):                × 1.6
  Catch-and-shoot contexts for non-primary players:                                             × 0.5
    (teammates rarely see ball → frozen out, almost never get "Open" upgrades)
  Pass loop: maximum 1 pass per possession before a shot is forced (reduces ball movement).
  Forced shot boost: primary handler's T is reduced by 0.08 (takes harder shots more willingly).

Emergent behaviors:
  STRENGTH — Elite primary ball handler dominates. Star's Attr_MidRange, Attr_ThreePoint, and
    Tendencies.Iso drive everything. Very low turnover rate (fewer pass dice rolls).
    Stable, predictable output — effective against any defense.
  WEAKNESS — Predictable: the primary defender's Matchup_Mod is applied with full weight every
    possession. Elite matchup defenders have outsized impact.
    Teammates "freeze out" → CatchAndShootCorner/Wing rarely appear (devastating to 3&D players).
    Average team TS% becomes anchored entirely to the star's hot/cold variance.
    Very low assist totals; role players lose rhythm and effectiveness.

── MOTION / FLOW ────────────────────────────────────────────────────────────────────────────────

Philosophy: Quality Multiplier. Ball movement constantly upgrades contested shots to open ones.
Entire defense must rotate, creating cracks in coverage.

Menu multipliers:
  Catch-and-shoot contexts (CatchAndShootCorner, CatchAndShootWing):                           × 1.8
  Cut contexts (CutLayup, CutDunk, AlleyOop):                                                  × 1.5
  Iso/self-creation contexts (IsolationMidRange, StepBackThree, PullUpThree):                  × 0.4
  Pass loop: 2–4 passes encouraged per possession before shooting (lower T requires higher PPS).
    Each pass: Attr_Passing of passer upgrades next handler's available contexts by one tier.
  BBIQ noise: stddev of Perceived_PPS noise is halved (motion offense requires reads → IQ matters more).
  Team-wide AssistWeight receives × 1.3 multiplier.

Emergent behaviors:
  STRENGTH — Best PPS per shot when players have high Attr_Passing and Attr_BasketballIQ.
    High passing frequency degrades defender positioning → more CatchAndShootCorner looks.
    Most assists of any style; role players get open shots and develop rhythm.
  WEAKNESS — High risk: every pass is a steal dice roll.
    Low-Attr_BasketballIQ teams will misread (Perceived_PPS noise spikes) and make bad passes.
    Requires roster depth: all 5 players must be willing passers — one low-IQ anchor breaks the chain.
    Slower to develop looks → higher clock consumption per pass cycle.

── GRIT & GRIND ─────────────────────────────────────────────────────────────────────────────────

Philosophy: High Floor. Maximizes physical dominance in the paint. Grinds possessions to minimize
opponent opportunities. Lives in SECOND_CHANCE states.

Menu multipliers:
  Post contexts (PostMove, PostMoveMidRange, ContactDunk, PickAndRollRoll):                    × 1.8
  SECOND_CHANCE emphasis: ORebWeight receives × 1.4 multiplier (aggressive crash).
  Interior contact contexts (DrivingLayup, ContactDunk):                                        × 1.3
  Floater:                                                                                       × 0.6
  3PT contexts (CatchAndShootCorner, CatchAndShootWing, PullUpThree, StepBackThree):           × 0.5
  Pace penalty: Team.Pace −5 baseline (slower tempo reduces total possessions, limits opponent scoring).
  PostContest from Strength is increased × 1.2 (physical teams get more from Strength attribute).

Emergent behaviors:
  STRENGTH — Elite bigs with high Strength, Height, Attr_Inside absolutely dominate.
    Very high floor — effective even against elite defenses because post game is hard to suppress.
    Maximizes SECOND_CHANCE possession states → outsized impact from high Jumping/OReb players.
    Opponent FAST_BREAK chances reduced (slower pace, more controlled resets).
  WEAKNESS — Spacing death: low 3PT output invites opponent DefensiveStyle.ProtectThePaint.
    When opponent sags, DrivingLayup PPS drops due to DriveContest from crowded paint.
    Cannot mount large comebacks (slow pace = fewer possessions when trailing).
    Perimeter players with high Attr_ThreePoint are essentially wasted.

── BALANCED ─────────────────────────────────────────────────────────────────────────────────────

Philosophy: Ultimate Adaptability. No forced weights. The engine naturally exploits whatever the
defense's weakest coverage zone is based on raw player attributes and tendencies.

Menu multipliers:
  All context categories: × 1.0 (no overrides — pure attribute/tendency expression).
  Pass loop: standard (1–2 passes per possession on average).
  T (threshold): standard defaults, no profile adjustment.
  Pace: no modification.

Emergent behaviors:
  STRENGTH — Roster-reflective: what you build is what you get.
    Best style for mixed rosters — won't waste a 3PT shooter by suppressing corner looks, and
    won't waste a post scorer by suppressing interior. Adapts possession-to-possession.
    No exploitable weakness in style itself (opponents cannot "scheme" against a tendency).
  WEAKNESS — No specialist boost. A team of 50-rated players in Balanced will simply average out,
    where a tuned system punches above roster weight.
    Won't proactively hunt the defense's weakest zone — a team with 4 elite shooters in Pace &
    Space actively creates and exploits corner 3s; Balanced waits for them to emerge naturally.
    No identity means no morale/momentum swing mechanic (future feature consideration).

6. The Decision Engine (PPS & Threshold)

Every ShotContext on the menu is assigned an Actual PPS and a Perceived PPS.

A. Actual PPS Formula

  Actual_PPS = (Base_ShotPct × Matchup_Contest × Physical_Modifiers × Coaching_Quality × Clock_Decay) × Point_Value

  - Base_ShotPct: Player's make% for the shot type (Player.InsideMakePct, MidRangeMakePct, ThreeMakePct,
    DunkMakePct, ContactDunkMakePct). Use the existing fatigue-aware properties.
  - Matchup_Contest: 1.0 − Player.ContestPenalty(shotType) for the assigned defender (GetMatchup).
    ContestPenalty now includes both skill (Attr_PerimeterDefense/Attr_InteriorDefense) and physical
    (Height + Jumping additive bonus per Section 3B). defCoachMod scales total penalty.
  - Physical_Modifiers: DriveContest and PostContest from Section 3B. Applied per context category.
  - Coaching_Quality: (OffensiveRating / 100.0); used in context selection (cm variable).
  - Clock_Decay: linear from 1.0 at 24s to 0.7 at 1s. Smooth replacement for discrete phase bonuses.
  - Point_Value: 2 for Inside/MidRange, 3 for ThreePointer.

B. Perceived PPS (The BBIQ Filter)

  Perceived_PPS = Actual_PPS + Gaussian_Noise(mean=0, stddev=f(Attr_BasketballIQ))

  - Attr_BasketballIQ (Player.Attr_BasketballIQ, 0–100): high IQ → small noise (accurate read);
    low IQ → large noise (erratic decisions).
  - Base stddev: 0.10 × (1 − Attr_BasketballIQ / 100.0). At IQ=95: ±0.005; at IQ=30: ±0.07.
  - Motion/Flow profile halves stddev for all players (style demands sharper reads).

C. The Threshold (T)

  - Shot Quality Threshold: minimum Perceived PPS a player will accept (default ≈ 0.85 for 2PT,
    i.e., ~42.5% eFG equivalent; ~0.87 for 3PT due to higher point value).
  - Tendency Modifier: PlayerTendencies fields lower T for favored shot types:
      Tendencies.MidRange (0–100) → T_mid  × (1 − (MidRange − 50) / 200)
      Tendencies.ThreePt  (0–100) → T_3pt  × (1 − (ThreePt  − 50) / 200)
      Tendencies.Drive    (0–100) → T_rim  × (1 − (Drive     − 50) / 200)
      Tendencies.Iso      (0–100) → lowers T for IsolationMidRange / StepBackThree contexts
      Tendencies.PostUp   (0–100) → lowers T for PostMove / PostMoveMidRange contexts
      Tendencies.PullUp   (0–100) → lowers T for PullUpMidRange / PullUpThree contexts
  - Heliocentric profile reduces primary handler T by flat 0.08.
  - Desperation Scaling: T decays exponentially as shot clock approaches 0.
    At <3s remaining, accept whatever has highest Perceived PPS (no floor).

7. The Execution Loop

Run this loop until a shot is taken or time expires (replaces current SimulatePossessionChain inner while).

  1. Select Ball Handler: Weighted random based on:
       USG_Weight × Pow(Tendencies.Usage / 50.0, 1.5) × (0.75 + Energy / 400.0)
     Heliocentric override: primary handler forced 80% of possessions.

  2. Turnover Check:
       Turnover_Chance = actionPlayer.TurnoverRate (fatigue + IQ + Dribbling composite)
       Steal share: totalStealThreat / (totalStealThreat + 0.12)
       where StealMod = Pow((Attr_PerimeterDefense + Speed) / 200.0, 1.3) × 0.07
       Motion/Flow: each pass cycle adds a small steal roll (risk of ball movement).

  3. Generate Menu: Apply Offensive Style Profile multipliers, then Coaching, Attr_Passing, Physical
     Modifiers, and Defensive Stance filters to produce (ShotContext, Actual_PPS) pairs.

  4. Threshold Check:
       If Max(Perceived_PPS across menu) > (T × Desperation_Factor): SHOOT.
       Else: PASS (Motion/Flow: encouraged; Heliocentric: suppressed after 1 pass).

  5. Pass Outcome: Subtract clock: max(0.5, Gaussian(2.5, 0.8)) seconds.
     Re-run loop with a new ball handler. Each pass in Motion/Flow upgrades next handler's context tier.

  6. Assist Attribution: Credit passer via AssistProbability(shotContext) × AssistWeight.

8. Resolution & Transition

A. The Block Check

  Block_Chance = (BlockMod + PhysicalBonus) × blockability × contestMod × blockTendFactor

  - BlockMod = Pow(Attr_InteriorDefense / 100.0, 1.3) × 0.26
  - PhysicalBonus: (Height + Jumping − 100) / 1200.0 × EnergyFactor_Physical (additive, floored at 0)
  - blockability: Inside non-dunk=1.0, dunk contexts=0.35, MidRange=0.85, ThreePointer=0.25.
    Fadeaway/floater: reduce blockability by (defHeight − offHeight) / 800.0.

B. The Rebound (The Next State)

  - Short misses (ShotType.Inside): C/PF get 1.3× DRebWeight; Guards/Wings get 0.8×.
  - Long misses (ShotType.ThreePointer): Guards/Wings get 1.2× DRebWeight; C/PF get 0.9×.
  - Grit & Grind: ORebWeight × 1.4 → more SECOND_CHANCE transitions.
  - Pace & Space: ORebWeight × 0.75 → fewer SECOND_CHANCE transitions (sprinting back on defense).
  - Result: PossessionState → SECOND_CHANCE (offense rebounds) or TRANSITION (defense rebounds).

9. Master Shot Type Registry (ShotContext mapping)

The existing ShotContext enum covers all cases. PPS values are pre-contest, pre-style targets.

ShotContext                | ShotType      | Target PPS | Key Driver Attributes                         | Passer Impact
---------------------------|---------------|------------|-----------------------------------------------|---------------
AlleyOop                   | Inside        | 1.30       | AlleyOopTendency, Jumping, Attr_Dunks         | Massive (1.0)
CutDunk / TransitionDunk   | Inside        | 1.25       | DunkTendency, Speed, Jumping                  | High (0.92)
CutLayup / FastBreakLayup  | Inside        | 1.20       | CutTendency, Speed, Jumping bonus             | Massive (0.92)
PickAndRollDunk            | Inside        | 1.18       | DunkTendency, DriveGravity                    | High (0.78)
PickAndRollRoll            | Inside        | 1.10       | DriveGravity, spacingLevel                    | High (0.80)
ContactDunk                | Inside        | 1.08       | Strength, Attr_Dunks, Jumping                 | Moderate (0.40)
DrivingLayup               | Inside        | 1.05       | DriveGravity, Speed; DriveContest reduces PPS | Moderate (0.58)
Putback / TipIn            | Inside        | 1.00       | ORebWeight, Jumping                           | None (0.00)
PostMove                   | Inside        | 0.96       | Attr_Inside, Strength, Height; PostContest    | Low (0.32)
FloaterLayup               | Inside        | 0.95       | Speed, Attr_Dribbling, Jumping bonus          | Low (0.40)
CatchAndShootCorner        | ThreePointer  | 1.10       | Attr_ThreePoint, PerimGravity                 | Massive (0.95)
CatchAndShootWing          | ThreePointer  | 1.05       | Attr_ThreePoint, PerimGravity                 | High (0.92)
TransitionThree            | ThreePointer  | 1.02       | ShotClockAggressiveness, Speed                | Moderate (0.52)
PickAndRollThree           | ThreePointer  | 0.98       | DriveGravity, Attr_ThreePoint                 | High (0.75)
PullUpThree                | ThreePointer  | 0.90       | DriveGravity, Tendencies.PullUp               | Low (0.22)
StepBackThree              | ThreePointer  | 0.88       | Attr_Dribbling, Tendencies.PullUp             | Very Low (0.13)
PickAndRollMidRange        | MidRange      | 0.92       | DriveGravity, Attr_MidRange                   | Moderate (0.65)
PostMoveMidRange           | MidRange      | 0.90       | Attr_MidRange, Strength, Height               | Low (0.28)
FadeawayMidRange           | MidRange      | 0.86       | Strength, Attr_MidRange; Jumping reduces block| Very Low (0.15)
PullUpMidRange             | MidRange      | 0.86       | DriveGravity, Tendencies.PullUp               | Low (0.28)
IsolationMidRange          | MidRange      | 0.84       | USG_Weight, Tendencies.Iso                    | Very Low (0.10)
LateClockMidRange          | MidRange      | 0.80       | (desperation fallback)                        | Very Low (0.08)

Notes:
- Passer Impact = AssistProbability(context) from existing implementation.
- PPS targets are pre-contest, pre-style. Actual in-game PPS will vary ±15–25% based on matchup/coaching.
- No Heave row: handled as a special case before loop runs (< 1.5s remaining).

10. Validation Scenarios (Unit Tests)

All tests run N=5,000 simulations with all-50-rated rosters unless otherwise specified.
Assertions are on means with ±2 standard errors as tolerance bands.
Target file: BasketballSim.Calibration/Program.cs (add alongside existing RunScenario calls).

── CORE PPS / ATTRIBUTE SCENARIOS ──────────────────────────────────────────────────────────────

Scenario 1: Mid-Range Specialist Fix
  Setup:    One player with Attr_MidRange=95, Attr_ThreePoint=35, Tendencies.MidRange=80. Shot clock = 3s.
  Expected: Player takes IsolationMidRange/PullUpMidRange significantly more than StepBackThree.
            Mid-range FG% > team-average 3PT% in late-clock situations.
  Assert:   Player's MidRangeAttempts / (MidRangeAttempts + ThreeAttempts) > 0.65 at <4s clock.

Scenario 2: Elite Passer Impact
  Setup:    Team A: PG Attr_Passing=90, Attr_BasketballIQ=85. Team B: PG Attr_Passing=40, Attr_BasketballIQ=50.
  Expected: Team A generates CatchAndShootCorner contexts ≥ 2× more often. Team A assist rate > Team B.
  Assert:   Team A PG Assists per game > Team B PG × 1.6. Team A TS% > Team B TS% by ≥2 ppts.

Scenario 3: Lockdown Perimeter Defender
  Setup:    90-rated shooter (Attr_ThreePoint=90) guarded by 95-rated perimeter defender.
  Expected: Elite defender reduces shooter's 3PT% by at least 5ppts vs. average defender (50-rated).
  Assert:   Shooter 3PT% vs. 95-defender ≤ Shooter 3PT% vs. 50-defender − 0.05.

Scenario 4: Interior Physical Dominance
  Setup:    Offense: C with Strength=90, Height=90, Jumping=85, Attr_Inside=85.
            Defense: C with Strength=40, Height=50, Attr_InteriorDefense=50.
  Expected: PostContest and HeightContest bonuses are negligible. Offensive big dominates post.
  Assert:   PostMove PPS ≥ 0.95 effective. Offensive C FG% from post ≥ 55%.

Scenario 5: Shot Clock Panic
  Setup:    Shot clock = 1.5s. All players 50-rated.
  Expected: Desperation_Factor floors T. Ball handler takes highest available context.
  Assert:   Shot attempt rate ≥ 98% of possessions (near-zero violations). LateClockMidRange / StepBackThree
            frequency spikes vs. normal-clock baseline.

Scenario 6: Speed Differential on Drives
  Setup:    Attacker Speed=85 vs. Defender Speed=40 (blowby scenario).
            Attacker Speed=40 vs. Defender Speed=85 (stuffed scenario).
  Expected: Fast attacker: DrivingLayup context PPS > slow attacker by ≥ 0.08.
  Assert:   DrivingLayup FG% when Speed_att > Speed_def by 40+ pts is ≥5ppts higher than reverse.

Scenario 7: Block Leaper vs. Flat-Footer
  Setup:    Defender A: Height=90, Jumping=90, Attr_InteriorDefense=80.
            Defender B: Height=50, Jumping=40, Attr_InteriorDefense=80 (same skill, different athleticism).
  Expected: Defender A blocks significantly more shots despite identical Attr_InteriorDefense.
  Assert:   Defender A Blocks/game > Defender B Blocks/game × 1.4.

── COACHING SCHEME SCENARIOS ───────────────────────────────────────────────────────────────────

All scheme tests use identically-rated all-50 rosters with perfectly matching playstyle attributes
unless noted. Run 10,000 simulations per matchup. Balance goal: no scheme should win by >7 pts/game
vs. another on a perfectly matched all-50 roster. "Well-fitting" roster tests check that the right
team gets a meaningful edge.

Scenario 8: Pace & Space Balance Check
  8a. Neutral roster (all 50): P&S vs. Balanced.
      Assert: Point differential < 5 pts/game either direction.
  8b. Well-fitting roster (Attr_ThreePoint ≥ 75, Jumping ≥ 70 for all players): P&S vs. Balanced.
      Assert: P&S team wins by ≥ 4 pts/game (demonstrating style synergy).
  8c. Misfit roster (Attr_ThreePoint ≤ 30, Attr_Inside ≥ 80): P&S vs. Grit & Grind.
      Assert: Grit & Grind wins by ≥ 5 pts/game (P&S is actively punished for wrong roster).
  8d. P&S transition defense check: measure opponent FAST_BREAK possession% with P&S team on defense.
      Assert: Opponent FAST_BREAK rate ≥ 1.15× baseline (Balanced defensive team's rate).

Scenario 9: Heliocentric Balance Check
  9a. Neutral roster: Heliocentric vs. Balanced.
      Assert: Point differential < 5 pts/game.
  9b. Elite star (Attr_MidRange=90, Tendencies.Usage=80, Tendencies.Iso=75): Helio vs. Balanced.
      Assert: Helio wins ≥ 5 pts/game (star carried by system).
  9c. Lockdown defense on star: opponent assigns best Attr_PerimeterDefense=95 defender to star.
      Assert: Helio team's scoring drops ≥ 6 pts/game vs. same team vs. average defender.
      (Validates "predictability" weakness — elite matchup devastates Helio more than other styles.)
  9d. Teammate efficiency: measure non-primary-handler CatchAndShootCorner attempts in Helio vs. Balanced.
      Assert: Helio non-star players get ≤ 60% as many catch-and-shoot corner looks as Balanced.

Scenario 10: Motion / Flow Balance Check
  10a. Neutral roster: Motion/Flow vs. Balanced.
       Assert: Point differential < 5 pts/game.
  10b. High-IQ/passing roster (Attr_Passing ≥ 75, Attr_BasketballIQ ≥ 75): Motion vs. Balanced.
       Assert: Motion wins ≥ 4 pts/game.
  10c. Low-IQ roster (Attr_BasketballIQ ≤ 35): Motion/Flow vs. Balanced.
       Assert: Motion loses by ≥ 4 pts/game (IQ noise spikes cause bad passes/turnovers).
  10d. Steal pressure: Motion vs. team with all Attr_PerimeterDefense=85, Speed=80.
       Assert: Motion team's turnovers/game ≥ 1.3× Balanced team's turnovers in same matchup.

Scenario 11: Grit & Grind Balance Check
  11a. Neutral roster: Grit & Grind vs. Balanced.
       Assert: Point differential < 5 pts/game.
  11b. Post-dominant roster (Strength ≥ 80, Height ≥ 80, Attr_Inside ≥ 80): Grit vs. Balanced.
       Assert: Grit wins ≥ 5 pts/game.
  11c. Spacing death: Grit & Grind vs. opponent DefensiveStyle.ProtectThePaint.
       Assert: Grit team's Points in Paint drops ≥ 8% vs. same matchup with DefStyle.Balanced.
       (Validates that sagging defense has real bite against interior-heavy teams.)
  11d. SECOND_CHANCE rate: measure Grit possession state distribution.
       Assert: Grit SECOND_CHANCE state% ≥ 1.3× Balanced team's SECOND_CHANCE rate.

Scenario 12: Cross-Style Rock-Paper-Scissors (Balance Matrix)
  Run all 10 matchup pairs of the 5 styles on all-50 rosters:
    P&S vs. Helio, P&S vs. Motion, P&S vs. Grit, P&S vs. Balanced
    Helio vs. Motion, Helio vs. Grit, Helio vs. Balanced
    Motion vs. Grit, Motion vs. Balanced
    Grit vs. Balanced
  Assert for ALL pairs: |mean point differential| < 7 pts/game.
  Soft target: no single style wins every matchup (some non-transitivity is acceptable and realistic).

── PHYSICAL ATTRIBUTE ISOLATION SCENARIOS ─────────────────────────────────────────────────────

Scenario 13: Height Impact on Blocks and Contests
  Setup:    All attributes 50, except vary defender Height: 30, 50, 70, 90.
  Assert:   Block rate increases monotonically with Height.
            ContestPenalty(Inside) increases monotonically with Height.
            Difference between Height=30 and Height=90: ≥ 4 blocks/game per team, ≥ 3ppts inside FG%.

Scenario 14: Strength Impact on Post
  Setup:    Offensive C Strength=90 vs. Defensive C Strength=30 (and vice versa).
  Assert:   Offense-favored Strength matchup: PostMove + PostMoveMidRange FG% ≥ 5ppts higher
            than Strength-neutral (both 50) matchup.

Scenario 15: Speed Impact on Drives and Steals
  Setup:    Vary PG Speed: 30, 50, 70, 90. All other attributes 50.
  Assert:   DrivingLayup + PickAndRollRoll attempts increase monotonically with Speed.
            Steals/game increases monotonically with Speed on defense side.
            Range of steals: Speed=30 team ≤ Speed=90 team − 1.5 steals/game.

Scenario 16: Jumping Impact on Dunks and Rebounds
  Setup:    Vary all-team Jumping: 20, 50, 80.
  Assert:   Dunk context frequency (sum of AlleyOop + CutDunk + TransitionDunk + etc.) increases
            monotonically. ORebounds/game increases monotonically.
            Jumping=80 team: ≥ 3 more dunks/game than Jumping=20 team.

11. Implementation Instructions

Target files (in order):
  1. BasketballSim/Models/CoachingProfile.cs — add OffensiveStyle enum; add OffStyle property to record.
  2. BasketballSim/Models/Player.cs — add physical modifiers: DriveContest, PostContest, BlockPhysicalBonus,
     HeightContestBonus, updated ContestPenalty to include Height/Jumping additive terms.
  3. BasketballSim/Simulation/GameEngine.cs — main refactor:
       a. Add PossessionState enum and tracking.
       b. Implement GenerateMenu(player, possState, offProfile, coach, defLineup) → List<(ShotContext, double pps)>.
       c. Implement ThresholdLoop replacing SimulatePossessionChain's shot-type selection block.
       d. Apply OffensiveStyle profile multipliers inside GenerateMenu.
       e. Replace discrete ShotClockPhase bonuses with smooth Clock_Decay.
       f. Add position-aware rebound modifiers in HandleRebound.
       g. Apply PhysicalBonus to BlockMod; apply DriveContest/PostContest per context.
  4. BasketballSim.Calibration/Program.cs — add all scenarios from Section 10.

Preserve all calibrated constants: FT rates (42% inside, 9% three, 6% mid), foul rates, steal share
formula (stealThreat / (stealThreat + 0.12)), OReb clamp [0.09, 0.35], energy drain/recover values.

Do NOT change: energy system, foul tracking, rotation/substitution logic, quarter/OT structure,
intentional foul handline, box score stat accumulation.

Add an optional checkbox in the game sim that shows every event in the sim. Every time a player 
touches a ball, the decision, and if i click on it a table of all the weights of the shot chances,
expected points, player percieved expected points, and threshold show up.