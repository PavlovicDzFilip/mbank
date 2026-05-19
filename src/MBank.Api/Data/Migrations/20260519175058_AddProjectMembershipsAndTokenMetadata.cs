using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MBank.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMembershipsAndTokenMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_projects_Name",
                table: "projects");

            migrationBuilder.AddColumn<string>(
                name: "TokenPrefix",
                table: "access_tokens",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "mbp_");

            migrationBuilder.AddColumn<string>(
                name: "TokenSuffix",
                table: "access_tokens",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "project_memberships",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_memberships", x => new { x.UserId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_project_memberships_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO project_memberships ("UserId", "ProjectId", "CreatedAt")
                SELECT DISTINCT "UserId", "ProjectId", now()
                FROM access_tokens
                ON CONFLICT DO NOTHING;

                INSERT INTO project_memberships ("UserId", "ProjectId", "CreatedAt")
                SELECT DISTINCT "UserId", "ProjectId", now()
                FROM memories
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_projects_Name",
                table: "projects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_project_memberships_ProjectId",
                table: "project_memberships",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_access_tokens_project_memberships_UserId_ProjectId",
                table: "access_tokens",
                columns: new[] { "UserId", "ProjectId" },
                principalTable: "project_memberships",
                principalColumns: new[] { "UserId", "ProjectId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_access_tokens_project_memberships_UserId_ProjectId",
                table: "access_tokens");

            migrationBuilder.DropTable(
                name: "project_memberships");

            migrationBuilder.DropIndex(
                name: "IX_projects_Name",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "TokenPrefix",
                table: "access_tokens");

            migrationBuilder.DropColumn(
                name: "TokenSuffix",
                table: "access_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_projects_Name",
                table: "projects",
                column: "Name",
                unique: true);
        }
    }
}
