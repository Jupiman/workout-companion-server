using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkoutCompanion.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_CompletedAt_Id",
                table: "WorkoutSessions",
                columns: new[] { "CompletedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_ProgramNameSnapshot",
                table: "WorkoutSessions",
                column: "ProgramNameSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_SessionExercises_ExerciseNameSnapshot",
                table: "SessionExercises",
                column: "ExerciseNameSnapshot");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutSessions_CompletedAt_Id",
                table: "WorkoutSessions");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutSessions_ProgramNameSnapshot",
                table: "WorkoutSessions");

            migrationBuilder.DropIndex(
                name: "IX_SessionExercises_ExerciseNameSnapshot",
                table: "SessionExercises");
        }
    }
}
