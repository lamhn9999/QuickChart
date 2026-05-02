using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class AddCanvasPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Canvas",
                schema: "app",
                columns: table => new
                {
                    CSVFileID = table.Column<int>(type: "integer", nullable: false),
                    Document = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Canvas", x => x.CSVFileID);
                    table.ForeignKey(
                        name: "FK_Canvas_CSVFile_CSVFileID",
                        column: x => x.CSVFileID,
                        principalSchema: "app",
                        principalTable: "CSVFile",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Canvas",
                schema: "app");
        }
    }
}
