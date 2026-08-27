using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryFlagAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Escalations -> EntryFlags is a data-preserving rename/reshape, not a drop+recreate - every
            // existing escalation becomes an EntryFlag (Pending -> open, Approved/Declined -> cleared). Pure
            // RenameTable/AddColumn/RenameColumn/DropColumn ops on a table with no legacy shape or FK/check
            // constraints translate directly on SQLite without needing a raw-SQL table rebuild.
            migrationBuilder.RenameTable(name: "Escalations", newName: "EntryFlags");

            migrationBuilder.AddColumn<bool>(
                name: "IsCleared", table: "EntryFlags", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>(
                name: "RaisedByUserId", table: "EntryFlags", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "RaisedNotes", table: "EntryFlags", type: "TEXT", maxLength: 1000, nullable: true);

            // Decision still exists at this point - safe to read before it's dropped below. All prior
            // escalations were system-raised, so RaisedByUserId stays NULL for every existing row.
            migrationBuilder.Sql("UPDATE EntryFlags SET IsCleared = CASE WHEN Decision <> 'Pending' THEN 1 ELSE 0 END;");

            migrationBuilder.RenameColumn(name: "DecidedByUserId", table: "EntryFlags", newName: "ClearedByUserId");
            migrationBuilder.RenameColumn(name: "DecidedAtUtc", table: "EntryFlags", newName: "ClearedAtUtc");
            migrationBuilder.RenameColumn(name: "DecisionNotes", table: "EntryFlags", newName: "ClearedNotes");

            migrationBuilder.DropIndex(name: "IX_Escalations_Decision", table: "EntryFlags");
            migrationBuilder.DropColumn(name: "Decision", table: "EntryFlags");

            // SQLite doesn't rename indexes/FK-constraint names along with RenameTable - drop and recreate so
            // they match what a fresh EntryFlags schema would produce.
            migrationBuilder.DropIndex(name: "IX_Escalations_ProjectId", table: "EntryFlags");
            migrationBuilder.DropIndex(name: "IX_Escalations_TimesheetEntryId", table: "EntryFlags");
            migrationBuilder.CreateIndex(name: "IX_EntryFlags_IsCleared", table: "EntryFlags", column: "IsCleared");
            migrationBuilder.CreateIndex(name: "IX_EntryFlags_ProjectId", table: "EntryFlags", column: "ProjectId");
            migrationBuilder.CreateIndex(name: "IX_EntryFlags_TimesheetEntryId", table: "EntryFlags", column: "TimesheetEntryId");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RecordedTimes");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ImpersonatedUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImpersonatedUserDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ImpersonatedUserId",
                table: "AuditLogs",
                column: "ImpersonatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");
        }

        /// <summary>Best-effort schema reversal only - NOT data-preserving (EntryFlag rows raised/cleared after
        /// Up() has no lossless Escalation-shape equivalent to roll back to, and AuditLog rows are simply
        /// discarded). Same convention as the AddRoleAndRateCard migration's Down().</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "EntryFlags");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RecordedTimes",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Escalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    TimesheetEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    BudgetLimitAtTimeOfEntry = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CumulativeValueAtTimeOfEntry = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DecisionNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RaisedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Escalations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Escalations_RecordedTimes_TimesheetEntryId",
                        column: x => x.TimesheetEntryId,
                        principalTable: "RecordedTimes",
                        principalColumn: "PK_RecordedTimes",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_Decision",
                table: "Escalations",
                column: "Decision");

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_ProjectId",
                table: "Escalations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Escalations_TimesheetEntryId",
                table: "Escalations",
                column: "TimesheetEntryId");
        }
    }
}
