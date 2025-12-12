using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Analyst",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analyst", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CSVFile",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnalystID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CSVFile", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CSVFile_Analyst_AnalystID",
                        column: x => x.AnalystID,
                        principalTable: "Analyst",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Field",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    TypeName = table.Column<string>(type: "text", nullable: true),
                    Categories = table.Column<int[]>(type: "integer[]", nullable: false),
                    CSVFileID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Field", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Field_CSVFile_CSVFileID",
                        column: x => x.CSVFileID,
                        principalTable: "CSVFile",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Value",
                columns: table => new
                {
                    ValueString = table.Column<string>(type: "text", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    FieldID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_Value_Field_FieldID",
                        column: x => x.FieldID,
                        principalTable: "Field",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CSVFile_AnalystID",
                table: "CSVFile",
                column: "AnalystID");

            migrationBuilder.CreateIndex(
                name: "IX_Field_CSVFileID",
                table: "Field",
                column: "CSVFileID");

            migrationBuilder.CreateIndex(
                name: "IX_Value_FieldID",
                table: "Value",
                column: "FieldID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Value");

            migrationBuilder.DropTable(
                name: "Field");

            migrationBuilder.DropTable(
                name: "CSVFile");

            migrationBuilder.DropTable(
                name: "Analyst");
        }
    }
}
