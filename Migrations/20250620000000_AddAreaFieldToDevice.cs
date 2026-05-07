using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SANJET.Migrations
{
    /// <inheritdoc />
    public partial class AddAreaFieldToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Devices",
                type: "TEXT",
                nullable: false,
                defaultValue: "展機區");
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
