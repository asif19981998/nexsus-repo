using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>
    /// cl.10: the monthly bill is reviewed on attendance, conduct, discipline and work skill (0–25 each).
    /// Bills evaluated before this keep only their total in Score.
    /// </summary>
    [DbContext(typeof(NexusDbContext))]
    [Migration("20261124180000_MonthlyReviewCriteria")]
    public partial class MonthlyReviewCriteria : Migration
    {
        private static readonly string[] Columns = ["AttendanceScore", "ConductScore", "DisciplineScore", "WorkSkillScore"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var c in Columns)
                migrationBuilder.AddColumn<int>(name: c, table: "AllowanceBills", type: "int", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var c in Columns)
                migrationBuilder.DropColumn(name: c, table: "AllowanceBills");
        }
    }
}
