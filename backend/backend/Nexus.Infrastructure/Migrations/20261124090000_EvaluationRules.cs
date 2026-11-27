using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>
    /// cl.14 evaluation rules as policy settings: when the department may evaluate, and the shortest recommendation.
    /// The defaults also fill the existing policy row.
    /// </summary>
    [DbContext(typeof(NexusDbContext))]
    [Migration("20261124090000_EvaluationRules")]
    public partial class EvaluationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EvaluationAfterReport",
                table: "InternshipPolicy",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MinRecommendationLength",
                table: "InternshipPolicy",
                type: "int",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvaluationAfterReport",
                table: "InternshipPolicy");

            migrationBuilder.DropColumn(
                name: "MinRecommendationLength",
                table: "InternshipPolicy");
        }
    }
}
