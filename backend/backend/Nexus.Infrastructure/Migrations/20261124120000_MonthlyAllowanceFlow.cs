using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>
    /// cl.10 monthly allowance flow: monthly report → monthly evaluation → payment with a slip.
    /// Bills paid before this change get a slip number and the payer's name; their amount stays computed.
    /// </summary>
    [DbContext(typeof(NexusDbContext))]
    [Migration("20261124120000_MonthlyAllowanceFlow")]
    public partial class MonthlyAllowanceFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Report",
                table: "AllowanceBills",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "AllowanceBills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationNote",
                table: "AllowanceBills",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvaluatedAt",
                table: "AllowanceBills",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EvaluatedById",
                table: "AllowanceBills",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluatedByName",
                table: "AllowanceBills",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidByName",
                table: "AllowanceBills",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaidAmount",
                table: "AllowanceBills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlipNumber",
                table: "AllowanceBills",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // Same format as AllowanceBill.SlipNumberFor.
            migrationBuilder.Sql(@"
UPDATE b SET
    SlipNumber = CONCAT('ALW-', REPLACE(b.Month, '-', ''), '-', RIGHT(CONCAT('00000', b.InternshipId), 5)),
    PaidByName = u.FullName
FROM AllowanceBills b
LEFT JOIN Users u ON u.Id = b.PaidById
WHERE b.PaidAt IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceBills_SlipNumber",
                table: "AllowanceBills",
                column: "SlipNumber",
                unique: true,
                filter: "[SlipNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AllowanceBills_SlipNumber",
                table: "AllowanceBills");

            migrationBuilder.DropColumn(name: "Report", table: "AllowanceBills");
            migrationBuilder.DropColumn(name: "Score", table: "AllowanceBills");
            migrationBuilder.DropColumn(name: "EvaluationNote", table: "AllowanceBills");
            migrationBuilder.DropColumn(name: "EvaluatedAt", table: "AllowanceBills");
            migrationBuilder.DropColumn(name: "EvaluatedById", table: "AllowanceBills");
            migrationBuilder.DropColumn(name: "EvaluatedByName", table: "AllowanceBills");
            migrationBuilder.DropColumn(name: "PaidByName", table: "AllowanceBills");
            migrationBuilder.DropColumn(name: "PaidAmount", table: "AllowanceBills");
            migrationBuilder.DropColumn(name: "SlipNumber", table: "AllowanceBills");
        }
    }
}
