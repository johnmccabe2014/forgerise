namespace ForgeRise.Api.Welfare;

/// <summary>
/// Coach-facing readiness categories. Master prompt §9.
/// Raw welfare data MUST be transformed into one of these before it reaches a coach UI.
/// </summary>
public enum SafeCategory
{
    Ready,
    Monitor,
    ModifyLoad,
    RecoveryFocus,
}

public static class SafeCategoryLabels
{
    public static string Label(SafeCategory category) => category switch
    {
        SafeCategory.Ready => "Ready",
        SafeCategory.Monitor => "Monitor",
        SafeCategory.ModifyLoad => "Modify Load",
        SafeCategory.RecoveryFocus => "Recovery Focus",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}
