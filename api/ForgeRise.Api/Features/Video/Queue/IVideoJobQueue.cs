namespace ForgeRise.Api.Features.Video.Queue;

/// <summary>
/// Pipeline-stage queue. V1 ships the interface only; V2/V3 supply a
/// Postgres outbox implementation, with MassTransit-on-RabbitMQ as a
/// possible future swap.
/// </summary>
public interface IVideoJobQueue
{
    /// <summary>
    /// Enqueue a job. Idempotent on <see cref="VideoJob.JobId"/> — calling
    /// twice with the same id MUST result in a single delivery.
    /// </summary>
    Task EnqueueAsync(VideoJob job, CancellationToken ct);

    /// <summary>
    /// Lease the next available job for up to <paramref name="leaseDuration"/>.
    /// Returns null if no job is available within
    /// <paramref name="waitTimeout"/>.
    /// </summary>
    Task<VideoJob?> LeaseNextAsync(
        TimeSpan waitTimeout,
        TimeSpan leaseDuration,
        CancellationToken ct);

    /// <summary>Acknowledge successful processing — removes the job.</summary>
    Task AckAsync(Guid jobId, CancellationToken ct);

    /// <summary>Negative-ack — releases the lease so the job can be retried.</summary>
    Task NackAsync(Guid jobId, CancellationToken ct);
}
