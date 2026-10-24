namespace MealManagement.Api.Data;

public static class Roles
{
    public const string Employee = "Employee";
    public const string Admin = "Admin";
}

public static class EmployeeStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}

public static class EntrySource
{
    public const string Auto = "Auto";
    public const string Employee = "Employee";
    public const string Admin = "Admin";
}

public class Department
{
    public int DepartmentId { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Designation
{
    public int DesignationId { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Employee
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int DesignationId { get; set; }
    public Designation? Designation { get; set; }
    public string MobileNo { get; set; } = "";
    public string? Email { get; set; }
    public string Status { get; set; } = EmployeeStatus.Active;
    public string Role { get; set; } = Roles.Employee;
    public string? PasswordHash { get; set; }
    public DateTime? PasswordSetAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public List<MealEligibility> Eligibilities { get; set; } = new();
}

public class MealType
{
    public int MealTypeId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsRegular { get; set; } = true;
    public decimal DefaultTotalCost { get; set; }
    public decimal DefaultEmployeeCost { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MealEligibility
{
    public int MealEligibilityId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int MealTypeId { get; set; }
    public MealType? MealType { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpireDate { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }

    public bool Covers(DateOnly date) => EffectiveDate <= date && (ExpireDate == null || ExpireDate >= date);
}

public class MealOccasion
{
    public int MealOccasionId { get; set; }
    public int MealTypeId { get; set; }
    public MealType? MealType { get; set; }
    public DateOnly OccasionDate { get; set; }
    public string Title { get; set; } = "";
    public bool AutoOnForAll { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
}

public class Holiday
{
    public int HolidayId { get; set; }
    public DateOnly HolidayDate { get; set; }
    public string Description { get; set; } = "";
}

public class MealDailyCost
{
    public int MealDailyCostId { get; set; }
    public DateOnly MealDate { get; set; }
    public int MealTypeId { get; set; }
    public MealType? MealType { get; set; }
    public decimal TotalCost { get; set; }
    public decimal EmployeeCost { get; set; }
    public decimal CompanyCost { get; private set; } // computed column
    public string? Remarks { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

public class MealEntry
{
    public long MealEntryId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly MealDate { get; set; }
    public int MealTypeId { get; set; }
    public MealType? MealType { get; set; }
    public bool IsOn { get; set; }
    public string Source { get; set; } = EntrySource.Auto;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

public class MealEntryLog
{
    public long MealEntryLogId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly MealDate { get; set; }
    public int MealTypeId { get; set; }
    public bool? OldIsOn { get; set; }
    public bool NewIsOn { get; set; }
    public string Source { get; set; } = "";
    public int? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class AppSetting
{
    public string SettingKey { get; set; } = "";
    public string SettingValue { get; set; } = "";
    public string? Description { get; set; }
}

public class CompanyLogo
{
    public byte CompanyLogoId { get; set; } = 1;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "";
    public string? FileName { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
