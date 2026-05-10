using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeRise.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class VideoModuleSkeleton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ThumbnailPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ProcessingState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProcessingError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoAssets_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoTags_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoUploadSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    DeclaredMimeType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DeclaredSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    VideoAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AbandonedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoUploadSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoUploadSessions_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiVideoInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Body = table.Column<string>(type: "jsonb", nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TokensUsed = table.Column<int>(type: "integer", nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVideoInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiVideoInsights_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiVideoInsights_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachVoiceNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AtSeconds = table.Column<double>(type: "double precision", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    TranscriptText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachVoiceNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachVoiceNotes_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoachVoiceNotes_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HighlightCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    EndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DismissedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PromotedToClipId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HighlightCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HighlightCandidates_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HighlightCandidates_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionVideoLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionVideoLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionVideoLinks_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionVideoLinks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionVideoLinks_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TranscriptSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    EndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranscriptSegments_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TranscriptSegments_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoClips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    EndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ThumbnailPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoClips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoClips_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoClips_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AtSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoTimelineEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoTimelineEvents_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoTimelineEvents_VideoAssets_VideoAssetId",
                        column: x => x.VideoAssetId,
                        principalTable: "VideoAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiVideoInsights_TeamId_VideoAssetId_Kind",
                table: "AiVideoInsights",
                columns: new[] { "TeamId", "VideoAssetId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_AiVideoInsights_VideoAssetId",
                table: "AiVideoInsights",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachVoiceNotes_TeamId_VideoAssetId_AtSeconds",
                table: "CoachVoiceNotes",
                columns: new[] { "TeamId", "VideoAssetId", "AtSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_CoachVoiceNotes_VideoAssetId",
                table: "CoachVoiceNotes",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_HighlightCandidates_TeamId_VideoAssetId_Score",
                table: "HighlightCandidates",
                columns: new[] { "TeamId", "VideoAssetId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_HighlightCandidates_VideoAssetId",
                table: "HighlightCandidates",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionVideoLinks_SessionId_VideoAssetId",
                table: "SessionVideoLinks",
                columns: new[] { "SessionId", "VideoAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionVideoLinks_TeamId_SessionId",
                table: "SessionVideoLinks",
                columns: new[] { "TeamId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionVideoLinks_VideoAssetId",
                table: "SessionVideoLinks",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSegments_TeamId_VideoAssetId_StartSeconds",
                table: "TranscriptSegments",
                columns: new[] { "TeamId", "VideoAssetId", "StartSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptSegments_VideoAssetId",
                table: "TranscriptSegments",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssets_TeamId_CreatedAt",
                table: "VideoAssets",
                columns: new[] { "TeamId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAssets_TeamId_ProcessingState",
                table: "VideoAssets",
                columns: new[] { "TeamId", "ProcessingState" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoClips_TeamId_VideoAssetId_StartSeconds",
                table: "VideoClips",
                columns: new[] { "TeamId", "VideoAssetId", "StartSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoClips_VideoAssetId",
                table: "VideoClips",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoTags_TeamId_Name",
                table: "VideoTags",
                columns: new[] { "TeamId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoTimelineEvents_TeamId_VideoAssetId_AtSeconds",
                table: "VideoTimelineEvents",
                columns: new[] { "TeamId", "VideoAssetId", "AtSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoTimelineEvents_VideoAssetId",
                table: "VideoTimelineEvents",
                column: "VideoAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoUploadSessions_TeamId_CreatedAt",
                table: "VideoUploadSessions",
                columns: new[] { "TeamId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiVideoInsights");

            migrationBuilder.DropTable(
                name: "CoachVoiceNotes");

            migrationBuilder.DropTable(
                name: "HighlightCandidates");

            migrationBuilder.DropTable(
                name: "SessionVideoLinks");

            migrationBuilder.DropTable(
                name: "TranscriptSegments");

            migrationBuilder.DropTable(
                name: "VideoClips");

            migrationBuilder.DropTable(
                name: "VideoTags");

            migrationBuilder.DropTable(
                name: "VideoTimelineEvents");

            migrationBuilder.DropTable(
                name: "VideoUploadSessions");

            migrationBuilder.DropTable(
                name: "VideoAssets");
        }
    }
}
