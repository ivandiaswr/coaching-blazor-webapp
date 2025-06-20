using Microsoft.EntityFrameworkCore.Migrations;

namespace DataLayer.Migrations
{
    public partial class AddUniqueConstraintToSubscriptionPrice : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPrices_SessionType_StripePriceId",
                table: "SubscriptionPrices",
                columns: new[] { "SessionType", "StripePriceId" },
                unique: true,
                filter: "[StripePriceId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPrices_SessionType_StripePriceId",
                table: "SubscriptionPrices");
        }
    }
}