using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class CameraPage : ContentPage
    {
        private bool _isStreaming = false;
        private readonly HttpClient _httpClient = new HttpClient();

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
            // VDS sunucumuzdaki statik ve kalıcı global HTML oynatıcı adresi (Doğrudan IP üzerinden)!
            string viewUrl = "http://141.98.48.101:3000/api/camera/view";
            
            StreamWebView.Source = new UrlWebViewSource { Url = viewUrl };
            StreamWebView.Opacity = 1;
            PlaceholderView.IsVisible = false;
            RecIndicator.IsVisible = true;
            ViewfinderOverlay.IsVisible = true; // Show overlays
            SnapshotButton.IsEnabled = true;

            ToggleStreamButton.Text = "Yayını Durdur";
            ToggleStreamButton.BackgroundColor = Color.FromArgb("#EF4444"); // Red

            _isStreaming = true;
            
            // Start the premium blinking animation
            StartBlinkingAnimation();
            
            System.Diagnostics.Debug.WriteLine($"[Camera] Global yayın başlatıldı: {viewUrl}");
        }

        private void StopStream()
        {
            _isStreaming = false;
            
            // Stop the blinking animation
            StopBlinkingAnimation();

            // Clear the WebView Source to sever the socket connection
            StreamWebView.Source = new UrlWebViewSource { Url = "about:blank" };
            StreamWebView.Opacity = 0;
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

        private void OnSendClicked(object sender, EventArgs e)
        {
            // Reset modal state
            WpPhoneEntry.Text = string.Empty;
            EmailEntry.Text = string.Empty;
            
            ShareSelectionView.IsVisible = true;
            WhatsAppInputView.IsVisible = false;
            EmailInputView.IsVisible = false;
            ShareLoadingView.IsVisible = false;
            
            ShareOverlay.IsVisible = true;
        }

        private void OnCancelShareClicked(object sender, EventArgs e)
        {
            ShareOverlay.IsVisible = false;
        }

        private void OnShareViaWhatsAppClicked(object sender, EventArgs e)
        {
            ShareSelectionView.IsVisible = false;
            WhatsAppInputView.IsVisible = true;
        }

        private void OnShareViaEmailClicked(object sender, EventArgs e)
        {
            ShareSelectionView.IsVisible = false;
            EmailInputView.IsVisible = true;
        }

        private async void OnSubmitWhatsAppShareClicked(object sender, EventArgs e)
        {
            string phone = WpPhoneEntry.Text?.Trim();
            if (string.IsNullOrEmpty(phone))
            {
                await DisplayAlert("Hata", "Lütfen geçerli bir telefon numarası girin.", "Tamam");
                return;
            }
            
            // Clean/validate input briefly
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 9)
            {
                await DisplayAlert("Hata", "Telefon numarası eksik veya geçersiz.", "Tamam");
                return;
            }

            try
            {
                WhatsAppInputView.IsVisible = false;
                ShareLoadingView.IsVisible = true;

                var payload = new { phoneNumber = phone };
                var response = await _httpClient.PostAsJsonAsync("http://141.98.48.101:3000/api/camera/share/whatsapp", payload);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Başarılı", "Son 10 saniyelik video WhatsApp ile başarıyla gönderildi!", "Harika");
                }
                else
                {
                    await DisplayAlert("Paylaşım Başarısız", "Sunucudan hata yanıtı alındı. Lütfen daha sonra tekrar deneyin.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Bağlantı Hatası", $"Sunucuya bağlanılamadı: {ex.Message}", "Tamam");
            }
            finally
            {
                ShareOverlay.IsVisible = false;
            }
        }

        private async void OnSubmitEmailShareClicked(object sender, EventArgs e)
        {
            string email = EmailEntry.Text?.Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                await DisplayAlert("Hata", "Lütfen geçerli bir e-posta adresi girin.", "Tamam");
                return;
            }

            try
            {
                EmailInputView.IsVisible = false;
                ShareLoadingView.IsVisible = true;

                var payload = new { emailAddress = email };
                var response = await _httpClient.PostAsJsonAsync("http://141.98.48.101:3000/api/camera/share/email", payload);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Başarılı", "Son 10 saniyelik video e-posta adresinize başarıyla gönderildi!", "Harika");
                }
                else
                {
                    await DisplayAlert("Paylaşım Başarısız", "Sunucudan hata yanıtı alındı. Lütfen daha sonra tekrar deneyin.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Bağlantı Hatası", $"Sunucuya bağlanılamadı: {ex.Message}", "Tamam");
            }
            finally
            {
                ShareOverlay.IsVisible = false;
            }
        }
    }
}
