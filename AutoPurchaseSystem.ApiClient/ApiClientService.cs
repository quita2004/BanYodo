using AutoPurchaseSystem.ApiClient;
using AutoPurchaseSystem.ApiClient.AuthProviders;

public class ApiClientService
{
    private readonly ApiClient _apiClient;
    private string? _jwtToken;
    private IAuthProvider? _authProvider;

    public ApiClientService(string baseUrl)
    {
        _apiClient = new ApiClient(baseUrl);
    }

    public void SetAuthProvider(IAuthProvider authProvider)
    {
        _authProvider = authProvider;
    }

    public ApiClient Client => _apiClient;

    public async Task<bool> LoginAsync()
    {
        if (_authProvider == null)
            throw new InvalidOperationException("AuthProvider chưa được set.");

        _jwtToken = await _authProvider.LoginAsync(_apiClient);
        return _jwtToken != null;
    }

    public async Task<T?> CallApiAsync<T>(Func<Task<T?>> apiCall)
    {
        try
        {
            return await apiCall();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            if (_authProvider != null)
            {
                bool loginSuccess = await LoginAsync();
                if (loginSuccess)
                    return await apiCall();
            }
            return default;
        }
    }

    public void Logout()
    {
        _jwtToken = null;
        _apiClient.SetJwtToken(string.Empty);
    }

    public string? GetToken() => _jwtToken;
}

// Example usage:

// CLIENT AUTHENTICATION
//using ApiClientLibrary;
//using ApiClientLibrary.AuthProviders;

//var apiService = new ApiClientService("https://localhost:5001/");
//apiService.SetAuthProvider(new ClientAuthProvider("LICENSE_00001"));
//await apiService.LoginAsync();

// ADMIN AUTHENTICATION
//using ApiClientLibrary;
//using ApiClientLibrary.AuthProviders;

//var apiService = new ApiClientService("https://localhost:5001/");
//apiService.SetAuthProvider(new AdminAuthProvider("admin01", "123456"));
//await apiService.LoginAsync();


// Calling API
//var newLicense = new LicenseDto
//{
//    LicenseId = Guid.NewGuid(),
//    LicenseKey = "LICENSE_99999",
//    CreatedAt = DateTime.UtcNow,
//    ExpiredAt = DateTime.UtcNow.AddDays(30),
//    IsActive = true
//};

//var createdLicense = await apiService.CallApiAsync(
//    () => apiService.Client.PostAsync<LicenseDto, LicenseDto>("api/admin/licenses", newLicense)
//)
