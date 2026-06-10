using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace AkilliEvMobil.Services
{
    /// <summary>
    /// Acil durum senaryolarında (gaz sızıntısı, yangın, hırsızlık vb.)
    /// telefonda sürekli ses çalınmasını, titreşimi ve ekranın açık kalmasını sağlayan servis.
    /// </summary>
    public class EmergencyService
    {
        private static EmergencyService? _instance;
        public static EmergencyService Instance => _instance ??= new EmergencyService();

        private bool _isAlarmRunning = false;
        private System.Threading.CancellationTokenSource? _vibrationCts;

        private EmergencyService() { }

        /// <summary>
        /// Alarmı başlatır; telefonu sürekli titretir, ekranı uyanık tutar ve kullanıcıya görsel uyarı sunar.
        /// </summary>
        public async Task TriggerEmergencyAlarmAsync(string title, string message)
        {
            if (_isAlarmRunning) return;
            _isAlarmRunning = true;

            // 1. Ekranın kararmasını ve kapanmasını önle (Cihaz uyanık kalsın)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DeviceDisplay.Current.KeepScreenOn = true;
            });

            // 2. Android platformunda yerel Ön Plan Servisi aktifse native siren ve titreşimi tetikle
#if ANDROID
            try
            {
                Platforms.Android.EmergencyForegroundService.Instance?.TriggerEmergencySystem();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to trigger native Android emergency system: {ex.Message}");
            }
#endif

            // 3. Arka planda sürekli titreşim döngüsünü başlat (SOS Ritmi veya sürekli titreşim)
            _vibrationCts = new System.Threading.CancellationTokenSource();
            _ = Task.Run(() => RunVibrationLoop(_vibrationCts.Token));

            // 4. Kullanıcıya tam ekran, premium acil durum uyarısını göster
            await ShowEmergencyModalAsync(title, message);
        }

        /// <summary>
        /// Alarmı ve tüm uyarıları durdurur.
        /// </summary>
        public void StopEmergencyAlarm()
        {
            if (!_isAlarmRunning) return;
            _isAlarmRunning = false;

            // 1. Titreşimi durdur
            _vibrationCts?.Cancel();
            try
            {
                Vibration.Default.Cancel();
            }
            catch { }

#if ANDROID
            // Android platformundaki arka plan servisinin siren ve titreşimini kapat
            try
            {
                Platforms.Android.EmergencyForegroundService.Instance?.StopSirenAndVibration();
            }
            catch { }
#endif

            // 2. Ekranı eski haline getir (kapanabilir)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DeviceDisplay.Current.KeepScreenOn = false;
            });
        }

        /// <summary>
        /// Alarm kapatılana kadar telefonu periyodik olarak titretir.
        /// </summary>
        private async Task RunVibrationLoop(System.Threading.CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _isAlarmRunning)
                {
                    // SOS Titreşim Şablonu: 3 Kısa, 3 Uzun, 3 Kısa
                    // Kısa Titreşimler (300ms titreşim, 200ms bekleme)
                    for (int i = 0; i < 3; i++)
                    {
                        if (token.IsCancellationRequested) return;
                        Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
                        await Task.Delay(500, token);
                    }

                    await Task.Delay(300, token);

                    // Uzun Titreşimler (800ms titreşim, 300ms bekleme)
                    for (int i = 0; i < 3; i++)
                    {
                        if (token.IsCancellationRequested) return;
                        Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(800));
                        await Task.Delay(1100, token);
                    }

                    await Task.Delay(300, token);

                    // Kısa Titreşimler (300ms titreşim, 200ms bekleme)
                    for (int i = 0; i < 3; i++)
                    {
                        if (token.IsCancellationRequested) return;
                        Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
                        await Task.Delay(500, token);
                    }

                    // Bir sonraki SOS döngüsü öncesi 2 saniye sessizlik
                    await Task.Delay(2000, token);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vibration Loop Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Acil durum için premium, çarpıcı bir tam ekran XAML modal sayfası sunar.
        /// </summary>
        private async Task ShowEmergencyModalAsync(string title, string message)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Mevcut aktif sayfayı al
                var activePage = Shell.Current?.CurrentPage;
                if (activePage == null) return;
                
                // SOS Işık Kırpma / Flaşör Efekti ve Haptic Feedback
                try
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);
                }
                catch { }

                // Yeni, premium tam ekran XAML alarm sayfasını modal olarak fırlat
                // Bu sayfa kilit ekranı aşıldığında tam ekran olarak çalar ve parlar
                var alarmPage = new Views.EmergencyAlarmPage(title, message);
                await activePage.Navigation.PushModalAsync(alarmPage);
            });
        }
    }
}
