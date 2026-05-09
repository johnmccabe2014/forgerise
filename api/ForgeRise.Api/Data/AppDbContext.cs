using ForgeRise.Api.Data.Entities;
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
    }
}