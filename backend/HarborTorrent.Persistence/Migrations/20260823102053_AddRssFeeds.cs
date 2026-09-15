using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborTorrent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRssFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RssFeedItemHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RssFeedId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RssFilterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemIdentifier = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RssFeedItemHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RssFeeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    LastPolledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RssFeeds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RssFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RssFeedId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RegexPattern = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SavePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RssFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RssFilters_RssFeeds_RssFeedId",
                        column: x => x.RssFeedId,
                        principalTable: "RssFeeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RssFeedItemHistories_RssFeedId_ItemIdentifier",
                table: "RssFeedItemHistories",
                columns: new[] { "RssFeedId", "ItemIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RssFilters_RssFeedId",
                table: "RssFilters",
                column: "RssFeedId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RssFeedItemHistories");

            migrationBuilder.DropTable(
                name: "RssFilters");

            migrationBuilder.DropTable(
                name: "RssFeeds");
        }
    }
}
