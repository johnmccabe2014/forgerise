namespace ForgeRise.Api.Data.Entities;

public enum WelfareAuditAction
{
    ReadRawCheckIn,
    ReadRawIncident,
    PurgeRawCheckIn,
    PurgeRawIncident,
    DeleteCheckIn,
    DeleteIncident,
}

/// <summary>
/// Append-only record of every access to raw welfare data. Master prompt §9.
/// </summary>
public class WelfareAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid? SubjectId { get; set; }   // CheckIn or Incident id
    public WelfareAuditAction Action { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
