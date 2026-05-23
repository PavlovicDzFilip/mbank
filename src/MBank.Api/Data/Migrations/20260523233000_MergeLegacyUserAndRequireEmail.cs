using MBank.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MBank.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MemoryDbContext))]
    [Migration("20260523233000_MergeLegacyUserAndRequireEmail")]
    public partial class MergeLegacyUserAndRequireEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    legacy_user_id uuid := '019e28ac-8a12-79e1-af6c-e4c829d78400';
                    target_email text := 'pavlovic.dz.filip@outlook.com';
                    target_user_id uuid;
                BEGIN
                    SELECT "Id" INTO target_user_id
                    FROM users
                    WHERE "Email" = target_email;

                    IF target_user_id IS NOT NULL
                       AND EXISTS (SELECT 1 FROM users WHERE "Id" = legacy_user_id) THEN
                        IF EXISTS (
                            SELECT 1
                            FROM access_tokens legacy_token
                            JOIN access_tokens target_token
                              ON target_token."UserId" = target_user_id
                             AND target_token."ProjectId" = legacy_token."ProjectId"
                             AND target_token."Name" = legacy_token."Name"
                            WHERE legacy_token."UserId" = legacy_user_id
                        ) THEN
                            RAISE EXCEPTION 'Cannot merge legacy user because duplicate token names would be created for the same project.';
                        END IF;

                        INSERT INTO project_memberships ("UserId", "ProjectId", "CreatedAt")
                        SELECT target_user_id, "ProjectId", "CreatedAt"
                        FROM project_memberships
                        WHERE "UserId" = legacy_user_id
                        ON CONFLICT ("UserId", "ProjectId") DO NOTHING;

                        UPDATE access_tokens
                        SET "UserId" = target_user_id
                        WHERE "UserId" = legacy_user_id;

                        UPDATE memories
                        SET "UserId" = target_user_id
                        WHERE "UserId" = legacy_user_id;

                        DELETE FROM project_memberships
                        WHERE "UserId" = legacy_user_id;

                        DELETE FROM users
                        WHERE "Id" = legacy_user_id;
                    END IF;

                    IF EXISTS (SELECT 1 FROM users WHERE "Email" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot require user emails while users with null email remain.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);
        }
    }
}
