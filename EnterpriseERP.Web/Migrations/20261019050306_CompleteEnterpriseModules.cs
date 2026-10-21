using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseERP.Web.Migrations
{
    /// <inheritdoc />
    public partial class CompleteEnterpriseModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantity",
                table: "StockItems",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_StockDocumentReturns_CompanyId_BranchId_Id",
                table: "StockDocumentReturns",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProductSales_CompanyId_BranchId_Id",
                table: "ProductSales",
                columns: new[] { "CompanyId", "BranchId", "Id" });

            migrationBuilder.CreateTable(
                name: "CompanyBusinessPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegionCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyMinorUnits = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewBasis = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExistingLedgerCurrencyConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyBusinessPolicies", x => x.Id);
                    table.UniqueConstraint("AK_CompanyBusinessPolicies_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_CompanyBusinessPolicy_MinorUnits", "[CurrencyMinorUnits] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_CompanyBusinessPolicy_Status", "[Status] IN ('Draft','Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_CompanyBusinessPolicies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCurrencyLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyMinorUnits = table.Column<int>(type: "int", nullable: false),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConfirmedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCurrencyLocks", x => x.Id);
                    table.CheckConstraint("CK_CompanyCurrencyLock_MinorUnits", "[CurrencyMinorUnits] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_CompanyCurrencyLocks_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMLeads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MarketingConsent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConsentEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ConsentRecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMLeads", x => x.Id);
                    table.UniqueConstraint("AK_CRMLeads_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_CRMLead_Consent", "[MarketingConsent] IN ('Unknown','Granted','Withdrawn')");
                    table.CheckConstraint("CK_CRMLead_Converted", "[Status] <> 'Converted' OR [CustomerId] IS NOT NULL");
                    table.CheckConstraint("CK_CRMLead_Status", "[Status] IN ('Active','Converted','Archived')");
                    table.ForeignKey(
                        name: "FK_CRMLeads_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMLeads_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMLeads_TradingParties_CompanyId_BranchId_CustomerId",
                        columns: x => new { x.CompanyId, x.BranchId, x.CustomerId },
                        principalTable: "TradingParties",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceCustomers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TradingPartyId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    LinkedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LinkedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceCustomers", x => x.Id);
                    table.UniqueConstraint("AK_EcommerceCustomers_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_EcommerceCustomers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCustomers_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCustomers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCustomers_TradingParties_CompanyId_BranchId_TradingPartyId",
                        columns: x => new { x.CompanyId, x.BranchId, x.TradingPartyId },
                        principalTable: "TradingParties",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HRPayrollStaff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayBasis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    IdentityUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DailyRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OvertimeHourlyRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HRPayrollStaff", x => x.Id);
                    table.UniqueConstraint("AK_HRPayrollStaff_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_HRPayrollStaff_AspNetUsers_IdentityUserId",
                        column: x => x.IdentityUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollStaff_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollStaff_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssetDepreciationRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BusinessPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencyMinorUnits = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancelledUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PostedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoucherId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssetDepreciationRuns", x => x.Id);
                    table.UniqueConstraint("AK_FixedAssetDepreciationRuns_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_FixedAssetRun_Status", "[Status] IN ('Draft','Approved','Posted','Cancelled')");
                    table.CheckConstraint("CK_FixedAssetRun_Total", "[Total] >= 0");
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_CompanyBusinessPolicies_CompanyId_BusinessPolicyId",
                        columns: x => new { x.CompanyId, x.BusinessPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationRuns_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AcquisitionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AcquisitionSettlement = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DepreciationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "int", nullable: false),
                    ResidualValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DepreciationStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PolicyReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BusinessPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencyMinorUnits = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancelledUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcquisitionVoucherId = table.Column<long>(type: "bigint", nullable: true),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LastDepreciationPeriod = table.Column<DateOnly>(type: "date", nullable: true),
                    FinancialVersion = table.Column<int>(type: "int", nullable: false),
                    Custodian = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisposedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssets", x => x.Id);
                    table.UniqueConstraint("AK_FixedAssets_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_FixedAsset_Policy", "([DepreciationMethod] = 'StraightLineMonthly' AND [UsefulLifeMonths] BETWEEN 1 AND 1200) OR ([DepreciationMethod] = 'None' AND [UsefulLifeMonths] = 0)");
                    table.CheckConstraint("CK_FixedAsset_Status", "[Status] IN ('Draft','Approved','Active','Disposed','Cancelled')");
                    table.CheckConstraint("CK_FixedAsset_Values", "[Cost] >= 0 AND [ResidualValue] >= 0 AND [ResidualValue] <= [Cost] AND [AccumulatedDepreciation] >= 0 AND ROUND([Cost] - [ResidualValue] - [AccumulatedDepreciation], 3) >= 0");
                    table.ForeignKey(
                        name: "FK_FixedAssets_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_CompanyBusinessPolicies_CompanyId_BusinessPolicyId",
                        columns: x => new { x.CompanyId, x.BusinessPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Vouchers_CompanyId_BranchId_AcquisitionVoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AcquisitionVoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HRPayrollPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayBasis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MonthlyProrationMode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MonthlyFixedUnits = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CapMonthlyPaidUnits = table.Column<bool>(type: "bit", nullable: false),
                    CompanyBusinessPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegionCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyDecimals = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveThrough = table.Column<DateOnly>(type: "date", nullable: false),
                    DeductionPercent = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    EmployerContributionPercent = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    JurisdictionAndBasis = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExpenseAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayableAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeductionAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployerExpenseAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployerPayableAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Reviewed = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HRPayrollPolicies", x => x.Id);
                    table.UniqueConstraint("AK_HRPayrollPolicies_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_HRPayrollPolicies_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollPolicies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollPolicies_CompanyBusinessPolicies_CompanyId_CompanyBusinessPolicyId",
                        columns: x => new { x.CompanyId, x.CompanyBusinessPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CompanyBusinessPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegionCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxTreatment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RatePercent = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TaxLiabilityAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPolicies", x => x.Id);
                    table.UniqueConstraint("AK_TaxPolicies_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_TaxPolicy_Rate", "[RatePercent] >= 0 AND [RatePercent] <= 100 AND (([TaxTreatment] = 'Standard' AND [RatePercent] > 0) OR ([TaxTreatment] IN ('Exempt','OutOfScope') AND [RatePercent] = 0))");
                    table.CheckConstraint("CK_TaxPolicy_Status", "[Status] IN ('Draft','Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_TaxPolicies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaxPolicies_CompanyBusinessPolicies_CompanyId_CompanyBusinessPolicyId",
                        columns: x => new { x.CompanyId, x.CompanyBusinessPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMConsentEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    LeadId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousConsent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Consent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMConsentEvents", x => x.Id);
                    table.CheckConstraint("CK_CRMConsentEvent_Consent", "[Consent] IN ('Unknown','Granted','Withdrawn') AND [PreviousConsent] IN ('Unknown','Granted','Withdrawn')");
                    table.ForeignKey(
                        name: "FK_CRMConsentEvents_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMConsentEvents_CRMLeads_CompanyId_BranchId_LeadId",
                        columns: x => new { x.CompanyId, x.BranchId, x.LeadId },
                        principalTable: "CRMLeads",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMConsentEvents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMOpportunities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    LeadId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpectedCloseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMOpportunities", x => x.Id);
                    table.UniqueConstraint("AK_CRMOpportunities_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.UniqueConstraint("AK_CRMOpportunities_CompanyId_BranchId_Id_LeadId", x => new { x.CompanyId, x.BranchId, x.Id, x.LeadId });
                    table.CheckConstraint("CK_CRMOpportunity_Stage", "[Stage] IN ('Qualified','Proposal','Negotiation','Won','Lost')");
                    table.ForeignKey(
                        name: "FK_CRMOpportunities_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMOpportunities_CRMLeads_CompanyId_BranchId_LeadId",
                        columns: x => new { x.CompanyId, x.BranchId, x.LeadId },
                        principalTable: "CRMLeads",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMOpportunities_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceCarts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    EcommerceCustomerId = table.Column<long>(type: "bigint", nullable: false),
                    RevisionToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceCarts", x => x.Id);
                    table.UniqueConstraint("AK_EcommerceCarts_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_EcommerceCarts_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCarts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCarts_EcommerceCustomers_CompanyId_BranchId_EcommerceCustomerId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceCustomerId },
                        principalTable: "EcommerceCustomers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    EcommerceCustomerId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CheckoutToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StateToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyMinorUnits = table.Column<int>(type: "int", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegionCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeliveryMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceOrders", x => x.Id);
                    table.UniqueConstraint("AK_EcommerceOrders_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_EcommerceOrder_Amounts", "[NetAmount] > 0 AND [TaxAmount] >= 0 AND [GrossAmount] > 0");
                    table.CheckConstraint("CK_EcommerceOrder_Delivery", "[DeliveryMethod] = 'BranchCollection'");
                    table.CheckConstraint("CK_EcommerceOrder_Payment", "[PaymentStatus] = 'PendingUnverified'");
                    table.CheckConstraint("CK_EcommerceOrder_Status", "[Status] IN ('Submitted','Approved','Fulfilled','Cancelled')");
                    table.ForeignKey(
                        name: "FK_EcommerceOrders_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrders_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrders_CompanyBusinessPolicies_CompanyId_CompanyPolicyId",
                        columns: x => new { x.CompanyId, x.CompanyPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrders_EcommerceCustomers_CompanyId_BranchId_EcommerceCustomerId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceCustomerId },
                        principalTable: "EcommerceCustomers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HRPayrollAttendance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidDays = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HRPayrollAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HRPayrollAttendance_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollAttendance_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollAttendance_HRPayrollStaff_CompanyId_BranchId_StaffId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StaffId },
                        principalTable: "HRPayrollStaff",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HRPayrollCompensations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PayBasis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OvertimeHourlyRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HRPayrollCompensations", x => x.Id);
                    table.UniqueConstraint("AK_HRPayrollCompensations_CompanyId_BranchId_StaffId_Id", x => new { x.CompanyId, x.BranchId, x.StaffId, x.Id });
                    table.ForeignKey(
                        name: "FK_HRPayrollCompensations_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollCompensations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollCompensations_HRPayrollStaff_CompanyId_BranchId_StaffId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StaffId },
                        principalTable: "HRPayrollStaff",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HRPayrollLeaves",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Through = table.Column<DateOnly>(type: "date", nullable: false),
                    Paid = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HRPayrollLeaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HRPayrollLeaves_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollLeaves_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollLeaves_HRPayrollStaff_CompanyId_BranchId_StaffId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StaffId },
                        principalTable: "HRPayrollStaff",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssetCustodies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousCustodian = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Custodian = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssetCustodies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssetCustodies_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCustodies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCustodies_FixedAssets_CompanyId_BranchId_FixedAssetId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FixedAssetId },
                        principalTable: "FixedAssets",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssetDepreciationLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    FixedAssetDepreciationRunId = table.Column<long>(type: "bigint", nullable: false),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedFinancialVersion = table.Column<int>(type: "int", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ResidualValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AccumulatedBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AccumulatedAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Posted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssetDepreciationLines", x => x.Id);
                    table.CheckConstraint("CK_FixedAssetLine_Values", "[Amount] >= 0 AND [AccumulatedBefore] >= 0 AND ROUND([Cost] - [ResidualValue] - [AccumulatedAfter], 3) >= 0");
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationLines_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationLines_FixedAssetDepreciationRuns_CompanyId_BranchId_FixedAssetDepreciationRunId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FixedAssetDepreciationRunId },
                        principalTable: "FixedAssetDepreciationRuns",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDepreciationLines_FixedAssets_CompanyId_BranchId_FixedAssetId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FixedAssetId },
                        principalTable: "FixedAssets",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssetDisposals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisposalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Counterparty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Proceeds = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Settlement = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CarryingValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    GainLoss = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ExpectedFinancialVersion = table.Column<int>(type: "int", nullable: false),
                    BusinessPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencyMinorUnits = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CancelledUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PostedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoucherId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssetDisposals", x => x.Id);
                    table.CheckConstraint("CK_FixedAssetDisposal_Status", "[Status] IN ('Draft','Approved','Posted','Cancelled')");
                    table.CheckConstraint("CK_FixedAssetDisposal_Values", "[Proceeds] >= 0 AND [CarryingValue] >= 0");
                    table.ForeignKey(
                        name: "FK_FixedAssetDisposals_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDisposals_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDisposals_CompanyBusinessPolicies_CompanyId_BusinessPolicyId",
                        columns: x => new { x.CompanyId, x.BusinessPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDisposals_FixedAssets_CompanyId_BranchId_FixedAssetId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FixedAssetId },
                        principalTable: "FixedAssets",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetDisposals_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HRPayrollRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    PolicyId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyDecimals = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VoucherId = table.Column<long>(type: "bigint", nullable: true),
                    Revision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HRPayrollRuns", x => x.Id);
                    table.UniqueConstraint("AK_HRPayrollRuns_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_HRPayrollRuns_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollRuns_HRPayrollPolicies_CompanyId_BranchId_PolicyId",
                        columns: x => new { x.CompanyId, x.BranchId, x.PolicyId },
                        principalTable: "HRPayrollPolicies",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollRuns_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceProducts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    CompanyPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    TaxPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NetUnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Published = table.Column<bool>(type: "bit", nullable: false),
                    RevisionToken = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceProducts", x => x.Id);
                    table.UniqueConstraint("AK_EcommerceProducts_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_EcommerceProduct_Price", "[NetUnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_EcommerceProducts_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceProducts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceProducts_CompanyBusinessPolicies_CompanyId_CompanyPolicyId",
                        columns: x => new { x.CompanyId, x.CompanyPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceProducts_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceProducts_TaxPolicies_CompanyId_TaxPolicyId",
                        columns: x => new { x.CompanyId, x.TaxPolicyId },
                        principalTable: "TaxPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    LeadId = table.Column<long>(type: "bigint", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompletionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CompletedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMActivities", x => x.Id);
                    table.UniqueConstraint("AK_CRMActivities_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_CRMActivity_Completion", "([Status] = 'Open' AND [CompletedUtc] IS NULL AND [CompletedBy] IS NULL) OR ([Status] <> 'Open' AND [CompletedUtc] IS NOT NULL AND [CompletedBy] IS NOT NULL)");
                    table.CheckConstraint("CK_CRMActivity_Kind", "[Kind] IN ('Task','Call','Meeting')");
                    table.CheckConstraint("CK_CRMActivity_Status", "[Status] IN ('Open','Done','Cancelled')");
                    table.ForeignKey(
                        name: "FK_CRMActivities_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMActivities_CRMLeads_CompanyId_BranchId_LeadId",
                        columns: x => new { x.CompanyId, x.BranchId, x.LeadId },
                        principalTable: "CRMLeads",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMActivities_CRMOpportunities_CompanyId_BranchId_OpportunityId_LeadId",
                        columns: x => new { x.CompanyId, x.BranchId, x.OpportunityId, x.LeadId },
                        principalTable: "CRMOpportunities",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id", "LeadId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMActivities_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMQuotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    LeadId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BusinessPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyMinorUnits = table.Column<int>(type: "int", nullable: false),
                    BusinessPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    QuoteDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcceptedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMQuotes", x => x.Id);
                    table.UniqueConstraint("AK_CRMQuotes_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_CRMQuote_Accept", "[Status] <> 'Accepted' OR ([AcceptedBy] IS NOT NULL AND [AcceptedUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_CRMQuote_Amounts", "[GrossAmount] > 0 AND [DiscountAmount] >= 0 AND [NetAmount] > 0 AND [DiscountAmount] < [GrossAmount]");
                    table.CheckConstraint("CK_CRMQuote_Review", "[Status] NOT IN ('Reviewed','Accepted') OR ([ReviewedBy] IS NOT NULL AND [ReviewedUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_CRMQuote_Status", "[Status] IN ('Draft','Reviewed','Accepted','Cancelled')");
                    table.ForeignKey(
                        name: "FK_CRMQuotes_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMQuotes_CRMLeads_CompanyId_BranchId_LeadId",
                        columns: x => new { x.CompanyId, x.BranchId, x.LeadId },
                        principalTable: "CRMLeads",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMQuotes_CRMOpportunities_CompanyId_BranchId_OpportunityId_LeadId",
                        columns: x => new { x.CompanyId, x.BranchId, x.OpportunityId, x.LeadId },
                        principalTable: "CRMOpportunities",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id", "LeadId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMQuotes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMQuotes_CompanyBusinessPolicies_CompanyId_BusinessPolicyId",
                        columns: x => new { x.CompanyId, x.BusinessPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceOrderEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    EcommerceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceOrderEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcommerceOrderEvents_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrderEvents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrderEvents_EcommerceOrders_CompanyId_BranchId_EcommerceOrderId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId },
                        principalTable: "EcommerceOrders",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HRPayrollLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompensationId = table.Column<long>(type: "bigint", nullable: false),
                    CompensationVersion = table.Column<int>(type: "int", nullable: false),
                    CompensationEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PayBasis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProrationMode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProrationDenominator = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PaidUnitsUsed = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    StaffCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StaffName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OvertimeHourlyRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AttendanceDays = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PaidLeaveDays = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BasePay = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OvertimePay = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Deductions = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    EmployerContribution = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HRPayrollLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HRPayrollLines_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollLines_HRPayrollCompensations_CompanyId_BranchId_StaffId_CompensationId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StaffId, x.CompensationId },
                        principalTable: "HRPayrollCompensations",
                        principalColumns: new[] { "CompanyId", "BranchId", "StaffId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollLines_HRPayrollRuns_CompanyId_BranchId_RunId",
                        columns: x => new { x.CompanyId, x.BranchId, x.RunId },
                        principalTable: "HRPayrollRuns",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HRPayrollLines_HRPayrollStaff_CompanyId_BranchId_StaffId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StaffId },
                        principalTable: "HRPayrollStaff",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceCartLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    EcommerceCartId = table.Column<long>(type: "bigint", nullable: false),
                    EcommerceProductId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    TaxPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    ProductRevisionToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetUnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceCartLines", x => x.Id);
                    table.CheckConstraint("CK_EcommerceCartLine_Amounts", "[NetAmount] > 0 AND [TaxAmount] >= 0 AND [GrossAmount] > 0");
                    table.CheckConstraint("CK_EcommerceCartLine_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_CompanyBusinessPolicies_CompanyId_CompanyPolicyId",
                        columns: x => new { x.CompanyId, x.CompanyPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_EcommerceCarts_CompanyId_BranchId_EcommerceCartId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceCartId },
                        principalTable: "EcommerceCarts",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_EcommerceProducts_CompanyId_BranchId_EcommerceProductId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceProductId },
                        principalTable: "EcommerceProducts",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_TaxPolicies_CompanyId_TaxPolicyId",
                        columns: x => new { x.CompanyId, x.TaxPolicyId },
                        principalTable: "TaxPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceOrderLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    EcommerceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    EcommerceProductId = table.Column<long>(type: "bigint", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    TaxPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetUnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TaxRatePercent = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TaxTreatment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxLiabilityAccount = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceOrderLines", x => x.Id);
                    table.UniqueConstraint("AK_EcommerceOrderLines_CompanyId_BranchId_EcommerceOrderId_StockItemId_Id", x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId, x.StockItemId, x.Id });
                    table.UniqueConstraint("AK_EcommerceOrderLines_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_EcommerceOrderLine_Amounts", "[NetAmount] > 0 AND [TaxAmount] >= 0 AND [GrossAmount] > 0");
                    table.CheckConstraint("CK_EcommerceOrderLine_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_EcommerceOrderLines_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrderLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrderLines_EcommerceOrders_CompanyId_BranchId_EcommerceOrderId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId },
                        principalTable: "EcommerceOrders",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrderLines_EcommerceProducts_CompanyId_BranchId_EcommerceProductId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceProductId },
                        principalTable: "EcommerceProducts",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrderLines_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceOrderLines_TaxPolicies_CompanyId_TaxPolicyId",
                        columns: x => new { x.CompanyId, x.TaxPolicyId },
                        principalTable: "TaxPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMQuoteLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    QuoteId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    SKU = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMQuoteLines", x => x.Id);
                    table.CheckConstraint("CK_CRMQuoteLine_Amounts", "[LineNumber] > 0 AND [Quantity] > 0 AND [UnitPrice] > 0 AND [GrossAmount] > 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] < [GrossAmount] AND [NetAmount] > 0");
                    table.ForeignKey(
                        name: "FK_CRMQuoteLines_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMQuoteLines_CRMQuotes_CompanyId_BranchId_QuoteId",
                        columns: x => new { x.CompanyId, x.BranchId, x.QuoteId },
                        principalTable: "CRMQuotes",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMQuoteLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMQuoteLines_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CRMSalesReadyRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    QuoteId = table.Column<long>(type: "bigint", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    LeadId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BusinessPolicyId = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyMinorUnits = table.Column<int>(type: "int", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AcceptanceEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRMSalesReadyRecords", x => x.Id);
                    table.UniqueConstraint("AK_CRMSalesReadyRecords_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_CRMSalesReady_Net", "[NetAmount] > 0");
                    table.ForeignKey(
                        name: "FK_CRMSalesReadyRecords_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMSalesReadyRecords_CRMLeads_CompanyId_BranchId_LeadId",
                        columns: x => new { x.CompanyId, x.BranchId, x.LeadId },
                        principalTable: "CRMLeads",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMSalesReadyRecords_CRMOpportunities_CompanyId_BranchId_OpportunityId_LeadId",
                        columns: x => new { x.CompanyId, x.BranchId, x.OpportunityId, x.LeadId },
                        principalTable: "CRMOpportunities",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id", "LeadId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMSalesReadyRecords_CRMQuotes_CompanyId_BranchId_QuoteId",
                        columns: x => new { x.CompanyId, x.BranchId, x.QuoteId },
                        principalTable: "CRMQuotes",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMSalesReadyRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMSalesReadyRecords_CompanyBusinessPolicies_CompanyId_BusinessPolicyId",
                        columns: x => new { x.CompanyId, x.BusinessPolicyId },
                        principalTable: "CompanyBusinessPolicies",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CRMSalesReadyRecords_TradingParties_CompanyId_BranchId_CustomerId",
                        columns: x => new { x.CompanyId, x.BranchId, x.CustomerId },
                        principalTable: "TradingParties",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceFulfillments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    EcommerceOrderLineId = table.Column<long>(type: "bigint", nullable: false),
                    ProductSaleId = table.Column<long>(type: "bigint", nullable: false),
                    FinanceDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceFulfillments", x => x.Id);
                    table.UniqueConstraint("AK_EcommerceFulfillments_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_EcommerceFulfillments_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceFulfillments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceFulfillments_EcommerceOrderLines_CompanyId_BranchId_EcommerceOrderLineId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceOrderLineId },
                        principalTable: "EcommerceOrderLines",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceFulfillments_FinanceDocuments_CompanyId_BranchId_FinanceDocumentId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FinanceDocumentId },
                        principalTable: "FinanceDocuments",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceFulfillments_ProductSales_CompanyId_BranchId_ProductSaleId",
                        columns: x => new { x.CompanyId, x.BranchId, x.ProductSaleId },
                        principalTable: "ProductSales",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceReturns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    EcommerceOrderLineId = table.Column<long>(type: "bigint", nullable: false),
                    StockDocumentReturnId = table.Column<long>(type: "bigint", nullable: false),
                    RequestToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceReturns", x => x.Id);
                    table.CheckConstraint("CK_EcommerceReturn_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_EcommerceReturns_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceReturns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceReturns_EcommerceOrderLines_CompanyId_BranchId_EcommerceOrderLineId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceOrderLineId },
                        principalTable: "EcommerceOrderLines",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceReturns_StockDocumentReturns_CompanyId_BranchId_StockDocumentReturnId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockDocumentReturnId },
                        principalTable: "StockDocumentReturns",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceStockReservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    EcommerceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    EcommerceOrderLineId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceStockReservations", x => x.Id);
                    table.CheckConstraint("CK_EcommerceReservation_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_EcommerceReservation_Status", "[Status] IN ('Reserved','Released','Consumed')");
                    table.ForeignKey(
                        name: "FK_EcommerceStockReservations_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceStockReservations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceStockReservations_EcommerceOrderLines_CompanyId_BranchId_EcommerceOrderId_StockItemId_EcommerceOrderLineId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId, x.StockItemId, x.EcommerceOrderLineId },
                        principalTable: "EcommerceOrderLines",
                        principalColumns: new[] { "CompanyId", "BranchId", "EcommerceOrderId", "StockItemId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceStockReservations_EcommerceOrders_CompanyId_BranchId_EcommerceOrderId",
                        columns: x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId },
                        principalTable: "EcommerceOrders",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceStockReservations_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockItem_Reservations",
                table: "StockItems",
                sql: "[ReservedQuantity] >= 0 AND [ReservedQuantity] <= [QuantityOnHand]");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyBusinessPolicies_CompanyId_EffectiveFrom_Status",
                table: "CompanyBusinessPolicies",
                columns: new[] { "CompanyId", "EffectiveFrom", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyBusinessPolicies_CompanyId_Version",
                table: "CompanyBusinessPolicies",
                columns: new[] { "CompanyId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCurrencyLocks_CompanyId",
                table: "CompanyCurrencyLocks",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRMActivities_CompanyId_BranchId_LeadId",
                table: "CRMActivities",
                columns: new[] { "CompanyId", "BranchId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMActivities_CompanyId_BranchId_OpportunityId_LeadId",
                table: "CRMActivities",
                columns: new[] { "CompanyId", "BranchId", "OpportunityId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMActivities_CompanyId_BranchId_Reference",
                table: "CRMActivities",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRMActivities_CompanyId_BranchId_Status_DueDate",
                table: "CRMActivities",
                columns: new[] { "CompanyId", "BranchId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMConsentEvents_CompanyId_BranchId_LeadId",
                table: "CRMConsentEvents",
                columns: new[] { "CompanyId", "BranchId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMLeads_CompanyId_BranchId_CustomerId",
                table: "CRMLeads",
                columns: new[] { "CompanyId", "BranchId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMLeads_CompanyId_BranchId_Reference",
                table: "CRMLeads",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRMOpportunities_CompanyId_BranchId_LeadId",
                table: "CRMOpportunities",
                columns: new[] { "CompanyId", "BranchId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMOpportunities_CompanyId_BranchId_Reference",
                table: "CRMOpportunities",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRMQuoteLines_CompanyId_BranchId_QuoteId_LineNumber",
                table: "CRMQuoteLines",
                columns: new[] { "CompanyId", "BranchId", "QuoteId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRMQuoteLines_CompanyId_BranchId_StockItemId",
                table: "CRMQuoteLines",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMQuotes_CompanyId_BranchId_LeadId",
                table: "CRMQuotes",
                columns: new[] { "CompanyId", "BranchId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMQuotes_CompanyId_BranchId_OpportunityId_LeadId",
                table: "CRMQuotes",
                columns: new[] { "CompanyId", "BranchId", "OpportunityId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMQuotes_CompanyId_BranchId_Reference",
                table: "CRMQuotes",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRMQuotes_CompanyId_BusinessPolicyId",
                table: "CRMQuotes",
                columns: new[] { "CompanyId", "BusinessPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMSalesReadyRecords_CompanyId_BranchId_CustomerId",
                table: "CRMSalesReadyRecords",
                columns: new[] { "CompanyId", "BranchId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMSalesReadyRecords_CompanyId_BranchId_LeadId",
                table: "CRMSalesReadyRecords",
                columns: new[] { "CompanyId", "BranchId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMSalesReadyRecords_CompanyId_BranchId_OpportunityId_LeadId",
                table: "CRMSalesReadyRecords",
                columns: new[] { "CompanyId", "BranchId", "OpportunityId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMSalesReadyRecords_CompanyId_BranchId_QuoteId",
                table: "CRMSalesReadyRecords",
                columns: new[] { "CompanyId", "BranchId", "QuoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMSalesReadyRecords_CompanyId_BranchId_Reference",
                table: "CRMSalesReadyRecords",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRMSalesReadyRecords_CompanyId_BusinessPolicyId",
                table: "CRMSalesReadyRecords",
                columns: new[] { "CompanyId", "BusinessPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CRMSalesReadyRecords_OpportunityId",
                table: "CRMSalesReadyRecords",
                column: "OpportunityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRMSalesReadyRecords_QuoteId",
                table: "CRMSalesReadyRecords",
                column: "QuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_CompanyId_BranchId_EcommerceCartId",
                table: "EcommerceCartLines",
                columns: new[] { "CompanyId", "BranchId", "EcommerceCartId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_CompanyId_BranchId_EcommerceProductId",
                table: "EcommerceCartLines",
                columns: new[] { "CompanyId", "BranchId", "EcommerceProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_CompanyId_CompanyPolicyId",
                table: "EcommerceCartLines",
                columns: new[] { "CompanyId", "CompanyPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_CompanyId_TaxPolicyId",
                table: "EcommerceCartLines",
                columns: new[] { "CompanyId", "TaxPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_EcommerceCartId_EcommerceProductId",
                table: "EcommerceCartLines",
                columns: new[] { "EcommerceCartId", "EcommerceProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCarts_CompanyId_BranchId_EcommerceCustomerId",
                table: "EcommerceCarts",
                columns: new[] { "CompanyId", "BranchId", "EcommerceCustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCustomers_CompanyId_BranchId_TradingPartyId",
                table: "EcommerceCustomers",
                columns: new[] { "CompanyId", "BranchId", "TradingPartyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCustomers_CompanyId_BranchId_UserId",
                table: "EcommerceCustomers",
                columns: new[] { "CompanyId", "BranchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCustomers_UserId",
                table: "EcommerceCustomers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceFulfillments_CompanyId_BranchId_EcommerceOrderLineId",
                table: "EcommerceFulfillments",
                columns: new[] { "CompanyId", "BranchId", "EcommerceOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceFulfillments_CompanyId_BranchId_FinanceDocumentId",
                table: "EcommerceFulfillments",
                columns: new[] { "CompanyId", "BranchId", "FinanceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceFulfillments_CompanyId_BranchId_ProductSaleId",
                table: "EcommerceFulfillments",
                columns: new[] { "CompanyId", "BranchId", "ProductSaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceFulfillments_EcommerceOrderLineId",
                table: "EcommerceFulfillments",
                column: "EcommerceOrderLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceFulfillments_FinanceDocumentId",
                table: "EcommerceFulfillments",
                column: "FinanceDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceFulfillments_ProductSaleId",
                table: "EcommerceFulfillments",
                column: "ProductSaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrderEvents_CompanyId_BranchId_EcommerceOrderId",
                table: "EcommerceOrderEvents",
                columns: new[] { "CompanyId", "BranchId", "EcommerceOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrderLines_CompanyId_BranchId_EcommerceProductId",
                table: "EcommerceOrderLines",
                columns: new[] { "CompanyId", "BranchId", "EcommerceProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrderLines_CompanyId_BranchId_StockItemId",
                table: "EcommerceOrderLines",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrderLines_CompanyId_TaxPolicyId",
                table: "EcommerceOrderLines",
                columns: new[] { "CompanyId", "TaxPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrders_CompanyId_BranchId_CheckoutToken",
                table: "EcommerceOrders",
                columns: new[] { "CompanyId", "BranchId", "CheckoutToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrders_CompanyId_BranchId_EcommerceCustomerId_Id",
                table: "EcommerceOrders",
                columns: new[] { "CompanyId", "BranchId", "EcommerceCustomerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrders_CompanyId_BranchId_Reference",
                table: "EcommerceOrders",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrders_CompanyId_CompanyPolicyId",
                table: "EcommerceOrders",
                columns: new[] { "CompanyId", "CompanyPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceProducts_CompanyId_BranchId_StockItemId",
                table: "EcommerceProducts",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceProducts_CompanyId_CompanyPolicyId",
                table: "EcommerceProducts",
                columns: new[] { "CompanyId", "CompanyPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceProducts_CompanyId_TaxPolicyId",
                table: "EcommerceProducts",
                columns: new[] { "CompanyId", "TaxPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceReturns_CompanyId_BranchId_EcommerceOrderLineId",
                table: "EcommerceReturns",
                columns: new[] { "CompanyId", "BranchId", "EcommerceOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceReturns_CompanyId_BranchId_RequestToken",
                table: "EcommerceReturns",
                columns: new[] { "CompanyId", "BranchId", "RequestToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceReturns_CompanyId_BranchId_StockDocumentReturnId",
                table: "EcommerceReturns",
                columns: new[] { "CompanyId", "BranchId", "StockDocumentReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceReturns_StockDocumentReturnId",
                table: "EcommerceReturns",
                column: "StockDocumentReturnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceStockReservations_CompanyId_BranchId_EcommerceOrderId_StockItemId_EcommerceOrderLineId",
                table: "EcommerceStockReservations",
                columns: new[] { "CompanyId", "BranchId", "EcommerceOrderId", "StockItemId", "EcommerceOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceStockReservations_CompanyId_BranchId_StockItemId",
                table: "EcommerceStockReservations",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceStockReservations_EcommerceOrderLineId",
                table: "EcommerceStockReservations",
                column: "EcommerceOrderLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCustodies_CompanyId_BranchId_FixedAssetId_Id",
                table: "FixedAssetCustodies",
                columns: new[] { "CompanyId", "BranchId", "FixedAssetId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationLines_CompanyId_BranchId_FixedAssetDepreciationRunId",
                table: "FixedAssetDepreciationLines",
                columns: new[] { "CompanyId", "BranchId", "FixedAssetDepreciationRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationLines_CompanyId_BranchId_FixedAssetId_Period",
                table: "FixedAssetDepreciationLines",
                columns: new[] { "CompanyId", "BranchId", "FixedAssetId", "Period" },
                unique: true,
                filter: "[Posted] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationLines_FixedAssetDepreciationRunId_FixedAssetId",
                table: "FixedAssetDepreciationLines",
                columns: new[] { "FixedAssetDepreciationRunId", "FixedAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_CompanyId_BranchId_Period",
                table: "FixedAssetDepreciationRuns",
                columns: new[] { "CompanyId", "BranchId", "Period" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [Status] <> 'Posted'");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_CompanyId_BranchId_Reference",
                table: "FixedAssetDepreciationRuns",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_CompanyId_BranchId_VoucherId",
                table: "FixedAssetDepreciationRuns",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_CompanyId_BusinessPolicyId",
                table: "FixedAssetDepreciationRuns",
                columns: new[] { "CompanyId", "BusinessPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDepreciationRuns_VoucherId",
                table: "FixedAssetDepreciationRuns",
                column: "VoucherId",
                unique: true,
                filter: "[VoucherId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDisposals_CompanyId_BranchId_FixedAssetId",
                table: "FixedAssetDisposals",
                columns: new[] { "CompanyId", "BranchId", "FixedAssetId" },
                unique: true,
                filter: "[Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDisposals_CompanyId_BranchId_Reference",
                table: "FixedAssetDisposals",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDisposals_CompanyId_BranchId_VoucherId",
                table: "FixedAssetDisposals",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDisposals_CompanyId_BusinessPolicyId",
                table: "FixedAssetDisposals",
                columns: new[] { "CompanyId", "BusinessPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetDisposals_VoucherId",
                table: "FixedAssetDisposals",
                column: "VoucherId",
                unique: true,
                filter: "[VoucherId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_AcquisitionVoucherId",
                table: "FixedAssets",
                column: "AcquisitionVoucherId",
                unique: true,
                filter: "[AcquisitionVoucherId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId_BranchId_AcquisitionVoucherId",
                table: "FixedAssets",
                columns: new[] { "CompanyId", "BranchId", "AcquisitionVoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId_BranchId_AssetCode",
                table: "FixedAssets",
                columns: new[] { "CompanyId", "BranchId", "AssetCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId_BranchId_SourceReference",
                table: "FixedAssets",
                columns: new[] { "CompanyId", "BranchId", "SourceReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId_BusinessPolicyId",
                table: "FixedAssets",
                columns: new[] { "CompanyId", "BusinessPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollAttendance_CompanyId_BranchId_StaffId_Date",
                table: "HRPayrollAttendance",
                columns: new[] { "CompanyId", "BranchId", "StaffId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollCompensations_CompanyId_BranchId_StaffId_EffectiveFrom",
                table: "HRPayrollCompensations",
                columns: new[] { "CompanyId", "BranchId", "StaffId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollCompensations_CompanyId_BranchId_StaffId_Version",
                table: "HRPayrollCompensations",
                columns: new[] { "CompanyId", "BranchId", "StaffId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollLeaves_CompanyId_BranchId_StaffId_From_Through",
                table: "HRPayrollLeaves",
                columns: new[] { "CompanyId", "BranchId", "StaffId", "From", "Through" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollLines_CompanyId_BranchId_RunId_StaffId",
                table: "HRPayrollLines",
                columns: new[] { "CompanyId", "BranchId", "RunId", "StaffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollLines_CompanyId_BranchId_StaffId_CompensationId",
                table: "HRPayrollLines",
                columns: new[] { "CompanyId", "BranchId", "StaffId", "CompensationId" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollPolicies_CompanyId_CompanyBusinessPolicyId",
                table: "HRPayrollPolicies",
                columns: new[] { "CompanyId", "CompanyBusinessPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollRuns_CompanyId_BranchId_PeriodStart",
                table: "HRPayrollRuns",
                columns: new[] { "CompanyId", "BranchId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollRuns_CompanyId_BranchId_PolicyId",
                table: "HRPayrollRuns",
                columns: new[] { "CompanyId", "BranchId", "PolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollRuns_CompanyId_BranchId_VoucherId",
                table: "HRPayrollRuns",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollRuns_CompanyId_Reference",
                table: "HRPayrollRuns",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollStaff_CompanyId_Code",
                table: "HRPayrollStaff",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollStaff_CompanyId_IdentityUserId",
                table: "HRPayrollStaff",
                columns: new[] { "CompanyId", "IdentityUserId" },
                unique: true,
                filter: "[IdentityUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HRPayrollStaff_IdentityUserId",
                table: "HRPayrollStaff",
                column: "IdentityUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPolicies_CompanyId_Code_Version",
                table: "TaxPolicies",
                columns: new[] { "CompanyId", "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxPolicies_CompanyId_CompanyBusinessPolicyId",
                table: "TaxPolicies",
                columns: new[] { "CompanyId", "CompanyBusinessPolicyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyCurrencyLocks");

            migrationBuilder.DropTable(
                name: "CRMActivities");

            migrationBuilder.DropTable(
                name: "CRMConsentEvents");

            migrationBuilder.DropTable(
                name: "CRMQuoteLines");

            migrationBuilder.DropTable(
                name: "CRMSalesReadyRecords");

            migrationBuilder.DropTable(
                name: "EcommerceCartLines");

            migrationBuilder.DropTable(
                name: "EcommerceFulfillments");

            migrationBuilder.DropTable(
                name: "EcommerceOrderEvents");

            migrationBuilder.DropTable(
                name: "EcommerceReturns");

            migrationBuilder.DropTable(
                name: "EcommerceStockReservations");

            migrationBuilder.DropTable(
                name: "FixedAssetCustodies");

            migrationBuilder.DropTable(
                name: "FixedAssetDepreciationLines");

            migrationBuilder.DropTable(
                name: "FixedAssetDisposals");

            migrationBuilder.DropTable(
                name: "HRPayrollAttendance");

            migrationBuilder.DropTable(
                name: "HRPayrollLeaves");

            migrationBuilder.DropTable(
                name: "HRPayrollLines");

            migrationBuilder.DropTable(
                name: "CRMQuotes");

            migrationBuilder.DropTable(
                name: "EcommerceCarts");

            migrationBuilder.DropTable(
                name: "EcommerceOrderLines");

            migrationBuilder.DropTable(
                name: "FixedAssetDepreciationRuns");

            migrationBuilder.DropTable(
                name: "FixedAssets");

            migrationBuilder.DropTable(
                name: "HRPayrollCompensations");

            migrationBuilder.DropTable(
                name: "HRPayrollRuns");

            migrationBuilder.DropTable(
                name: "CRMOpportunities");

            migrationBuilder.DropTable(
                name: "EcommerceOrders");

            migrationBuilder.DropTable(
                name: "EcommerceProducts");

            migrationBuilder.DropTable(
                name: "HRPayrollStaff");

            migrationBuilder.DropTable(
                name: "HRPayrollPolicies");

            migrationBuilder.DropTable(
                name: "CRMLeads");

            migrationBuilder.DropTable(
                name: "EcommerceCustomers");

            migrationBuilder.DropTable(
                name: "TaxPolicies");

            migrationBuilder.DropTable(
                name: "CompanyBusinessPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockItem_Reservations",
                table: "StockItems");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_StockDocumentReturns_CompanyId_BranchId_Id",
                table: "StockDocumentReturns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProductSales_CompanyId_BranchId_Id",
                table: "ProductSales");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "StockItems");
        }
    }
}
