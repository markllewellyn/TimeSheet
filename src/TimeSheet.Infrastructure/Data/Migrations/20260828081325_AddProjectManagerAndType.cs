using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectManagerAndType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectManagerUserId",
                table: "Projects",
                type: "INTEGER",
                nullable: true);

            // Existing rows predate ProjectType - default them to Development (the most common/generic case)
            // rather than an empty string, which wouldn't round-trip through the string-backed enum conversion.
            migrationBuilder.AddColumn<string>(
                name: "ProjectType",
                table: "Projects",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Development");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectManagerUserId",
                table: "Projects",
                column: "ProjectManagerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Staff_ProjectManagerUserId",
                table: "Projects",
                column: "ProjectManagerUserId",
                principalTable: "Staff",
                principalColumn: "PK_Staff",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Staff_ProjectManagerUserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjectManagerUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectManagerUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectType",
                table: "Projects");
        }
    }
}
