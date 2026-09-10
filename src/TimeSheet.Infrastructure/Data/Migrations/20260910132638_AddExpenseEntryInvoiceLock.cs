using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseEntryInvoiceLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InvoiceId",
                table: "ExpenseEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseEntries_InvoiceId",
                table: "ExpenseEntries",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseEntries_Invoices_InvoiceId",
                table: "ExpenseEntries",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseEntries_Invoices_InvoiceId",
                table: "ExpenseEntries");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseEntries_InvoiceId",
                table: "ExpenseEntries");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "ExpenseEntries");
        }
    }
}
