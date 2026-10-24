using System.Globalization;
using MealManagement.Api.Data;
using MealManagement.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MealManagement.Api.Services;

public static class SettingKeys
{
    public const string BookingWindowDays = "BookingWindowDays";
    public const string CutoffTime = "CutoffTime";
    public const string WeeklyOffDays = "WeeklyOffDays";
    public const string AllowEligibleOptOut = "AllowEligibleOptOut";
    public const string TimeZoneId = "TimeZoneId";
    public const string CompanyName = "CompanyName";
}

public record MealSettings(
    int BookingWindowDays,
    TimeOnly CutoffTime,
    IReadOnlySet<DayOfWeek> WeeklyOffDays,
    bool AllowEligibleOptOut,
    TimeZoneInfo TimeZone,
    string TimeZoneId,
    string CompanyName)
{
    public DateTime NowLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);
    public DateOnly Today => DateOnly.FromDateTime(NowLocal);
}

public class SettingsService
{
    private const string CacheKey = "meal-settings";
    private readonly MealDbContext _db;
    private readonly IMemoryCache _cache;

    public SettingsService(MealDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<MealSettings> GetAsync()
    {
        if (_cache.TryGetValue(CacheKey, out MealSettings? cached) && cached != null) return cached;

        var raw = await _db.AppSettings.AsNoTracking().ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);
        string Get(string key, string fallback) => raw.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fallback;

        var window = int.TryParse(Get(SettingKeys.BookingWindowDays, "7"), out var w) ? Math.Clamp(w, 1, 60) : 7;
        var cutoff = TimeOnly.TryParseExact(Get(SettingKeys.CutoffTime, "17:00"), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var c)
            ? c : new TimeOnly(17, 0);
        var offDays = ParseDays(Get(SettingKeys.WeeklyOffDays, "Friday"));
        var optOut = bool.TryParse(Get(SettingKeys.AllowEligibleOptOut, "true"), out var o) ? o : true;
        var tzId = Get(SettingKeys.TimeZoneId, "Asia/Dhaka");

        var settings = new MealSettings(window, cutoff, offDays, optOut, TimeZoneHelper.Find(tzId), tzId,
            Get(SettingKeys.CompanyName, "Company"));

        _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(10));
        return settings;
    }

    public async Task<List<AppSetting>> GetAllAsync() =>
        await _db.AppSettings.AsNoTracking().OrderBy(x => x.SettingKey).ToListAsync();

    public async Task UpdateAsync(Dictionary<string, string> values)
    {
        foreach (var (key, rawValue) in values)
        {
            var value = (rawValue ?? "").Trim();
            Validate(key, value);
            var row = await _db.AppSettings.FindAsync(key);
            if (row == null)
                _db.AppSettings.Add(new AppSetting { SettingKey = key, SettingValue = value });
            else
                row.SettingValue = value;
        }
        await _db.SaveChangesAsync();
        _cache.Remove(CacheKey);
    }

    private static void Validate(string key, string value)
    {
        switch (key)
        {
            case SettingKeys.BookingWindowDays:
                if (!int.TryParse(value, out var w) || w < 1 || w > 60)
                    throw new AppException("Booking window must be between 1 and 60 days.");
                break;
            case SettingKeys.CutoffTime:
                if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    throw new AppException("Cut-off time must be in HH:mm (24 hour) format, e.g. 17:00.");
                break;
            case SettingKeys.WeeklyOffDays:
                if (value.Length > 0 && ParseDays(value).Count == 0)
                    throw new AppException("Weekly off days must be weekday names, e.g. Friday,Saturday.");
                break;
            case SettingKeys.AllowEligibleOptOut:
                if (!bool.TryParse(value, out _)) throw new AppException("AllowEligibleOptOut must be true or false.");
                break;
            case SettingKeys.TimeZoneId:
            case SettingKeys.CompanyName:
                if (value.Length == 0) throw new AppException($"{key} cannot be empty.");
                break;
            default:
                throw new AppException($"Unknown setting '{key}'.");
        }
    }

    private static HashSet<DayOfWeek> ParseDays(string value)
    {
        var set = new HashSet<DayOfWeek>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<DayOfWeek>(part, true, out var d)) set.Add(d);
        return set;
    }
}
