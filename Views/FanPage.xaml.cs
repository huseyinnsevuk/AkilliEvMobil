namespace AkilliEvMobil.Views;

public partial class FanPage : ContentPage
{
    public FanPage()
    {
        InitializeComponent();
        
        SpeedSlider.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Controls.StripedSlider.Value))
            {
                UpdateSpeedUI(SpeedSlider.Value);
            }
        };
    }

    private void UpdateSpeedUI(double value)
    {
        int percentage = (int)value;
        SpeedLabel.Text = $"{percentage}%";
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnFanToggled(object sender, ToggledEventArgs e)
    {
        bool isOn = e.Value;
        StatusLabel.Text = isOn ? "Şu an açık" : "Şu an kapalı";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }
}
