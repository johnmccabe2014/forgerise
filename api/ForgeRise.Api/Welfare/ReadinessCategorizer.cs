namespace ForgeRise.Api.Welfare;

/// <summary>
/// Pure heuristic that maps raw welfare scores to a coach-safe readiness category.
/// Master prompt §9: this is NOT a medical assessment.
///
/// Inputs (all optional; missing values are ignored when scoring):
/// - sleepHours: 0-24
/// - sorenessScore, moodScore, stressScore, fatigueScore: each 1-5
///   (1 = best / lowest concern, 5 = worst / highest concern; mood is inverted internally)
/// </summary>
public static class ReadinessCategorizer
{
    public static SafeCategory Categorize(
        double? sleepHours,
        int? sorenessScore,
        int? moodScore,
        int? stressScore,
        int? fatigueScore)
    {
        // Concern points: each axis adds 0-3 points.
        var concern = 0;

        if (sleepHours.HasValue)
        {
            if (sleepHours < 4) concern += 3;
            else if (sleepHours < 6) concern += 2;
            else if (sleepHours < 7) concern += 1;
        }

        concern += AxisConcern(sorenessScore);
        concern += AxisConcern(stressScore);
        concern += AxisConcern(fatigueScore);
        // Mood: 5 = best, 1 = worst, so invert.
        concern += AxisConcernInverted(moodScore);

        return concern switch
        {
            <= 1 => SafeCategory.Ready,
            <= 3 => SafeCategory.Monitor,
            <= 6 => SafeCategory.ModifyLoad,
            _ => SafeCategory.RecoveryFocus,
        };
    }

    private static int AxisConcern(int? score) => score switch
    {
        null or <= 2 => 0,
        3 => 1,
        4 => 2,
        _ => 3,                 // 5 or higher
    };

    private static int AxisConcernInverted(int? score) => score switch
    {
        null or >= 4 => 0,
        3 => 1,
        2 => 2,
        _ => 3,                 // 1 or lower
    };
}
