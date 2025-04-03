using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSessionVideoSessionTableConn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Sessions");

            migrationBuilder.AddColumn<int>(
                name: "SessionRefId",
                table: "VideoSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoSessions_SessionRefId",
                table: "VideoSessions",
                column: "SessionRefId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoSessions_Sessions_SessionRefId",
                table: "VideoSessions",
                column: "SessionRefId",
                principalTable: "Sessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoSessions_Sessions_SessionRefId",
                table: "VideoSessions");

            migrationBuilder.DropIndex(
                name: "IX_VideoSessions_SessionRefId",
                table: "VideoSessions");

            migrationBuilder.DropColumn(
                name: "SessionRefId",
                table: "VideoSessions");

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
