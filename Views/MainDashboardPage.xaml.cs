using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AkilliEvMobil.Services;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    /*
     * MainDashboardPage.xaml.cs: Ev otomasyonu ana ekranının mantığı.
     * Raspberry Pi okumaları ve MQTT haberleşmesi buradan yönetilir.
     */
    public partial class MainDashboardPage : ContentPage
    {
        private bool _isMockDataRunning;
        private Random _random = new Random();

        public ObservableCollection<SmartDevice> FavoriteDevices { get; set; } = new ObservableCollection<SmartDevice>();

        public MainDashboardPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UserNameLabel.Text = DeviceService.Instance.CurrentUserName ?? "Değerli Müşterimiz";
            _isMockDataRunning = true;
            StartMockDataLoop();
            RefreshFavorites();
        }

        private void RefreshFavorites()
        {
            FavoriteDevices.Clear();
            var favorites = DeviceService.Instance.Devices.Where(d => d.IsFavorite).ToList();
            foreach (var device in favorites)
            {
                FavoriteDevices.Add(device);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isMockDataRunning = false;
        }

        private async void StartMockDataLoop()
        {
            while (_isMockDataRunning)
            {
                try
                {
                    // Hava Durumunu Güncelle (Her 30 saniyede bir veya döngü başında)
                    await UpdateWeatherAsync();

                    string userId = DeviceService.Instance.CurrentUserId;
                    if (string.IsNullOrEmpty(userId))
                    {
                        // Henüz Sync yapılmadıysa bekle
                        await DeviceService.Instance.SyncWithBackendAsync();
                        userId = DeviceService.Instance.CurrentUserId;
                    }

                    if (!string.IsNullOrEmpty(userId))
                    {
                        string baseUrl = "http://nart3d.com:3000";

                        using var client = new System.Net.Http.HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(3);

                        // 1. Yeni rastgele veri simüle et (Kullanıcıya özel)
                        await client.PostAsync($"{baseUrl}/api/simulate/{userId}", null);

                        // 2. En güncel veriyi çek
                        var response = await client.GetAsync($"{baseUrl}/api/users/{userId}/sensors/latest");
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var log = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(json);

                            if (log != null)
                            {
                                double temp = log["temperature"]?.GetValue<double>() ?? 0;
                                double humidity = log["humidity"]?.GetValue<double>() ?? 0;
                                bool isRaining = log["isRaining"]?.GetValue<bool>() ?? false;
                                bool gasDetected = log["gasDetected"]?.GetValue<bool>() ?? false;

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    TempLabel.Text = $"{temp:F1} °C";
                                    HumidityLabel.Text = $"%{humidity:F0}";
                                    
                                    // Gaz durumunu da burada bir yere ekleyebiliriz veya RainLabel rengini değiştirebiliriz
                                    if (gasDetected)
                                    {
                                        TempLabel.TextColor = Colors.Red;
                                    }
                                    else
                                    {
                                        TempLabel.TextColor = Color.FromArgb("#1E293B");
                                    }
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Sensor Loop Error: {ex.Message}");
                }

                await Task.Delay(5000); // 5 saniyede bir güncelle
            }
        }

        private double _currentLat = 40.76;
        private double _currentLon = 29.92;
        private string _currentCity = "İzmit";

        private async Task UpdateWeatherAsync()
        {
            try
            {
                string baseUrl = "http://nart3d.com:3000";

                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync($"{baseUrl}/api/weather?lat={_currentLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={_currentLon.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(json);
                    var current = data["current_weather"]?.AsObject();

                    if (current != null)
                    {
                        double temp = current["temperature"]?.GetValue<double>() ?? 0;
                        int code = current["weathercode"]?.GetValue<int>() ?? 0;

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            WeatherTempLabel.Text = $"{temp:F1}°C";
                            WeatherImage.Source = GetWeatherIconUrl(code);
                            CityLabel.Text = _currentCity;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Weather Update Error: {ex.Message}");
            }
        }

        private string GetWeatherIconUrl(int code)
        {
            // Open-Meteo WMO codes mapped to WeatherAPI.com style high-quality IDs
            // Daha premium görünümlü bir set kullanıyoruz.
            string iconName = code switch
            {
                0 => "113", // Sunny
                1 or 2 or 3 => "116", // Partly cloudy
                45 or 48 => "143", // Mist
                51 or 53 or 55 => "266", // Light drizzle
                61 or 63 or 65 => "296", // Patchy light rain
                71 or 73 or 75 => "326", // Light snow
                95 or 96 or 99 => "389", // Thunder
                _ => "119" // Cloudy
            };
            
            // Gerçekçi ve Premium ikonlar (CDN üzerinden)
            return $"https://cdn.weatherapi.com/weather/128x128/day/{iconName}.png";
        }

        private async void OnWeatherCardTapped(object sender, EventArgs e)
        {
            // [DÜZELTME] Prompt'un çalışması için UI thread garantisi ve backend proxy kullanımı
            string result = await MainThread.InvokeOnMainThreadAsync(async () => 
            {
                return await DisplayPromptAsync("Konum Değiştir", "Şehir adını giriniz:", "Ara", "İptal", "Örn: İstanbul");
            });
            
            if (!string.IsNullOrWhiteSpace(result))
            {
                try
                {
                    string baseUrl = "http://nart3d.com:3000";

                    using var client = new System.Net.Http.HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    
                    // Backend Proxy üzerinden koordinat bul (Daha güvenli ve stabil)
                    var geoRes = await client.GetAsync($"{baseUrl}/api/geocode?name={Uri.EscapeDataString(result)}");
                    
                    if (geoRes.IsSuccessStatusCode)
                    {
                        var json = await geoRes.Content.ReadAsStringAsync();
                        var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(json);
                        
                        if (data != null)
                        {
                            _currentLat = data["latitude"]?.GetValue<double>() ?? 40.76;
                            _currentLon = data["longitude"]?.GetValue<double>() ?? 29.92;
                            _currentCity = data["name"]?.ToString() ?? result;
                            
                            await UpdateWeatherAsync();
                        }
                    }
                    else
                    {
                        await DisplayAlert("Hata", "Şehir bulunamadı. Lütfen bağlantınızı kontrol edin.", "Tamam");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Geocode Error: {ex.Message}");
                }
            }
        }

        private void OnFavoriteTapped(object sender, EventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is SmartDevice device)
            {
                DeviceService.Instance.ToggleFavorite(device);
                RefreshFavorites();
            }
        }

        // --- BOTTOM BAR TIKLAMA VE ANİMASYON OLAYLARI ---

        private async void OnHomeTapped(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
            // Zaten Ana Sayfadayız, o yüzden sayfa değiştirmeye gerek yok.
        }

        private async void OnGridTapped(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
            await Shell.Current.GoToAsync("//AllDevicesPage");
        }

        private async void OnAutomationTapped(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
            await Shell.Current.GoToAsync("//AutomationPage");
        }

        private async void OnSettingsTapped(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
            await Shell.Current.GoToAsync("//SettingsPage");
        }
    }
}
