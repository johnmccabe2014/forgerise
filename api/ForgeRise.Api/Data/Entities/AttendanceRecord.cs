namespace ForgeRise.Api.Data.Entities;

public enum AttendanceStatus
{
    Absent = 0,
    Present = 1,
    Late = 2,
    Excused = 3,
}

public sealed class AttendanceRecord
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid RecordedByUserId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
