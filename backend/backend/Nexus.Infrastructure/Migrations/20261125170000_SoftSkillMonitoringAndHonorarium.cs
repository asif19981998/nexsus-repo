using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>
    /// Soft Skill monitoring visits (cl.14 ঘ) and honoraria (cl.17). Honorarium rates on the policy row start at 0;
    /// the Commission sets them from the government's notices before anything is paid.
    /// </summary>
    public partial class SoftSkillMonitoringAndHonorarium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CoordinatorRatePerCohort",
                table: "SoftSkillPolicy",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpertRatePerVisit",
                table: "SoftSkillPolicy",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TrainerRatePerSession",
                table: "SoftSkillPolicy",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SoftSkillHonoraria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    PersonId = table.Column<long>(type: "bigint", nullable: false),
                    CohortId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidById = table.Column<long>(type: "bigint", nullable: true),
                    PaidByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SlipNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillHonoraria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillHonoraria_SoftSkillCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "SoftSkillCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftSkillHonoraria_Users_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftSkillMonitoringVisits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CohortId = table.Column<long>(type: "bigint", nullable: false),
                    MonitorId = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<long>(type: "bigint", nullable: true),
                    PlannedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VisitedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveryRating = table.Column<int>(type: "int", nullable: true),
                    EngagementRating = table.Column<int>(type: "int", nullable: true),
                    AttendanceRating = table.Column<int>(type: "int", nullable: true),
                    FacilitiesRating = table.Column<int>(type: "int", nullable: true),
                    Findings = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Recommendations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftSkillMonitoringVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftSkillMonitoringVisits_SoftSkillCohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "SoftSkillCohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftSkillMonitoringVisits_SoftSkillSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "SoftSkillSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftSkillMonitoringVisits_Users_MonitorId",
                        column: x => x.MonitorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillHonoraria_CohortId",
                table: "SoftSkillHonoraria",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillHonoraria_Kind_SourceId",
                table: "SoftSkillHonoraria",
                columns: new[] { "Kind", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillHonoraria_PersonId",
                table: "SoftSkillHonoraria",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillMonitoringVisits_CohortId_Status",
                table: "SoftSkillMonitoringVisits",
                columns: new[] { "CohortId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillMonitoringVisits_MonitorId_Status",
                table: "SoftSkillMonitoringVisits",
                columns: new[] { "MonitorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftSkillMonitoringVisits_SessionId",
                table: "SoftSkillMonitoringVisits",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoftSkillHonoraria");

            migrationBuilder.DropTable(
                name: "SoftSkillMonitoringVisits");

            migrationBuilder.DropColumn(
                name: "CoordinatorRatePerCohort",
                table: "SoftSkillPolicy");

            migrationBuilder.DropColumn(
                name: "ExpertRatePerVisit",
                table: "SoftSkillPolicy");

            migrationBuilder.DropColumn(
                name: "TrainerRatePerSession",
                table: "SoftSkillPolicy");
        }
    }
}
