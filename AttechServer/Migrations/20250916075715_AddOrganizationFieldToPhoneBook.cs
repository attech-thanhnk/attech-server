using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttechServer.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationFieldToPhoneBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Organization",
                table: "PhoneBooks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneBooks_Organization",
                table: "PhoneBooks",
                column: "Organization");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoneBooks_Organization",
                table: "PhoneBooks");

            migrationBuilder.DropColumn(
                name: "Organization",
                table: "PhoneBooks");
        }
    }
}
