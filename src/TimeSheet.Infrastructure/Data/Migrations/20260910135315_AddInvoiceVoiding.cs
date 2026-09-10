using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceVoiding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_ClientId_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Invoices",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAtUtc",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedByName",
                table: "Invoices",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoidedByUserId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "ClientId", "InvoiceNumber" },
                unique: true,
                filter: "\"Status\" IN ('Finalized', 'Voided')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_ClientId_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedByName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "ClientId", "InvoiceNumber" },
                unique: true,
                filter: "\"Status\" = 'Finalized'");
        }
    }
}
