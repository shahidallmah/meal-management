/* =====================================================================
   Company logo (single row, Id = 1).
   Safe to run on a database already created with 01_Schema.sql
   (it is also included in 01_Schema.sql for fresh installs).
   Upload / remove it from the web app: Admin → Settings → Company logo.
   ===================================================================== */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.CompanyLogo', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyLogo (
        CompanyLogoId TINYINT        NOT NULL CONSTRAINT PK_CompanyLogo PRIMARY KEY
                                     CONSTRAINT CK_CompanyLogo_Single CHECK (CompanyLogoId = 1),
        Content       VARBINARY(MAX) NOT NULL,
        ContentType   VARCHAR(50)    NOT NULL,   -- image/png, image/jpeg, image/webp
        FileName      NVARCHAR(200)  NULL,
        UpdatedBy     INT            NULL,
        UpdatedAt     DATETIME2(0)   NOT NULL CONSTRAINT DF_CompanyLogo_UpdatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO
