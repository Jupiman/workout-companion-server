using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutCompanion.Server.Data.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(WorkoutDbContext))]
[Migration("202609150001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WorkoutSessions",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SyncId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                ProgramNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                WorkoutNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                PayloadSchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                SourceDeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_WorkoutSessions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SessionExercises",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SyncId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                WorkoutSessionId = table.Column<long>(type: "INTEGER", nullable: false),
                SourceProgressionTrackSyncId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                ExerciseNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SortOrderSnapshot = table.Column<int>(type: "INTEGER", nullable: false),
                TrackingMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                RepMinSnapshot = table.Column<int>(type: "INTEGER", nullable: true),
                RepMaxSnapshot = table.Column<int>(type: "INTEGER", nullable: true),
                TargetRepsSnapshot = table.Column<int>(type: "INTEGER", nullable: true),
                PrescribedWeightCentiKgSnapshot = table.Column<int>(type: "INTEGER", nullable: true),
                TargetDurationSecondsSnapshot = table.Column<int>(type: "INTEGER", nullable: true),
                ResultingProgressionWeightCentiKg = table.Column<int>(type: "INTEGER", nullable: true),
                ResultingProgressionTargetReps = table.Column<int>(type: "INTEGER", nullable: true),
                ResultingProgressionDurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionExercises", x => x.Id);
                table.ForeignKey(
                    name: "FK_SessionExercises_WorkoutSessions_WorkoutSessionId",
                    column: x => x.WorkoutSessionId,
                    principalTable: "WorkoutSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SessionSets",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SyncId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                SessionExerciseId = table.Column<long>(type: "INTEGER", nullable: false),
                SetOrder = table.Column<int>(type: "INTEGER", nullable: false),
                SetType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                IsPlanned = table.Column<bool>(type: "INTEGER", nullable: false),
                CountsForProgression = table.Column<bool>(type: "INTEGER", nullable: false),
                PrescribedWeightCentiKg = table.Column<int>(type: "INTEGER", nullable: true),
                PrescribedReps = table.Column<int>(type: "INTEGER", nullable: true),
                PrescribedDurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                ActualWeightCentiKg = table.Column<int>(type: "INTEGER", nullable: true),
                ActualReps = table.Column<int>(type: "INTEGER", nullable: true),
                ActualDurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionSets", x => x.Id);
                table.ForeignKey(
                    name: "FK_SessionSets_SessionExercises_SessionExerciseId",
                    column: x => x.SessionExerciseId,
                    principalTable: "SessionExercises",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WorkoutSessions_CompletedAt",
            table: "WorkoutSessions",
            column: "CompletedAt");
        migrationBuilder.CreateIndex(
            name: "IX_WorkoutSessions_SyncId",
            table: "WorkoutSessions",
            column: "SyncId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_SessionExercises_SourceProgressionTrackSyncId",
            table: "SessionExercises",
            column: "SourceProgressionTrackSyncId");
        migrationBuilder.CreateIndex(
            name: "IX_SessionExercises_SyncId",
            table: "SessionExercises",
            column: "SyncId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_SessionExercises_WorkoutSessionId_SortOrderSnapshot",
            table: "SessionExercises",
            columns: new[] { "WorkoutSessionId", "SortOrderSnapshot" });
        migrationBuilder.CreateIndex(
            name: "IX_SessionSets_SessionExerciseId_SetOrder",
            table: "SessionSets",
            columns: new[] { "SessionExerciseId", "SetOrder" });
        migrationBuilder.CreateIndex(
            name: "IX_SessionSets_SyncId",
            table: "SessionSets",
            column: "SyncId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SessionSets");
        migrationBuilder.DropTable(name: "SessionExercises");
        migrationBuilder.DropTable(name: "WorkoutSessions");
    }
}

