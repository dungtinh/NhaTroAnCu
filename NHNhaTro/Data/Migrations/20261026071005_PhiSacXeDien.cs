using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NHNhaTro.Data.Migrations
{
    /// <inheritdoc />
    public partial class PhiSacXeDien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EvChargingFee",
                table: "Settings",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 100000m);

            migrationBuilder.AddColumn<bool>(
                name: "EvCharging",
                table: "Leases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "EvChargeAmount",
                table: "Bills",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvChargingFee",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "EvCharging",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "EvChargeAmount",
                table: "Bills");
        }
    }
}
