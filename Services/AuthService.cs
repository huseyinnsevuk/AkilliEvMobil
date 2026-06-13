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

                // ADIM 2: Firebase E-posta Doğrulama Linki Gönderme
                try 
                {
                    string idToken = await userCredential.User.GetIdTokenAsync();
                    bool emailSent = await InternalSendEmailVerificationAsync(idToken);
                    if (!emailSent)
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", "Firebase doğrulama e-postası gönderilemedi.", "Tamam");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("E-posta Hatası", $"E-posta gönderme sırasında hata: {ex.Message}", "Tamam");
                    return false;
                }

                // ADIM 3: WhatsApp ile 6 Haneli Doğrulama Kodu Gönderme (Green API)
                try 
                {
                    bool wpSent = await SendVerificationCodeAsync(request.PhoneNumber);
                    if (!wpSent)
                    {
                        await Application.Current.MainPage.DisplayAlert("Hata", "WhatsApp doğrulama kodu gönderilemedi. Lütfen telefon numaranızı kontrol edin.", "Tamam");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("WhatsApp Hatası", $"WhatsApp servisine bağlanılamadı: {ex.Message}", "Tamam");
                    return false;
                }

                // ADIM 4: Lokal / VDS Postgres Veritabanına Kaydet (Admin Paneli İçin)
                try
                {
                    string baseUrl = "http://nart3d.com:3000";
                    var backendPayload = new
                    {
                        fullName = request.FullName,
                        email = request.Email,
                        phoneNumber = request.PhoneNumber,
                        passwordHash = "firebase-handled"
                    };
                    var backendRes = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/users", backendPayload);
                    if (!backendRes.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"Lokal backend kaydı başarısız: {backendRes.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lokal backend bağlantı hatası: {ex.Message}");
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

        public async Task<bool> SendVerificationCodeAsync(string target)
        {
            try 
            {
                _generatedWpCode = new Random().Next(100000, 999999).ToString();

                if (target.Contains("@"))
                {
                    // E-posta ile 6 haneli kod gönderme
                    string baseUrl = "http://nart3d.com:3000";
                    var payload = new { email = target, code = _generatedWpCode };
                    var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/auth/send-email-code", payload);
                    System.Diagnostics.Debug.WriteLine($"EMAIL OTP GONDERILDI: {_generatedWpCode} -> {target} (Durum: {response.StatusCode})");
                    return response.IsSuccessStatusCode;
                }
                else
                {
                    // Telefon Numarası / WhatsApp ile Gönderme (Green API)
                    string idInstance = "7105411368";
                    string apiTokenInstance = "04c359491bde449a8820fc445674cb90d29d3fd0036e4b81a2"; 
                    
                    string cleanNumber = new string(target.Where(char.IsDigit).ToArray());
                    while (cleanNumber.StartsWith("0")) cleanNumber = cleanNumber.Substring(1);
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending OTP: {ex.Message}");
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

        public async Task<bool> IsEmailVerifiedInDbAsync(string email)
        {
            // Test kullanıcısı için otomatik geçiş
            if (email == "huseyin@example.com")
            {
                return true;
            }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                string baseUrl = "http://nart3d.com:3000";
                var response = await client.GetAsync($"{baseUrl}/api/users");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var users = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<System.Text.Json.Nodes.JsonObject>>(content);
                    
                    var user = users?.FirstOrDefault(u => u["email"]?.ToString().Equals(email, StringComparison.OrdinalIgnoreCase) == true);
                    if (user != null)
                    {
                        return user["isEmailVerified"]?.GetValue<bool>() ?? false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Db verification check error: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> IsFirebaseEmailVerifiedAsync()
        {
            if (_firebaseClient.User != null)
            {
                try
                {
                    string idToken = await _firebaseClient.User.GetIdTokenAsync();
                    var url = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={ApiKey}";
                    var payload = new { idToken = idToken };
                    
                    var response = await _httpClient.PostAsJsonAsync(url, payload);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("users", out var usersProp) && usersProp.ValueKind == System.Text.Json.JsonValueKind.Array && usersProp.GetArrayLength() > 0)
                        {
                            var firstUser = usersProp[0];
                            if (firstUser.TryGetProperty("emailVerified", out var verifiedProp))
                            {
                                return verifiedProp.GetBoolean();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking firebase email verified: {ex.Message}");
                }
            }
            return false;
        }

        public string GetCurrentUserPhone()
        {
            // Placeholder: Normalde Firebase Metadata veya Backend'den çekilir.
            return ""; 
        }
    }
}
