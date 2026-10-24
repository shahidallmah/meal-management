/* =====================================================================
   One-time codes issued by the admin for first sign-in (Activation)
   and forgotten passwords (Reset).
   Safe to run on a database already created with 01_Schema.sql
   (it is also included in 01_Schema.sql / 02_Seed.sql for fresh installs).
   Only a hash of each code is stored; the plain code is shown once to the admin.
   ===================================================================== */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.PasswordSetupCode', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordSetupCode (
        PasswordSetupCodeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordSetupCode PRIMARY KEY,
        EmployeeId     INT            NOT NULL CONSTRAINT FK_PasswordSetupCode_Employee REFERENCES dbo.Employee(EmployeeId),
        Purpose        VARCHAR(10)    NOT NULL CONSTRAINT CK_PasswordSetupCode_Purpose CHECK (Purpose IN ('Activation', 'Reset')),
        CodeHash       NVARCHAR(500)  NOT NULL,
        ExpiresAt      DATETIME2(0)   NOT NULL,   -- UTC
        FailedAttempts TINYINT        NOT NULL CONSTRAINT DF_PasswordSetupCode_Failed DEFAULT (0),
        UsedAt         DATETIME2(0)   NULL,       -- UTC, set when the employee sets a password with it
        RevokedAt      DATETIME2(0)   NULL,       -- UTC, replaced by a newer code or blocked after too many wrong tries
        CreatedBy      INT            NULL,
        CreatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_PasswordSetupCode_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_PasswordSetupCode_Employee ON dbo.PasswordSetupCode (EmployeeId, CreatedAt DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AppSetting WHERE SettingKey = 'SetupCodeHours')
    INSERT INTO dbo.AppSetting (SettingKey, SettingValue, Description)
    VALUES ('SetupCodeHours', '24', N'How many hours an admin-issued one-time code stays valid (1-168)');
GO
