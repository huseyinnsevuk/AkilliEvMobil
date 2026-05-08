using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace AkilliEvMobil.Views
{
    public partial class CustomTabBar : ContentView
    {
        public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
            nameof(ActiveTab), typeof(string), typeof(CustomTabBar), string.Empty, propertyChanged: OnActiveTabChanged);

        public string ActiveTab
        {
            get => (string)GetValue(ActiveTabProperty);
            set => SetValue(ActiveTabProperty, value);
        }

        public CustomTabBar()
        {
            InitializeComponent();
            UpdateActiveTab(ActiveTab); // İlk yüklemede varsayılan sekmeyi tetikle
        }

        private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (CustomTabBar)bindable;
            control.UpdateActiveTab((string)newValue);
        }

        private void UpdateActiveTab(string activeTab)
        {
            // Tüm sekmeleri kapalı (off) haline getir (Beyaz zemin, off resmi)
            HomeBorder.BackgroundColor = Colors.White;
            HomeImage.Source = "home_off.png";
            
            GridBorder.BackgroundColor = Colors.White;
            GridImage.Source = "grid_off.png";
            
            AutomationBorder.BackgroundColor = Colors.White;
            AutomationImage.Source = "automation_off.png";
            
            SettingsBorder.BackgroundColor = Colors.White;
            SettingsImage.Source = "settings_off.png";

            // Seçili olanı aktif (on) haline getir (Mavi zemin, on resmi)
            Color activeColor = Color.FromArgb("#4A90E2");

            switch (activeTab)
            {
                case "Home":
                    HomeBorder.BackgroundColor = activeColor;
                    HomeImage.Source = "home_on.png";
                    break;
                case "Grid":
                    GridBorder.BackgroundColor = activeColor;
                    GridImage.Source = "grid_on.png";
                    break;
                case "Automation":
                    AutomationBorder.BackgroundColor = activeColor;
                    AutomationImage.Source = "automation_on.png";
                    break;
                case "Settings":
                    SettingsBorder.BackgroundColor = activeColor;
                    SettingsImage.Source = "settings_on.png";
                    break;
            }
        }

        private async void OnHomeTapped(object sender, EventArgs e)
        {
            await AnimateClick(sender);
            if (ActiveTab != "Home") await Shell.Current.GoToAsync("//MainDashboardPage");
        }

        private async void OnGridTapped(object sender, EventArgs e)
        {
            await AnimateClick(sender);
            if (ActiveTab != "Grid") await Shell.Current.GoToAsync("//AllDevicesPage");
        }

        private async void OnAutomationTapped(object sender, EventArgs e)
        {
            await AnimateClick(sender);
            if (ActiveTab != "Automation") await Shell.Current.GoToAsync("//AutomationPage");
        }

        private async void OnSettingsTapped(object sender, EventArgs e)
        {
            await AnimateClick(sender);
            if (ActiveTab != "Settings") await Shell.Current.GoToAsync("//SettingsPage");
        }

        private async Task AnimateClick(object sender)
        {
            if (sender is View view)
            {
                await view.ScaleTo(0.8, 100);
                await view.ScaleTo(1.0, 100);
            }
        }
    }
}
