using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AutoPurchaseAPI.Services;

public class LicenseService
{
    private readonly IConfiguration _config;
    public LicenseService(IConfiguration config) => _config = config;

    public async Task<string?> LoginByLicenseAsync(string licenseKey)
    {
        Log.Warning("Attempting to login with license key: {LicenseKey}", licenseKey);
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        var licenseId = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SP_CLIENT_PRELOGIN",
            new { LICENSE_KEY = licenseKey },
            commandType: System.Data.CommandType.StoredProcedure);

        if (licenseId is null) return null;

        var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"]);
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("licenseKey", licenseKey) }),
            Expires = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresMinutes"])),
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
