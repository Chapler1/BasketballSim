using BasketballSim.Models;

namespace BasketballSim.Services;

/// <summary>
/// Applies end-of-season development and decline to a coach's ratings.
/// Young coaches with high Potential grow toward their ceiling; coaches past
/// peak age (52) slowly decline. Veteran coaches get a stability floor.
/// </summary>
public static class CoachProgressionService
{
    private const int PeakAge = 52;

    /// <summary>
    /// Advance the coach's age and YearsCoached by one season, then adjust ratings.
    /// Call this once per team per offseason.
    /// </summary>
    public static void ApplyOffseasonProgression(Coach coach)
    {
        coach.Age++;
        coach.YearsCoached++;

        double ceiling = 45.0 + coach.Potential * 0.45;  // Potential 0→45, 100→90

        if (coach.Age < PeakAge)
        {
            // Growth phase: close gap to ceiling proportional to potential
            double growthFactor = (coach.Potential / 100.0) * 0.04;
            coach.OffensiveRating = (int)Math.Round(
                coach.OffensiveRating + growthFactor * (ceiling - coach.OffensiveRating));
            coach.DefensiveRating = (int)Math.Round(
                coach.DefensiveRating + growthFactor * (ceiling - coach.DefensiveRating));
        }
        else
        {
            // Decline phase: gentle annual drop, accelerating after 60
            double declineRate = 0.008 + Math.Max(0, coach.Age - 60) * 0.002;
            int newOff = (int)Math.Round(coach.OffensiveRating * (1.0 - declineRate));
            int newDef = (int)Math.Round(coach.DefensiveRating * (1.0 - declineRate));

            // Veteran stability floor: experience buffers the sharpest drops
            if (coach.YearsCoached >= 10)
            {
                newOff = Math.Max(newOff, coach.OffensiveRating - 3);
                newDef = Math.Max(newDef, coach.DefensiveRating - 3);
            }

            coach.OffensiveRating = Math.Max(newOff, 35);
            coach.DefensiveRating = Math.Max(newDef, 35);
        }
    }
}
