using System;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace AkilliEvMobil.Views
{
    /*
     * LoginPage.xaml.cs: Giriş sayfasının arka plan mantığı (code-behind).
     * Bu sınıf sayfa üzerindeki buton tıklamaları ve yönlendirmeleri yönetir.
     */
    public partial class LoginPage : ContentPage
    {
        // DI Fallback için parametresiz constructor
        public LoginPage()
        {
            InitializeComponent();
        }

        private void OnBackgroundTapped(object sender, EventArgs e)
        {
            EmailEntry.Unfocus();
            PasswordEntry.Unfocus();
        }

        /*
         * Giriş Yap Butonu Tıklama Olayı:
         * Kullanıcı bilgilerini doğrular (İleride API entegrasyonu ile sunucu kontrolü yapılacak).
         */
        private async void OnLoginClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
            {
                await DisplayAlert("Eksik Bilgi", "Lütfen girmek istediğiniz email ve şifreyi doldurunuz.", "Tamam");
                return;
            }

            // [YENİ] Cihaz internete bağlı değilse süreci durdur ve uyar
            if (Microsoft.Maui.Networking.Connectivity.Current.NetworkAccess != Microsoft.Maui.Networking.NetworkAccess.Internet)
            {
                await DisplayAlert("Bağlantı Hatası 📶", "Cihazınızın internet bağlantısı yok. Güvenli giriş işlemi için lütfen aktif bir ağ (Wi-Fi/Mobil Veri) kullanın.", "Tamam");
                return;
            }

            var authService = Application.Current?.Handler?.MauiContext?.Services?.GetRequiredService<Services.IAuthService>();
            if (authService == null)
            {
                await DisplayAlert("Hata", "Kimlik doğrulama servisine ulaşılamadı.", "Tamam");
                return;
            }

            bool success = await authService.LoginAsync(EmailEntry.Text, PasswordEntry.Text);

            if (success)
            {
                // Kullanıcı e-postasını doğrulamış mı?
                bool isEmailVerified = await authService.IsUserActiveAsync(""); // "" geçici, internal fb user'a bakar
                
                // TEST/GELİŞTİRME AŞAMASI: SMS ve Mail Doğrulaması tamamen atlandı.
                // Başarılı girişten hemen sonra ana menüye aktarıyoruz.
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Application.Current.MainPage = new AppShell();
                });
            }
            else
            {
                // AuthService içindeki detaylı hata logu zaten DisplayAlert olarak kullanıcıya yansıtılacak.
                // Burada çift popup çıkmasını önlemek adına işlemi sonlandırıyoruz.
                return;
            }
        }

        /*
         * Kayıt Ol Yazısı Tıklama Olayı:
         * Kullanıcıyı yeni bir sayfa (RegisterPage) formuna yönlendirir.
         */
        private async void OnRegisterTapped(object sender, EventArgs e)
        {
            // DI üzerinden RegisterPage resolve edilir (Dependencies otomatik yüklenir)
            var registerPage = Application.Current?.Handler?.MauiContext?.Services?.GetService<RegisterPage>();
            if (registerPage != null)
            {
                await Navigation.PushAsync(registerPage);
            }
        }

        /*
         * Şifre Görünürlüğünü Değiştirme Olayı:
         * Kullanıcının girdiği şifreyi görmesini veya gizlemesini sağlar.
         */
        private void OnTogglePasswordClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton button)
            {
                PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
                button.Source = PasswordEntry.IsPassword ? "hide.png" : "view.png";
            }
        }

        /*
         * Şifremi Unuttum Yazısı Tıklama Olayı:
         * Şifre sıfırlama işlemlerini başlatır (E-posta veya SMS doğrulaması için).
         */
        private async void OnForgotPasswordTapped(object sender, EventArgs e)
        {
            await DisplayAlert("Bilgi", "Şifre sıfırlama ekranına yönlendiriliyorsunuz.", "Tamam");
        }
    }
}
