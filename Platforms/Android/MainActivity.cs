using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace AkilliEvMobil
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(new[] { global::Android.Content.Intent.ActionView },
                  DataScheme = "akilliev",
                  DataHost = "payment-success",
                  Categories = new[] { global::Android.Content.Intent.CategoryDefault, global::Android.Content.Intent.CategoryBrowsable })]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Ekran kilitli bile olsa uygulamanın öne fırlayıp ekranı uyandırmasını sağla
            // Bu flags OnCreate'den ve base.OnCreate'den ÖNCE ayarlanmalıdır!
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.OMr1)
                {
                    SetShowWhenLocked(true);
                    SetTurnScreenOn(true);
                    var keyguardManager = (KeyguardManager?)GetSystemService(KeyguardService);
                    keyguardManager?.RequestDismissKeyguard(this, null);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to configure screen flags before OnCreate: {ex.Message}");
            }

            base.OnCreate(savedInstanceState);
            
            // Alternatif ve eski cihaz uyumluluk bayraklarını pencere seviyesinde de ekle
            try
            {
#pragma warning disable CS0618
                Window?.AddFlags(Android.Views.WindowManagerFlags.ShowWhenLocked |
                                 Android.Views.WindowManagerFlags.TurnScreenOn |
                                 Android.Views.WindowManagerFlags.DismissKeyguard |
                                 Android.Views.WindowManagerFlags.KeepScreenOn);
#pragma warning restore CS0618
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to configure screen window flags: {ex.Message}");
            }

            // Android 13+ (API 33+) için bildirim iznini çalışma zamanında iste
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    if (CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
                    {
                        RequestPermissions(new[] { Android.Manifest.Permission.PostNotifications }, 101);
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to request notification permission: {ex.Message}");
            }

            // Pil Tasarrufu Modunun Arka Plan Servisini Kapatmasını Önle (Battery Optimization Bypass)
            try
            {
                var pm = (PowerManager?)GetSystemService(PowerService);
                if (pm != null && !pm.IsIgnoringBatteryOptimizations(PackageName))
                {
                    var ignoreIntent = new Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                    ignoreIntent.SetData(Android.Net.Uri.Parse($"package:{PackageName}"));
                    ignoreIntent.AddFlags(ActivityFlags.NewTask);
                    StartActivity(ignoreIntent);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to request battery optimization ignore: {ex.Message}");
            }

            // 7/24 Arka Plan Güvenlik Koruma Servisini Başlat
            try
            {
                var intent = new Intent(this, typeof(Platforms.Android.EmergencyForegroundService));
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    StartForegroundService(intent);
                }
                else
                {
                    StartService(intent);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start security service: {ex.Message}");
            }
            
            // Soğuk başlangıçta da alarm tetikleyicisini kontrol et
            HandleAlarmIntent(Intent);
        }

        protected override void OnNewIntent(global::Android.Content.Intent? intent)
        {
            base.OnNewIntent(intent);
            Platform.OnNewIntent(intent);
            
            // Sıcak başlangıçta (arka plandan çağrıldığında) kontrol et
            HandleAlarmIntent(intent);
        }

        private void HandleAlarmIntent(global::Android.Content.Intent? intent)
        {
            if (intent?.GetStringExtra("trigger_alarm") == "gas")
            {
                // MAUI katmanındaki alarm ekranını tetikle
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    // Arayüz yerleşimlerinin oturması için çok kısa bir an bekle
                    await System.Threading.Tasks.Task.Delay(500);
                    
                    await Services.EmergencyService.Instance.TriggerEmergencyAlarmAsync(
                        "Tehlikeli Gaz Sızıntısı!",
                        "Evinizde tehlikeli olabilecek düzeyde gaz sızıntısı algılandı! lütfen ortamı havalandırın ve tehlike geçene kadar binayı terk edin."
                    );
                });
            }

            if (intent?.GetStringExtra("open_page") == "camera")
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(500);
                        if (Shell.Current != null)
                        {
                            var currentPage = Shell.Current.CurrentPage;
                            if (currentPage?.GetType().Name != "CameraPage")
                            {
                                await Shell.Current.GoToAsync(nameof(Views.CameraPage));
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to navigate to CameraPage: {ex.Message}");
                    }
                });
            }

            if (intent?.GetStringExtra("open_page") == "tent")
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(500);
                        if (Shell.Current != null)
                        {
                            var currentPage = Shell.Current.CurrentPage;
                            if (currentPage?.GetType().Name != "TentPage")
                            {
                                await Shell.Current.GoToAsync(nameof(Views.TentPage));
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to navigate to TentPage: {ex.Message}");
                    }
                });
            }
        }
    }
}
