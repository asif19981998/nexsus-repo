using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminWeekendDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WeekendDays",
                table: "InternshipPolicy",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Friday,Saturday");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekendDays",
                table: "InternshipPolicy");
        }
    }
}
