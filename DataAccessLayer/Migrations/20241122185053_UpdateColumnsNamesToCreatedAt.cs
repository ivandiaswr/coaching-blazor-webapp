using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateColumnsNamesToCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TimeStampInserted",
                table: "EmailSubscriptions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "TimeStampInserted",
                table: "Contacts",
                newName: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "EmailSubscriptions",
                newName: "TimeStampInserted");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Contacts",
                newName: "TimeStampInserted");
        }
    }
}
