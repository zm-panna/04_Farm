using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseERP.Web.Migrations
{
    /// <inheritdoc />
    public partial class ScopedRealtimeAndServerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_CompanyId",
                table: "UserPermissions");

            migrationBuilder.DropIndex(
                name: "IX_UserCompanyAccess_CompanyId",
                table: "UserCompanyAccess");

            migrationBuilder.DropIndex(
                name: "IX_UserBranchAccess_CompanyId_BranchId",
                table: "UserBranchAccess");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_CompanyId_BranchId",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_Audits_CompanyId",
                table: "Audits");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Notifications_CompanyId_BranchId_Id",
                table: "Notifications",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateTable(
                name: "InternalMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalMessages", x => x.Id);
                    table.UniqueConstraint("AK_InternalMessages_CompanyId_BranchId_Id_RecipientUserId", x => new { x.CompanyId, x.BranchId, x.Id, x.RecipientUserId });
                    table.CheckConstraint("CK_InternalMessage_Content", "LEN([Subject]) BETWEEN 1 AND 160 AND LEN([Body]) BETWEEN 1 AND 8000 AND LEN([IdempotencyKey]) = 32 AND LEN([PayloadHash]) = 64");
                    table.ForeignKey(
                        name: "FK_InternalMessages_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalMessages_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalMessages_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessagingAutomationReads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NotificationId = table.Column<long>(type: "bigint", nullable: false),
                    ReadUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagingAutomationReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessagingAutomationReads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessagingAutomationReads_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessagingAutomationReads_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessagingAutomationReads_Notifications_CompanyId_BranchId_NotificationId",
                        columns: x => new { x.CompanyId, x.BranchId, x.NotificationId },
                        principalTable: "Notifications",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessagingRateBuckets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    WindowUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagingRateBuckets", x => x.Id);
                    table.CheckConstraint("CK_MessagingRateBucket_Limit", "([Period] = 'Minute' AND [Count] BETWEEN 1 AND 20) OR ([Period] = 'Day' AND [Count] BETWEEN 1 AND 200)");
                    table.ForeignKey(
                        name: "FK_MessagingRateBuckets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessagingRateBuckets_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessagingRateBuckets_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScaleCacheEpochs",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ScopeBranchId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScaleCacheEpochs", x => new { x.CompanyId, x.ScopeBranchId });
                    table.ForeignKey(
                        name: "FK_ScaleCacheEpochs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserInboxNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInboxNotifications", x => x.Id);
                    table.UniqueConstraint("AK_UserInboxNotifications_CompanyId_BranchId_Id_UserId", x => new { x.CompanyId, x.BranchId, x.Id, x.UserId });
                    table.ForeignKey(
                        name: "FK_UserInboxNotifications_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserInboxNotifications_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserInboxNotifications_InternalMessages_CompanyId_BranchId_MessageId_UserId",
                        columns: x => new { x.CompanyId, x.BranchId, x.MessageId, x.UserId },
                        principalTable: "InternalMessages",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id", "RecipientUserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessagingOutbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NotificationId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    HintProcessedUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    LeaseUntilUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    LeaseOwner = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagingOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessagingOutbox_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessagingOutbox_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessagingOutbox_UserInboxNotifications_CompanyId_BranchId_NotificationId_UserId",
                        columns: x => new { x.CompanyId, x.BranchId, x.NotificationId, x.UserId },
                        principalTable: "UserInboxNotifications",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_CompanyId_Id",
                table: "UserPermissions",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyAccess_CompanyId_Id",
                table: "UserCompanyAccess",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBranchAccess_CompanyId_BranchId_Id",
                table: "UserBranchAccess",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CompanyId_BranchId_Id",
                table: "SupportTickets",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketActivities_CompanyId_Id",
                table: "SupportTicketActivities",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_CompanyId_Id",
                table: "SubscriptionPayments",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CompanyId_Id",
                table: "StockTransfers",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReorderPolicies_CompanyId_BranchId_Id",
                table: "StockReorderPolicies",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_CompanyId_Id",
                table: "ScheduledJobs",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_CompanyId_BranchId_Id",
                table: "PurchaseOrderReceipts",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningStockValues_CompanyId_BranchId_Id",
                table: "OpeningStockValues",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningAnimalValues_CompanyId_BranchId_Id",
                table: "OpeningAnimalValues",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MilkYields_CompanyId_BranchId_Id",
                table: "MilkYields",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostBalances_CompanyId_BranchId_Id",
                table: "InventoryCostBalances",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollLines_CompanyId_BranchId_Id",
                table: "HRPayrollLines",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollLeaves_CompanyId_BranchId_Id",
                table: "HRPayrollLeaves",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollCompensations_CompanyId_BranchId_Id",
                table: "HRPayrollCompensations",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollAttendance_CompanyId_BranchId_Id",
                table: "HRPayrollAttendance",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDisposals_CompanyId_BranchId_Id",
                table: "FixedAssetDisposals",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationLines_CompanyId_BranchId_Id",
                table: "FixedAssetDepreciationLines",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCustodies_CompanyId_BranchId_Id",
                table: "FixedAssetCustodies",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePaymentAllocations_CompanyId_BranchId_Id",
                table: "FinancePaymentAllocations",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FatteningPlans_CompanyId_BranchId_Id",
                table: "FatteningPlans",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmExpenses_CompanyId_BranchId_Id",
                table: "FarmExpenses",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceStockReservations_CompanyId_BranchId_Id",
                table: "EcommerceStockReservations",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceReturns_CompanyId_BranchId_Id",
                table: "EcommerceReturns",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrderEvents_CompanyId_BranchId_Id",
                table: "EcommerceOrderEvents",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_CompanyId_BranchId_Id",
                table: "EcommerceCartLines",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMQuoteLines_CompanyId_BranchId_Id",
                table: "CRMQuoteLines",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMConsentEvents_CompanyId_BranchId_Id",
                table: "CRMConsentEvents",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptionPolicies_CompanyId_Id",
                table: "CompanySubscriptionPolicies",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCurrencyLocks_CompanyId_Id",
                table: "CompanyCurrencyLocks",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycleEvents_CompanyId_BranchId_Id",
                table: "BreedingCycleEvents",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_CompanyId_BranchId_Id",
                table: "BankStatementLines",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_CompanyId_Id",
                table: "AutomationExecutions",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationApprovals_CompanyId_Id",
                table: "AutomationApprovals",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Audits_CompanyId_Id",
                table: "Audits",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalWeightRecords_CompanyId_BranchId_Id",
                table: "AnimalWeightRecords",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalSales_CompanyId_BranchId_Id",
                table: "AnimalSales",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalHealthRecords_CompanyId_BranchId_Id",
                table: "AnimalHealthRecords",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedRecords_CompanyId_BranchId_Id",
                table: "AnimalFeedRecords",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriodCloses_CompanyId_BranchId_Id",
                table: "AccountingPeriodCloses",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalMessages_CompanyId_BranchId_Id",
                table: "InternalMessages",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalMessages_CompanyId_BranchId_SenderUserId_Id",
                table: "InternalMessages",
                columns: new[] { "CompanyId", "BranchId", "SenderUserId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalMessages_CompanyId_BranchId_SenderUserId_IdempotencyKey",
                table: "InternalMessages",
                columns: new[] { "CompanyId", "BranchId", "SenderUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalMessages_RecipientUserId",
                table: "InternalMessages",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalMessages_SenderUserId",
                table: "InternalMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagingAutomationReads_CompanyId_BranchId_Id",
                table: "MessagingAutomationReads",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagingAutomationReads_CompanyId_BranchId_NotificationId",
                table: "MessagingAutomationReads",
                columns: new[] { "CompanyId", "BranchId", "NotificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagingAutomationReads_CompanyId_BranchId_UserId_NotificationId",
                table: "MessagingAutomationReads",
                columns: new[] { "CompanyId", "BranchId", "UserId", "NotificationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessagingAutomationReads_UserId",
                table: "MessagingAutomationReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagingOutbox_CompanyId_BranchId_Id",
                table: "MessagingOutbox",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagingOutbox_CompanyId_BranchId_NotificationId_UserId",
                table: "MessagingOutbox",
                columns: new[] { "CompanyId", "BranchId", "NotificationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagingOutbox_HintProcessedUtcTicks_LeaseUntilUtcTicks_Id",
                table: "MessagingOutbox",
                columns: new[] { "HintProcessedUtcTicks", "LeaseUntilUtcTicks", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagingOutbox_NotificationId",
                table: "MessagingOutbox",
                column: "NotificationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessagingRateBuckets_CompanyId_BranchId_Id",
                table: "MessagingRateBuckets",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagingRateBuckets_CompanyId_BranchId_UserId_Period_WindowUtcTicks",
                table: "MessagingRateBuckets",
                columns: new[] { "CompanyId", "BranchId", "UserId", "Period", "WindowUtcTicks" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessagingRateBuckets_UserId",
                table: "MessagingRateBuckets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInboxNotifications_CompanyId_BranchId_Id",
                table: "UserInboxNotifications",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInboxNotifications_CompanyId_BranchId_MessageId_UserId",
                table: "UserInboxNotifications",
                columns: new[] { "CompanyId", "BranchId", "MessageId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInboxNotifications_CompanyId_BranchId_UserId_MessageId",
                table: "UserInboxNotifications",
                columns: new[] { "CompanyId", "BranchId", "UserId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserInboxNotifications_CompanyId_BranchId_UserId_ReadUtc_Id",
                table: "UserInboxNotifications",
                columns: new[] { "CompanyId", "BranchId", "UserId", "ReadUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessagingAutomationReads");

            migrationBuilder.DropTable(
                name: "MessagingOutbox");

            migrationBuilder.DropTable(
                name: "MessagingRateBuckets");

            migrationBuilder.DropTable(
                name: "ScaleCacheEpochs");

            migrationBuilder.DropTable(
                name: "UserInboxNotifications");

            migrationBuilder.DropTable(
                name: "InternalMessages");

            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_CompanyId_Id",
                table: "UserPermissions");

            migrationBuilder.DropIndex(
                name: "IX_UserCompanyAccess_CompanyId_Id",
                table: "UserCompanyAccess");

            migrationBuilder.DropIndex(
                name: "IX_UserBranchAccess_CompanyId_BranchId_Id",
                table: "UserBranchAccess");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_CompanyId_BranchId_Id",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTicketActivities_CompanyId_Id",
                table: "SupportTicketActivities");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPayments_CompanyId_Id",
                table: "SubscriptionPayments");

            migrationBuilder.DropIndex(
                name: "IX_StockTransfers_CompanyId_Id",
                table: "StockTransfers");

            migrationBuilder.DropIndex(
                name: "IX_StockReorderPolicies_CompanyId_BranchId_Id",
                table: "StockReorderPolicies");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledJobs_CompanyId_Id",
                table: "ScheduledJobs");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderReceipts_CompanyId_BranchId_Id",
                table: "PurchaseOrderReceipts");

            migrationBuilder.DropIndex(
                name: "IX_OpeningStockValues_CompanyId_BranchId_Id",
                table: "OpeningStockValues");

            migrationBuilder.DropIndex(
                name: "IX_OpeningAnimalValues_CompanyId_BranchId_Id",
                table: "OpeningAnimalValues");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Notifications_CompanyId_BranchId_Id",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_MilkYields_CompanyId_BranchId_Id",
                table: "MilkYields");

            migrationBuilder.DropIndex(
                name: "IX_InventoryCostBalances_CompanyId_BranchId_Id",
                table: "InventoryCostBalances");

            migrationBuilder.DropIndex(
                name: "IX_HRPayrollLines_CompanyId_BranchId_Id",
                table: "HRPayrollLines");

            migrationBuilder.DropIndex(
                name: "IX_HRPayrollLeaves_CompanyId_BranchId_Id",
                table: "HRPayrollLeaves");

            migrationBuilder.DropIndex(
                name: "IX_HRPayrollCompensations_CompanyId_BranchId_Id",
                table: "HRPayrollCompensations");

            migrationBuilder.DropIndex(
                name: "IX_HRPayrollAttendance_CompanyId_BranchId_Id",
                table: "HRPayrollAttendance");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetDisposals_CompanyId_BranchId_Id",
                table: "FixedAssetDisposals");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetDepreciationLines_CompanyId_BranchId_Id",
                table: "FixedAssetDepreciationLines");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetCustodies_CompanyId_BranchId_Id",
                table: "FixedAssetCustodies");

            migrationBuilder.DropIndex(
                name: "IX_FinancePaymentAllocations_CompanyId_BranchId_Id",
                table: "FinancePaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_FatteningPlans_CompanyId_BranchId_Id",
                table: "FatteningPlans");

            migrationBuilder.DropIndex(
                name: "IX_FarmExpenses_CompanyId_BranchId_Id",
                table: "FarmExpenses");

            migrationBuilder.DropIndex(
                name: "IX_EcommerceStockReservations_CompanyId_BranchId_Id",
                table: "EcommerceStockReservations");

            migrationBuilder.DropIndex(
                name: "IX_EcommerceReturns_CompanyId_BranchId_Id",
                table: "EcommerceReturns");

            migrationBuilder.DropIndex(
                name: "IX_EcommerceOrderEvents_CompanyId_BranchId_Id",
                table: "EcommerceOrderEvents");

            migrationBuilder.DropIndex(
                name: "IX_EcommerceCartLines_CompanyId_BranchId_Id",
                table: "EcommerceCartLines");

            migrationBuilder.DropIndex(
                name: "IX_CRMQuoteLines_CompanyId_BranchId_Id",
                table: "CRMQuoteLines");

            migrationBuilder.DropIndex(
                name: "IX_CRMConsentEvents_CompanyId_BranchId_Id",
                table: "CRMConsentEvents");

            migrationBuilder.DropIndex(
                name: "IX_CompanySubscriptionPolicies_CompanyId_Id",
                table: "CompanySubscriptionPolicies");

            migrationBuilder.DropIndex(
                name: "IX_CompanyCurrencyLocks_CompanyId_Id",
                table: "CompanyCurrencyLocks");

            migrationBuilder.DropIndex(
                name: "IX_BreedingCycleEvents_CompanyId_BranchId_Id",
                table: "BreedingCycleEvents");

            migrationBuilder.DropIndex(
                name: "IX_BankStatementLines_CompanyId_BranchId_Id",
                table: "BankStatementLines");

            migrationBuilder.DropIndex(
                name: "IX_AutomationExecutions_CompanyId_Id",
                table: "AutomationExecutions");

            migrationBuilder.DropIndex(
                name: "IX_AutomationApprovals_CompanyId_Id",
                table: "AutomationApprovals");

            migrationBuilder.DropIndex(
                name: "IX_Audits_CompanyId_Id",
                table: "Audits");

            migrationBuilder.DropIndex(
                name: "IX_AnimalWeightRecords_CompanyId_BranchId_Id",
                table: "AnimalWeightRecords");

            migrationBuilder.DropIndex(
                name: "IX_AnimalSales_CompanyId_BranchId_Id",
                table: "AnimalSales");

            migrationBuilder.DropIndex(
                name: "IX_AnimalHealthRecords_CompanyId_BranchId_Id",
                table: "AnimalHealthRecords");

            migrationBuilder.DropIndex(
                name: "IX_AnimalFeedRecords_CompanyId_BranchId_Id",
                table: "AnimalFeedRecords");

            migrationBuilder.DropIndex(
                name: "IX_AccountingPeriodCloses_CompanyId_BranchId_Id",
                table: "AccountingPeriodCloses");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_CompanyId",
                table: "UserPermissions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyAccess_CompanyId",
                table: "UserCompanyAccess",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranchAccess_CompanyId_BranchId",
                table: "UserBranchAccess",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CompanyId_BranchId",
                table: "SupportTickets",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Audits_CompanyId",
                table: "Audits",
                column: "CompanyId");
        }
    }
}
