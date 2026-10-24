using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using MealManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MealManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting(RateLimits.Auth)]
    public Task<AuthResponse> Login(LoginRequest req) => _auth.LoginAsync(req);

    /// <summary>Lets the sign-in screen send first-time users to "Create password".</summary>
    [AllowAnonymous, HttpPost("check-mobile"), EnableRateLimiting(RateLimits.Auth)]
    public Task<CheckMobileResponse> CheckMobile(CheckMobileRequest req) => _auth.CheckMobileAsync(req.MobileNo);

    /// <summary>First sign-in or forgotten password: mobile no + one-time code issued by the admin.</summary>
    [AllowAnonymous, HttpPost("set-password"), EnableRateLimiting(RateLimits.Auth)]
    public Task<AuthResponse> SetPassword(SetPasswordWithCodeRequest req) => _auth.SetPasswordWithCodeAsync(req);

    [Authorize, HttpGet("me")]
    public Task<UserDto> Me() => _auth.GetUserAsync(User.GetEmployeeId());

    /// <summary>Returns a fresh token; sessions on other devices are signed out.</summary>
    [Authorize, HttpPost("change-password")]
    public Task<AuthResponse> ChangePassword(ChangePasswordRequest req) => _auth.ChangePasswordAsync(User.GetEmployeeId(), req);
}

[ApiController]
[Authorize]
[Route("api/my")]
public class MyMealsController : ControllerBase
{
    private readonly MealService _meals;
    private readonly SettingsService _settings;

    public MyMealsController(MealService meals, SettingsService settings)
    {
        _meals = meals;
        _settings = settings;
    }

    [HttpGet("meals")]
    public Task<MyMealsDto> GetMeals() => _meals.GetMyMealsAsync(User.GetEmployeeId());

    [HttpPut("meals")]
    public async Task<MyMealsDto> SetMeal(SetMealRequest req)
    {
        var id = User.GetEmployeeId();
        await _meals.SetMyMealAsync(id, req);
        return await _meals.GetMyMealsAsync(id);
    }

    [HttpGet("history")]
    public async Task<MealHistoryDto> History([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var today = (await _settings.GetAsync()).Today;
        var f = from ?? new DateOnly(today.Year, today.Month, 1);
        var t = to ?? today;
        return await _meals.GetHistoryAsync(User.GetEmployeeId(), f, t);
    }
}
