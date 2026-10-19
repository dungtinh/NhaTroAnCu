using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NHNhaTro.Data.Migrations
{
    /// <inheritdoc />
    public partial class TienDienNuocTong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UtilityBillId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UtilityBills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    ElectricityKwh = table.Column<int>(type: "int", nullable: true),
                    ElectricityAmount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    WaterM3 = table.Column<int>(type: "int", nullable: true),
                    WaterAmount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    PaidDate = table.Column<DateTime>(type: "date", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityBills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtilityBills_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UtilityBillId",
                table: "Transactions",
                column: "UtilityBillId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBills_AccountId",
                table: "UtilityBills",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityBills_Year_Month",
                table: "UtilityBills",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_UtilityBills_UtilityBillId",
                table: "Transactions",
                column: "UtilityBillId",
                principalTable: "UtilityBills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_UtilityBills_UtilityBillId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "UtilityBills");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UtilityBillId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UtilityBillId",
                table: "Transactions");
        }
    }
}
