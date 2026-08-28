using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSheet.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "RateCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "InvoiceLineItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "InvoiceLineItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            // Existing rows predate GrossAmount/DiscountPercent - backfill GrossAmount to match their existing
            // (undiscounted, since discounts didn't exist yet) Amount rather than leaving it at 0.
            migrationBuilder.Sql("UPDATE InvoiceLineItems SET GrossAmount = Amount;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "RateCards");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "InvoiceLineItems");
        }
    }
}
