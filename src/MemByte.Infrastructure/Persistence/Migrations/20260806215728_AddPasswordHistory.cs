using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemByte.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasswordHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordHistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordHistoryEntries_UserId_CreatedAt",
                table: "PasswordHistoryEntries",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordHistoryEntries");
        }
    }
}
