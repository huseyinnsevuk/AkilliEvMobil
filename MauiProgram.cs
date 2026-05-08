using Microsoft.Extensions.Logging;
using AkilliEvMobil.Services;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;

namespace AkilliEvMobil
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitMediaElement()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Premium display fontlar
                    fonts.AddFont("StackSansHeadline-Bold.ttf", "StackSansBold");
                    fonts.AddFont("StackSansHeadline-SemiBold.ttf", "StackSansSemiBold");
                });

            // Servis Kayıtları
            builder.Services.AddSingleton<IAuthService, AuthService>();

            // View Models & Pages
            builder.Services.AddTransient<Views.LoginPage>();
            builder.Services.AddTransient<Views.RegisterPage>();
            builder.Services.AddTransient<Views.VerifyCodePage>();
            builder.Services.AddTransient<Views.MainDashboardPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
