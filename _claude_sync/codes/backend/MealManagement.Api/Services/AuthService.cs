using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using MealManagement.Api.Data;
using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    /// <summary>JWT claim holding the password version; tokens issued before a password change stop working.</summary>
    public const string PasswordStampClaim = "pst";
    public const int MaxCodeAttempts = 5;
    // No 0/O, 1/I/L so codes are easy to read out over the phone.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly MealDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly IPasswordHasher<Employee> _hasher;
    private readonly SettingsService _settings;
    private readonly IMemoryCache _cache;

    public AuthService(MealDbContext db, IOptions<JwtOptions> jwt, IPasswordHasher<Employee> hasher,
        SettingsService settings, IMemoryCache cache)
    {
        _db = db;
        _jwt = jwt.Value;
        _hasher = hasher;
        _settings = settings;
        _cache = cache;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var mobile = MobileNumber.Normalize(req.MobileNo);
        var emp = await _db.Employees.Include(x => x.Department).Include(x => x.Designation)
            .FirstOrDefaultAsync(x => x.MobileNo == mobile);

        const string invalid = "Mobile number or password is incorrect.";
        if (emp == null) throw new AppException(invalid, StatusCodes.Status401Unauthorized);
        if (emp.PasswordHash == null)
            throw new AppException("Your account is not activated yet. Ask the admin for a one-time code, then tap 'Set password with code'.", StatusCodes.Status401Unauthorized);

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

    // ------------------------------------------------------------------
    // One-time codes (admin-issued) — first sign-in and forgotten passwords
    // ------------------------------------------------------------------

    /// <summary>
    /// Admin issues a one-time code for an employee. Any earlier unused code is cancelled.
    /// Mobile number and employee code are known to colleagues, so they are never enough on their own:
    /// the employee must also have this code, which the admin hands over in person or by phone.
    /// The employee's current password (if any) keeps working until the code is used.
    /// </summary>
    public async Task<SetupCodeDto> IssueSetupCodeAsync(int employeeId, int adminId)
    {
        var emp = await _db.Employees.FindAsync(employeeId) ?? throw AppException.NotFound("Employee");
        if (emp.Status != EmployeeStatus.Active)
            throw new AppException("This employee is inactive. Activate the employee before issuing a code.");

        var now = UtcNowSeconds();
        var open = await _db.PasswordSetupCodes
            .Where(x => x.EmployeeId == employeeId && x.UsedAt == null && x.RevokedAt == null)
            .ToListAsync();
        foreach (var old in open) old.RevokedAt = now;

        var code = GenerateCode();
        var hours = (await _settings.GetAsync()).SetupCodeHours;
        var row = new PasswordSetupCode
        {
            EmployeeId = employeeId,
            Purpose = emp.PasswordHash == null ? SetupCodePurpose.Activation : SetupCodePurpose.Reset,
            CodeHash = _hasher.HashPassword(emp, NormalizeCode(code)),
            ExpiresAt = now.AddHours(hours),
            CreatedBy = adminId,
            CreatedAt = now,
        };
        _db.PasswordSetupCodes.Add(row);
        await _db.SaveChangesAsync();

        return new SetupCodeDto(emp.EmployeeId, emp.EmployeeCode, emp.EmployeeName, emp.MobileNo,
            row.Purpose, code, DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc));
    }

    /// <summary>Employee sets a new password with mobile number + the one-time code from the admin.</summary>
    public async Task<AuthResponse> SetPasswordWithCodeAsync(SetPasswordWithCodeRequest req)
    {
        const string invalid = "Mobile number or code is incorrect, or the code has expired. Please check with the admin.";
        var mobile = MobileNumber.Normalize(req.MobileNo);
        var emp = await _db.Employees.Include(x => x.Department).Include(x => x.Designation)
            .FirstOrDefaultAsync(x => x.MobileNo == mobile);
        if (emp == null) throw new AppException(invalid);

        var now = UtcNowSeconds();
        var row = await _db.PasswordSetupCodes
            .Where(x => x.EmployeeId == emp.EmployeeId && x.UsedAt == null && x.RevokedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
        if (row == null || row.ExpiresAt <= now) throw new AppException(invalid);

        if (_hasher.VerifyHashedPassword(emp, row.CodeHash, NormalizeCode(req.Code)) == PasswordVerificationResult.Failed)
        {
            row.FailedAttempts++;
            var blocked = row.FailedAttempts >= MaxCodeAttempts;
            if (blocked) row.RevokedAt = now;
            await _db.SaveChangesAsync();
            throw new AppException(blocked
                ? "Too many wrong attempts. This code is now blocked. Please ask the admin for a new code."
                : invalid);
        }
        if (emp.Status != EmployeeStatus.Active)
            throw new AppException("Your account is inactive. Please contact the admin.");

        emp.PasswordHash = _hasher.HashPassword(emp, req.Password);
        emp.PasswordSetAt = now;
        emp.LastLoginAt = now;
        row.UsedAt = now;
        await _db.SaveChangesAsync();
        ForgetSessionStamp(emp.EmployeeId); // signs out every other device
        return BuildResponse(emp);
    }

    public async Task<AuthResponse> ChangePasswordAsync(int employeeId, ChangePasswordRequest req)
    {
        var emp = await _db.Employees.Include(x => x.Department).Include(x => x.Designation)
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId) ?? throw AppException.NotFound("Employee");
        if (emp.PasswordHash == null || _hasher.VerifyHashedPassword(emp, emp.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new AppException("Current password is incorrect.");
        emp.PasswordHash = _hasher.HashPassword(emp, req.NewPassword);
        emp.PasswordSetAt = UtcNowSeconds();
        await _db.SaveChangesAsync();
        ForgetSessionStamp(emp.EmployeeId);
        return BuildResponse(emp); // fresh token for this device; other devices are signed out
    }

    private static string GenerateCode()
    {
        Span<char> c = stackalloc char[8];
        for (var i = 0; i < c.Length; i++) c[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return $"{new string(c[..4])}-{new string(c[4..])}";
    }

    private static string NormalizeCode(string code) =>
        new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    // ------------------------------------------------------------------
    // Session stamp: a token is valid only while the password it was issued for is current
    // and the employee is active. Checked on every request (cached briefly).
    // ------------------------------------------------------------------
    /// <summary>Changes when the password or role changes, so older tokens stop working.</summary>
    public static string StampOf(Employee e) => Stamp(e.PasswordSetAt, e.Role);
    // Whole seconds: PasswordSetAt is stored as DATETIME2(0).
    private static string Stamp(DateTime? passwordSetAt, string role) =>
        $"{(passwordSetAt?.Ticks ?? 0) / TimeSpan.TicksPerSecond}.{role}";

    /// <summary>UTC now without fractional seconds, matching DATETIME2(0) columns.</summary>
    private static DateTime UtcNowSeconds()
    {
        var t = DateTime.UtcNow.Ticks;
        return new DateTime(t - t % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    private static string StampCacheKey(int employeeId) => $"session-stamp:{employeeId}";

    public void ForgetSessionStamp(int employeeId) => _cache.Remove(StampCacheKey(employeeId));

    public static async Task<bool> IsSessionValidAsync(MealDbContext db, IMemoryCache cache, int employeeId, string? tokenStamp)
    {
        if (tokenStamp == null) return false;
        var current = await cache.GetOrCreateAsync(StampCacheKey(employeeId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var e = await db.Employees.AsNoTracking().Where(x => x.EmployeeId == employeeId)
                .Select(x => new { x.PasswordSetAt, x.Status, x.Role }).FirstOrDefaultAsync();
            return e == null || e.Status != EmployeeStatus.Active ? null : Stamp(e.PasswordSetAt, e.Role);
        });
        return current != null && current == tokenStamp;
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
            new(PasswordStampClaim, StampOf(emp)),
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
