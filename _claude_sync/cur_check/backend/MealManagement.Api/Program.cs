using System.Text;
using System.Text.Json.Serialization;
using MealManagement.Api.Data;
using MealManagement.Api.Infrastructure;
using MealManagement.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Database ----------
builder.Services.AddDbContext<MealDbContext>(o =>
    o.UseSqlServer(config.GetConnectionString("MealDb"), sql => sql.EnableRetryOnFailure(3)));

// ---------- Options ----------
builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
builder.Services.Configure<SeedAdminOptions>(config.GetSection("SeedAdmin"));
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key in appsettings.json must be at least 32 characters long.");

// ---------- Services ----------
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPasswordHasher<Employee>, PasswordHasher<Employee>>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<MealCalendarFactory>();
builder.Services.AddScoped<MealService>();
builder.Services.AddScoped<MealGenerationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddHostedService<MealGenerationJob>();

// ---------- Auth ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

// ---------- CORS (web dev server + Capacitor mobile apps) ----------
var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));

// ---------- MVC ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    // Return validation errors as { message } like the rest of the API.
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var first = ctx.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Please check the entered data.";
        return new BadRequestObjectResult(new { message = first, errors = ctx.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray()) });
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Meal Management API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Paste the token from /api/auth/login"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
    c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
});

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
if (app.Environment.IsDevelopment() || config.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serves the built React app from wwwroot (copy frontend/dist here for single-site hosting).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
app.MapFallbackToFile("index.html");

try
{
    await AuthService.EnsureSeedAdminAsync(app.Services);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Could not check/create the seed admin. Is the database reachable and the schema created?");
}
app.Run();
