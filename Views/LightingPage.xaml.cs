namespace AkilliEvMobil.Views;

public partial class LightingPage : ContentPage
{
    private System.Timers.Timer _sliderDebounceTimer;
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

        // Initialize Debounce Timer
        _sliderDebounceTimer = new System.Timers.Timer(150); // 150ms gecikme ile gönder
        _sliderDebounceTimer.AutoReset = false;
        _sliderDebounceTimer.Elapsed += async (s, e) =>
        {
            int brightness = (int)_lastSliderValue;
            await SendLightingCommandAsync("ON", brightness);
        };
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

        // Timer'ı sıfırla ve son değeri kaydet
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

        // Backend'e komut gönder
        await SendLightingCommandAsync(isOn ? "ON" : "OFF", (int)BrightnessSlider.Value);
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
            
            // Backend sunucu adresi
            string baseUrl = "http://nart3d.com:3000"; 
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
}
