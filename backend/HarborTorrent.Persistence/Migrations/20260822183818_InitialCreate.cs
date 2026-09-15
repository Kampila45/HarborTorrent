using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborTorrent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Torrents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InfoHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Progress = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    DownloadedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DownloadSpeedBytesPerSecond = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadSpeedBytesPerSecond = table.Column<long>(type: "INTEGER", nullable: false),
                    EtaSeconds = table.Column<long>(type: "INTEGER", nullable: true),
                    SavePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Ratio = table.Column<decimal>(type: "TEXT", precision: 8, scale: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    SourceKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceValue = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Torrents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Torrents_InfoHash",
                table: "Torrents",
                column: "InfoHash",
                unique: true,
                filter: "\"InfoHash\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Torrents");
        }
    }
}
