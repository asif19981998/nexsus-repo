using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>
    /// Renames the Soft Skill "cohort" to "university group" in place: the table, every UniversityGroupId column, their
    /// keys and indexes, the coordinator's honorarium rate, and the stored permission codes, audit entries and
    /// notification links. Renames only, so no row is lost.
    /// </summary>
    public partial class RenameCohortToUniversityGroup : Migration
    {
        // Table holding a UniversityGroupId column, the rest of its index name, and its delete rule.
        private static readonly (string Table, string IndexSuffix, ReferentialAction OnDelete)[] Children =
        [
            ("SoftSkillApplications", "_Status", ReferentialAction.Restrict),
            ("SoftSkillAssessments", "_No", ReferentialAction.Cascade),
            ("SoftSkillHonoraria", "", ReferentialAction.Restrict),
            ("SoftSkillMonitoringVisits", "_Status", ReferentialAction.Restrict),
            ("SoftSkillSessions", "_Date", ReferentialAction.Cascade),
            ("SoftSkillTrainees", "", ReferentialAction.Restrict),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, "Cohort", "UniversityGroup");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, "UniversityGroup", "Cohort");

        private static void Rename(MigrationBuilder mb, string from, string to)
        {
            string oldTable = $"SoftSkill{from}s", newTable = $"SoftSkill{to}s";

            foreach (var (table, _, _) in Children)
                mb.DropForeignKey(name: $"FK_{table}_{oldTable}_{from}Id", table: table);

            mb.RenameTable(name: oldTable, newName: newTable);
            mb.Sql($"EXEC sp_rename N'PK_{oldTable}', N'PK_{newTable}', N'OBJECT';");
            foreach (var fk in new[] { "SoftSkillBatches_BatchId", "Universities_UniversityId", "Users_CoordinatorId" })
                mb.Sql($"EXEC sp_rename N'FK_{oldTable}_{fk}', N'FK_{newTable}_{fk}', N'OBJECT';");
            foreach (var ix in new[] { "BatchId_UniversityId", "CoordinatorId", "UniversityId" })
                mb.RenameIndex(name: $"IX_{oldTable}_{ix}", table: newTable, newName: $"IX_{newTable}_{ix}");

            foreach (var (table, suffix, onDelete) in Children)
            {
                mb.RenameColumn(name: $"{from}Id", table: table, newName: $"{to}Id");
                mb.RenameIndex(name: $"IX_{table}_{from}Id{suffix}", table: table, newName: $"IX_{table}_{to}Id{suffix}");
                mb.AddForeignKey(
                    name: $"FK_{table}_{newTable}_{to}Id",
                    table: table,
                    column: $"{to}Id",
                    principalTable: newTable,
                    principalColumn: "Id",
                    onDelete: onDelete);
            }

            mb.RenameColumn(name: $"CoordinatorRatePer{from}", table: "SoftSkillPolicy", newName: $"CoordinatorRatePer{to}");

            // Stored strings: permission codes (role grants follow the row), audit entries and notification links.
            bool up = from == "Cohort";
            string oldCode = up ? "softskill.cohort." : "softskill.university_group.";
            string newCode = up ? "softskill.university_group." : "softskill.cohort.";
            string oldLink = up ? "/softskill/cohorts/" : "/softskill/university-groups/";
            string newLink = up ? "/softskill/university-groups/" : "/softskill/cohorts/";
            mb.Sql($"UPDATE Permissions SET Code = REPLACE(Code, '{oldCode}', '{newCode}') WHERE Code LIKE '{oldCode}%';");
            mb.Sql($"UPDATE AuditLogs SET Action = REPLACE(Action, '{oldCode}', '{newCode}') WHERE Action LIKE '{oldCode}%';");
            mb.Sql($"UPDATE AuditLogs SET EntityType = 'SoftSkill{to}' WHERE EntityType = 'SoftSkill{from}';");
            mb.Sql($"UPDATE Notifications SET Link = REPLACE(Link, '{oldLink}', '{newLink}') WHERE Link LIKE '{oldLink}%';");
        }
    }
}
