using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NHNhaTro.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuetBangAI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FPTReaderAPI");

            migrationBuilder.AddColumn<string>(
                name: "ClaudeApiKey",
                table: "Settings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaudeModel",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeminiApiKey",
                table: "Settings",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeminiModel",
                table: "Settings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanProvider",
                table: "Settings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaudeApiKey",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ClaudeModel",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "GeminiApiKey",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "GeminiModel",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ScanProvider",
                table: "Settings");

            migrationBuilder.CreateTable(
                name: "FPTReaderAPI",
                columns: table => new
                {
                    ApiKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FPTReaderAPI", x => new { x.ApiKey, x.Email, x.Status });
                });
        }
    }
}
