using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntryTypeId",
                table: "RecordedTimes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EntryTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsContractType = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryTypes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "PK_Projects",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordedTimes_EntryTypeId",
                table: "RecordedTimes",
                column: "EntryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryTypes_ProjectId_Name",
                table: "EntryTypes",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RecordedTimes_EntryTypes_EntryTypeId",
                table: "RecordedTimes",
                column: "EntryTypeId",
                principalTable: "EntryTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecordedTimes_EntryTypes_EntryTypeId",
                table: "RecordedTimes");

            migrationBuilder.DropTable(
                name: "EntryTypes");

            migrationBuilder.DropIndex(
                name: "IX_RecordedTimes_EntryTypeId",
                table: "RecordedTimes");

            migrationBuilder.DropColumn(
                name: "EntryTypeId",
                table: "RecordedTimes");
        }
    }
}
