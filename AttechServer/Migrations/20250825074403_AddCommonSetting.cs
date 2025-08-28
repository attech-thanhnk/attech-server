using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttechServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCommonSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LanguageContentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageContentCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LanguageContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContentKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ValueVi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "common"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageContents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LanguageContentCategories_Name",
                table: "LanguageContentCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LanguageContentCategory",
                table: "LanguageContentCategories",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageContent",
                table: "LanguageContents",
                columns: new[] { "Id", "Deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_LanguageContents_Category",
                table: "LanguageContents",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageContents_ContentKey",
                table: "LanguageContents",
                column: "ContentKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LanguageContentCategories");

            migrationBuilder.DropTable(
                name: "LanguageContents");
        }
    }
}
