using System;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    /*
     * RegisterPage.xaml.cs: Kayıt sayfasının arka plan mantığı (code-behind).
     * Bu sınıf yeni kullanıcı kayıt işlemlerini ve navigasyonu yönetir.
     */
    public partial class RegisterPage : ContentPage
    {
        private readonly Services.IAuthService _authService;

        // XAML veya manuel geçişler için parametresiz constructor (DI Fallback)
        public RegisterPage()
        {
            InitializeComponent();
            _authService = Application.Current.Handler.MauiContext.Services.GetRequiredService<Services.IAuthService>();
        }

        public RegisterPage(Services.IAuthService authService)
        {
            InitializeComponent();
            _authService = authService;
        }

        /*
         * Hesap Oluştur Butonu Tıklama Olayı:
         * Formdaki verileri toplar, doğrular ve Firebase üzerinden kayıt işlemi başlatır.
         */
        private async void OnSignUpClicked(object sender, EventArgs e)
        {
            // Hata mesajını sıfırla
            ErrorLabel.IsVisible = false;
            ErrorLabel.Text = "";

            // 1. Validasyon Kontrolleri
            if (string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text))
            {
                ErrorLabel.Text = "Lütfen tüm alanları doldurun.";
                ErrorLabel.IsVisible = true;
                return;
            }

            // [YENİ] Cihaz internete bağlı mı kontrolü
            if (Microsoft.Maui.Networking.Connectivity.Current.NetworkAccess != Microsoft.Maui.Networking.NetworkAccess.Internet)
            {
                ErrorLabel.Text = "🚨 Kayıt Olmak İçin Lütfen İnternet Bağlantınızı Kontrol Edin.";
                ErrorLabel.IsVisible = true;
                return;
            }

            if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
            {
                ErrorLabel.Text = "Şifreler uyuşmuyor!";
                ErrorLabel.IsVisible = true;
                return;
            }

            try 
            {
                // 2. Firebase Kayıt İşlemi
                var request = new Models.RegisterRequest
                {
                    FullName = NameEntry.Text,
                    Email = EmailEntry.Text,
                    Password = PasswordEntry.Text,
                    PhoneNumber = PhoneEntry.Text
                };

                bool success = await _authService.RegisterAsync(request);

                if (success)
                {
                    await DisplayAlert("E-posta Doğrulama", "Lütfen e-posta adresinize gönderilen linke tıklayarak hesabınızı doğrulayın. Ardından giriş yapabilirsiniz.", "Tamam");
                    
                    // Kullanıcıyı login sayfasına geri gönderiyoruz
                    await Navigation.PopAsync();
                }
                else
                {
                    ErrorLabel.Text = "Kayıt işlemi başarısız oldu. Bilgilerinizi kontrol edin.";
                    ErrorLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Sistem Hatası", "Bir hata oluştu: " + ex.Message, "Tamam");
            }
        }

        /*
         * Şifre Görünürlüğünü Değiştirme Olayı:
         * Kullanıcının girdiği şifreyi görmesini veya gizlemesini sağlar.
         */
        private void OnTogglePasswordClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton button && button.CommandParameter is string entryName)
            {
                var entry = entryName == "PasswordEntry" ? PasswordEntry : ConfirmPasswordEntry;
                entry.IsPassword = !entry.IsPassword;
                button.Source = entry.IsPassword ? "hide.png" : "view.png";
            }
        }

        /*
         * Giriş Yap Yazısı Tıklama Olayı:
         * Kullanıcıyı geri giriş (LoginPage) formuna yönlendirir.
         */
        private async void OnSignInTapped(object sender, EventArgs e)
        {
            // Navigation stack üzerinden önceki sayfaya (LoginPage) dönülür.
            await Navigation.PopAsync();
        }
    }
}
