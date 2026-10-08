using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowPay.Transfers.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReconciliationAttempts",
                table: "transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReconciliationAttempts",
                table: "transfers");
        }
    }
}
