using System;
using Microsoft.Maui.Controls;

namespace AkilliEvMobil.Views
{
    public partial class AutomationPage : ContentPage
    {
        public AutomationPage()
        {
            InitializeComponent();
        }

        private async void OnHomeTapped(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
            await Shell.Current.GoToAsync("//MainDashboardPage");
        }

        private async void OnGridTapped(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
            await Shell.Current.GoToAsync("//AllDevicesPage");
        }

        private async void OnAutomationTapped(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
            // Zaten bu sayfadayız
        }

        private async void OnSettingsTapped(object sender, EventArgs e)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
            await Shell.Current.GoToAsync("//SettingsPage");
        }
    }
}
