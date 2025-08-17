using AutoPurchaseAdmin.Helpers;
using AutoPurchaseAdmin.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPurchaseAdmin.ViewModels
{
    public class LicenseViewModel : BaseViewModel
    {
        private readonly ApiClientService _apiService;
        public ObservableCollection<LicenseDto> Licenses { get; set; } = new();

        private LicenseDto? _selectedLicense;
        public LicenseDto? SelectedLicense { get => _selectedLicense; set { _selectedLicense = value; OnPropertyChanged(); } }

        public RelayCommand LoadCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand UpdateCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public LicenseViewModel(ApiClientService apiService)
        {
            _apiService = apiService;

            LoadCommand = new RelayCommand(async _ => await LoadLicenses());
            AddCommand = new RelayCommand(async _ => await AddLicense());
            UpdateCommand = new RelayCommand(async _ => await UpdateLicense(), _ => SelectedLicense != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteLicense(), _ => SelectedLicense != null);
        }

        private async Task LoadLicenses()
        {
            var list = await _apiService.CallApiAsync(() =>
                _apiService.Client.GetAsync<System.Collections.Generic.List<LicenseDto>>("api/admin/licenses"));
            if (list != null)
            {
                Licenses.Clear();
                foreach (var l in list) Licenses.Add(l);
            }
        }

        private async Task AddLicense()
        {
            var newLicense = new LicenseDto
            {
                LicenseId = Guid.NewGuid(),
                LicenseKey = "LICENSE_" + new Random().Next(1000, 9999),
                CreatedAt = DateTime.UtcNow,
                ExpiredAt = DateTime.UtcNow.AddDays(30),
                IsActive = true
            };

            var created = await _apiService.CallApiAsync(() =>
                _apiService.Client.PostAsync<LicenseDto, LicenseDto>("api/admin/licenses", newLicense));

            if (created != null) await LoadLicenses();
        }

        private async Task UpdateLicense()
        {
            if (SelectedLicense == null) return;
            SelectedLicense.IsActive = !SelectedLicense.IsActive;

            var updated = await _apiService.CallApiAsync(() =>
                _apiService.Client.PutAsync<LicenseDto, LicenseDto>($"api/admin/licenses/{SelectedLicense.LicenseId}", SelectedLicense));

            if (updated != null) await LoadLicenses();
        }

        private async Task DeleteLicense()
        {
            if (SelectedLicense == null) return;

            var deleted = await _apiService.CallApiAsync(() =>
                _apiService.Client.DeleteAsync<bool>($"api/admin/licenses/{SelectedLicense.LicenseId}"));

            if (deleted == true) await LoadLicenses();
        }
    }
}
