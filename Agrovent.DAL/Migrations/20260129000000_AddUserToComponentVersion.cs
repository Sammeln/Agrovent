using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace Agrovent.DAL.Migrations
{
    public partial class AddUserToComponentVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Создаем таблицу Users
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    Patronymic = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // Добавляем колонку SavedByUserId в таблицу ComponentVersions
            migrationBuilder.AddColumn<int>(
                name: "SavedByUserId",
                table: "ComponentVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Добавляем внешний ключ на таблицу Users
            migrationBuilder.CreateIndex(
                name: "IX_ComponentVersions_SavedByUserId",
                table: "ComponentVersions",
                column: "SavedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComponentVersions_Users_SavedByUserId",
                table: "ComponentVersions",
                column: "SavedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Удаляем внешний ключ
            migrationBuilder.DropForeignKey(
                name: "FK_ComponentVersions_Users_SavedByUserId",
                table: "ComponentVersions");

            // Удаляем индекс
            migrationBuilder.DropIndex(
                name: "IX_ComponentVersions_SavedByUserId",
                table: "ComponentVersions");

            // Удаляем колонку SavedByUserId
            migrationBuilder.DropColumn(
                name: "SavedByUserId",
                table: "ComponentVersions");

            // Удаляем таблицу Users
            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
