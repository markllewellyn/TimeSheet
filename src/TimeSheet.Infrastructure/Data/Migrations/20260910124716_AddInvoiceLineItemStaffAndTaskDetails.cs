using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineItemStaffAndTaskDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Rate",
                table: "InvoiceLineItems",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StaffId",
                table: "InvoiceLineItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffName",
                table: "InvoiceLineItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TaskDate",
                table: "InvoiceLineItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rate",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "StaffId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "StaffName",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "TaskDate",
                table: "InvoiceLineItems");
        }
    }
}
