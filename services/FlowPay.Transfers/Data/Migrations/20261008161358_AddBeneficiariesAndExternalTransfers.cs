using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowPay.Transfers.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBeneficiariesAndExternalTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ToWalletId",
                table: "transfers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "DestinationType",
                table: "transfers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "InternalWallet");

            migrationBuilder.AddColumn<string>(
                name: "ExternalBankAccountNumber",
                table: "transfers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalBankName",
                table: "transfers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FeeLedgerReference",
                table: "transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FeeMinorUnits",
                table: "transfers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "ToBeneficiaryId",
                table: "transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "beneficiaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beneficiaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_beneficiaries_AccountId",
                table: "beneficiaries",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beneficiaries");

            migrationBuilder.DropColumn(
                name: "DestinationType",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "ExternalBankAccountNumber",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "ExternalBankName",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "FeeLedgerReference",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "FeeMinorUnits",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "ToBeneficiaryId",
                table: "transfers");

            migrationBuilder.AlterColumn<Guid>(
                name: "ToWalletId",
                table: "transfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
