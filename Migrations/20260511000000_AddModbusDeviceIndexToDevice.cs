using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SANJET.Migrations
{
    /// <inheritdoc />
    public partial class AddModbusDeviceIndexToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModbusDeviceIndex",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModbusDeviceIndex",
                table: "Devices");
        }
    }
}
