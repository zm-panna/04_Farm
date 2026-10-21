using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseERP.Web.Migrations
{
    /// <inheritdoc />
    public partial class ReviewedScanningWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScanLabels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: true),
                    AnimalId = table.Column<int>(type: "int", nullable: true),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: true),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: true),
                    FinanceDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanLabels", x => x.Id);
                    table.UniqueConstraint("AK_ScanLabels_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_ScanLabel_Target", "([Kind]='Product' AND [StockItemId] IS NOT NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR ([Kind]='Animal' AND [StockItemId] IS NULL AND [AnimalId] IS NOT NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR ([Kind]='Asset' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NOT NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR ([Kind]='PurchaseOrder' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NOT NULL AND [FinanceDocumentId] IS NULL) OR ([Kind]='Document' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ScanLabels_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanLabels_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanLabels_AspNetUsers_RevokedBy",
                        column: x => x.RevokedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanLabels_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanLabels_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanLabels_FinanceDocuments_CompanyId_BranchId_FinanceDocumentId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FinanceDocumentId },
                        principalTable: "FinanceDocuments",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanLabels_FixedAssets_CompanyId_BranchId_FixedAssetId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FixedAssetId },
                        principalTable: "FixedAssets",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanLabels_PurchaseOrders_CompanyId_BranchId_PurchaseOrderId",
                        columns: x => new { x.CompanyId, x.BranchId, x.PurchaseOrderId },
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanLabels_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScanIntents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScanLabelId = table.Column<long>(type: "bigint", nullable: false),
                    SecondaryBranchId = table.Column<int>(type: "int", nullable: true),
                    SecondaryLabelId = table.Column<long>(type: "bigint", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReviewJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    BusinessReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanIntents", x => x.Id);
                    table.CheckConstraint("CK_ScanIntent_Secondary", "([SecondaryBranchId] IS NULL AND [SecondaryLabelId] IS NULL) OR ([SecondaryBranchId] IS NOT NULL AND [SecondaryLabelId] IS NOT NULL)");
                    table.CheckConstraint("CK_ScanIntent_State", "([Status]='Pending' AND [CompletedUtc] IS NULL) OR ([Status] IN ('Committed','Cancelled') AND [CompletedUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ScanIntents_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanIntents_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanIntents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanIntents_ScanLabels_CompanyId_BranchId_ScanLabelId",
                        columns: x => new { x.CompanyId, x.BranchId, x.ScanLabelId },
                        principalTable: "ScanLabels",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanIntents_ScanLabels_CompanyId_SecondaryBranchId_SecondaryLabelId",
                        columns: x => new { x.CompanyId, x.SecondaryBranchId, x.SecondaryLabelId },
                        principalTable: "ScanLabels",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_CompanyId_BranchId_Id",
                table: "ScanIntents",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_CompanyId_BranchId_OwnerUserId_CreatedUtc",
                table: "ScanIntents",
                columns: new[] { "CompanyId", "BranchId", "OwnerUserId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_CompanyId_BranchId_OwnerUserId_Id",
                table: "ScanIntents",
                columns: new[] { "CompanyId", "BranchId", "OwnerUserId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_CompanyId_BranchId_OwnerUserId_RequestKey",
                table: "ScanIntents",
                columns: new[] { "CompanyId", "BranchId", "OwnerUserId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_CompanyId_BranchId_OwnerUserId_Status_ExpiresUtc",
                table: "ScanIntents",
                columns: new[] { "CompanyId", "BranchId", "OwnerUserId", "Status", "ExpiresUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_CompanyId_BranchId_ScanLabelId",
                table: "ScanIntents",
                columns: new[] { "CompanyId", "BranchId", "ScanLabelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_CompanyId_BusinessReference",
                table: "ScanIntents",
                columns: new[] { "CompanyId", "BusinessReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_CompanyId_SecondaryBranchId_SecondaryLabelId",
                table: "ScanIntents",
                columns: new[] { "CompanyId", "SecondaryBranchId", "SecondaryLabelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanIntents_OwnerUserId",
                table: "ScanIntents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_CompanyId_BranchId_AnimalId",
                table: "ScanLabels",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_CompanyId_BranchId_FinanceDocumentId",
                table: "ScanLabels",
                columns: new[] { "CompanyId", "BranchId", "FinanceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_CompanyId_BranchId_FixedAssetId",
                table: "ScanLabels",
                columns: new[] { "CompanyId", "BranchId", "FixedAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_CompanyId_BranchId_Kind_Id",
                table: "ScanLabels",
                columns: new[] { "CompanyId", "BranchId", "Kind", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_CompanyId_BranchId_PurchaseOrderId",
                table: "ScanLabels",
                columns: new[] { "CompanyId", "BranchId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_CompanyId_BranchId_StockItemId",
                table: "ScanLabels",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_CompanyId_BranchId_ValueHash",
                table: "ScanLabels",
                columns: new[] { "CompanyId", "BranchId", "ValueHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_CreatedBy",
                table: "ScanLabels",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ScanLabels_RevokedBy",
                table: "ScanLabels",
                column: "RevokedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScanIntents");

            migrationBuilder.DropTable(
                name: "ScanLabels");
        }
    }
}
