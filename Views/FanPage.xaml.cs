namespace AkilliEvMobil.Views;

public partial class FanPage : ContentPage
{
    private System.Timers.Timer _sliderDebounceTimer;
    private double _lastSliderValue;

    public FanPage()
    {
        InitializeComponent();
        
        // Subscribe to slider value changes
        SpeedSlider.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Controls.StripedSlider.Value))
            {
                UpdateSpeedUI(SpeedSlider.Value);
            }
        };

        // Initialize Debounce Timer
        _sliderDebounceTimer = new System.Timers.Timer(150); // 150ms delay to prevent network flood
        _sliderDebounceTimer.AutoReset = false;
        _sliderDebounceTimer.Elapsed += async (s, e) =>
        {
            int speed = (int)_lastSliderValue;
            await SendFanCommandAsync("ON", speed);
        };
    }

    private void UpdateSpeedUI(double value)
    {
        int percentage = (int)value;
        SpeedLabel.Text = $"{percentage}%";

        // Reset debounce timer and store last value
        _lastSliderValue = value;
        _sliderDebounceTimer.Stop();
        _sliderDebounceTimer.Start();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnFanToggled(object sender, ToggledEventArgs e)
    {
        bool isOn = e.Value;
        StatusLabel.Text = isOn ? "Şu an açık" : "Şu an kapalı";

        // Send control command to backend
        await SendFanCommandAsync(isOn ? "ON" : "OFF", (int)SpeedSlider.Value);
    }

    private async System.Threading.Tasks.Task SendFanCommandAsync(string state, int speed)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = System.TimeSpan.FromSeconds(5);
            
            var payload = new
            {
                deviceType = "fan",
                data = new
                {
                    state = state,
                    speed = speed
                }
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            // Backend server address (Direct VDS IP as requested for reliability)
            string baseUrl = "http://141.98.48.101:3000"; 
            var response = await client.PostAsync($"{baseUrl}/api/devices/control", content);
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[Fan] HTTP Error: {response.StatusCode}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Fan] Command successfully sent: {state} (Speed: {speed}%)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Fan] Connection Error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        _sliderDebounceTimer?.Dispose();
        base.OnDisappearing();
    }
}
