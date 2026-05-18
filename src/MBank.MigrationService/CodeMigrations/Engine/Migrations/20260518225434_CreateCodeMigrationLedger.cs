using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MBank.MigrationService.CodeMigrations.Engine.Migrations
{
    /// <inheritdoc />
    public partial class CreateCodeMigrationLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "__code_migrations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___code_migrations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "__code_migrations");
        }
    }
}
