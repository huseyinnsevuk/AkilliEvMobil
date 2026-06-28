using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class SplashPage : ContentPage
    {
        private bool _isAnimationCancelled = false;

        public SplashPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await StartSplashAnimationsAsync();
        }

        private async Task StartSplashAnimationsAsync()
        {
            try
            {
                // 1. Kısa bir başlangıç gecikmesi (Sayfanın ekranda çizilmesi için)
                await Task.Delay(200);

                // 2. Logo Giriş Animasyonu (Ölçeklenme ve Belirme)
                var logoFade = LogoImage.FadeTo(1, 900, Easing.CubicOut);
                var logoScale = LogoImage.ScaleTo(1.0, 900, Easing.CubicOut);
                await Task.WhenAll(logoFade, logoScale);

                if (_isAnimationCancelled) return;

                // 3. Arka plandaki sinyal halkasının sürekli puls/nabız atışını başlat (Arka planda çalışır)
                _ = StartGlowRingPulseLoopAsync();

                // 4. Başlık ve Alt Başlık Animasyonları (Yukarı doğru kayma ve belirme - Staggered)
                var titleSlide = TitleLabel.TranslateTo(0, 0, 750, Easing.CubicOut);
                var titleFade = TitleLabel.FadeTo(1, 750, Easing.CubicOut);
                await Task.WhenAll(titleSlide, titleFade);

                if (_isAnimationCancelled) return;

                await Task.Delay(150); // Alt başlık için kısa bir gecikme (Stagger)

                var subtitleSlide = SubtitleLabel.TranslateTo(0, 0, 750, Easing.CubicOut);
                var subtitleFade = SubtitleLabel.FadeTo(1, 750, Easing.CubicOut);
                await Task.WhenAll(subtitleSlide, subtitleFade);

                if (_isAnimationCancelled) return;

                // 5. Yükleme Spinner ve Bilgilendirme Metninin Belirmesi
                var spinnerFade = LoadingSpinner.FadeTo(1, 400);
                var statusFade = StatusLabel.FadeTo(0.7, 400);
                await Task.WhenAll(spinnerFade, statusFade);

                if (_isAnimationCancelled) return;

                // 6. Taklit edilen yükleme süresi (Örn: Veritabanı bağlantısı, servis başlatmaları)
                await Task.Delay(1800);

                if (_isAnimationCancelled) return;

                // Durum yazısını güncelle
                StatusLabel.Text = "Hazır!";
                await StatusLabel.FadeTo(1, 200);
                await Task.Delay(300);

                // 7. Çıkış Animasyonu (Tüm sayfa içeriğini karartarak yumuşak geçiş yap)
                await this.Content.FadeTo(0, 500, Easing.CubicIn);

                // 8. Giriş Sayfasına Geçiş
                if (Application.Current != null)
                {
                    Application.Current.MainPage = new NavigationPage(new LoginPage());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Splash screen animation failed: {ex.Message}");
                // Hata durumunda doğrudan Login sayfasına yönlendir ki kullanıcı kilitli kalmasın
                if (Application.Current != null)
                {
                    Application.Current.MainPage = new NavigationPage(new LoginPage());
                }
            }
        }

        private async Task StartGlowRingPulseLoopAsync()
        {
            // Sayfa kapatılana kadar veya iptal edilene kadar halkayı canlandır
            while (!_isAnimationCancelled)
            {
                try
                {
                    GlowRing.Scale = 0.8;
                    GlowRing.Opacity = 0.6;

                    // Genişleme ve kaybolma animasyonu
                    var scaleUp = GlowRing.ScaleTo(1.4, 1800, Easing.SinOut);
                    var fadeOut = GlowRing.FadeTo(0, 1800, Easing.SinOut);

                    await Task.WhenAll(scaleUp, fadeOut);
                    
                    // İki dalga arasında kısa bir mola
                    await Task.Delay(300);
                }
                catch
                {
                    break;
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isAnimationCancelled = true; // Arka plandaki döngüleri durdurmak için
        }
    }
}
