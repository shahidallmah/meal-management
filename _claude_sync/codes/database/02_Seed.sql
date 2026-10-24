/* =====================================================================
   Seed data. Run after 01_Schema.sql.
   The first Admin login is created by the API on first start-up from
   appsettings.json (SeedAdmin section), so no password hash is needed here.
   ===================================================================== */
SET NOCOUNT ON;

INSERT INTO dbo.EmployeeRole (RoleCode, RoleName, Description) VALUES
 ('Employee', N'Employee', N'Turns own meals ON/OFF, sees own history and contribution'),
 ('Admin',    N'Admin',    N'Full access: employees, eligibility, meal board, costs, settings, reports');

INSERT INTO dbo.AppSetting (SettingKey, SettingValue, Description) VALUES
 ('BookingWindowDays',  '7',          N'How many days ahead employees can turn meals ON/OFF'),
 ('CutoffTime',         '17:00',      N'A day''s meal locks at this time on the previous day (HH:mm, 24h)'),
 ('WeeklyOffDays',      'Friday',     N'Comma separated weekdays with no regular meal, e.g. Friday,Saturday'),
 ('AllowEligibleOptOut','true',       N'Can default-eligible employees turn their auto meal OFF'),
 ('TimeZoneId',         'Asia/Dhaka', N'Time zone used for dates and cut-off'),
 ('CompanyName',        N'Company',   N'Shown on reports'),
 ('SetupCodeHours',     '24',         N'How many hours an admin-issued one-time code stays valid (1-168)');

INSERT INTO dbo.MealType (Code, Name, IsRegular, DefaultTotalCost, DefaultEmployeeCost, SortOrder, IsActive) VALUES
 ('LUNCH',  N'Lunch',  1, 0, 0, 1, 1),
 ('DINNER', N'Dinner', 0, 0, 0, 2, 1),
 ('IFTAR',  N'Iftar',  0, 0, 0, 3, 0);

INSERT INTO dbo.Department (Name) VALUES (N'Administration'), (N'Finance'), (N'HR'), (N'IT'), (N'Operations'), (N'Sales');
INSERT INTO dbo.Designation (Name) VALUES (N'Executive'), (N'Senior Executive'), (N'Officer'), (N'Manager'), (N'Senior Manager');
GO
