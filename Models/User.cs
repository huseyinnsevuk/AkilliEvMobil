using System;

namespace AkilliEvMobil.Models
{
    /*
     * AppUser: Sistemdeki her bir kullanıcıyı temsil eden ana model.
     * Bu model hem mobil uygulama tarafında hem de backend (VPS) tarafında ortak kullanılacaktır.
     */
    public class AppUser
    {
        // Her kullanıcıya atanan benzersiz ID (Donanım eşleşmesi için kritik)
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string FullName { get; set; } = string.Empty;
        
        public string Email { get; set; } = string.Empty;
        
        public string PhoneNumber { get; set; } = string.Empty;
        
        // Şifre güvenliği için (Kaydedilirken hash'lenmiş olmalıdır)
        public string Password { get; set; } = string.Empty;

        // Doğrulama Durumları
        public bool IsEmailVerified { get; set; } = false;
        public bool IsPhoneVerified { get; set; } = false;

        // Üyelik Durumu (Aktif/Pasif) - Stripe ile kontrol edilecek
        public bool IsActive { get; set; } = true;

        // Üyelik Tipi (Örn: "Basic", "Premium")
        public string SubscriptionType { get; set; } = "Basic";

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
