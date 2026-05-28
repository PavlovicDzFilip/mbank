using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MBank.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectOpenAiCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_openai_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ApiKeySuffix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_openai_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_openai_credentials_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_openai_credentials_ProjectId",
                table: "project_openai_credentials",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_openai_credentials");
        }
    }
}
