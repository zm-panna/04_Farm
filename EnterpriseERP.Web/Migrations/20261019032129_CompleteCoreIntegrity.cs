using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseERP.Web.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCoreIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasure",
                table: "StockItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_StockMovements_CompanyId_BranchId_Id",
                table: "StockMovements",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateTable(
                name: "OpeningBalances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OpeningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalances", x => x.Id);
                    table.UniqueConstraint("AK_OpeningBalances_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_OpeningBalances_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    SourceBranchId = table.Column<int>(type: "int", nullable: false),
                    DestinationBranchId = table.Column<int>(type: "int", nullable: false),
                    SourceStockItemId = table.Column<int>(type: "int", nullable: false),
                    DestinationStockItemId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceBranchName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DestinationBranchName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSku = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DestinationSku = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DestinationProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SourceQuantityBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SourceQuantityAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DestinationQuantityBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DestinationQuantityAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SourceValueBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SourceValueAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DestinationValueBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DestinationValueAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SourceMovementId = table.Column<long>(type: "bigint", nullable: false),
                    DestinationMovementId = table.Column<long>(type: "bigint", nullable: false),
                    SourceVoucherId = table.Column<long>(type: "bigint", nullable: true),
                    DestinationVoucherId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.CheckConstraint("CK_StockTransfer_DifferentBranches", "[SourceBranchId] <> [DestinationBranchId] AND [SourceStockItemId] <> [DestinationStockItemId] AND [SourceMovementId] <> [DestinationMovementId]");
                    table.CheckConstraint("CK_StockTransfer_NonnegativeBalances", "[SourceQuantityBefore] >= 0 AND [SourceQuantityAfter] >= 0 AND [DestinationQuantityBefore] >= 0 AND [DestinationQuantityAfter] >= 0 AND [SourceValueBefore] >= 0 AND [SourceValueAfter] >= 0 AND [DestinationValueBefore] >= 0 AND [DestinationValueAfter] >= 0");
                    table.CheckConstraint("CK_StockTransfer_PairedVouchers", "([CostAmount] = 0 AND [SourceVoucherId] IS NULL AND [DestinationVoucherId] IS NULL) OR ([CostAmount] > 0 AND [SourceVoucherId] IS NOT NULL AND [DestinationVoucherId] IS NOT NULL AND [SourceVoucherId] <> [DestinationVoucherId])");
                    table.CheckConstraint("CK_StockTransfer_PositiveQuantity", "[Quantity] > 0 AND [CostAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Branches_CompanyId_DestinationBranchId",
                        columns: x => new { x.CompanyId, x.DestinationBranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Branches_CompanyId_SourceBranchId",
                        columns: x => new { x.CompanyId, x.SourceBranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_StockItems_CompanyId_DestinationBranchId_DestinationStockItemId",
                        columns: x => new { x.CompanyId, x.DestinationBranchId, x.DestinationStockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_StockItems_CompanyId_SourceBranchId_SourceStockItemId",
                        columns: x => new { x.CompanyId, x.SourceBranchId, x.SourceStockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_StockMovements_CompanyId_DestinationBranchId_DestinationMovementId",
                        columns: x => new { x.CompanyId, x.DestinationBranchId, x.DestinationMovementId },
                        principalTable: "StockMovements",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_StockMovements_CompanyId_SourceBranchId_SourceMovementId",
                        columns: x => new { x.CompanyId, x.SourceBranchId, x.SourceMovementId },
                        principalTable: "StockMovements",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Vouchers_CompanyId_DestinationBranchId_DestinationVoucherId",
                        columns: x => new { x.CompanyId, x.DestinationBranchId, x.DestinationVoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Vouchers_CompanyId_SourceBranchId_SourceVoucherId",
                        columns: x => new { x.CompanyId, x.SourceBranchId, x.SourceVoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningAnimalValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OpeningBalanceId = table.Column<long>(type: "bigint", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningAnimalValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningAnimalValues_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningAnimalValues_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningAnimalValues_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningAnimalValues_OpeningBalances_CompanyId_BranchId_OpeningBalanceId",
                        columns: x => new { x.CompanyId, x.BranchId, x.OpeningBalanceId },
                        principalTable: "OpeningBalances",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningStockValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OpeningBalanceId = table.Column<long>(type: "bigint", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningStockValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningStockValues_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningStockValues_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningStockValues_OpeningBalances_CompanyId_BranchId_OpeningBalanceId",
                        columns: x => new { x.CompanyId, x.BranchId, x.OpeningBalanceId },
                        principalTable: "OpeningBalances",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningStockValues_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningAnimalValues_CompanyId_BranchId_AnimalId",
                table: "OpeningAnimalValues",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningAnimalValues_CompanyId_BranchId_OpeningBalanceId",
                table: "OpeningAnimalValues",
                columns: new[] { "CompanyId", "BranchId", "OpeningBalanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_CompanyId_BranchId",
                table: "OpeningBalances",
                columns: new[] { "CompanyId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_CompanyId_BranchId_VoucherId",
                table: "OpeningBalances",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_CompanyId_Reference",
                table: "OpeningBalances",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_VoucherId",
                table: "OpeningBalances",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningStockValues_CompanyId_BranchId_OpeningBalanceId",
                table: "OpeningStockValues",
                columns: new[] { "CompanyId", "BranchId", "OpeningBalanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningStockValues_CompanyId_BranchId_StockItemId",
                table: "OpeningStockValues",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_DestinationBranchId_CreatedUtc",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "DestinationBranchId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_DestinationBranchId_DestinationMovementId",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "DestinationBranchId", "DestinationMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_DestinationBranchId_DestinationStockItemId",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "DestinationBranchId", "DestinationStockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_DestinationBranchId_DestinationVoucherId",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "DestinationBranchId", "DestinationVoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_Reference",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_SourceBranchId_CreatedUtc",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "SourceBranchId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_SourceBranchId_SourceMovementId",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "SourceBranchId", "SourceMovementId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_SourceBranchId_SourceStockItemId",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "SourceBranchId", "SourceStockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_SourceBranchId_SourceVoucherId",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "SourceBranchId", "SourceVoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationMovementId",
                table: "StockTransfers",
                column: "DestinationMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationVoucherId",
                table: "StockTransfers",
                column: "DestinationVoucherId",
                unique: true,
                filter: "[DestinationVoucherId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_SourceMovementId",
                table: "StockTransfers",
                column: "SourceMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_SourceVoucherId",
                table: "StockTransfers",
                column: "SourceVoucherId",
                unique: true,
                filter: "[SourceVoucherId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpeningAnimalValues");

            migrationBuilder.DropTable(
                name: "OpeningStockValues");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropTable(
                name: "OpeningBalances");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_StockMovements_CompanyId_BranchId_Id",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "StockItems");
        }
    }
}
