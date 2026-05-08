namespace AkilliEvMobil
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new NavigationPage(new Views.LoginPage());
        }

        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);

            if (uri.Host == "payment-success")
            {
                // Ödeme başarılı mesajını yayınla
                MessagingCenter.Send(this, "PaymentSuccess");
            }
        }
    }
}
