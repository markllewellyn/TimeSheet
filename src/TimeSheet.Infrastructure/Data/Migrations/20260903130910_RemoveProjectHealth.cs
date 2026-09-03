using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectHealthAssessments");

            migrationBuilder.DropColumn(
                name: "LatestHealthAssessmentId",
                table: "Projects");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LatestHealthAssessmentId",
                table: "Projects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectHealthAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    AiModelUsed = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AssessedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ContributingFactorsJson = table.Column<string>(type: "TEXT", nullable: false),
                    NotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    PercentBudgetConsumed = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    PercentTimeElapsed = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    RecommendedAction = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectHealthAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectHealthAssessments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHealthAssessments_ProjectId_AssessedAtUtc",
                table: "ProjectHealthAssessments",
                columns: new[] { "ProjectId", "AssessedAtUtc" });
        }
    }
}
