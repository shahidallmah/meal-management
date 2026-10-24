using MealManagement.Api.Data;
using MealManagement.Api.Infrastructure;
using MealManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Controllers;

public record BrandingDto(string CompanyName, string? LogoUrl);

/// <summary>
/// Company branding (name + logo). Reading is public so the sign-in page can show the logo;
/// changing the logo is admin only (Settings → Company logo).
/// </summary>
[ApiController]
[Route("api/branding")]
public class BrandingController : ControllerBase
{
    public const int MaxLogoBytes = 1024 * 1024; // 1 MB — keep in sync with the web app

    private readonly MealDbContext _db;
    private readonly SettingsService _settings;

    public BrandingController(MealDbContext db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [HttpGet, AllowAnonymous]
    public async Task<BrandingDto> Get()
    {
        var settings = await _settings.GetAsync();
        var updatedAt = await _db.CompanyLogos.AsNoTracking()
            .Where(x => x.CompanyLogoId == 1)
            .Select(x => (DateTime?)x.UpdatedAt)
            .FirstOrDefaultAsync();
        // The version query makes browsers and the mobile app fetch the new image after a change.
        var url = updatedAt.HasValue ? $"/api/branding/logo?v={updatedAt.Value.Ticks}" : null;
        return new BrandingDto(settings.CompanyName, url);
    }

    [HttpGet("logo"), AllowAnonymous]
    public async Task<IActionResult> Logo()
    {
        var logo = await _db.CompanyLogos.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyLogoId == 1);
        if (logo == null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=86400";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'";
        return File(logo.Content, logo.ContentType);
    }

    [HttpPut("logo"), Authorize(Roles = Roles.Admin)]
    [RequestSizeLimit(MaxLogoBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxLogoBytes + 64 * 1024)]
    public async Task<BrandingDto> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0) throw new AppException("Please choose an image file.");
        if (file.Length > MaxLogoBytes) throw new AppException("The logo must be 1 MB or smaller.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        // Trust the file content, not the extension or the browser's content type.
        var contentType = DetectImageType(bytes)
            ?? throw new AppException("Only PNG, JPG or WebP images can be used as the logo.");

        var row = await _db.CompanyLogos.FirstOrDefaultAsync(x => x.CompanyLogoId == 1);
        if (row == null)
        {
            row = new CompanyLogo { CompanyLogoId = 1 };
            _db.CompanyLogos.Add(row);
        }
        row.Content = bytes;
        row.ContentType = contentType;
        row.FileName = Path.GetFileName(file.FileName) is { Length: > 0 } n ? n[..Math.Min(n.Length, 200)] : null;
        row.UpdatedBy = User.GetEmployeeId();
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await Get();
    }

    [HttpDelete("logo"), Authorize(Roles = Roles.Admin)]
    public async Task<BrandingDto> Remove()
    {
        var row = await _db.CompanyLogos.FirstOrDefaultAsync(x => x.CompanyLogoId == 1);
        if (row != null)
        {
            _db.CompanyLogos.Remove(row);
            await _db.SaveChangesAsync();
        }
        return await Get();
    }

    private static string? DetectImageType(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }
}
