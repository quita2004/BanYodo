using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPurchaseSystem.ApiClient.AuthProviders
{
    public class ClientAuthProvider : IAuthProvider
    {
        private readonly string _licenseKey;

        public ClientAuthProvider(string licenseKey)
        {
            _licenseKey = licenseKey;
        }

        public async Task<string?> LoginAsync(ApiClient apiClient)
        {
            var result = await apiClient.PostAsync<object, Dictionary<string, string>>("api/client/login",
                new { LicenseKey = _licenseKey });

            if (result != null && result.TryGetValue("token", out var token))
            {
                apiClient.SetJwtToken(token);
                return token;
            }

            return null;
        }
    }
}
