using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class AddedExistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Existance",
                table: "Value",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Existance",
                table: "Value");
        }
    }
}
