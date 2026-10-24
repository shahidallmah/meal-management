using MealManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Services;

/// <summary>
/// Materialises the automatic "meal ON" entries for default-eligible employees
/// (and for occasions marked AutoOnForAll) so that the kitchen list and reports
/// read straight from MealEntry.
/// </summary>
public class MealGenerationService
{
    private readonly MealDbContext _db;
    private readonly MealCalendarFactory _calendars;
    private readonly ILogger<MealGenerationService> _logger;

    public MealGenerationService(MealDbContext db, MealCalendarFactory calendars, ILogger<MealGenerationService> logger)
    {
        _db = db;
        _calendars = calendars;
        _logger = logger;
    }

    /// <summary>Insert missing default-ON entries between the dates. Existing entries are never touched.</summary>
    public async Task<int> GenerateAsync(DateOnly from, DateOnly to, int? employeeId = null)
    {
        if (to < from) return 0;
        var cal = await _calendars.CreateAsync(from, to);

        var empQuery = _db.Employees.AsNoTracking().Include(x => x.Eligibilities)
            .Where(x => x.Status == EmployeeStatus.Active);
        if (employeeId.HasValue) empQuery = empQuery.Where(x => x.EmployeeId == employeeId);
        var employees = await empQuery.ToListAsync();
        if (employees.Count == 0) return 0;

        var existingQuery = _db.MealEntries.AsNoTracking().Where(x => x.MealDate >= from && x.MealDate <= to);
        if (employeeId.HasValue) existingQuery = existingQuery.Where(x => x.EmployeeId == employeeId);
        var existing = (await existingQuery.Select(x => new { x.EmployeeId, x.MealDate, x.MealTypeId }).ToListAsync())
            .Select(x => (x.EmployeeId, x.MealDate, x.MealTypeId)).ToHashSet();

        var now = DateTime.UtcNow;
        var added = 0;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            foreach (var type in cal.MealTypes)
            {
                if (!cal.IsServed(date, type)) continue;
                foreach (var emp in employees)
                {
                    if (existing.Contains((emp.EmployeeId, date, type.MealTypeId))) continue;
                    if (!cal.DefaultIsOn(emp, date, type)) continue;
                    _db.MealEntries.Add(new MealEntry
                    {
                        EmployeeId = emp.EmployeeId, MealDate = date, MealTypeId = type.MealTypeId,
                        IsOn = true, Source = EntrySource.Auto, CreatedAt = now
                    });
                    added++;
                }
            }
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
            _logger.LogInformation("Generated {Count} automatic meal entries for {From}..{To}", added, from, to);
        }
        return added;
    }

    /// <summary>
    /// Re-apply rules to future, still-open dates after an admin change
    /// (eligibility, employee status, holiday, occasion, weekly off):
    ///  - removes entries on dates where the meal is no longer served or the employee is inactive,
    ///  - removes automatic entries that no longer match the default,
    ///  - then generates missing automatic entries.
    /// Today and past dates are never changed.
    /// </summary>
    public async Task RefreshFutureAsync(int? employeeId = null)
    {
        var window = await _calendars.CreateForWindowAsync();
        var from = window.Today.AddDays(1);
        var to = window.LastBookableDate;
        var cal = await _calendars.CreateAsync(from, to);

        var q = _db.MealEntries.Include(x => x.Employee!).ThenInclude(e => e.Eligibilities)
            .Where(x => x.MealDate >= from && x.MealDate <= to);
        if (employeeId.HasValue) q = q.Where(x => x.EmployeeId == employeeId);
        var entries = await q.ToListAsync();

        var types = await _db.MealTypes.AsNoTracking().ToDictionaryAsync(x => x.MealTypeId);
        var removed = 0;
        foreach (var entry in entries)
        {
            var type = types[entry.MealTypeId];
            var emp = entry.Employee!;
            var shouldRemove =
                emp.Status != EmployeeStatus.Active
                || !cal.IsServed(entry.MealDate, type)
                || (entry.Source == EntrySource.Auto && !cal.DefaultIsOn(emp, entry.MealDate, type));
            if (shouldRemove)
            {
                _db.MealEntries.Remove(entry);
                removed++;
            }
        }
        if (removed > 0) await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await GenerateAsync(from, to, employeeId);
    }
}

/// <summary>Runs generation at start-up and every 30 minutes.</summary>
public class MealGenerationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MealGenerationJob> _logger;

    public MealGenerationJob(IServiceScopeFactory scopes, ILogger<MealGenerationJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var settings = await scope.ServiceProvider.GetRequiredService<SettingsService>().GetAsync();
                var gen = scope.ServiceProvider.GetRequiredService<MealGenerationService>();
                var today = settings.Today;
                // Back-fill a week in case the server was down; then cover the booking window.
                await gen.GenerateAsync(today.AddDays(-7), today.AddDays(settings.BookingWindowDays));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Meal generation job failed");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
