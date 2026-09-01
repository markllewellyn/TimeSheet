using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollPeriodAndClientBillingPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingPeriod",
                table: "Customers",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "OneOff");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CurrentPeriodEnd",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CurrentPeriodStart",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TotalOutOfHoursHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalOutOfHoursPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriodLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PayrollPeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    OutOfHoursHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OutOfHoursPay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriodLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodLines_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodLines_Staff_UserId",
                        column: x => x.UserId,
                        principalTable: "Staff",
                        principalColumn: "PK_Staff",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodLines_PayrollPeriodId_UserId",
                table: "PayrollPeriodLines",
                columns: new[] { "PayrollPeriodId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodLines_UserId",
                table: "PayrollPeriodLines",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_PeriodStart",
                table: "PayrollPeriods",
                column: "PeriodStart",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollPeriodLines");

            migrationBuilder.DropTable(
                name: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEnd",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodStart",
                table: "Customers");
        }
    }
}
