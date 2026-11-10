using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemByte.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Employees",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Employee");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Employees");
        }
    }
}
