using ForgeRise.Api.Sessions;
using Xunit;

namespace ForgeRise.Api.Tests.Sessions;

public class GlossaryCatalogueTests
{
    [Fact]
    public void Matches_known_acronym_in_block_title()
    {
        var hits = GlossaryCatalogue.MatchIn("RAMP warm-up + activation", "Full intensity ramp, prep contact loads.");
        Assert.Contains(hits, t => t.Term == "RAMP");
    }

    [Fact]
    public void Acronym_match_is_word_bounded_not_substring()
    {
        // "ramp" as a lowercase substring of a normal word should not match the
        // RAMP acronym entry. We do not want spurious tooltips on words like
        // "ramps", "preamble", "trampoline".
        var hits = GlossaryCatalogue.MatchIn("the team ramps up gradually");
        Assert.DoesNotContain(hits, t => t.Term == "RAMP");
    }

    [Fact]
    public void Multi_word_term_matches_inside_a_sentence()
    {
        var hits = GlossaryCatalogue.MatchIn("Walk-through phase play", "Defensive line + ruck shape at walking pace.");
        Assert.Contains(hits, t => t.Term == "walk-through");
        Assert.Contains(hits, t => t.Term == "phase play");
        Assert.Contains(hits, t => t.Term == "ruck");
    }

    [Fact]
    public void Null_and_empty_fragments_are_ignored()
    {
        var hits = GlossaryCatalogue.MatchIn(null, "", "   ");
        Assert.Empty(hits);
    }

    [Fact]
    public void Result_is_distinct_even_if_term_appears_in_multiple_fragments()
    {
        var hits = GlossaryCatalogue.MatchIn("SSG block", "Conditioned SSG with constraints.", "Another SSG mention.");
        Assert.Single(hits, t => t.Term == "SSG");
    }

    [Fact]
    public void Result_is_stable_in_catalogue_order()
    {
        // Catalogue declares RAMP before SSG; output must reflect that regardless
        // of input order so the UI renders chips deterministically.
        var hits = GlossaryCatalogue.MatchIn("SSG before RAMP");
        var idxRamp = hits.ToList().FindIndex(t => t.Term == "RAMP");
        var idxSsg = hits.ToList().FindIndex(t => t.Term == "SSG");
        Assert.True(idxRamp >= 0 && idxSsg >= 0);
        Assert.True(idxRamp < idxSsg);
    }
}
