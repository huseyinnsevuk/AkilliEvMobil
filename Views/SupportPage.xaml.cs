using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class SupportPage : ContentPage
    {
        private readonly Services.IAuthService _authService;
        private bool _isSending = false;

        public SupportPage()
        {
            InitializeComponent();
            _authService = Application.Current.Handler.MauiContext.Services.GetRequiredService<Services.IAuthService>();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnSubmitTapped(object sender, EventArgs e)
        {
            if (_isSending) return;

            string subject = SubjectEntry.Text?.Trim();
            string message = MessageEditor.Text?.Trim();

            if (string.IsNullOrEmpty(subject))
            {
                await DisplayAlert("Uyarı", "Lütfen bir konu başlığı yazın.", "Tamam");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                await DisplayAlert("Uyarı", "Lütfen talebinizin detayını açıklayan bir mesaj yazın.", "Tamam");
                return;
            }

            try
            {
                _isSending = true;
                LoadingIndicator.IsRunning = true;

                string email = _authService.GetCurrentUserEmail() ?? "huseyin@example.com";
                string name = _authService.GetCurrentUserDisplayName() ?? Services.DeviceService.Instance.CurrentUserName ?? "Değerli Müşterimiz";

                string baseUrl = "http://141.98.48.101:3000";
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                var payload = new
                {
                    subject = subject,
                    message = message,
                    userEmail = email,
                    userName = name
                };

                var response = await client.PostAsJsonAsync($"{baseUrl}/api/support/tickets", payload);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Başarılı", "Destek talebiniz başarıyla oluşturulmuştur. Yanıt e-posta adresinize gönderilecektir.", "Tamam");
                    
                    // Input alanlarını temizle
                    SubjectEntry.Text = string.Empty;
                    MessageEditor.Text = string.Empty;

                    // Sayfadan çık
                    await Navigation.PopAsync();
                }
                else
                {
                    await DisplayAlert("Hata", "Talebiniz gönderilemedi. Sunucu hatası oluştu.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Bağlantı Hatası", $"Destek talebi gönderilirken bir hata oluştu: {ex.Message}", "Tamam");
            }
            finally
            {
                _isSending = false;
                LoadingIndicator.IsRunning = false;
            }
        }
    }
}
