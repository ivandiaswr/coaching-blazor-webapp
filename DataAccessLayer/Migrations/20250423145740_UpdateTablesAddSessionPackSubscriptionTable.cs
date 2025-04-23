using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTablesAddSessionPackSubscriptionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalSessions",
                table: "SessionPacks",
                newName: "DefinitionId");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SubscriptionPlans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SessionPackDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    PriceEUR = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalSessions = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionPackDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionPacks_DefinitionId",
                table: "SessionPacks",
                column: "DefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionPacks_SessionPackDefinitions_DefinitionId",
                table: "SessionPacks",
                column: "DefinitionId",
                principalTable: "SessionPackDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionPacks_SessionPackDefinitions_DefinitionId",
                table: "SessionPacks");

            migrationBuilder.DropTable(
                name: "SessionPackDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_SessionPacks_DefinitionId",
                table: "SessionPacks");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SubscriptionPlans");

            migrationBuilder.RenameColumn(
                name: "DefinitionId",
                table: "SessionPacks",
                newName: "TotalSessions");
        }
    }
}
