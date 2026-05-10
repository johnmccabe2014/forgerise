using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Data.Entities.Video;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<TeamInvite> TeamInvites => Set<TeamInvite>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<WellnessCheckIn> WellnessCheckIns => Set<WellnessCheckIn>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<WelfareAuditLog> WelfareAuditLogs => Set<WelfareAuditLog>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<SessionPlan> SessionPlans => Set<SessionPlan>();
    public DbSet<PlayerLink> PlayerLinks => Set<PlayerLink>();
    public DbSet<PlayerInvite> PlayerInvites => Set<PlayerInvite>();
    public DbSet<TeamActivitySeen> TeamActivitySeens => Set<TeamActivitySeen>();
    public DbSet<TeamDrillPreference> TeamDrillPreferences => Set<TeamDrillPreference>();

    // --- Video Intelligence module (V1 skeleton) ---
    public DbSet<VideoUploadSession> VideoUploadSessions => Set<VideoUploadSession>();
    public DbSet<VideoAsset> VideoAssets => Set<VideoAsset>();
    public DbSet<SessionVideoLink> SessionVideoLinks => Set<SessionVideoLink>();
    public DbSet<VideoTimelineEvent> VideoTimelineEvents => Set<VideoTimelineEvent>();
    public DbSet<VideoClip> VideoClips => Set<VideoClip>();
    public DbSet<VideoTag> VideoTags => Set<VideoTag>();
    public DbSet<CoachVoiceNote> CoachVoiceNotes => Set<CoachVoiceNote>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<AiVideoInsight> AiVideoInsights => Set<AiVideoInsight>();
    public DbSet<HighlightCandidate> HighlightCandidates => Set<HighlightCandidate>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Team>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.OwnerUserId, x.Code }).IsUnique();
            e.HasOne(x => x.Owner)
                .WithMany(u => u.Teams)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<TeamMembership>(e =>
        {
            e.HasKey(x => x.Id);
            // One row per (team,user) — a user can only be on a team once.
            e.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
            e.HasOne(x => x.Team)
                .WithMany(t => t.Memberships)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TeamInvite>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.TeamId);
            e.HasOne(x => x.Team)
                .WithMany(t => t.Invites)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Player>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Position).HasMaxLength(40);
            e.HasOne(x => x.Team)
                .WithMany(t => t.Players)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<WellnessCheckIn>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.InjuryNotes).HasMaxLength(2_000);
            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PlayerId, x.AsOf });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<IncidentReport>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Summary).HasMaxLength(280).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(4_000);
            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PlayerId, x.OccurredAt });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<WelfareAuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PlayerId, x.At });
            e.HasIndex(x => new { x.ActorUserId, x.At });
        });

        b.Entity<Session>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Location).HasMaxLength(120);
            e.Property(x => x.Focus).HasMaxLength(200);
            e.Property(x => x.ReviewNotes).HasMaxLength(4_000);
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.ScheduledAt });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<AttendanceRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.SessionId, x.PlayerId }).IsUnique();
        });

        b.Entity<SessionPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Focus).HasMaxLength(200).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(2_000).IsRequired();
            e.Property(x => x.PlanJson).IsRequired();
            e.Property(x => x.ReadinessSnapshotJson).IsRequired();
            e.Property(x => x.RecommendationsJson).IsRequired().HasDefaultValue("[]");
            e.Property(x => x.RecentSelfIncidentCount).HasDefaultValue(0);
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.GeneratedAt });
        });

        b.Entity<PlayerLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PlayerId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PlayerInvite>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.PlayerId);
            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TeamActivitySeen>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
        });

        b.Entity<TeamDrillPreference>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DrillId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.TeamId, x.DrillId }).IsUnique();
        });

        // --- Video Intelligence module (V1 skeleton) ---
        // All entities are TeamId-scoped; (TeamId, ...) leading indexes
        // match the convention used elsewhere in this file. Soft-delete
        // (DeletedAt) entities also get a query filter so default reads are
        // safe.
        b.Entity<VideoUploadSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.DeclaredMimeType).HasMaxLength(120).IsRequired();
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.CreatedAt });
        });

        b.Entity<VideoAsset>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.MimeType).HasMaxLength(120).IsRequired();
            e.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.ThumbnailPath).HasMaxLength(1024);
            e.Property(x => x.ContentSha256).HasMaxLength(64);
            e.Property(x => x.ProcessingError).HasMaxLength(2000);
            e.Property(x => x.ProcessingState).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.CreatedAt });
            e.HasIndex(x => new { x.TeamId, x.ProcessingState });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<SessionVideoLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VideoAsset)
                .WithMany()
                .HasForeignKey(x => x.VideoAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.SessionId });
            e.HasIndex(x => new { x.SessionId, x.VideoAssetId }).IsUnique();
        });

        b.Entity<VideoTimelineEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Body).HasMaxLength(4000);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VideoAsset)
                .WithMany()
                .HasForeignKey(x => x.VideoAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.VideoAssetId, x.AtSeconds });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<VideoClip>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.StoragePath).HasMaxLength(1024);
            e.Property(x => x.ThumbnailPath).HasMaxLength(1024);
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VideoAsset)
                .WithMany()
                .HasForeignKey(x => x.VideoAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.VideoAssetId, x.StartSeconds });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<VideoTag>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Color).HasMaxLength(16);
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.Name }).IsUnique();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<CoachVoiceNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.TranscriptText).HasMaxLength(8000);
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VideoAsset)
                .WithMany()
                .HasForeignKey(x => x.VideoAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.VideoAssetId, x.AtSeconds });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        b.Entity<TranscriptSegment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VideoAsset)
                .WithMany()
                .HasForeignKey(x => x.VideoAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.VideoAssetId, x.StartSeconds });
        });

        b.Entity<AiVideoInsight>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            e.Property(x => x.Model).HasMaxLength(120).IsRequired();
            // jsonb on Postgres; on InMemory the column type hint is ignored.
            e.Property(x => x.Body).HasColumnType("jsonb");
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VideoAsset)
                .WithMany()
                .HasForeignKey(x => x.VideoAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.VideoAssetId, x.Kind });
        });

        b.Entity<HighlightCandidate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.VideoAsset)
                .WithMany()
                .HasForeignKey(x => x.VideoAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeamId, x.VideoAssetId, x.Score });
        });
    }
}