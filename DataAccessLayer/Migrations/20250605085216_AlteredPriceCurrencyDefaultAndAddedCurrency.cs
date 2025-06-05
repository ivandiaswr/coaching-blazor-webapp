using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AlteredPriceCurrencyDefaultAndAddedCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PriceEUR",
                table: "SubscriptionPrices",
                newName: "PriceGBP");

            migrationBuilder.RenameColumn(
                name: "PriceEUR",
                table: "SessionPrices",
                newName: "PriceGBP");

            migrationBuilder.RenameColumn(
                name: "PriceEUR",
                table: "SessionPackPrices",
                newName: "PriceGBP");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "SubscriptionPrices",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PriceConverted",
                table: "SubscriptionPrices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "SessionPrices",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "SubscriptionPrices");

            migrationBuilder.DropColumn(
                name: "PriceConverted",
                table: "SubscriptionPrices");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "SessionPrices");

            migrationBuilder.RenameColumn(
                name: "PriceGBP",
                table: "SubscriptionPrices",
                newName: "PriceEUR");

            migrationBuilder.RenameColumn(
                name: "PriceGBP",
                table: "SessionPrices",
                newName: "PriceEUR");

            migrationBuilder.RenameColumn(
                name: "PriceGBP",
                table: "SessionPackPrices",
                newName: "PriceEUR");
        }
    }
}
