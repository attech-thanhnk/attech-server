using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttechServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "ProductCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "ProductCategories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "NotificationCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "NotificationCategories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "NewsCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "NewsCategories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Order",
                table: "ProductCategories",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_ParentId",
                table: "ProductCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationCategories_Order",
                table: "NotificationCategories",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationCategories_ParentId",
                table: "NotificationCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsCategories_Order",
                table: "NewsCategories",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_NewsCategories_ParentId",
                table: "NewsCategories",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_NewsCategories_NewsCategories_ParentId",
                table: "NewsCategories",
                column: "ParentId",
                principalTable: "NewsCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationCategories_NotificationCategories_ParentId",
                table: "NotificationCategories",
                column: "ParentId",
                principalTable: "NotificationCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCategories_ProductCategories_ParentId",
                table: "ProductCategories",
                column: "ParentId",
                principalTable: "ProductCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NewsCategories_NewsCategories_ParentId",
                table: "NewsCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationCategories_NotificationCategories_ParentId",
                table: "NotificationCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductCategories_ProductCategories_ParentId",
                table: "ProductCategories");

            migrationBuilder.DropIndex(
                name: "IX_ProductCategories_Order",
                table: "ProductCategories");

            migrationBuilder.DropIndex(
                name: "IX_ProductCategories_ParentId",
                table: "ProductCategories");

            migrationBuilder.DropIndex(
                name: "IX_NotificationCategories_Order",
                table: "NotificationCategories");

            migrationBuilder.DropIndex(
                name: "IX_NotificationCategories_ParentId",
                table: "NotificationCategories");

            migrationBuilder.DropIndex(
                name: "IX_NewsCategories_Order",
                table: "NewsCategories");

            migrationBuilder.DropIndex(
                name: "IX_NewsCategories_ParentId",
                table: "NewsCategories");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "NotificationCategories");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "NotificationCategories");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "NewsCategories");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "NewsCategories");
        }
    }
}
