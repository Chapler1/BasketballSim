# GameEngine.cs — Possession Overhaul

## What to change

In `GameEngine.cs`, the possession simulation currently does:
1. Roll for turnover / FT / shot
2. Roll shot type
3. Roll shot outcome
4. Roll rebound

**Add a new first roll: Play Type.** The play type result changes the shot type probabilities for Roll 2, and must appear in the narrative.

---

## Step 1 — Add the PlayType enum

Add this enum to the file, next to the existing `ShotType` enum:

```csharp
public enum PlayType { Transition, PickAndRoll, Isolation, PostUp, SpotUpThree, Cut }
```

---

## Step 2 — Add a RollPlayType() method

Add this method to `GameEngine`:

```csharp
private PlayType RollPlayType()
{
    double r = _rng.NextDouble() * 100;
    if (r < 12) return PlayType.Transition;
    if (r < 40) return PlayType.PickAndRoll;
    if (r < 58) return PlayType.Isolation;
    if (r < 70) return PlayType.PostUp;
    if (r < 90) return PlayType.SpotUpThree;
    return PlayType.Cut;
}
```

---

## Step 3 — Replace RollShotType() to accept a PlayType

Replace the existing `RollShotType()` with a version that takes the play type and uses different weights depending on it:

```csharp
private ShotType RollShotType(PlayType playType)
{
    // Weights: [Layup, Dunk, MidRange, ThreePointer]
    double[] w = playType switch
    {
        PlayType.Transition   => [35, 20, 10, 35],
        PlayType.PickAndRoll  => [30, 10, 20, 40],
        PlayType.Isolation    => [20,  5, 35, 40],
        PlayType.PostUp       => [45, 10, 35, 10],
        PlayType.SpotUpThree  => [ 5,  2, 13, 80],
        PlayType.Cut          => [60, 25, 10,  5],
        _                     => [30, 11, 12, 57]
    };

    double r = _rng.NextDouble() * (w[0] + w[1] + w[2] + w[3]);
    if (r < w[0]) return ShotType.Layup;
    if (r < w[0] + w[1]) return ShotType.Dunk;
    if (r < w[0] + w[1] + w[2]) return ShotType.MidRange;
    return ShotType.ThreePointer;
}
```

---

## Step 4 — Update SimulatePossessionChain to call RollPlayType() first

In `SimulatePossessionChain`, find the line that says:

```csharp
// ── Roll 2a: Shot type ───────────────────────────────────────────
var shot = RollShotType();
```

**Before that line**, add:

```csharp
// ── Roll 1: Play type ────────────────────────────────────────────
var playType = RollPlayType();
```

Then change the `RollShotType()` call to pass the play type:

```csharp
var shot = RollShotType(playType);
```

---

## Step 5 — Update MadeNarrative and MissedNarrative to include play type

Replace the existing `MadeNarrative` and `MissedNarrative` methods with these versions that take a `PlayType` and weave it into the narrative:

```csharp
private string MadeNarrative(PlayType playType, ShotType shot)
{
    string context = playType switch
    {
        PlayType.Transition  => "in transition",
        PlayType.PickAndRoll => "off the pick-and-roll",
        PlayType.Isolation   => "off the isolation",
        PlayType.PostUp      => "from the post",
        PlayType.SpotUpThree => "from the corner",
        PlayType.Cut         => "on the backdoor cut",
        _                    => ""
    };

    return shot switch
    {
        ShotType.Layup        => Pick($"Drives {context} — lays it in!", $"Finishes {context} at the rim!"),
        ShotType.Dunk         => Pick($"SLAMS it home {context}!", $"Throws it down {context}!"),
        ShotType.MidRange     => Pick($"Pulls up {context} — GOOD!", $"Mid-range {context} — hits it!"),
        ShotType.ThreePointer => Pick($"BANG! Three {context}!", $"Knocks down the three {context}!"),
        _                     => "GOOD!"
    };
}

private string MissedNarrative(PlayType playType, ShotType shot)
{
    string context = playType switch
    {
        PlayType.Transition  => "in transition",
        PlayType.PickAndRoll => "off the pick-and-roll",
        PlayType.Isolation   => "off the isolation",
        PlayType.PostUp      => "from the post",
        PlayType.SpotUpThree => "from the corner",
        PlayType.Cut         => "on the cut",
        _                    => ""
    };

    return shot switch
    {
        ShotType.Layup        => Pick($"Layup {context} — no good", $"Misses at the rim {context}"),
        ShotType.Dunk         => $"Misses the dunk {context} — rare opportunity wasted",
        ShotType.MidRange     => Pick($"Mid-range {context} — off the iron", $"Pulls up {context} — no good"),
        ShotType.ThreePointer => Pick($"Three {context} — no good", $"Brick from deep {context}"),
        _                     => "No good"
    };
}
```

---

## Step 6 — Update all call sites for MadeNarrative and MissedNarrative

In `SimulatePossessionChain`, find the calls to `MadeNarrative(shot)` and `MissedNarrative(shot)` and pass `playType` as the first argument:

```csharp
Add(results, team, MadeNarrative(playType, shot), ...);
// and
Add(results, team, MissedNarrative(shot), ...);  // <-- change to:
Add(results, team, MissedNarrative(playType, shot), ...);
```

Also update `BlockedNarrative` call sites — `BlockedNarrative` doesn't need play type, leave it as-is.

---

## Nothing else changes

- Do not touch turnover logic, FT logic, rebound percentages, scoring, or anything else.
- The only change is: play type is rolled first, feeds into shot type weights, and appears in the narrative.