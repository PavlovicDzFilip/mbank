using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace MBank.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMemorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "source_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceText = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ConversationUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SubjectKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SearchText = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    SourceRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_memories_source_records_SourceRecordId",
                        column: x => x.SourceRecordId,
                        principalTable: "source_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "memory_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromMemoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToMemoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Relation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_memory_links_memories_FromMemoryId",
                        column: x => x.FromMemoryId,
                        principalTable: "memories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_links_memories_ToMemoryId",
                        column: x => x.ToMemoryId,
                        principalTable: "memories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_memories_Embedding",
                table: "memories",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);

            migrationBuilder.CreateIndex(
                name: "IX_memories_SourceRecordId",
                table: "memories",
                column: "SourceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_memories_Status",
                table: "memories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_memories_Subject",
                table: "memories",
                column: "Subject");

            migrationBuilder.CreateIndex(
                name: "IX_memories_SubjectKind",
                table: "memories",
                column: "SubjectKind");

            migrationBuilder.CreateIndex(
                name: "IX_memories_Type",
                table: "memories",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_memory_links_FromMemoryId_ToMemoryId",
                table: "memory_links",
                columns: new[] { "FromMemoryId", "ToMemoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memory_links_ToMemoryId",
                table: "memory_links",
                column: "ToMemoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "memory_links");

            migrationBuilder.DropTable(
                name: "memories");

            migrationBuilder.DropTable(
                name: "source_records");
        }
    }
}
