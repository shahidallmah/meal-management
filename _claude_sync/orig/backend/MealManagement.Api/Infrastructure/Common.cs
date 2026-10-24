using System.Security.Claims;
using System.Text.Json;

namespace MealManagement.Api.Infrastructure;

/// <summary>Business-rule error returned to the client as { message } with the given status code.</summary>
public class AppException : Exception
{
    public int StatusCode { get; }
    public AppException(string message, int statusCode = StatusCodes.Status400BadRequest) : base(message)
        => StatusCode = statusCode;

    public static AppException NotFound(string what) => new($"{what} not found.", StatusCodes.Status404NotFound);
}

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await Write(context, ex.StatusCode, ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database update failed");
            var inner = ex.InnerException?.Message ?? ex.Message;
            var msg = inner.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) || inner.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                ? "A record with the same unique value already exists."
                : "The change could not be saved. Please check the data and try again.";
            await Write(context, StatusCodes.Status409Conflict, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error");
            await Write(context, StatusCodes.Status500InternalServerError, "Something went wrong. Please try again.");
        }
    }

    private static async Task Write(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
    }
}

public static class ClaimsExtensions
{
    public static int GetEmployeeId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (!int.TryParse(value, out var id)) throw new AppException("Not signed in.", StatusCodes.Status401Unauthorized);
        return id;
    }
}

public static class MobileNumber
{
    /// <summary>Normalises Bangladeshi mobile numbers: strips spaces/dashes and the +88 / 88 prefix → 01XXXXXXXXX.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("880") && digits.Length == 13) digits = digits[2..];
        return digits;
    }

    public static bool IsValid(string normalized) => normalized.Length >= 10 && normalized.Length <= 15;
}

public static class TimeZoneHelper
{
    public static TimeZoneInfo Find(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var win))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(win); } catch { }
            }
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var iana))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(iana); } catch { }
            }
            // Fallback: Bangladesh Standard Time (UTC+6, no DST)
            return TimeZoneInfo.CreateCustomTimeZone("BST+6", TimeSpan.FromHours(6), "UTC+06:00", "UTC+06:00");
        }
    }
}
