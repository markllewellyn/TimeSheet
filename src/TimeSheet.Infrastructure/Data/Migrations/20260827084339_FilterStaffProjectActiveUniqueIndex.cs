using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FilterStaffProjectActiveUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffProjects_PK_StaffCosts_PK_Projects",
                table: "StaffProjects");

            migrationBuilder.CreateIndex(
                name: "IX_StaffProjects_PK_StaffCosts_PK_Projects",
                table: "StaffProjects",
                columns: new[] { "PK_StaffCosts", "PK_Projects" },
                unique: true,
                filter: "\"Active\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffProjects_PK_StaffCosts_PK_Projects",
                table: "StaffProjects");

            migrationBuilder.CreateIndex(
                name: "IX_StaffProjects_PK_StaffCosts_PK_Projects",
                table: "StaffProjects",
                columns: new[] { "PK_StaffCosts", "PK_Projects" },
                unique: true);
        }
    }
}
