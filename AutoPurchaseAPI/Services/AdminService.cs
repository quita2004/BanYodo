using AutoPurchaseAPI.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AutoPurchaseAPI.Services;

public class AdminService
{
    private readonly IConfiguration _config;
    public AdminService(IConfiguration config) => _config = config;

    public async Task<IEnumerable<LicenseModel>> GetAllLicensesAsync()
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        return await conn.QueryAsync<LicenseModel>(
            "SP_ADMIN_LIST_LICENSES",
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<LicenseModel> AddLicenseAsync(LicenseModel license)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        await conn.ExecuteAsync(
            "SP_ADMIN_ADD_LICENSE",
            new { LICENSE_KEY = license.LicenseKey, EXPIRED_AT = license.ExpiredAt, IS_ACTIVE = license.IsActive },
            commandType: System.Data.CommandType.StoredProcedure);
        return license;
    }

    public async Task<bool> UpdateLicenseAsync(Guid id, LicenseModel license)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        var rows = await conn.ExecuteAsync(
            "SP_ADMIN_UPDATE_LICENSE",
            new { LICENSE_ID = id, LICENSE_KEY = license.LicenseKey, EXPIRED_AT = license.ExpiredAt, IS_ACTIVE = license.IsActive },
            commandType: System.Data.CommandType.StoredProcedure);
        return rows > 0;
    }

    public async Task<bool> DeleteLicenseAsync(Guid id)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        var rows = await conn.ExecuteAsync(
            "SP_ADMIN_DELETE_LICENSE",
            new { LICENSE_ID = id },
            commandType: System.Data.CommandType.StoredProcedure);
        return rows > 0;
    }

    public async Task<string?> LoginAsync(string username, string password)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

        var adminId = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SP_ADMIN_LOGIN",
            new { USERNAME = username, PASSWORD = password },
            commandType: System.Data.CommandType.StoredProcedure);

        if (adminId is null) return null;

        // Tạo JWT
        var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"]);
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("username", username),
                new Claim("role", "Admin")
            }),
            Expires = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresMinutes"] ?? "60")),
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
