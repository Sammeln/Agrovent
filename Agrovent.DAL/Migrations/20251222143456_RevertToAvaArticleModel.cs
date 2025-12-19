using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrovent.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RevertToAvaArticleModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComponentVersions_AvaArticles_AvaArticleArticle",
                table: "ComponentVersions");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Components_PartNumber",
                table: "Components",
                column: "PartNumber");

            migrationBuilder.CreateTable(
                name: "technological_processes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technological_processes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_technological_processes_Components_part_number",
                        column: x => x.part_number,
                        principalTable: "Components",
                        principalColumn: "PartNumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    technological_process_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    section = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    labor_intensity_minutes = table.Column<decimal>(type: "numeric", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operations_technological_processes_technological_process_id",
                        column: x => x.technological_process_id,
                        principalTable: "technological_processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operations_technological_process_id",
                table: "operations",
                column: "technological_process_id");

            migrationBuilder.CreateIndex(
                name: "IX_operations_technological_process_id_sequence_number",
                table: "operations",
                columns: new[] { "technological_process_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_technological_processes_part_number",
                table: "technological_processes",
                column: "part_number",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ComponentVersions_AvaArticles_AvaArticleArticle",
                table: "ComponentVersions",
                column: "AvaArticleArticle",
                principalTable: "AvaArticles",
                principalColumn: "Article",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComponentVersions_AvaArticles_AvaArticleArticle",
                table: "ComponentVersions");

            migrationBuilder.DropTable(
                name: "operations");

            migrationBuilder.DropTable(
                name: "technological_processes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Components_PartNumber",
                table: "Components");

            migrationBuilder.AddForeignKey(
                name: "FK_ComponentVersions_AvaArticles_AvaArticleArticle",
                table: "ComponentVersions",
                column: "AvaArticleArticle",
                principalTable: "AvaArticles",
                principalColumn: "Article");
        }
    }
}
