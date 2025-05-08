using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPricesRelatedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionPacks_SessionPackDefinitions_DefinitionId",
                table: "SessionPacks");

            migrationBuilder.RenameColumn(
                name: "DefinitionId",
                table: "SessionPacks",
                newName: "PriceId");

            migrationBuilder.RenameIndex(
                name: "IX_SessionPacks_DefinitionId",
                table: "SessionPacks",
                newName: "IX_SessionPacks_PriceId");

            migrationBuilder.AddColumn<int>(
                name: "PriceId",
                table: "UserSubscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SessionPackPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    TotalSessions = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceEUR = table.Column<decimal>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionPackPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    MonthlyLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceEUR = table.Column<decimal>(type: "TEXT", nullable: false),
                    StripePriceId = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPrices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PriceId",
                table: "UserSubscriptions",
                column: "PriceId");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionPacks_SessionPackPrices_PriceId",
                table: "SessionPacks",
                column: "PriceId",
                principalTable: "SessionPackPrices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPrices_PriceId",
                table: "UserSubscriptions",
                column: "PriceId",
                principalTable: "SubscriptionPrices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionPacks_SessionPackPrices_PriceId",
                table: "SessionPacks");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPrices_PriceId",
                table: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "SessionPackPrices");

            migrationBuilder.DropTable(
                name: "SubscriptionPrices");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_PriceId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "PriceId",
                table: "UserSubscriptions");

            migrationBuilder.RenameColumn(
                name: "PriceId",
                table: "SessionPacks",
                newName: "DefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_SessionPacks_PriceId",
                table: "SessionPacks",
                newName: "IX_SessionPacks_DefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionPacks_SessionPackDefinitions_DefinitionId",
                table: "SessionPacks",
                column: "DefinitionId",
                principalTable: "SessionPackDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
