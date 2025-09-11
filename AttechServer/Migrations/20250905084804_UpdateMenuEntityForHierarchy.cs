using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttechServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMenuEntityForHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CategoryType",
                table: "Menus",
                newName: "SourceType");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "Menus",
                newName: "SourceParentId");

            migrationBuilder.AddColumn<int>(
                name: "SourceId",
                table: "Menus",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "Menus");

            migrationBuilder.RenameColumn(
                name: "SourceType",
                table: "Menus",
                newName: "CategoryType");

            migrationBuilder.RenameColumn(
                name: "SourceParentId",
                table: "Menus",
                newName: "CategoryId");
        }
    }
}
