namespace AkilliEvMobil.Views;

public partial class TentPage : ContentPage
{
    private int _currentOpening = 50;
    private int _currentSpeed = 50; // Varsayılan hız %50

    public TentPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSpeedPresetClicked(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await button.ScaleTo(0.9, 100);
            await button.ScaleTo(1.0, 100);

            UpdatePresetSelection(button);

            string text = button.Text.Replace("%", "");
            if (int.TryParse(text, out int val))
            {
                _currentSpeed = val; // Burası artık "Hız"ı temsil ediyor
                StatusLabel.Text = $"Çalışma hızı: %{_currentSpeed}";
            }
        }
    }

    private void UpdatePresetSelection(Button selectedButton)
    {
        var buttons = new[] { Btn25, Btn50, Btn75, Btn100 };
        
        foreach (var btn in buttons)
        {
            if (btn == selectedButton)
            {
                // Set Selected Gradient
                btn.Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb("#54BDFF"), Offset = 0.0f },
                        new GradientStop { Color = Color.FromArgb("#0042C7"), Offset = 1.0f }
                    }
                };
                btn.TextColor = Colors.White;
            }
            else
            {
                // Reset to Normal
                btn.Background = new SolidColorBrush(Colors.White);
                btn.TextColor = Color.FromArgb("#1E293B");
            }
        }
    }

    private async void OnUpTapped(object sender, EventArgs e)
    {
        if (sender is View view)
        {
            await view.ScaleTo(0.92, 100);
            await view.ScaleTo(1.0, 100);
            
            if (_currentOpening < 100)
            {
                _currentOpening = Math.Min(100, _currentOpening + 10);
                StatusLabel.Text = $"Tente Açıklığı: %{_currentOpening} | Hız: %{_currentSpeed}";
                await SendCommand("tente", _currentOpening, _currentSpeed);
            }
        }
    }

    private async void OnDownTapped(object sender, EventArgs e)
    {
        if (sender is View view)
        {
            await view.ScaleTo(0.92, 100);
            await view.ScaleTo(1.0, 100);
            
            if (_currentOpening > 0)
            {
                _currentOpening = Math.Max(0, _currentOpening - 10);
                StatusLabel.Text = $"Tente Açıklığı: %{_currentOpening} | Hız: %{_currentSpeed}";
                await SendCommand("tente", _currentOpening, _currentSpeed);
            }
        }
    }

    private async Task SendCommand(string deviceType, int position, int speed)
    {
        try
        {
            string baseUrl = "http://nart3d.com:3000";
            using var client = new System.Net.Http.HttpClient();
            
            // Yeni veri formatı: { position: 50, speed: 50 }
            var payload = new { 
                deviceType = deviceType, 
                data = new { position = position, speed = speed } 
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            await client.PostAsync($"{baseUrl}/api/devices/control", content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending command: {ex.Message}");
        }
    }
}
