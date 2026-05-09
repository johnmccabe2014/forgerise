namespace ForgeRise.Api.Welfare;

/// <summary>
/// Single source of truth for raw welfare field names. Master prompt §9.
/// Used by the Serilog destructuring policy to redact any structured object
/// passed to the logger that carries one of these property names.
/// </summary>
public static class RawWelfareFields
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sleepHours",
        "sorenessScore",
        "menstrualPhase",
        "menstrualSymptoms",
        "moodScore",
        "stressScore",
        "fatigueScore",
        "injuryNotes",
        "medicalNotes",
    };
}
