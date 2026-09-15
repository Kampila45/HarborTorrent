using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborTorrent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaxDownloadSpeedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxUploadSpeedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DefaultDownloadPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    EnableDht = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablePex = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableLpd = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
