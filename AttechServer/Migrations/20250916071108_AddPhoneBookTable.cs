using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttechServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneBookTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhoneBooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneBooks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneBook",
                table: "PhoneBooks",
                columns: new[] { "Id", "Deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneBooks_Department",
                table: "PhoneBooks",
                column: "Department");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneBooks_Email",
                table: "PhoneBooks",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneBooks_Order",
                table: "PhoneBooks",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneBooks_Position",
                table: "PhoneBooks",
                column: "Position");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhoneBooks");
        }
    }
}
