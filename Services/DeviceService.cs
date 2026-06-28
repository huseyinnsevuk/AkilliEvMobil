using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Net.Http.Json;

namespace AkilliEvMobil.Services
{
    public class SmartDevice : INotifyPropertyChanged
    {
        private bool _isFavorite;
        private bool _isOn;
        private bool _isLocked;

        public string Name { get; set; }
        public string ImageSource { get; set; }
        public string Id { get; set; } // API ile eşleşmesi için id eklendi

        public bool IsOn 
        { 
            get => _isOn; 
            set { _isOn = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(StatusImage)); } 
        }

        public bool IsFavorite 
        { 
            get => _isFavorite; 
            set { _isFavorite = value; OnPropertyChanged(); OnPropertyChanged(nameof(FavoriteIcon)); } 
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(); OnPropertyChanged(nameof(Opacity)); OnPropertyChanged(nameof(LockIconVisible)); }
        }

        public double Opacity => IsLocked ? 0.4 : 1.0;
        public bool LockIconVisible => IsLocked;

        public string StatusColor => IsOn ? "#4A90E2" : "#333333";
        public string StatusImage => IsOn ? "automation_on.png" : "automation_off.png";
        
        // Use Unicode for heart icons: Hollow (♡) and Solid (♥)
        public string FavoriteIcon => IsFavorite ? "fav.png" : "unfav.png";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DeviceService
    {
        private static DeviceService _instance;
        public static DeviceService Instance => _instance ??= new DeviceService();

        public ObservableCollection<SmartDevice> Devices { get; set; }
        public string CurrentUserId { get; private set; } // Aktif kullanıcı ID'si
        public string CurrentUserName { get; private set; } // Aktif kullanıcı adı
        public string CurrentPlan { get; private set; } // Basic veya Premium
        public string CurrentUserAvatar { get; private set; } = "user.png"; // Profil Resmi
        public double PremiumPrice { get; private set; } = 250; // Varsayılan fiyat

        private DeviceService()
        {
            Devices = new ObservableCollection<SmartDevice>
            {
                new SmartDevice { Id = "light", Name = "Aydınlatma", ImageSource = "light.png", IsOn = true, IsFavorite = false },
                new SmartDevice { Id = "fan", Name = "Fan", ImageSource = "fan.png", IsOn = true, IsFavorite = false },
                new SmartDevice { Id = "camera", Name = "Kamera", ImageSource = "cctv.png", IsOn = false, IsFavorite = false },
                new SmartDevice { Id = "tent", Name = "Tente", ImageSource = "tent.png", IsOn = false, IsFavorite = false },
                new SmartDevice { Id = "heater", Name = "Isıtıcı", ImageSource = "heater.png", IsOn = true, IsFavorite = false }
            };
        }

        public async System.Threading.Tasks.Task ToggleFavoriteAsync(SmartDevice device)
        {
            device.IsFavorite = !device.IsFavorite;

            try
            {
                var authService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService<IAuthService>();
                string activeEmail = authService?.GetCurrentUserEmail() ?? "";
                if (!string.IsNullOrEmpty(activeEmail))
                {
                    using var client = new System.Net.Http.HttpClient();
                    client.Timeout = System.TimeSpan.FromSeconds(3);
                    string baseUrl = "http://141.98.48.101:3000";

                    var favoriteDeviceIds = Devices.Where(d => d.IsFavorite).Select(d => d.Id).ToList();
                    var payload = new { favoriteDevices = favoriteDeviceIds };

                    var response = await client.PutAsJsonAsync($"{baseUrl}/api/users/email/{System.Uri.EscapeDataString(activeEmail)}/favorites", payload);
                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to update favorites: {response.StatusCode}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating favorites: {ex.Message}");
            }
        }

        public async System.Threading.Tasks.Task SyncWithBackendAsync()
        {
            try
            {
                var authService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService<IAuthService>();
                string activeEmail = authService?.GetCurrentUserEmail() ?? "huseyin@example.com";
                if (string.IsNullOrEmpty(activeEmail))
                {
                    activeEmail = "huseyin@example.com";
                }

                using var client = new System.Net.Http.HttpClient();
                client.Timeout = System.TimeSpan.FromSeconds(3); // 3 saniyede bağlanamazsa bekleme
                
                string baseUrl = "http://141.98.48.101:3000";

                var usersRes = await client.GetAsync($"{baseUrl}/api/users");
                var settingsRes = await client.GetAsync($"{baseUrl}/api/settings");

                if (usersRes.IsSuccessStatusCode && settingsRes.IsSuccessStatusCode)
                {
                    var usersJson = await usersRes.Content.ReadAsStringAsync();
                    var settingsJson = await settingsRes.Content.ReadAsStringAsync();

                    var users = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<System.Text.Json.Nodes.JsonObject>>(usersJson);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(settingsJson);

                    // Aktif kullanıcıyı bul
                    var currentUser = users?.FirstOrDefault(u => u["email"]?.ToString().Equals(activeEmail, System.StringComparison.OrdinalIgnoreCase) == true);
                    
                    if (currentUser != null && settings != null)
                    {
                        CurrentUserId = currentUser["id"]?.ToString();
                        if (!string.IsNullOrEmpty(CurrentUserId))
                        {
                            Microsoft.Maui.Storage.Preferences.Default.Set("userId", CurrentUserId);
                        }
                        CurrentUserName = currentUser["fullName"]?.ToString() ?? "Değerli Müşterimiz";
                        string plan = currentUser["subscriptionType"]?.ToString() ?? "Free";
                        CurrentPlan = plan;
                        bool isActive = currentUser["isActive"]?.GetValue<bool>() ?? false;
                        PremiumPrice = settings["premiumPrice"]?.GetValue<double>() ?? 250;

                        // Kullanıcının listedeki sırasına göre avatar URL üret (Admin paneli ile eşleşmesi için)
                        int index = users?.FindIndex(u => u["email"]?.ToString().Equals(activeEmail, System.StringComparison.OrdinalIgnoreCase) == true) ?? 0;
                        if (index < 0) index = 0;
                        CurrentUserAvatar = $"https://i.pravatar.cc/150?img={(index % 50) + 1}";

                        // Eğer hesap inaktifse otomatik Basic gibi davran veya tamamen kilitle
                        if (!isActive) plan = "Basic";

                        var basicModulesArray = settings["basicPlanModules"]?.AsArray();
                        var premiumModulesArray = settings["premiumPlanModules"]?.AsArray();

                        var basicModules = basicModulesArray?.Select(x => x.ToString()).ToList() ?? new System.Collections.Generic.List<string>();
                        var premiumModules = premiumModulesArray?.Select(x => x.ToString()).ToList() ?? new System.Collections.Generic.List<string>();

                        var allowedModules = plan == "Premium" ? premiumModules : basicModules;

                        var favoriteDevicesArray = currentUser["favoriteDevices"]?.AsArray();
                        var favoriteDevicesList = favoriteDevicesArray?.Select(x => x.ToString()).ToList() ?? new System.Collections.Generic.List<string>();

                        var lockedModulesArray = currentUser["lockedModules"]?.AsArray();
                        var lockedModulesList = lockedModulesArray?.Select(x => x.ToString()).ToList() ?? new System.Collections.Generic.List<string>();

                        bool favoritesModified = false;
                        var finalFavorites = new System.Collections.Generic.List<string>();

                        foreach (var device in Devices)
                        {
                            device.IsLocked = !allowedModules.Contains(device.Id) || lockedModulesList.Contains(device.Id);
                            bool isFavInDb = favoriteDevicesList.Contains(device.Id);
                            
                            if (device.IsLocked && isFavInDb)
                            {
                                device.IsFavorite = false;
                                favoritesModified = true;
                            }
                            else
                            {
                                device.IsFavorite = isFavInDb;
                            }

                            if (device.IsFavorite)
                            {
                                finalFavorites.Add(device.Id);
                            }
                        }

                        if (favoritesModified)
                        {
                            try
                            {
                                var payload = new { favoriteDevices = finalFavorites };
                                var favUpdateResponse = await client.PutAsJsonAsync($"{baseUrl}/api/users/email/{System.Uri.EscapeDataString(activeEmail)}/favorites", payload);
                                if (!favUpdateResponse.IsSuccessStatusCode)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to sync removed favorites back to DB: {favUpdateResponse.StatusCode}");
                                }
                            }
                            catch (System.Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error updating database favorites during sync: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Backend Sync Error: " + ex.Message);
            }
        }
    }
}

