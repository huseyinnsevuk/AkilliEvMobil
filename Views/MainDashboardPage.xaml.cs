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
        private bool _lastGasDetected = false;

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
            ProfileImage.Source = DeviceService.Instance.CurrentUserAvatar ?? "user.png";
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

        private void StartMockDataLoop()
        {
            _ = Task.Run(async () =>
            {
                var client = Services.DeviceService.Instance.SharedHttpClient;
                string baseUrl = "http://141.98.48.101:3000";

                while (_isMockDataRunning)
                {
                    try
                    {
                    // Hava Durumunu Arka Planda Güncelle (Sensor döngüsünü engellemez)
                    _ = Task.Run(async () =>
                    {
                        try { await UpdateWeatherAsync(); } catch { }
                    });

                    string userId = DeviceService.Instance.CurrentUserId;
                    if (string.IsNullOrEmpty(userId))
                    {
                        // Henüz Sync yapılmadıysa bekle
                        await DeviceService.Instance.SyncWithBackendAsync();
                        userId = DeviceService.Instance.CurrentUserId;
                    }

                        string url = !string.IsNullOrEmpty(userId) 
                            ? $"{baseUrl}/api/users/{userId}/sensors/latest" 
                            : $"{baseUrl}/api/sensors/latest";
                        var response = await client.GetAsync(url);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var log = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(json);

                            if (log != null)
                            {
                                // JSON'dan değerleri güvenli bir şekilde al
                                double temp = log["temperature"]?.GetValue<double>() ?? 0;
                                double humidity = log["humidity"]?.GetValue<double>() ?? 0;
                                bool isRaining = log["isRaining"]?.GetValue<bool>() ?? false;
                                bool gasDetected = log["gasDetected"]?.GetValue<bool>() ?? false;

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    // Sıcaklık ve Nem Güncelleme
                                    TempLabel.Text = temp > 0 ? $"{temp:F1} °C" : "-- °C";
                                    HumidityLabel.Text = humidity > 0 ? $"%{humidity:F0}" : "% --";
                                    
                                    // Yağmur Durumu (Varsa etiketiniz güncellenir)
                                    if (isRaining) {
                                        // Örn: RainLabel.Text = "Yağmur Yağıyor";
                                    }

                                    // Gaz Alarmı Görselleştirme
                                    TempLabel.TextColor = gasDetected ? Colors.Red : Color.FromArgb("#1E293B");
                                    if (gasDetected) {
                                        if (!_lastGasDetected)
                                        {
                                            _lastGasDetected = true;
                                            _ = Task.Run(async () =>
                                            {
                                                await EmergencyService.Instance.TriggerEmergencyAlarmAsync(
                                                    "Tehlikeli Gaz Sızıntısı!",
                                                    "Evinizde tehlikeli düzeyde GAZ SIZINTISI algılandı! Telefon siren çalıyor, ekran uyanık kalacak ve telefonunuz SOS ritminde titreyecektir."
                                                );
                                            });
                                        }
                                    }
                                    else
                                    {
                                        if (_lastGasDetected)
                                        {
                                            _lastGasDetected = false;
                                            EmergencyService.Instance.StopEmergencyAlarm();
                                        }
                                    }
                                });
                            }
                        }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Sensor Loop Error: {ex.Message}");
                }

                await Task.Delay(2000); // 2 saniyede bir güncelle (Yüksek tepki hızı)
                }
            });
        }

        private double _currentLat = 40.76;
        private double _currentLon = 29.92;
        private string _currentCity = "İzmit";

        private async Task UpdateWeatherAsync()
        {
            try
            {
                string baseUrl = "http://141.98.48.101:3000";

                var client = Services.DeviceService.Instance.SharedHttpClient;
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
                    string baseUrl = "http://141.98.48.101:3000";

                    var client = Services.DeviceService.Instance.SharedHttpClient;
                    
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

        private async void OnFavoriteTapped(object sender, EventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is SmartDevice device)
            {
                await DeviceService.Instance.ToggleFavoriteAsync(device);
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

        private async void OnDeviceTapped(object sender, EventArgs e)
        {
            if (sender is Border border && border.BindingContext is SmartDevice device)
            {
                // Noticeable Pop Effect
                await border.ScaleTo(0.9, 100, Easing.CubicOut);
                await border.ScaleTo(1.0, 100, Easing.CubicIn);

                if (device.IsLocked)
                {
                    await DisplayAlert("Kilitli Özellik", "Premium üyelik veya yetki gerektirir.", "Tamam");
                }
                else if (device.Id == "light")
                {
                    await Shell.Current.GoToAsync(nameof(LightingPage));
                }
                else if (device.Id == "camera")
                {
                    await Shell.Current.GoToAsync(nameof(CameraPage));
                }
                else if (device.Id == "tent")
                {
                    await Shell.Current.GoToAsync(nameof(TentPage));
                }
                else if (device.Id == "fan")
                {
                    await Shell.Current.GoToAsync(nameof(FanPage));
                }
                else if (device.Id == "heater")
                {
                    await Shell.Current.GoToAsync(nameof(HeaterPage));
                }
            }
        }
    }
}

