using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Media;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Maui.Devices;
using Microsoft.Maui.ApplicationModel;
using AndroidX.Core.App;

namespace AkilliEvMobil.Platforms.Android
{
    /// <summary>
    /// Android işletim sistemi üzerinde uygulama kapalı veya uyku modunda olsa dahi
    /// 7/24 gaz sızıntısı takibi yapan ve siren çalıp titreşim tetikleyen Ön Plan Servisi (Foreground Service).
    /// </summary>
    [Service(Name = "com.companyname.akillievmobil.EmergencyForegroundService", Enabled = true, Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
    public class EmergencyForegroundService : Service
    {
        private const int SERVICE_NOTIFICATION_ID = 1001;
        private const int ALARM_NOTIFICATION_ID = 1002;
        private const string CHANNEL_ID = "akilliev_security_channel";
        private const string ALARM_CHANNEL_ID = "akilliev_alarm_channel";
        
        private CancellationTokenSource? _cts;
        private bool _isServiceRunning = false;
        private bool _lastGasState = false;
        private bool _isAlarmAcknowledged = false; // Latch (Kilit): Kullanıcı susturunca gaz temizlenene kadar tekrar çalmayı önler
        
        // Siren ve Titreşim Kaynakları
        private MediaPlayer? _mediaPlayer;
        private Vibrator? _vibrator;
        private bool _isAlarmPlaying = false;

        public static EmergencyForegroundService? Instance { get; private set; }

        public override IBinder? OnBind(Intent? intent) => null;

        [Register("onCreate", "()V", "GetOnCreateHandler")]
        public override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            
            // Titreşim servisini hazırla
            _vibrator = (Vibrator?)GetSystemService(VibratorService);
            
            CreateNotificationChannels();
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            // Kullanıcı alarmı manuel kapatmak için bildirimden butona bastıysa
            if (intent?.Action == "ACTION_SILENCE_ALARM")
            {
                StopSirenAndVibration();
                return StartCommandResult.Sticky;
            }

            if (_isServiceRunning) return StartCommandResult.Sticky;
            _isServiceRunning = true;

            // Ön plan servisi bildirimi oluştur (Durum çubuğunda kalıcı olarak durur ve Android'in servisi kapatmasını önler)
            var notification = CreateServiceNotification();
            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                StartForeground(SERVICE_NOTIFICATION_ID, notification, global::Android.Content.PM.ForegroundService.TypeDataSync);
            }
            else
            {
                StartForeground(SERVICE_NOTIFICATION_ID, notification);
            }

            // Arka plan sorgulama döngüsünü başlat (Her 3 saniyede bir gaz durumunu kontrol eder)
            _cts = new CancellationTokenSource();
            Task.Run(() => PollGasSensorLoopAsync(_cts.Token));

            return StartCommandResult.Sticky;
        }

        private Notification CreateServiceNotification()
        {
            var intent = new Intent(this, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            return new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle("Akıllı Ev Güvenliği Aktif 🛡️")
                .SetContentText("Gaz sızıntısı ve yangın riski arka planda 7/24 taranıyor.")
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .SetCategory(Notification.CategoryService)
                .Build();
        }

        private void CreateNotificationChannels()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var manager = (NotificationManager?)GetSystemService(NotificationService);
                if (manager == null) return;

                // 1. Standart Takip Kanalı (Düşük sesli)
                var serviceChannel = new NotificationChannel(CHANNEL_ID, "Güvenlik Koruma Servisi", NotificationImportance.Low)
                {
                    Description = "Uygulama kapalıyken arka plan güvenlik dinlemesini sürdürmek için kullanılır."
                };
                manager.CreateNotificationChannel(serviceChannel);

                // 2. Kritik Acil Durum Alarm Kanalı (Maksimum önem ve ses)
                var alarmChannel = new NotificationChannel(ALARM_CHANNEL_ID, "ACİL DURUM UYARILARI 🚨", NotificationImportance.High)
                {
                    Description = "Gaz sızıntısı veya kritik tehlikelerde kilit ekranını aşan siren uyarıları için kullanılır."
                };
                alarmChannel.EnableVibration(true);
                alarmChannel.SetBypassDnd(true); // Rahatsız Etme modunu bypass eder

                // Dinamik olarak custom siren sesinin Android kaynağını al ve kanala ata
                int customSoundId = Resources.GetIdentifier("alarm", "raw", PackageName);
                if (customSoundId == 0) customSoundId = Resources.GetIdentifier("siren", "raw", PackageName);

                if (customSoundId != 0)
                {
                    var soundUri = global::Android.Net.Uri.Parse($"android.resource://{PackageName}/{customSoundId}");
                    alarmChannel.SetSound(soundUri, new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Alarm)
                        .SetContentType(AudioContentType.Music)
                        .Build());
                    System.Diagnostics.Debug.WriteLine("📡 Bildirim kanalı için özel alarm/siren sesi atandı.");
                }

                manager.CreateNotificationChannel(alarmChannel);
            }
        }

        private async Task PollGasSensorLoopAsync(CancellationToken token)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            string baseUrl = "http://141.98.48.101:3000";

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var response = await client.GetAsync($"{baseUrl}/api/sensors/latest", token);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(token);
                        var log = JsonSerializer.Deserialize<JsonObject>(json);
                        if (log != null)
                        {
                            bool gasDetected = log["gasDetected"]?.GetValue<bool>() ?? false;

                            if (gasDetected)
                            {
                                if (!_isAlarmAcknowledged && !_lastGasState)
                                {
                                    _lastGasState = true;
                                    TriggerEmergencySystem();
                                }
                            }
                            else
                            {
                                _lastGasState = false;
                                _isAlarmAcknowledged = false; // Gaz temizlendiğinde kilidi sıfırla (Bir sonraki tehlikeye hazır olsun)
                                StopSirenAndVibration();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background Poll Error: {ex.Message}");
                }

                // Her 3 saniyede bir sorgula
                await Task.Delay(3000, token);
            }
        }

        /// <summary>
        /// Gaz sızıntısı anında kilit ekranını aşan siren, kesintisiz titreşim ve tam ekran arama bildirimi tetikler.
        /// </summary>
        public void TriggerEmergencySystem()
        {
            if (_isAlarmPlaying) return;
            _isAlarmPlaying = true;

            // 0. PHYSICAL WAKE LOCK (Ekran tamamen kapalıysa ekranı anında uyandırır ve aydınlatır)
            try
            {
                var powerManager = (PowerManager?)GetSystemService(PowerService);
                if (powerManager != null)
                {
                    // Ekranı tam güçle aç ve tuş kilidi ışıklarını yakıp 15 saniye açık tut
#pragma warning disable CS0618 // Type or member is obsolete
                    var wakeLock = powerManager.NewWakeLock(
                        WakeLockFlags.Full | WakeLockFlags.AcquireCausesWakeup | WakeLockFlags.OnAfterRelease, 
                        "AkilliEvMobil::EmergencyWakeLock"
                    );
#pragma warning restore CS0618
                    wakeLock.Acquire(15000); // 15 saniye sonra serbest bırak
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WakeLock alınamadı: {ex.Message}");
            }

            // 1. NATIVE SIREN SESI (MediaPlayer ile arka planda kesintisiz döngü)
            try
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.SetAudioAttributes(new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Alarm)
                    .SetContentType(AudioContentType.Music)
                    .Build());
                _mediaPlayer.Looping = true;

                bool soundLoaded = false;

                // A. Birinci Yol: Android Native Assets'ten Yerel Önbelleğe Kopyala ve Oynat (MAUI Bağımsız ve Kesin Çözüm!)
                try
                {
                    string cachePath = System.IO.Path.Combine(CacheDir!.AbsolutePath, "alarm.mp3");
                    
                    if (!System.IO.File.Exists(cachePath))
                    {
                        System.IO.Stream? stream = null;
                        try
                        {
                            stream = Assets!.Open("alarm.mp3");
                        }
                        catch
                        {
                            try
                            {
                                stream = Assets!.Open("siren.mp3");
                            }
                            catch { }
                        }

                        if (stream != null)
                        {
                            using (stream)
                            using (var fileStream = System.IO.File.Create(cachePath))
                            {
                                stream.CopyTo(fileStream);
                            }
                        }
                    }

                    if (System.IO.File.Exists(cachePath))
                    {
                        _mediaPlayer.SetDataSource(cachePath);
                        soundLoaded = true;
                        System.Diagnostics.Debug.WriteLine("📡 Özel alarm sesi Android native önbellekten başarıyla yüklendi.");
                    }
                }
                catch (Exception assetEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Android Native Assets alarm yükleme veya kopyalama hatası: {assetEx.Message}");
                }

                // B. İkinci Yol: Android Native Raw Klasöründen Oku (Fallback)
                if (!soundLoaded)
                {
                    int customSoundId = Resources.GetIdentifier("alarm", "raw", PackageName);
                    if (customSoundId == 0) customSoundId = Resources.GetIdentifier("siren", "raw", PackageName);

                    if (customSoundId != 0)
                    {
                        var fd = Resources.OpenRawResourceFd(customSoundId);
                        if (fd != null)
                        {
                            _mediaPlayer.SetDataSource(fd.FileDescriptor, fd.StartOffset, fd.Length);
                            fd.Close();
                            soundLoaded = true;
                            System.Diagnostics.Debug.WriteLine("📡 Siren ses dosyası Android Native Raw klasöründen başarıyla yüklendi.");
                        }
                    }
                }

                // C. Üçüncü Yol (Fallback): Sistem Varsayılan Alarm Sesi
                if (!soundLoaded)
                {
                    var alertUri = RingtoneManager.GetDefaultUri(RingtoneType.Alarm);
                    if (alertUri == null) alertUri = RingtoneManager.GetDefaultUri(RingtoneType.Ringtone);
                    _mediaPlayer.SetDataSource(this, alertUri);
                    System.Diagnostics.Debug.WriteLine("📡 Özel siren sesi bulunamadı, sistem varsayılan alarm sesi yükleniyor.");
                }
                
                _mediaPlayer.Prepare();
                _mediaPlayer.SetVolume(0.3f, 0.3f); // Sesi %30 seviyesine çekiyoruz
                _mediaPlayer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Siren sesi oynatılamadı, yedek sistem sesi deneniyor: {ex.Message}");
                try
                {
                    var alertUri = RingtoneManager.GetDefaultUri(RingtoneType.Alarm) ?? RingtoneManager.GetDefaultUri(RingtoneType.Ringtone);
                    if (alertUri != null)
                    {
                        _mediaPlayer = new MediaPlayer();
                        _mediaPlayer.SetDataSource(this, alertUri);
                        _mediaPlayer.Prepare();
                        _mediaPlayer.SetVolume(0.3f, 0.3f); // Sesi %30 yapıyoruz
                        _mediaPlayer.Start();
                    }
                }
                catch { }
            }

            // 2. NATIVE KESİNTİSİZ SOS TİTREŞİMİ
            try
            {
                if (_vibrator != null && _vibrator.HasVibrator)
                {
                    long[] pattern = { 0, 500, 300, 500, 300, 500, 600, 1000, 400, 1000, 400, 1000, 600, 500, 300, 500, 300, 500, 1500 }; // SOS Ritim
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    {
                        _vibrator.Vibrate(VibrationEffect.CreateWaveform(pattern, 0)); // 0: Döngü yap
                    }
                    else
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        _vibrator.Vibrate(pattern, 0);
#pragma warning restore CS0618
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Titreşim başlatılamadı: {ex.Message}");
            }

            // 3. TAM EKRAN ARAMA BİLDİRİMİ (Heads-Up / Full-Screen Intent - WhatsApp Arama Ekranı Tarzı)
            try
            {
                var mainIntent = new Intent(this, typeof(MainActivity));
                mainIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
                mainIntent.PutExtra("trigger_alarm", "gas");
                
                var pendingIntent = PendingIntent.GetActivity(this, 0, mainIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

                // Bildirim üzerinden susturmak için buton aksiyonu
                var silenceIntent = new Intent(this, typeof(EmergencyForegroundService));
                silenceIntent.SetAction("ACTION_SILENCE_ALARM");
                var silencePendingIntent = PendingIntent.GetService(this, 0, silenceIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

                var alarmNotification = new NotificationCompat.Builder(this, ALARM_CHANNEL_ID)
                    .SetContentTitle("🚨 TEHLİKELİ GAZ SIZINTISI!")
                    .SetContentText("Evinizde yüksek seviyede gaz algılandı! Siren çalıyor.")
                    .SetSmallIcon(global::Android.Resource.Drawable.IcDialogAlert)
                    .SetPriority(NotificationCompat.PriorityMax)
                    .SetCategory(NotificationCompat.CategoryAlarm)
                    .SetFullScreenIntent(pendingIntent, true) // Ekran kilitliyse anında tam ekran uygulmayı fırlatır
                    .SetContentIntent(pendingIntent)
                    .SetOngoing(true)
                    .SetAutoCancel(false)
                    .AddAction(global::Android.Resource.Drawable.IcMenuCloseClearCancel, "SİRENİ SUSTUR VE DİNDİR", silencePendingIntent)
                    .Build();

                var manager = (NotificationManager?)GetSystemService(NotificationService);
                manager?.Notify(ALARM_NOTIFICATION_ID, alarmNotification);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Acil bildirim gönderilemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Çalan sireni ve titreşimi tamamen durdurur, kilit ekranı acil bildirimini temizler.
        /// </summary>
        public void StopSirenAndVibration()
        {
            _isAlarmAcknowledged = true; // Alarmı kullanıcı susturdu, gaz temizlenene kadar tekrar çalmayı engelle
            
            if (!_isAlarmPlaying) return;
            _isAlarmPlaying = false;

            // 1. Sireni durdur
            try
            {
                if (_mediaPlayer != null)
                {
                    if (_mediaPlayer.IsPlaying)
                    {
                        _mediaPlayer.Stop();
                    }
                    _mediaPlayer.Release();
                    _mediaPlayer = null;
                }
            }
            catch { }

            // 2. Titreşimi durdur
            try
            {
                _vibrator?.Cancel();
            }
            catch { }

            // 3. Acil durum bildirimini temizle
            try
            {
                var manager = (NotificationManager?)GetSystemService(NotificationService);
                manager?.Cancel(ALARM_NOTIFICATION_ID);
            }
            catch { }
        }

        public override void OnTaskRemoved(Intent? rootIntent)
        {
            base.OnTaskRemoved(rootIntent);
            
            // Uygulama son uygulamalardan (recents) kapatılsa dahi servisin hayatta kalması veya tekrar anında başlaması için kendini tetikler
            try
            {
                Intent restartServiceIntent = new Intent(ApplicationContext, typeof(EmergencyForegroundService));
                restartServiceIntent.SetPackage(PackageName);
                
                var pendingIntent = PendingIntent.GetService(
                    ApplicationContext, 
                    1, 
                    restartServiceIntent, 
                    PendingIntentFlags.OneShot | PendingIntentFlags.Immutable
                );
                
                var alarmService = (AlarmManager?)GetSystemService(AlarmService);
                if (alarmService != null)
                {
                    // Uygulama kapatıldıktan 1 saniye sonra servisi tekrar canlandır
                    alarmService.Set(
                        AlarmType.ElapsedRealtimeWakeup, 
                        SystemClock.ElapsedRealtime() + 1000, 
                        pendingIntent
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restart service on task removed: {ex.Message}");
            }
        }

        public override void OnDestroy()
        {
            StopSirenAndVibration();
            _cts?.Cancel();
            _isServiceRunning = false;
            Instance = null;
            base.OnDestroy();
        }
    }
}

