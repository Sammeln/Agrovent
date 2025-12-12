using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrovent.DAL.Migrations
{
    /// <inheritdoc />
    public partial class FixAssemblyStructureForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssemblyStructures_AssemblyStructures_ParentStructureId",
                table: "AssemblyStructures");

            migrationBuilder.AddForeignKey(
                name: "FK_AssemblyStructures_AssemblyStructures_ParentStructureId",
                table: "AssemblyStructures",
                column: "ParentStructureId",
                principalTable: "AssemblyStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssemblyStructures_AssemblyStructures_ParentStructureId",
                table: "AssemblyStructures");

            migrationBuilder.AddForeignKey(
                name: "FK_AssemblyStructures_AssemblyStructures_ParentStructureId",
                table: "AssemblyStructures",
                column: "ParentStructureId",
                principalTable: "AssemblyStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
