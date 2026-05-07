using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace MBank.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "memories",
                type: "tsvector",
                nullable: false)
                .Annotation("Npgsql:TsVectorConfig", "english")
                .Annotation("Npgsql:TsVectorProperties", new[] { "SearchText" });

            migrationBuilder.CreateIndex(
                name: "IX_memories_SearchVector",
                table: "memories",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memories_SearchVector",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "memories");
        }
    }
}
