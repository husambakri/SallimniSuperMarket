using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sallimni.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTalabatPriceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TalabatPriceIndex",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<string>(type: "text", nullable: false),
                    StoreName = table.Column<string>(type: "text", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Special = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InStock = table.Column<bool>(type: "boolean", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    ProductUrl = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TalabatPriceIndex", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TalabatPriceIndex_Barcode",
                table: "TalabatPriceIndex",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_TalabatPriceIndex_BranchId_Barcode",
                table: "TalabatPriceIndex",
                columns: new[] { "BranchId", "Barcode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TalabatPriceIndex");
        }
    }
}
