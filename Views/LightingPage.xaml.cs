namespace AkilliEvMobil.Views;

public partial class LightingPage : ContentPage
{
    public LightingPage()
    {
        InitializeComponent();
        
        // Subscribe to slider value changes
        BrightnessSlider.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Controls.StripedSlider.Value))
            {
                UpdateBrightnessUI(BrightnessSlider.Value);
            }
        };
    }

    private void UpdateBrightnessUI(double value)
    {
        int percentage = (int)value;
        BrightnessLabel.Text = $"{percentage}%";
        
        // Update glow opacity based on brightness (No scale animation as requested)
        if (GlowEffect != null)
        {
            GlowEffect.Opacity = (value / 100.0) * 0.3; // Max opacity 0.3
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnLightToggled(object sender, ToggledEventArgs e)
    {
        bool isOn = e.Value;
        StatusLabel.Text = isOn ? "Şu an açık" : "Şu an kapalı";
        
        // Direct opacity change instead of FadeTo animation
        if (GlowEffect != null)
        {
            GlowEffect.Opacity = isOn ? (BrightnessSlider.Value / 100.0) * 0.3 : 0;
        }
    }
}
