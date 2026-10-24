using MealManagement.Api.Data;
using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using MealManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Controllers;

// ---------------------------------------------------------------------
// Departments & designations
// ---------------------------------------------------------------------
[ApiController]
[Authorize]
[Route("api/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly MealDbContext _db;
    public DepartmentsController(MealDbContext db) => _db = db;

    [HttpGet]
    public Task<List<NamedItemDto>> List() =>
        _db.Departments.AsNoTracking().OrderBy(x => x.Name).Select(x => new NamedItemDto(x.DepartmentId, x.Name, x.IsActive)).ToListAsync();

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<NamedItemDto> Create(NamedItemSaveRequest req)
    {
        var row = new Department { Name = req.Name.Trim(), IsActive = req.IsActive };
        _db.Departments.Add(row);
        await _db.SaveChangesAsync();
        return new NamedItemDto(row.DepartmentId, row.Name, row.IsActive);
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<NamedItemDto> Update(int id, NamedItemSaveRequest req)
    {
        var row = await _db.Departments.FindAsync(id) ?? throw AppException.NotFound("Department");
        row.Name = req.Name.Trim();
        row.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        return new NamedItemDto(row.DepartmentId, row.Name, row.IsActive);
    }
}

[ApiController]
[Authorize]
[Route("api/designations")]
public class DesignationsController : ControllerBase
{
    private readonly MealDbContext _db;
    public DesignationsController(MealDbContext db) => _db = db;

    [HttpGet]
    public Task<List<NamedItemDto>> List() =>
        _db.Designations.AsNoTracking().OrderBy(x => x.Name).Select(x => new NamedItemDto(x.DesignationId, x.Name, x.IsActive)).ToListAsync();

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<NamedItemDto> Create(NamedItemSaveRequest req)
    {
        var row = new Designation { Name = req.Name.Trim(), IsActive = req.IsActive };
        _db.Designations.Add(row);
        await _db.SaveChangesAsync();
        return new NamedItemDto(row.DesignationId, row.Name, row.IsActive);
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<NamedItemDto> Update(int id, NamedItemSaveRequest req)
    {
        var row = await _db.Designations.FindAsync(id) ?? throw AppException.NotFound("Designation");
        row.Name = req.Name.Trim();
        row.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        return new NamedItemDto(row.DesignationId, row.Name, row.IsActive);
    }
}

// ---------------------------------------------------------------------
// Meal types
// ---------------------------------------------------------------------
[ApiController]
[Authorize]
[Route("api/meal-types")]
public class MealTypesController : ControllerBase
{
    private readonly MealDbContext _db;
    private readonly MealGenerationService _generation;

    public MealTypesController(MealDbContext db, MealGenerationService generation)
    {
        _db = db;
        _generation = generation;
    }

    [HttpGet]
    public Task<List<MealTypeDto>> List() =>
        _db.MealTypes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new MealTypeDto(x.MealTypeId, x.Code, x.Name, x.IsRegular, x.DefaultTotalCost, x.DefaultEmployeeCost, x.SortOrder, x.IsActive))
            .ToListAsync();

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<MealTypeDto> Create(MealTypeSaveRequest req)
    {
        var row = new MealType();
        Apply(row, req);
        _db.MealTypes.Add(row);
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return ToDto(row);
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<MealTypeDto> Update(int id, MealTypeSaveRequest req)
    {
        var row = await _db.MealTypes.FindAsync(id) ?? throw AppException.NotFound("Meal type");
        Apply(row, req);
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return ToDto(row);
    }

    private static void Apply(MealType row, MealTypeSaveRequest req)
    {
        if (req.DefaultEmployeeCost > req.DefaultTotalCost)
            throw new AppException("Employee portion cannot be more than the total meal cost.");
        row.Code = req.Code.Trim().ToUpperInvariant();
        row.Name = req.Name.Trim();
        row.IsRegular = req.IsRegular;
        row.DefaultTotalCost = req.DefaultTotalCost;
        row.DefaultEmployeeCost = req.DefaultEmployeeCost;
        row.SortOrder = req.SortOrder;
        row.IsActive = req.IsActive;
    }

    private static MealTypeDto ToDto(MealType x) =>
        new(x.MealTypeId, x.Code, x.Name, x.IsRegular, x.DefaultTotalCost, x.DefaultEmployeeCost, x.SortOrder, x.IsActive);
}

// ---------------------------------------------------------------------
// Occasions (special meals on specific dates)
// ---------------------------------------------------------------------
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/occasions")]
public class OccasionsController : ControllerBase
{
    private readonly MealDbContext _db;
    private readonly MealGenerationService _generation;

    public OccasionsController(MealDbContext db, MealGenerationService generation)
    {
        _db = db;
        _generation = generation;
    }

    [HttpGet]
    public Task<List<OccasionDto>> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var q = _db.MealOccasions.AsNoTracking();
        if (from.HasValue) q = q.Where(x => x.OccasionDate >= from);
        if (to.HasValue) q = q.Where(x => x.OccasionDate <= to);
        return q.OrderByDescending(x => x.OccasionDate)
            .Select(x => new OccasionDto(x.MealOccasionId, x.MealTypeId, x.MealType!.Name, x.OccasionDate, x.Title, x.AutoOnForAll, x.IsActive))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> Create(OccasionSaveRequest req)
    {
        await ValidateAsync(req, null);
        _db.MealOccasions.Add(new MealOccasion
        {
            MealTypeId = req.MealTypeId, OccasionDate = req.OccasionDate, Title = req.Title.Trim(),
            AutoOnForAll = req.AutoOnForAll, IsActive = req.IsActive, CreatedAt = DateTime.UtcNow, CreatedBy = User.GetEmployeeId()
        });
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return NoContent();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, OccasionSaveRequest req)
    {
        var row = await _db.MealOccasions.FindAsync(id) ?? throw AppException.NotFound("Occasion");
        await ValidateAsync(req, id);
        row.MealTypeId = req.MealTypeId;
        row.OccasionDate = req.OccasionDate;
        row.Title = req.Title.Trim();
        row.AutoOnForAll = req.AutoOnForAll;
        row.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.MealOccasions.FindAsync(id) ?? throw AppException.NotFound("Occasion");
        if (await _db.MealEntries.AnyAsync(x => x.MealDate == row.OccasionDate && x.MealTypeId == row.MealTypeId && x.MealDate <= DateOnly.FromDateTime(DateTime.UtcNow)))
            throw new AppException("This occasion already has meals recorded. Mark it inactive instead of deleting.");
        _db.MealOccasions.Remove(row);
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return NoContent();
    }

    private async Task ValidateAsync(OccasionSaveRequest req, int? id)
    {
        var type = await _db.MealTypes.FindAsync(req.MealTypeId) ?? throw AppException.NotFound("Meal type");
        if (type.IsRegular) throw new AppException($"{type.Name} is a regular meal and is served every working day. Choose an occasional meal type.");
        if (await _db.MealOccasions.AnyAsync(x => x.MealTypeId == req.MealTypeId && x.OccasionDate == req.OccasionDate && x.MealOccasionId != (id ?? 0)))
            throw new AppException($"{type.Name} is already scheduled on {req.OccasionDate:dd MMM yyyy}.");
    }
}

// ---------------------------------------------------------------------
// Holidays
// ---------------------------------------------------------------------
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/holidays")]
public class HolidaysController : ControllerBase
{
    private readonly MealDbContext _db;
    private readonly MealGenerationService _generation;

    public HolidaysController(MealDbContext db, MealGenerationService generation)
    {
        _db = db;
        _generation = generation;
    }

    [HttpGet]
    public async Task<List<HolidayDto>> List([FromQuery] int? year)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var rows = await _db.Holidays.AsNoTracking()
            .Where(x => x.HolidayDate >= new DateOnly(y, 1, 1) && x.HolidayDate <= new DateOnly(y, 12, 31))
            .OrderBy(x => x.HolidayDate).ToListAsync();
        return rows.Select(x => new HolidayDto(x.HolidayId, x.HolidayDate, x.HolidayDate.DayOfWeek.ToString(), x.Description)).ToList();
    }

    [HttpPost]
    public async Task<IActionResult> Create(HolidaySaveRequest req)
    {
        if (await _db.Holidays.AnyAsync(x => x.HolidayDate == req.HolidayDate))
            throw new AppException($"{req.HolidayDate:dd MMM yyyy} is already a holiday.");
        _db.Holidays.Add(new Holiday { HolidayDate = req.HolidayDate, Description = req.Description.Trim() });
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return NoContent();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, HolidaySaveRequest req)
    {
        var row = await _db.Holidays.FindAsync(id) ?? throw AppException.NotFound("Holiday");
        if (await _db.Holidays.AnyAsync(x => x.HolidayDate == req.HolidayDate && x.HolidayId != id))
            throw new AppException($"{req.HolidayDate:dd MMM yyyy} is already a holiday.");
        row.HolidayDate = req.HolidayDate;
        row.Description = req.Description.Trim();
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.Holidays.FindAsync(id) ?? throw AppException.NotFound("Holiday");
        _db.Holidays.Remove(row);
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return NoContent();
    }
}

// ---------------------------------------------------------------------
// Settings
// ---------------------------------------------------------------------
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settings;
    private readonly MealGenerationService _generation;

    public SettingsController(SettingsService settings, MealGenerationService generation)
    {
        _settings = settings;
        _generation = generation;
    }

    [HttpGet]
    public async Task<List<SettingDto>> Get() =>
        (await _settings.GetAllAsync()).Select(x => new SettingDto(x.SettingKey, x.SettingValue, x.Description)).ToList();

    [HttpPut]
    public async Task<List<SettingDto>> Update(Dictionary<string, string> values)
    {
        await _settings.UpdateAsync(values);
        await _generation.RefreshFutureAsync();
        return await Get();
    }
}
