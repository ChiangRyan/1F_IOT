using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SANJET.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Devices",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Display");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Area",
                table: "Devices");
        }
    }
}
