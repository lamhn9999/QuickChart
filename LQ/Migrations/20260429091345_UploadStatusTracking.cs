using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class UploadStatusTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CSVFile_AnalystID",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.AddColumn<string>(
                name: "ProcessingPayload",
                schema: "app",
                table: "CSVFile",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicToken",
                schema: "app",
                table: "CSVFile",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "app",
                table: "CSVFile",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_CSVFile_AnalystID_Status",
                schema: "app",
                table: "CSVFile",
                columns: new[] { "AnalystID", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CSVFile_GuestToken_Status",
                schema: "app",
                table: "CSVFile",
                columns: new[] { "GuestToken", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CSVFile_PublicToken",
                schema: "app",
                table: "CSVFile",
                column: "PublicToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CSVFile_AnalystID_Status",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.DropIndex(
                name: "IX_CSVFile_GuestToken_Status",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.DropIndex(
                name: "IX_CSVFile_PublicToken",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.DropColumn(
                name: "ProcessingPayload",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.DropColumn(
                name: "PublicToken",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.CreateIndex(
                name: "IX_CSVFile_AnalystID",
                schema: "app",
                table: "CSVFile",
                column: "AnalystID");
        }
    }
}
