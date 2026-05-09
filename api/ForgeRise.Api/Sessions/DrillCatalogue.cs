namespace ForgeRise.Api.Sessions;

/// <summary>
/// One drill in the static catalogue. <see cref="Tags"/> drives matching by
/// intensity, contact load, and focus area. The catalogue is intentionally
/// in-process and hand-curated for v1 — master prompt §4 leaves room for a
/// data-driven catalogue later.
/// </summary>
public sealed record Drill(
    string Id,
    string Title,
    string Description,
    int DurationMinutes,
    IReadOnlyList<string> Tags);

public static class DrillCatalogue
{
    public static readonly IReadOnlyList<Drill> All = new[]
    {
        new Drill("mobility-flow", "Mobility flow + breathing",
            "10-min guided mobility circuit; nasal breathing only.",
            10, new[] { "low_contact", "recovery", "warmup" }),
        new Drill("walk-throughs", "Walk-through phase play",
            "Defensive line + ruck shape at walking pace; talk through cues.",
            12, new[] { "low_contact", "decision", "team_shape" }),
        new Drill("hand-skill-square", "Hand-skill square",
            "4-corner passing square; both hands, 60–70% pace.",
            12, new[] { "low_contact", "skill", "back_play" }),
        new Drill("ruck-clean-tech", "Ruck clean technique",
            "Bagged hits → live cleanout, body height + foot speed cues.",
            15, new[] { "skill", "forward_play", "contact" }),
        new Drill("lineout-throws", "Lineout throwing + lifting",
            "Calls → throws → contested lifts; reset every 5.",
            15, new[] { "skill", "forward_play", "lineout", "low_contact" }),
        new Drill("scrum-engage", "Scrum engagement sequence",
            "Crouch-bind-set sequence with scrum machine; live for last 3.",
            18, new[] { "skill", "forward_play", "scrum", "contact" }),
        new Drill("attack-shape", "Attack shape vs passive D",
            "Run patterns vs passive defenders; coach calls phase.",
            15, new[] { "decision", "back_play", "team_shape", "low_contact" }),
        new Drill("kick-chase", "Kick chase pressure",
            "Box kick + chase; reload; emphasise alignment.",
            12, new[] { "decision", "back_play", "fitness" }),
        new Drill("conditioned-game", "Conditioned small-sided game",
            "8 v 8 in 40m channel; constraints to bias your focus.",
            18, new[] { "game", "decision", "fitness", "contact" }),
        new Drill("touch-decision", "Touch + decision SSG",
            "Touch rugby with a tag rule; rewards decision over collision.",
            16, new[] { "game", "decision", "low_contact", "fitness" }),
        new Drill("cooldown-1to1", "Cool-down + 1:1 check-in",
            "Light jog, mobility, coach checks in with each player.",
            10, new[] { "low_contact", "recovery", "cooldown" }),
    };
}
