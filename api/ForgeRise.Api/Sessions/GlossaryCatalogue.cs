namespace ForgeRise.Api.Sessions;

/// <summary>
/// A plain-English explanation of a piece of rugby/coaching jargon. Surfaced
/// in the UI alongside plan blocks and drill titles so a brand-new coach
/// isn't faced with shorthand like "RAMP" or "SSG" without context.
/// </summary>
public sealed record GlossaryTerm(string Term, string Plain);

/// <summary>
/// Static, hand-curated glossary. Term matching is case-insensitive whole-word
/// (or whole-acronym) inside arbitrary free text. The catalogue is small on
/// purpose — every entry should earn its place by being a term a first-time
/// coach is genuinely likely to stumble on.
/// </summary>
public static class GlossaryCatalogue
{
    public static readonly IReadOnlyList<GlossaryTerm> All = new[]
    {
        new GlossaryTerm("RAMP",
            "RAMP = Raise (heart rate), Activate (muscles), Mobilise (joints), Potentiate " +
            "(prime for performance). A four-step warm-up framework."),
        new GlossaryTerm("SSG",
            "Small-Sided Game. A short, condensed version of rugby with fewer players and " +
            "added rules that nudge players into specific decisions."),
        new GlossaryTerm("walk-through",
            "Rehearsing a phase of play at walking pace before doing it at speed — like a " +
            "stage read-through. Lets everyone learn their role safely."),
        new GlossaryTerm("ruck",
            "The contest for the ball on the ground after a tackle. Players bind over the " +
            "tackled player to win or steal possession."),
        new GlossaryTerm("ruck clean",
            "Clearing opposition players off the ball at the ruck so your team's scrum-half " +
            "can pass it away cleanly."),
        new GlossaryTerm("lineout",
            "The set-piece restart when the ball goes out of play on the side. A forward is " +
            "lifted in the air to catch a throw-in from the sideline."),
        new GlossaryTerm("scrum",
            "The set-piece restart for minor infringements. Eight forwards from each team " +
            "bind together and push for the ball."),
        new GlossaryTerm("phase play",
            "The passages of play between set-pieces. 'Phase 2' means the second ruck after " +
            "the last lineout/scrum."),
        new GlossaryTerm("box kick",
            "A high, short kick from the scrum-half (9) behind the ruck, designed to be " +
            "chased and contested in the air."),
        new GlossaryTerm("offload",
            "A pass made in or just after the tackle, before going to ground. Keeps the " +
            "attack moving and bypasses the breakdown."),
        new GlossaryTerm("pillar",
            "The forward standing immediately next to the ruck on defence. First defender, " +
            "owns the inside channel."),
        new GlossaryTerm("guard",
            "The defender standing one position out from the pillar. Owns the second " +
            "channel out from the ruck."),
        new GlossaryTerm("breakdown",
            "Catch-all term for the contest that happens at and after a tackle — usually " +
            "the ruck."),
        new GlossaryTerm("front-foot ball",
            "Quick possession won going forward, before the defence can re-set. The " +
            "opposite of 'slow ball'."),
        new GlossaryTerm("conditioned game",
            "A game with extra rules ('conditions') added by the coach to bias players " +
            "toward specific decisions. The condition does the teaching."),
        new GlossaryTerm("touch",
            "Touch rugby — a tag/touch version with no tackling. Used to drill decision " +
            "making with zero collision load."),
        new GlossaryTerm("constraint",
            "A rule the coach adds to a game or drill (e.g. 'every try must come from " +
            "an offload') that shapes the kind of decisions players have to make."),
        new GlossaryTerm("set-piece",
            "Structured restarts — scrums and lineouts. Both teams know it's coming and " +
            "have a planned shape."),
    };

    /// <summary>
    /// Returns the (distinct, in-order) glossary entries whose term appears in any of the
    /// supplied text fragments. Case-insensitive. Matches are bounded by non-letter
    /// characters so "ruck" doesn't match inside "truck" — and "RAMP" doesn't match
    /// inside "ramps" or "preamble". Multi-word terms are matched as substrings (with
    /// boundary guards), so "ruck clean" matches both inside a description and in the
    /// drill title.
    /// </summary>
    public static IReadOnlyList<GlossaryTerm> MatchIn(params string?[] fragments)
    {
        var hits = new List<GlossaryTerm>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Match longest terms first so "ruck clean" wins over "ruck" when both appear in
        // the same phrase — we still surface the shorter entry separately if it appears
        // elsewhere in the fragments.
        foreach (var term in All.OrderByDescending(t => t.Term.Length))
        {
            if (seen.Contains(term.Term)) continue;
            foreach (var frag in fragments)
            {
                if (string.IsNullOrWhiteSpace(frag)) continue;
                if (ContainsWholeTerm(frag!, term.Term))
                {
                    hits.Add(term);
                    seen.Add(term.Term);
                    break;
                }
            }
        }

        // Re-order to original catalogue order so the response shape is stable.
        return All.Where(t => seen.Contains(t.Term)).ToList();
    }

    private static bool ContainsWholeTerm(string haystack, string needle)
    {
        var idx = 0;
        while (idx < haystack.Length)
        {
            var found = haystack.IndexOf(needle, idx, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;
            var before = found == 0 || !IsTermChar(haystack[found - 1]);
            var afterIdx = found + needle.Length;
            var after = afterIdx >= haystack.Length || !IsTermChar(haystack[afterIdx]);
            if (before && after) return true;
            idx = found + 1;
        }
        return false;
    }

    private static bool IsTermChar(char c) => char.IsLetterOrDigit(c);
}
