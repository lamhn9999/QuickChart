using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LQ.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsAndDecoupleCanvas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -----------------------------------------------------------------
            // 1. Create the Project table (no inbound FKs yet)
            // -----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "Project",
                schema: "app",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicToken = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnalystID = table.Column<string>(type: "text", nullable: true),
                    GuestToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Project_Analyst_AnalystID",
                        column: x => x.AnalystID,
                        principalSchema: "analyst",
                        principalTable: "Analyst",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Project_AnalystID",
                schema: "app", table: "Project", column: "AnalystID");
            migrationBuilder.CreateIndex(
                name: "IX_Project_GuestToken",
                schema: "app", table: "Project", column: "GuestToken");
            migrationBuilder.CreateIndex(
                name: "IX_Project_PublicToken",
                schema: "app", table: "Project", column: "PublicToken", unique: true);

            // -----------------------------------------------------------------
            // 2. Backfill one "My Workspace" Project per existing analyst/guest
            //    that owns any CSVFile. ID column auto-fills from identity seq.
            // -----------------------------------------------------------------
            migrationBuilder.Sql(@"
                INSERT INTO app.""Project"" (""Name"", ""CreatedTime"", ""ModifiedTime"", ""AnalystID"", ""GuestToken"")
                SELECT 'My Workspace', NOW(), NOW(), s.""AnalystID"", NULL
                FROM (SELECT DISTINCT ""AnalystID"" FROM app.""CSVFile"" WHERE ""AnalystID"" IS NOT NULL) AS s;

                INSERT INTO app.""Project"" (""Name"", ""CreatedTime"", ""ModifiedTime"", ""AnalystID"", ""GuestToken"")
                SELECT 'My Workspace', NOW(), NOW(), NULL, s.""GuestToken""
                FROM (SELECT DISTINCT ""GuestToken"" FROM app.""CSVFile"" WHERE ""GuestToken"" IS NOT NULL) AS s;
            ");

            // -----------------------------------------------------------------
            // 3. Add ProjectID to CSVFile, backfill, then enforce NOT NULL + FK
            // -----------------------------------------------------------------
            migrationBuilder.AddColumn<int>(
                name: "ProjectID",
                schema: "app",
                table: "CSVFile",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE app.""CSVFile"" AS f
                SET ""ProjectID"" = p.""ID""
                FROM app.""Project"" AS p
                WHERE (f.""AnalystID"" IS NOT NULL AND f.""AnalystID"" = p.""AnalystID"")
                   OR (f.""GuestToken"" IS NOT NULL AND f.""GuestToken"" = p.""GuestToken"");
            ");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectID",
                schema: "app",
                table: "CSVFile",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CSVFile_ProjectID",
                schema: "app", table: "CSVFile", column: "ProjectID");

            migrationBuilder.AddForeignKey(
                name: "FK_CSVFile_Project_ProjectID",
                schema: "app",
                table: "CSVFile",
                column: "ProjectID",
                principalSchema: "app",
                principalTable: "Project",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            // -----------------------------------------------------------------
            // 4. Restructure Canvas: new PK + new columns, then drop CSVFileID.
            //    Drop the old PK/FK first so columns can be reshaped.
            // -----------------------------------------------------------------
            migrationBuilder.DropForeignKey(
                name: "FK_Canvas_CSVFile_CSVFileID",
                schema: "app",
                table: "Canvas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Canvas",
                schema: "app",
                table: "Canvas");

            // Add ID as plain integer (nullable) first; backfill with row-number;
            // promote to identity + PK afterwards. This avoids the duplicate-0
            // problem you get with AddColumn(defaultValue: 0).
            migrationBuilder.Sql(@"
                ALTER TABLE app.""Canvas"" ADD COLUMN ""ID"" integer;
                WITH numbered AS (
                    SELECT ctid, ROW_NUMBER() OVER (ORDER BY ""UpdatedAt"", ""CSVFileID"") AS rn
                    FROM app.""Canvas""
                )
                UPDATE app.""Canvas"" c
                SET ""ID"" = n.rn
                FROM numbered n
                WHERE c.ctid = n.ctid;
                ALTER TABLE app.""Canvas"" ALTER COLUMN ""ID"" SET NOT NULL;
                ALTER TABLE app.""Canvas"" ALTER COLUMN ""ID"" ADD GENERATED BY DEFAULT AS IDENTITY;
                SELECT setval(pg_get_serial_sequence('app.""Canvas""', 'ID'), COALESCE(MAX(""ID""), 0) + 1, false) FROM app.""Canvas"";
            ");

            migrationBuilder.AddColumn<Guid>(
                name: "PublicToken",
                schema: "app",
                table: "Canvas",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "app",
                table: "Canvas",
                type: "text",
                nullable: false,
                defaultValue: "Untitled canvas");

            migrationBuilder.AddColumn<int>(
                name: "ProjectID",
                schema: "app",
                table: "Canvas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastUsedCSVFileID",
                schema: "app",
                table: "Canvas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviewJson",
                schema: "app",
                table: "Canvas",
                type: "jsonb",
                nullable: true);

            // Backfill Canvas metadata from the file it used to be tied to.
            // After this, every legacy canvas has ProjectID, a sensible Name,
            // and a LastUsedCSVFileID pointer for the legacy session lookup.
            migrationBuilder.Sql(@"
                UPDATE app.""Canvas"" AS c
                SET ""ProjectID"" = f.""ProjectID"",
                    ""Name"" = f.""Name"" || ' — canvas',
                    ""LastUsedCSVFileID"" = c.""CSVFileID""
                FROM app.""CSVFile"" AS f
                WHERE c.""CSVFileID"" = f.""ID"";
            ");

            migrationBuilder.DropColumn(
                name: "CSVFileID",
                schema: "app",
                table: "Canvas");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectID",
                schema: "app",
                table: "Canvas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Canvas",
                schema: "app",
                table: "Canvas",
                column: "ID");

            migrationBuilder.CreateIndex(
                name: "IX_Canvas_PublicToken",
                schema: "app", table: "Canvas", column: "PublicToken", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Canvas_ProjectID",
                schema: "app", table: "Canvas", column: "ProjectID");
            migrationBuilder.CreateIndex(
                name: "IX_Canvas_LastUsedCSVFileID",
                schema: "app", table: "Canvas", column: "LastUsedCSVFileID");

            migrationBuilder.AddForeignKey(
                name: "FK_Canvas_Project_ProjectID",
                schema: "app",
                table: "Canvas",
                column: "ProjectID",
                principalSchema: "app",
                principalTable: "Project",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Canvas_CSVFile_LastUsedCSVFileID",
                schema: "app",
                table: "Canvas",
                column: "LastUsedCSVFileID",
                principalSchema: "app",
                principalTable: "CSVFile",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Destructive: down does not restore the CSVFileID-as-PK invariant
            // because the data needed (a single file per canvas) is no longer
            // guaranteed after Phase 4. Recover from backup if you must roll back.
            migrationBuilder.DropForeignKey(
                name: "FK_Canvas_CSVFile_LastUsedCSVFileID",
                schema: "app",
                table: "Canvas");

            migrationBuilder.DropForeignKey(
                name: "FK_Canvas_Project_ProjectID",
                schema: "app",
                table: "Canvas");

            migrationBuilder.DropForeignKey(
                name: "FK_CSVFile_Project_ProjectID",
                schema: "app",
                table: "CSVFile");

            migrationBuilder.DropIndex(name: "IX_Canvas_LastUsedCSVFileID", schema: "app", table: "Canvas");
            migrationBuilder.DropIndex(name: "IX_Canvas_ProjectID", schema: "app", table: "Canvas");
            migrationBuilder.DropIndex(name: "IX_Canvas_PublicToken", schema: "app", table: "Canvas");
            migrationBuilder.DropPrimaryKey(name: "PK_Canvas", schema: "app", table: "Canvas");
            migrationBuilder.DropIndex(name: "IX_CSVFile_ProjectID", schema: "app", table: "CSVFile");

            migrationBuilder.DropColumn(name: "ID", schema: "app", table: "Canvas");
            migrationBuilder.DropColumn(name: "PublicToken", schema: "app", table: "Canvas");
            migrationBuilder.DropColumn(name: "Name", schema: "app", table: "Canvas");
            migrationBuilder.DropColumn(name: "ProjectID", schema: "app", table: "Canvas");
            migrationBuilder.DropColumn(name: "LastUsedCSVFileID", schema: "app", table: "Canvas");
            migrationBuilder.DropColumn(name: "PreviewJson", schema: "app", table: "Canvas");
            migrationBuilder.DropColumn(name: "ProjectID", schema: "app", table: "CSVFile");

            migrationBuilder.DropTable(name: "Project", schema: "app");

            // Caller must restore Canvas.CSVFileID separately if they ever need this.
        }
    }
}
