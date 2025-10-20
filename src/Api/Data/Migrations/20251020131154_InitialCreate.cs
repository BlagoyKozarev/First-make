using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoqItemName = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    CandidatesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedMatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", nullable: false),
                    BoqDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", nullable: false),
                    BoqItemId = table.Column<string>(type: "TEXT", nullable: false),
                    BoqItemName = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedCatalogueItemId = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedCatalogueItemName = table.Column<string>(type: "TEXT", nullable: false),
                    BasePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    MatchScore = table.Column<double>(type: "REAL", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApprovals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedMatch_BoqItemName_Unit",
                table: "CachedMatches",
                columns: new[] { "BoqItemName", "Unit" });

            migrationBuilder.CreateIndex(
                name: "IX_CachedMatch_CreatedAt",
                table: "CachedMatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SessionData_CreatedAt",
                table: "SessionData",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserApproval_BoqItemId",
                table: "UserApprovals",
                column: "BoqItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserApproval_SessionId",
                table: "UserApprovals",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedMatches");

            migrationBuilder.DropTable(
                name: "SessionData");

            migrationBuilder.DropTable(
                name: "UserApprovals");
        }
    }
}
