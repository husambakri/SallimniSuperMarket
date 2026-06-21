using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sallimni.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MerchantStoreFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchId",
                table: "Merchants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryText",
                table: "Merchants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "Merchants",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTimeText",
                table: "Merchants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinOrder",
                table: "Merchants",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rating",
                table: "Merchants",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "CategoryText",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "DeliveryTimeText",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "MinOrder",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Merchants");
        }
    }
}
