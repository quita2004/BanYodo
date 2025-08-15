using AutoPurchaseAPI.Models;
using Dapper;
using Microsoft.Data.SqlClient;

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
}
