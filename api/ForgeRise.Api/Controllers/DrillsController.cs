using ForgeRise.Api.Sessions;
using ForgeRise.Api.Sessions.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeRise.Api.Controllers;

/// <summary>
/// Read-only access to the static drill catalogue. The catalogue lives in
/// process for v1 (see <see cref="DrillCatalogue"/>) and is intentionally
/// small and hand-curated. These endpoints exist so the web app can render
/// a coach-friendly drill library and the "Run session" mode can fetch the
/// long-form coaching cues + diagram metadata for any drill the plan refers
/// to without having to fan out across plan endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("drills")]
public sealed class DrillsController : ControllerBase
{
    private static DrillDto ToDto(Drill d) => new(
        d.Id, d.Title, d.Description, d.DurationMinutes, d.Tags,
        d.LongDescription, d.CoachingCues, d.Equipment, d.WhatItMeans, d.DiagramKey,
        GlossaryCatalogue.MatchIn(d.Title, d.Description, d.LongDescription, d.WhatItMeans)
            .Select(g => new GlossaryTermDto(g.Term, g.Plain))
            .ToList());

    [HttpGet]
    public IActionResult List() => Ok(DrillCatalogue.All.Select(ToDto));

    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        var drill = DrillCatalogue.TryFind(id);
        if (drill is null) return NotFound();
        return Ok(ToDto(drill));
    }
}
