using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NHNhaTro.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompanyContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyContractId",
                table: "Leases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractPeriodId",
                table: "Bills",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Representative = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RepresentativeTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BankAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SignedDate = table.Column<DateTime>(type: "date", nullable: true),
                    RentMonthsPerPeriod = table.Column<int>(type: "int", nullable: false),
                    WaterPerRoom = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    MaxOccupantsPerRoom = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyContracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContractTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyContractId = table.Column<int>(type: "int", nullable: false),
                    DocumentNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SignedDate = table.Column<DateTime>(type: "date", nullable: true),
                    FromDate = table.Column<DateTime>(type: "date", nullable: false),
                    ToDate = table.Column<DateTime>(type: "date", nullable: false),
                    RentPerRoom = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    DepositPerRoom = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    RoomCodes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractTerms_CompanyContracts_CompanyContractId",
                        column: x => x.CompanyContractId,
                        principalTable: "CompanyContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractTermId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    From = table.Column<DateTime>(type: "date", nullable: false),
                    To = table.Column<DateTime>(type: "date", nullable: false),
                    PayFrom = table.Column<DateTime>(type: "date", nullable: false),
                    PayTo = table.Column<DateTime>(type: "date", nullable: false),
                    SettledOutside = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractPeriods_ContractTerms_ContractTermId",
                        column: x => x.ContractTermId,
                        principalTable: "ContractTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_CompanyContractId",
                table: "Leases",
                column: "CompanyContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_ContractPeriodId",
                table: "Bills",
                column: "ContractPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractPeriods_ContractTermId",
                table: "ContractPeriods",
                column: "ContractTermId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerms_CompanyContractId",
                table: "ContractTerms",
                column: "CompanyContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_ContractPeriods_ContractPeriodId",
                table: "Bills",
                column: "ContractPeriodId",
                principalTable: "ContractPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Leases_CompanyContracts_CompanyContractId",
                table: "Leases",
                column: "CompanyContractId",
                principalTable: "CompanyContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_ContractPeriods_ContractPeriodId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_Leases_CompanyContracts_CompanyContractId",
                table: "Leases");

            migrationBuilder.DropTable(
                name: "ContractPeriods");

            migrationBuilder.DropTable(
                name: "ContractTerms");

            migrationBuilder.DropTable(
                name: "CompanyContracts");

            migrationBuilder.DropIndex(
                name: "IX_Leases_CompanyContractId",
                table: "Leases");

            migrationBuilder.DropIndex(
                name: "IX_Bills_ContractPeriodId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CompanyContractId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ContractPeriodId",
                table: "Bills");
        }
    }
}
