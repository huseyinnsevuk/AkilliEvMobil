using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

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

        public event PropertyChangedEventHandler PropertyChanged;
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

        public void ToggleFavorite(SmartDevice device)
        {
            device.IsFavorite = !device.IsFavorite;
        }

        public async System.Threading.Tasks.Task SyncWithBackendAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = System.TimeSpan.FromSeconds(3); // 3 saniyede bağlanamazsa bekleme
                
                // Fiziksel Android cihazdan (USB Debug) bilgisayardaki Node.js sunucusuna ulaşabilmek için 
                // bilgisayarın o anki Wi-Fi IP adresi gereklidir. (ipconfig'den alınan Güncel IP: 10.49.76.214)
                string baseUrl = Microsoft.Maui.Devices.DeviceInfo.Platform == Microsoft.Maui.Devices.DevicePlatform.Android 
                    ? "http://10.49.76.214:3000" 
                    : "http://localhost:3000";

                var usersRes = await client.GetAsync($"{baseUrl}/api/users");
                var settingsRes = await client.GetAsync($"{baseUrl}/api/settings");

                if (usersRes.IsSuccessStatusCode && settingsRes.IsSuccessStatusCode)
                {
                    var usersJson = await usersRes.Content.ReadAsStringAsync();
                    var settingsJson = await settingsRes.Content.ReadAsStringAsync();

                    var users = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<System.Text.Json.Nodes.JsonObject>>(usersJson);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(settingsJson);

                    // Hüseyin kullanıcısını bul
                    var currentUser = users?.FirstOrDefault(u => u["email"]?.ToString() == "huseyin@example.com");
                    
                    if (currentUser != null && settings != null)
                    {
                        CurrentUserId = currentUser["id"]?.ToString();
                        CurrentUserName = currentUser["fullName"]?.ToString() ?? "Değerli Müşterimiz";
                        string plan = currentUser["subscriptionType"]?.ToString() ?? "Free";
                        CurrentPlan = plan;
                        bool isActive = currentUser["isActive"]?.GetValue<bool>() ?? false;
                        PremiumPrice = settings["premiumPrice"]?.GetValue<double>() ?? 250;

                        // Eğer hesap inaktifse otomatik Basic gibi davran veya tamamen kilitle
                        if (!isActive) plan = "Basic";

                        var basicModulesArray = settings["basicPlanModules"]?.AsArray();
                        var premiumModulesArray = settings["premiumPlanModules"]?.AsArray();

                        var basicModules = basicModulesArray?.Select(x => x.ToString()).ToList() ?? new System.Collections.Generic.List<string>();
                        var premiumModules = premiumModulesArray?.Select(x => x.ToString()).ToList() ?? new System.Collections.Generic.List<string>();

                        var allowedModules = plan == "Premium" ? premiumModules : basicModules;

                        foreach (var device in Devices)
                        {
                            device.IsLocked = !allowedModules.Contains(device.Id);
                            
                            // Eğer cihaz kilitlendiyse ve favorilerdeyse, otomatik olarak favorilerden çıkar
                            if (device.IsLocked && device.IsFavorite)
                            {
                                device.IsFavorite = false;
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
