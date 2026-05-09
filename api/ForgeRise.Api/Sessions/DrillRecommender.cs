namespace ForgeRise.Api.Sessions;

/// <summary>
/// One recommended drill plus the (deterministic) reason it was picked. The
/// rationale is shown to the coach so they can sanity-check the heuristic
/// before adopting the suggestion.
/// </summary>
public sealed record DrillRecommendation(
    string DrillId,
    string Title,
    string Description,
    int DurationMinutes,
    string Rationale,
    IReadOnlyList<string> Tags);

/// <summary>
/// Pure, deterministic recommender. Picks 2–4 drills from <see cref="DrillCatalogue"/>
/// driven by intensity bucket, focus keywords, and whether the team has a
/// recent self-reported incident (which biases toward low-contact options).
/// </summary>
public static class DrillRecommender
{
    public static IReadOnlyList<DrillRecommendation> Recommend(
        string intensity,
        string? focus,
        bool hasRecentSelfIncident,
        int max = 4)
    {
        var picks = new List<DrillRecommendation>();
        var focusLower = (focus ?? string.Empty).ToLowerInvariant();
        var focusForRationale = focus ?? string.Empty;
        var requireLowContact = intensity == "Recovery emphasis" || hasRecentSelfIncident;

        bool MatchesFocus(Drill d) =>
            (focusLower.Contains("scrum") && d.Tags.Contains("scrum")) ||
            (focusLower.Contains("lineout") && d.Tags.Contains("lineout")) ||
            (focusLower.Contains("attack") && d.Tags.Contains("back_play")) ||
            (focusLower.Contains("defence") && d.Tags.Contains("team_shape")) ||
            (focusLower.Contains("defense") && d.Tags.Contains("team_shape")) ||
            (focusLower.Contains("kick") && d.Tags.Contains("kick"));

        IEnumerable<Drill> pool = DrillCatalogue.All;
        if (requireLowContact) pool = pool.Where(d => d.Tags.Contains("low_contact"));

        // Pass 1: focus-matched.
        foreach (var d in pool.Where(MatchesFocus))
        {
            if (picks.Count >= max) break;
            picks.Add(new DrillRecommendation(d.Id, d.Title, d.Description, d.DurationMinutes,
                Rationale(d, intensity, focusForRationale, hasRecentSelfIncident, focusMatch: true), d.Tags));
        }

        // Pass 2: fill from intensity-appropriate defaults.
        foreach (var d in pool.Where(d => !picks.Any(p => p.DrillId == d.Id)))
        {
            if (picks.Count >= max) break;
            // For Recovery emphasis prefer recovery-tagged; for Reduced prefer skill/decision; Standard takes the rest.
            var fits = intensity switch
            {
                "Recovery emphasis" => d.Tags.Contains("recovery") || d.Tags.Contains("decision"),
                "Reduced" => d.Tags.Contains("skill") || d.Tags.Contains("decision"),
                _ => d.Tags.Contains("game") || d.Tags.Contains("decision") || d.Tags.Contains("skill"),
            };
            if (!fits) continue;
            picks.Add(new DrillRecommendation(d.Id, d.Title, d.Description, d.DurationMinutes,
                Rationale(d, intensity, focusForRationale, hasRecentSelfIncident, focusMatch: false), d.Tags));
        }

        // Last-resort: ensure at least 2 picks even if filters are tight.
        if (picks.Count < 2)
        {
            foreach (var d in pool.Where(d => !picks.Any(p => p.DrillId == d.Id)))
            {
                if (picks.Count >= 2) break;
                picks.Add(new DrillRecommendation(d.Id, d.Title, d.Description, d.DurationMinutes,
                    Rationale(d, intensity, focusForRationale, hasRecentSelfIncident, focusMatch: false), d.Tags));
            }
        }

        return picks;
    }

    private static string Rationale(Drill d, string intensity, string focus, bool hasIncident, bool focusMatch)
    {
        var bits = new List<string>();
        if (focusMatch && !string.IsNullOrWhiteSpace(focus)) bits.Add($"matches focus '{focus.Trim()}'");
        if (intensity == "Recovery emphasis") bits.Add("intensity bucket is Recovery");
        else if (intensity == "Reduced") bits.Add("intensity bucket is Reduced");
        if (hasIncident && d.Tags.Contains("low_contact")) bits.Add("recent self-reported incident → low contact preferred");
        if (bits.Count == 0) bits.Add($"fits a {intensity} session");
        return string.Join("; ", bits) + ".";
    }
}
