namespace AkilliEvMobil
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(Views.LightingPage), typeof(Views.LightingPage));
            Routing.RegisterRoute(nameof(Views.TentPage), typeof(Views.TentPage));
            Routing.RegisterRoute(nameof(Views.FanPage), typeof(Views.FanPage));
            Routing.RegisterRoute(nameof(Views.HeaterPage), typeof(Views.HeaterPage));
            Routing.RegisterRoute(nameof(Views.CameraPage), typeof(Views.CameraPage));
            Routing.RegisterRoute(nameof(Views.PersonalInfoPage), typeof(Views.PersonalInfoPage));
        }
    }
}
