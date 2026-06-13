using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
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

        private string _email;
        private string _phone;

        public VerifyCodePage(string email, string phone = "")
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            _email = email;
            _phone = phone;
            TargetIdentifierLabel.Text = email;
            _authService = Application.Current.Handler.MauiContext.Services.GetRequiredService<Services.IAuthService>();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (string.IsNullOrEmpty(_phone))
            {
                _phone = await FetchPhoneFromDbAsync(_email);
            }
        }

        private async System.Threading.Tasks.Task<string> FetchPhoneFromDbAsync(string email)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                string baseUrl = "http://nart3d.com:3000";
                var response = await client.GetAsync($"{baseUrl}/api/users");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var users = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<System.Text.Json.Nodes.JsonObject>>(content);
                    
                    var user = users?.FirstOrDefault(u => u["email"]?.ToString().Equals(email, StringComparison.OrdinalIgnoreCase) == true);
                    if (user != null)
                    {
                        return user["phoneNumber"]?.ToString() ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Db phone fetch error: {ex.Message}");
            }
            return "";
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
            // Kutucuklardaki karakterleri birleştir (null değerleri güvenli şekilde boş string'e çevir)
            string code = (Digit1.Text ?? "") + 
                          (Digit2.Text ?? "") + 
                          (Digit3.Text ?? "") + 
                          (Digit4.Text ?? "") + 
                          (Digit5.Text ?? "") + 
                          (Digit6.Text ?? "");
            
            if (code.Length < 6)
            {
                await DisplayAlert("Uyarı", "Lütfen 6 haneli doğrulama kodunu tam girin.", "Tamam");
                return;
            }

            // 1. WhatsApp OTP Kodu Doğrula
            var wpSuccess = await _authService.VerifyCodeAsync(code);
            if (!wpSuccess)
            {
                await DisplayAlert("Hata", "Girdiğiniz WhatsApp doğrulama kodu hatalı veya süresi dolmuş.", "Tamam");
                return;
            }

            // 2. Firebase E-posta Link Doğrulaması Kontrolü
            var emailSuccess = await _authService.IsFirebaseEmailVerifiedAsync();
            if (!emailSuccess)
            {
                await DisplayAlert("E-posta Onaylanmadı 📧", "Telefon numaranız doğrulandı! Ancak devam etmek için lütfen e-posta adresinize gönderilen doğrulama linkine tıklayın ve ardından tekrar buraya gelip Doğrula butonuna basın.", "Tamam");
                return;
            }

            // 3. İki doğrulama da başarılıysa veritabanında durumları güncelle
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                string baseUrl = "http://nart3d.com:3000";
                
                var updatePayload = new { isEmailVerified = true, isPhoneVerified = true };
                var response = await client.PutAsJsonAsync($"{baseUrl}/api/users/email/{Uri.EscapeDataString(_email)}/verify-both", updatePayload);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Lokal backend e-posta/telefon güncelleme hatası: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lokal backend güncelleme bağlantı hatası: {ex.Message}");
            }

            await DisplayAlert("Başarılı", "Hesabınız başarıyla doğrulandı.", "Tamam");
            // Doğrulama başarılıysa ana dashboard'a yönlendirilir.
            Application.Current.MainPage = new AppShell();
        }

        /*
         * Kodu Tekrar Gönder Tıklama Olayı:
         * E-posta servisini tekrar tetikler.
         */
        private async void OnResendCodeTapped(object sender, EventArgs e)
        {
            // 1. Firebase Email Doğrulama Linkini Tekrar Gönder
            var emailSent = await _authService.SendEmailVerificationAsync(_email);
            
            // 2. WhatsApp OTP Kodunu Tekrar Gönder
            bool wpSent = false;
            if (!string.IsNullOrEmpty(_phone))
            {
                wpSent = await _authService.SendVerificationCodeAsync(_phone);
            }
            else
            {
                _phone = await FetchPhoneFromDbAsync(_email);
                if (!string.IsNullOrEmpty(_phone))
                {
                    wpSent = await _authService.SendVerificationCodeAsync(_phone);
                }
            }

            if (emailSent && wpSent)
            {
                await DisplayAlert("Bilgi", "Doğrulama linki e-postanıza ve giriş kodu WhatsApp hattınıza tekrar gönderildi.", "Tamam");
            }
            else if (emailSent)
            {
                await DisplayAlert("Bilgi", "Doğrulama linki e-postanıza tekrar gönderildi, ancak WhatsApp kodu gönderilemedi.", "Tamam");
            }
            else
            {
                await DisplayAlert("Hata", "Doğrulama kodları gönderilemedi. Lütfen bağlantınızı kontrol edin.", "Tamam");
            }
        }

        /*
         * E-postayı Düzenle Tıklama Olayı:
         * Kullanıcının hatalı girdiği e-posta adresini değiştirmesine olanak tanır.
         */
        private async void OnEditPhoneClicked(object sender, EventArgs e)
        {
            string result = await DisplayActionSheet($"Mevcut E-posta: {TargetIdentifierLabel.Text}", "İptal", null, "E-postayı Değiştir");
            
            if (result == "E-postayı Değiştir")
            {
                string newEmail = await DisplayPromptAsync("E-postayı Güncelle", "Yeni e-posta adresinizi girin:", "Güncelle", "İptal", "email@example.com", -1, Keyboard.Email);
                
                if (!string.IsNullOrEmpty(newEmail))
                {
                    TargetIdentifierLabel.Text = newEmail;
                    _email = newEmail;
                    _phone = await FetchPhoneFromDbAsync(newEmail); // Telefonu da güncelle
                    await DisplayAlert("Bilgi", "E-posta güncellendi. Yeni adresinize kod talep edebilirsiniz.", "Tamam");
                }
            }
        }

        /*
         * Otomatik Odak Geçişi (Auto-advance & Auto-backspace):
         * Kullanıcı bir rakam girdiğinde otomatik olarak sonraki kutucuğa odaklanır, sildiğinde ise önceki kutucuğa döner.
         */
        private void OnDigitTextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            if (entry == null) return;

            string newText = e.NewTextValue;

            // Eğer kutucuğa bir karakter girildiyse
            if (!string.IsNullOrEmpty(newText))
            {
                // Sonraki kutucuğa odaklan
                if (entry == Digit1) Digit2.Focus();
                else if (entry == Digit2) Digit3.Focus();
                else if (entry == Digit3) Digit4.Focus();
                else if (entry == Digit4) Digit5.Focus();
                else if (entry == Digit5) Digit6.Focus();
                else if (entry == Digit6)
                {
                    entry.Unfocus(); // Son hanede klavyeyi kapat
                }
            }
            // Eğer kutucuktaki karakter silindiyse
            else
            {
                // Önceki kutucuğa odaklan
                if (entry == Digit6) Digit5.Focus();
                else if (entry == Digit5) Digit4.Focus();
                else if (entry == Digit4) Digit3.Focus();
                else if (entry == Digit3) Digit2.Focus();
                else if (entry == Digit2) Digit1.Focus();
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
