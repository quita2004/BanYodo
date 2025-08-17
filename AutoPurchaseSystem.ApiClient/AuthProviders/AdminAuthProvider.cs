using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPurchaseSystem.ApiClient.AuthProviders
{
    public class AdminAuthProvider : IAuthProvider
    {
        private readonly string _username;
        private readonly string _password;

        public AdminAuthProvider(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public async Task<string?> LoginAsync(ApiClient apiClient)
        {
            var result = await apiClient.PostAsync<object, Dictionary<string, string>>("api/admin/login",
                new { Username = _username, Password = _password });

            if (result != null && result.TryGetValue("token", out var token))
            {
                apiClient.SetJwtToken(token);
                return token;
            }

            return null;
        }
    }
}
