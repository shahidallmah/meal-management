using MealManagement.Api.Data;
using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using MealManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Controllers;

// ---------------------------------------------------------------------
// Meal board: who eats on a date; admin can override ON/OFF
// ---------------------------------------------------------------------
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/meal-board")]
public class MealBoardController : ControllerBase
{
    private readonly MealService _meals;
    public MealBoardController(MealService meals) => _meals = meals;

    [HttpGet]
    public Task<MealBoardDto> Get([FromQuery] DateOnly date, [FromQuery] int mealTypeId, [FromQuery] int? departmentId, [FromQuery] string? search)
        => _meals.GetBoardAsync(date, mealTypeId, departmentId, search);

    [HttpPost("set")]
    public async Task<object> Set(AdminSetMealRequest req)
    {
        var updated = await _meals.AdminSetAsync(req, User.GetEmployeeId());
        return new { updated };
    }
}

// ---------------------------------------------------------------------
// Daily costs
// ---------------------------------------------------------------------
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/costs")]
public class CostsController : ControllerBase
{
    private readonly MealDbContext _db;
    private readonly MealCalendarFactory _calendars;

    public CostsController(MealDbContext db, MealCalendarFactory calendars)
    {
        _db = db;
        _calendars = calendars;
    }

    /// <summary>One row per date and meal type in the range (served days, plus any day that already has a cost).</summary>
    [HttpGet]
    public async Task<List<DailyCostDto>> List([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int? mealTypeId)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 92) throw new AppException("Please choose up to 3 months at a time.");

        var cal = await _calendars.CreateAsync(from, to, includeInactiveTypes: true);
        var saved = await _db.MealDailyCosts.AsNoTracking().Where(x => x.MealDate >= from && x.MealDate <= to)
            .ToDictionaryAsync(x => (x.MealDate, x.MealTypeId));
        var counts = await _db.MealEntries.AsNoTracking().Where(x => x.IsOn && x.MealDate >= from && x.MealDate <= to)
            .GroupBy(x => new { x.MealDate, x.MealTypeId }).Select(g => new { g.Key.MealDate, g.Key.MealTypeId, Count = g.Count() })
            .ToListAsync();
        var countMap = counts.ToDictionary(x => (x.MealDate, x.MealTypeId), x => x.Count);

        var result = new List<DailyCostDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            foreach (var type in cal.MealTypes)
            {
                if (mealTypeId.HasValue && type.MealTypeId != mealTypeId) continue;
                var served = cal.IsServed(d, type);
                saved.TryGetValue((d, type.MealTypeId), out var row);
                if (!served && row == null && (!type.IsRegular || !type.IsActive)) continue;

                var c = cal.Cost(d, type.MealTypeId);
                var n = countMap.GetValueOrDefault((d, type.MealTypeId));
                result.Add(new DailyCostDto(d, d.DayOfWeek.ToString(), type.MealTypeId, type.Name, served, cal.NotServedReason(d, type),
                    row?.MealDailyCostId, c.Total, c.Employee, c.Company, c.IsDefault, row?.Remarks,
                    n, c.Total * n, c.Employee * n, c.Company * n));
            }
        }
        return result;
    }

    [HttpPut]
    public async Task<IActionResult> Save(CostSaveRequest req)
    {
        if (req.EmployeeCost > req.TotalCost) throw new AppException("Employee portion cannot be more than the total meal cost.");
        if (!await _db.MealTypes.AnyAsync(x => x.MealTypeId == req.MealTypeId)) throw AppException.NotFound("Meal type");
        await UpsertAsync(req.MealDate, req.MealTypeId, req.TotalCost, req.EmployeeCost, req.Remarks, true);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Apply one cost to every day in a range (e.g. a whole month).</summary>
    [HttpPost("bulk")]
    public async Task<object> Bulk(CostBulkRequest req)
    {
        if (req.EmployeeCost > req.TotalCost) throw new AppException("Employee portion cannot be more than the total meal cost.");
        var (from, to) = req.To < req.From ? (req.To, req.From) : (req.From, req.To);
        if (to.DayNumber - from.DayNumber > 92) throw new AppException("Please choose up to 3 months at a time.");
        var cal = await _calendars.CreateAsync(from, to);
        var type = cal.Type(req.MealTypeId) ?? throw AppException.NotFound("Meal type");

        var saved = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (req.ServedDaysOnly && !cal.IsServed(d, type)) continue;
            if (await UpsertAsync(d, type.MealTypeId, req.TotalCost, req.EmployeeCost, req.Remarks, req.OverwriteExisting)) saved++;
        }
        await _db.SaveChangesAsync();
        return new { saved };
    }

    /// <summary>Remove a day's cost so the meal type's default cost applies again.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.MealDailyCosts.FindAsync(id) ?? throw AppException.NotFound("Cost");
        _db.MealDailyCosts.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> UpsertAsync(DateOnly date, int mealTypeId, decimal total, decimal employee, string? remarks, bool overwrite)
    {
        var row = await _db.MealDailyCosts.FirstOrDefaultAsync(x => x.MealDate == date && x.MealTypeId == mealTypeId);
        if (row != null && !overwrite) return false;
        if (row == null)
        {
            row = new MealDailyCost { MealDate = date, MealTypeId = mealTypeId };
            _db.MealDailyCosts.Add(row);
        }
        row.TotalCost = total;
        row.EmployeeCost = employee;
        row.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = User.GetEmployeeId();
        return true;
    }
}

// ---------------------------------------------------------------------
// Dashboard
// ---------------------------------------------------------------------
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly MealDbContext _db;
    private readonly MealService _meals;
    private readonly MealCalendarFactory _calendars;
    private readonly SettingsService _settings;

    public DashboardController(MealDbContext db, MealService meals, MealCalendarFactory calendars, SettingsService settings)
    {
        _db = db;
        _meals = meals;
        _calendars = calendars;
        _settings = settings;
    }

    [HttpGet]
    public async Task<DashboardDto> Get()
    {
        var today = (await _settings.GetAsync()).Today;
        var todayMeals = await _meals.GetCountsAsync(today);
        var tomorrowMeals = await _meals.GetCountsAsync(today.AddDays(1));

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var trendStart = today.AddDays(-13);
        var from = monthStart < trendStart ? monthStart : trendStart;
        var cal = await _calendars.CreateAsync(from, today, includeInactiveTypes: true);
        var entries = await _db.MealEntries.AsNoTracking().Where(x => x.IsOn && x.MealDate >= from && x.MealDate <= today)
            .GroupBy(x => new { x.MealDate, x.MealTypeId }).Select(g => new { g.Key.MealDate, g.Key.MealTypeId, Count = g.Count() })
            .ToListAsync();

        var withCost = entries.Select(e =>
        {
            var c = cal.Cost(e.MealDate, e.MealTypeId);
            return new { e.MealDate, e.Count, Total = c.Total * e.Count, Emp = c.Employee * e.Count, Co = c.Company * e.Count };
        }).ToList();

        var month = withCost.Where(x => x.MealDate >= monthStart).ToList();
        var monthDto = new MonthSummaryDto(monthStart, today, month.Sum(x => x.Count), month.Sum(x => x.Total),
            month.Sum(x => x.Emp), month.Sum(x => x.Co));

        var trend = new List<TrendPointDto>();
        for (var d = trendStart; d <= today; d = d.AddDays(1))
        {
            var day = withCost.Where(x => x.MealDate == d).ToList();
            trend.Add(new TrendPointDto(d, day.Sum(x => x.Count), day.Sum(x => x.Total)));
        }

        var stats = new EmployeeStatsDto(
            await _db.Employees.CountAsync(x => x.Status == EmployeeStatus.Active),
            await _db.Employees.CountAsync(x => x.Status == EmployeeStatus.Inactive),
            await _db.Employees.CountAsync(x => x.Status == EmployeeStatus.Active &&
                x.Eligibilities.Any(e => e.EffectiveDate <= today && (e.ExpireDate == null || e.ExpireDate >= today))),
            await _db.Employees.CountAsync(x => x.Status == EmployeeStatus.Active && x.PasswordHash == null));

        return new DashboardDto(today, todayMeals, tomorrowMeals, monthDto, stats, trend);
    }
}

// ---------------------------------------------------------------------
// Reports
// ---------------------------------------------------------------------
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _reports;
    private readonly SettingsService _settings;

    public ReportsController(ReportService reports, SettingsService settings)
    {
        _reports = reports;
        _settings = settings;
    }

    [HttpGet]
    public List<ReportDefinition> List() => ReportService.Definitions;

    [HttpGet("{code}")]
    public Task<ReportTable> Preview(string code, [FromQuery] ReportQuery q) => _reports.BuildAsync(code, q);

    [HttpGet("{code}/export")]
    public async Task<IActionResult> Export(string code, [FromQuery] ReportQuery q, [FromQuery] string format = "xlsx")
    {
        var table = await _reports.BuildAsync(code, q);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return File(ReportService.ToCsv(table), "text/csv", $"{code}-{stamp}.csv");

        var company = (await _settings.GetAsync()).CompanyName;
        return File(ReportService.ToExcel(table, company),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{code}-{stamp}.xlsx");
    }
}
