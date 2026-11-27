using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NexusDbContext))]
    [Migration("20261124190000_AttendancePunches")]
    public partial class AttendancePunches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "ShiftEnd",
                table: "InternshipPolicy",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(17, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "ShiftName",
                table: "InternshipPolicy",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "Day shift");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ShiftStart",
                table: "InternshipPolicy",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(9, 0, 0));

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Departments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Departments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RadiusMeters",
                table: "Departments",
                type: "int",
                nullable: false,
                defaultValue: 200);

            // Existing departments start at the UGC office (Plus Code Q92J+RQ, Dhaka). Admin can change each one.
            migrationBuilder.Sql("UPDATE Departments SET Latitude = 23.7520625, Longitude = 90.3819375 WHERE Latitude IS NULL");

            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                table: "AttendanceRecords",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInAccuracyMeters",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInAtUtc",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInDistanceMeters",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckInKey",
                table: "AttendanceRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLatitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckInLongitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutAccuracyMeters",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckOutAtUtc",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutDistanceMeters",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckOutKey",
                table: "AttendanceRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLatitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CheckOutLongitude",
                table: "AttendanceRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "AttendanceRecords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date",
                table: "Holidays",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropColumn(
                name: "ShiftEnd",
                table: "InternshipPolicy");

            migrationBuilder.DropColumn(
                name: "ShiftName",
                table: "InternshipPolicy");

            migrationBuilder.DropColumn(
                name: "ShiftStart",
                table: "InternshipPolicy");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "RadiusMeters",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "AppVersion",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInAccuracyMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInAtUtc",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInDistanceMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInKey",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInLatitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckInLongitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutAccuracyMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutAtUtc",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutDistanceMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutKey",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "AttendanceRecords");
        }
    }
}
