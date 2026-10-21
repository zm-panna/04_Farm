BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE TABLE [ScanLabels] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Format] nvarchar(12) NOT NULL,
        [Value] nvarchar(80) NOT NULL,
        [ValueHash] nvarchar(64) NOT NULL,
        [StockItemId] int NULL,
        [AnimalId] int NULL,
        [FixedAssetId] bigint NULL,
        [PurchaseOrderId] bigint NULL,
        [FinanceDocumentId] bigint NULL,
        [CreatedBy] nvarchar(128) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [RevokedUtc] datetimeoffset NULL,
        [RevokedBy] nvarchar(200) NULL,
        [RevocationReason] nvarchar(300) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ScanLabels] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_ScanLabels_CompanyId_BranchId_Id] UNIQUE ([CompanyId], [BranchId], [Id]),
        CONSTRAINT [CK_ScanLabel_Target] CHECK (([Kind]='Product' AND [StockItemId] IS NOT NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR ([Kind]='Animal' AND [StockItemId] IS NULL AND [AnimalId] IS NOT NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR ([Kind]='Asset' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NOT NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR ([Kind]='PurchaseOrder' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NOT NULL AND [FinanceDocumentId] IS NULL) OR ([Kind]='Document' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NOT NULL)),
        CONSTRAINT [FK_ScanLabels_Animals_CompanyId_BranchId_AnimalId] FOREIGN KEY ([CompanyId], [BranchId], [AnimalId]) REFERENCES [Animals] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanLabels_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanLabels_AspNetUsers_RevokedBy] FOREIGN KEY ([RevokedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanLabels_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanLabels_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanLabels_FinanceDocuments_CompanyId_BranchId_FinanceDocumentId] FOREIGN KEY ([CompanyId], [BranchId], [FinanceDocumentId]) REFERENCES [FinanceDocuments] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanLabels_FixedAssets_CompanyId_BranchId_FixedAssetId] FOREIGN KEY ([CompanyId], [BranchId], [FixedAssetId]) REFERENCES [FixedAssets] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanLabels_PurchaseOrders_CompanyId_BranchId_PurchaseOrderId] FOREIGN KEY ([CompanyId], [BranchId], [PurchaseOrderId]) REFERENCES [PurchaseOrders] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanLabels_StockItems_CompanyId_BranchId_StockItemId] FOREIGN KEY ([CompanyId], [BranchId], [StockItemId]) REFERENCES [StockItems] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE TABLE [ScanIntents] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [BranchId] int NOT NULL,
        [OwnerUserId] nvarchar(128) NOT NULL,
        [RequestKey] nvarchar(32) NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [ScanLabelId] bigint NOT NULL,
        [SecondaryBranchId] int NULL,
        [SecondaryLabelId] bigint NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [SourceHash] nvarchar(64) NOT NULL,
        [ReviewJson] nvarchar(max) NOT NULL,
        [BusinessReference] nvarchar(40) NOT NULL,
        [Status] nvarchar(12) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [ExpiresUtc] datetimeoffset NOT NULL,
        [CompletedUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ScanIntents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ScanIntent_Secondary] CHECK (([SecondaryBranchId] IS NULL AND [SecondaryLabelId] IS NULL) OR ([SecondaryBranchId] IS NOT NULL AND [SecondaryLabelId] IS NOT NULL)),
        CONSTRAINT [CK_ScanIntent_State] CHECK (([Status]='Pending' AND [CompletedUtc] IS NULL) OR ([Status] IN ('Committed','Cancelled') AND [CompletedUtc] IS NOT NULL)),
        CONSTRAINT [FK_ScanIntents_AspNetUsers_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanIntents_Branches_CompanyId_BranchId] FOREIGN KEY ([CompanyId], [BranchId]) REFERENCES [Branches] ([CompanyId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanIntents_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanIntents_ScanLabels_CompanyId_BranchId_ScanLabelId] FOREIGN KEY ([CompanyId], [BranchId], [ScanLabelId]) REFERENCES [ScanLabels] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScanIntents_ScanLabels_CompanyId_SecondaryBranchId_SecondaryLabelId] FOREIGN KEY ([CompanyId], [SecondaryBranchId], [SecondaryLabelId]) REFERENCES [ScanLabels] ([CompanyId], [BranchId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanIntents_CompanyId_BranchId_Id] ON [ScanIntents] ([CompanyId], [BranchId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanIntents_CompanyId_BranchId_OwnerUserId_CreatedUtc] ON [ScanIntents] ([CompanyId], [BranchId], [OwnerUserId], [CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanIntents_CompanyId_BranchId_OwnerUserId_Id] ON [ScanIntents] ([CompanyId], [BranchId], [OwnerUserId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ScanIntents_CompanyId_BranchId_OwnerUserId_RequestKey] ON [ScanIntents] ([CompanyId], [BranchId], [OwnerUserId], [RequestKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanIntents_CompanyId_BranchId_OwnerUserId_Status_ExpiresUtc] ON [ScanIntents] ([CompanyId], [BranchId], [OwnerUserId], [Status], [ExpiresUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanIntents_CompanyId_BranchId_ScanLabelId] ON [ScanIntents] ([CompanyId], [BranchId], [ScanLabelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ScanIntents_CompanyId_BusinessReference] ON [ScanIntents] ([CompanyId], [BusinessReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanIntents_CompanyId_SecondaryBranchId_SecondaryLabelId] ON [ScanIntents] ([CompanyId], [SecondaryBranchId], [SecondaryLabelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanIntents_OwnerUserId] ON [ScanIntents] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanLabels_CompanyId_BranchId_AnimalId] ON [ScanLabels] ([CompanyId], [BranchId], [AnimalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanLabels_CompanyId_BranchId_FinanceDocumentId] ON [ScanLabels] ([CompanyId], [BranchId], [FinanceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanLabels_CompanyId_BranchId_FixedAssetId] ON [ScanLabels] ([CompanyId], [BranchId], [FixedAssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanLabels_CompanyId_BranchId_Kind_Id] ON [ScanLabels] ([CompanyId], [BranchId], [Kind], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanLabels_CompanyId_BranchId_PurchaseOrderId] ON [ScanLabels] ([CompanyId], [BranchId], [PurchaseOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanLabels_CompanyId_BranchId_StockItemId] ON [ScanLabels] ([CompanyId], [BranchId], [StockItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ScanLabels_CompanyId_BranchId_ValueHash] ON [ScanLabels] ([CompanyId], [BranchId], [ValueHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanLabels_CreatedBy] ON [ScanLabels] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    CREATE INDEX [IX_ScanLabels_RevokedBy] ON [ScanLabels] ([RevokedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094521_ReviewedScanningWorkflows'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020094521_ReviewedScanningWorkflows', N'10.0.13');
END;

COMMIT;
GO

