using System;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class SettingsPage : ContentPage
    {
        private readonly Services.IAuthService _authService;

        public SettingsPage()
        {
            InitializeComponent();
            _authService = Application.Current.Handler.MauiContext.Services.GetRequiredService<Services.IAuthService>();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Kullanıcı bilgilerini Firebase / AuthService'den dinamik çek
            string email = _authService.GetCurrentUserEmail();
            string displayName = _authService.GetCurrentUserDisplayName();

            UserNameLabel.Text = string.IsNullOrEmpty(displayName) ? "Hüseyin Sevuk" : displayName;
            UserEmailLabel.Text = string.IsNullOrEmpty(email) ? "huseyin@example.com" : email;
            CurrentPlanLabel.Text = (Services.DeviceService.Instance.CurrentPlan ?? "Basic") + " Paket";
        }

        private async void OnPersonalInfoClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new PersonalInfoPage());
        }

        private async void OnManageSubscriptionClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SubscriptionPage());
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Çıkış", "Çıkış yapmak istediğinizden emin misiniz?", "Evet", "Vazgeç");
            if (confirm)
            {
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            }
        }
    }
}
