-- Batch Release IQ – database schema (SQL Server)
-- Run this inside the empty database you created, then start the API with Database:SeedSampleData
-- true (demo data) or false (empty system). Generated from the EF Core model; do not edit by hand.
CREATE TABLE [AuditEntries] (
    [Id] bigint NOT NULL IDENTITY,
    [Timestamp] datetimeoffset NOT NULL,
    [UserId] int NULL,
    [Username] nvarchar(max) NOT NULL,
    [Action] nvarchar(max) NOT NULL,
    [EntityType] nvarchar(450) NOT NULL,
    [EntityId] nvarchar(450) NOT NULL,
    [BatchId] int NULL,
    [FieldName] nvarchar(max) NULL,
    [OldValue] nvarchar(max) NULL,
    [NewValue] nvarchar(max) NULL,
    [Reason] nvarchar(max) NULL,
    CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Customers] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Country] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Departments] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ExternalParties] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [PartyType] nvarchar(40) NOT NULL,
    [City] nvarchar(max) NOT NULL,
    [Country] nvarchar(max) NOT NULL,
    [IsApproved] bit NOT NULL,
    [QualityAgreementValidUntil] date NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_ExternalParties] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Markets] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [RegulatoryAuthority] nvarchar(max) NOT NULL,
    [MinShelfLifePctAtDispatch] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Markets] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [PrioritySettings] (
    [Id] int NOT NULL,
    [ReadinessBandSize] int NOT NULL,
    [BlockedItemPenalty] decimal(8,2) NOT NULL,
    [OpenEventPenalty] decimal(8,2) NOT NULL,
    [CriticalEventPenalty] decimal(8,2) NOT NULL,
    [MaxPenalty] decimal(8,2) NOT NULL,
    [MinStageWeight] decimal(8,2) NOT NULL,
    [MaxStageWeight] decimal(8,2) NOT NULL,
    [StuckDays] int NOT NULL,
    [ExpiryWarningDays] int NOT NULL,
    [AtRiskBufferDays] int NOT NULL,
    [DefaultItemDurationDays] decimal(8,2) NOT NULL,
    [EnforceSegregationOfDuties] bit NOT NULL,
    CONSTRAINT [PK_PrioritySettings] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Products] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Strength] nvarchar(max) NOT NULL,
    [StrengthUnit] nvarchar(max) NOT NULL,
    [DosageForm] nvarchar(max) NOT NULL,
    [ProductType] nvarchar(max) NOT NULL,
    [PackSize] nvarchar(max) NOT NULL,
    [ShelfLifeMonths] int NOT NULL,
    [BatchPrefix] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Roles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(450) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [IsSystem] bit NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Signatures] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [Username] nvarchar(max) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Meaning] nvarchar(max) NOT NULL,
    [SignedAt] datetimeoffset NOT NULL,
    [EntityType] nvarchar(max) NOT NULL,
    [EntityId] nvarchar(max) NOT NULL,
    [BatchId] int NULL,
    [RecordHash] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Signatures] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [DepartmentId] int NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [FailedLoginCount] int NOT NULL,
    [LockedUntil] datetimeoffset NULL,
    [LastLoginAt] datetimeoffset NULL,
    [PasswordChangedAt] datetimeoffset NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id])
);
GO


CREATE TABLE [Sites] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [City] nvarchar(max) NOT NULL,
    [Country] nvarchar(max) NOT NULL,
    [SiteType] nvarchar(40) NOT NULL,
    [ExternalPartyId] int NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Sites] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sites_ExternalParties_ExternalPartyId] FOREIGN KEY ([ExternalPartyId]) REFERENCES [ExternalParties] ([Id])
);
GO


CREATE TABLE [Templates] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Version] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Priority] int NOT NULL,
    [ProductId] int NULL,
    [DosageForm] nvarchar(max) NULL,
    [MarketId] int NULL,
    [CustomerId] int NULL,
    [BatchType] nvarchar(40) NULL,
    [SiteType] nvarchar(40) NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Templates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Templates_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
    CONSTRAINT [FK_Templates_Markets_MarketId] FOREIGN KEY ([MarketId]) REFERENCES [Markets] ([Id]),
    CONSTRAINT [FK_Templates_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
);
GO


CREATE TABLE [RolePermissions] (
    [RoleId] int NOT NULL,
    [PermissionCode] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionCode]),
    CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [UserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Batches] (
    [Id] int NOT NULL IDENTITY,
    [BatchNo] nvarchar(450) NOT NULL,
    [ProductId] int NOT NULL,
    [SiteId] int NOT NULL,
    [CustomerId] int NULL,
    [BatchType] nvarchar(40) NOT NULL,
    [ParentBatchId] int NULL,
    [LotType] nvarchar(max) NOT NULL,
    [BatchSize] decimal(18,3) NOT NULL,
    [BatchSizeUnit] nvarchar(max) NOT NULL,
    [ManufacturingBlock] nvarchar(max) NOT NULL,
    [ProcessOrderDate] date NULL,
    [ManufacturingDate] date NOT NULL,
    [BatchCompletionDate] date NULL,
    [TargetReleaseDate] date NOT NULL,
    [ShelfLifeMonths] int NOT NULL,
    [ExpiryDate] date NOT NULL,
    [ExpiryOverrideReason] nvarchar(max) NULL,
    [Disposition] nvarchar(40) NOT NULL,
    [DispositionReason] nvarchar(max) NULL,
    [TemplateId] int NULL,
    [TemplateVersion] int NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedById] int NOT NULL,
    [ReadinessPct] decimal(5,1) NOT NULL,
    [IsReadyForRelease] bit NOT NULL,
    [CurrentStage] nvarchar(max) NOT NULL,
    [LastProgressAt] datetimeoffset NULL,
    [DispatchByDate] date NOT NULL,
    [Deadline] date NOT NULL,
    [DeadlineType] nvarchar(40) NOT NULL,
    [ReleasedAt] datetimeoffset NULL,
    CONSTRAINT [PK_Batches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Batches_Batches_ParentBatchId] FOREIGN KEY ([ParentBatchId]) REFERENCES [Batches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Batches_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
    CONSTRAINT [FK_Batches_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Batches_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [Sites] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Batches_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([Id]),
    CONSTRAINT [FK_Batches_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [TemplateItems] (
    [Id] int NOT NULL IDENTITY,
    [TemplateId] int NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Stage] nvarchar(max) NOT NULL,
    [DepartmentId] int NOT NULL,
    [ItemType] nvarchar(40) NOT NULL,
    [IsMandatory] bit NOT NULL,
    [Weight] decimal(6,2) NULL,
    [Sequence] int NOT NULL,
    [DueOffsetDays] int NOT NULL,
    [IsFinalRelease] bit NOT NULL,
    [IsPerMarket] bit NOT NULL,
    [DefaultSource] nvarchar(40) NOT NULL,
    [DependsOn] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_TemplateItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TemplateItems_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TemplateItems_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [BatchMarkets] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [MarketId] int NOT NULL,
    [RegulatoryStatus] nvarchar(40) NOT NULL,
    [ApprovalReference] nvarchar(max) NULL,
    [ApprovedAt] datetimeoffset NULL,
    [Source] nvarchar(40) NOT NULL,
    [LastSyncedAt] datetimeoffset NULL,
    CONSTRAINT [PK_BatchMarkets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BatchMarkets_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Batches] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BatchMarkets_Markets_MarketId] FOREIGN KEY ([MarketId]) REFERENCES [Markets] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ChangeRequests] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [ChangeType] nvarchar(40) NOT NULL,
    [ChecklistItemId] int NULL,
    [ProposedName] nvarchar(max) NULL,
    [ProposedStage] nvarchar(max) NULL,
    [ProposedDepartmentId] int NULL,
    [ProposedItemType] nvarchar(40) NULL,
    [ProposedIsMandatory] bit NULL,
    [ProposedWeight] decimal(6,2) NULL,
    [ProposedSequence] int NULL,
    [ProposedDueDate] date NULL,
    [Reason] nvarchar(max) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [RequestedById] int NOT NULL,
    [RequestedAt] datetimeoffset NOT NULL,
    [DecidedById] int NULL,
    [DecidedAt] datetimeoffset NULL,
    [DecisionComment] nvarchar(max) NULL,
    [SignatureId] int NULL,
    CONSTRAINT [PK_ChangeRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChangeRequests_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Batches] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChangeRequests_Signatures_SignatureId] FOREIGN KEY ([SignatureId]) REFERENCES [Signatures] ([Id]),
    CONSTRAINT [FK_ChangeRequests_Users_DecidedById] FOREIGN KEY ([DecidedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChangeRequests_Users_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ChecklistItems] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [TemplateItemId] int NULL,
    [Code] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Stage] nvarchar(max) NOT NULL,
    [DepartmentId] int NOT NULL,
    [ItemType] nvarchar(40) NOT NULL,
    [IsMandatory] bit NOT NULL,
    [Weight] decimal(6,2) NOT NULL,
    [Sequence] int NOT NULL,
    [IsFinalRelease] bit NOT NULL,
    [MarketId] int NULL,
    [ExternalMovementId] int NULL,
    [Status] nvarchar(40) NOT NULL,
    [DueDate] date NULL,
    [StartedAt] datetimeoffset NULL,
    [CompletedAt] datetimeoffset NULL,
    [CompletedById] int NULL,
    [Comments] nvarchar(max) NULL,
    [NotApplicableJustification] nvarchar(max) NULL,
    [BlockedReason] nvarchar(max) NULL,
    [EvidenceReference] nvarchar(max) NULL,
    [Source] nvarchar(40) NOT NULL,
    [SourceReference] nvarchar(max) NULL,
    [LastSyncedAt] datetimeoffset NULL,
    [IsRemoved] bit NOT NULL,
    CONSTRAINT [PK_ChecklistItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChecklistItems_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Batches] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChecklistItems_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChecklistItems_Markets_MarketId] FOREIGN KEY ([MarketId]) REFERENCES [Markets] ([Id]),
    CONSTRAINT [FK_ChecklistItems_Users_CompletedById] FOREIGN KEY ([CompletedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Dispatches] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [MarketId] int NULL,
    [DispatchedOn] date NOT NULL,
    [Reference] nvarchar(max) NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [RecordedById] int NOT NULL,
    CONSTRAINT [PK_Dispatches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Dispatches_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Batches] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Dispatches_Markets_MarketId] FOREIGN KEY ([MarketId]) REFERENCES [Markets] ([Id])
);
GO


CREATE TABLE [Movements] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [ExternalPartyId] int NOT NULL,
    [Purpose] nvarchar(40) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Quantity] decimal(18,3) NULL,
    [Unit] nvarchar(max) NULL,
    [Reference] nvarchar(max) NULL,
    [SentOn] date NULL,
    [ReceivedByVendorOn] date NULL,
    [ExpectedReturnOn] date NULL,
    [ReturnedOn] date NULL,
    CONSTRAINT [PK_Movements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Movements_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Batches] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Movements_ExternalParties_ExternalPartyId] FOREIGN KEY ([ExternalPartyId]) REFERENCES [ExternalParties] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [QualityEvents] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [EventType] nvarchar(40) NOT NULL,
    [Number] nvarchar(450) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [Criticality] nvarchar(40) NOT NULL,
    [OpenedOn] date NOT NULL,
    [ClosedOn] date NULL,
    [Source] nvarchar(40) NOT NULL,
    [LastSyncedAt] datetimeoffset NULL,
    CONSTRAINT [PK_QualityEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QualityEvents_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Batches] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Releases] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [Kind] nvarchar(40) NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [MarketId] int NULL,
    [Justification] nvarchar(max) NULL,
    [PendingItemsSummary] nvarchar(max) NULL,
    [ReleasedAt] datetimeoffset NOT NULL,
    [ReleasedById] int NOT NULL,
    [SignatureId] int NOT NULL,
    CONSTRAINT [PK_Releases] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Releases_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Batches] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Releases_Markets_MarketId] FOREIGN KEY ([MarketId]) REFERENCES [Markets] ([Id]),
    CONSTRAINT [FK_Releases_Signatures_SignatureId] FOREIGN KEY ([SignatureId]) REFERENCES [Signatures] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ChecklistItemDependencies] (
    [ChecklistItemId] int NOT NULL,
    [DependsOnItemId] int NOT NULL,
    CONSTRAINT [PK_ChecklistItemDependencies] PRIMARY KEY ([ChecklistItemId], [DependsOnItemId]),
    CONSTRAINT [FK_ChecklistItemDependencies_ChecklistItems_ChecklistItemId] FOREIGN KEY ([ChecklistItemId]) REFERENCES [ChecklistItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChecklistItemDependencies_ChecklistItems_DependsOnItemId] FOREIGN KEY ([DependsOnItemId]) REFERENCES [ChecklistItems] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Evidence] (
    [Id] int NOT NULL IDENTITY,
    [ChecklistItemId] int NOT NULL,
    [FileName] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [Sha256] nvarchar(max) NOT NULL,
    [Content] varbinary(max) NOT NULL,
    [UploadedAt] datetimeoffset NOT NULL,
    [UploadedById] int NOT NULL,
    CONSTRAINT [PK_Evidence] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Evidence_ChecklistItems_ChecklistItemId] FOREIGN KEY ([ChecklistItemId]) REFERENCES [ChecklistItems] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_AuditEntries_BatchId_Timestamp] ON [AuditEntries] ([BatchId], [Timestamp]);
GO


CREATE INDEX [IX_AuditEntries_EntityType_EntityId] ON [AuditEntries] ([EntityType], [EntityId]);
GO


CREATE UNIQUE INDEX [IX_Batches_BatchNo] ON [Batches] ([BatchNo]);
GO


CREATE INDEX [IX_Batches_CreatedById] ON [Batches] ([CreatedById]);
GO


CREATE INDEX [IX_Batches_CustomerId] ON [Batches] ([CustomerId]);
GO


CREATE INDEX [IX_Batches_Disposition] ON [Batches] ([Disposition]);
GO


CREATE INDEX [IX_Batches_ParentBatchId] ON [Batches] ([ParentBatchId]);
GO


CREATE INDEX [IX_Batches_ProductId] ON [Batches] ([ProductId]);
GO


CREATE INDEX [IX_Batches_SiteId] ON [Batches] ([SiteId]);
GO


CREATE INDEX [IX_Batches_TargetReleaseDate] ON [Batches] ([TargetReleaseDate]);
GO


CREATE INDEX [IX_Batches_TemplateId] ON [Batches] ([TemplateId]);
GO


CREATE UNIQUE INDEX [IX_BatchMarkets_BatchId_MarketId] ON [BatchMarkets] ([BatchId], [MarketId]);
GO


CREATE INDEX [IX_BatchMarkets_MarketId] ON [BatchMarkets] ([MarketId]);
GO


CREATE INDEX [IX_ChangeRequests_BatchId] ON [ChangeRequests] ([BatchId]);
GO


CREATE INDEX [IX_ChangeRequests_DecidedById] ON [ChangeRequests] ([DecidedById]);
GO


CREATE INDEX [IX_ChangeRequests_RequestedById] ON [ChangeRequests] ([RequestedById]);
GO


CREATE INDEX [IX_ChangeRequests_SignatureId] ON [ChangeRequests] ([SignatureId]);
GO


CREATE INDEX [IX_ChecklistItemDependencies_DependsOnItemId] ON [ChecklistItemDependencies] ([DependsOnItemId]);
GO


CREATE INDEX [IX_ChecklistItems_BatchId] ON [ChecklistItems] ([BatchId]);
GO


CREATE INDEX [IX_ChecklistItems_CompletedById] ON [ChecklistItems] ([CompletedById]);
GO


CREATE INDEX [IX_ChecklistItems_DepartmentId] ON [ChecklistItems] ([DepartmentId]);
GO


CREATE INDEX [IX_ChecklistItems_MarketId] ON [ChecklistItems] ([MarketId]);
GO


CREATE UNIQUE INDEX [IX_Customers_Code] ON [Customers] ([Code]);
GO


CREATE UNIQUE INDEX [IX_Departments_Code] ON [Departments] ([Code]);
GO


CREATE INDEX [IX_Dispatches_BatchId] ON [Dispatches] ([BatchId]);
GO


CREATE INDEX [IX_Dispatches_MarketId] ON [Dispatches] ([MarketId]);
GO


CREATE INDEX [IX_Evidence_ChecklistItemId] ON [Evidence] ([ChecklistItemId]);
GO


CREATE UNIQUE INDEX [IX_ExternalParties_Code] ON [ExternalParties] ([Code]);
GO


CREATE UNIQUE INDEX [IX_Markets_Code] ON [Markets] ([Code]);
GO


CREATE INDEX [IX_Movements_BatchId] ON [Movements] ([BatchId]);
GO


CREATE INDEX [IX_Movements_ExternalPartyId] ON [Movements] ([ExternalPartyId]);
GO


CREATE UNIQUE INDEX [IX_Products_Code] ON [Products] ([Code]);
GO


CREATE INDEX [IX_QualityEvents_BatchId] ON [QualityEvents] ([BatchId]);
GO


CREATE INDEX [IX_QualityEvents_Number] ON [QualityEvents] ([Number]);
GO


CREATE INDEX [IX_Releases_BatchId] ON [Releases] ([BatchId]);
GO


CREATE INDEX [IX_Releases_MarketId] ON [Releases] ([MarketId]);
GO


CREATE INDEX [IX_Releases_SignatureId] ON [Releases] ([SignatureId]);
GO


CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Sites_Code] ON [Sites] ([Code]);
GO


CREATE INDEX [IX_Sites_ExternalPartyId] ON [Sites] ([ExternalPartyId]);
GO


CREATE INDEX [IX_TemplateItems_DepartmentId] ON [TemplateItems] ([DepartmentId]);
GO


CREATE INDEX [IX_TemplateItems_TemplateId] ON [TemplateItems] ([TemplateId]);
GO


CREATE INDEX [IX_Templates_CustomerId] ON [Templates] ([CustomerId]);
GO


CREATE INDEX [IX_Templates_MarketId] ON [Templates] ([MarketId]);
GO


CREATE INDEX [IX_Templates_ProductId] ON [Templates] ([ProductId]);
GO


CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
GO


CREATE INDEX [IX_Users_DepartmentId] ON [Users] ([DepartmentId]);
GO


CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
GO




-- Append-only audit trail and e-signatures
CREATE TRIGGER trg_AuditEntries_append_only ON [AuditEntries] INSTEAD OF UPDATE, DELETE AS
BEGIN THROW 51000, 'Table AuditEntries is append-only (GxP audit trail)', 1; END
GO

CREATE TRIGGER trg_Signatures_append_only ON [Signatures] INSTEAD OF UPDATE, DELETE AS
BEGIN THROW 51000, 'Table Signatures is append-only (GxP audit trail)', 1; END
GO
