using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NHNhaTro.Data.Migrations
{
    /// <summary>
    /// Cách thu mới (19/10/2026): không quản lý chỉ số công tơ từng phòng, mỗi phiếu chỉ lưu số điện đã dùng (kWh);
    /// tiền cọc mặc định 1.000.000đ cho mọi khách. Số điện của phiếu cũ = số cuối − số đầu.
    /// </summary>
    public partial class SoDienTheoKwh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDeposit",
                table: "Settings",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 1000000m);

            migrationBuilder.AddColumn<int>(
                name: "ElectricityKwh",
                table: "Bills",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Chuyển dữ liệu cũ: số điện đã dùng = số cuối − số đầu (khi có cả hai)
            migrationBuilder.Sql("""
                UPDATE Bills SET ElectricityKwh = MeterEnd - MeterStart
                WHERE MeterEnd IS NOT NULL AND MeterStart IS NOT NULL AND MeterEnd >= MeterStart;
                """);

            migrationBuilder.DropColumn(
                name: "MeterReading",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "EndMeterReading",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "StartMeterReading",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "MeterEnd",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "MeterStart",
                table: "Bills");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MeterReading",
                table: "Rooms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EndMeterReading",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartMeterReading",
                table: "Leases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MeterEnd",
                table: "Bills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeterStart",
                table: "Bills",
                type: "int",
                nullable: true);

            // Không khôi phục được chỉ số đầu/cuối, chỉ giữ số đã dùng làm số cuối
            migrationBuilder.Sql("UPDATE Bills SET MeterStart = 0, MeterEnd = ElectricityKwh WHERE ElectricityKwh > 0;");

            migrationBuilder.DropColumn(
                name: "DefaultDeposit",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ElectricityKwh",
                table: "Bills");
        }
    }
}
