using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborTorrent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnchorMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AnchorMaxDownloadSpeedBytes",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "AnchorMaxUploadSpeedBytes",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "AnchorModeEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnchorMaxDownloadSpeedBytes",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AnchorMaxUploadSpeedBytes",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "AnchorModeEnabled",
                table: "Settings");
        }
    }
}
