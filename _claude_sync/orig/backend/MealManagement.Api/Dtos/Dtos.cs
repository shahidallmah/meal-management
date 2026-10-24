using System.ComponentModel.DataAnnotations;

namespace MealManagement.Api.Dtos;

// ---------------- Common ----------------
public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
public record NamedItemDto(int Id, string Name, bool IsActive);

public class NamedItemSaveRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// ---------------- Auth ----------------
public class LoginRequest
{
    [Required] public string MobileNo { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class CheckMobileRequest
{
    [Required] public string MobileNo { get; set; } = "";
}
public record CheckMobileResponse(bool Registered, bool HasPassword, bool IsActive);

public class CreatePasswordRequest
{
    [Required] public string MobileNo { get; set; } = "";
    [Required] public string EmployeeCode { get; set; } = "";
    [Required, MinLength(6, ErrorMessage = "Password must be at least 6 characters.")] public string Password { get; set; } = "";
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(6, ErrorMessage = "Password must be at least 6 characters.")] public string NewPassword { get; set; } = "";
}

public record UserDto(int EmployeeId, string EmployeeCode, string EmployeeName, string MobileNo, string Role,
    string Department, string Designation, string? Email);
public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---------------- Employees ----------------
public record EmployeeListItemDto(int EmployeeId, string EmployeeCode, string EmployeeName,
    int DepartmentId, string Department, int DesignationId, string Designation,
    string MobileNo, string? Email, string Status, string Role, bool HasPassword,
    bool IsDefaultEligible, DateTime? LastLoginAt);

public record EligibilityDto(int MealEligibilityId, int MealTypeId, string MealTypeName,
    DateOnly EffectiveDate, DateOnly? ExpireDate, string? Remarks, bool IsCurrent);

public record EmployeeDetailDto(EmployeeListItemDto Employee, List<EligibilityDto> Eligibilities);

public class EmployeeSaveRequest
{
    [Required, StringLength(30)] public string EmployeeCode { get; set; } = "";
    [Required, StringLength(150)] public string EmployeeName { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Department is required.")] public int DepartmentId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Designation is required.")] public int DesignationId { get; set; }
    [Required, StringLength(20)] public string MobileNo { get; set; } = "";
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [Required] public string Status { get; set; } = "Active";
    [Required] public string Role { get; set; } = "Employee";

    /// <summary>On create only: also add a regular-meal eligibility starting on EligibleFrom.</summary>
    public bool DefaultEligible { get; set; }
    public DateOnly? EligibleFrom { get; set; }
    public DateOnly? EligibleTo { get; set; }
}

public class EligibilitySaveRequest
{
    [Range(1, int.MaxValue)] public int MealTypeId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpireDate { get; set; }
    [StringLength(250)] public string? Remarks { get; set; }
}

public class BulkEligibilityRequest : EligibilitySaveRequest
{
    [Required, MinLength(1)] public List<int> EmployeeIds { get; set; } = new();
}

// ---------------- Meals ----------------
public record MyMealItemDto(int MealTypeId, string MealTypeName, bool IsRegular, bool IsServed, string? NotServedReason,
    bool IsOn, bool IsEligible, bool IsOpen, bool CanChange, string? Source, DateTime LockTime, string? OccasionTitle,
    decimal EmployeeCost, decimal TotalCost);
public record MyMealDayDto(DateOnly Date, string DayName, bool IsToday, string? Holiday, List<MyMealItemDto> Meals);
public record MyMealsDto(DateOnly Today, int BookingWindowDays, string CutoffTime, bool AllowEligibleOptOut, List<MyMealDayDto> Days);

public class SetMealRequest
{
    public DateOnly MealDate { get; set; }
    [Range(1, int.MaxValue)] public int MealTypeId { get; set; }
    public bool IsOn { get; set; }
}

public class AdminSetMealRequest : SetMealRequest
{
    [Required, MinLength(1)] public List<int> EmployeeIds { get; set; } = new();
}

public record MealHistoryItemDto(DateOnly MealDate, int MealTypeId, string MealTypeName, string Source,
    decimal TotalCost, decimal EmployeeCost, decimal CompanyCost);
public record MealHistoryDto(DateOnly From, DateOnly To, int MealCount, decimal TotalCost, decimal EmployeeCost,
    decimal CompanyCost, List<MealHistoryItemDto> Items);

public record MealBoardRowDto(int EmployeeId, string EmployeeCode, string EmployeeName, string Department,
    string Designation, string MobileNo, bool IsEligible, bool IsOn, string? Source, DateTime? UpdatedAt);
public record MealBoardDto(DateOnly Date, int MealTypeId, string MealTypeName, bool IsServed, string? NotServedReason,
    bool IsOpenForEmployees, DateTime LockTime, int OnCount, int OffCount, decimal CostPerMeal,
    decimal EmployeeCostPerMeal, decimal TotalCost, List<MealBoardRowDto> Rows);

public record MealCountDto(DateOnly Date, int MealTypeId, string MealTypeName, bool IsServed, string? NotServedReason,
    int OnCount, decimal CostPerMeal, decimal EmployeeCostPerMeal, decimal TotalCost, decimal EmployeeTotal,
    decimal CompanyTotal, string? OccasionTitle);

// ---------------- Meal types / occasions / holidays ----------------
public record MealTypeDto(int MealTypeId, string Code, string Name, bool IsRegular, decimal DefaultTotalCost,
    decimal DefaultEmployeeCost, int SortOrder, bool IsActive);

public class MealTypeSaveRequest
{
    [Required, StringLength(20)] public string Code { get; set; } = "";
    [Required, StringLength(50)] public string Name { get; set; } = "";
    public bool IsRegular { get; set; }
    [Range(0, 100000)] public decimal DefaultTotalCost { get; set; }
    [Range(0, 100000)] public decimal DefaultEmployeeCost { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public record OccasionDto(int MealOccasionId, int MealTypeId, string MealTypeName, DateOnly OccasionDate,
    string Title, bool AutoOnForAll, bool IsActive);

public class OccasionSaveRequest
{
    [Range(1, int.MaxValue)] public int MealTypeId { get; set; }
    public DateOnly OccasionDate { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = "";
    public bool AutoOnForAll { get; set; }
    public bool IsActive { get; set; } = true;
}

public record HolidayDto(int HolidayId, DateOnly HolidayDate, string DayName, string Description);

public class HolidaySaveRequest
{
    public DateOnly HolidayDate { get; set; }
    [Required, StringLength(150)] public string Description { get; set; } = "";
}

// ---------------- Costs ----------------
public record DailyCostDto(DateOnly MealDate, string DayName, int MealTypeId, string MealTypeName, bool IsServed,
    string? NotServedReason, int? MealDailyCostId, decimal TotalCost, decimal EmployeeCost, decimal CompanyCost,
    bool IsDefault, string? Remarks, int MealCount, decimal DayTotal, decimal DayEmployeeTotal, decimal DayCompanyTotal);

public class CostSaveRequest
{
    public DateOnly MealDate { get; set; }
    [Range(1, int.MaxValue)] public int MealTypeId { get; set; }
    [Range(0, 100000)] public decimal TotalCost { get; set; }
    [Range(0, 100000)] public decimal EmployeeCost { get; set; }
    [StringLength(250)] public string? Remarks { get; set; }
}

public class CostBulkRequest
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    [Range(1, int.MaxValue)] public int MealTypeId { get; set; }
    [Range(0, 100000)] public decimal TotalCost { get; set; }
    [Range(0, 100000)] public decimal EmployeeCost { get; set; }
    public bool ServedDaysOnly { get; set; } = true;
    public bool OverwriteExisting { get; set; } = true;
    [StringLength(250)] public string? Remarks { get; set; }
}

// ---------------- Settings ----------------
public record SettingDto(string Key, string Value, string? Description);

// ---------------- Dashboard ----------------
public record MonthSummaryDto(DateOnly From, DateOnly To, int Meals, decimal TotalCost, decimal EmployeeTotal, decimal CompanyTotal);
public record EmployeeStatsDto(int Active, int Inactive, int DefaultEligible, int WithoutPassword);
public record TrendPointDto(DateOnly Date, int Meals, decimal TotalCost);
public record DashboardDto(DateOnly Today, List<MealCountDto> TodayMeals, List<MealCountDto> TomorrowMeals,
    MonthSummaryDto Month, EmployeeStatsDto Employees, List<TrendPointDto> Trend);
