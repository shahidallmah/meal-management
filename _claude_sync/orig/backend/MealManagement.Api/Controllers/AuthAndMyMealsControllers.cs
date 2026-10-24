using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using MealManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MealManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    [AllowAnonymous, HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest req) => _auth.LoginAsync(req);

    /// <summary>Lets the sign-in screen send first-time users to "Create password".</summary>
    [AllowAnonymous, HttpPost("check-mobile")]
    public Task<CheckMobileResponse> CheckMobile(CheckMobileRequest req) => _auth.CheckMobileAsync(req.MobileNo);

    [AllowAnonymous, HttpPost("create-password")]
    public Task<AuthResponse> CreatePassword(CreatePasswordRequest req) => _auth.CreatePasswordAsync(req);

    [Authorize, HttpGet("me")]
    public Task<UserDto> Me() => _auth.GetUserAsync(User.GetEmployeeId());

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        await _auth.ChangePasswordAsync(User.GetEmployeeId(), req);
        return NoContent();
    }
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
