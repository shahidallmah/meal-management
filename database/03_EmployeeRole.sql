/* =====================================================================
   EmployeeRole lookup table.
   Safe to run on a database already created with 01_Schema.sql
   (it is also included in 01_Schema.sql for fresh installs).
   Employee.Role keeps the role code ('Employee' / 'Admin') and now has
   a foreign key to this table instead of a fixed CHECK list.
   ===================================================================== */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.EmployeeRole', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeRole (
        RoleCode     VARCHAR(10)   NOT NULL CONSTRAINT PK_EmployeeRole PRIMARY KEY,
        RoleName     NVARCHAR(50)  NOT NULL,
        Description  NVARCHAR(250) NULL,
        IsActive     BIT           NOT NULL CONSTRAINT DF_EmployeeRole_IsActive DEFAULT (1)
    );
END;
GO

MERGE dbo.EmployeeRole AS t
USING (VALUES
    ('Employee', N'Employee', N'Turns own meals ON/OFF, sees own history and contribution'),
    ('Admin',    N'Admin',    N'Full access: employees, eligibility, meal board, costs, settings, reports')
) AS s (RoleCode, RoleName, Description)
ON t.RoleCode = s.RoleCode
WHEN NOT MATCHED THEN INSERT (RoleCode, RoleName, Description) VALUES (s.RoleCode, s.RoleName, s.Description);
GO

-- Replace the fixed CHECK list with a foreign key
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Employee_Role' AND parent_object_id = OBJECT_ID('dbo.Employee'))
    ALTER TABLE dbo.Employee DROP CONSTRAINT CK_Employee_Role;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Employee_EmployeeRole')
    ALTER TABLE dbo.Employee WITH CHECK
        ADD CONSTRAINT FK_Employee_EmployeeRole FOREIGN KEY (Role) REFERENCES dbo.EmployeeRole (RoleCode);
GO

SELECT RoleCode, RoleName, Description, IsActive FROM dbo.EmployeeRole;
GO
