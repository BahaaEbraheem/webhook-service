-- خدمة الويب هوك - Webhook Service
-- ترحيل قاعدة البيانات الأولي - Initial Database Migration
-- تاريخ الإنشاء: 2025-09-10

-- إنشاء جدول المشتركين - Create Subscribers table
CREATE TABLE [Subscribers] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [TenantId] nvarchar(100) NOT NULL,
    [CallbackUrl] nvarchar(500) NOT NULL,
    [EventTypes] nvarchar(max) NOT NULL,
    [EncryptedSecret] nvarchar(500) NOT NULL,
    [KeyId] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL DEFAULT 1,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    
    -- فهارس للأداء - Performance indexes
    INDEX [IX_Subscribers_TenantId] ([TenantId]),
    INDEX [IX_Subscribers_IsActive] ([IsActive]),
    INDEX [IX_Subscribers_KeyId] ([KeyId])
);

-- إنشاء جدول الأحداث - Create Events table
CREATE TABLE [Events] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [TenantId] nvarchar(100) NOT NULL,
    [EventType] nvarchar(100) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [IdempotencyKey] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    
    -- فهارس للأداء - Performance indexes
    INDEX [IX_Events_TenantId] ([TenantId]),
    INDEX [IX_Events_EventType] ([EventType]),
    INDEX [IX_Events_IdempotencyKey] ([IdempotencyKey]),
    INDEX [IX_Events_CreatedAt] ([CreatedAt])
);

-- إنشاء جدول التسليمات - Create Deliveries table
CREATE TABLE [Deliveries] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [EventId] uniqueidentifier NOT NULL,
    [SubscriberId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [AttemptNumber] int NOT NULL DEFAULT 1,
    [HttpStatusCode] int NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [DurationMs] bigint NOT NULL DEFAULT 0,
    [CreatedAt] datetime2 NOT NULL,
    [DeliveredAt] datetime2 NULL,
    [NextRetryAt] datetime2 NULL,
    
    -- المفاتيح الخارجية - Foreign keys
    CONSTRAINT [FK_Deliveries_Events] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Deliveries_Subscribers] FOREIGN KEY ([SubscriberId]) REFERENCES [Subscribers] ([Id]) ON DELETE CASCADE,
    
    -- فهارس للأداء - Performance indexes
    INDEX [IX_Deliveries_EventId] ([EventId]),
    INDEX [IX_Deliveries_SubscriberId] ([SubscriberId]),
    INDEX [IX_Deliveries_Status] ([Status]),
    INDEX [IX_Deliveries_NextRetryAt] ([NextRetryAt]),
    INDEX [IX_Deliveries_CreatedAt] ([CreatedAt])
);

-- إضافة قيود فريدة - Add unique constraints
ALTER TABLE [Events] ADD CONSTRAINT [UQ_Events_IdempotencyKey] UNIQUE ([IdempotencyKey]);
ALTER TABLE [Subscribers] ADD CONSTRAINT [UQ_Subscribers_KeyId] UNIQUE ([KeyId]);

-- إضافة تعليقات للجداول - Add table comments
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'جدول المشتركين في خدمة الويب هوك - Webhook service subscribers table',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Subscribers';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'جدول الأحداث في خدمة الويب هوك - Webhook service events table',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Events';

EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'جدول التسليمات في خدمة الويب هوك - Webhook service deliveries table',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Deliveries';

-- إدراج بيانات تجريبية - Insert sample data
INSERT INTO [Subscribers] ([Id], [TenantId], [CallbackUrl], [EventTypes], [EncryptedSecret], [KeyId], [IsActive], [CreatedAt], [UpdatedAt])
VALUES 
    (NEWID(), 'tenant-demo', 'https://webhook.site/demo', '["user.created","order.completed"]', 'encrypted_secret_demo', 'demo_key_123', 1, GETUTCDATE(), GETUTCDATE());

PRINT 'تم إنشاء قاعدة البيانات بنجاح - Database created successfully';
