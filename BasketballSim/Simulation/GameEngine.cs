using BasketballSim.Models;

namespace BasketballSim.Simulation;

public enum ShotType { Layup, Dunk, MidRange, ThreePointer }

public enum PlayType { Transition, PickAndRoll, Isolation, PostUp, SpotUpThree, Cut }

public class GameEngine
{
    private readonly Random _rng = new();

    public List<PossessionResult> SimulateGame(string homeTeam, string awayTeam)
    {
        var results = new List<PossessionResult>(300);
        int homeScore = 0, awayScore = 0;

        for (int turn = 0; turn < 200; turn++)
        {
            bool isHome = turn % 2 == 0;
            int quarter = turn / 50 + 1;
            int clockSeconds = 720 - (int)(turn % 50 * 14.4);

            SimulatePossessionChain(homeTeam, awayTeam, isHome,
                quarter, clockSeconds, ref homeScore, ref awayScore, results);
        }

        return results;
    }

    private void SimulatePossessionChain(
        string homeTeam, string awayTeam, bool isHome,
        int quarter, int clockSeconds,
        ref int homeScore, ref int awayScore,
        List<PossessionResult> results)
    {
        int orbCount = 0;

        while (true)
        {
            string team = isHome ? homeTeam : awayTeam;

            // Anti-blowout: trailing team gets a make-probability boost
            int deficit = isHome ? awayScore - homeScore : homeScore - awayScore;
            double blowoutBoost = deficit > 0 ? Math.Min(deficit, 20) * 0.003 : 0;

            // Home court advantage (~3 extra pts/game, per Sagarin)
            double homeAdv = isHome ? 0.015 : 0.0;

            double makeBoost = blowoutBoost + homeAdv;

            // ── Roll 1: Possession type ──────────────────────────────────────
            // 7% steal | 6% dead-ball TO | 8% of 87% = ~7% FT | rest regular shot
            double r1 = _rng.NextDouble();

            if (r1 < 0.07)
            {
                Add(results, team, Pick("Turnover — stolen!", "Bad pass intercepted!"),
                    homeScore, awayScore, quarter, clockSeconds, 0);
                return;
            }

            if (r1 < 0.13)
            {
                Add(results, team, Pick("Turnover — out of bounds", "Lost handle, turnover"),
                    homeScore, awayScore, quarter, clockSeconds, 0);
                return;
            }

            if (r1 < 0.13 + 0.87 * 0.08) // Free throw possession
            {
                int pts = 0;
                for (int i = 0; i < 2; i++)
                    if (_rng.NextDouble() < 0.77 + makeBoost * 0.5) pts++;

                if (isHome) homeScore += pts;
                else awayScore += pts;

                string ftNarrative = pts switch
                {
                    2 => "Fouled — makes both free throws",
                    1 => "Fouled — 1 of 2 free throws",
                    _ => "Fouled — misses both free throws"
                };

                Add(results, team, ftNarrative, homeScore, awayScore, quarter, clockSeconds, pts);
                return;
            }

            // ── Roll 1: Play type ────────────────────────────────────────────
            var playType = RollPlayType();

            // ── Roll 2a: Shot type ───────────────────────────────────────────
            var shot = RollShotType(playType);

            // ── Roll 2b: Shot outcome ────────────────────────────────────────
            var (makePct, blockPct) = Probabilities(shot);
            double adjMake = Math.Clamp(makePct + makeBoost, 0, 1);
            double r2 = _rng.NextDouble();

            if (r2 < adjMake) // Made
            {
                int pts = shot == ShotType.ThreePointer ? 3 : 2;
                if (isHome) homeScore += pts;
                else awayScore += pts;

                Add(results, team, MadeNarrative(playType, shot), homeScore, awayScore, quarter, clockSeconds, pts);
                return;
            }

            if (r2 < adjMake + blockPct) // Blocked
            {
                // ── Roll 3: Rebound after block (40% offensive) ──────────────
                bool offReb = _rng.NextDouble() < 0.40 && orbCount < 8;

                if (offReb)
                {
                    Add(results, team, "Blocked! But the offense recovers!",
                        homeScore, awayScore, quarter, clockSeconds, 0);
                    orbCount++;
                    clockSeconds = Math.Max(0, clockSeconds - 5);
                    // same team continues
                }
                else
                {
                    Add(results, team, BlockedNarrative(shot),
                        homeScore, awayScore, quarter, clockSeconds, 0);
                    return;
                }
            }
            else // Missed
            {
                Add(results, team, MissedNarrative(playType, shot),
                    homeScore, awayScore, quarter, clockSeconds, 0);

                // ── Roll 3: Rebound after miss (27% offensive) ───────────────
                bool offReb = _rng.NextDouble() < 0.27 && orbCount < 8;

                if (offReb)
                {
                    Add(results, team, Pick("Offensive rebound — second chance!", "Tip in opportunity!"),
                        homeScore, awayScore, quarter, clockSeconds, 0);
                    orbCount++;
                    clockSeconds = Math.Max(0, clockSeconds - 5);
                    // same team continues
                }
                else
                {
                    return; // defense rebounds, possession changes
                }
            }
        }
    }

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

    private ShotType RollShotType(PlayType playType)
    {
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

    private static (double make, double block) Probabilities(ShotType shot) => shot switch
    {
        ShotType.Layup =>        (0.60, 0.04),
        ShotType.Dunk =>         (0.76, 0.01),
        ShotType.MidRange =>     (0.42, 0.02),
        ShotType.ThreePointer => (0.36, 0.005),
        _ => (0.50, 0.02)
    };

    private string MadeNarrative(PlayType playType, ShotType shot)
    {
        string context = playType switch
        {
            PlayType.Transition   => "in transition",
            PlayType.PickAndRoll  => "off the pick-and-roll",
            PlayType.Isolation    => "off the isolation",
            PlayType.PostUp       => "from the post",
            PlayType.SpotUpThree  => "from the corner",
            PlayType.Cut          => "on the backdoor cut",
            _                     => ""
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
            PlayType.Transition   => "in transition",
            PlayType.PickAndRoll  => "off the pick-and-roll",
            PlayType.Isolation    => "off the isolation",
            PlayType.PostUp       => "from the post",
            PlayType.SpotUpThree  => "from the corner",
            PlayType.Cut          => "on the cut",
            _                     => ""
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

    private string BlockedNarrative(ShotType shot) => shot switch
    {
        ShotType.Layup =>        Pick("BLOCKED at the rim!", "Stuffed!"),
        ShotType.Dunk =>         "Rejected at the rim!",
        ShotType.MidRange =>     "Floater swatted away!",
        ShotType.ThreePointer => "Three blocked!",
        _ => "Blocked!"
    };

    private string Pick(params string[] options) =>
        options[_rng.Next(options.Length)];

    private static void Add(
        List<PossessionResult> results, string team, string narrative,
        int homeScore, int awayScore, int quarter, int clockSeconds, int pointsScored)
    {
        results.Add(new PossessionResult
        {
            Team = team,
            Narrative = narrative,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Quarter = quarter,
            ClockSeconds = clockSeconds,
            PointsScored = pointsScored
        });
    }
}
