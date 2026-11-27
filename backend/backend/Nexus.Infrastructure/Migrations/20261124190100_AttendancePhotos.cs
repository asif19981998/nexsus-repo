using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NexusDbContext))]
    [Migration("20261124190100_AttendancePhotos")]
    public partial class AttendancePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckInPhotoPath",
                table: "AttendanceRecords",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckOutPhotoPath",
                table: "AttendanceRecords",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInPhotoPath",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutPhotoPath",
                table: "AttendanceRecords");
        }
    }
}
