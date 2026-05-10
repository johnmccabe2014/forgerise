namespace ForgeRise.Api.Features.Video.Queue;

/// <summary>
/// Worker job descriptor. <see cref="TeamId"/> is mandatory so consumers can
/// filter by tenant before doing any work (security review iter1, finding
/// #5). <see cref="TraceParent"/> propagates the W3C trace context across
/// the queue so the upload-complete -&gt; ready trace stays connected.
/// </summary>
/// <param name="JobId">Idempotency key for the job. Re-enqueuing the same id is a no-op.</param>
/// <param name="TeamId">Tenant the job belongs to. Consumers MUST filter on this.</param>
/// <param name="VideoAssetId">Asset the job operates on.</param>
/// <param name="Stage">Pipeline stage: probe, encode, transcribe, summarise, highlights.</param>
/// <param name="TraceParent">W3C traceparent header value, or null.</param>
/// <param name="EnqueuedAt">Wall-clock enqueue time, set by the producer.</param>
public sealed record VideoJob(
    Guid JobId,
    Guid TeamId,
    Guid VideoAssetId,
    string Stage,
    string? TraceParent,
    DateTimeOffset EnqueuedAt);
