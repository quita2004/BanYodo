using AutoPurchaseAdmin.Helpers;
using AutoPurchaseAdmin.Services;
using AutoPurchaseSystem.ApiClient.AuthProviders;
using System;
using System.Threading.Tasks;

namespace AutoPurchaseAdmin.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {

        public ApiClientService ApiService => _apiService;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _error = string.Empty;

        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
        public string Error { get => _error; set { _error = value; OnPropertyChanged(); } }

        public RelayCommand LoginCommand { get; }

        private readonly ApiClientService _apiService;

        public Action OpenMainWindowAction { get; set; }


        public LoginViewModel()
        {
            _apiService = ApiClientServiceSingleton.Instance;
            LoginCommand = new RelayCommand(async _ => await LoginAsync());
        }

        private async Task LoginAsync()
        {
            try
            {
                _apiService.SetAuthProvider(new AdminAuthProvider(Username, Password));
                bool success = await _apiService.LoginAsync();

                if (success)
                {
                     OpenMainWindowAction?.Invoke();
                }
                else
                {
                    Error = "Login failed. Check username/password.";
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        }
    }
}
