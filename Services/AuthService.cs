using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AkilliEvMobil.Models;
using Firebase.Auth;
using Firebase.Auth.Providers;

namespace AkilliEvMobil.Services
{
    /*
     * AuthService: Firebase SDK kullanarak kimlik doğrulama işlemlerini yönetir.
     */
    public class AuthService : IAuthService
    {
        private readonly FirebaseAuthClient _firebaseClient;
        private readonly HttpClient _httpClient;
        private const string ApiKey = "AIzaSyCVRku44269JqVYwEjUbrEdat1RLvltVtI"; // Firebase Console > Proje Ayarları'ndan alın

        public AuthService()
        {
            _httpClient = new HttpClient();
            var config = new FirebaseAuthConfig
            {
                ApiKey = ApiKey,
                AuthDomain = "akillievmobil.firebaseapp.com",
                Providers = new FirebaseAuthProvider[]
                {
                    new EmailProvider()
                }
            };

            _firebaseClient = new FirebaseAuthClient(config);
        }

        public async Task<bool> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // ADIM 1: Firebase Kullanıcı Oluşturma
                Firebase.Auth.UserCredential userCredential;
                try 
                {
                    userCredential = await _firebaseClient.CreateUserWithEmailAndPasswordAsync(request.Email, request.Password, request.FullName);
                }
                catch (FirebaseAuthException ex) 
                {
                    // Şüpheli durum: Hata kodu EmailExists ise veya mesaj EMAIL_EXISTS içeriyorsa
                    if (ex.Reason == AuthErrorReason.EmailExists || ex.Message.Contains("EMAIL_EXISTS"))
                    {
                        try 
                        {
                            var loginResult = await _firebaseClient.SignInWithEmailAndPasswordAsync(request.Email, request.Password);
                            if (loginResult.User != null)
                            {
                                await SendVerificationCodeAsync(request.PhoneNumber);
                                return true; 
                            }
                        }
                        catch 
                        {
                            await Application.Current.MainPage.DisplayAlert("Hata", "Bu e-posta adresi zaten kayıtlı. Lütfen şifrenizi kontrol edin.", "Tamam");
                            return false;
                        }
                    }
                    
                    // Diğer Firebase hataları
                    await Application.Current.MainPage.DisplayAlert("Firebase Hatası", $"Kullanıcı oluşturulamadı: {ex.Reason}\nDetay: {ex.Message}", "Tamam");
                    return false;
                }

                if (userCredential.User == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Kullanıcı oluşturuldu ancak referans alınamadı.", "Tamam");
                    return false;
                }

                // ADIM 2: Email Doğrulaması Gönderme
                try 
                {
                    string idToken = await userCredential.User.GetIdTokenAsync();
                    bool emailSent = await InternalSendEmailVerificationAsync(idToken);
                    if (!emailSent)
                    {
                        await Application.Current.MainPage.DisplayAlert("Uyarı", "Kullanıcı oluşturuldu ancak doğrulama e-postası gönderilemedi.", "Tamam");
                    }
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("E-posta Hatası", $"E-posta gönderme sırasında hata: {ex.Message}", "Tamam");
                }

                // ADIM 3: WhatsApp/OTP Gönderme (Green API)
                try 
                {
                    bool wpSent = await SendVerificationCodeAsync(request.PhoneNumber);
                    if (!wpSent)
                    {
                        await Application.Current.MainPage.DisplayAlert("WhatsApp Hatası", "WhatsApp doğrulama kodu gönderilemedi. Lütfen servis durumunu ve numaranızı kontrol edin.", "Tamam");
                        // WhatsApp gönderilemese bile kullanıcı oluşturulduğu için sürece devam edebiliriz veya false dönebiliriz.
                        // Şimdilik test için false dönelim ki sorunu anlayalım.
                        return false; 
                    }
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Green API Hatası", $"WhatsApp servisine bağlanılamadı: {ex.Message}", "Tamam");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Sistem Hatası", $"Beklenmedik bir hata oluştu: {ex.Message}", "Tamam");
                return false;
            }
        }

        public async Task<bool> LoginAsync(string identifier, string password)
        {
            // [TEST BYPASS] Prisma'da oluşturduğumuz test kullanıcısı için
            if (identifier == "huseyin@example.com" && password == "dummy_hash_for_now")
            {
                return true;
            }

            try
            {
                var userCredential = await _firebaseClient.SignInWithEmailAndPasswordAsync(identifier, password);
                return userCredential.User != null;
            }
            catch (FirebaseAuthException ex)
            {
                // Enum tanınmadığında Asıl API yanıtını (Message) okuyoruz.
                System.Diagnostics.Debug.WriteLine($"LOGIN HATA: {ex.Reason} - {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Giriş Reddedildi", $"Firebase Yanıtı:\n{ex.Message}", "Tamam");
                return false;
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"SISTEM HATA: {ex.Message}");
                return false; 
            }
        }

        public async Task<bool> SendEmailVerificationAsync(string email)
        {
            if (_firebaseClient.User != null)
            {
                string idToken = await _firebaseClient.User.GetIdTokenAsync();
                return await InternalSendEmailVerificationAsync(idToken);
            }
            return false;
        }

        private async Task<bool> InternalSendEmailVerificationAsync(string idToken)
        {
            try 
            {
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={ApiKey}";
                var payload = new { requestType = "VERIFY_EMAIL", idToken = idToken };
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private string? _generatedWpCode;

        public async Task<bool> SendVerificationCodeAsync(string phoneNumber)
        {
            try 
            {
                _generatedWpCode = new Random().Next(100000, 999999).ToString();

                string idInstance = "7105411368";
                string apiTokenInstance = "04c359491bde449a8820fc445674cb90d29d3fd0036e4b81a2"; 
                
                // TELEFON TEMİZLEME MANTIĞI:
                string cleanNumber = new string(phoneNumber.Where(char.IsDigit).ToArray());
                
                // Başındaki 0'ları temizle (Örn: 0506... -> 506...)
                while (cleanNumber.StartsWith("0")) cleanNumber = cleanNumber.Substring(1);
                
                // Eğer numara 5 ile başlıyorsa ve 10 haneliyse başına 90 ekle
                if (cleanNumber.StartsWith("5") && cleanNumber.Length == 10) cleanNumber = "90" + cleanNumber;
                
                string chatId = $"{cleanNumber}@c.us";
                string message = $"*Akıllı Ev Sistemi*\n\nDoğrulama Kodunuz: *{_generatedWpCode}*\n\nLütfen bu kodu kimseyle paylaşmayın.";

                var url = $"https://api.green-api.com/waInstance{idInstance}/sendMessage/{apiTokenInstance}";
                var payload = new { chatId = chatId, message = message };

                var response = await _httpClient.PostAsJsonAsync(url, payload);
                System.Diagnostics.Debug.WriteLine($"WP OTP GONDERILDI: {_generatedWpCode} -> {chatId} (Durum: {response.StatusCode})");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"WP HATA DETAYI: {errorContent}");
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WP Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> VerifyCodeAsync(string code)
        {
            if (string.IsNullOrEmpty(_generatedWpCode)) return false;
            bool isValid = (code == _generatedWpCode);
            if (isValid) _generatedWpCode = null;
            return await Task.FromResult(isValid);
        }

        public async Task<bool> IsUserActiveAsync(string userId)
        {
            // Şimdilik Prisma üzerinden doğrulanmış kabul ediyoruz.
            return true;
        }

        public string GetCurrentUserPhone()
        {
            // Placeholder: Normalde Firebase Metadata veya Backend'den çekilir.
            return ""; 
        }
    }
}
