using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoPurchaseSystem.ApiClient;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly int _maxRetry;
    private string? _jwtToken;

    public ApiClient(string baseUrl, int maxRetry = 3, TimeSpan? timeout = null)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        if (timeout.HasValue) _httpClient.Timeout = timeout.Value;
        _maxRetry = maxRetry;
    }

    public void SetJwtToken(string token)
    {
        _jwtToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
    }

    // ---------------- Generic GET/POST with auto mapping ----------------
    public async Task<T?> SendRequestAsync<T>(Func<Task<HttpResponseMessage>> httpCall)
    {
        var response = await SendWithRetry(httpCall);
        if (response == null) return default;

        var jsonString = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(jsonString)) return default;

        return JsonSerializer.Deserialize<T>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    // ---------------- GET ----------------
    public Task<T?> GetAsync<T>(string endpoint, Dictionary<string, string>? queryParams = null)
    {
        string url = endpoint;
        if (queryParams != null && queryParams.Any())
        {
            var queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            url += "?" + queryString;
        }

        return SendRequestAsync<T>(() => _httpClient.GetAsync(url));
    }

    // ---------------- POST ----------------
    public Task<T?> PostAsync<TRequest, T>(string endpoint, TRequest data)
    {
        return SendRequestAsync<T>(() => _httpClient.PostAsJsonAsync(endpoint, data));
    }

    // ---------------- PUT ----------------
    public Task<T?> PutAsync<TRequest, T>(string endpoint, TRequest data)
    {
        return SendRequestAsync<T>(() => _httpClient.PutAsJsonAsync(endpoint, data));
    }

    // ---------------- DELETE ----------------
    public Task<T?> DeleteAsync<T>(string endpoint)
    {
        return SendRequestAsync<T>(() => _httpClient.DeleteAsync(endpoint));
    }

    // ---------------- Retry ----------------
    private async Task<HttpResponseMessage?> SendWithRetry(Func<Task<HttpResponseMessage>> action)
    {
        int attempt = 0;
        while (attempt < _maxRetry)
        {
            try
            {
                attempt++;
                var response = await action();
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch
            {
                if (attempt >= _maxRetry)
                    return null;

                await Task.Delay(1000 * attempt);
            }
        }
        return null;
    }
}
