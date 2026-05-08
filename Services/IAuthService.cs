using System.Threading.Tasks;
using AkilliEvMobil.Models;

namespace AkilliEvMobil.Services
{
    /*
     * IAuthService: Kimlik doğrulama işlemlerinin soyut tanımı.
     * Bu sayede ileride servis sağlayıcısını (Firebase, Kendi API'niz vb.) kolayca değiştirebiliriz.
     */
    public interface IAuthService
    {
        // Temel Giriş ve Kayıt
        Task<bool> LoginAsync(string identifier, string password);
        Task<bool> RegisterAsync(RegisterRequest request);

        // Doğrulama İşlemleri
        Task<bool> SendEmailVerificationAsync(string email);
        Task<bool> SendVerificationCodeAsync(string phoneNumber);
        
        // Kod Onaylama
        Task<bool> VerifyCodeAsync(string code);

        // Durum Sorgulama
        Task<bool> IsUserActiveAsync(string userId);
        string GetCurrentUserPhone();
    }
}
