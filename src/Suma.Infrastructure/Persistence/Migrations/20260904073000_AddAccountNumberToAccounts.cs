using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountNumberToAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "account_number",
                table: "accounts",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "account_number",
                table: "accounts");
        }
    }
}
