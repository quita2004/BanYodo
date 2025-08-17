global using AutoPurchaseAPI.Services;
global using Microsoft.AspNetCore.Authentication.JwtBearer;
global using Microsoft.IdentityModel.Tokens;
global using System.Text;
using AutoPurchaseAPI.DTOs;
using AutoPurchaseAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ======================
// Cấu hình Serilog
// ======================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddSingleton<LicenseService>();
builder.Services.AddSingleton<AdminService>();

// JWT Authentication
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// -------------------- Client login --------------------
app.MapPost("/api/client/login", async (ClientLoginRequest request, LicenseService service) =>
{
    var token = await service.LoginByLicenseAsync(request.LicenseKey);
    return token is null ? Results.Unauthorized() : Results.Ok(new { token });
});

// -------------------- Admin License APIs --------------------
app.MapGet("/api/admin/licenses", [Microsoft.AspNetCore.Authorization.Authorize] async (AdminService service) =>
{
    var licenses = await service.GetAllLicensesAsync();
    return Results.Ok(licenses);
});

app.MapPost("/api/admin/licenses", [Microsoft.AspNetCore.Authorization.Authorize] async (LicenseModel license, AdminService service) =>
{
    var created = await service.AddLicenseAsync(license);
    return Results.Ok(created);
});

app.MapPut("/api/admin/licenses/{id}", [Microsoft.AspNetCore.Authorization.Authorize] async (Guid id, LicenseModel license, AdminService service) =>
{
    var updated = await service.UpdateLicenseAsync(id, license);
    return updated ? Results.Ok() : Results.NotFound();
});

app.MapDelete("/api/admin/licenses/{id}", [Microsoft.AspNetCore.Authorization.Authorize] async (Guid id, AdminService service) =>
{
    var deleted = await service.DeleteLicenseAsync(id);
    return deleted ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/admin/login", async (AdminLoginRequest request, AdminService service) =>
{
    var token = await service.LoginAsync(request.Username, request.Password);
    return token is null ? Results.Unauthorized() : Results.Ok(new { token });
});

app.Run();