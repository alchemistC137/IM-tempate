using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemByte.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogCategories_CatalogCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "CatalogCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogCategories_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PriceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PriceCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItems_CatalogCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CatalogCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogItems_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCategories_ClientId_Name",
                table: "CatalogCategories",
                columns: new[] { "ClientId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCategories_ParentCategoryId",
                table: "CatalogCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_CategoryId",
                table: "CatalogItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_ClientId_Name",
                table: "CatalogItems",
                columns: new[] { "ClientId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItems");

            migrationBuilder.DropTable(
                name: "CatalogCategories");
        }
    }
}
