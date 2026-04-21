using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class GuestUploadSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AnalystID",
                schema: "app",
                table: "CSVFile",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "GuestToken",
                schema: "app",
                table: "CSVFile",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CSVFile_GuestToken",
                schema: "app",
                table: "CSVFile",
                column: "GuestToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CSVFile_GuestToken",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.DropColumn(
                name: "GuestToken",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.AlterColumn<string>(
                name: "AnalystID",
                schema: "app",
                table: "CSVFile",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
