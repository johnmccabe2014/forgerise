using System.Text.Json;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using ForgeRise.Api.Sessions;
using ForgeRise.Api.Sessions.Contracts;
using ForgeRise.Api.Welfare;
using Microsoft.EntityFrameworkCore;

namespace ForgeRise.Api.Sessions;

/// <summary>
/// Plants a deterministic demo dataset so a brand-new coach can land in the
/// app, click into a team, and see realistic shapes — players, a reviewed
/// past session, a generated session plan ready to "Run" — without having
/// to laboriously seed it themselves.
///
/// Contract:
/// - Idempotent: running twice is a no-op. Identity is the demo coach's email.
/// - Refuses to run in Production (master prompt §7). The caller is expected
///   to enforce this; the seeder also defends in depth via a flag.
/// - Uses the same <see cref="ISessionPlanGenerator"/> the real endpoint uses,
///   so the generated plan exercises the heuristic exactly as a real coach
///   would see it.
/// - Never persists raw welfare scores in the snapshot JSON. The check-ins
///   themselves carry their raw scores in the DB (matching the production
///   shape), but the plan response stays SafeCategory-only, master prompt §9.
/// </summary>
public sealed class DemoSeeder
{
    public const string CoachEmail = "demo-coach@forgerise.local";
    public const string CoachPassword = "DemoCoach2026!";
    public const string CoachDisplayName = "Demo Coach";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionPlanGenerator _generator;
    private readonly TimeProvider _time;
    private readonly ILogger<DemoSeeder> _log;

    public DemoSeeder(
        AppDbContext db,
        IPasswordHasher hasher,
        ISessionPlanGenerator generator,
        TimeProvider time,
        ILogger<DemoSeeder> log)
    {
        _db = db;
        _hasher = hasher;
        _generator = generator;
        _time = time;
        _log = log;
    }

    /// <summary>
    /// Returns true if the seed ran and inserted rows; false if the demo
    /// coach already exists (i.e. nothing to do).
    /// </summary>
    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == CoachEmail, ct);
        if (existing is not null)
        {
            _log.LogInformation("demo_seed.skipped coach already exists {UserId}", existing.Id);
            return false;
        }

        var now = _time.GetUtcNow();
        var coach = new User
        {
            Id = Guid.NewGuid(),
            Email = CoachEmail,
            DisplayName = CoachDisplayName,
            PasswordHash = _hasher.Hash(CoachPassword),
            CreatedAt = now,
        };
        _db.Users.Add(coach);

        await SeedTeam(coach.Id, "Demo U15s", "demo-u15s", forwardCount: 8, backCount: 7,
            // U15 squad: bias toward Ready/Monitor — younger players, lighter loads.
            categoryMix: new[]
            {
                SafeCategory.Ready, SafeCategory.Ready, SafeCategory.Ready, SafeCategory.Ready,
                SafeCategory.Ready, SafeCategory.Monitor, SafeCategory.Monitor, SafeCategory.Monitor,
                SafeCategory.Monitor, SafeCategory.ModifyLoad, SafeCategory.ModifyLoad,
                SafeCategory.Ready, SafeCategory.Ready, SafeCategory.Monitor, SafeCategory.Ready,
            },
            previousFocus: "Catch-pass under fatigue",
            previousReview: "Hands sharp by end of session. Need more line speed on D.",
            now: now,
            ct: ct);

        await SeedTeam(coach.Id, "Demo Seniors", "demo-srs", forwardCount: 9, backCount: 7,
            // Senior squad mid-week: realistic mix with one Recovery Focus to exercise
            // the "Recovery emphasis" intensity bucket in the generated plan.
            categoryMix: new[]
            {
                SafeCategory.Ready, SafeCategory.Ready, SafeCategory.Ready,
                SafeCategory.Monitor, SafeCategory.Monitor, SafeCategory.Monitor,
                SafeCategory.ModifyLoad, SafeCategory.ModifyLoad, SafeCategory.ModifyLoad,
                SafeCategory.ModifyLoad, SafeCategory.RecoveryFocus, SafeCategory.Monitor,
                SafeCategory.Ready, SafeCategory.Ready, SafeCategory.Monitor, SafeCategory.Ready,
            },
            previousFocus: "Lineout shape vs short throws",
            previousReview: "Front lifts much sharper. Maul defence still soft on second drive.",
            now: now,
            ct: ct);

        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "demo_seed.completed coach={CoachId} email={CoachEmail}",
            coach.Id, CoachEmail);
        return true;
    }

    private async Task SeedTeam(
        Guid ownerId,
        string name,
        string code,
        int forwardCount,
        int backCount,
        IReadOnlyList<SafeCategory> categoryMix,
        string previousFocus,
        string previousReview,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            Name = name,
            Code = code,
            CreatedAt = now,
        };
        _db.Teams.Add(team);

        var forwardNames = new[]
        {
            "Alex Quinn", "Sam Foley", "Jamie O'Hara", "Rowan Ellis", "Charlie Webb",
            "Taylor Reid", "Drew Lewis", "Robin Marsh", "Casey Wood",
        };
        var backNames = new[]
        {
            "Jordan Lane", "Frankie Tate", "Morgan Page", "Kai Hughes",
            "Ari Brennan", "Pat Doyle", "Nicky Vaughan",
        };

        var players = new List<Player>();
        for (var i = 0; i < forwardCount; i++)
        {
            players.Add(new Player
            {
                Id = Guid.NewGuid(),
                TeamId = team.Id,
                DisplayName = forwardNames[i % forwardNames.Length] + (i >= forwardNames.Length ? $" #{i + 1}" : string.Empty),
                Position = "Forward",
                JerseyNumber = i + 1,
                CreatedAt = now,
            });
        }
        for (var i = 0; i < backCount; i++)
        {
            players.Add(new Player
            {
                Id = Guid.NewGuid(),
                TeamId = team.Id,
                DisplayName = backNames[i % backNames.Length] + (i >= backNames.Length ? $" #{i + 1}" : string.Empty),
                Position = "Back",
                JerseyNumber = forwardCount + i + 1,
                CreatedAt = now,
            });
        }
        _db.Players.AddRange(players);

        // Wellness check-ins: one per player, AsOf earlier today. Raw scores are
        // representative of the target SafeCategory bucket but kept benign.
        for (var i = 0; i < players.Count; i++)
        {
            var category = categoryMix[i % categoryMix.Count];
            var (sleep, soreness, mood, stress, fatigue) = ScoresFor(category);
            _db.WellnessCheckIns.Add(new WellnessCheckIn
            {
                Id = Guid.NewGuid(),
                PlayerId = players[i].Id,
                RecordedByUserId = ownerId,
                SubmittedBySelf = false,
                AsOf = now.AddHours(-6),
                CreatedAt = now.AddHours(-6),
                SleepHours = sleep,
                SorenessScore = soreness,
                MoodScore = mood,
                StressScore = stress,
                FatigueScore = fatigue,
                Category = category,
            });
        }

        // A reviewed past session — gives the plan generator a focus to inherit.
        var pastSession = new Session
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            ScheduledAt = now.AddDays(-3),
            DurationMinutes = 75,
            Type = SessionType.Training,
            Location = "Main pitch",
            Focus = previousFocus,
            ReviewNotes = previousReview,
            ReviewedAt = now.AddDays(-3).AddHours(2),
            CreatedByUserId = ownerId,
            CreatedAt = now.AddDays(-3),
        };
        _db.Sessions.Add(pastSession);

        // Generate a plan with the real generator so the demo experience matches
        // what a coach would see after pressing "Generate plan" themselves.
        var snapshot = players
            .Select((p, idx) => new PlayerReadiness(p.Id, categoryMix[idx % categoryMix.Count]))
            .ToList();
        var ctx = new SessionPlanContext(
            TeamId: team.Id,
            FocusOverride: null,
            PreviousSessionFocus: previousFocus,
            PreviousSessionReview: previousReview,
            GeneratedAt: now,
            Readiness: snapshot,
            HasRecentSelfIncident: false,
            FavouriteDrillIds: null,
            ExcludedDrillIds: null);
        var generated = await _generator.GenerateAsync(ctx, ct);

        _db.SessionPlans.Add(new SessionPlan
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            GeneratedAt = now,
            GeneratedByUserId = ownerId,
            BasedOnSessionId = pastSession.Id,
            Focus = generated.Focus,
            Summary = generated.Summary,
            PlanJson = JsonSerializer.Serialize(
                generated.Blocks.Select(b => new SessionPlanBlockDto(
                    b.Block, b.Title, b.DurationMinutes, b.Intent, b.Intensity)),
                JsonOpts),
            ReadinessSnapshotJson = JsonSerializer.Serialize(
                generated.ReadinessSnapshot.Select(r => new SessionPlanReadinessRow(r.PlayerId, r.Category)),
                JsonOpts),
            RecommendationsJson = JsonSerializer.Serialize(
                generated.Recommendations.Select(r => new SessionPlanRecommendationDto(
                    r.DrillId, r.Title, r.Description, r.DurationMinutes, r.Rationale, r.Tags)),
                JsonOpts),
            RecentSelfIncidentCount = 0,
        });
    }

    private static (double sleep, int soreness, int mood, int stress, int fatigue) ScoresFor(SafeCategory cat) => cat switch
    {
        SafeCategory.Ready          => (8.0, 1, 5, 1, 1),
        SafeCategory.Monitor        => (7.0, 2, 4, 2, 2),
        SafeCategory.ModifyLoad     => (6.0, 3, 3, 3, 3),
        SafeCategory.RecoveryFocus  => (4.0, 4, 2, 4, 5),
        _                           => (7.0, 2, 4, 2, 2),
    };
}
