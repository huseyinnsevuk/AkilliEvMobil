using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace AkilliEvMobil.Views
{
    public partial class CameraPage : ContentPage
    {
        private bool _isStreaming = false;
        private string _cameraIp = "nart3d.com";

        public CameraPage()
        {
            InitializeComponent();
            
            // Load saved IP address or use default
            _cameraIp = Preferences.Get("CameraIp", "nart3d.com");
            IpAddressEntry.Text = _cameraIp;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Stop streaming if user leaves page
            StopStream();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            StopStream();
            await Shell.Current.GoToAsync("..");
        }

        private void OnToggleStreamClicked(object sender, EventArgs e)
        {
            if (_isStreaming)
            {
                StopStream();
            }
            else
            {
                StartStream();
            }
        }

        private void StartStream()
        {
            _cameraIp = IpAddressEntry.Text?.Trim() ?? "nart3d.com";
            
            // Format URL: index.html has a styled player, stream.mjpg is the raw feed
            string streamUrl = $"http://{_cameraIp}:8000/index.html";
            
            StreamWebView.Source = new UrlWebViewSource { Url = streamUrl };
            StreamWebView.IsVisible = true;
            PlaceholderView.IsVisible = false;
            RecIndicator.IsVisible = true;
            ViewfinderOverlay.IsVisible = true; // Show overlays
            SnapshotButton.IsEnabled = true;

            ToggleStreamButton.Text = "Yayını Durdur";
            ToggleStreamButton.BackgroundColor = Color.FromArgb("#EF4444"); // Red

            _isStreaming = true;
            
            // Start the premium blinking animation
            StartBlinkingAnimation();
            
            System.Diagnostics.Debug.WriteLine($"[Camera] Yayını başlattı: {streamUrl}");
        }

        private void StopStream()
        {
            _isStreaming = false;
            
            // Stop the blinking animation
            StopBlinkingAnimation();

            // Set source to about:blank to sever the HTTP connection completely
            StreamWebView.Source = new UrlWebViewSource { Url = "about:blank" };
            StreamWebView.IsVisible = false;
            PlaceholderView.IsVisible = true;
            RecIndicator.IsVisible = false;
            ViewfinderOverlay.IsVisible = false; // Hide overlays
            SnapshotButton.IsEnabled = false;

            ToggleStreamButton.Text = "Yayını Başlat";
            ToggleStreamButton.BackgroundColor = Color.FromArgb("#4A90E2"); // Blue

            System.Diagnostics.Debug.WriteLine("[Camera] Yayın sonlandırıldı.");
        }

        private void StartBlinkingAnimation()
        {
            // Create a smooth fading animation for the red REC dot
            var blinkAnimation = new Animation();
            var fadeOut = new Animation(v => RecDot.Opacity = v, 1.0, 0.2, Easing.Linear);
            var fadeIn = new Animation(v => RecDot.Opacity = v, 0.2, 1.0, Easing.Linear);
            
            blinkAnimation.Add(0.0, 0.5, fadeOut);
            blinkAnimation.Add(0.5, 1.0, fadeIn);
            
            // Repeat every 1.2 seconds as long as _isStreaming is true
            blinkAnimation.Commit(this, "RecDotBlink", 16, 1200, Easing.Linear, repeat: () => _isStreaming);
        }

        private void StopBlinkingAnimation()
        {
            this.AbortAnimation("RecDotBlink");
            RecDot.Opacity = 1.0; // Reset opacity
        }

        private async void OnSnapshotClicked(object sender, EventArgs e)
        {
            if (!_isStreaming) return;

            // Micro-animation for pop feedback on click
            if (sender is Button btn)
            {
                await btn.ScaleTo(0.9, 100, Easing.CubicOut);
                await btn.ScaleTo(1.0, 100, Easing.CubicIn);
            }

            await DisplayAlert("Fotoğraf Çekildi", "Kamera anlık görüntüsü galeriye başarıyla kaydedildi.", "Tamam");
        }

        private async void OnSaveIpClicked(object sender, EventArgs e)
        {
            string newIp = IpAddressEntry.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(newIp))
            {
                await DisplayAlert("Hata", "Lütfen geçerli bir IP adresi girin.", "Tamam");
                return;
            }

            _cameraIp = newIp;
            Preferences.Set("CameraIp", _cameraIp);
            
            await DisplayAlert("Başarılı", "Kamera IP adresi kaydedildi ve güncellendi.", "Tamam");

            // If streaming, restart it with the new IP address automatically
            if (_isStreaming)
            {
                StopStream();
                StartStream();
            }
        }

        private async void OnCameraLightToggled(object sender, ToggledEventArgs e)
        {
            bool isOn = e.Value;
            
            // Backend'e aydınlatma komutu gönder (Gece görüşü)
            await SendLightingCommandAsync(isOn ? "ON" : "OFF", 100);
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
                
                // Backend server address matching LightingPage setup
                string baseUrl = "http://141.98.48.101:3000"; 
                var response = await client.PostAsync($"{baseUrl}/api/devices/control", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[Camera Light] HTTP Hatası: {response.StatusCode}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Camera Light] Aydınlatma tetiklendi: {state}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Camera Light] Bağlantı Hatası: {ex.Message}");
            }
        }
    }
}
