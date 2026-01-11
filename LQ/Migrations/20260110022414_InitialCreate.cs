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
            migrationBuilder.EnsureSchema(
                name: "analyst");

            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "Analyst",
                schema: "analyst",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "text", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analyst", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CSVFile",
                schema: "app",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnalystID = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CSVFile", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CSVFile_Analyst_AnalystID",
                        column: x => x.AnalystID,
                        principalSchema: "analyst",
                        principalTable: "Analyst",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Field",
                schema: "app",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
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
                        principalSchema: "app",
                        principalTable: "CSVFile",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Value",
                schema: "app",
                columns: table => new
                {
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    FieldID = table.Column<int>(type: "integer", nullable: false),
                    ValueStringFormatted = table.Column<string>(type: "text", nullable: true),
                    ValueString = table.Column<string>(type: "text", nullable: true),
                    Existance = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Value", x => new { x.FieldID, x.RowIndex });
                    table.ForeignKey(
                        name: "FK_Value_Field_FieldID",
                        column: x => x.FieldID,
                        principalSchema: "app",
                        principalTable: "Field",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CSVFile_AnalystID",
                schema: "app",
                table: "CSVFile",
                column: "AnalystID");

            migrationBuilder.CreateIndex(
                name: "IX_Field_CSVFileID",
                schema: "app",
                table: "Field",
                column: "CSVFileID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Value",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Field",
                schema: "app");

            migrationBuilder.DropTable(
                name: "CSVFile",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Analyst",
                schema: "analyst");
        }
    }
}
