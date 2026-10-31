using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NHNhaTro.Data.Migrations
{
    /// <inheritdoc />
    public partial class NgayVaoDaXacNhan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StartDateConfirmed",
                table: "Leases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Hợp đồng lập trên phần mềm: ngày vào là ngày thật. Khách cũ nhập ban đầu mang ngày vào tạm = ngày chốt của kỳ lúc nhập
            // (ngày nhập >= ngày chốt thì kỳ tháng đó, ngược lại kỳ tháng trước); ngày vào đã khác ngày tạm nghĩa là chủ nhà đã sửa thành ngày thật.
            // Phòng thuộc hợp đồng công ty: ngày vào là ngày bắt đầu hợp đồng.
            migrationBuilder.Sql("""
                UPDATE l SET StartDateConfirmed = 1
                FROM Leases l CROSS JOIN Settings s
                WHERE l.IsExistingTenant = 0
                   OR l.CompanyContractId IS NOT NULL
                   OR CAST(l.StartDate AS date) <> DATEFROMPARTS(
                        YEAR(DATEADD(month, CASE WHEN DAY(l.CreatedAt) >= s.BillingDay THEN 0 ELSE -1 END, l.CreatedAt)),
                        MONTH(DATEADD(month, CASE WHEN DAY(l.CreatedAt) >= s.BillingDay THEN 0 ELSE -1 END, l.CreatedAt)),
                        s.BillingDay);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartDateConfirmed",
                table: "Leases");
        }
    }
}
