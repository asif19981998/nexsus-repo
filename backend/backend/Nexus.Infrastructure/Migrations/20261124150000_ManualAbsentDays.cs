using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>
    /// cl.10: the mentor enters the month's absent days with the monthly evaluation; the deduction is worked out from them.
    /// Null on bills evaluated before this, which keep using the attendance register.
    /// </summary>
    [DbContext(typeof(NexusDbContext))]
    [Migration("20261124150000_ManualAbsentDays")]
    public partial class ManualAbsentDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbsentDays",
                table: "AllowanceBills",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AbsentDays", table: "AllowanceBills");
        }
    }
}
