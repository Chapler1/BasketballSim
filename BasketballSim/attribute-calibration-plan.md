# Attribute Calibration Plan
## Anchoring the 0–100 Scale to Real NBA Data (2014–2024)

---

## Approach

Every attribute uses a **5–95 working range**. The idea:

- **Attr 95** = the best realistic single-season performance from the last 10 seasons (roughly top 5 in the league at that skill, sustained over a season)
- **Attr 5** = the worst realistic single-season performance for a player who still saw meaningful minutes
- **Attr 100** is reserved for all-time GOATs (Shaq inside, Curry three-ball, etc.) and can exist but shouldn't be assigned to current rosters
- **Attr 50** should land close to the NBA-wide average for players at that position who are league-qualified

The simulation's output stats should match real NBA averages when rosters are populated with realistic attributes. **The validation test:** build a 10-player roster of attrs all set to 50 and run 10,000 games through SimLab. The resulting box scores should resemble a typical NBA game.

---

## Data Sources

| Source | Use |
|--------|-----|
| [Basketball Reference](https://www.basketball-reference.com) | FT%, 3P%, TRB%, AST%, TOV%, STL%, BLK% — use "Per Game" or "Advanced" leaderboards |
| [NBA.com/stats — Shooting](https://www.nba.com/stats/players/shooting) | At-rim FG% and mid-range FG% by zone (filter "Restricted Area", "In The Paint", "Mid-Range") |
| [NBA.com/stats — Tracking](https://www.nba.com/stats/players/speed-distance) | Speed, drives, rebounding % |
| [PBP Stats](https://www.pbpstats.com) | Granular shot context data, contested FG%, possessions |
| [Cleaning the Glass](https://cleaningtheglass.com) | Shot quality–adjusted percentages, rebounding rates by position |

**Key filters to apply:**
- Seasons: 2014–15 through 2023–24 (10 seasons)
- Minimum: 500 minutes played per season (to avoid noise from short-stint players)
- Pull **individual season lines**, not career averages (a player may have one great season at 95 level even if their career is 75)

---

## Part 1 — Shooting Attributes

These map directly to make percentages in the engine. The formula currently used is `base + (attr / 100) * multiplier`.

### 3-Point % (`Attr_ThreePoint → ThreeMakePct`)

**Research target:** Filter BBRef leaders by 3P% for 2014–2024, min 3.0 attempts per game per season.

| Attr | Target 3P% | NBA Anchor | Notes |
|------|-----------|-----------|-------|
| 95 | 44% | Stephen Curry (2020-21: 45.4%, career best years 43–45%) | |
| 75 | 39% | JJ Redick, Joe Harris peak years (~40%) | |
| 50 | 36% | League average for qualified shooters | |
| 25 | 32% | Below-average rotation shooter | |
| 5 | 25% | LeBron James worst 3PT seasons, Giannis (~27%), Patrick Beverley | Players who attempt but are a liability |

**Current formula:** `0.28 + (attr / 100) * 0.17`
- Attr 5 → 28.9% ← **too high** (floor should be 25%)
- Attr 50 → 36.5% ← OK
- Attr 95 → 44.2% ← OK

**Target formula:** `0.233 + (attr / 100) * 0.218`
- Attr 5 → 24.4% ← matches bad-but-real shooter
- Attr 50 → 36.2% ← league average
- Attr 95 → 44.0% ← Curry level

**Data to pull:** BBRef 3P% leaders and bottom-dwellers by season, filter min 3 3PA/G.

---

### Mid-Range FG% (`Attr_MidRange → MidRangeMakePct`)

**Research target:** NBA.com Shot Dashboard → filter "Mid-Range" zone, min 2.0 mid-range FGA per game.

| Attr | Target Mid% | NBA Anchor |
|------|------------|-----------|
| 95 | 52% | DeMar DeRozan (51–54%), Kevin Durant (52–54% mid-range) |
| 75 | 45% | Kawhi Leonard, Khris Middleton |
| 50 | 40% | League average mid-range |
| 25 | 35% | Average PG pulling up off the dribble |
| 5 | 29% | Non-shooter forced into a mid-range pull-up |

**Current formula:** `0.30 + (attr / 100) * 0.22`
- Attr 5 → 31.1% ← slightly high
- Attr 50 → 41.0% ← close
- Attr 95 → 50.9% ← close

**Target formula:** `0.278 + (attr / 100) * 0.253`
- Attr 5 → 29.1%
- Attr 50 → 40.4%
- Attr 95 → 51.8%

**Data to pull:** NBA.com Shot Dashboard, Mid-Range zone, sort by FG%, last 10 seasons. Find the top performers (95) and the miserable souls forced into mid-range pull-ups (5).

---

### At-Rim / Inside FG% (`Attr_Inside → InsideMakePct`)

**Research target:** NBA.com Shot Dashboard → "Restricted Area" zone, min 3.0 FGA per game.

| Attr | Target Rim% | NBA Anchor |
|------|------------|-----------|
| 95 | 77% | Rudy Gobert (76–78%), Clint Capela (72–76%), DeAndre Jordan (70–74%) |
| 75 | 68% | Elite wing finisher, Anthony Davis rim shots |
| 50 | 62% | League average at rim for rotation players |
| 25 | 54% | Below-average finisher |
| 5 | 44% | Poor finisher who still gets to the rim (early career Ben Simmons layup troubles, 46-48%) |

**Current formula:** `0.45 + (attr / 100) * 0.27`
- Attr 5 → 46.4% ← close but slightly high
- Attr 50 → 58.5% ← **too low** (league avg is ~62%)
- Attr 95 → 70.7% ← **too low** (Gobert hits ~77%)

**Target formula:** `0.422 + (attr / 100) * 0.378`
- Attr 5 → 44.1%
- Attr 50 → 61.1%
- Attr 95 → 78.0%

**Data to pull:** NBA.com Restricted Area FG% leaders 2014–2024. Also look at restricted area FG% for known poor finishers.

---

### Free Throw % (`Attr_FreeThrow → FTMakePct`)

**Research target:** BBRef FT% leaders, min 2.0 FTA per game per season.

| Attr | Target FT% | NBA Anchor |
|------|-----------|-----------|
| 95 | 93% | Kyle Korver (93%), JJ Redick (89–93%), Stephen Curry (90–91%), Jose Calderón (98% career but pre-2014) |
| 75 | 84% | Good FT shooting role player |
| 50 | 74% | League average (~77%, so attr 50 is just below avg) |
| 25 | 61% | Below-average big who's not a disaster |
| 5 | 41% | DeAndre Jordan (39–45%), Andre Drummond (38–51%), Dwight Howard (49–57%) |

**Current formula:** `0.55 + (attr / 100) * 0.40`
- Attr 5 → 57.0% ← **too high** (DeAndre Jordan shoots 40–45%)
- Attr 50 → 75.0% ← close
- Attr 95 → 93.0% ← correct

**Target formula:** `0.386 + (attr / 100) * 0.572`
- Attr 5 → 41.5% ← DeAndre Jordan territory
- Attr 50 → 67.2% ← below-average big (so attr 55–60 hits league avg, which makes sense)
- Attr 95 → 92.9% ← elite FT shooter

**Important note:** The floor matters a lot here because the engine awards free throws on fouls, and teams intentionally foul bad FT shooters. The Jordan rules effect should be visible in simulations when attr is 5 vs 95.

**Data to pull:** BBRef FT% season leaders AND worst performers (min 2 FTA/G). Note: many elite players have 85–92% FT%. True 95-level players are rare.

---

### Dunks (`Attr_Dunks`)

This attribute **does not affect a make percentage** — it affects **shot type selection** (how likely a player attempts a dunk vs. a layup). The engine uses `Attr_Dunks * (Jumping / 100)` as the dunk weight.

| Attr | Effect in Sim |
|------|--------------|
| 95 | Very high dunk attempt rate — Zion Williamson, Giannis, DeAndre Jordan profile |
| 50 | Balanced dunk/layup mix for an athletic forward |
| 5 | Almost never dunks — small guard or poor leaper |

**Calibration approach:** Count dunks per 100 inside shot attempts for known players. Zion dunks on ~40–50% of his rim attempts. An average PG might dunk on 5–10%. Use these as 95/5 anchors.

**Data to pull:** NBA.com tracking → Dunk makes + attempts per game. Divide by total at-rim FGA.

---

## Part 2 — Skill Attributes

These drive rates (assist rate, turnover rate, etc.) rather than make percentages.

### Turnover Rate (`Attr_BasketballIQ + Attr_Dribbling → TurnoverRate`)

The engine formula: `0.18 - ((IQ + Drib) / 200) * 0.12`

This is the probability a player turns the ball over on any possession where they are the action player.

**Research target:** BBRef Advanced — TOV% column (turnovers per 100 possessions used), min 500 min.

| IQ + Drib (each) | Target TOV% | NBA Anchor |
|-----------------|------------|-----------|
| 95 / 95 | 6% | Chris Paul (6–9%), Kyle Lowry (7–8%), Steph Curry (7%) |
| 60 / 60 | 12% | Solid rotation player |
| 50 / 50 | 13% | League average (~11–13%) |
| 25 / 25 | 17% | Sloppy with the ball |
| 5 / 5 | 20% | Russell Westbrook peak chaos years (17–19%), Ben Simmons drove high TOV despite not shooting |

**Current formula output:**
- IQ 95 / Drib 95 → 6.6% ← close
- IQ 50 / Drib 50 → 12.0% ← good
- IQ 5 / Drib 5 → 17.4% ← **slightly low** (should be ~20%)

**Target formula:** `0.208 - ((IQ + Drib) / 200) * 0.158`
- 95/95 → 5.8%
- 50/50 → 12.7%
- 5/5 → 20.1%

**Data to pull:** BBRef Advanced stats, TOV% column. Sort ascending for low-TO (95 anchor) and descending for high-TO (5 anchor).

---

### Assist Weight (`Attr_Passing + Attr_BasketballIQ → AssistWeight`)

AssistWeight = `(Passing + IQ) / 200`, ranging from 0.05 to 0.95.

This is used in the engine to select who throws the assist (weighted random), NOT how many assists the player generates. The assist probability is set by shot context (alley-oops always assisted, isos rarely).

**Research target:** BBRef Advanced — AST% (percentage of team FG a player assists while on the floor).

| Passing + IQ (each) | Expected AST% flavor | NBA Anchor |
|--------------------|---------------------|-----------|
| 95 / 95 | Primary playmaker, nearly always involved in assists | Nikola Jokić (38–44%), LeBron James (38%), Chris Paul (42%) |
| 60 / 60 | Secondary playmaker or high-IQ cutter | |
| 50 / 50 | Average — contributes to team flow | ~15% AST% |
| 25 / 25 | Catch-and-shoot, limited playmaking | |
| 5 / 5 | Pure scorer who doesn't create for others | Devin Booker early career (~5%), Bradley Beal isolationist style |

**Note:** This attribute pair doesn't produce a clean single-stat output since it's weighted probability. Validate by checking if playmakers (high passing) get credited with assists at a realistic rate in long simulations (should be ~6–10 assists per game for elite passers).

**Data to pull:** BBRef Advanced AST% column, sorted high and low.

---

## Part 3 — Rebounding Attributes

### Offensive Rebounding (`Attr_Rebounding_Off + Jumping → ORebWeight`)

ORebWeight = `(RebOff + Jumping) / 200`, range 0.05 to 0.95.

The team OREB% is clamped in the engine to 18–38%. Individual credit goes to a player weighted by ORebWeight.

**Research target:** BBRef Advanced — ORB% (percentage of available offensive rebounds grabbed while on the floor).

| RebOff + Jumping (each) | Target ORB% | NBA Anchor |
|------------------------|------------|-----------|
| 95 / 95 | ~18–22% | Andre Drummond (19.9%), Moses Brown, Hassan Whiteside |
| 60 / 60 | ~10% | Active big who crashes the glass |
| 50 / 50 | ~7% | Average — league avg for centers is ~10%, for forwards ~6%, for guards ~2% |
| 25 / 25 | ~3% | Wing who occasionally sneaks in |
| 5 / 5 | ~1% | Guard who never crashes (Harden, Lillard type) |

**Important:** Jumping matters here as much as the skill. A 95 Jumping / 5 RebOff player (athletic but not a natural rebounder) should land around 8–10% OREB, not 18%.

**Data to pull:** BBRef Advanced ORB% — sort high for elite offensive rebounders and filter for guards at the low end.

---

### Defensive Rebounding (`Attr_Rebounding_Def + Height → DRebWeight`)

DRebWeight = `(RebDef + Height) / 200`, range 0.05 to 0.95.

**Research target:** BBRef Advanced — DRB% column.

| RebDef + Height (each) | Target DRB% | NBA Anchor |
|-----------------------|------------|-----------|
| 95 / 95 | ~28–33% | DeAndre Jordan (32%), Andre Drummond (29%), Rudy Gobert (27%) |
| 60 / 60 | ~16% | Solid defensive rebounder |
| 50 / 50 | ~13% | League average center |
| 25 / 25 | ~8% | Small forward who competes |
| 5 / 5 | ~4% | Point guard — Steph Curry (~5%), Kyle Lowry |

**Note:** Height is fully visible (physical attribute) and should anchor the floor heavily. A 7-footer with DRebDef of 5 should still grab some boards; a 6-footer with DRebDef of 95 should outperform most.

**Data to pull:** BBRef Advanced DRB% — sort descending for 95 anchors and look at qualified guards for 5 anchors.

---

## Part 4 — Defensive Attributes

### Interior Defense (`Attr_InteriorDefense → BlockMod + ContestPenalty`)

Two effects:
1. **BlockMod** = `(IntDef / 100) * 0.08` → probability per shot of a block
2. **ContestPenalty on inside shots** = `(IntDef / 100) * 0.09` → FG% reduction applied to the shooter

**Research target for blocks:** BBRef — BLK% (blocks per 100 opponent 2PT FGA while on floor), min 500 min.

| Attr | Target BLK% | Target Contest Penalty | NBA Anchor |
|------|------------|----------------------|-----------|
| 95 | 5.5–8% block rate | ~8–9% FG reduction on inside shots | Myles Turner (6–8%), Jaren Jackson Jr. (7–9%), Gobert (5–7%) |
| 50 | 1.5–2% | ~4–5% | Average big |
| 5 | 0.2–0.5% | ~0.5% | Guard who never contests inside |

**Current BlockMod output:**
- Attr 95 → 7.6% per shot ← in range
- Attr 50 → 4.0% ← **too high** (average should be ~1.5–2%)
- Attr 5 → 0.4% ← OK

The current formula is linear. The real world is nonlinear — the difference between a bad and an average shot blocker is small; the difference between an average and an elite one is large. Consider using a power formula: `(IntDef / 100)^1.5 * 0.09`.

**Data to pull:** BBRef BLK% leaderboard, filter min 500 min. Elite blockers: 5–9%. Role players: 1–2%. Guards: <0.5%.

---

### Perimeter Defense (`Attr_PerimeterDefense + Speed → StealMod + ContestPenalty`)

Two effects:
1. **StealMod** = `((PerimDef + Speed) / 200) * 0.03` → steal probability per possession
2. **ContestPenalty on 3PT** = `(PerimDef / 100) * 0.06` → 3P% reduction

**Research target for steals:** BBRef — STL% (steals per 100 team possessions while on floor).

| PerimDef + Speed (each) | Target STL% | Target 3PT Contest Penalty | NBA Anchor |
|------------------------|------------|--------------------------|-----------|
| 95 / 95 | 3.0–3.5 steals per 100 poss | ~5–6% | Marcus Smart (3.5%), Kawhi Leonard (3.0%), Chris Paul (3.0%) |
| 60 / 60 | 1.2–1.5 | ~3% | Solid perimeter defender |
| 50 / 50 | 0.9–1.2 | ~3% | League avg is ~1.0 per 100 poss |
| 25 / 25 | 0.5–0.8 | ~1.5% | Average big who can guard the perimeter |
| 5 / 5 | 0.2–0.3 | ~0.3% | Slow center who can't keep up |

**Current StealMod output (per possession):**
- Attr 95/95 → 2.85% ← close
- Attr 50/50 → 1.5% ← slightly high
- Attr 5/5 → 0.15% ← good

**Data to pull:** BBRef STL% column (steals per 100 possessions), league leaders and bottom. Marcus Smart, Chris Paul at top; slow rim-protecting centers at bottom.

---

## Part 5 — Physical Attributes

Physical attributes are visible to the GM and compound with hidden attributes.

### Height

Stored as a 0–100 scale but represents actual inches. The mapping should be:

| Attr | Height | Examples |
|------|--------|---------|
| 95 | ~88" (7'4") | Victor Wembanyama, Kristaps Porziņģis |
| 80 | ~84" (7'0") | Rudy Gobert, Brook Lopez |
| 70 | ~81" (6'9") | Bigs and wings |
| 60 | ~78" (6'6") | Wings |
| 50 | ~76" (6'4") | Combo guard / small forward |
| 30 | ~73" (6'1") | Average guard |
| 10 | ~70" (5'10") | Small guards |
| 5 | ~68" (5'8") | Isaiah Thomas, Muggsy Bogues territory |

Height feeds into DRebWeight directly. It should also influence ContestPenalty for interior defense in a future update.

---

### Speed

Affects: `DriveGravity`, `ShotClockAggressiveness`, `CutTendency`, `AlleyOopTendency`, `StealMod`.

| Attr | Expected behavior |
|------|-----------------|
| 95 | Ja Morant, De'Aaron Fox speed — drives constantly, creates fast breaks |
| 50 | Average NBA starter pace |
| 5 | Slow big man — rarely drives, very low cut/steal tendency |

**Research target:** NBA tracking — "Speed" in mph and "Drives per game." Top guards: 4.6+ mph, 15+ drives/game. Bottom: centers at 3.8 mph, 2–3 drives/game.

---

### Jumping

Affects: `AlleyOopTendency`, `CutTendency`, `ORebWeight`, `BlockMod` (via Attr_InteriorDefense formula — note: currently Jumping does NOT feed BlockMod, only ORebWeight).

| Attr | Vertical / Profile |
|------|------------------|
| 95 | Zach LaVine (46" vert), Zion Williamson, Anthony Davis |
| 50 | Average NBA player (~28–30" vert) |
| 5 | Slow, earthbound big — Nikola Jokić-type (low vertical, compensates with skill) |

**Note:** Jumping should feed into block rate. Currently it doesn't. A player with 95 Jumping and 5 InteriorDefense probably shouldn't block shots, but Jumping should slightly boost the effective block chance.

---

### Strength

Currently minimal impact on the engine. Future improvements should tie Strength to:
- Drawing fouls (higher strength → more foul drawn probability on contact shots)
- Contested rebound success rate
- Post play effectiveness

For now: note which attributes Strength currently does NOT feed into so we know what to fix.

**Strength currently feeds into: nothing directly** (FadeawayMidRange shot weight uses Strength in the shot context selection, but it's minor).

---

### Endurance

Affects fatigue accumulation rate. Currently: `actionPlayer.Fatigue += 0.004` per possession played, and Endurance does not modify this rate.

**Planned change:** `Fatigue += 0.004 * (1 - (Endurance - 50) / 200.0)` so higher Endurance reduces fatigue buildup.

| Attr | Effect |
|------|-------|
| 95 | LeBron James ironman — barely fatigues across 4 quarters |
| 50 | Average — starts showing fatigue mid-Q4 |
| 5 | Gasol late career — makes/misses affected by Q3 |

---

## Summary: Current vs Target Comparison

| Property | Formula Input | Attr 5 (Current) | Attr 5 (Target) | Attr 50 (Current) | Attr 50 (Target) | Attr 95 (Current) | Attr 95 (Target) | Fix Priority |
|----------|--------------|-----------------|-----------------|------------------|------------------|------------------|------------------|-------------|
| ThreeMakePct | ThreePoint | 28.9% | 25% | 36.5% | 36% | 44.2% | 44% | Low (floor too high) |
| MidRangeMakePct | MidRange | 31.1% | 29% | 41.0% | 40% | 50.9% | 52% | Low (minor ceiling raise) |
| InsideMakePct | Inside | 46.4% | 44% | 58.5% | 62% | 70.7% | 78% | **High** (ceiling off by ~7%) |
| FTMakePct | FreeThrow | 57.0% | 42% | 75.0% | 74% | 93.0% | 93% | **High** (floor off by ~15%) |
| TurnoverRate | IQ + Drib | 17.4% | 20% | 12.0% | 13% | 6.6% | 6% | Medium |
| BlockMod | InteriorDef | 0.4% | 0.4% | 4.0% | 2.0% | 7.6% | 8% | Medium (mid-range inflated) |
| StealMod | PerimDef + Spd | 0.15% | 0.2% | 1.5% | 1.0% | 2.85% | 3.0% | Low (minor mid-range fix) |

---

## Research Checklist

Pull the following datasets from BBRef and NBA.com. For each, record:
- The top 5 seasonal performances (for the 95 anchor)
- The median performance (for the 50 anchor)
- The bottom 5 for qualified players (for the 5 anchor)

### From Basketball Reference (`2014–15 to 2023–24`, Totals or Per-Game)

- [ ] **3P%** — sort 3P% desc/asc, filter min 3.0 3PA/G. Note: also record the player's 3PA/G so we know if high% is on volume.
- [ ] **FT%** — sort desc/asc, filter min 2.0 FTA/G. The bottom is more important than the top here.
- [ ] **TOV%** (Advanced tab) — sort asc for best ball handlers, desc for worst.
- [ ] **AST%** (Advanced tab) — sort desc for elite passers.
- [ ] **ORB%** (Advanced tab) — sort desc for elite offensive rebounders.
- [ ] **DRB%** (Advanced tab) — sort desc for elite defensive rebounders.
- [ ] **STL%** (Advanced tab) — sort desc for elite defenders.
- [ ] **BLK%** (Advanced tab) — sort desc for elite shot blockers.

### From NBA.com/stats

- [ ] **At-Rim FG%** — Shot Dashboard → Restricted Area, sort FG%, min 3.0 FGA/game.
- [ ] **Mid-Range FG%** — Shot Dashboard → Mid-Range, sort FG%, min 2.0 FGA/game.
- [ ] **Drives per game + Drive FG%** — Tracking stats, for DriveGravity calibration.
- [ ] **Speed (mph avg)** — Tracking → Speed-Distance, for Speed attribute anchors.

---

## Validation Protocol

After updating formulas, run this test:

1. In SimLab, set **all 10 players to attr 50** across all attributes
2. Set pace to 100 for both teams
3. Run **10,000 games**
4. Compare average outputs to real NBA averages:

| Metric | Real NBA Average | Acceptable Sim Range |
|--------|-----------------|---------------------|
| Points per team per game | 113.5 (2023–24) | 108–120 |
| FG% | 47.0% | 44–50% |
| 3P% | 36.2% | 34–38% |
| FT% | 77.0% | 74–80% |
| Team OReb per game | 10.3 | 9–12 |
| Team DReb per game | 33.2 | 30–36 |
| Assists per game | 26.4 | 23–30 |
| Turnovers per game | 13.6 | 12–16 |
| Steals per game | 7.7 | 6–10 |
| Blocks per game | 4.7 | 3–7 |

If the sim is outside these ranges with 50-across attributes, the formulas need adjustment before adding roster-specific data.

---

## Next Step

Once the real NBA data is pulled and the 5/95 anchors are confirmed, the formula revisions are simple algebra:

Given a target `low` (attr 5) and `high` (attr 95):
```
multiplier = (high - low) / (0.95 - 0.05)  = (high - low) / 0.9
base       = low - 0.05 * multiplier
formula    = base + (attr / 100) * multiplier
```

Apply this to each property in `Player.cs`, update the Knicks and Thunder rosters to reflect realistic player attributes against the new scale, then re-run validation.
