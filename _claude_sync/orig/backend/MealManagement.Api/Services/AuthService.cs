using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MealManagement.Api.Data;
using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MealManagement.Api.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "MealManagement";
    public string Audience { get; set; } = "MealManagement";
    public string Key { get; set; } = "";
    public int ExpiryDays { get; set; } = 7;
}

public class SeedAdminOptions
{
    public string EmployeeCode { get; set; } = "ADMIN";
    public string EmployeeName { get; set; } = "System Admin";
    public string MobileNo { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AuthService
{
    private readonly MealDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly IPasswordHasher<Employee> _hasher;

    public AuthService(MealDbContext db, IOptions<JwtOptions> jwt, IPasswordHasher<Employee> hasher)
    {
        _db = db;
        _jwt = jwt.Value;
        _hasher = hasher;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var mobile = MobileNumber.Normalize(req.MobileNo);
        var emp = await _db.Employees.Include(x => x.Department).Include(x => x.Designation)
            .FirstOrDefaultAsync(x => x.MobileNo == mobile);

        const string invalid = "Mobile number or password is incorrect.";
        if (emp == null) throw new AppException(invalid, StatusCodes.Status401Unauthorized);
        if (emp.PasswordHash == null)
            throw new AppException("You have not created a password yet. Tap 'Create password' to set one.", StatusCodes.Status401Unauthorized);

        var result = _hasher.VerifyHashedPassword(emp, emp.PasswordHash, req.Password);
        if (result == PasswordVerificationResult.Failed) throw new AppException(invalid, StatusCodes.Status401Unauthorized);
        if (emp.Status != EmployeeStatus.Active)
            throw new AppException("Your account is inactive. Please contact the admin.", StatusCodes.Status403Forbidden);

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            emp.PasswordHash = _hasher.HashPassword(emp, req.Password);
        emp.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return BuildResponse(emp);
    }

    public async Task<CheckMobileResponse> CheckMobileAsync(string mobileNo)
    {
        var mobile = MobileNumber.Normalize(mobileNo);
        var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.MobileNo == mobile);
        return emp == null
            ? new CheckMobileResponse(false, false, false)
            : new CheckMobileResponse(true, emp.PasswordHash != null, emp.Status == EmployeeStatus.Active);
    }

    /// <summary>
    /// First-time password creation. The employee proves identity with mobile number + employee code
    /// (both registered by the admin). Only allowed while no password exists (or after an admin reset).
    /// </summary>
    public async Task<AuthResponse> CreatePasswordAsync(CreatePasswordRequest req)
    {
        var mobile = MobileNumber.Normalize(req.MobileNo);
        var emp = await _db.Employees.Include(x => x.Department).Include(x => x.Designation)
            .FirstOrDefaultAsync(x => x.MobileNo == mobile);

        if (emp == null || !string.Equals(emp.EmployeeCode.Trim(), req.EmployeeCode.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new AppException("Mobile number and employee code do not match our records. Please contact the admin.");
        if (emp.Status != EmployeeStatus.Active)
            throw new AppException("Your account is inactive. Please contact the admin.");
        if (emp.PasswordHash != null)
            throw new AppException("A password already exists for this account. Please sign in, or ask the admin to reset it.");

        emp.PasswordHash = _hasher.HashPassword(emp, req.Password);
        emp.PasswordSetAt = DateTime.UtcNow;
        emp.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return BuildResponse(emp);
    }

    public async Task ChangePasswordAsync(int employeeId, ChangePasswordRequest req)
    {
        var emp = await _db.Employees.FindAsync(employeeId) ?? throw AppException.NotFound("Employee");
        if (emp.PasswordHash == null || _hasher.VerifyHashedPassword(emp, emp.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new AppException("Current password is incorrect.");
        emp.PasswordHash = _hasher.HashPassword(emp, req.NewPassword);
        emp.PasswordSetAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<UserDto> GetUserAsync(int employeeId)
    {
        var emp = await _db.Employees.AsNoTracking().Include(x => x.Department).Include(x => x.Designation)
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId) ?? throw AppException.NotFound("Employee");
        return ToUser(emp);
    }

    public static UserDto ToUser(Employee e) => new(e.EmployeeId, e.EmployeeCode, e.EmployeeName, e.MobileNo, e.Role,
        e.Department?.Name ?? "", e.Designation?.Name ?? "", e.Email);

    private AuthResponse BuildResponse(Employee emp)
    {
        var expires = DateTime.UtcNow.AddDays(_jwt.ExpiryDays);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, emp.EmployeeId.ToString()),
            new(ClaimTypes.Name, emp.EmployeeName),
            new(ClaimTypes.Role, emp.Role),
            new("code", emp.EmployeeCode),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, ToUser(emp));
    }

    /// <summary>Creates the first Admin from configuration if no admin exists yet.</summary>
    public static async Task EnsureSeedAdminAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealDbContext>();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AuthService>>();

        if (await db.Employees.AnyAsync(x => x.Role == Roles.Admin)) return;
        var mobile = MobileNumber.Normalize(opts.MobileNo);
        if (mobile.Length == 0 || string.IsNullOrWhiteSpace(opts.Password))
        {
            logger.LogWarning("No admin exists and SeedAdmin is not configured in appsettings.json.");
            return;
        }

        var existing = await db.Employees.FirstOrDefaultAsync(x => x.MobileNo == mobile);
        if (existing != null)
        {
            existing.Role = Roles.Admin;
            existing.PasswordHash ??= hasher.HashPassword(existing, opts.Password);
            await db.SaveChangesAsync();
            return;
        }

        var dept = await db.Departments.OrderBy(x => x.DepartmentId).FirstOrDefaultAsync();
        var desig = await db.Designations.OrderBy(x => x.DesignationId).FirstOrDefaultAsync();
        if (dept == null || desig == null)
        {
            logger.LogWarning("Cannot create seed admin: run database/02_Seed.sql first (needs a department and designation).");
            return;
        }

        var admin = new Employee
        {
            EmployeeCode = opts.EmployeeCode, EmployeeName = opts.EmployeeName, MobileNo = mobile,
            DepartmentId = dept.DepartmentId, DesignationId = desig.DesignationId,
            Status = EmployeeStatus.Active, Role = Roles.Admin, CreatedAt = DateTime.UtcNow, PasswordSetAt = DateTime.UtcNow
        };
        admin.PasswordHash = hasher.HashPassword(admin, opts.Password);
        db.Employees.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Seed admin created with mobile {Mobile}. Change the password after first login.", mobile);
    }
}
