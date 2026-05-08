using System;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    /*
     * VerifyCodePage.xaml.cs: SMS veya Email doğrulama kodlarının girildiği sayfanın mantığı.
     * Firebase'den gelen kodun doğrulanması burada tetiklenir.
     */
    public partial class VerifyCodePage : ContentPage
    {
        private readonly Services.IAuthService _authService;

        public VerifyCodePage(string targetIdentifier)
        {
            InitializeComponent();
            TargetIdentifierLabel.Text = targetIdentifier;
            _authService = Application.Current.Handler.MauiContext.Services.GetRequiredService<Services.IAuthService>();
        }

        /*
         * Geri Butonu Tıklama Olayı:
         * Önceki sayfaya (RegisterPage) geri döner.
         */
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        /*
         * Doğrula Butonu Tıklama Olayı:
         * Servis üzerinden girilen kodu doğrular.
         */
        private async void OnVerifyClicked(object sender, EventArgs e)
        {
            // Kutucuklardaki karakterleri birleştir
            string code = Digit1.Text + Digit2.Text + Digit3.Text + Digit4.Text + Digit5.Text + Digit6.Text;
            
            if (code.Length < 6)
            {
                await DisplayAlert("Uyarı", "Lütfen 6 haneli doğrulama kodunu tam girin.", "Tamam");
                return;
            }

            var success = await _authService.VerifyCodeAsync(code);
            
            if (success)
            {
                await DisplayAlert("Başarılı", "Hesabınız başarıyla doğrulandı.", "Tamam");
                // Doğrulama başarılıysa ana dashboard'a yönlendirilir.
                Application.Current.MainPage = new AppShell();
            }
            else
            {
                await DisplayAlert("Hata", "Girdiğiniz kod hatalı veya süresi dolmuş. Lütfen tekrar deneyin.", "Tamam");
            }
        }

        /*
         * Kodu Tekrar Gönder Tıklama Olayı:
         * WhatsApp servisini tekrar tetikler.
         */
        private async void OnResendCodeTapped(object sender, EventArgs e)
        {
            var success = await _authService.SendVerificationCodeAsync(TargetIdentifierLabel.Text);
            if (success)
                await DisplayAlert("Bilgi", "Doğrulama kodu WhatsApp üzerinden tekrar gönderildi.", "Tamam");
            else
                await DisplayAlert("Hata", "Kod gönderilemedi. Lütfen numaranızı kontrol edin.", "Tamam");
        }

        /*
         * Numarayı Düzenle Tıklama Olayı:
         * Kullanıcının hatalı girdiği numarayı değiştirmesine olanak tanır.
         */
        private async void OnEditPhoneClicked(object sender, EventArgs e)
        {
            string result = await DisplayActionSheet($"Mevcut Numara: {TargetIdentifierLabel.Text}", "İptal", null, "Numarayı Değiştir");
            
            if (result == "Numarayı Değiştir")
            {
                string newPhone = await DisplayPromptAsync("Numarayı Güncelle", "Yeni telefon numaranızı girin:", "Güncelle", "İptal", "+90...", 13, Keyboard.Telephone);
                
                if (!string.IsNullOrEmpty(newPhone))
                {
                    TargetIdentifierLabel.Text = newPhone;
                    await DisplayAlert("Bilgi", "Numara güncellendi. Yeni numaranıza kod talep edebilirsiniz.", "Tamam");
                }
            }
        }

        // Sayı giriş simülasyonu (İleride özel keypad ile bağlanacak)
        public void SetDigit(int boxIndex, string value)
        {
            if (boxIndex == 1) Digit1.Text = value;
            if (boxIndex == 2) Digit2.Text = value;
            if (boxIndex == 3) Digit3.Text = value;
            if (boxIndex == 4) Digit4.Text = value;
            if (boxIndex == 5) Digit5.Text = value;
            if (boxIndex == 6) Digit6.Text = value;
        }
    }
}
