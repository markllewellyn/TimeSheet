using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBudgetNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetAlertThresholdPercent",
                table: "Projects");

            migrationBuilder.AddColumn<int>(
                name: "HighestBudgetNotificationPercent",
                table: "Projects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectBudgetAlertThresholdPercent",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProjectBudgetWarningThresholdPercent",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ProjectBudgetAlertThresholdPercent", "ProjectBudgetWarningThresholdPercent" },
                values: new object[] { 75, 50 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighestBudgetNotificationPercent",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectBudgetAlertThresholdPercent",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ProjectBudgetWarningThresholdPercent",
                table: "AppSettings");

            migrationBuilder.AddColumn<int>(
                name: "BudgetAlertThresholdPercent",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
