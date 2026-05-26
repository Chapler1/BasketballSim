using BasketballSim.Models;

namespace BasketballSim.Services;

public static class InjuryService
{
    // ── Core susceptibility formula ───────────────────────────────────────────
    // Linear (exp=1.0): rating=99→0.02, rating=70→0.60, rating=50→1.0, rating=10→1.80.
    // Less steep than the old ^1.5 (which gave 0.003/0.47/1.0/2.4) — adds randomness.
    private static double Susceptibility(int rating) =>
        (100 - rating) / 50.0;

    // ── New injury roll (for players with no current injury) ──────────────────
    public static ActiveInjury? RollForInjury(Player player, double energy, Random rng)
    {
        if (player.CurrentInjury is not null) return null;

        double fatigueMult = 1.0 + (1.0 - energy / 100.0) * 0.5;

        // Compute per-part contribution and total rate
        var partKeys  = InjuryTables.BodyParts.Keys.ToArray();
        var partRates = new double[partKeys.Length];
        double totalRate = 0.0;

        for (int i = 0; i < partKeys.Length; i++)
        {
            int rating = player.InjuryRatings.GetValueOrDefault(partKeys[i], 99);
            double rate = InjuryTables.BaseRates[partKeys[i]] * Susceptibility(rating) * fatigueMult;
            partRates[i] = rate;
            totalRate += rate;
        }

        if (rng.NextDouble() >= totalRate) return null;

        // Weighted body-part selection
        double pick = rng.NextDouble() * totalRate;
        double cumulative = 0.0;
        string selectedKey = partKeys[^1];
        for (int i = 0; i < partKeys.Length; i++)
        {
            cumulative += partRates[i];
            if (pick < cumulative) { selectedKey = partKeys[i]; break; }
        }

        return BuildInjury(player, selectedKey, rng);
    }

    // ── Re-injury roll (for Grade 1/2 players already playing through an injury) ──
    // Two outcomes: setback (same injury, clock reset) or escalation (grade upgrade).
    // G1: 75% setback / 25% escalation. G2: 60% setback / 40% escalation.
    public static ActiveInjury? RollReInjury(Player player, Random rng)
    {
        if (player.CurrentInjury is null or { Definition.Grade: 3 }) return null;
        if (rng.NextDouble() >= player.CurrentInjury.ReInjuryChancePerPoss) return null;

        int grade = player.CurrentInjury.Definition.Grade;
        double escalationChance = grade == 1 ? 0.25 : 0.40;
        bool isEscalation = rng.NextDouble() < escalationChance;

        if (!isEscalation)
        {
            // Setback: same injury definition, recovery clock re-rolled from scratch.
            int newDays = RollRecoveryDays(player.CurrentInjury.Definition, rng);
            bool setbackPlaying = grade == 1;
            return new ActiveInjury
            {
                Definition      = player.CurrentInjury.Definition,
                BodyPartKey     = player.CurrentInjury.BodyPartKey,
                BodyPartDisplay = player.CurrentInjury.BodyPartDisplay,
                DaysRemaining   = newDays,
                IsPlaying       = setbackPlaying,
                EffectiveDebuff = player.CurrentInjury.EffectiveDebuff,
            };
        }

        // Escalation: upgrade to next grade
        int newGrade = Math.Min(grade + 1, 3);
        var bodyPartKey = player.CurrentInjury.BodyPartKey;
        var (display, type, _) = InjuryTables.BodyParts[bodyPartKey];

        if (!InjuryTables.ByType.TryGetValue(type, out var defs)) return null;
        var options = defs.Where(d => d.Grade == newGrade).ToList();
        if (options.Count == 0) return null;

        var def = options[rng.Next(options.Count)];
        int days = RollRecoveryDays(def, rng);
        var debuff = ComputeEffectiveDebuff(def, bodyPartKey, player.DominantHand);
        bool escalationPlaying = newGrade == 1;

        return new ActiveInjury
        {
            Definition       = def,
            BodyPartKey      = bodyPartKey,
            BodyPartDisplay  = display,
            DaysRemaining    = days,
            IsPlaying        = escalationPlaying,
            EffectiveDebuff  = debuff,
        };
    }

    // ── Recovery time ─────────────────────────────────────────────────────────
    public static int RollRecoveryDays(InjuryDefinition def, Random rng)
    {
        const double noise = 0.40;
        int min = Math.Max(1, (int)Math.Round(def.ExpectedDays * (1.0 - noise)));
        int max = (int)Math.Round(def.ExpectedDays * (1.0 + noise));
        return min >= max ? min : rng.Next(min, max + 1);
    }

    // ── Permanent debuff roll (at moment of injury — Grade 1 always null) ─────
    public static Dictionary<string, int>? RollPermanentDebuff(
        Player player, ActiveInjury injury, Random rng)
    {
        var def = injury.Definition;
        if (def.Grade == 1 || def.BasePermChance <= 0 || def.PermanentAtRisk.Length == 0)
            return null;

        int rating = player.InjuryRatings.GetValueOrDefault(injury.BodyPartKey, 99);
        double gradeMult = def.Grade == 2 ? 0.8 : 1.5;
        double chance = def.BasePermChance * ((100 - rating) / 50.0) * gradeMult;

        if (rng.NextDouble() >= chance) return null;

        // Filter sentinel keys — oBBIQ/dBBIQ are mental and never permanently penalized
        var eligible = def.PermanentAtRisk
            .Where(k => !k.EndsWith("_never") && k != "oBBIQ" && k != "dBBIQ")
            .ToList();
        if (eligible.Count == 0) return null;

        string attrKey = eligible[rng.Next(eligible.Count)];
        int loss = def.Grade == 2 ? rng.Next(1, 4) : rng.Next(2, 6);  // G2: 1–3 pts, G3: 2–5 pts

        return new Dictionary<string, int> { [attrKey] = loss };
    }

    // ── G1 play-through threshold decision ────────────────────────────────────
    // threshold  = gameImportance × playerImportance (how much the team needs them)
    // injuryRisk = f(daysRemaining, body-part rating, debuff) (cost of playing)
    // Decision is probabilistic via sigmoid on (threshold - injuryRisk).
    //
    // gameImportance: 0.25 = regular season, 1.0 = Game 7 Finals
    // teamRank: 1-indexed rank by overall within the team (1 = best player)
    public static bool ShouldPlayThroughG1(
        Player player,
        int teamRank,
        ActiveInjury injury,
        double gameImportance,
        Random rng)
    {
        // Exponential importance decay by rank: 1→1.0, 3→0.50, 6→0.17, 8→0.09
        double playerImportance = Math.Exp(-0.35 * (teamRank - 1));
        double threshold = gameImportance * playerImportance;

        double daysFactor     = Math.Min(injury.DaysRemaining / 7.0, 1.0);
        int bodyPartRating    = player.InjuryRatings.GetValueOrDefault(injury.BodyPartKey, 99);
        double susceptibility = (100 - bodyPartRating) / 100.0;
        var deb = injury.EffectiveDebuff;
        double debuffSeverity = 1.0 - (deb.Shooting + deb.Physical + deb.Speed + deb.Jump) / 4.0;
        double injuryRisk = daysFactor * 0.5 + susceptibility * 0.3 + debuffSeverity * 0.2;

        double playProb = 1.0 / (1.0 + Math.Exp(-(threshold - injuryRisk) * 8.0));
        return rng.NextDouble() < playProb;
    }

    // ── Tick recovery (calendar days between games) ───────────────────────────
    // Returns true when the player is fully healed.
    public static bool TickRecovery(ActiveInjury injury, int calendarDays)
    {
        injury.DaysRemaining = Math.Max(0, injury.DaysRemaining - calendarDays);
        return injury.DaysRemaining == 0;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static ActiveInjury? BuildInjury(Player player, string bodyPartKey, Random rng)
    {
        var (display, type, _) = InjuryTables.BodyParts[bodyPartKey];
        if (!InjuryTables.ByType.TryGetValue(type, out var defs)) return null;

        // Grade roll: < 0.70 → G1, < 0.95 → G2, else G3
        double gradeRoll = rng.NextDouble();
        int grade = gradeRoll < InjuryTables.GradeThresholds[0] ? 1
                  : gradeRoll < InjuryTables.GradeThresholds[1] ? 2 : 3;

        var options = defs.Where(d => d.Grade == grade).ToList();
        if (options.Count == 0)
        {
            // No definition for this grade at this body part — pick closest available grade
            options = defs.OrderBy(d => Math.Abs(d.Grade - grade)).Take(1).ToList();
            if (options.Count == 0) return null;
        }

        var def  = options[rng.Next(options.Count)];
        int days = RollRecoveryDays(def, rng);
        var debuff = ComputeEffectiveDebuff(def, bodyPartKey, player.DominantHand);
        // G1: always plays through. G2/G3: DNP.
        // The 5-player floor bypass in CheckInjuries handles depleted-roster edge cases.
        bool isPlaying = def.Grade == 1;

        return new ActiveInjury
        {
            Definition       = def,
            BodyPartKey      = bodyPartKey,
            BodyPartDisplay  = display,
            DaysRemaining    = days,
            IsPlaying        = isPlaying,
            EffectiveDebuff  = debuff,
        };
    }

    // Apply dominant-hand shooting scale for arm/hand injuries.
    // Dom side: debuff × 1.5 (can't grip or follow through).
    // Non-dom side: debuff × 0.6 (less impact on shooting form).
    private static InjuryDebuff ComputeEffectiveDebuff(
        InjuryDefinition def, string bodyPartKey, DominantHand dominantHand)
    {
        if (!InjuryTables.ArmBodyPartKeys.Contains(bodyPartKey))
            return def.Debuff;

        bool isLeft = bodyPartKey.StartsWith("InjL");
        bool isDominant = (dominantHand == DominantHand.Left  &&  isLeft)
                       || (dominantHand == DominantHand.Right && !isLeft);

        double scale = isDominant ? 1.5 : 0.6;
        // How much the injury reduces shooting (1 - debuff is the penalty)
        double effectiveShooting = Math.Clamp(1.0 - (1.0 - def.Debuff.Shooting) * scale, 0.50, 1.0);

        return def.Debuff with { Shooting = effectiveShooting };
    }
}
