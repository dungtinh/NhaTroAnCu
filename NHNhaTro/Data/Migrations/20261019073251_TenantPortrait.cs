using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NHNhaTro.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantPortrait : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Portrait",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Portrait",
                table: "Tenants");
        }
    }
}
