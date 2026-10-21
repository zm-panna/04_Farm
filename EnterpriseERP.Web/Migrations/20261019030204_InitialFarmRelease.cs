using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseERP.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialFarmRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrimaryColor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WhatsAppNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Audits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Audits_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                    table.UniqueConstraint("AK_AutomationRules_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_AutomationRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanySubscriptionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    NoticeDays = table.Column<int>(type: "int", nullable: false),
                    AlertDays = table.Column<int>(type: "int", nullable: false),
                    SuspendDays = table.Column<int>(type: "int", nullable: false),
                    GraceDays = table.Column<int>(type: "int", nullable: false),
                    OverrideUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySubscriptionPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySubscriptionPolicies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Farms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farms", x => x.Id);
                    table.UniqueConstraint("AK_Farms_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_Farms_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionInvoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Paid = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Cancelled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionInvoices", x => x.Id);
                    table.UniqueConstraint("AK_SubscriptionInvoices_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_SubscriptionInvoices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserCompanyAccess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanyAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompanyAccess_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationApprovals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    RuleId = table.Column<long>(type: "bigint", nullable: false),
                    RequesterId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ApproverId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationApprovals_AutomationRules_CompanyId_RuleId",
                        columns: x => new { x.CompanyId, x.RuleId },
                        principalTable: "AutomationRules",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationApprovals_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationExecutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    RuleId = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationExecutions_AutomationRules_CompanyId_RuleId",
                        columns: x => new { x.CompanyId, x.RuleId },
                        principalTable: "AutomationRules",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationExecutions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    RuleId = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DueUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledJobs_AutomationRules_CompanyId_RuleId",
                        columns: x => new { x.CompanyId, x.RuleId },
                        principalTable: "AutomationRules",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledJobs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FarmId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.UniqueConstraint("AK_Branches_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_Branches_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Branches_Farms_CompanyId_FarmId",
                        columns: x => new { x.CompanyId, x.FarmId },
                        principalTable: "Farms",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPayments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPayments_SubscriptionInvoices_CompanyId_InvoiceId",
                        columns: x => new { x.CompanyId, x.InvoiceId },
                        principalTable: "SubscriptionInvoices",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingPeriodCloses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ClosedThrough = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPeriodCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingPeriodCloses_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingPeriodCloses_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Animals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Breed = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PurchaseCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CurrentWeightKg = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Sold = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Animals", x => x.Id);
                    table.UniqueConstraint("AK_Animals_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_Animals_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Animals_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ExecutionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SKU = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockItems", x => x.Id);
                    table.UniqueConstraint("AK_StockItems_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_StockItems_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockItems_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.UniqueConstraint("AK_SupportTickets_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportTickets_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradingParties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingParties", x => x.Id);
                    table.UniqueConstraint("AK_TradingParties_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_TradingParty_Kind", "[Kind] IN ('Customer','Supplier')");
                    table.ForeignKey(
                        name: "FK_TradingParties_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradingParties_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserBranchAccess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranchAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBranchAccess_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBranchAccess_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Posted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.UniqueConstraint("AK_Vouchers_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_Vouchers_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimalHealthRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FollowUpCompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalHealthRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalHealthRecords_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalHealthRecords_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalHealthRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimalSales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalSales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalSales_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalSales_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalSales_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimalWeightRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    WeighedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalWeightRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalWeightRecords_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalWeightRecords_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalWeightRecords_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalWeightRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BreedingCycles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedCalvingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SireOrSemen = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    LastEventDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreedingCycles", x => x.Id);
                    table.UniqueConstraint("AK_BreedingCycles_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_BreedingCycles_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingCycles_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingCycles_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingCycles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FatteningPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InitialWeightKg = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TargetWeightKg = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ExpectedSaleAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FatteningPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FatteningPlans_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FatteningPlans_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FatteningPlans_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FatteningPlans_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MilkYields",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Session = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Litres = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkYields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilkYields_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MilkYields_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MilkYields_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimalFeedRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FeedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalFeedRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimalFeedRecords_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalFeedRecords_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalFeedRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnimalFeedRecords_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostBalances", x => x.Id);
                    table.CheckConstraint("CK_InventoryCostBalance_Value", "[Value] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryCostBalances_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostBalances_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostBalances_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostBalances_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReorderPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TargetQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReorderPolicies", x => x.Id);
                    table.CheckConstraint("CK_StockReorderPolicy_Levels", "[ReorderPoint] >= 0 AND [TargetQuantity] >= [ReorderPoint]");
                    table.ForeignKey(
                        name: "FK_StockReorderPolicies_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReorderPolicies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReorderPolicies_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReorderPolicies_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicketActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTicketActivities_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketActivities_SupportTickets_CompanyId_TicketId",
                        columns: x => new { x.CompanyId, x.TicketId },
                        principalTable: "SupportTickets",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseOrders_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_PurchaseOrder_Amounts", "[Quantity] > 0 AND [UnitPrice] > 0 AND [Amount] > 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] AND [ReceivedAmount] >= 0 AND [ReceivedAmount] <= [Amount]");
                    table.CheckConstraint("CK_PurchaseOrder_Status", "[Status] IN ('Draft','Approved','PartiallyReceived','Received','Cancelled')");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_StockItems_StockItemId",
                        column: x => x.StockItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_TradingParties_CompanyId_BranchId_SupplierId",
                        columns: x => new { x.CompanyId, x.BranchId, x.SupplierId },
                        principalTable: "TradingParties",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MatchedVoucherId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Vouchers_CompanyId_BranchId_MatchedVoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.MatchedVoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Vouchers_MatchedVoucherId",
                        column: x => x.MatchedVoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FarmExpenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AnimalId = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmExpenses_Animals_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FarmExpenses_Animals_CompanyId_BranchId_AnimalId",
                        columns: x => new { x.CompanyId, x.BranchId, x.AnimalId },
                        principalTable: "Animals",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FarmExpenses_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FarmExpenses_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FarmExpenses_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FarmExpenses_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyId = table.Column<int>(type: "int", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceDocuments", x => x.Id);
                    table.UniqueConstraint("AK_FinanceDocuments_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.CheckConstraint("CK_FinanceDocument_Amounts", "[Amount] > 0 AND [CostAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_FinanceDocuments_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceDocuments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceDocuments_TradingParties_CompanyId_BranchId_PartyId",
                        columns: x => new { x.CompanyId, x.BranchId, x.PartyId },
                        principalTable: "TradingParties",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceDocuments_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceDocuments_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancePayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartyType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancePayments", x => x.Id);
                    table.UniqueConstraint("AK_FinancePayments_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_FinancePayments_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePayments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePayments_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductSales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSales_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSales_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSales_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSales_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReceipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StockItemId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceipts", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseReceipts_CompanyId_BranchId_Id", x => new { x.CompanyId, x.BranchId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_StockItems_CompanyId_BranchId_StockItemId",
                        columns: x => new { x.CompanyId, x.BranchId, x.StockItemId },
                        principalTable: "StockItems",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoucherLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherLines_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BreedingCycleEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    BreedingCycleId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreedingCycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreedingCycleEvents_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingCycleEvents_BreedingCycles_BreedingCycleId",
                        column: x => x.BreedingCycleId,
                        principalTable: "BreedingCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingCycleEvents_BreedingCycles_CompanyId_BranchId_BreedingCycleId",
                        columns: x => new { x.CompanyId, x.BranchId, x.BreedingCycleId },
                        principalTable: "BreedingCycles",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingCycleEvents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockDocumentReturns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    FinanceDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDocumentReturns", x => x.Id);
                    table.CheckConstraint("CK_StockDocumentReturn_Amounts", "[Quantity] > 0 AND [Amount] > 0 AND [CostAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_StockDocumentReturns_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocumentReturns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocumentReturns_FinanceDocuments_CompanyId_BranchId_FinanceDocumentId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FinanceDocumentId },
                        principalTable: "FinanceDocuments",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocumentReturns_FinanceDocuments_FinanceDocumentId",
                        column: x => x.FinanceDocumentId,
                        principalTable: "FinanceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocumentReturns_Vouchers_CompanyId_BranchId_VoucherId",
                        columns: x => new { x.CompanyId, x.BranchId, x.VoucherId },
                        principalTable: "Vouchers",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockDocumentReturns_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancePaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    FinanceDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    FinancePaymentId = table.Column<long>(type: "bigint", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancePaymentAllocations", x => x.Id);
                    table.CheckConstraint("CK_FinancePaymentAllocation_Amount", "[SignedAmount] <> 0");
                    table.ForeignKey(
                        name: "FK_FinancePaymentAllocations_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePaymentAllocations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePaymentAllocations_FinanceDocuments_CompanyId_BranchId_FinanceDocumentId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FinanceDocumentId },
                        principalTable: "FinanceDocuments",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePaymentAllocations_FinanceDocuments_FinanceDocumentId",
                        column: x => x.FinanceDocumentId,
                        principalTable: "FinanceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePaymentAllocations_FinancePayments_CompanyId_BranchId_FinancePaymentId",
                        columns: x => new { x.CompanyId, x.BranchId, x.FinancePaymentId },
                        principalTable: "FinancePayments",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePaymentAllocations_FinancePayments_FinancePaymentId",
                        column: x => x.FinancePaymentId,
                        principalTable: "FinancePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderReceipts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderReceipts", x => x.Id);
                    table.CheckConstraint("CK_PurchaseOrderReceipt_Amounts", "[Quantity] > 0 AND [Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_Branches_CompanyId_BranchId",
                        columns: x => new { x.CompanyId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_PurchaseOrders_CompanyId_BranchId_PurchaseOrderId",
                        columns: x => new { x.CompanyId, x.BranchId, x.PurchaseOrderId },
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_PurchaseReceipts_CompanyId_BranchId_PurchaseReceiptId",
                        columns: x => new { x.CompanyId, x.BranchId, x.PurchaseReceiptId },
                        principalTable: "PurchaseReceipts",
                        principalColumns: new[] { "CompanyId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriodCloses_CompanyId_BranchId",
                table: "AccountingPeriodCloses",
                columns: new[] { "CompanyId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedRecords_CompanyId_AnimalId_FeedDate",
                table: "AnimalFeedRecords",
                columns: new[] { "CompanyId", "AnimalId", "FeedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedRecords_CompanyId_BranchId_AnimalId",
                table: "AnimalFeedRecords",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedRecords_CompanyId_BranchId_StockItemId",
                table: "AnimalFeedRecords",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalFeedRecords_CompanyId_Reference",
                table: "AnimalFeedRecords",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalHealthRecords_CompanyId_BranchId_AnimalId",
                table: "AnimalHealthRecords",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalHealthRecords_CompanyId_Reference",
                table: "AnimalHealthRecords",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Animals_CompanyId_Tag",
                table: "Animals",
                columns: new[] { "CompanyId", "Tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalSales_CompanyId_AnimalId",
                table: "AnimalSales",
                columns: new[] { "CompanyId", "AnimalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalSales_CompanyId_BranchId_AnimalId",
                table: "AnimalSales",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalSales_CompanyId_Reference",
                table: "AnimalSales",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalWeightRecords_AnimalId",
                table: "AnimalWeightRecords",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimalWeightRecords_CompanyId_AnimalId_WeighedOn",
                table: "AnimalWeightRecords",
                columns: new[] { "CompanyId", "AnimalId", "WeighedOn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnimalWeightRecords_CompanyId_BranchId_AnimalId",
                table: "AnimalWeightRecords",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalWeightRecords_CompanyId_Reference",
                table: "AnimalWeightRecords",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_CompanyId",
                table: "Audits",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationApprovals_CompanyId_RuleId",
                table: "AutomationApprovals",
                columns: new[] { "CompanyId", "RuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_CompanyId_ExecutionKey",
                table: "AutomationExecutions",
                columns: new[] { "CompanyId", "ExecutionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_CompanyId_RuleId",
                table: "AutomationExecutions",
                columns: new[] { "CompanyId", "RuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_CompanyId_BranchId_ExternalReference",
                table: "BankStatementLines",
                columns: new[] { "CompanyId", "BranchId", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_CompanyId_BranchId_MatchedVoucherId",
                table: "BankStatementLines",
                columns: new[] { "CompanyId", "BranchId", "MatchedVoucherId" },
                unique: true,
                filter: "[MatchedVoucherId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_MatchedVoucherId",
                table: "BankStatementLines",
                column: "MatchedVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_CompanyId_FarmId",
                table: "Branches",
                columns: new[] { "CompanyId", "FarmId" });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycleEvents_BreedingCycleId",
                table: "BreedingCycleEvents",
                column: "BreedingCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycleEvents_CompanyId_BranchId_BreedingCycleId",
                table: "BreedingCycleEvents",
                columns: new[] { "CompanyId", "BranchId", "BreedingCycleId" });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycleEvents_CompanyId_BreedingCycleId_EventDate",
                table: "BreedingCycleEvents",
                columns: new[] { "CompanyId", "BreedingCycleId", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycleEvents_CompanyId_Reference",
                table: "BreedingCycleEvents",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycles_AnimalId",
                table: "BreedingCycles",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycles_CompanyId_AnimalId",
                table: "BreedingCycles",
                columns: new[] { "CompanyId", "AnimalId" },
                unique: true,
                filter: "[IsClosed] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycles_CompanyId_BranchId_AnimalId",
                table: "BreedingCycles",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_BreedingCycles_CompanyId_Reference",
                table: "BreedingCycles",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptionPolicies_CompanyId",
                table: "CompanySubscriptionPolicies",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FarmExpenses_AnimalId",
                table: "FarmExpenses",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmExpenses_CompanyId_BranchId_AnimalId",
                table: "FarmExpenses",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmExpenses_CompanyId_BranchId_ExpenseDate",
                table: "FarmExpenses",
                columns: new[] { "CompanyId", "BranchId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmExpenses_CompanyId_BranchId_VoucherId",
                table: "FarmExpenses",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmExpenses_CompanyId_Reference",
                table: "FarmExpenses",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FarmExpenses_VoucherId",
                table: "FarmExpenses",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_FatteningPlans_AnimalId",
                table: "FatteningPlans",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_FatteningPlans_CompanyId_AnimalId",
                table: "FatteningPlans",
                columns: new[] { "CompanyId", "AnimalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FatteningPlans_CompanyId_BranchId_AnimalId",
                table: "FatteningPlans",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_FatteningPlans_CompanyId_Reference",
                table: "FatteningPlans",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceDocuments_CompanyId_BranchId_PartyId",
                table: "FinanceDocuments",
                columns: new[] { "CompanyId", "BranchId", "PartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceDocuments_CompanyId_BranchId_Reference",
                table: "FinanceDocuments",
                columns: new[] { "CompanyId", "BranchId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceDocuments_CompanyId_BranchId_VoucherId",
                table: "FinanceDocuments",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceDocuments_CompanyId_Kind_SourceId",
                table: "FinanceDocuments",
                columns: new[] { "CompanyId", "Kind", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceDocuments_VoucherId",
                table: "FinanceDocuments",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePaymentAllocations_CompanyId_BranchId_FinanceDocumentId",
                table: "FinancePaymentAllocations",
                columns: new[] { "CompanyId", "BranchId", "FinanceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePaymentAllocations_CompanyId_BranchId_FinancePaymentId",
                table: "FinancePaymentAllocations",
                columns: new[] { "CompanyId", "BranchId", "FinancePaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePaymentAllocations_FinanceDocumentId",
                table: "FinancePaymentAllocations",
                column: "FinanceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePaymentAllocations_FinancePaymentId",
                table: "FinancePaymentAllocations",
                column: "FinancePaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_CompanyId_BranchId_VoucherId",
                table: "FinancePayments",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_CompanyId_Reference",
                table: "FinancePayments",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostBalances_CompanyId_BranchId_StockItemId",
                table: "InventoryCostBalances",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostBalances_StockItemId",
                table: "InventoryCostBalances",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MilkYields_CompanyId_AnimalId_Date_Session",
                table: "MilkYields",
                columns: new[] { "CompanyId", "AnimalId", "Date", "Session" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MilkYields_CompanyId_BranchId_AnimalId",
                table: "MilkYields",
                columns: new[] { "CompanyId", "BranchId", "AnimalId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CompanyId_BranchId_ExecutionKey",
                table: "Notifications",
                columns: new[] { "CompanyId", "BranchId", "ExecutionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSales_CompanyId_BranchId_StockItemId",
                table: "ProductSales",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSales_CompanyId_BranchId_VoucherId",
                table: "ProductSales",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSales_CompanyId_Reference",
                table: "ProductSales",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_CompanyId_BranchId_PurchaseOrderId",
                table: "PurchaseOrderReceipts",
                columns: new[] { "CompanyId", "BranchId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_CompanyId_BranchId_PurchaseReceiptId",
                table: "PurchaseOrderReceipts",
                columns: new[] { "CompanyId", "BranchId", "PurchaseReceiptId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_PurchaseReceiptId",
                table: "PurchaseOrderReceipts",
                column: "PurchaseReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CompanyId_BranchId_StockItemId",
                table: "PurchaseOrders",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CompanyId_BranchId_SupplierId",
                table: "PurchaseOrders",
                columns: new[] { "CompanyId", "BranchId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CompanyId_Reference",
                table: "PurchaseOrders",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_StockItemId",
                table: "PurchaseOrders",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_CompanyId_BranchId_StockItemId",
                table: "PurchaseReceipts",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_CompanyId_BranchId_VoucherId",
                table: "PurchaseReceipts",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_CompanyId_Reference",
                table: "PurchaseReceipts",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_CompanyId_ExecutionKey",
                table: "ScheduledJobs",
                columns: new[] { "CompanyId", "ExecutionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_CompanyId_RuleId",
                table: "ScheduledJobs",
                columns: new[] { "CompanyId", "RuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockDocumentReturns_CompanyId_BranchId_FinanceDocumentId",
                table: "StockDocumentReturns",
                columns: new[] { "CompanyId", "BranchId", "FinanceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockDocumentReturns_CompanyId_BranchId_VoucherId",
                table: "StockDocumentReturns",
                columns: new[] { "CompanyId", "BranchId", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockDocumentReturns_CompanyId_Reference",
                table: "StockDocumentReturns",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockDocumentReturns_FinanceDocumentId",
                table: "StockDocumentReturns",
                column: "FinanceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDocumentReturns_VoucherId",
                table: "StockDocumentReturns",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_CompanyId_BranchId_SKU",
                table: "StockItems",
                columns: new[] { "CompanyId", "BranchId", "SKU" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CompanyId_BranchId_StockItemId",
                table: "StockMovements",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CompanyId_Reference_StockItemId",
                table: "StockMovements",
                columns: new[] { "CompanyId", "Reference", "StockItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReorderPolicies_CompanyId_BranchId_StockItemId",
                table: "StockReorderPolicies",
                columns: new[] { "CompanyId", "BranchId", "StockItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReorderPolicies_StockItemId",
                table: "StockReorderPolicies",
                column: "StockItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionInvoices_CompanyId_Reference",
                table: "SubscriptionInvoices",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_CompanyId_InvoiceId",
                table: "SubscriptionPayments",
                columns: new[] { "CompanyId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_CompanyId_Reference",
                table: "SubscriptionPayments",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketActivities_CompanyId_TicketId_CreatedUtc",
                table: "SupportTicketActivities",
                columns: new[] { "CompanyId", "TicketId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CompanyId_BranchId",
                table: "SupportTickets",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CompanyId_TicketNumber",
                table: "SupportTickets",
                columns: new[] { "CompanyId", "TicketNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradingParties_CompanyId_BranchId_Code",
                table: "TradingParties",
                columns: new[] { "CompanyId", "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBranchAccess_CompanyId_BranchId",
                table: "UserBranchAccess",
                columns: new[] { "CompanyId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBranchAccess_UserId_CompanyId_BranchId",
                table: "UserBranchAccess",
                columns: new[] { "UserId", "CompanyId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyAccess_CompanyId",
                table: "UserCompanyAccess",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyAccess_UserId_CompanyId",
                table: "UserCompanyAccess",
                columns: new[] { "UserId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_CompanyId",
                table: "UserPermissions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_CompanyId_Permission",
                table: "UserPermissions",
                columns: new[] { "UserId", "CompanyId", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherLines_VoucherId",
                table: "VoucherLines",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CompanyId_Reference",
                table: "Vouchers",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingPeriodCloses");

            migrationBuilder.DropTable(
                name: "AnimalFeedRecords");

            migrationBuilder.DropTable(
                name: "AnimalHealthRecords");

            migrationBuilder.DropTable(
                name: "AnimalSales");

            migrationBuilder.DropTable(
                name: "AnimalWeightRecords");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Audits");

            migrationBuilder.DropTable(
                name: "AutomationApprovals");

            migrationBuilder.DropTable(
                name: "AutomationExecutions");

            migrationBuilder.DropTable(
                name: "BankStatementLines");

            migrationBuilder.DropTable(
                name: "BreedingCycleEvents");

            migrationBuilder.DropTable(
                name: "CompanySubscriptionPolicies");

            migrationBuilder.DropTable(
                name: "FarmExpenses");

            migrationBuilder.DropTable(
                name: "FatteningPlans");

            migrationBuilder.DropTable(
                name: "FinancePaymentAllocations");

            migrationBuilder.DropTable(
                name: "InventoryCostBalances");

            migrationBuilder.DropTable(
                name: "MilkYields");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ProductSales");

            migrationBuilder.DropTable(
                name: "PurchaseOrderReceipts");

            migrationBuilder.DropTable(
                name: "ScheduledJobs");

            migrationBuilder.DropTable(
                name: "StockDocumentReturns");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockReorderPolicies");

            migrationBuilder.DropTable(
                name: "SubscriptionPayments");

            migrationBuilder.DropTable(
                name: "SupportTicketActivities");

            migrationBuilder.DropTable(
                name: "UserBranchAccess");

            migrationBuilder.DropTable(
                name: "UserCompanyAccess");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "VoucherLines");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "BreedingCycles");

            migrationBuilder.DropTable(
                name: "FinancePayments");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "PurchaseReceipts");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropTable(
                name: "FinanceDocuments");

            migrationBuilder.DropTable(
                name: "SubscriptionInvoices");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "Animals");

            migrationBuilder.DropTable(
                name: "StockItems");

            migrationBuilder.DropTable(
                name: "TradingParties");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Farms");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
