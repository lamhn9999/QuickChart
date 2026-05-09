using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class AddSparklineJsonToField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SparklineJson",
                schema: "app",
                table: "Field",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SparklineTypeName",
                schema: "app",
                table: "Field",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SparklineJson",
                schema: "app",
                table: "Field");

            migrationBuilder.DropColumn(
                name: "SparklineTypeName",
                schema: "app",
                table: "Field");
        }
    }
}
