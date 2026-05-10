namespace ForgeRise.Api.Data.Entities.Video;

/// <summary>
/// Lifecycle of a single uploaded video, from the moment a coach starts an
/// upload session to a fully-processed asset (or a terminal failure).
/// Stored as a string column in Postgres (matches existing enum convention)
/// so values are diff-friendly and grep-friendly.
/// </summary>
public enum VideoProcessingState
{
    Queued,
    Probing,
    Encoding,
    Transcribing,
    Summarising,
    Ready,
    Failed,
}

/// <summary>
/// Type of an entry on a video's coach-driven timeline.
/// <c>WelfareFlag</c> events are scrubbed from non-welfare viewers — see
/// master prompt §9 and the V5 slice in the architecture doc.
/// </summary>
public enum VideoTimelineEventKind
{
    Note,
    Highlight,
    Drill,
    /// <summary>
    /// Welfare-relevant moment. Body must never be shown to non-welfare
    /// viewers and never included in AI prompts. The list/read endpoint
    /// scrub MUST land in the same PR as the first endpoint that exposes
    /// timeline events (see security review iter1, finding #9).
    /// </summary>
    WelfareFlag,
}
