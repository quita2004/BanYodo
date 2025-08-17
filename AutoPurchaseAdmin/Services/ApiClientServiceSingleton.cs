using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPurchaseAdmin.Services
{
    public sealed class ApiClientServiceSingleton
    {
        private static readonly Lazy<ApiClientService> _instance =
            new Lazy<ApiClientService>(() => new ApiClientService("http://localhost:5006/"));

        public static ApiClientService Instance => _instance.Value;

        private ApiClientServiceSingleton() { }
    }
}
