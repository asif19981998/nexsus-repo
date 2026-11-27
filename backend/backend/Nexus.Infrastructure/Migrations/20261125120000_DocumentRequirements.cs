using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexus.Infrastructure.Migrations
{
    /// <summary>Which documents each programme asks for, and which document versions went with each application.</summary>
    [DbContext(typeof(NexusDbContext))]
    [Migration("20261125120000_DocumentRequirements")]
    public partial class DocumentRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    UserDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationDocuments_UserDocuments_UserDocumentId",
                        column: x => x.UserDocumentId,
                        principalTable: "UserDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModuleDocumentRequirements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Module = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GroupKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedById = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleDocumentRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleDocumentRequirements_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_Module_ApplicationId_UserDocumentId",
                table: "ApplicationDocuments",
                columns: new[] { "Module", "ApplicationId", "UserDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_UserDocumentId",
                table: "ApplicationDocuments",
                column: "UserDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleDocumentRequirements_DocumentTypeId",
                table: "ModuleDocumentRequirements",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleDocumentRequirements_Module_DocumentTypeId",
                table: "ModuleDocumentRequirements",
                columns: new[] { "Module", "DocumentTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationDocuments");

            migrationBuilder.DropTable(
                name: "ModuleDocumentRequirements");
        }
    }
}
