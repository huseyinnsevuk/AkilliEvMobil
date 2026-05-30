using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using AkilliEvMobil.Services;

namespace AkilliEvMobil.Views
{
    public partial class EmergencyAlarmPage : ContentPage
    {
        private bool _isAnimationRunning = false;

        public EmergencyAlarmPage(string title, string message)
        {
            InitializeComponent();
            
            AlarmTitleLabel.Text = title;
            AlarmMessageLabel.Text = message;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            _isAnimationRunning = true;
            _ = StartPulseAnimationsAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isAnimationRunning = false;
        }

        /// <summary>
        /// Arka plandaki parıltı çemberlerini arama efekti gibi büyütüp söndürür (Pulsing Animation).
        /// </summary>
        private async Task StartPulseAnimationsAsync()
        {
            while (_isAnimationRunning)
            {
                try
                {
                    // Halka 1 ve Halka 2'yi sırayla büyüt ve söndür
                    _ = PulseRing1.ScaleTo(1.6, 1200, Easing.CubicOut);
                    _ = PulseRing1.FadeTo(0, 1200, Easing.CubicOut);

                    await Task.Delay(400);

                    _ = PulseRing2.ScaleTo(1.4, 1000, Easing.CubicOut);
                    _ = PulseRing2.FadeTo(0, 1000, Easing.CubicOut);

                    await Task.Delay(1000);

                    // Değerleri sıfırla ve yeniden başlat
                    PulseRing1.Scale = 1.0;
                    PulseRing1.Opacity = 0.2;
                    PulseRing2.Scale = 1.0;
                    PulseRing2.Opacity = 0.3;
                }
                catch
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Kullanıcı alarmı susturma butonuna bastığında çağrılır.
        /// </summary>
        private async void OnSilenceClicked(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                // Hafif buton tıklama animasyonu
                await button.ScaleTo(0.9, 80);
                await button.ScaleTo(1.0, 80);
            }

            // 1. Sireni, titreşimi ve uyanık kalma modunu kapat
            EmergencyService.Instance.StopEmergencyAlarm();

            // 2. Tam ekran modal sayfayı kapat ve ana ekrana dön
            await Navigation.PopModalAsync();
        }
    }
}
