using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using AkilliEvMobil.Services;

namespace AkilliEvMobil.Views
{
    public partial class AllDevicesPage : ContentPage
    {
        public ObservableCollection<SmartDevice> FilteredDevices { get; set; }
        private bool _showInactive = false;

        public AllDevicesPage()
        {
            InitializeComponent();
            FilteredDevices = new ObservableCollection<SmartDevice>();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RefreshList(); // Ekranın boş kalmaması için hemen listeyi doldur
            await DeviceService.Instance.SyncWithBackendAsync();
            RefreshList(); // Backend'den gelen verilere göre kilitleri güncelle
        }

        private void RefreshList()
        {
            var searchText = SearchEntry.Text?.ToLower() ?? "";
            
            var filtered = DeviceService.Instance.Devices.Where(d => 
                (string.IsNullOrEmpty(searchText) || d.Name.ToLower().Contains(searchText) || 
                (searchText == "kamera" && d.Name.Contains("Kamera")) ||
                (searchText == "ısıtıcı" && d.Name.Contains("Isıtıcı")) ||
                (searchText == "fan" && d.Name.Contains("Fan")) ||
                (searchText == "aydınlatma" && d.Name.Contains("Aydınlatma")) ||
                (searchText == "tente" && d.Name.Contains("Tente"))) &&
                d.IsLocked == _showInactive
            ).ToList();

            FilteredDevices.Clear();
            foreach (var device in filtered)
            {
                FilteredDevices.Add(device);
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshList();
        }

        private async void OnActiveTabTapped(object sender, EventArgs e)
        {
            _showInactive = false;
            ActiveTabBorder.BackgroundColor = Color.FromArgb("#4A90E2");
            ActiveTabLabel.TextColor = Colors.White;
            
            InactiveTabBorder.BackgroundColor = Colors.Transparent;
            InactiveTabLabel.TextColor = Color.FromArgb("#7D8BA1");
            
            await DeviceService.Instance.SyncWithBackendAsync();
            RefreshList();
        }

        private async void OnInactiveTabTapped(object sender, EventArgs e)
        {
            _showInactive = true;
            InactiveTabBorder.BackgroundColor = Color.FromArgb("#4A90E2");
            InactiveTabLabel.TextColor = Colors.White;
            
            ActiveTabBorder.BackgroundColor = Colors.Transparent;
            ActiveTabLabel.TextColor = Color.FromArgb("#7D8BA1");
            
            await DeviceService.Instance.SyncWithBackendAsync();
            RefreshList();
        }

        private async void OnDeviceTapped(object sender, EventArgs e)
        {
            if (sender is Border border && border.BindingContext is SmartDevice device)
            {
                if (device.IsLocked)
                {
                    await DisplayAlert("Kilitli Özellik", "Premium üyelik gerektirir.", "Tamam");
                }
            }
        }

        private void OnFavoriteTapped(object sender, EventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is SmartDevice device)
            {
                if (device.IsLocked) return; // Kilitli cihaz favoriye eklenemez
                
                DeviceService.Instance.ToggleFavorite(device);
            }
        }

        private async void OnHomeTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//MainDashboardPage");
        }

        private async void OnGridTapped(object sender, EventArgs e)
        {
            // Zaten bu sayfadayız
        }

        private async void OnAutomationTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//AutomationPage");
        }

        private async void OnSettingsTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//SettingsPage");
        }
    }
}
