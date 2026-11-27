using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>
    /// Education history moves to its own table, one line per degree, shared by every module. Each student's academic
    /// columns become their main education line; the columns then leave StudentProfiles. Down copies the main line back.
    /// </summary>
    [DbContext(typeof(NexusDbContext))]
    [Migration("20261125140000_Educations")]
    public partial class Educations : Migration
    {
        /// <summary>The first day of this month in Bangladesh (UTC+6), the same cut-off the application uses.</summary>
        private const string ThisMonthBd = "DATEFROMPARTS(YEAR(DATEADD(hour, 6, SYSUTCDATETIME())), MONTH(DATEADD(hour, 6, SYSUTCDATETIME())), 1)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Educations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Institution = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Programme = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentYear = table.Column<int>(type: "int", nullable: true),
                    ResultMonth = table.Column<DateOnly>(type: "date", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Educations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Educations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Educations_UserId",
                table: "Educations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Educations_UserId_Main",
                table: "Educations",
                column: "UserId",
                unique: true,
                filter: "[IsMain] = 1");

            // Undergraduate (1) → Bachelor (4), Postgraduate (2) → Master (5). Completed (2) when no year of study is set
            // and the graduation month has come; otherwise Studying (1).
            migrationBuilder.Sql($@"
INSERT INTO Educations (UserId, Level, Institution, Programme, Status, CurrentYear, ResultMonth, Result, IsMain,
                        CreatedAt, CreatedById, LastModifiedAt, LastModifiedById)
SELECT p.UserId,
       CASE WHEN p.Level = 2 THEN 5 ELSE 4 END,
       LTRIM(RTRIM(p.University)),
       p.Programme,
       CASE WHEN p.CurrentYear IS NULL AND p.GraduationMonth IS NOT NULL AND p.GraduationMonth <= {ThisMonthBd} THEN 2 ELSE 1 END,
       CASE WHEN p.CurrentYear IS NULL AND p.GraduationMonth IS NOT NULL AND p.GraduationMonth <= {ThisMonthBd} THEN NULL ELSE p.CurrentYear END,
       p.GraduationMonth,
       NULL,
       1,
       p.CreatedAt, p.CreatedById, p.LastModifiedAt, p.LastModifiedById
FROM StudentProfiles p
WHERE p.University IS NOT NULL AND LTRIM(RTRIM(p.University)) <> '';");

            migrationBuilder.DropColumn(name: "CurrentYear", table: "StudentProfiles");
            migrationBuilder.DropColumn(name: "GraduationMonth", table: "StudentProfiles");
            migrationBuilder.DropColumn(name: "Level", table: "StudentProfiles");
            migrationBuilder.DropColumn(name: "Programme", table: "StudentProfiles");
            migrationBuilder.DropColumn(name: "University", table: "StudentProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "CurrentYear", table: "StudentProfiles", type: "int", nullable: true);
            migrationBuilder.AddColumn<DateOnly>(name: "GraduationMonth", table: "StudentProfiles", type: "date", nullable: true);
            migrationBuilder.AddColumn<int>(name: "Level", table: "StudentProfiles", type: "int", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<string>(name: "Programme", table: "StudentProfiles", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "University", table: "StudentProfiles", type: "nvarchar(200)", maxLength: 200, nullable: true);

            migrationBuilder.Sql(@"
UPDATE p SET
    p.University = e.Institution,
    p.Programme = e.Programme,
    p.Level = CASE WHEN e.Level >= 5 THEN 2 ELSE 1 END,
    p.CurrentYear = CASE WHEN e.Status = 1 THEN e.CurrentYear ELSE NULL END,
    p.GraduationMonth = e.ResultMonth
FROM StudentProfiles p
JOIN Educations e ON e.UserId = p.UserId AND e.IsMain = 1;");

            migrationBuilder.DropTable(name: "Educations");
        }
    }
}
