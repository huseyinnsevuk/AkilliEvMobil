using System;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class SubscriptionPage : ContentPage
    {
        private bool _isPolling = false;
        private bool _isOrbiting = false;

        public SubscriptionPage()
        {
            InitializeComponent();

            // Ödeme sonrası deep link dönüşünü dinle
#pragma warning disable CS0618
            MessagingCenter.Subscribe<App>(this, "PaymentSuccess", (sender) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await ShowSuccessScreen();
                });
            });
#pragma warning restore CS0618
        }

        // ─── SAYFA OLAYLARI ───────────────────────────────────────────────
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await Services.DeviceService.Instance.SyncWithBackendAsync();
            UpdateViewStates();

            if (!_isPolling && Services.DeviceService.Instance.CurrentPlan != "Premium")
                StartPaymentPolling();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isPolling = false;
            _isOrbiting = false;
        }

        // ─── POLLING ─────────────────────────────────────────────────────
        private async void StartPaymentPolling()
        {
            _isPolling = true;
            while (_isPolling)
            {
                await Task.Delay(2000);
                await Services.DeviceService.Instance.SyncWithBackendAsync();

                if (Services.DeviceService.Instance.CurrentPlan == "Premium")
                {
                    _isPolling = false;
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await ShowSuccessScreen();
                    });
                    break;
                }
            }
        }

        // ─── BAŞARI EKRANI ───────────────────────────────────────────────
        private async Task ShowSuccessScreen()
        {
            // Önce ekranı göster
            SuccessOverlay.IsVisible = true;
            SuccessOverlay.Opacity = 0;
            await SuccessOverlay.FadeTo(1, 500, Easing.CubicOut);

            // Görseli sıfırla
            SuccessImage.TranslationX = 0;
            SuccessImage.TranslationY = 0;

            // Eliptik orbital animasyonu başlat
            StartOrbitalAnimation();

            // Arka planda veriyi güncelle
            await Services.DeviceService.Instance.SyncWithBackendAsync();
            UpdateViewStates();
        }

        private void StartOrbitalAnimation()
        {
            _isOrbiting = true;
            double radiusX = 18;  // Yatay yarıçap
            double radiusY = 9;   // Dikey yarıçap
            double angle = 0;

            _ = Task.Run(async () =>
            {
                while (_isOrbiting)
                {
                    double radians = angle * Math.PI / 180.0;
                    double tx = radiusX * Math.Cos(radians);
                    double ty = radiusY * Math.Sin(radians);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (SuccessImage != null)
                        {
                            SuccessImage.TranslationX = tx;
                            SuccessImage.TranslationY = ty;
                        }
                    });

                    angle = (angle + 1.5) % 360;
                    await Task.Delay(16); // ~60fps
                }
            });
        }

        // ─── GÖRÜNÜM DURUMU ──────────────────────────────────────────────
        private void UpdateViewStates()
        {
            bool isPremium = Services.DeviceService.Instance.CurrentPlan == "Premium";
            BasicView.IsVisible = !isPremium;
            PremiumView.IsVisible = isPremium;
            PayButtonContainer.IsVisible = !isPremium;
            
            // Fiyatı güncelle
            PriceLabel.Text = $"₺{Services.DeviceService.Instance.PremiumPrice:N2}";
        }

        // ─── BUTON OLAYLARI ──────────────────────────────────────────────
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        // MOK MOD: Stripe devre dışı — direkt başarı ekranı aç
        private async void OnPayButtonTapped(object sender, TappedEventArgs e)
        {
            await ShowSuccessScreen();
        }

        private async void OnGoHomeClicked(object sender, TappedEventArgs e)
        {
            _isOrbiting = false;
            SuccessOverlay.IsVisible = false;

            // Ana sayfaya dön
            await Navigation.PopAsync();
        }

        private async void OnViewInvoiceTapped(object sender, TappedEventArgs e)
        {
            await DisplayAlert("Fatura", "Fatura özelliği yakında aktif edilecek.", "Tamam");
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Abonelik İptali", "Premium avantajlarını kaybetmek istediğinizden emin misiniz?", "Evet, İptal Et", "Vazgeç");

            if (confirm)
            {
                try
                {
                    string userId = Services.DeviceService.Instance.CurrentUserId;
                    string baseUrl = "http://nart3d.com:3000";

                    using var client = new System.Net.Http.HttpClient();
                    var requestBody = new { userId = userId };
                    var response = await client.PostAsync($"{baseUrl}/api/payments/cancel-subscription",
                        new System.Net.Http.StringContent(
                            System.Text.Json.JsonSerializer.Serialize(requestBody),
                            System.Text.Encoding.UTF8,
                            "application/json"));

                    if (response.IsSuccessStatusCode)
                    {
                        await DisplayAlert("İptal Edildi", "Aboneliğiniz sonlandırıldı.", "Tamam");
                        await Services.DeviceService.Instance.SyncWithBackendAsync();
                        UpdateViewStates();
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Hata", ex.Message, "Tamam");
                }
            }
        }

        // Geriye dönük uyumluluk için (polling'den çağrılıyor)
        private async void OnPayClicked(object sender, EventArgs e)
        {
            await ShowSuccessScreen();
        }
    }
}
