/* =====================================================================
   Employee Meal Management  -  SQL Server schema
   Target: SQL Server 2016+ (Express works)
   Run on an empty database, e.g.:
       CREATE DATABASE MealManagement;
       GO
       USE MealManagement;
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- Master data ---------- */
CREATE TABLE dbo.Department (
    DepartmentId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Department PRIMARY KEY,
    Name           NVARCHAR(100)     NOT NULL,
    IsActive       BIT               NOT NULL CONSTRAINT DF_Department_IsActive DEFAULT (1),
    CONSTRAINT UQ_Department_Name UNIQUE (Name)
);
GO

CREATE TABLE dbo.Designation (
    DesignationId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Designation PRIMARY KEY,
    Name           NVARCHAR(100)     NOT NULL,
    IsActive       BIT               NOT NULL CONSTRAINT DF_Designation_IsActive DEFAULT (1),
    CONSTRAINT UQ_Designation_Name UNIQUE (Name)
);
GO

/* ---------- Roles ---------- */
CREATE TABLE dbo.EmployeeRole (
    RoleCode     VARCHAR(10)   NOT NULL CONSTRAINT PK_EmployeeRole PRIMARY KEY,
    RoleName     NVARCHAR(50)  NOT NULL,
    Description  NVARCHAR(250) NULL,
    IsActive     BIT           NOT NULL CONSTRAINT DF_EmployeeRole_IsActive DEFAULT (1)
);
GO

/* ---------- Employees / users ----------
   Login = MobileNo + password. PasswordHash stays NULL until the employee
   creates a password after the admin registers them.                   */
CREATE TABLE dbo.Employee (
    EmployeeId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Employee PRIMARY KEY,
    EmployeeCode   NVARCHAR(30)      NOT NULL,
    EmployeeName   NVARCHAR(150)     NOT NULL,
    DepartmentId   INT               NOT NULL CONSTRAINT FK_Employee_Department REFERENCES dbo.Department(DepartmentId),
    DesignationId  INT               NOT NULL CONSTRAINT FK_Employee_Designation REFERENCES dbo.Designation(DesignationId),
    MobileNo       VARCHAR(20)       NOT NULL,
    Email          NVARCHAR(150)     NULL,
    Status         VARCHAR(10)       NOT NULL CONSTRAINT DF_Employee_Status DEFAULT ('Active'),
    Role           VARCHAR(10)       NOT NULL CONSTRAINT DF_Employee_Role DEFAULT ('Employee')
                                     CONSTRAINT FK_Employee_EmployeeRole REFERENCES dbo.EmployeeRole(RoleCode),
    PasswordHash   NVARCHAR(500)     NULL,
    PasswordSetAt  DATETIME2(0)      NULL,
    LastLoginAt    DATETIME2(0)      NULL,
    CreatedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_Employee_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy      INT               NULL,
    UpdatedAt      DATETIME2(0)      NULL,
    UpdatedBy      INT               NULL,
    CONSTRAINT UQ_Employee_Code   UNIQUE (EmployeeCode),
    CONSTRAINT UQ_Employee_Mobile UNIQUE (MobileNo),
    CONSTRAINT CK_Employee_Status CHECK (Status IN ('Active','Inactive'))
);
GO

/* ---------- Meal types ----------
   IsRegular = 1 : served every working day (Lunch).
   IsRegular = 0 : only served on dates defined in MealOccasion (special dinner, iftar ...). */
CREATE TABLE dbo.MealType (
    MealTypeId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MealType PRIMARY KEY,
    Code                VARCHAR(20)       NOT NULL,
    Name                NVARCHAR(50)      NOT NULL,
    IsRegular           BIT               NOT NULL CONSTRAINT DF_MealType_IsRegular DEFAULT (1),
    DefaultTotalCost    DECIMAL(10,2)     NOT NULL CONSTRAINT DF_MealType_DefTotal DEFAULT (0),
    DefaultEmployeeCost DECIMAL(10,2)     NOT NULL CONSTRAINT DF_MealType_DefEmp DEFAULT (0),
    SortOrder           INT               NOT NULL CONSTRAINT DF_MealType_Sort DEFAULT (0),
    IsActive            BIT               NOT NULL CONSTRAINT DF_MealType_IsActive DEFAULT (1),
    CONSTRAINT UQ_MealType_Code UNIQUE (Code),
    CONSTRAINT CK_MealType_DefCost CHECK (DefaultEmployeeCost >= 0 AND DefaultEmployeeCost <= DefaultTotalCost)
);
GO

/* ---------- Default eligibility ----------
   An employee with an eligibility row covering a date gets the meal
   marked ON automatically for that date. ExpireDate NULL = open-ended.  */
CREATE TABLE dbo.MealEligibility (
    MealEligibilityId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MealEligibility PRIMARY KEY,
    EmployeeId        INT           NOT NULL CONSTRAINT FK_MealEligibility_Employee REFERENCES dbo.Employee(EmployeeId),
    MealTypeId        INT           NOT NULL CONSTRAINT FK_MealEligibility_MealType REFERENCES dbo.MealType(MealTypeId),
    EffectiveDate     DATE          NOT NULL,
    ExpireDate        DATE          NULL,
    Remarks           NVARCHAR(250) NULL,
    CreatedAt         DATETIME2(0)  NOT NULL CONSTRAINT DF_MealEligibility_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy         INT           NULL,
    CONSTRAINT CK_MealEligibility_Dates CHECK (ExpireDate IS NULL OR ExpireDate >= EffectiveDate)
);
GO
CREATE INDEX IX_MealEligibility_Employee ON dbo.MealEligibility (EmployeeId, MealTypeId, EffectiveDate);
GO

/* ---------- Occasions (for non-regular meal types) ---------- */
CREATE TABLE dbo.MealOccasion (
    MealOccasionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MealOccasion PRIMARY KEY,
    MealTypeId     INT           NOT NULL CONSTRAINT FK_MealOccasion_MealType REFERENCES dbo.MealType(MealTypeId),
    OccasionDate   DATE          NOT NULL,
    Title          NVARCHAR(150) NOT NULL,
    AutoOnForAll   BIT           NOT NULL CONSTRAINT DF_MealOccasion_AutoOn DEFAULT (0), -- 1 = every active employee is marked ON; 0 = opt-in
    IsActive       BIT           NOT NULL CONSTRAINT DF_MealOccasion_IsActive DEFAULT (1),
    CreatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_MealOccasion_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy      INT           NULL,
    CONSTRAINT UQ_MealOccasion UNIQUE (MealTypeId, OccasionDate)
);
GO

/* ---------- Holidays (no regular meal) ---------- */
CREATE TABLE dbo.Holiday (
    HolidayId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Holiday PRIMARY KEY,
    HolidayDate  DATE          NOT NULL,
    Description  NVARCHAR(150) NOT NULL,
    CONSTRAINT UQ_Holiday_Date UNIQUE (HolidayDate)
);
GO

/* ---------- Daily cost ----------
   TotalCost    = cost of one meal on that date
   EmployeeCost = portion paid by the employee
   CompanyCost  = TotalCost - EmployeeCost (computed)
   If no row exists for a date, MealType default costs are used.          */
CREATE TABLE dbo.MealDailyCost (
    MealDailyCostId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MealDailyCost PRIMARY KEY,
    MealDate        DATE          NOT NULL,
    MealTypeId      INT           NOT NULL CONSTRAINT FK_MealDailyCost_MealType REFERENCES dbo.MealType(MealTypeId),
    TotalCost       DECIMAL(10,2) NOT NULL,
    EmployeeCost    DECIMAL(10,2) NOT NULL,
    CompanyCost     AS (TotalCost - EmployeeCost) PERSISTED,
    Remarks         NVARCHAR(250) NULL,
    UpdatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_MealDailyCost_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedBy       INT           NULL,
    CONSTRAINT UQ_MealDailyCost UNIQUE (MealDate, MealTypeId),
    CONSTRAINT CK_MealDailyCost CHECK (EmployeeCost >= 0 AND EmployeeCost <= TotalCost)
);
GO

/* ---------- Meal entries (one row per employee / date / meal type) ---------- */
CREATE TABLE dbo.MealEntry (
    MealEntryId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MealEntry PRIMARY KEY,
    EmployeeId   INT          NOT NULL CONSTRAINT FK_MealEntry_Employee REFERENCES dbo.Employee(EmployeeId),
    MealDate     DATE         NOT NULL,
    MealTypeId   INT          NOT NULL CONSTRAINT FK_MealEntry_MealType REFERENCES dbo.MealType(MealTypeId),
    IsOn         BIT          NOT NULL,
    Source       VARCHAR(10)  NOT NULL,   -- Auto | Employee | Admin
    CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_MealEntry_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt    DATETIME2(0) NULL,
    UpdatedBy    INT          NULL,
    CONSTRAINT UQ_MealEntry UNIQUE (EmployeeId, MealDate, MealTypeId),
    CONSTRAINT CK_MealEntry_Source CHECK (Source IN ('Auto','Employee','Admin'))
);
GO
CREATE INDEX IX_MealEntry_Date ON dbo.MealEntry (MealDate, MealTypeId) INCLUDE (EmployeeId, IsOn);
GO

/* ---------- Audit trail of ON/OFF changes ---------- */
CREATE TABLE dbo.MealEntryLog (
    MealEntryLogId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MealEntryLog PRIMARY KEY,
    EmployeeId     INT          NOT NULL,
    MealDate       DATE         NOT NULL,
    MealTypeId     INT          NOT NULL,
    OldIsOn        BIT          NULL,
    NewIsOn        BIT          NOT NULL,
    Source         VARCHAR(10)  NOT NULL,
    ChangedBy      INT          NULL,
    ChangedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_MealEntryLog_ChangedAt DEFAULT (SYSUTCDATETIME())
);
GO
CREATE INDEX IX_MealEntryLog_Emp ON dbo.MealEntryLog (EmployeeId, MealDate);
GO

/* ---------- Settings (key/value) ---------- */
CREATE TABLE dbo.AppSetting (
    SettingKey   VARCHAR(50)   NOT NULL CONSTRAINT PK_AppSetting PRIMARY KEY,
    SettingValue NVARCHAR(200) NOT NULL,
    Description  NVARCHAR(300) NULL
);
GO
