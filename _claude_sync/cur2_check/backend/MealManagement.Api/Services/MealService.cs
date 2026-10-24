using MealManagement.Api.Data;
using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Services;

public class MealService
{
    private readonly MealDbContext _db;
    private readonly MealCalendarFactory _calendars;

    public MealService(MealDbContext db, MealCalendarFactory calendars)
    {
        _db = db;
        _calendars = calendars;
    }

    // ------------------------------------------------------------------
    // Employee: upcoming meals
    // ------------------------------------------------------------------
    public async Task<MyMealsDto> GetMyMealsAsync(int employeeId)
    {
        var cal = await _calendars.CreateForWindowAsync();
        var employee = await LoadEmployeeAsync(employeeId);
        var from = cal.Today;
        var to = cal.LastBookableDate;

        var entries = await _db.MealEntries.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.MealDate >= from && x.MealDate <= to)
            .ToDictionaryAsync(x => (x.MealDate, x.MealTypeId));

        var days = new List<MyMealDayDto>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var items = new List<MyMealItemDto>();
            foreach (var type in cal.MealTypes)
            {
                var served = cal.IsServed(date, type);
                // Occasional meal types are only listed on dates they are scheduled.
                if (!type.IsRegular && !served) continue;

                entries.TryGetValue((date, type.MealTypeId), out var entry);
                var eligible = type.IsRegular && MealCalendar.IsEligible(employee, date, type.MealTypeId);
                var isOn = served && (entry?.IsOn ?? cal.DefaultIsOn(employee, date, type));
                var editable = served && employee.Status == EmployeeStatus.Active && cal.IsEditableByEmployee(date);
                var canTurnOff = cal.Settings.AllowEligibleOptOut || !eligible;
                var cost = cal.Cost(date, type.MealTypeId);

                items.Add(new MyMealItemDto(
                    type.MealTypeId, type.Name, type.IsRegular, served, cal.NotServedReason(date, type),
                    isOn, eligible, editable, editable && (isOn ? canTurnOff : true),
                    entry?.Source ?? (isOn ? EntrySource.Auto : null),
                    cal.LockTime(date), cal.Occasion(date, type.MealTypeId)?.Title,
                    cost.Employee, cost.Total));
            }
            days.Add(new MyMealDayDto(date, date.DayOfWeek.ToString(), date == cal.Today, cal.HolidayName(date), items));
        }

        return new MyMealsDto(cal.Today, cal.Settings.BookingWindowDays, cal.Settings.CutoffTime.ToString("HH:mm"),
            cal.Settings.AllowEligibleOptOut, days);
    }

    public async Task SetMyMealAsync(int employeeId, SetMealRequest req)
    {
        var cal = await _calendars.CreateAsync(req.MealDate, req.MealDate);
        var employee = await LoadEmployeeAsync(employeeId);
        if (employee.Status != EmployeeStatus.Active) throw new AppException("Your account is inactive.");

        var type = cal.Type(req.MealTypeId) ?? throw AppException.NotFound("Meal type");
        if (!cal.IsServed(req.MealDate, type))
            throw new AppException($"{type.Name} is not served on {req.MealDate:dd MMM yyyy} ({cal.NotServedReason(req.MealDate, type)}).");

        if (req.MealDate <= cal.Today)
            throw new AppException("Meals for today and past days can no longer be changed.");
        if (req.MealDate > cal.LastBookableDate)
            throw new AppException($"You can only change meals for the next {cal.Settings.BookingWindowDays} days.");
        if (!cal.IsEditableByEmployee(req.MealDate))
            throw new AppException($"Changes for {req.MealDate:dd MMM} closed at {cal.LockTime(req.MealDate):dd MMM, h:mm tt}.");

        if (!req.IsOn && !cal.Settings.AllowEligibleOptOut && MealCalendar.IsEligible(employee, req.MealDate, type.MealTypeId))
            throw new AppException("Your meal is provided by default and cannot be turned off. Please contact the admin.");

        await UpsertAsync(employeeId, req.MealDate, type.MealTypeId, req.IsOn, EntrySource.Employee, employeeId);
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------
    // Employee: history with costs
    // ------------------------------------------------------------------
    public async Task<MealHistoryDto> GetHistoryAsync(int employeeId, DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) throw new AppException("Please choose a period of one year or less.");

        var cal = await _calendars.CreateAsync(from, to, includeInactiveTypes: true);
        var rows = await _db.MealEntries.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.IsOn && x.MealDate >= from && x.MealDate <= to)
            .OrderBy(x => x.MealDate).ThenBy(x => x.MealTypeId)
            .Select(x => new { x.MealDate, x.MealTypeId, x.Source })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var c = cal.Cost(r.MealDate, r.MealTypeId);
            return new MealHistoryItemDto(r.MealDate, r.MealTypeId, cal.Type(r.MealTypeId)?.Name ?? "", r.Source,
                c.Total, c.Employee, c.Company);
        }).ToList();

        return new MealHistoryDto(from, to, items.Count,
            items.Sum(x => x.TotalCost), items.Sum(x => x.EmployeeCost), items.Sum(x => x.CompanyCost), items);
    }

    // ------------------------------------------------------------------
    // Admin: meal board for one date
    // ------------------------------------------------------------------
    public async Task<MealBoardDto> GetBoardAsync(DateOnly date, int mealTypeId, int? departmentId, string? search)
    {
        var cal = await _calendars.CreateAsync(date, date);
        var type = cal.Type(mealTypeId) ?? throw AppException.NotFound("Meal type");

        var q = _db.Employees.AsNoTracking()
            .Include(x => x.Department).Include(x => x.Designation).Include(x => x.Eligibilities)
            .Where(x => x.Status == EmployeeStatus.Active);
        if (departmentId.HasValue) q = q.Where(x => x.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.EmployeeName.Contains(s) || x.EmployeeCode.Contains(s) || x.MobileNo.Contains(s));
        }
        var employees = await q.OrderBy(x => x.EmployeeCode).ToListAsync();

        var entries = await _db.MealEntries.AsNoTracking()
            .Where(x => x.MealDate == date && x.MealTypeId == mealTypeId)
            .ToDictionaryAsync(x => x.EmployeeId);

        var served = cal.IsServed(date, type);
        var rows = employees.Select(e =>
        {
            entries.TryGetValue(e.EmployeeId, out var entry);
            var isOn = served && (entry?.IsOn ?? cal.DefaultIsOn(e, date, type));
            return new MealBoardRowDto(e.EmployeeId, e.EmployeeCode, e.EmployeeName,
                e.Department?.Name ?? "", e.Designation?.Name ?? "", e.MobileNo,
                MealCalendar.IsEligible(e, date, mealTypeId), isOn,
                entry?.Source ?? (isOn ? EntrySource.Auto : null), entry?.UpdatedAt ?? entry?.CreatedAt);
        }).ToList();

        var cost = cal.Cost(date, mealTypeId);
        var onCount = rows.Count(r => r.IsOn);
        return new MealBoardDto(date, mealTypeId, type.Name, served, cal.NotServedReason(date, type),
            cal.IsEditableByEmployee(date), cal.LockTime(date),
            onCount, rows.Count - onCount, cost.Total, cost.Employee, cost.Total * onCount, rows);
    }

    public async Task<int> AdminSetAsync(AdminSetMealRequest req, int adminId)
    {
        if (req.EmployeeIds.Count == 0) throw new AppException("Select at least one employee.");
        var cal = await _calendars.CreateAsync(req.MealDate, req.MealDate);
        var type = cal.Type(req.MealTypeId) ?? throw AppException.NotFound("Meal type");
        if (!cal.IsServed(req.MealDate, type))
            throw new AppException($"{type.Name} is not served on {req.MealDate:dd MMM yyyy} ({cal.NotServedReason(req.MealDate, type)}).");

        var activeIds = await _db.Employees.Where(x => req.EmployeeIds.Contains(x.EmployeeId) && x.Status == EmployeeStatus.Active)
            .Select(x => x.EmployeeId).ToListAsync();
        foreach (var id in activeIds)
            await UpsertAsync(id, req.MealDate, type.MealTypeId, req.IsOn, EntrySource.Admin, adminId);
        await _db.SaveChangesAsync();
        return activeIds.Count;
    }

    // ------------------------------------------------------------------
    // Counts for dashboard (resolved: saved entry, else default)
    // ------------------------------------------------------------------
    public async Task<List<MealCountDto>> GetCountsAsync(DateOnly date)
    {
        var cal = await _calendars.CreateAsync(date, date);
        var employees = await _db.Employees.AsNoTracking().Include(x => x.Eligibilities)
            .Where(x => x.Status == EmployeeStatus.Active).ToListAsync();
        var entries = await _db.MealEntries.AsNoTracking().Where(x => x.MealDate == date)
            .ToDictionaryAsync(x => (x.EmployeeId, x.MealTypeId));

        var result = new List<MealCountDto>();
        foreach (var type in cal.MealTypes)
        {
            var served = cal.IsServed(date, type);
            if (!type.IsRegular && !served) continue;
            var on = !served ? 0 : employees.Count(e =>
                entries.TryGetValue((e.EmployeeId, type.MealTypeId), out var en) ? en.IsOn : cal.DefaultIsOn(e, date, type));
            var cost = cal.Cost(date, type.MealTypeId);
            result.Add(new MealCountDto(date, type.MealTypeId, type.Name, served, cal.NotServedReason(date, type),
                on, cost.Total, cost.Employee, cost.Total * on, cost.Employee * on, cost.Company * on,
                cal.Occasion(date, type.MealTypeId)?.Title));
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Shared
    // ------------------------------------------------------------------
    private async Task<Employee> LoadEmployeeAsync(int employeeId) =>
        await _db.Employees.AsNoTracking().Include(x => x.Eligibilities)
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId)
        ?? throw AppException.NotFound("Employee");

    /// <summary>Insert or update one entry and write the audit log. Caller saves.</summary>
    private async Task UpsertAsync(int employeeId, DateOnly date, int mealTypeId, bool isOn, string source, int changedBy)
    {
        var entry = await _db.MealEntries
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.MealDate == date && x.MealTypeId == mealTypeId);
        bool? old = entry?.IsOn;
        var now = DateTime.UtcNow;

        if (entry == null)
        {
            _db.MealEntries.Add(new MealEntry
            {
                EmployeeId = employeeId, MealDate = date, MealTypeId = mealTypeId,
                IsOn = isOn, Source = source, CreatedAt = now, UpdatedAt = now, UpdatedBy = changedBy
            });
        }
        else
        {
            if (entry.IsOn == isOn && entry.Source == source) return;
            entry.IsOn = isOn;
            entry.Source = source;
            entry.UpdatedAt = now;
            entry.UpdatedBy = changedBy;
        }

        _db.MealEntryLogs.Add(new MealEntryLog
        {
            EmployeeId = employeeId, MealDate = date, MealTypeId = mealTypeId,
            OldIsOn = old, NewIsOn = isOn, Source = source, ChangedBy = changedBy, ChangedAt = now
        });
    }
}
