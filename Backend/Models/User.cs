using System;
using System.ComponentModel.DataAnnotations;

namespace AkilliEvBackend.Models
{
    /*
     * User: Veritabanındaki 'Users' tablosuna karşılık gelen sınıf.
     */
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Doğrulama durumları
        public bool IsEmailVerified { get; set; } = false;
        public bool IsPhoneVerified { get; set; } = false;
        
        // Üyelik durumu
        public bool IsActive { get; set; } = true;
        public string SubscriptionType { get; set; } = "Basic";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string RegistrationIp { get; set; } = string.Empty;
    }
}
