using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LockTimesheetEntriesOnInvoiceFinalize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InvoiceId",
                table: "RecordedTimes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordedTimes_InvoiceId",
                table: "RecordedTimes",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecordedTimes_Invoices_InvoiceId",
                table: "RecordedTimes",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecordedTimes_Invoices_InvoiceId",
                table: "RecordedTimes");

            migrationBuilder.DropIndex(
                name: "IX_RecordedTimes_InvoiceId",
                table: "RecordedTimes");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "RecordedTimes");
        }
    }
}
