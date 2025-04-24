using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsLifeColumnSessionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLive",
                table: "Sessions");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Sessions");

            migrationBuilder.AddColumn<bool>(
                name: "IsLive",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
