IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(200) NULL,
        [PrimaryColor] nvarchar(200) NULL,
        [WhatsAppNumber] nvarchar(200) NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [Audits] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [ActorId] nvarchar(128) NOT NULL,
        [Action] nvarchar(200) NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Audits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Audits_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AutomationRules] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Trigger] nvarchar(200) NOT NULL,
        [Action] nvarchar(200) NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_AutomationRules] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_AutomationRules_CompanyId_Id] UNIQUE ([CompanyId], [Id]),
        CONSTRAINT [FK_AutomationRules_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [CompanySubscriptionPolicies] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [NoticeDays] int NOT NULL,
        [AlertDays] int NOT NULL,
        [SuspendDays] int NOT NULL,
        [GraceDays] int NOT NULL,
        [OverrideUntil] date NULL,
        [OverrideReason] nvarchar(200) NULL,
        CONSTRAINT [PK_CompanySubscriptionPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompanySubscriptionPolicies_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [Farms] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Farms] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Farms_CompanyId_Id] UNIQUE ([CompanyId], [Id]),
        CONSTRAINT [FK_Farms_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [SubscriptionInvoices] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [DueDate] date NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Paid] decimal(18,3) NOT NULL,
        [Cancelled] bit NOT NULL,
        CONSTRAINT [PK_SubscriptionInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_SubscriptionInvoices_CompanyId_Id] UNIQUE ([CompanyId], [Id]),
        CONSTRAINT [FK_SubscriptionInvoices_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [UserCompanyAccess] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(128) NOT NULL,
        [CompanyId] int NOT NULL,
        CONSTRAINT [PK_UserCompanyAccess] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserCompanyAccess_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [UserPermissions] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] nvarchar(128) NOT NULL,
        [CompanyId] int NOT NULL,
        [Permission] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissions_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AutomationApprovals] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [RuleId] bigint NOT NULL,
        [RequesterId] nvarchar(128) NOT NULL,
        [ApproverId] nvarchar(128) NULL,
        [Status] nvarchar(200) NOT NULL,
        [PayloadHash] nvarchar(200) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AutomationApprovals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AutomationApprovals_AutomationRules_CompanyId_RuleId] FOREIGN KEY ([CompanyId], [RuleId]) REFERENCES [AutomationRules] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AutomationApprovals_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AutomationExecutions] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [RuleId] bigint NOT NULL,
        [ExecutionKey] nvarchar(200) NOT NULL,
        [Result] nvarchar(4000) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AutomationExecutions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AutomationExecutions_AutomationRules_CompanyId_RuleId] FOREIGN KEY ([CompanyId], [RuleId]) REFERENCES [AutomationRules] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AutomationExecutions_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [ScheduledJobs] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [RuleId] bigint NOT NULL,
        [ExecutionKey] nvarchar(200) NOT NULL,
        [DueUtc] datetimeoffset NOT NULL,
        [LockedUntilUtc] datetimeoffset NULL,
        [Attempt] int NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [LastError] nvarchar(4000) NULL,
        CONSTRAINT [PK_ScheduledJobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ScheduledJobs_AutomationRules_CompanyId_RuleId] FOREIGN KEY ([CompanyId], [RuleId]) REFERENCES [AutomationRules] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScheduledJobs_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [FarmId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Branches_CompanyId_Id] UNIQUE ([CompanyId], [Id]),
        CONSTRAINT [FK_Branches_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Branches_Farms_CompanyId_FarmId] FOREIGN KEY ([CompanyId], [FarmId]) REFERENCES [Farms] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [SubscriptionPayments] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [InvoiceId] bigint NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [RecordedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SubscriptionPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionPayments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SubscriptionPayments_SubscriptionInvoices_CompanyId_InvoiceId] FOREIGN KEY ([CompanyId], [InvoiceId]) REFERENCES [SubscriptionInvoices] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AccountingPeriodCloses] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [ClosedThrough] date NOT NULL,
        [ClosedBy] nvarchar(200) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [ClosedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AccountingPeriodCloses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountingPeriodCloses_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AccountingPeriodCloses_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [Animals] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Tag] nvarchar(200) NOT NULL,
        [Breed] nvarchar(200) NOT NULL,
        [PurchaseCost] decimal(18,3) NOT NULL,
        [CurrentWeightKg] decimal(18,3) NULL,
        [Sold] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Animals] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Animals_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_Animals_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Animals_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [ExecutionKey] nvarchar(200) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Category] nvarchar(200) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Notifications_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [StockItems] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [SKU] nvarchar(200) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [QuantityOnHand] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,3) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_StockItems] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_StockItems_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_StockItems_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockItems_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [SupportTickets] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [TicketNumber] nvarchar(200) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [ContactName] nvarchar(200) NOT NULL,
        [ContactPhone] nvarchar(200) NULL,
        [Priority] nvarchar(200) NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [AssignedTo] nvarchar(128) NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [UpdatedUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SupportTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_SupportTickets_CompanyId_Id] UNIQUE ([CompanyId], [Id]),
        CONSTRAINT [FK_SupportTickets_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTickets_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [TradingParties] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [Phone] nvarchar(40) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_TradingParties] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TradingParties_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_TradingParty_Kind] CHECK ([Kind] IN ('Customer','Supplier')),
        CONSTRAINT [FK_TradingParties_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TradingParties_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [UserBranchAccess] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(128) NOT NULL,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        CONSTRAINT [PK_UserBranchAccess] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserBranchAccess_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserBranchAccess_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [Vouchers] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [Date] datetimeoffset NOT NULL,
        [Posted] bit NOT NULL,
        CONSTRAINT [PK_Vouchers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Vouchers_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_Vouchers_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Vouchers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AnimalHealthRecords] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AnimalId] int NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [EventType] nvarchar(200) NOT NULL,
        [EventDate] date NOT NULL,
        [NextDueDate] date NULL,
        [FollowUpCompletedDate] date NULL,
        [Details] nvarchar(4000) NOT NULL,
        [RecordedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AnimalHealthRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnimalHealthRecords_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalHealthRecords_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalHealthRecords_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AnimalSales] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AnimalId] int NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [Date] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AnimalSales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnimalSales_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalSales_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalSales_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AnimalWeightRecords] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AnimalId] int NOT NULL,
        [Reference] nvarchar(90) NOT NULL,
        [WeighedOn] date NOT NULL,
        [WeightKg] decimal(18,3) NOT NULL,
        [Notes] nvarchar(1000) NOT NULL,
        [RecordedBy] nvarchar(450) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AnimalWeightRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnimalWeightRecords_Animals_AnimalId] FOREIGN KEY ([AnimalId]) REFERENCES [Animals] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalWeightRecords_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalWeightRecords_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalWeightRecords_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [BreedingCycles] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AnimalId] int NOT NULL,
        [Reference] nvarchar(90) NOT NULL,
        [ServiceDate] date NOT NULL,
        [ExpectedCalvingDate] date NOT NULL,
        [Method] nvarchar(30) NOT NULL,
        [SireOrSemen] nvarchar(120) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [IsClosed] bit NOT NULL,
        [LastEventDate] date NULL,
        [RecordedBy] nvarchar(450) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BreedingCycles] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_BreedingCycles_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_BreedingCycles_Animals_AnimalId] FOREIGN KEY ([AnimalId]) REFERENCES [Animals] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BreedingCycles_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BreedingCycles_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BreedingCycles_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [FatteningPlans] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AnimalId] int NOT NULL,
        [Reference] nvarchar(90) NOT NULL,
        [StartDate] date NOT NULL,
        [TargetDate] date NOT NULL,
        [InitialWeightKg] decimal(18,3) NOT NULL,
        [TargetWeightKg] decimal(18,3) NOT NULL,
        [ExpectedSaleAmount] decimal(18,3) NOT NULL,
        [RecordedBy] nvarchar(450) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_FatteningPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FatteningPlans_Animals_AnimalId] FOREIGN KEY ([AnimalId]) REFERENCES [Animals] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FatteningPlans_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FatteningPlans_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FatteningPlans_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [MilkYields] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AnimalId] int NOT NULL,
        [Date] date NOT NULL,
        [Session] nvarchar(200) NOT NULL,
        [Litres] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_MilkYields] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MilkYields_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MilkYields_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MilkYields_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [AnimalFeedRecords] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AnimalId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,3) NOT NULL,
        [CostAmount] decimal(18,3) NOT NULL,
        [FeedDate] date NOT NULL,
        [RecordedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AnimalFeedRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnimalFeedRecords_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalFeedRecords_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalFeedRecords_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AnimalFeedRecords_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [InventoryCostBalances] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [Value] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_InventoryCostBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InventoryCostBalance_Value] CHECK ([Value] >= 0),
        CONSTRAINT [FK_InventoryCostBalances_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryCostBalances_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryCostBalances_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryCostBalances_StockItems_StockItemId] FOREIGN KEY ([StockItemId]) REFERENCES [StockItems] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [StockMovements] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [QuantityDelta] decimal(18,3) NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockMovements_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockMovements_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockMovements_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [StockReorderPolicies] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [ReorderPoint] decimal(18,3) NOT NULL,
        [TargetQuantity] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_StockReorderPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockReorderPolicy_Levels] CHECK ([ReorderPoint] >= 0 AND [TargetQuantity] >= [ReorderPoint]),
        CONSTRAINT [FK_StockReorderPolicies_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockReorderPolicies_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockReorderPolicies_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockReorderPolicies_StockItems_StockItemId] FOREIGN KEY ([StockItemId]) REFERENCES [StockItems] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [SupportTicketActivities] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [TicketId] bigint NOT NULL,
        [ActorId] nvarchar(128) NOT NULL,
        [Action] nvarchar(200) NOT NULL,
        [Note] nvarchar(4000) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SupportTicketActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupportTicketActivities_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupportTicketActivities_SupportTickets_CompanyId_TicketId] FOREIGN KEY ([CompanyId], [TicketId]) REFERENCES [SupportTickets] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [PurchaseOrders] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [SupplierId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [ReceivedQuantity] decimal(18,3) NOT NULL,
        [ReceivedAmount] decimal(18,3) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ApprovedUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_PurchaseOrders_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_PurchaseOrder_Amounts] CHECK ([Quantity] > 0 AND [UnitPrice] > 0 AND [Amount] > 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] AND [ReceivedAmount] >= 0 AND [ReceivedAmount] <= [Amount]),
        CONSTRAINT [CK_PurchaseOrder_Status] CHECK ([Status] IN ('Draft','Approved','PartiallyReceived','Received','Cancelled')),
        CONSTRAINT [FK_PurchaseOrders_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_StockItems_StockItemId] FOREIGN KEY ([StockItemId]) REFERENCES [StockItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_TradingParties_CompanyId_BranchId_SupplierId] FOREIGN KEY ([CompanyId], [BranchId], [SupplierId]) REFERENCES [TradingParties] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [BankStatementLines] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [ExternalReference] nvarchar(200) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [ValueDate] date NOT NULL,
        [MatchedVoucherId] bigint NULL,
        CONSTRAINT [PK_BankStatementLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BankStatementLines_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BankStatementLines_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BankStatementLines_Vouchers_CompanyId_BranchId_MatchedVoucherId] FOREIGN KEY ([CompanyId], [BranchId], [MatchedVoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BankStatementLines_Vouchers_MatchedVoucherId] FOREIGN KEY ([MatchedVoucherId]) REFERENCES [Vouchers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [FarmExpenses] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AnimalId] int NULL,
        [Reference] nvarchar(90) NOT NULL,
        [ExpenseDate] date NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [VoucherId] bigint NOT NULL,
        [RecordedBy] nvarchar(450) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_FarmExpenses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FarmExpenses_Animals_AnimalId] FOREIGN KEY ([AnimalId]) REFERENCES [Animals] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FarmExpenses_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FarmExpenses_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FarmExpenses_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FarmExpenses_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FarmExpenses_Vouchers_VoucherId] FOREIGN KEY ([VoucherId]) REFERENCES [Vouchers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [FinanceDocuments] (
        [Id] bigint NOT NULL IDENTITY,
        [PartyId] int NULL,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [SourceId] bigint NOT NULL,
        [Reference] nvarchar(100) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [CostAmount] decimal(18,3) NOT NULL,
        [VoucherId] bigint NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_FinanceDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_FinanceDocuments_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_FinanceDocument_Amounts] CHECK ([Amount] > 0 AND [CostAmount] >= 0),
        CONSTRAINT [FK_FinanceDocuments_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinanceDocuments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinanceDocuments_TradingParties_CompanyId_BranchId_PartyId] FOREIGN KEY ([CompanyId], [BranchId], [PartyId]) REFERENCES [TradingParties] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinanceDocuments_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinanceDocuments_Vouchers_VoucherId] FOREIGN KEY ([VoucherId]) REFERENCES [Vouchers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [FinancePayments] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [PartyType] nvarchar(200) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [VoucherId] bigint NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_FinancePayments] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_FinancePayments_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_FinancePayments_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePayments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePayments_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [ProductSales] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,3) NOT NULL,
        [VoucherId] bigint NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProductSales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductSales_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductSales_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductSales_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductSales_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [PurchaseReceipts] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [VoucherId] bigint NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PurchaseReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_PurchaseReceipts_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_PurchaseReceipts_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseReceipts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseReceipts_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseReceipts_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [VoucherLines] (
        [Id] bigint NOT NULL IDENTITY,
        [VoucherId] bigint NOT NULL,
        [AccountCode] nvarchar(200) NOT NULL,
        [Debit] decimal(18,3) NOT NULL,
        [Credit] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_VoucherLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VoucherLines_Vouchers_VoucherId] FOREIGN KEY ([VoucherId]) REFERENCES [Vouchers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [BreedingCycleEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [BreedingCycleId] bigint NOT NULL,
        [Reference] nvarchar(90) NOT NULL,
        [Outcome] nvarchar(30) NOT NULL,
        [EventDate] date NOT NULL,
        [Notes] nvarchar(2000) NOT NULL,
        [RecordedBy] nvarchar(450) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_BreedingCycleEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BreedingCycleEvents_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BreedingCycleEvents_BreedingCycles_BreedingCycleId] FOREIGN KEY ([BreedingCycleId]) REFERENCES [BreedingCycles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BreedingCycleEvents_BreedingCycles_CompanyId_BranchId_BreedingCycleId] FOREIGN KEY ([CompanyId], [BranchId], [BreedingCycleId]) REFERENCES [BreedingCycles] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BreedingCycleEvents_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [StockDocumentReturns] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [FinanceDocumentId] bigint NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [CostAmount] decimal(18,3) NOT NULL,
        [VoucherId] bigint NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StockDocumentReturns] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockDocumentReturn_Amounts] CHECK ([Quantity] > 0 AND [Amount] > 0 AND [CostAmount] >= 0),
        CONSTRAINT [FK_StockDocumentReturns_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockDocumentReturns_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockDocumentReturns_FinanceDocuments_CompanyId_BranchId_FinanceDocumentId] FOREIGN KEY ([CompanyId], [BranchId], [FinanceDocumentId]) REFERENCES [FinanceDocuments] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockDocumentReturns_FinanceDocuments_FinanceDocumentId] FOREIGN KEY ([FinanceDocumentId]) REFERENCES [FinanceDocuments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockDocumentReturns_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockDocumentReturns_Vouchers_VoucherId] FOREIGN KEY ([VoucherId]) REFERENCES [Vouchers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [FinancePaymentAllocations] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [FinanceDocumentId] bigint NOT NULL,
        [FinancePaymentId] bigint NOT NULL,
        [SignedAmount] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_FinancePaymentAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_FinancePaymentAllocation_Amount] CHECK ([SignedAmount] <> 0),
        CONSTRAINT [FK_FinancePaymentAllocations_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePaymentAllocations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePaymentAllocations_FinanceDocuments_CompanyId_BranchId_FinanceDocumentId] FOREIGN KEY ([CompanyId], [BranchId], [FinanceDocumentId]) REFERENCES [FinanceDocuments] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePaymentAllocations_FinanceDocuments_FinanceDocumentId] FOREIGN KEY ([FinanceDocumentId]) REFERENCES [FinanceDocuments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePaymentAllocations_FinancePayments_CompanyId_BranchId_FinancePaymentId] FOREIGN KEY ([CompanyId], [BranchId], [FinancePaymentId]) REFERENCES [FinancePayments] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePaymentAllocations_FinancePayments_FinancePaymentId] FOREIGN KEY ([FinancePaymentId]) REFERENCES [FinancePayments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE TABLE [PurchaseOrderReceipts] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [PurchaseOrderId] bigint NOT NULL,
        [PurchaseReceiptId] bigint NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_PurchaseOrderReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PurchaseOrderReceipt_Amounts] CHECK ([Quantity] > 0 AND [Amount] > 0),
        CONSTRAINT [FK_PurchaseOrderReceipts_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderReceipts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderReceipts_PurchaseOrders_CompanyId_BranchId_PurchaseOrderId] FOREIGN KEY ([CompanyId], [BranchId], [PurchaseOrderId]) REFERENCES [PurchaseOrders] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderReceipts_PurchaseReceipts_CompanyId_BranchId_PurchaseReceiptId] FOREIGN KEY ([CompanyId], [BranchId], [PurchaseReceiptId]) REFERENCES [PurchaseReceipts] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AccountingPeriodCloses_CompanyId_BranchId] ON [AccountingPeriodCloses] ([CompanyId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AnimalFeedRecords_CompanyId_AnimalId_FeedDate] ON [AnimalFeedRecords] ([CompanyId], [AnimalId], [FeedDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AnimalFeedRecords_CompanyId_BranchId_AnimalId] ON [AnimalFeedRecords] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AnimalFeedRecords_CompanyId_BranchId_StockItemId] ON [AnimalFeedRecords] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AnimalFeedRecords_CompanyId_Reference] ON [AnimalFeedRecords] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AnimalHealthRecords_CompanyId_BranchId_AnimalId] ON [AnimalHealthRecords] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AnimalHealthRecords_CompanyId_Reference] ON [AnimalHealthRecords] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Animals_CompanyId_Tag] ON [Animals] ([CompanyId], [Tag]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AnimalSales_CompanyId_AnimalId] ON [AnimalSales] ([CompanyId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AnimalSales_CompanyId_BranchId_AnimalId] ON [AnimalSales] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AnimalSales_CompanyId_Reference] ON [AnimalSales] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AnimalWeightRecords_AnimalId] ON [AnimalWeightRecords] ([AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AnimalWeightRecords_CompanyId_AnimalId_WeighedOn] ON [AnimalWeightRecords] ([CompanyId], [AnimalId], [WeighedOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AnimalWeightRecords_CompanyId_BranchId_AnimalId] ON [AnimalWeightRecords] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AnimalWeightRecords_CompanyId_Reference] ON [AnimalWeightRecords] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_Audits_CompanyId] ON [Audits] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AutomationApprovals_CompanyId_RuleId] ON [AutomationApprovals] ([CompanyId], [RuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AutomationExecutions_CompanyId_ExecutionKey] ON [AutomationExecutions] ([CompanyId], [ExecutionKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_AutomationExecutions_CompanyId_RuleId] ON [AutomationExecutions] ([CompanyId], [RuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BankStatementLines_CompanyId_BranchId_ExternalReference] ON [BankStatementLines] ([CompanyId], [BranchId], [ExternalReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BankStatementLines_CompanyId_BranchId_MatchedVoucherId] ON [BankStatementLines] ([CompanyId], [BranchId], [MatchedVoucherId]) WHERE [MatchedVoucherId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_BankStatementLines_MatchedVoucherId] ON [BankStatementLines] ([MatchedVoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_Branches_CompanyId_FarmId] ON [Branches] ([CompanyId], [FarmId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_BreedingCycleEvents_BreedingCycleId] ON [BreedingCycleEvents] ([BreedingCycleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_BreedingCycleEvents_CompanyId_BranchId_BreedingCycleId] ON [BreedingCycleEvents] ([CompanyId], [BranchId], [BreedingCycleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_BreedingCycleEvents_CompanyId_BreedingCycleId_EventDate] ON [BreedingCycleEvents] ([CompanyId], [BreedingCycleId], [EventDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BreedingCycleEvents_CompanyId_Reference] ON [BreedingCycleEvents] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_BreedingCycles_AnimalId] ON [BreedingCycles] ([AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BreedingCycles_CompanyId_AnimalId] ON [BreedingCycles] ([CompanyId], [AnimalId]) WHERE [IsClosed] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_BreedingCycles_CompanyId_BranchId_AnimalId] ON [BreedingCycles] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BreedingCycles_CompanyId_Reference] ON [BreedingCycles] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompanySubscriptionPolicies_CompanyId] ON [CompanySubscriptionPolicies] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FarmExpenses_AnimalId] ON [FarmExpenses] ([AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FarmExpenses_CompanyId_BranchId_AnimalId] ON [FarmExpenses] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FarmExpenses_CompanyId_BranchId_ExpenseDate] ON [FarmExpenses] ([CompanyId], [BranchId], [ExpenseDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FarmExpenses_CompanyId_BranchId_VoucherId] ON [FarmExpenses] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FarmExpenses_CompanyId_Reference] ON [FarmExpenses] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FarmExpenses_VoucherId] ON [FarmExpenses] ([VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FatteningPlans_AnimalId] ON [FatteningPlans] ([AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FatteningPlans_CompanyId_AnimalId] ON [FatteningPlans] ([CompanyId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FatteningPlans_CompanyId_BranchId_AnimalId] ON [FatteningPlans] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FatteningPlans_CompanyId_Reference] ON [FatteningPlans] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FinanceDocuments_CompanyId_BranchId_PartyId] ON [FinanceDocuments] ([CompanyId], [BranchId], [PartyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FinanceDocuments_CompanyId_BranchId_Reference] ON [FinanceDocuments] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FinanceDocuments_CompanyId_BranchId_VoucherId] ON [FinanceDocuments] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FinanceDocuments_CompanyId_Kind_SourceId] ON [FinanceDocuments] ([CompanyId], [Kind], [SourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FinanceDocuments_VoucherId] ON [FinanceDocuments] ([VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FinancePaymentAllocations_CompanyId_BranchId_FinanceDocumentId] ON [FinancePaymentAllocations] ([CompanyId], [BranchId], [FinanceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FinancePaymentAllocations_CompanyId_BranchId_FinancePaymentId] ON [FinancePaymentAllocations] ([CompanyId], [BranchId], [FinancePaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FinancePaymentAllocations_FinanceDocumentId] ON [FinancePaymentAllocations] ([FinanceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FinancePaymentAllocations_FinancePaymentId] ON [FinancePaymentAllocations] ([FinancePaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_FinancePayments_CompanyId_BranchId_VoucherId] ON [FinancePayments] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FinancePayments_CompanyId_Reference] ON [FinancePayments] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryCostBalances_CompanyId_BranchId_StockItemId] ON [InventoryCostBalances] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_InventoryCostBalances_StockItemId] ON [InventoryCostBalances] ([StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MilkYields_CompanyId_AnimalId_Date_Session] ON [MilkYields] ([CompanyId], [AnimalId], [Date], [Session]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_MilkYields_CompanyId_BranchId_AnimalId] ON [MilkYields] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Notifications_CompanyId_BranchId_ExecutionKey] ON [Notifications] ([CompanyId], [BranchId], [ExecutionKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_ProductSales_CompanyId_BranchId_StockItemId] ON [ProductSales] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_ProductSales_CompanyId_BranchId_VoucherId] ON [ProductSales] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductSales_CompanyId_Reference] ON [ProductSales] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderReceipts_CompanyId_BranchId_PurchaseOrderId] ON [PurchaseOrderReceipts] ([CompanyId], [BranchId], [PurchaseOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderReceipts_CompanyId_BranchId_PurchaseReceiptId] ON [PurchaseOrderReceipts] ([CompanyId], [BranchId], [PurchaseReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseOrderReceipts_PurchaseReceiptId] ON [PurchaseOrderReceipts] ([PurchaseReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_CompanyId_BranchId_StockItemId] ON [PurchaseOrders] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_CompanyId_BranchId_SupplierId] ON [PurchaseOrders] ([CompanyId], [BranchId], [SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseOrders_CompanyId_Reference] ON [PurchaseOrders] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_StockItemId] ON [PurchaseOrders] ([StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_PurchaseReceipts_CompanyId_BranchId_StockItemId] ON [PurchaseReceipts] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_PurchaseReceipts_CompanyId_BranchId_VoucherId] ON [PurchaseReceipts] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseReceipts_CompanyId_Reference] ON [PurchaseReceipts] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ScheduledJobs_CompanyId_ExecutionKey] ON [ScheduledJobs] ([CompanyId], [ExecutionKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_ScheduledJobs_CompanyId_RuleId] ON [ScheduledJobs] ([CompanyId], [RuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_StockDocumentReturns_CompanyId_BranchId_FinanceDocumentId] ON [StockDocumentReturns] ([CompanyId], [BranchId], [FinanceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_StockDocumentReturns_CompanyId_BranchId_VoucherId] ON [StockDocumentReturns] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockDocumentReturns_CompanyId_Reference] ON [StockDocumentReturns] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_StockDocumentReturns_FinanceDocumentId] ON [StockDocumentReturns] ([FinanceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_StockDocumentReturns_VoucherId] ON [StockDocumentReturns] ([VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockItems_CompanyId_BranchId_SKU] ON [StockItems] ([CompanyId], [BranchId], [SKU]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_StockMovements_CompanyId_BranchId_StockItemId] ON [StockMovements] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockMovements_CompanyId_Reference_StockItemId] ON [StockMovements] ([CompanyId], [Reference], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockReorderPolicies_CompanyId_BranchId_StockItemId] ON [StockReorderPolicies] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_StockReorderPolicies_StockItemId] ON [StockReorderPolicies] ([StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionInvoices_CompanyId_Reference] ON [SubscriptionInvoices] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_SubscriptionPayments_CompanyId_InvoiceId] ON [SubscriptionPayments] ([CompanyId], [InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionPayments_CompanyId_Reference] ON [SubscriptionPayments] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_SupportTicketActivities_CompanyId_TicketId_CreatedUtc] ON [SupportTicketActivities] ([CompanyId], [TicketId], [CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_CompanyId_BranchId] ON [SupportTickets] ([CompanyId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SupportTickets_CompanyId_TicketNumber] ON [SupportTickets] ([CompanyId], [TicketNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TradingParties_CompanyId_BranchId_Code] ON [TradingParties] ([CompanyId], [BranchId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_UserBranchAccess_CompanyId_BranchId] ON [UserBranchAccess] ([CompanyId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserBranchAccess_UserId_CompanyId_BranchId] ON [UserBranchAccess] ([UserId], [CompanyId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_UserCompanyAccess_CompanyId] ON [UserCompanyAccess] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserCompanyAccess_UserId_CompanyId] ON [UserCompanyAccess] ([UserId], [CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_UserPermissions_CompanyId] ON [UserPermissions] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPermissions_UserId_CompanyId_Permission] ON [UserPermissions] ([UserId], [CompanyId], [Permission]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE INDEX [IX_VoucherLines_VoucherId] ON [VoucherLines] ([VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vouchers_CompanyId_Reference] ON [Vouchers] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019030204_InitialFarmRelease'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019030204_InitialFarmRelease', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    ALTER TABLE [StockItems] ADD [UnitOfMeasure] nvarchar(30) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    ALTER TABLE [StockMovements] ADD CONSTRAINT [AK_StockMovements_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE TABLE [OpeningBalances] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Reference] nvarchar(60) NOT NULL,
        [OpeningDate] date NOT NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [RecordedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [VoucherId] bigint NOT NULL,
        CONSTRAINT [PK_OpeningBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_OpeningBalances_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_OpeningBalances_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningBalances_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningBalances_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningBalances_Vouchers_VoucherId] FOREIGN KEY ([VoucherId]) REFERENCES [Vouchers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE TABLE [StockTransfers] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [SourceBranchId] int NOT NULL,
        [DestinationBranchId] int NOT NULL,
        [SourceStockItemId] int NOT NULL,
        [DestinationStockItemId] int NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [Note] nvarchar(500) NOT NULL,
        [SourceBranchName] nvarchar(200) NOT NULL,
        [DestinationBranchName] nvarchar(200) NOT NULL,
        [SourceSku] nvarchar(200) NOT NULL,
        [DestinationSku] nvarchar(200) NOT NULL,
        [SourceProductName] nvarchar(200) NOT NULL,
        [DestinationProductName] nvarchar(200) NOT NULL,
        [UnitOfMeasure] nvarchar(30) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [CostAmount] decimal(18,3) NOT NULL,
        [SourceQuantityBefore] decimal(18,3) NOT NULL,
        [SourceQuantityAfter] decimal(18,3) NOT NULL,
        [DestinationQuantityBefore] decimal(18,3) NOT NULL,
        [DestinationQuantityAfter] decimal(18,3) NOT NULL,
        [SourceValueBefore] decimal(18,3) NOT NULL,
        [SourceValueAfter] decimal(18,3) NOT NULL,
        [DestinationValueBefore] decimal(18,3) NOT NULL,
        [DestinationValueAfter] decimal(18,3) NOT NULL,
        [SourceMovementId] bigint NOT NULL,
        [DestinationMovementId] bigint NOT NULL,
        [SourceVoucherId] bigint NULL,
        [DestinationVoucherId] bigint NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StockTransfers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockTransfer_DifferentBranches] CHECK ([SourceBranchId] <> [DestinationBranchId] AND [SourceStockItemId] <> [DestinationStockItemId] AND [SourceMovementId] <> [DestinationMovementId]),
        CONSTRAINT [CK_StockTransfer_NonnegativeBalances] CHECK ([SourceQuantityBefore] >= 0 AND [SourceQuantityAfter] >= 0 AND [DestinationQuantityBefore] >= 0 AND [DestinationQuantityAfter] >= 0 AND [SourceValueBefore] >= 0 AND [SourceValueAfter] >= 0 AND [DestinationValueBefore] >= 0 AND [DestinationValueAfter] >= 0),
        CONSTRAINT [CK_StockTransfer_PairedVouchers] CHECK (([CostAmount] = 0 AND [SourceVoucherId] IS NULL AND [DestinationVoucherId] IS NULL) OR ([CostAmount] > 0 AND [SourceVoucherId] IS NOT NULL AND [DestinationVoucherId] IS NOT NULL AND [SourceVoucherId] <> [DestinationVoucherId])),
        CONSTRAINT [CK_StockTransfer_PositiveQuantity] CHECK ([Quantity] > 0 AND [CostAmount] >= 0),
        CONSTRAINT [FK_StockTransfers_Branches_CompanyId_DestinationBranchId] FOREIGN KEY ([CompanyId], [DestinationBranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_Branches_CompanyId_SourceBranchId] FOREIGN KEY ([CompanyId], [SourceBranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_StockItems_CompanyId_DestinationBranchId_DestinationStockItemId] FOREIGN KEY ([CompanyId], [DestinationBranchId], [DestinationStockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_StockItems_CompanyId_SourceBranchId_SourceStockItemId] FOREIGN KEY ([CompanyId], [SourceBranchId], [SourceStockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_StockMovements_CompanyId_DestinationBranchId_DestinationMovementId] FOREIGN KEY ([CompanyId], [DestinationBranchId], [DestinationMovementId]) REFERENCES [StockMovements] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_StockMovements_CompanyId_SourceBranchId_SourceMovementId] FOREIGN KEY ([CompanyId], [SourceBranchId], [SourceMovementId]) REFERENCES [StockMovements] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_Vouchers_CompanyId_DestinationBranchId_DestinationVoucherId] FOREIGN KEY ([CompanyId], [DestinationBranchId], [DestinationVoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransfers_Vouchers_CompanyId_SourceBranchId_SourceVoucherId] FOREIGN KEY ([CompanyId], [SourceBranchId], [SourceVoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE TABLE [OpeningAnimalValues] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [OpeningBalanceId] bigint NOT NULL,
        [AnimalId] int NOT NULL,
        [Value] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_OpeningAnimalValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OpeningAnimalValues_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningAnimalValues_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningAnimalValues_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningAnimalValues_OpeningBalances_CompanyId_BranchId_OpeningBalanceId] FOREIGN KEY ([CompanyId], [BranchId], [OpeningBalanceId]) REFERENCES [OpeningBalances] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE TABLE [OpeningStockValues] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [OpeningBalanceId] bigint NOT NULL,
        [StockItemId] int NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Value] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_OpeningStockValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OpeningStockValues_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningStockValues_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningStockValues_OpeningBalances_CompanyId_BranchId_OpeningBalanceId] FOREIGN KEY ([CompanyId], [BranchId], [OpeningBalanceId]) REFERENCES [OpeningBalances] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OpeningStockValues_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OpeningAnimalValues_CompanyId_BranchId_AnimalId] ON [OpeningAnimalValues] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_OpeningAnimalValues_CompanyId_BranchId_OpeningBalanceId] ON [OpeningAnimalValues] ([CompanyId], [BranchId], [OpeningBalanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OpeningBalances_CompanyId_BranchId] ON [OpeningBalances] ([CompanyId], [BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_OpeningBalances_CompanyId_BranchId_VoucherId] ON [OpeningBalances] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OpeningBalances_CompanyId_Reference] ON [OpeningBalances] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_OpeningBalances_VoucherId] ON [OpeningBalances] ([VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_OpeningStockValues_CompanyId_BranchId_OpeningBalanceId] ON [OpeningStockValues] ([CompanyId], [BranchId], [OpeningBalanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OpeningStockValues_CompanyId_BranchId_StockItemId] ON [OpeningStockValues] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_DestinationBranchId_CreatedUtc] ON [StockTransfers] ([CompanyId], [DestinationBranchId], [CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_DestinationBranchId_DestinationMovementId] ON [StockTransfers] ([CompanyId], [DestinationBranchId], [DestinationMovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_DestinationBranchId_DestinationStockItemId] ON [StockTransfers] ([CompanyId], [DestinationBranchId], [DestinationStockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_DestinationBranchId_DestinationVoucherId] ON [StockTransfers] ([CompanyId], [DestinationBranchId], [DestinationVoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockTransfers_CompanyId_Reference] ON [StockTransfers] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_SourceBranchId_CreatedUtc] ON [StockTransfers] ([CompanyId], [SourceBranchId], [CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_SourceBranchId_SourceMovementId] ON [StockTransfers] ([CompanyId], [SourceBranchId], [SourceMovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_SourceBranchId_SourceStockItemId] ON [StockTransfers] ([CompanyId], [SourceBranchId], [SourceStockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_SourceBranchId_SourceVoucherId] ON [StockTransfers] ([CompanyId], [SourceBranchId], [SourceVoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockTransfers_DestinationMovementId] ON [StockTransfers] ([DestinationMovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StockTransfers_DestinationVoucherId] ON [StockTransfers] ([DestinationVoucherId]) WHERE [DestinationVoucherId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockTransfers_SourceMovementId] ON [StockTransfers] ([SourceMovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StockTransfers_SourceVoucherId] ON [StockTransfers] ([SourceVoucherId]) WHERE [SourceVoucherId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019032129_CompleteCoreIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019032129_CompleteCoreIntegrity', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    ALTER TABLE [StockItems] ADD [ReservedQuantity] decimal(18,3) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    ALTER TABLE [StockDocumentReturns] ADD CONSTRAINT [AK_StockDocumentReturns_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    ALTER TABLE [ProductSales] ADD CONSTRAINT [AK_ProductSales_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CompanyBusinessPolicies] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Version] int NOT NULL,
        [CountryCode] nvarchar(200) NOT NULL,
        [RegionCode] nvarchar(200) NOT NULL,
        [CurrencyCode] nvarchar(200) NOT NULL,
        [CurrencyMinorUnits] int NOT NULL,
        [TimeZoneId] nvarchar(200) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [ReviewBasis] nvarchar(2000) NOT NULL,
        [ExistingLedgerCurrencyConfirmed] bit NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [ApprovedUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CompanyBusinessPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CompanyBusinessPolicies_CompanyId_Id] UNIQUE ([CompanyId], [Id]),
        CONSTRAINT [CK_CompanyBusinessPolicy_MinorUnits] CHECK ([CurrencyMinorUnits] BETWEEN 0 AND 3),
        CONSTRAINT [CK_CompanyBusinessPolicy_Status] CHECK ([Status] IN ('Draft','Approved','Rejected')),
        CONSTRAINT [FK_CompanyBusinessPolicies_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CompanyCurrencyLocks] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [CurrencyCode] nvarchar(200) NOT NULL,
        [CurrencyMinorUnits] int NOT NULL,
        [ConfirmedBy] nvarchar(200) NOT NULL,
        [ConfirmedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CompanyCurrencyLocks] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CompanyCurrencyLock_MinorUnits] CHECK ([CurrencyMinorUnits] BETWEEN 0 AND 3),
        CONSTRAINT [FK_CompanyCurrencyLocks_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CRMLeads] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [ContactName] nvarchar(200) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [Phone] nvarchar(40) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [Source] nvarchar(200) NOT NULL,
        [Notes] nvarchar(2000) NOT NULL,
        [MarketingConsent] nvarchar(200) NOT NULL,
        [ConsentEvidence] nvarchar(1000) NOT NULL,
        [ConsentRecordedUtc] datetimeoffset NULL,
        [Status] nvarchar(200) NOT NULL,
        [CustomerId] int NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [UpdatedUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CRMLeads] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CRMLeads_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_CRMLead_Consent] CHECK ([MarketingConsent] IN ('Unknown','Granted','Withdrawn')),
        CONSTRAINT [CK_CRMLead_Converted] CHECK ([Status] <> 'Converted' OR [CustomerId] IS NOT NULL),
        CONSTRAINT [CK_CRMLead_Status] CHECK ([Status] IN ('Active','Converted','Archived')),
        CONSTRAINT [FK_CRMLeads_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMLeads_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMLeads_TradingParties_CompanyId_BranchId_CustomerId] FOREIGN KEY ([CompanyId], [BranchId], [CustomerId]) REFERENCES [TradingParties] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceCustomers] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [UserId] nvarchar(128) NOT NULL,
        [TradingPartyId] int NOT NULL,
        [Active] bit NOT NULL,
        [LinkedBy] nvarchar(200) NOT NULL,
        [LinkedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EcommerceCustomers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EcommerceCustomers_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_EcommerceCustomers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCustomers_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCustomers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCustomers_TradingParties_CompanyId_BranchId_TradingPartyId] FOREIGN KEY ([CompanyId], [BranchId], [TradingPartyId]) REFERENCES [TradingParties] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [HRPayrollStaff] (
        [Id] int NOT NULL IDENTITY,
        [PayBasis] nvarchar(200) NOT NULL,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [JobTitle] nvarchar(200) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [Active] bit NOT NULL,
        [IdentityUserId] nvarchar(128) NULL,
        [DailyRate] decimal(18,3) NOT NULL,
        [MonthlySalary] decimal(18,3) NOT NULL,
        [OvertimeHourlyRate] decimal(18,3) NOT NULL,
        [Revision] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_HRPayrollStaff] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_HRPayrollStaff_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_HRPayrollStaff_AspNetUsers_IdentityUserId] FOREIGN KEY ([IdentityUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollStaff_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollStaff_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [FixedAssetDepreciationRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Period] date NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [BusinessPolicyId] bigint NOT NULL,
        [BusinessPolicyVersion] int NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [CurrencyMinorUnits] int NOT NULL,
        [CancellationReason] nvarchar(1000) NOT NULL,
        [CancelledBy] nvarchar(200) NULL,
        [CancelledUtc] datetimeoffset NULL,
        [Status] nvarchar(200) NOT NULL,
        [Total] decimal(18,3) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [ApprovedUtc] datetimeoffset NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [PostedBy] nvarchar(200) NULL,
        [PostedUtc] datetimeoffset NULL,
        [VoucherId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_FixedAssetDepreciationRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_FixedAssetDepreciationRuns_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_FixedAssetRun_Status] CHECK ([Status] IN ('Draft','Approved','Posted','Cancelled')),
        CONSTRAINT [CK_FixedAssetRun_Total] CHECK ([Total] >= 0),
        CONSTRAINT [FK_FixedAssetDepreciationRuns_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDepreciationRuns_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDepreciationRuns_CompanyBusinessPolicies_CompanyId_BusinessPolicyId] FOREIGN KEY ([CompanyId], [BusinessPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDepreciationRuns_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [FixedAssets] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [AssetCode] nvarchar(60) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Category] nvarchar(200) NOT NULL,
        [SerialNumber] nvarchar(200) NOT NULL,
        [Notes] nvarchar(1000) NOT NULL,
        [AcquisitionDate] date NOT NULL,
        [Cost] decimal(18,3) NOT NULL,
        [AcquisitionSettlement] nvarchar(200) NOT NULL,
        [SourceReference] nvarchar(80) NOT NULL,
        [DepreciationMethod] nvarchar(200) NOT NULL,
        [UsefulLifeMonths] int NOT NULL,
        [ResidualValue] decimal(18,3) NOT NULL,
        [DepreciationStart] date NOT NULL,
        [PolicyReason] nvarchar(1000) NOT NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [BusinessPolicyId] bigint NOT NULL,
        [BusinessPolicyVersion] int NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [CurrencyMinorUnits] int NOT NULL,
        [CancellationReason] nvarchar(1000) NOT NULL,
        [CancelledBy] nvarchar(200) NULL,
        [CancelledUtc] datetimeoffset NULL,
        [Status] nvarchar(200) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [ApprovedUtc] datetimeoffset NULL,
        [PostedBy] nvarchar(200) NULL,
        [PostedUtc] datetimeoffset NULL,
        [AcquisitionVoucherId] bigint NULL,
        [AccumulatedDepreciation] decimal(18,3) NOT NULL,
        [LastDepreciationPeriod] date NULL,
        [FinancialVersion] int NOT NULL,
        [Custodian] nvarchar(200) NOT NULL,
        [Location] nvarchar(200) NOT NULL,
        [DisposedOn] date NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_FixedAssets] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_FixedAssets_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_FixedAsset_Policy] CHECK (([DepreciationMethod] = 'StraightLineMonthly' AND [UsefulLifeMonths] BETWEEN 1 AND 1200) OR ([DepreciationMethod] = 'None' AND [UsefulLifeMonths] = 0)),
        CONSTRAINT [CK_FixedAsset_Status] CHECK ([Status] IN ('Draft','Approved','Active','Disposed','Cancelled')),
        CONSTRAINT [CK_FixedAsset_Values] CHECK ([Cost] >= 0 AND [ResidualValue] >= 0 AND [ResidualValue] <= [Cost] AND [AccumulatedDepreciation] >= 0 AND ROUND([Cost] - [ResidualValue] - [AccumulatedDepreciation], 3) >= 0),
        CONSTRAINT [FK_FixedAssets_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssets_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssets_CompanyBusinessPolicies_CompanyId_BusinessPolicyId] FOREIGN KEY ([CompanyId], [BusinessPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssets_Vouchers_CompanyId_BranchId_AcquisitionVoucherId] FOREIGN KEY ([CompanyId], [BranchId], [AcquisitionVoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [HRPayrollPolicies] (
        [Id] int NOT NULL IDENTITY,
        [PayBasis] nvarchar(200) NOT NULL,
        [MonthlyProrationMode] nvarchar(200) NOT NULL,
        [MonthlyFixedUnits] decimal(18,3) NOT NULL,
        [CapMonthlyPaidUnits] bit NOT NULL,
        [CompanyBusinessPolicyId] bigint NOT NULL,
        [CountryCode] nvarchar(200) NOT NULL,
        [RegionCode] nvarchar(200) NOT NULL,
        [TimeZoneId] nvarchar(200) NOT NULL,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Currency] nvarchar(200) NOT NULL,
        [CurrencyDecimals] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveThrough] date NOT NULL,
        [DeductionPercent] decimal(18,3) NOT NULL,
        [EmployerContributionPercent] decimal(18,3) NOT NULL,
        [JurisdictionAndBasis] nvarchar(2000) NOT NULL,
        [ExpenseAccount] nvarchar(200) NOT NULL,
        [PayableAccount] nvarchar(200) NOT NULL,
        [DeductionAccount] nvarchar(200) NOT NULL,
        [EmployerExpenseAccount] nvarchar(200) NOT NULL,
        [EmployerPayableAccount] nvarchar(200) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [Reviewed] bit NOT NULL,
        [ReviewedBy] nvarchar(200) NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [Revision] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_HRPayrollPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_HRPayrollPolicies_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_HRPayrollPolicies_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollPolicies_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollPolicies_CompanyBusinessPolicies_CompanyId_CompanyBusinessPolicyId] FOREIGN KEY ([CompanyId], [CompanyBusinessPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [TaxPolicies] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [CompanyBusinessPolicyId] bigint NOT NULL,
        [Code] nvarchar(200) NOT NULL,
        [Version] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CurrencyCode] nvarchar(200) NOT NULL,
        [CountryCode] nvarchar(200) NOT NULL,
        [RegionCode] nvarchar(200) NOT NULL,
        [TaxTreatment] nvarchar(200) NOT NULL,
        [RatePercent] decimal(18,3) NOT NULL,
        [TaxLiabilityAccount] nvarchar(200) NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [Status] nvarchar(200) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [ApprovedUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TaxPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TaxPolicies_CompanyId_Id] UNIQUE ([CompanyId], [Id]),
        CONSTRAINT [CK_TaxPolicy_Rate] CHECK ([RatePercent] >= 0 AND [RatePercent] <= 100 AND (([TaxTreatment] = 'Standard' AND [RatePercent] > 0) OR ([TaxTreatment] IN ('Exempt','OutOfScope') AND [RatePercent] = 0))),
        CONSTRAINT [CK_TaxPolicy_Status] CHECK ([Status] IN ('Draft','Approved','Rejected')),
        CONSTRAINT [FK_TaxPolicies_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaxPolicies_CompanyBusinessPolicies_CompanyId_CompanyBusinessPolicyId] FOREIGN KEY ([CompanyId], [CompanyBusinessPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CRMConsentEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [LeadId] bigint NOT NULL,
        [PreviousConsent] nvarchar(200) NOT NULL,
        [Consent] nvarchar(200) NOT NULL,
        [Evidence] nvarchar(1000) NOT NULL,
        [ActorId] nvarchar(128) NOT NULL,
        [RecordedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CRMConsentEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CRMConsentEvent_Consent] CHECK ([Consent] IN ('Unknown','Granted','Withdrawn') AND [PreviousConsent] IN ('Unknown','Granted','Withdrawn')),
        CONSTRAINT [FK_CRMConsentEvents_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMConsentEvents_CRMLeads_CompanyId_BranchId_LeadId] FOREIGN KEY ([CompanyId], [BranchId], [LeadId]) REFERENCES [CRMLeads] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMConsentEvents_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CRMOpportunities] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [LeadId] bigint NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Stage] nvarchar(200) NOT NULL,
        [ExpectedCloseDate] date NOT NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [ReviewedBy] nvarchar(200) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [UpdatedUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CRMOpportunities] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CRMOpportunities_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [AK_CRMOpportunities_CompanyId_BranchId_Id_LeadId] UNIQUE ([CompanyId], [BranchId], [Id], [LeadId]),
        CONSTRAINT [CK_CRMOpportunity_Stage] CHECK ([Stage] IN ('Qualified','Proposal','Negotiation','Won','Lost')),
        CONSTRAINT [FK_CRMOpportunities_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMOpportunities_CRMLeads_CompanyId_BranchId_LeadId] FOREIGN KEY ([CompanyId], [BranchId], [LeadId]) REFERENCES [CRMLeads] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMOpportunities_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceCarts] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [EcommerceCustomerId] bigint NOT NULL,
        [RevisionToken] nvarchar(32) NOT NULL,
        [ExpiresUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EcommerceCarts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EcommerceCarts_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_EcommerceCarts_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCarts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCarts_EcommerceCustomers_CompanyId_BranchId_EcommerceCustomerId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceCustomerId]) REFERENCES [EcommerceCustomers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceOrders] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [EcommerceCustomerId] bigint NOT NULL,
        [CompanyPolicyId] bigint NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [CheckoutToken] nvarchar(32) NOT NULL,
        [StateToken] nvarchar(32) NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [CurrencyCode] nvarchar(200) NOT NULL,
        [CurrencyMinorUnits] int NOT NULL,
        [CountryCode] nvarchar(200) NOT NULL,
        [RegionCode] nvarchar(200) NOT NULL,
        [NetAmount] decimal(18,3) NOT NULL,
        [TaxAmount] decimal(18,3) NOT NULL,
        [GrossAmount] decimal(18,3) NOT NULL,
        [PaymentStatus] nvarchar(200) NOT NULL,
        [DeliveryMethod] nvarchar(200) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        CONSTRAINT [PK_EcommerceOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EcommerceOrders_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_EcommerceOrder_Amounts] CHECK ([NetAmount] > 0 AND [TaxAmount] >= 0 AND [GrossAmount] > 0),
        CONSTRAINT [CK_EcommerceOrder_Delivery] CHECK ([DeliveryMethod] = 'BranchCollection'),
        CONSTRAINT [CK_EcommerceOrder_Payment] CHECK ([PaymentStatus] = 'PendingUnverified'),
        CONSTRAINT [CK_EcommerceOrder_Status] CHECK ([Status] IN ('Submitted','Approved','Fulfilled','Cancelled')),
        CONSTRAINT [FK_EcommerceOrders_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrders_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrders_CompanyBusinessPolicies_CompanyId_CompanyPolicyId] FOREIGN KEY ([CompanyId], [CompanyPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrders_EcommerceCustomers_CompanyId_BranchId_EcommerceCustomerId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceCustomerId]) REFERENCES [EcommerceCustomers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [HRPayrollAttendance] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StaffId] int NOT NULL,
        [Date] date NOT NULL,
        [PaidDays] decimal(18,3) NOT NULL,
        [OvertimeHours] decimal(18,3) NOT NULL,
        [Note] nvarchar(4000) NOT NULL,
        [RecordedBy] nvarchar(128) NOT NULL,
        [Revision] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_HRPayrollAttendance] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HRPayrollAttendance_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollAttendance_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollAttendance_HRPayrollStaff_CompanyId_BranchId_StaffId] FOREIGN KEY ([CompanyId], [BranchId], [StaffId]) REFERENCES [HRPayrollStaff] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [HRPayrollCompensations] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StaffId] int NOT NULL,
        [Version] int NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [PayBasis] nvarchar(200) NOT NULL,
        [DailyRate] decimal(18,3) NOT NULL,
        [MonthlySalary] decimal(18,3) NOT NULL,
        [OvertimeHourlyRate] decimal(18,3) NOT NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [ReviewedBy] nvarchar(200) NOT NULL,
        [ReviewedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_HRPayrollCompensations] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_HRPayrollCompensations_CompanyId_BranchId_StaffId_Id] UNIQUE ([CompanyId], [BranchId], [StaffId], [Id]),
        CONSTRAINT [FK_HRPayrollCompensations_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollCompensations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollCompensations_HRPayrollStaff_CompanyId_BranchId_StaffId] FOREIGN KEY ([CompanyId], [BranchId], [StaffId]) REFERENCES [HRPayrollStaff] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [HRPayrollLeaves] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StaffId] int NOT NULL,
        [From] date NOT NULL,
        [Through] date NOT NULL,
        [Paid] bit NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [RequestedBy] nvarchar(200) NOT NULL,
        [ReviewedBy] nvarchar(200) NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [Revision] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_HRPayrollLeaves] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HRPayrollLeaves_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollLeaves_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollLeaves_HRPayrollStaff_CompanyId_BranchId_StaffId] FOREIGN KEY ([CompanyId], [BranchId], [StaffId]) REFERENCES [HRPayrollStaff] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [FixedAssetCustodies] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [FixedAssetId] bigint NOT NULL,
        [EffectiveDate] date NOT NULL,
        [PreviousCustodian] nvarchar(200) NOT NULL,
        [PreviousLocation] nvarchar(200) NOT NULL,
        [Custodian] nvarchar(200) NOT NULL,
        [Location] nvarchar(200) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [RecordedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_FixedAssetCustodies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FixedAssetCustodies_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetCustodies_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetCustodies_FixedAssets_CompanyId_BranchId_FixedAssetId] FOREIGN KEY ([CompanyId], [BranchId], [FixedAssetId]) REFERENCES [FixedAssets] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [FixedAssetDepreciationLines] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [FixedAssetDepreciationRunId] bigint NOT NULL,
        [FixedAssetId] bigint NOT NULL,
        [AssetCode] nvarchar(60) NOT NULL,
        [Period] date NOT NULL,
        [ExpectedFinancialVersion] int NOT NULL,
        [Cost] decimal(18,3) NOT NULL,
        [ResidualValue] decimal(18,3) NOT NULL,
        [AccumulatedBefore] decimal(18,3) NOT NULL,
        [Amount] decimal(18,3) NOT NULL,
        [AccumulatedAfter] decimal(18,3) NOT NULL,
        [Posted] bit NOT NULL,
        CONSTRAINT [PK_FixedAssetDepreciationLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_FixedAssetLine_Values] CHECK ([Amount] >= 0 AND [AccumulatedBefore] >= 0 AND ROUND([Cost] - [ResidualValue] - [AccumulatedAfter], 3) >= 0),
        CONSTRAINT [FK_FixedAssetDepreciationLines_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDepreciationLines_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDepreciationLines_FixedAssetDepreciationRuns_CompanyId_BranchId_FixedAssetDepreciationRunId] FOREIGN KEY ([CompanyId], [BranchId], [FixedAssetDepreciationRunId]) REFERENCES [FixedAssetDepreciationRuns] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDepreciationLines_FixedAssets_CompanyId_BranchId_FixedAssetId] FOREIGN KEY ([CompanyId], [BranchId], [FixedAssetId]) REFERENCES [FixedAssets] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [FixedAssetDisposals] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [FixedAssetId] bigint NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [DisposalDate] date NOT NULL,
        [Counterparty] nvarchar(200) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [Proceeds] decimal(18,3) NOT NULL,
        [Settlement] nvarchar(200) NOT NULL,
        [Cost] decimal(18,3) NOT NULL,
        [AccumulatedDepreciation] decimal(18,3) NOT NULL,
        [CarryingValue] decimal(18,3) NOT NULL,
        [GainLoss] decimal(18,3) NOT NULL,
        [ExpectedFinancialVersion] int NOT NULL,
        [BusinessPolicyId] bigint NOT NULL,
        [BusinessPolicyVersion] int NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [CurrencyMinorUnits] int NOT NULL,
        [CancellationReason] nvarchar(1000) NOT NULL,
        [CancelledBy] nvarchar(200) NULL,
        [CancelledUtc] datetimeoffset NULL,
        [Status] nvarchar(200) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [ApprovedUtc] datetimeoffset NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [PostedBy] nvarchar(200) NULL,
        [PostedUtc] datetimeoffset NULL,
        [VoucherId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_FixedAssetDisposals] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_FixedAssetDisposal_Status] CHECK ([Status] IN ('Draft','Approved','Posted','Cancelled')),
        CONSTRAINT [CK_FixedAssetDisposal_Values] CHECK ([Proceeds] >= 0 AND [CarryingValue] >= 0),
        CONSTRAINT [FK_FixedAssetDisposals_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDisposals_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDisposals_CompanyBusinessPolicies_CompanyId_BusinessPolicyId] FOREIGN KEY ([CompanyId], [BusinessPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDisposals_FixedAssets_CompanyId_BranchId_FixedAssetId] FOREIGN KEY ([CompanyId], [BranchId], [FixedAssetId]) REFERENCES [FixedAssets] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FixedAssetDisposals_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [HRPayrollRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [PolicyId] int NOT NULL,
        [Reference] nvarchar(60) NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [Currency] nvarchar(200) NOT NULL,
        [CurrencyDecimals] int NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [SourceHash] nvarchar(200) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [ApprovedBy] nvarchar(200) NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [ApprovedUtc] datetimeoffset NULL,
        [PostedBy] nvarchar(200) NULL,
        [PostedUtc] datetimeoffset NULL,
        [PostingDate] date NULL,
        [VoucherId] bigint NULL,
        [Revision] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_HRPayrollRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_HRPayrollRuns_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_HRPayrollRuns_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollRuns_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollRuns_HRPayrollPolicies_CompanyId_BranchId_PolicyId] FOREIGN KEY ([CompanyId], [BranchId], [PolicyId]) REFERENCES [HRPayrollPolicies] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollRuns_Vouchers_CompanyId_BranchId_VoucherId] FOREIGN KEY ([CompanyId], [BranchId], [VoucherId]) REFERENCES [Vouchers] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceProducts] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [CompanyPolicyId] bigint NOT NULL,
        [TaxPolicyId] bigint NOT NULL,
        [CurrencyCode] nvarchar(200) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Category] nvarchar(200) NOT NULL,
        [NetUnitPrice] decimal(18,3) NOT NULL,
        [Published] bit NOT NULL,
        [RevisionToken] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_EcommerceProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EcommerceProducts_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_EcommerceProduct_Price] CHECK ([NetUnitPrice] > 0),
        CONSTRAINT [FK_EcommerceProducts_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceProducts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceProducts_CompanyBusinessPolicies_CompanyId_CompanyPolicyId] FOREIGN KEY ([CompanyId], [CompanyPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceProducts_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceProducts_TaxPolicies_CompanyId_TaxPolicyId] FOREIGN KEY ([CompanyId], [TaxPolicyId]) REFERENCES [TaxPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CRMActivities] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [LeadId] bigint NOT NULL,
        [OpportunityId] bigint NULL,
        [Reference] nvarchar(80) NOT NULL,
        [Kind] nvarchar(200) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Notes] nvarchar(2000) NOT NULL,
        [DueDate] date NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [CompletionNote] nvarchar(1000) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CompletedBy] nvarchar(200) NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [CompletedUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CRMActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CRMActivities_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_CRMActivity_Completion] CHECK (([Status] = 'Open' AND [CompletedUtc] IS NULL AND [CompletedBy] IS NULL) OR ([Status] <> 'Open' AND [CompletedUtc] IS NOT NULL AND [CompletedBy] IS NOT NULL)),
        CONSTRAINT [CK_CRMActivity_Kind] CHECK ([Kind] IN ('Task','Call','Meeting')),
        CONSTRAINT [CK_CRMActivity_Status] CHECK ([Status] IN ('Open','Done','Cancelled')),
        CONSTRAINT [FK_CRMActivities_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMActivities_CRMLeads_CompanyId_BranchId_LeadId] FOREIGN KEY ([CompanyId], [BranchId], [LeadId]) REFERENCES [CRMLeads] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMActivities_CRMOpportunities_CompanyId_BranchId_OpportunityId_LeadId] FOREIGN KEY ([CompanyId], [BranchId], [OpportunityId], [LeadId]) REFERENCES [CRMOpportunities] ([CompanyId], [BranchId], [Id], [LeadId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMActivities_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CRMQuotes] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [OpportunityId] bigint NOT NULL,
        [LeadId] bigint NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [BusinessPolicyId] bigint NOT NULL,
        [CurrencyMinorUnits] int NOT NULL,
        [BusinessPolicyVersion] int NOT NULL,
        [QuoteDate] date NOT NULL,
        [ValidUntil] date NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        [CustomerName] nvarchar(160) NOT NULL,
        [ContactName] nvarchar(200) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [Phone] nvarchar(40) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [Terms] nvarchar(2000) NOT NULL,
        [ReviewNote] nvarchar(1000) NOT NULL,
        [GrossAmount] decimal(18,3) NOT NULL,
        [DiscountAmount] decimal(18,3) NOT NULL,
        [NetAmount] decimal(18,3) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [ReviewedBy] nvarchar(200) NULL,
        [AcceptedBy] nvarchar(200) NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ReviewedUtc] datetimeoffset NULL,
        [AcceptedUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CRMQuotes] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CRMQuotes_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_CRMQuote_Accept] CHECK ([Status] <> 'Accepted' OR ([AcceptedBy] IS NOT NULL AND [AcceptedUtc] IS NOT NULL)),
        CONSTRAINT [CK_CRMQuote_Amounts] CHECK ([GrossAmount] > 0 AND [DiscountAmount] >= 0 AND [NetAmount] > 0 AND [DiscountAmount] < [GrossAmount]),
        CONSTRAINT [CK_CRMQuote_Review] CHECK ([Status] NOT IN ('Reviewed','Accepted') OR ([ReviewedBy] IS NOT NULL AND [ReviewedUtc] IS NOT NULL)),
        CONSTRAINT [CK_CRMQuote_Status] CHECK ([Status] IN ('Draft','Reviewed','Accepted','Cancelled')),
        CONSTRAINT [FK_CRMQuotes_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMQuotes_CRMLeads_CompanyId_BranchId_LeadId] FOREIGN KEY ([CompanyId], [BranchId], [LeadId]) REFERENCES [CRMLeads] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMQuotes_CRMOpportunities_CompanyId_BranchId_OpportunityId_LeadId] FOREIGN KEY ([CompanyId], [BranchId], [OpportunityId], [LeadId]) REFERENCES [CRMOpportunities] ([CompanyId], [BranchId], [Id], [LeadId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMQuotes_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMQuotes_CompanyBusinessPolicies_CompanyId_BusinessPolicyId] FOREIGN KEY ([CompanyId], [BusinessPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceOrderEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [EcommerceOrderId] bigint NOT NULL,
        [Action] nvarchar(200) NOT NULL,
        [Note] nvarchar(1000) NOT NULL,
        [ActorId] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EcommerceOrderEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EcommerceOrderEvents_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrderEvents_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrderEvents_EcommerceOrders_CompanyId_BranchId_EcommerceOrderId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceOrderId]) REFERENCES [EcommerceOrders] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [HRPayrollLines] (
        [Id] bigint NOT NULL IDENTITY,
        [CompensationId] bigint NOT NULL,
        [CompensationVersion] int NOT NULL,
        [CompensationEffectiveFrom] date NOT NULL,
        [PayBasis] nvarchar(200) NOT NULL,
        [ProrationMode] nvarchar(200) NOT NULL,
        [ProrationDenominator] decimal(18,3) NOT NULL,
        [PaidUnitsUsed] decimal(18,3) NOT NULL,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [RunId] bigint NOT NULL,
        [StaffId] int NOT NULL,
        [StaffCode] nvarchar(200) NOT NULL,
        [StaffName] nvarchar(200) NOT NULL,
        [DailyRate] decimal(18,3) NOT NULL,
        [MonthlySalary] decimal(18,3) NOT NULL,
        [OvertimeHourlyRate] decimal(18,3) NOT NULL,
        [AttendanceDays] decimal(18,3) NOT NULL,
        [PaidLeaveDays] decimal(18,3) NOT NULL,
        [OvertimeHours] decimal(18,3) NOT NULL,
        [BasePay] decimal(18,3) NOT NULL,
        [OvertimePay] decimal(18,3) NOT NULL,
        [GrossPay] decimal(18,3) NOT NULL,
        [Deductions] decimal(18,3) NOT NULL,
        [NetPay] decimal(18,3) NOT NULL,
        [EmployerContribution] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_HRPayrollLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HRPayrollLines_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollLines_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollLines_HRPayrollCompensations_CompanyId_BranchId_StaffId_CompensationId] FOREIGN KEY ([CompanyId], [BranchId], [StaffId], [CompensationId]) REFERENCES [HRPayrollCompensations] ([CompanyId], [BranchId], [StaffId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollLines_HRPayrollRuns_CompanyId_BranchId_RunId] FOREIGN KEY ([CompanyId], [BranchId], [RunId]) REFERENCES [HRPayrollRuns] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HRPayrollLines_HRPayrollStaff_CompanyId_BranchId_StaffId] FOREIGN KEY ([CompanyId], [BranchId], [StaffId]) REFERENCES [HRPayrollStaff] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceCartLines] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [EcommerceCartId] bigint NOT NULL,
        [EcommerceProductId] bigint NOT NULL,
        [CompanyPolicyId] bigint NOT NULL,
        [TaxPolicyId] bigint NOT NULL,
        [ProductRevisionToken] nvarchar(200) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [CurrencyCode] nvarchar(200) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [NetUnitPrice] decimal(18,3) NOT NULL,
        [NetAmount] decimal(18,3) NOT NULL,
        [TaxAmount] decimal(18,3) NOT NULL,
        [GrossAmount] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_EcommerceCartLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EcommerceCartLine_Amounts] CHECK ([NetAmount] > 0 AND [TaxAmount] >= 0 AND [GrossAmount] > 0),
        CONSTRAINT [CK_EcommerceCartLine_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_EcommerceCartLines_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCartLines_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCartLines_CompanyBusinessPolicies_CompanyId_CompanyPolicyId] FOREIGN KEY ([CompanyId], [CompanyPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCartLines_EcommerceCarts_CompanyId_BranchId_EcommerceCartId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceCartId]) REFERENCES [EcommerceCarts] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCartLines_EcommerceProducts_CompanyId_BranchId_EcommerceProductId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceProductId]) REFERENCES [EcommerceProducts] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceCartLines_TaxPolicies_CompanyId_TaxPolicyId] FOREIGN KEY ([CompanyId], [TaxPolicyId]) REFERENCES [TaxPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceOrderLines] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [EcommerceOrderId] bigint NOT NULL,
        [EcommerceProductId] bigint NOT NULL,
        [StockItemId] int NOT NULL,
        [TaxPolicyId] bigint NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [UnitOfMeasure] nvarchar(200) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [NetUnitPrice] decimal(18,3) NOT NULL,
        [NetAmount] decimal(18,3) NOT NULL,
        [TaxAmount] decimal(18,3) NOT NULL,
        [GrossAmount] decimal(18,3) NOT NULL,
        [TaxRatePercent] decimal(18,3) NOT NULL,
        [TaxTreatment] nvarchar(200) NOT NULL,
        [TaxLiabilityAccount] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_EcommerceOrderLines] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EcommerceOrderLines_CompanyId_BranchId_EcommerceOrderId_StockItemId_Id] UNIQUE ([CompanyId], [BranchId], [EcommerceOrderId], [StockItemId], [Id]),
        CONSTRAINT [AK_EcommerceOrderLines_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_EcommerceOrderLine_Amounts] CHECK ([NetAmount] > 0 AND [TaxAmount] >= 0 AND [GrossAmount] > 0),
        CONSTRAINT [CK_EcommerceOrderLine_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_EcommerceOrderLines_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrderLines_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrderLines_EcommerceOrders_CompanyId_BranchId_EcommerceOrderId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceOrderId]) REFERENCES [EcommerceOrders] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrderLines_EcommerceProducts_CompanyId_BranchId_EcommerceProductId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceProductId]) REFERENCES [EcommerceProducts] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrderLines_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceOrderLines_TaxPolicies_CompanyId_TaxPolicyId] FOREIGN KEY ([CompanyId], [TaxPolicyId]) REFERENCES [TaxPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CRMQuoteLines] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [QuoteId] bigint NOT NULL,
        [LineNumber] int NOT NULL,
        [StockItemId] int NOT NULL,
        [SKU] nvarchar(200) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [UnitOfMeasure] nvarchar(30) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,3) NOT NULL,
        [GrossAmount] decimal(18,3) NOT NULL,
        [DiscountAmount] decimal(18,3) NOT NULL,
        [NetAmount] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_CRMQuoteLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CRMQuoteLine_Amounts] CHECK ([LineNumber] > 0 AND [Quantity] > 0 AND [UnitPrice] > 0 AND [GrossAmount] > 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] < [GrossAmount] AND [NetAmount] > 0),
        CONSTRAINT [FK_CRMQuoteLines_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMQuoteLines_CRMQuotes_CompanyId_BranchId_QuoteId] FOREIGN KEY ([CompanyId], [BranchId], [QuoteId]) REFERENCES [CRMQuotes] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMQuoteLines_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMQuoteLines_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [CRMSalesReadyRecords] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [QuoteId] bigint NOT NULL,
        [OpportunityId] bigint NOT NULL,
        [LeadId] bigint NOT NULL,
        [CustomerId] int NOT NULL,
        [Reference] nvarchar(80) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [BusinessPolicyId] bigint NOT NULL,
        [CurrencyMinorUnits] int NOT NULL,
        [CustomerCode] nvarchar(200) NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [NetAmount] decimal(18,3) NOT NULL,
        [AcceptanceEvidence] nvarchar(1000) NOT NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CRMSalesReadyRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CRMSalesReadyRecords_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_CRMSalesReady_Net] CHECK ([NetAmount] > 0),
        CONSTRAINT [FK_CRMSalesReadyRecords_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMSalesReadyRecords_CRMLeads_CompanyId_BranchId_LeadId] FOREIGN KEY ([CompanyId], [BranchId], [LeadId]) REFERENCES [CRMLeads] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMSalesReadyRecords_CRMOpportunities_CompanyId_BranchId_OpportunityId_LeadId] FOREIGN KEY ([CompanyId], [BranchId], [OpportunityId], [LeadId]) REFERENCES [CRMOpportunities] ([CompanyId], [BranchId], [Id], [LeadId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMSalesReadyRecords_CRMQuotes_CompanyId_BranchId_QuoteId] FOREIGN KEY ([CompanyId], [BranchId], [QuoteId]) REFERENCES [CRMQuotes] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMSalesReadyRecords_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMSalesReadyRecords_CompanyBusinessPolicies_CompanyId_BusinessPolicyId] FOREIGN KEY ([CompanyId], [BusinessPolicyId]) REFERENCES [CompanyBusinessPolicies] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CRMSalesReadyRecords_TradingParties_CompanyId_BranchId_CustomerId] FOREIGN KEY ([CompanyId], [BranchId], [CustomerId]) REFERENCES [TradingParties] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceFulfillments] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [EcommerceOrderLineId] bigint NOT NULL,
        [ProductSaleId] bigint NOT NULL,
        [FinanceDocumentId] bigint NOT NULL,
        [CostAmount] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_EcommerceFulfillments] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_EcommerceFulfillments_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [FK_EcommerceFulfillments_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceFulfillments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceFulfillments_EcommerceOrderLines_CompanyId_BranchId_EcommerceOrderLineId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceOrderLineId]) REFERENCES [EcommerceOrderLines] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceFulfillments_FinanceDocuments_CompanyId_BranchId_FinanceDocumentId] FOREIGN KEY ([CompanyId], [BranchId], [FinanceDocumentId]) REFERENCES [FinanceDocuments] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceFulfillments_ProductSales_CompanyId_BranchId_ProductSaleId] FOREIGN KEY ([CompanyId], [BranchId], [ProductSaleId]) REFERENCES [ProductSales] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceReturns] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [EcommerceOrderLineId] bigint NOT NULL,
        [StockDocumentReturnId] bigint NOT NULL,
        [RequestToken] nvarchar(200) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [NetAmount] decimal(18,3) NOT NULL,
        [TaxAmount] decimal(18,3) NOT NULL,
        [CostAmount] decimal(18,3) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_EcommerceReturns] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EcommerceReturn_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_EcommerceReturns_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceReturns_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceReturns_EcommerceOrderLines_CompanyId_BranchId_EcommerceOrderLineId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceOrderLineId]) REFERENCES [EcommerceOrderLines] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceReturns_StockDocumentReturns_CompanyId_BranchId_StockDocumentReturnId] FOREIGN KEY ([CompanyId], [BranchId], [StockDocumentReturnId]) REFERENCES [StockDocumentReturns] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE TABLE [EcommerceStockReservations] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [StockItemId] int NOT NULL,
        [EcommerceOrderId] bigint NOT NULL,
        [EcommerceOrderLineId] bigint NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Status] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_EcommerceStockReservations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EcommerceReservation_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_EcommerceReservation_Status] CHECK ([Status] IN ('Reserved','Released','Consumed')),
        CONSTRAINT [FK_EcommerceStockReservations_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceStockReservations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceStockReservations_EcommerceOrderLines_CompanyId_BranchId_EcommerceOrderId_StockItemId_EcommerceOrderLineId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceOrderId], [StockItemId], [EcommerceOrderLineId]) REFERENCES [EcommerceOrderLines] ([CompanyId], [BranchId], [EcommerceOrderId], [StockItemId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceStockReservations_EcommerceOrders_CompanyId_BranchId_EcommerceOrderId] FOREIGN KEY ([CompanyId], [BranchId], [EcommerceOrderId]) REFERENCES [EcommerceOrders] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EcommerceStockReservations_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    EXEC(N'ALTER TABLE [StockItems] ADD CONSTRAINT [CK_StockItem_Reservations] CHECK ([ReservedQuantity] >= 0 AND [ReservedQuantity] <= [QuantityOnHand])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CompanyBusinessPolicies_CompanyId_EffectiveFrom_Status] ON [CompanyBusinessPolicies] ([CompanyId], [EffectiveFrom], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompanyBusinessPolicies_CompanyId_Version] ON [CompanyBusinessPolicies] ([CompanyId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompanyCurrencyLocks_CompanyId] ON [CompanyCurrencyLocks] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMActivities_CompanyId_BranchId_LeadId] ON [CRMActivities] ([CompanyId], [BranchId], [LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMActivities_CompanyId_BranchId_OpportunityId_LeadId] ON [CRMActivities] ([CompanyId], [BranchId], [OpportunityId], [LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CRMActivities_CompanyId_BranchId_Reference] ON [CRMActivities] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMActivities_CompanyId_BranchId_Status_DueDate] ON [CRMActivities] ([CompanyId], [BranchId], [Status], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMConsentEvents_CompanyId_BranchId_LeadId] ON [CRMConsentEvents] ([CompanyId], [BranchId], [LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMLeads_CompanyId_BranchId_CustomerId] ON [CRMLeads] ([CompanyId], [BranchId], [CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CRMLeads_CompanyId_BranchId_Reference] ON [CRMLeads] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMOpportunities_CompanyId_BranchId_LeadId] ON [CRMOpportunities] ([CompanyId], [BranchId], [LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CRMOpportunities_CompanyId_BranchId_Reference] ON [CRMOpportunities] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CRMQuoteLines_CompanyId_BranchId_QuoteId_LineNumber] ON [CRMQuoteLines] ([CompanyId], [BranchId], [QuoteId], [LineNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMQuoteLines_CompanyId_BranchId_StockItemId] ON [CRMQuoteLines] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMQuotes_CompanyId_BranchId_LeadId] ON [CRMQuotes] ([CompanyId], [BranchId], [LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMQuotes_CompanyId_BranchId_OpportunityId_LeadId] ON [CRMQuotes] ([CompanyId], [BranchId], [OpportunityId], [LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CRMQuotes_CompanyId_BranchId_Reference] ON [CRMQuotes] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMQuotes_CompanyId_BusinessPolicyId] ON [CRMQuotes] ([CompanyId], [BusinessPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMSalesReadyRecords_CompanyId_BranchId_CustomerId] ON [CRMSalesReadyRecords] ([CompanyId], [BranchId], [CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMSalesReadyRecords_CompanyId_BranchId_LeadId] ON [CRMSalesReadyRecords] ([CompanyId], [BranchId], [LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMSalesReadyRecords_CompanyId_BranchId_OpportunityId_LeadId] ON [CRMSalesReadyRecords] ([CompanyId], [BranchId], [OpportunityId], [LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMSalesReadyRecords_CompanyId_BranchId_QuoteId] ON [CRMSalesReadyRecords] ([CompanyId], [BranchId], [QuoteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CRMSalesReadyRecords_CompanyId_BranchId_Reference] ON [CRMSalesReadyRecords] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_CRMSalesReadyRecords_CompanyId_BusinessPolicyId] ON [CRMSalesReadyRecords] ([CompanyId], [BusinessPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CRMSalesReadyRecords_OpportunityId] ON [CRMSalesReadyRecords] ([OpportunityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CRMSalesReadyRecords_QuoteId] ON [CRMSalesReadyRecords] ([QuoteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceCartLines_CompanyId_BranchId_EcommerceCartId] ON [EcommerceCartLines] ([CompanyId], [BranchId], [EcommerceCartId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceCartLines_CompanyId_BranchId_EcommerceProductId] ON [EcommerceCartLines] ([CompanyId], [BranchId], [EcommerceProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceCartLines_CompanyId_CompanyPolicyId] ON [EcommerceCartLines] ([CompanyId], [CompanyPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceCartLines_CompanyId_TaxPolicyId] ON [EcommerceCartLines] ([CompanyId], [TaxPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceCartLines_EcommerceCartId_EcommerceProductId] ON [EcommerceCartLines] ([EcommerceCartId], [EcommerceProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceCarts_CompanyId_BranchId_EcommerceCustomerId] ON [EcommerceCarts] ([CompanyId], [BranchId], [EcommerceCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceCustomers_CompanyId_BranchId_TradingPartyId] ON [EcommerceCustomers] ([CompanyId], [BranchId], [TradingPartyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceCustomers_CompanyId_BranchId_UserId] ON [EcommerceCustomers] ([CompanyId], [BranchId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceCustomers_UserId] ON [EcommerceCustomers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceFulfillments_CompanyId_BranchId_EcommerceOrderLineId] ON [EcommerceFulfillments] ([CompanyId], [BranchId], [EcommerceOrderLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceFulfillments_CompanyId_BranchId_FinanceDocumentId] ON [EcommerceFulfillments] ([CompanyId], [BranchId], [FinanceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceFulfillments_CompanyId_BranchId_ProductSaleId] ON [EcommerceFulfillments] ([CompanyId], [BranchId], [ProductSaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceFulfillments_EcommerceOrderLineId] ON [EcommerceFulfillments] ([EcommerceOrderLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceFulfillments_FinanceDocumentId] ON [EcommerceFulfillments] ([FinanceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceFulfillments_ProductSaleId] ON [EcommerceFulfillments] ([ProductSaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceOrderEvents_CompanyId_BranchId_EcommerceOrderId] ON [EcommerceOrderEvents] ([CompanyId], [BranchId], [EcommerceOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceOrderLines_CompanyId_BranchId_EcommerceProductId] ON [EcommerceOrderLines] ([CompanyId], [BranchId], [EcommerceProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceOrderLines_CompanyId_BranchId_StockItemId] ON [EcommerceOrderLines] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceOrderLines_CompanyId_TaxPolicyId] ON [EcommerceOrderLines] ([CompanyId], [TaxPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceOrders_CompanyId_BranchId_CheckoutToken] ON [EcommerceOrders] ([CompanyId], [BranchId], [CheckoutToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceOrders_CompanyId_BranchId_EcommerceCustomerId_Id] ON [EcommerceOrders] ([CompanyId], [BranchId], [EcommerceCustomerId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceOrders_CompanyId_BranchId_Reference] ON [EcommerceOrders] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceOrders_CompanyId_CompanyPolicyId] ON [EcommerceOrders] ([CompanyId], [CompanyPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceProducts_CompanyId_BranchId_StockItemId] ON [EcommerceProducts] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceProducts_CompanyId_CompanyPolicyId] ON [EcommerceProducts] ([CompanyId], [CompanyPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceProducts_CompanyId_TaxPolicyId] ON [EcommerceProducts] ([CompanyId], [TaxPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceReturns_CompanyId_BranchId_EcommerceOrderLineId] ON [EcommerceReturns] ([CompanyId], [BranchId], [EcommerceOrderLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceReturns_CompanyId_BranchId_RequestToken] ON [EcommerceReturns] ([CompanyId], [BranchId], [RequestToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceReturns_CompanyId_BranchId_StockDocumentReturnId] ON [EcommerceReturns] ([CompanyId], [BranchId], [StockDocumentReturnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceReturns_StockDocumentReturnId] ON [EcommerceReturns] ([StockDocumentReturnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceStockReservations_CompanyId_BranchId_EcommerceOrderId_StockItemId_EcommerceOrderLineId] ON [EcommerceStockReservations] ([CompanyId], [BranchId], [EcommerceOrderId], [StockItemId], [EcommerceOrderLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_EcommerceStockReservations_CompanyId_BranchId_StockItemId] ON [EcommerceStockReservations] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EcommerceStockReservations_EcommerceOrderLineId] ON [EcommerceStockReservations] ([EcommerceOrderLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_FixedAssetCustodies_CompanyId_BranchId_FixedAssetId_Id] ON [FixedAssetCustodies] ([CompanyId], [BranchId], [FixedAssetId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_FixedAssetDepreciationLines_CompanyId_BranchId_FixedAssetDepreciationRunId] ON [FixedAssetDepreciationLines] ([CompanyId], [BranchId], [FixedAssetDepreciationRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FixedAssetDepreciationLines_CompanyId_BranchId_FixedAssetId_Period] ON [FixedAssetDepreciationLines] ([CompanyId], [BranchId], [FixedAssetId], [Period]) WHERE [Posted] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FixedAssetDepreciationLines_FixedAssetDepreciationRunId_FixedAssetId] ON [FixedAssetDepreciationLines] ([FixedAssetDepreciationRunId], [FixedAssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FixedAssetDepreciationRuns_CompanyId_BranchId_Period] ON [FixedAssetDepreciationRuns] ([CompanyId], [BranchId], [Period]) WHERE [Status] <> ''Cancelled'' AND [Status] <> ''Posted''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FixedAssetDepreciationRuns_CompanyId_BranchId_Reference] ON [FixedAssetDepreciationRuns] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_FixedAssetDepreciationRuns_CompanyId_BranchId_VoucherId] ON [FixedAssetDepreciationRuns] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_FixedAssetDepreciationRuns_CompanyId_BusinessPolicyId] ON [FixedAssetDepreciationRuns] ([CompanyId], [BusinessPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FixedAssetDepreciationRuns_VoucherId] ON [FixedAssetDepreciationRuns] ([VoucherId]) WHERE [VoucherId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FixedAssetDisposals_CompanyId_BranchId_FixedAssetId] ON [FixedAssetDisposals] ([CompanyId], [BranchId], [FixedAssetId]) WHERE [Status] <> ''Cancelled''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FixedAssetDisposals_CompanyId_BranchId_Reference] ON [FixedAssetDisposals] ([CompanyId], [BranchId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_FixedAssetDisposals_CompanyId_BranchId_VoucherId] ON [FixedAssetDisposals] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_FixedAssetDisposals_CompanyId_BusinessPolicyId] ON [FixedAssetDisposals] ([CompanyId], [BusinessPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FixedAssetDisposals_VoucherId] ON [FixedAssetDisposals] ([VoucherId]) WHERE [VoucherId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FixedAssets_AcquisitionVoucherId] ON [FixedAssets] ([AcquisitionVoucherId]) WHERE [AcquisitionVoucherId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_FixedAssets_CompanyId_BranchId_AcquisitionVoucherId] ON [FixedAssets] ([CompanyId], [BranchId], [AcquisitionVoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FixedAssets_CompanyId_BranchId_AssetCode] ON [FixedAssets] ([CompanyId], [BranchId], [AssetCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FixedAssets_CompanyId_BranchId_SourceReference] ON [FixedAssets] ([CompanyId], [BranchId], [SourceReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_FixedAssets_CompanyId_BusinessPolicyId] ON [FixedAssets] ([CompanyId], [BusinessPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HRPayrollAttendance_CompanyId_BranchId_StaffId_Date] ON [HRPayrollAttendance] ([CompanyId], [BranchId], [StaffId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_HRPayrollCompensations_CompanyId_BranchId_StaffId_EffectiveFrom] ON [HRPayrollCompensations] ([CompanyId], [BranchId], [StaffId], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HRPayrollCompensations_CompanyId_BranchId_StaffId_Version] ON [HRPayrollCompensations] ([CompanyId], [BranchId], [StaffId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_HRPayrollLeaves_CompanyId_BranchId_StaffId_From_Through] ON [HRPayrollLeaves] ([CompanyId], [BranchId], [StaffId], [From], [Through]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HRPayrollLines_CompanyId_BranchId_RunId_StaffId] ON [HRPayrollLines] ([CompanyId], [BranchId], [RunId], [StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_HRPayrollLines_CompanyId_BranchId_StaffId_CompensationId] ON [HRPayrollLines] ([CompanyId], [BranchId], [StaffId], [CompensationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_HRPayrollPolicies_CompanyId_CompanyBusinessPolicyId] ON [HRPayrollPolicies] ([CompanyId], [CompanyBusinessPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HRPayrollRuns_CompanyId_BranchId_PeriodStart] ON [HRPayrollRuns] ([CompanyId], [BranchId], [PeriodStart]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_HRPayrollRuns_CompanyId_BranchId_PolicyId] ON [HRPayrollRuns] ([CompanyId], [BranchId], [PolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_HRPayrollRuns_CompanyId_BranchId_VoucherId] ON [HRPayrollRuns] ([CompanyId], [BranchId], [VoucherId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HRPayrollRuns_CompanyId_Reference] ON [HRPayrollRuns] ([CompanyId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HRPayrollStaff_CompanyId_Code] ON [HRPayrollStaff] ([CompanyId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_HRPayrollStaff_CompanyId_IdentityUserId] ON [HRPayrollStaff] ([CompanyId], [IdentityUserId]) WHERE [IdentityUserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_HRPayrollStaff_IdentityUserId] ON [HRPayrollStaff] ([IdentityUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaxPolicies_CompanyId_Code_Version] ON [TaxPolicies] ([CompanyId], [Code], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    CREATE INDEX [IX_TaxPolicies_CompanyId_CompanyBusinessPolicyId] ON [TaxPolicies] ([CompanyId], [CompanyBusinessPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019050306_CompleteEnterpriseModules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019050306_CompleteEnterpriseModules', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    DROP INDEX [IX_UserPermissions_CompanyId] ON [UserPermissions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    DROP INDEX [IX_UserCompanyAccess_CompanyId] ON [UserCompanyAccess];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    DROP INDEX [IX_UserBranchAccess_CompanyId_BranchId] ON [UserBranchAccess];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    DROP INDEX [IX_SupportTickets_CompanyId_BranchId] ON [SupportTickets];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    DROP INDEX [IX_Audits_CompanyId] ON [Audits];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    ALTER TABLE [Notifications] ADD CONSTRAINT [AK_Notifications_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE TABLE [InternalMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [SenderUserId] nvarchar(128) NOT NULL,
        [RecipientUserId] nvarchar(128) NOT NULL,
        [Subject] nvarchar(160) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [IdempotencyKey] nvarchar(32) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InternalMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_InternalMessages_CompanyId_BranchId_Id_RecipientUserId] UNIQUE ([CompanyId], [BranchId], [Id], [RecipientUserId]),
        CONSTRAINT [CK_InternalMessage_Content] CHECK (LEN([Subject]) BETWEEN 1 AND 160 AND LEN([Body]) BETWEEN 1 AND 8000 AND LEN([IdempotencyKey]) = 32 AND LEN([PayloadHash]) = 64),
        CONSTRAINT [FK_InternalMessages_AspNetUsers_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InternalMessages_AspNetUsers_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InternalMessages_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InternalMessages_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE TABLE [MessagingAutomationReads] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [UserId] nvarchar(128) NOT NULL,
        [NotificationId] bigint NOT NULL,
        [ReadUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_MessagingAutomationReads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessagingAutomationReads_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessagingAutomationReads_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessagingAutomationReads_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessagingAutomationReads_Notifications_CompanyId_BranchId_NotificationId] FOREIGN KEY ([CompanyId], [BranchId], [NotificationId]) REFERENCES [Notifications] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE TABLE [MessagingRateBuckets] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [UserId] nvarchar(128) NOT NULL,
        [Period] nvarchar(6) NOT NULL,
        [WindowUtcTicks] bigint NOT NULL,
        [Count] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MessagingRateBuckets] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MessagingRateBucket_Limit] CHECK (([Period] = 'Minute' AND [Count] BETWEEN 1 AND 20) OR ([Period] = 'Day' AND [Count] BETWEEN 1 AND 200)),
        CONSTRAINT [FK_MessagingRateBuckets_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessagingRateBuckets_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessagingRateBuckets_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE TABLE [ScaleCacheEpochs] (
        [CompanyId] int NOT NULL,
        [ScopeBranchId] int NOT NULL,
        [Token] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_ScaleCacheEpochs] PRIMARY KEY ([CompanyId], [ScopeBranchId]),
        CONSTRAINT [FK_ScaleCacheEpochs_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE TABLE [UserInboxNotifications] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [UserId] nvarchar(128) NOT NULL,
        [MessageId] bigint NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ReadUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UserInboxNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_UserInboxNotifications_CompanyId_BranchId_Id_UserId] UNIQUE ([CompanyId], [BranchId], [Id], [UserId]),
        CONSTRAINT [FK_UserInboxNotifications_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserInboxNotifications_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserInboxNotifications_InternalMessages_CompanyId_BranchId_MessageId_UserId] FOREIGN KEY ([CompanyId], [BranchId], [MessageId], [UserId]) REFERENCES [InternalMessages] ([CompanyId], [BranchId], [Id], [RecipientUserId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE TABLE [MessagingOutbox] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [UserId] nvarchar(128) NOT NULL,
        [NotificationId] bigint NOT NULL,
        [CreatedUtcTicks] bigint NOT NULL,
        [HintProcessedUtcTicks] bigint NULL,
        [LeaseUntilUtcTicks] bigint NOT NULL,
        [LeaseOwner] nvarchar(32) NULL,
        [Attempts] int NOT NULL,
        CONSTRAINT [PK_MessagingOutbox] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessagingOutbox_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessagingOutbox_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MessagingOutbox_UserInboxNotifications_CompanyId_BranchId_NotificationId_UserId] FOREIGN KEY ([CompanyId], [BranchId], [NotificationId], [UserId]) REFERENCES [UserInboxNotifications] ([CompanyId], [BranchId], [Id], [UserId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_UserPermissions_CompanyId_Id] ON [UserPermissions] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_UserCompanyAccess_CompanyId_Id] ON [UserCompanyAccess] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_UserBranchAccess_CompanyId_BranchId_Id] ON [UserBranchAccess] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_SupportTickets_CompanyId_BranchId_Id] ON [SupportTickets] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_SupportTicketActivities_CompanyId_Id] ON [SupportTicketActivities] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_SubscriptionPayments_CompanyId_Id] ON [SubscriptionPayments] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_StockTransfers_CompanyId_Id] ON [StockTransfers] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_StockReorderPolicies_CompanyId_BranchId_Id] ON [StockReorderPolicies] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_ScheduledJobs_CompanyId_Id] ON [ScheduledJobs] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderReceipts_CompanyId_BranchId_Id] ON [PurchaseOrderReceipts] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_OpeningStockValues_CompanyId_BranchId_Id] ON [OpeningStockValues] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_OpeningAnimalValues_CompanyId_BranchId_Id] ON [OpeningAnimalValues] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MilkYields_CompanyId_BranchId_Id] ON [MilkYields] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_InventoryCostBalances_CompanyId_BranchId_Id] ON [InventoryCostBalances] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_HRPayrollLines_CompanyId_BranchId_Id] ON [HRPayrollLines] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_HRPayrollLeaves_CompanyId_BranchId_Id] ON [HRPayrollLeaves] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_HRPayrollCompensations_CompanyId_BranchId_Id] ON [HRPayrollCompensations] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_HRPayrollAttendance_CompanyId_BranchId_Id] ON [HRPayrollAttendance] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_FixedAssetDisposals_CompanyId_BranchId_Id] ON [FixedAssetDisposals] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_FixedAssetDepreciationLines_CompanyId_BranchId_Id] ON [FixedAssetDepreciationLines] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_FixedAssetCustodies_CompanyId_BranchId_Id] ON [FixedAssetCustodies] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_FinancePaymentAllocations_CompanyId_BranchId_Id] ON [FinancePaymentAllocations] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_FatteningPlans_CompanyId_BranchId_Id] ON [FatteningPlans] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_FarmExpenses_CompanyId_BranchId_Id] ON [FarmExpenses] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_EcommerceStockReservations_CompanyId_BranchId_Id] ON [EcommerceStockReservations] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_EcommerceReturns_CompanyId_BranchId_Id] ON [EcommerceReturns] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_EcommerceOrderEvents_CompanyId_BranchId_Id] ON [EcommerceOrderEvents] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_EcommerceCartLines_CompanyId_BranchId_Id] ON [EcommerceCartLines] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_CRMQuoteLines_CompanyId_BranchId_Id] ON [CRMQuoteLines] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_CRMConsentEvents_CompanyId_BranchId_Id] ON [CRMConsentEvents] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_CompanySubscriptionPolicies_CompanyId_Id] ON [CompanySubscriptionPolicies] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_CompanyCurrencyLocks_CompanyId_Id] ON [CompanyCurrencyLocks] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_BreedingCycleEvents_CompanyId_BranchId_Id] ON [BreedingCycleEvents] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_BankStatementLines_CompanyId_BranchId_Id] ON [BankStatementLines] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_AutomationExecutions_CompanyId_Id] ON [AutomationExecutions] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_AutomationApprovals_CompanyId_Id] ON [AutomationApprovals] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_Audits_CompanyId_Id] ON [Audits] ([CompanyId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_AnimalWeightRecords_CompanyId_BranchId_Id] ON [AnimalWeightRecords] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_AnimalSales_CompanyId_BranchId_Id] ON [AnimalSales] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_AnimalHealthRecords_CompanyId_BranchId_Id] ON [AnimalHealthRecords] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_AnimalFeedRecords_CompanyId_BranchId_Id] ON [AnimalFeedRecords] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_AccountingPeriodCloses_CompanyId_BranchId_Id] ON [AccountingPeriodCloses] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_InternalMessages_CompanyId_BranchId_Id] ON [InternalMessages] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_InternalMessages_CompanyId_BranchId_SenderUserId_Id] ON [InternalMessages] ([CompanyId], [BranchId], [SenderUserId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InternalMessages_CompanyId_BranchId_SenderUserId_IdempotencyKey] ON [InternalMessages] ([CompanyId], [BranchId], [SenderUserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_InternalMessages_RecipientUserId] ON [InternalMessages] ([RecipientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_InternalMessages_SenderUserId] ON [InternalMessages] ([SenderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MessagingAutomationReads_CompanyId_BranchId_Id] ON [MessagingAutomationReads] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MessagingAutomationReads_CompanyId_BranchId_NotificationId] ON [MessagingAutomationReads] ([CompanyId], [BranchId], [NotificationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MessagingAutomationReads_CompanyId_BranchId_UserId_NotificationId] ON [MessagingAutomationReads] ([CompanyId], [BranchId], [UserId], [NotificationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MessagingAutomationReads_UserId] ON [MessagingAutomationReads] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MessagingOutbox_CompanyId_BranchId_Id] ON [MessagingOutbox] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MessagingOutbox_CompanyId_BranchId_NotificationId_UserId] ON [MessagingOutbox] ([CompanyId], [BranchId], [NotificationId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MessagingOutbox_HintProcessedUtcTicks_LeaseUntilUtcTicks_Id] ON [MessagingOutbox] ([HintProcessedUtcTicks], [LeaseUntilUtcTicks], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MessagingOutbox_NotificationId] ON [MessagingOutbox] ([NotificationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MessagingRateBuckets_CompanyId_BranchId_Id] ON [MessagingRateBuckets] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MessagingRateBuckets_CompanyId_BranchId_UserId_Period_WindowUtcTicks] ON [MessagingRateBuckets] ([CompanyId], [BranchId], [UserId], [Period], [WindowUtcTicks]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_MessagingRateBuckets_UserId] ON [MessagingRateBuckets] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_UserInboxNotifications_CompanyId_BranchId_Id] ON [UserInboxNotifications] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_UserInboxNotifications_CompanyId_BranchId_MessageId_UserId] ON [UserInboxNotifications] ([CompanyId], [BranchId], [MessageId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserInboxNotifications_CompanyId_BranchId_UserId_MessageId] ON [UserInboxNotifications] ([CompanyId], [BranchId], [UserId], [MessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    CREATE INDEX [IX_UserInboxNotifications_CompanyId_BranchId_UserId_ReadUtc_Id] ON [UserInboxNotifications] ([CompanyId], [BranchId], [UserId], [ReadUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020061732_ScopedRealtimeAndServerTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020061732_ScopedRealtimeAndServerTables', N'10.0.13');
END;

COMMIT;
GO

