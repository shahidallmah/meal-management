using MealManagement.Api.Data;
using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using MealManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly MealDbContext _db;
    private readonly SettingsService _settings;
    private readonly MealGenerationService _generation;

    public EmployeesController(MealDbContext db, SettingsService settings, MealGenerationService generation)
    {
        _db = db;
        _settings = settings;
        _generation = generation;
    }

    [HttpGet]
    public async Task<PagedResult<EmployeeListItemDto>> List(
        [FromQuery] string? search, [FromQuery] int? departmentId, [FromQuery] string? status,
        [FromQuery] bool? eligible, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var today = (await _settings.GetAsync()).Today;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 500);

        var q = _db.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.EmployeeName.Contains(s) || x.EmployeeCode.Contains(s) || x.MobileNo.Contains(s));
        }
        if (departmentId.HasValue) q = q.Where(x => x.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        if (eligible.HasValue)
            q = q.Where(x => x.Eligibilities.Any(e => e.EffectiveDate <= today && (e.ExpireDate == null || e.ExpireDate >= today)) == eligible.Value);

        var total = await q.CountAsync();
        // Order/page on the entity before projecting (EF cannot translate members of constructor-projected records).
        var items = await Project(q.OrderBy(x => x.EmployeeCode).Skip((page - 1) * pageSize).Take(pageSize), today)
            .ToListAsync();
        return new PagedResult<EmployeeListItemDto>(items, total, page, pageSize);
    }

    [HttpGet("{id:int}")]
    public async Task<EmployeeDetailDto> Get(int id)
    {
        var today = (await _settings.GetAsync()).Today;
        var emp = await Project(_db.Employees.AsNoTracking().Where(x => x.EmployeeId == id), today).FirstOrDefaultAsync()
                  ?? throw AppException.NotFound("Employee");
        var elig = await _db.MealEligibilities.AsNoTracking().Where(x => x.EmployeeId == id)
            .OrderByDescending(x => x.EffectiveDate)
            .Select(x => new EligibilityDto(x.MealEligibilityId, x.MealTypeId, x.MealType!.Name, x.EffectiveDate, x.ExpireDate,
                x.Remarks, x.EffectiveDate <= today && (x.ExpireDate == null || x.ExpireDate >= today)))
            .ToListAsync();
        return new EmployeeDetailDto(emp, elig);
    }

    [HttpPost]
    public async Task<EmployeeDetailDto> Create(EmployeeSaveRequest req)
    {
        var adminId = User.GetEmployeeId();
        var emp = new Employee { CreatedAt = DateTime.UtcNow, CreatedBy = adminId };
        await ApplyAsync(emp, req);
        _db.Employees.Add(emp);

        if (req.DefaultEligible)
        {
            var regular = await _db.MealTypes.Where(x => x.IsRegular && x.IsActive).OrderBy(x => x.SortOrder).FirstOrDefaultAsync()
                          ?? throw new AppException("No active regular meal type (e.g. Lunch) is configured.");
            var from = req.EligibleFrom ?? (await _settings.GetAsync()).Today.AddDays(1);
            if (req.EligibleTo.HasValue && req.EligibleTo < from) throw new AppException("Expire date must be on or after the effective date.");
            emp.Eligibilities.Add(new MealEligibility
            {
                MealTypeId = regular.MealTypeId, EffectiveDate = from, ExpireDate = req.EligibleTo,
                CreatedAt = DateTime.UtcNow, CreatedBy = adminId
            });
        }

        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync(emp.EmployeeId);
        return await Get(emp.EmployeeId);
    }

    [HttpPut("{id:int}")]
    public async Task<EmployeeDetailDto> Update(int id, EmployeeSaveRequest req)
    {
        var emp = await _db.Employees.FindAsync(id) ?? throw AppException.NotFound("Employee");
        if (emp.EmployeeId == User.GetEmployeeId() && (req.Role != Roles.Admin || req.Status != EmployeeStatus.Active))
            throw new AppException("You cannot remove your own admin access or deactivate yourself.");
        await ApplyAsync(emp, req);
        emp.UpdatedAt = DateTime.UtcNow;
        emp.UpdatedBy = User.GetEmployeeId();
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync(id);
        return await Get(id);
    }

    /// <summary>Clears the password so the employee can create a new one with mobile no + employee code.</summary>
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var emp = await _db.Employees.FindAsync(id) ?? throw AppException.NotFound("Employee");
        emp.PasswordHash = null;
        emp.PasswordSetAt = null;
        emp.UpdatedAt = DateTime.UtcNow;
        emp.UpdatedBy = User.GetEmployeeId();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------------- eligibility ----------------
    [HttpPost("{id:int}/eligibilities")]
    public async Task<EmployeeDetailDto> AddEligibility(int id, EligibilitySaveRequest req)
    {
        if (!await _db.Employees.AnyAsync(x => x.EmployeeId == id)) throw AppException.NotFound("Employee");
        await ValidateEligibilityAsync(id, req, null);
        _db.MealEligibilities.Add(new MealEligibility
        {
            EmployeeId = id, MealTypeId = req.MealTypeId, EffectiveDate = req.EffectiveDate, ExpireDate = req.ExpireDate,
            Remarks = req.Remarks, CreatedAt = DateTime.UtcNow, CreatedBy = User.GetEmployeeId()
        });
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync(id);
        return await Get(id);
    }

    [HttpPut("eligibilities/{eligibilityId:int}")]
    public async Task<EmployeeDetailDto> UpdateEligibility(int eligibilityId, EligibilitySaveRequest req)
    {
        var row = await _db.MealEligibilities.FindAsync(eligibilityId) ?? throw AppException.NotFound("Eligibility");
        await ValidateEligibilityAsync(row.EmployeeId, req, eligibilityId);
        row.MealTypeId = req.MealTypeId;
        row.EffectiveDate = req.EffectiveDate;
        row.ExpireDate = req.ExpireDate;
        row.Remarks = req.Remarks;
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync(row.EmployeeId);
        return await Get(row.EmployeeId);
    }

    [HttpDelete("eligibilities/{eligibilityId:int}")]
    public async Task<EmployeeDetailDto> DeleteEligibility(int eligibilityId)
    {
        var row = await _db.MealEligibilities.FindAsync(eligibilityId) ?? throw AppException.NotFound("Eligibility");
        var empId = row.EmployeeId;
        _db.MealEligibilities.Remove(row);
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync(empId);
        return await Get(empId);
    }

    /// <summary>Give the same eligibility period to many employees at once.</summary>
    [HttpPost("eligibilities/bulk")]
    public async Task<object> BulkEligibility(BulkEligibilityRequest req)
    {
        var ids = await _db.Employees.Where(x => req.EmployeeIds.Contains(x.EmployeeId)).Select(x => x.EmployeeId).ToListAsync();
        var added = 0;
        var skipped = new List<int>();
        foreach (var id in ids)
        {
            try { await ValidateEligibilityAsync(id, req, null); }
            catch (AppException) { skipped.Add(id); continue; }
            _db.MealEligibilities.Add(new MealEligibility
            {
                EmployeeId = id, MealTypeId = req.MealTypeId, EffectiveDate = req.EffectiveDate, ExpireDate = req.ExpireDate,
                Remarks = req.Remarks, CreatedAt = DateTime.UtcNow, CreatedBy = User.GetEmployeeId()
            });
            added++;
        }
        await _db.SaveChangesAsync();
        await _generation.RefreshFutureAsync();
        return new { added, skipped = skipped.Count };
    }

    // ---------------- helpers ----------------
    private static IQueryable<EmployeeListItemDto> Project(IQueryable<Employee> q, DateOnly today) =>
        q.Select(x => new EmployeeListItemDto(x.EmployeeId, x.EmployeeCode, x.EmployeeName,
            x.DepartmentId, x.Department!.Name, x.DesignationId, x.Designation!.Name,
            x.MobileNo, x.Email, x.Status, x.Role, x.PasswordHash != null,
            x.Eligibilities.Any(e => e.EffectiveDate <= today && (e.ExpireDate == null || e.ExpireDate >= today)),
            x.LastLoginAt));

    private async Task ApplyAsync(Employee emp, EmployeeSaveRequest req)
    {
        var mobile = MobileNumber.Normalize(req.MobileNo);
        if (!MobileNumber.IsValid(mobile)) throw new AppException("Please enter a valid mobile number.");
        if (req.Status != EmployeeStatus.Active && req.Status != EmployeeStatus.Inactive) throw new AppException("Status must be Active or InActive.");
        if (req.Role != Roles.Admin && req.Role != Roles.Employee) throw new AppException("Role must be Employee or Admin.");

        var code = req.EmployeeCode.Trim();
        if (await _db.Employees.AnyAsync(x => x.EmployeeCode == code && x.EmployeeId != emp.EmployeeId))
            throw new AppException($"Employee code {code} is already registered.");
        if (await _db.Employees.AnyAsync(x => x.MobileNo == mobile && x.EmployeeId != emp.EmployeeId))
            throw new AppException($"Mobile number {mobile} is already registered to another employee.");
        if (!await _db.Departments.AnyAsync(x => x.DepartmentId == req.DepartmentId)) throw new AppException("Please choose a department.");
        if (!await _db.Designations.AnyAsync(x => x.DesignationId == req.DesignationId)) throw new AppException("Please choose a designation.");

        emp.EmployeeCode = code;
        emp.EmployeeName = req.EmployeeName.Trim();
        emp.DepartmentId = req.DepartmentId;
        emp.DesignationId = req.DesignationId;
        emp.MobileNo = mobile;
        emp.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        emp.Status = req.Status;
        emp.Role = req.Role;
    }

    private async Task ValidateEligibilityAsync(int employeeId, EligibilitySaveRequest req, int? excludeId)
    {
        if (req.ExpireDate.HasValue && req.ExpireDate < req.EffectiveDate)
            throw new AppException("Expire date must be on or after the effective date.");
        var type = await _db.MealTypes.FindAsync(req.MealTypeId) ?? throw AppException.NotFound("Meal type");
        if (!type.IsRegular) throw new AppException($"{type.Name} is an occasional meal. Default eligibility applies to regular meals only.");

        var newEnd = req.ExpireDate ?? DateOnly.MaxValue;
        var overlaps = await _db.MealEligibilities.AnyAsync(x =>
            x.EmployeeId == employeeId && x.MealTypeId == req.MealTypeId && x.MealEligibilityId != (excludeId ?? 0) &&
            x.EffectiveDate <= newEnd && (x.ExpireDate == null || x.ExpireDate >= req.EffectiveDate));
        if (overlaps) throw new AppException("This period overlaps an existing eligibility period for the same meal.");
    }
}
