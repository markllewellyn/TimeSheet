using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveInvoicePdfToBlobStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfContent",
                table: "Invoices");

            migrationBuilder.AddColumn<string>(
                name: "PdfStorageKey",
                table: "Invoices",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfStorageKey",
                table: "Invoices");

            migrationBuilder.AddColumn<byte[]>(
                name: "PdfContent",
                table: "Invoices",
                type: "BLOB",
                nullable: true);
        }
    }
}
