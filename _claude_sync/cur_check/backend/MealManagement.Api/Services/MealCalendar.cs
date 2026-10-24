using MealManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Services;

/// <summary>
/// Snapshot of everything needed to decide, for a date range, whether a meal is served,
/// whether it can still be changed, what an employee's default is and what it costs.
/// Build it once per request with <see cref="MealCalendarFactory"/>.
/// </summary>
public class MealCalendar
{
    public MealSettings Settings { get; }
    public DateTime Now { get; }
    public DateOnly Today { get; }
    public IReadOnlyList<MealType> MealTypes { get; }

    private readonly Dictionary<DateOnly, string> _holidays;
    private readonly Dictionary<(DateOnly, int), MealOccasion> _occasions;
    private readonly Dictionary<(DateOnly, int), MealDailyCost> _costs;
    private readonly Dictionary<int, MealType> _typesById;

    public MealCalendar(MealSettings settings, IReadOnlyList<MealType> mealTypes,
        IEnumerable<Holiday> holidays, IEnumerable<MealOccasion> occasions, IEnumerable<MealDailyCost> costs)
    {
        Settings = settings;
        Now = settings.NowLocal;
        Today = DateOnly.FromDateTime(Now);
        MealTypes = mealTypes;
        _typesById = mealTypes.ToDictionary(x => x.MealTypeId);
        _holidays = holidays.ToDictionary(x => x.HolidayDate, x => x.Description);
        _occasions = occasions.ToDictionary(x => (x.OccasionDate, x.MealTypeId));
        _costs = costs.ToDictionary(x => (x.MealDate, x.MealTypeId));
    }

    public MealType? Type(int mealTypeId) => _typesById.GetValueOrDefault(mealTypeId);

    public DateOnly LastBookableDate => Today.AddDays(Settings.BookingWindowDays);

    public string? HolidayName(DateOnly date) => _holidays.GetValueOrDefault(date);

    public bool IsWeeklyOff(DateOnly date) => Settings.WeeklyOffDays.Contains(date.DayOfWeek);

    public bool IsWorkingDay(DateOnly date) => !IsWeeklyOff(date) && !_holidays.ContainsKey(date);

    public MealOccasion? Occasion(DateOnly date, int mealTypeId) => _occasions.GetValueOrDefault((date, mealTypeId));

    /// <summary>Is this meal served on this date?</summary>
    public bool IsServed(DateOnly date, MealType type) =>
        type.IsActive && (type.IsRegular ? IsWorkingDay(date) : Occasion(date, type.MealTypeId) != null);

    /// <summary>Human readable reason a meal is not served (null when it is served).</summary>
    public string? NotServedReason(DateOnly date, MealType type)
    {
        if (IsServed(date, type)) return null;
        if (!type.IsActive) return "Meal type inactive";
        if (!type.IsRegular) return "Not scheduled";
        if (_holidays.TryGetValue(date, out var name)) return $"Holiday: {name}";
        return "Weekly off";
    }

    /// <summary>The moment a date's meal locks: cut-off time on the previous day.</summary>
    public DateTime LockTime(DateOnly date) => date.AddDays(-1).ToDateTime(Settings.CutoffTime);

    /// <summary>Can an employee still change this date?</summary>
    public bool IsEditableByEmployee(DateOnly date) =>
        date > Today && date <= LastBookableDate && Now < LockTime(date);

    /// <summary>What the system marks for this employee if they never touch the meal.</summary>
    public bool DefaultIsOn(Employee employee, DateOnly date, MealType type)
    {
        if (employee.Status != EmployeeStatus.Active || !IsServed(date, type)) return false;
        if (type.IsRegular) return IsEligible(employee, date, type.MealTypeId);
        return Occasion(date, type.MealTypeId)?.AutoOnForAll == true;
    }

    public static bool IsEligible(Employee employee, DateOnly date, int mealTypeId) =>
        employee.Eligibilities.Any(e => e.MealTypeId == mealTypeId && e.Covers(date));

    /// <summary>Per-meal cost for the date: the daily entry if set, otherwise the meal type default.</summary>
    public (decimal Total, decimal Employee, decimal Company, bool IsDefault) Cost(DateOnly date, int mealTypeId)
    {
        if (_costs.TryGetValue((date, mealTypeId), out var c))
            return (c.TotalCost, c.EmployeeCost, c.TotalCost - c.EmployeeCost, false);
        var t = Type(mealTypeId);
        var total = t?.DefaultTotalCost ?? 0;
        var emp = t?.DefaultEmployeeCost ?? 0;
        return (total, emp, total - emp, true);
    }
}

public class MealCalendarFactory
{
    private readonly MealDbContext _db;
    private readonly SettingsService _settings;

    public MealCalendarFactory(MealDbContext db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<MealCalendar> CreateAsync(DateOnly from, DateOnly to, bool includeInactiveTypes = false)
    {
        var settings = await _settings.GetAsync();
        var types = await _db.MealTypes.AsNoTracking()
            .Where(x => includeInactiveTypes || x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .ToListAsync();
        var holidays = await _db.Holidays.AsNoTracking()
            .Where(x => x.HolidayDate >= from && x.HolidayDate <= to).ToListAsync();
        var occasions = await _db.MealOccasions.AsNoTracking()
            .Where(x => x.IsActive && x.OccasionDate >= from && x.OccasionDate <= to).ToListAsync();
        var costs = await _db.MealDailyCosts.AsNoTracking()
            .Where(x => x.MealDate >= from && x.MealDate <= to).ToListAsync();
        return new MealCalendar(settings, types, holidays, occasions, costs);
    }

    /// <summary>Calendar covering today .. today + booking window (plus some history).</summary>
    public async Task<MealCalendar> CreateForWindowAsync(int daysBack = 0)
    {
        var settings = await _settings.GetAsync();
        var today = settings.Today;
        return await CreateAsync(today.AddDays(-daysBack), today.AddDays(settings.BookingWindowDays + 1));
    }
}
