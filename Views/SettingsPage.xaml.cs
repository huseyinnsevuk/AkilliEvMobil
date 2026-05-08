using System;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Kullanıcı bilgilerini DeviceService'den çek
            UserNameLabel.Text = Services.DeviceService.Instance.CurrentUserName ?? "Hüseyin Sevuk";
            UserEmailLabel.Text = "huseyin@example.com"; // Gerçekte login bilgisinden çekilmeli
            CurrentPlanLabel.Text = (Services.DeviceService.Instance.CurrentPlan ?? "Basic") + " Paket";
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
