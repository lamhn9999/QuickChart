using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class FixedValueKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Value_FieldID",
                table: "Value");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Value",
                table: "Value",
                columns: new[] { "FieldID", "RowIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Value",
                table: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_Value_FieldID",
                table: "Value",
                column: "FieldID");
        }
    }
}
