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

            // 2. Arka planda sürekli titreşim döngüsünü başlat (SOS Ritmi veya sürekli titreşim)
            _vibrationCts = new System.Threading.CancellationTokenSource();
            _ = Task.Run(() => RunVibrationLoop(_vibrationCts.Token));

            // 3. Kullanıcıya tam ekran, premium acil durum uyarısını göster
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
        /// Acil durum için premium, çarpıcı bir tam ekran modal diyalog sunar.
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

                // 3 parametreli tek butonlu güvenli overload kullanıyoruz.
                // Null değerli 4. parametre Android AlertDialogBuilder tarafında IllegalArgumentException fırlatır.
                await activePage.DisplayAlert(
                    $"🚨 {title.ToUpper()}", 
                    $"{message}\n\nBu uyarı siz kapatana kadar telefonunuzu titretmeye ve ekranı açık tutmaya devam edecektir.", 
                    "SİRENİ SUSTUR VE DİNDİR"
                );

                StopEmergencyAlarm();
                await activePage.DisplayAlert("Siren Susturuldu", "Acil durum uyarısı kullanıcı tarafından doğrulandı ve cihaz sessize alındı. Lütfen ortamı havalandırın veya güvenliği kontrol edin.", "Tamam");
            });
        }
    }
}
