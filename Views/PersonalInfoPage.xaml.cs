using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class PersonalInfoPage : ContentPage
    {
        private readonly Services.IAuthService _authService;
        private string _email = "";
        private bool _isEmailVerified = false;

        public PersonalInfoPage()
        {
            InitializeComponent();
            _authService = Application.Current.Handler.MauiContext.Services.GetRequiredService<Services.IAuthService>();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadUserInfoAsync();
        }

        private async Task LoadUserInfoAsync()
        {
            try
            {
                // 1. Firebase'den temel verileri al
                _email = _authService.GetCurrentUserEmail();
                string displayName = _authService.GetCurrentUserDisplayName();

                EmailLabel.Text = _email;
                NameLabel.Text = displayName;

                // 2. Firebase üzerinden gerçek e-posta doğrulama durumunu sorgula
                _isEmailVerified = await _authService.IsFirebaseEmailVerifiedAsync();

                // 3. Backend'den telefon numarasını ve diğer bilgileri çek
                string phone = "";
                if (!string.IsNullOrEmpty(_email))
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(3);
                    string baseUrl = "http://141.98.48.101:3000";
                    var response = await client.GetAsync($"{baseUrl}/api/users/email/{Uri.EscapeDataString(_email)}");
                    if (response.IsSuccessStatusCode)
                    {
                        var userJson = await response.Content.ReadFromJsonAsync<JsonObject>();
                        if (userJson != null)
                        {
                            phone = userJson["phoneNumber"]?.ToString() ?? "";
                            string dbName = userJson["fullName"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(dbName))
                            {
                                NameLabel.Text = dbName;
                            }
                        }
                    }
                }

                PhoneLabel.Text = string.IsNullOrEmpty(phone) ? "Eklenmemiş" : phone;

                // 4. E-posta doğrulama durumunu veritabanında da eşitle (eğer Firebase'de doğrulanmışsa)
                if (_isEmailVerified)
                {
                    await SyncEmailVerificationStatusInDbAsync();
                }

                // 5. Arayüzü güncelle
                UpdateEmailStatusUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading user info: {ex.Message}");
            }
        }

        private void UpdateEmailStatusUI()
        {
            if (_isEmailVerified)
            {
                
                EmailStatusText.Text = "E-posta Doğrulandı";
                EmailStatusText.TextColor = Color.FromArgb("#10B981");
                EmailStatusText.TextDecorations = TextDecorations.None;
            }
            else
            {
                
                EmailStatusText.Text = "E-postanız doğrulanmadı (Tekrar göndermek için tıklayın)";
                EmailStatusText.TextColor = Color.FromArgb("#EF4444");
                EmailStatusText.TextDecorations = TextDecorations.Underline;
            }
        }

        private async Task SyncEmailVerificationStatusInDbAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                string baseUrl = "http://141.98.48.101:3000";
                
                var updatePayload = new { isEmailVerified = true };
                var response = await client.PutAsJsonAsync($"{baseUrl}/api/users/email/{Uri.EscapeDataString(_email)}/verify-both", updatePayload);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Lokal backend e-posta durumu senkronizasyon hatası: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lokal backend senkronizasyon bağlantı hatası: {ex.Message}");
            }
        }

        private async void OnVerifyEmailClicked(object sender, EventArgs e)
        {
            if (_isEmailVerified) return; // Zaten doğrulanmışsa işlem yapma

            try
            {
                bool success = await _authService.SendEmailVerificationAsync(_email);
                if (success)
                {
                    await DisplayAlert("Başarılı", "Doğrulama linki e-posta adresinize tekrar gönderildi. Lütfen gelen kutunuzu (veya spam klasörünü) kontrol edin.", "Tamam");
                }
                else
                {
                    await DisplayAlert("Hata", "Doğrulama e-postası gönderilemedi. Lütfen daha sonra tekrar deneyin.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"E-posta gönderimi başarısız oldu: {ex.Message}", "Tamam");
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}

