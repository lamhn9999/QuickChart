using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePreviewJsonWithPreviewPng : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewJson",
                schema: "app",
                table: "Canvas");

            migrationBuilder.AddColumn<byte[]>(
                name: "PreviewPng",
                schema: "app",
                table: "Canvas",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewPng",
                schema: "app",
                table: "Canvas");

            migrationBuilder.AddColumn<string>(
                name: "PreviewJson",
                schema: "app",
                table: "Canvas",
                type: "text",
                nullable: true);
        }
    }
}
