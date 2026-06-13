namespace AkilliEvMobil.Views;

public partial class LightingPage : ContentPage
{
    private System.Timers.Timer _sliderDebounceTimer;
    private System.Timers.Timer _sensorPollTimer;
    private double _lastSliderValue;

    public LightingPage()
    {
        InitializeComponent();
        
        // Subscribe to slider value changes
        BrightnessSlider.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Controls.StripedSlider.Value))
            {
                UpdateBrightnessUI(BrightnessSlider.Value);
            }
        };

        // Initialize Debounce Timer for lighting control slider
        _sliderDebounceTimer = new System.Timers.Timer(150); // 150ms delay
        _sliderDebounceTimer.AutoReset = false;
        _sliderDebounceTimer.Elapsed += async (s, e) =>
        {
            int brightness = (int)_lastSliderValue;
            await SendLightingCommandAsync("ON", brightness);
        };

        // Initialize Poll Timer for LDR light sensor readings
        _sensorPollTimer = new System.Timers.Timer(3000); // Poll every 3 seconds
        _sensorPollTimer.AutoReset = true;
        _sensorPollTimer.Elapsed += async (s, e) =>
        {
            await PollSensorDataAsync();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _sensorPollTimer.Start();
        
        // Load the initial value immediately
        await PollSensorDataAsync();
    }

    private void UpdateBrightnessUI(double value)
    {
        int percentage = (int)value;
        BrightnessLabel.Text = $"{percentage}%";
        
        // Update glow opacity based on brightness (No scale animation as requested)
        if (GlowEffect != null)
        {
            GlowEffect.Opacity = (value / 100.0) * 0.3; // Max opacity 0.3
        }

        // Reset timer and store last value
        _lastSliderValue = value;
        _sliderDebounceTimer.Stop();
        _sliderDebounceTimer.Start();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnLightToggled(object sender, ToggledEventArgs e)
    {
        bool isOn = e.Value;
        StatusLabel.Text = isOn ? "Şu an açık" : "Şu an kapalı";
        
        // Direct opacity change instead of FadeTo animation
        if (GlowEffect != null)
        {
            GlowEffect.Opacity = isOn ? (BrightnessSlider.Value / 100.0) * 0.3 : 0;
        }

        // Send command to backend
        await SendLightingCommandAsync(isOn ? "ON" : "OFF", (int)BrightnessSlider.Value);
    }

    private async System.Threading.Tasks.Task PollSensorDataAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = System.TimeSpan.FromSeconds(3);
            
            string baseUrl = "http://141.98.48.101:3000";
            var response = await client.GetAsync($"{baseUrl}/api/sensors/latest");
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                if (root.TryGetProperty("lightLevel", out var lightProp))
                {
                    double lux = lightProp.GetDouble();
                    
                    // Update UI on main thread safely
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LuxLabel.Text = $"{lux:F0} Lux";
                        LuxLabel.FontSize = 18; // Reset size once loaded
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Lighting] Error polling light sensor: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task SendLightingCommandAsync(string state, int brightness)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = System.TimeSpan.FromSeconds(5);
            
            var payload = new
            {
                deviceType = "aydinlatma",
                data = new
                {
                    state = state,
                    brightness = brightness
                }
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            string baseUrl = "http://141.98.48.101:3000"; 
            var response = await client.PostAsync($"{baseUrl}/api/devices/control", content);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[Lighting] HTTP Hatası: {response.StatusCode}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Lighting] Komut başarıyla gönderildi: {state}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Lighting] Bağlantı Hatası: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        _sensorPollTimer?.Stop();
        _sensorPollTimer?.Dispose();
        _sliderDebounceTimer?.Dispose();
        base.OnDisappearing();
    }
}

