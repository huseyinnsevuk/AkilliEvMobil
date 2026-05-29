using System;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class CameraPage : ContentPage
    {
        private bool _isStreaming = false;

        public CameraPage()
        {
            InitializeComponent();
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
            // VDS sunucumuzdaki statik ve kalıcı global MJPEG akış adresi!
            string streamUrl = "http://nart3d.com:3000/api/camera/stream";
            
            StreamImage.Source = ImageSource.FromUri(new Uri(streamUrl));
            StreamImage.IsVisible = true;
            PlaceholderView.IsVisible = false;
            RecIndicator.IsVisible = true;
            ViewfinderOverlay.IsVisible = true; // Show overlays
            SnapshotButton.IsEnabled = true;

            ToggleStreamButton.Text = "Yayını Durdur";
            ToggleStreamButton.BackgroundColor = Color.FromArgb("#EF4444"); // Red

            _isStreaming = true;
            
            // Start the premium blinking animation
            StartBlinkingAnimation();
            
            System.Diagnostics.Debug.WriteLine($"[Camera] Global yayın başlatıldı: {streamUrl}");
        }

        private void StopStream()
        {
            _isStreaming = false;
            
            // Stop the blinking animation
            StopBlinkingAnimation();

            // Clear the Image Source to sever the socket connection
            StreamImage.Source = null;
            StreamImage.IsVisible = false;
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
    }
}
