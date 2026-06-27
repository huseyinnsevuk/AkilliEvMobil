using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class AutomationPage : ContentPage
    {
        public ObservableCollection<Routine> Routines { get; set; }
        public ObservableCollection<Routine> FilteredRoutines { get; set; }

        public AutomationPage()
        {
            InitializeComponent();
            
            Routines = new ObservableCollection<Routine>
            {
                new Routine { Name = "Karşılama Modu", Description = "Siz eve gelmeden konfor şartlarını hazırlar.", Icon = "arrivals.png", IconBackground = "#4A9EF7" },
                new Routine { Name = "Tasarruf Modu", Description = "Enerji tüketimini minimuma indirir.", Icon = "power_plug.png", IconBackground = "#34C759" },
                new Routine { Name = "Tatil Modu", Description = "Güvenliği maksimize eder ve tam koruma sağlar.", Icon = "luggage.png", IconBackground = "#FF9500" }
            };

            FilteredRoutines = new ObservableCollection<Routine>(Routines);
            BindingContext = this;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = e.NewTextValue?.ToLower() ?? "";
            var filtered = Routines.Where(r => 
                r.Name.ToLower().Contains(searchText) || 
                r.Description.ToLower().Contains(searchText)
            ).ToList();

            FilteredRoutines.Clear();
            foreach (var routine in filtered)
            {
                FilteredRoutines.Add(routine);
            }
        }

        private async void OnRoutineTapped(object sender, EventArgs e)
        {
            if (sender is View view && view.BindingContext is Routine routine)
            {
                // Pop animation
                await view.ScaleTo(0.95, 100);
                await view.ScaleTo(1.0, 100);

                bool targetState = !routine.IsActive;

                // Deactivate all other routines first to ensure "only one active at a time"
                foreach (var r in Routines)
                {
                    r.IsActive = false;
                }

                // Set the state of the tapped routine
                routine.IsActive = targetState;

                if (routine.IsActive)
                {
                    // Trigger the active routine actions
                    await ExecuteRoutineActionsAsync(routine.Name);
                    await DisplayAlert("Rutin Başlatıldı", $"{routine.Name} senaryosu başarıyla çalıştırıldı.", "Tamam");
                }
                else
                {
                    // If turned off, reset/turn off the devices
                    await TurnOffAllDevicesAsync();
                    await DisplayAlert("Rutin Sonlandırıldı", $"{routine.Name} senaryosu kapatıldı.", "Tamam");
                }
            }
        }

        private async System.Threading.Tasks.Task ExecuteRoutineActionsAsync(string routineName)
        {
            if (routineName == "Karşılama Modu")
            {
                // Aydınlatmalar %50 seviyesinde, Fan %70 hızında çalışmaya başlar
                await SendDeviceCommandAsync("aydinlatma", new { state = "ON", brightness = 50 });
                await SendDeviceCommandAsync("fan", new { state = "ON", speed = 70 });
                await SendDeviceCommandAsync("heater", new { state = "OFF" });
            }
            else if (routineName == "Tasarruf Modu")
            {
                // Enerji tüketimini minimuma indirir
                await SendDeviceCommandAsync("aydinlatma", new { state = "ON", brightness = 10 });
                await SendDeviceCommandAsync("fan", new { state = "OFF", speed = 0 });
                await SendDeviceCommandAsync("heater", new { state = "OFF" });
            }
            else if (routineName == "Tatil Modu")
            {
                // Güvenliği maksimize eder ve tam koruma sağlar
                await SendDeviceCommandAsync("aydinlatma", new { state = "OFF", brightness = 0 });
                await SendDeviceCommandAsync("fan", new { state = "OFF", speed = 0 });
                await SendDeviceCommandAsync("heater", new { state = "OFF" });
                await SendDeviceCommandAsync("tente", new { position = 0, speed = 50 });
            }
        }

        private async System.Threading.Tasks.Task TurnOffAllDevicesAsync()
        {
            await SendDeviceCommandAsync("aydinlatma", new { state = "OFF", brightness = 0 });
            await SendDeviceCommandAsync("fan", new { state = "OFF", speed = 0 });
            await SendDeviceCommandAsync("heater", new { state = "OFF" });
        }

        private async System.Threading.Tasks.Task SendDeviceCommandAsync(string deviceType, object data)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = System.TimeSpan.FromSeconds(5);
                
                var payload = new
                {
                    deviceType = deviceType,
                    data = data
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                string baseUrl = "http://141.98.48.101:3000"; 
                var response = await client.PostAsync($"{baseUrl}/api/devices/control", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[Automation] HTTP Hatası ({deviceType}): {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Automation] Cihaz Kontrol Hatası ({deviceType}): {ex.Message}");
            }
        }
    }

    public class Routine : INotifyPropertyChanged
    {
        private bool _isActive;

        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string IconBackground { get; set; } = "#4A9EF7";

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
