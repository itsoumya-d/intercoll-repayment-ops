CREATE TABLE dbo.Customers (
    CustomerId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    FullName NVARCHAR(160) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.DebtAccounts (
    AccountId NVARCHAR(24) NOT NULL PRIMARY KEY,
    CustomerId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.Customers(CustomerId),
    Balance DECIMAL(18,2) NOT NULL CHECK (Balance >= 0),
    Arrears DECIMAL(18,2) NOT NULL CHECK (Arrears >= 0),
    HardshipStatus TINYINT NOT NULL DEFAULT 0,
    IsDisputed BIT NOT NULL DEFAULT 0,
    RowVersion ROWVERSION
);

CREATE TABLE dbo.RepaymentPlans (
    PlanId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    AccountId NVARCHAR(24) NOT NULL REFERENCES dbo.DebtAccounts(AccountId),
    ScheduledPayment DECIMAL(18,2) NOT NULL CHECK (ScheduledPayment >= 0),
    Frequency TINYINT NOT NULL,
    Instalments INT NOT NULL CHECK (Instalments >= 0),
    FirstPayment DATE NOT NULL,
    FinalPayment DATE NOT NULL,
    Rationale NVARCHAR(400) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.FieldVisits (
    VisitId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    AccountId NVARCHAR(24) NOT NULL REFERENCES dbo.DebtAccounts(AccountId),
    VisitDate DATE NOT NULL,
    Outcome TINYINT NOT NULL,
    Notes NVARCHAR(1000) NOT NULL,
    PaymentAgreed DECIMAL(18,2) NULL,
    FollowUp DATE NULL
);

CREATE TABLE dbo.AuditEvents (
    EventId BIGINT IDENTITY(1,1) PRIMARY KEY,
    AccountId NVARCHAR(24) NOT NULL,
    Action NVARCHAR(80) NOT NULL,
    Detail NVARCHAR(400) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_RepaymentPlans_AccountId ON dbo.RepaymentPlans(AccountId, CreatedAt DESC);
CREATE INDEX IX_FieldVisits_AccountId ON dbo.FieldVisits(AccountId, VisitDate DESC);
CREATE INDEX IX_AuditEvents_AccountId ON dbo.AuditEvents(AccountId, CreatedAt DESC);
