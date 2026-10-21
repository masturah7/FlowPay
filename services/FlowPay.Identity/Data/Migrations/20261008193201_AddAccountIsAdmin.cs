using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowPay.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountIsAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "accounts");
        }
    }
}
